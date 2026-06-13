using ASM.Application.Contracts;
using ASM.Application.Interfaces;
using ASM.Domain.Constants;
using ASM.Domain.Entities;
using ASM.Domain.Enums;
using ASM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using TaskWorkflowStatus = ASM.Domain.Enums.TaskStatus;

namespace ASM.Infrastructure.Services;

public class OrderService(
    AppDbContext dbContext,
    ICurrentUserService currentUser) : IOrderService
{
    public async Task<IReadOnlyCollection<InboundOrderDto>> GetInboundOrdersAsync(CancellationToken cancellationToken)
    {
        var orders = await dbContext.InboundOrders
            .Where(x => x.TenantId == currentUser.TenantId)
            .Include(x => x.Warehouse)
            .Include(x => x.AssignedToUser)
            .Include(x => x.Lines)
                .ThenInclude(x => x.Product)
            .Include(x => x.Lines)
                .ThenInclude(x => x.Pallet)
                    .ThenInclude(x => x!.CurrentSlot)
                        .ThenInclude(x => x!.Rack)
                            .ThenInclude(x => x!.Area)
            .Include(x => x.Lines)
                .ThenInclude(x => x.TargetSlot)
                    .ThenInclude(x => x!.Rack)
                        .ThenInclude(x => x!.Area)
            .Include(x => x.TaskAssignment)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return orders.Select(MapInbound).ToList();
    }

    public async Task<InboundOrderDto?> GetInboundOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await dbContext.InboundOrders
            .Where(x => x.Id == orderId && x.TenantId == currentUser.TenantId)
            .Include(x => x.Warehouse)
            .Include(x => x.AssignedToUser)
            .Include(x => x.Lines)
                .ThenInclude(x => x.Product)
            .Include(x => x.Lines)
                .ThenInclude(x => x.Pallet)
                    .ThenInclude(x => x!.CurrentSlot)
                        .ThenInclude(x => x!.Rack)
                            .ThenInclude(x => x!.Area)
            .Include(x => x.Lines)
                .ThenInclude(x => x.TargetSlot)
                    .ThenInclude(x => x!.Rack)
                        .ThenInclude(x => x!.Area)
            .Include(x => x.TaskAssignment)
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null)
        {
            return null;
        }

        if (currentUser.IsInRole(RoleNames.Staff) && order.AssignedToUserId != currentUser.UserId)
        {
            throw new InvalidOperationException("Ban khong co quyen xem lenh nhap nay.");
        }

        return MapInbound(order);
    }

    public async Task<InboundOrderDto> CreateInboundOrderAsync(CreateInboundOrderRequest request, CancellationToken cancellationToken)
    {
        var warehouse = await EnsureTenantEntityAsync<Warehouse>(request.WarehouseId, cancellationToken);
        await EnsureTenantEntityAsync<Product>(request.ProductId, cancellationToken);
        var pallet = await EnsureTenantEntityAsync<Pallet>(request.PalletId, cancellationToken);
        var slot = await LoadSlotWithHierarchyAsync(request.TargetSlotId, cancellationToken);
        await EnsureAssignedStaffAsync(request.AssignedToUserId, cancellationToken);

        if (pallet.Status != PalletStatus.Empty && pallet.InventoryItems.Any())
        {
            throw new InvalidOperationException("Pallet nhap kho phai dang trong.");
        }

        EnsurePalletBelongsToWarehouse(pallet, warehouse.Id);
        EnsureSlotBelongsToWarehouse(slot, warehouse.Id);

        var order = new InboundOrder
        {
            TenantId = currentUser.TenantId,
            WarehouseId = request.WarehouseId,
            CreatedByUserId = currentUser.UserId,
            AssignedToUserId = request.AssignedToUserId,
            ReferenceCode = string.IsNullOrWhiteSpace(request.ReferenceCode)
                ? $"IN-{DateTime.UtcNow:yyyyMMddHHmmss}"
                : request.ReferenceCode,
            Status = OrderStatus.Assigned
        };

        var line = new InboundOrderLine
        {
            TenantId = currentUser.TenantId,
            InboundOrder = order,
            ProductId = request.ProductId,
            PalletId = request.PalletId,
            TargetSlotId = request.TargetSlotId,
            Quantity = request.Quantity
        };

        var task = new TaskAssignment
        {
            TenantId = currentUser.TenantId,
            AssignedToUserId = request.AssignedToUserId,
            TaskType = TaskType.Inbound,
            InboundOrder = order,
            ExpectedTargetType = QrTargetType.Slot,
            ExpectedTargetId = request.TargetSlotId,
            Title = $"Nhap kho {order.ReferenceCode}",
            Instruction = $"Dua pallet {pallet.Code} den {BuildSlotPath(warehouse.Name, slot)} va quet QR vi tri.",
            Status = TaskWorkflowStatus.Pending
        };

        dbContext.InboundOrders.Add(order);
        dbContext.InboundOrderLines.Add(line);
        dbContext.TaskAssignments.Add(task);
        await SaveAuditAsync("CreateInboundOrder", nameof(InboundOrder), order.Id, order.ReferenceCode, cancellationToken);

        return MapInbound(await LoadInboundOrderAsync(order.Id, cancellationToken));
    }

    public async Task CompleteInboundOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await dbContext.InboundOrders
            .Include(x => x.Lines)
            .Include(x => x.TaskAssignment)
            .FirstOrDefaultAsync(x => x.Id == orderId && x.TenantId == currentUser.TenantId, cancellationToken)
            ?? throw new InvalidOperationException("Khong tim thay lenh nhap.");

        EnsureTaskCompletable(order.TaskAssignment);
        if (currentUser.IsInRole(RoleNames.Staff) && order.AssignedToUserId != currentUser.UserId)
        {
            throw new InvalidOperationException("Ban khong duoc phep hoan thanh lenh nay.");
        }

        var line = order.Lines.Single();
        var pallet = await dbContext.Pallets.Include(x => x.InventoryItems).FirstAsync(x => x.Id == line.PalletId, cancellationToken);
        var slot = await dbContext.Slots.FirstAsync(x => x.Id == line.TargetSlotId, cancellationToken);
        var existingItem = pallet.InventoryItems.FirstOrDefault(x => x.ProductId == line.ProductId);

        if (pallet.CurrentSlotId.HasValue && pallet.CurrentSlotId != slot.Id)
        {
            var previousSlot = await dbContext.Slots.FirstOrDefaultAsync(x => x.Id == pallet.CurrentSlotId.Value, cancellationToken);
            if (previousSlot is not null)
            {
                previousSlot.IsOccupied = false;
            }
        }

        pallet.CurrentSlotId = slot.Id;
        pallet.Status = PalletStatus.Occupied;
        slot.IsOccupied = true;

        if (existingItem is null)
        {
            dbContext.InventoryItems.Add(new InventoryItem
            {
                TenantId = currentUser.TenantId,
                ProductId = line.ProductId,
                PalletId = pallet.Id,
                Quantity = line.Quantity
            });
        }
        else
        {
            existingItem.Quantity += line.Quantity;
        }

        order.Status = OrderStatus.Completed;
        order.TaskAssignment!.Status = TaskWorkflowStatus.Completed;
        await SaveAuditAsync("CompleteInboundOrder", nameof(InboundOrder), order.Id, order.ReferenceCode, cancellationToken);
    }

    public async Task<IReadOnlyCollection<OutboundOrderDto>> GetOutboundOrdersAsync(CancellationToken cancellationToken)
    {
        var orders = await dbContext.OutboundOrders
            .Where(x => x.TenantId == currentUser.TenantId)
            .Include(x => x.Warehouse)
            .Include(x => x.AssignedToUser)
            .Include(x => x.Lines)
                .ThenInclude(x => x.Product)
            .Include(x => x.Lines)
                .ThenInclude(x => x.SourcePallet)
                    .ThenInclude(x => x!.CurrentSlot)
                        .ThenInclude(x => x!.Rack)
                            .ThenInclude(x => x!.Area)
            .Include(x => x.Lines)
                .ThenInclude(x => x.SourceSlot)
                    .ThenInclude(x => x!.Rack)
                        .ThenInclude(x => x!.Area)
            .Include(x => x.TaskAssignment)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return orders.Select(MapOutbound).ToList();
    }

    public async Task<OutboundOrderDto?> GetOutboundOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await dbContext.OutboundOrders
            .Where(x => x.Id == orderId && x.TenantId == currentUser.TenantId)
            .Include(x => x.Warehouse)
            .Include(x => x.AssignedToUser)
            .Include(x => x.Lines)
                .ThenInclude(x => x.Product)
            .Include(x => x.Lines)
                .ThenInclude(x => x.SourcePallet)
                    .ThenInclude(x => x!.CurrentSlot)
                        .ThenInclude(x => x!.Rack)
                            .ThenInclude(x => x!.Area)
            .Include(x => x.Lines)
                .ThenInclude(x => x.SourceSlot)
                    .ThenInclude(x => x!.Rack)
                        .ThenInclude(x => x!.Area)
            .Include(x => x.TaskAssignment)
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null)
        {
            return null;
        }

        if (currentUser.IsInRole(RoleNames.Staff) && order.AssignedToUserId != currentUser.UserId)
        {
            throw new InvalidOperationException("Ban khong co quyen xem lenh xuat nay.");
        }

        return MapOutbound(order);
    }

    public async Task<OutboundOrderDto> CreateOutboundOrderAsync(CreateOutboundOrderRequest request, CancellationToken cancellationToken)
    {
        var warehouse = await EnsureTenantEntityAsync<Warehouse>(request.WarehouseId, cancellationToken);
        await EnsureTenantEntityAsync<Product>(request.ProductId, cancellationToken);
        var pallet = await dbContext.Pallets
            .Include(x => x.InventoryItems)
            .Include(x => x.CurrentSlot)
                .ThenInclude(x => x!.Rack)
                    .ThenInclude(x => x!.Area)
            .FirstOrDefaultAsync(x => x.Id == request.SourcePalletId && x.TenantId == currentUser.TenantId, cancellationToken)
            ?? throw new InvalidOperationException("Khong tim thay pallet.");
        var slot = await LoadSlotWithHierarchyAsync(request.SourceSlotId, cancellationToken);
        await EnsureAssignedStaffAsync(request.AssignedToUserId, cancellationToken);

        EnsurePalletBelongsToWarehouse(pallet, warehouse.Id);
        EnsureSlotBelongsToWarehouse(slot, warehouse.Id);

        if (pallet.CurrentSlotId != slot.Id)
        {
            throw new InvalidOperationException("Pallet source does not belong to the selected slot.");
        }

        var inventory = pallet.InventoryItems.FirstOrDefault(x => x.ProductId == request.ProductId)
            ?? throw new InvalidOperationException("Pallet khong co san pham can xuat.");

        if (inventory.Quantity < request.Quantity)
        {
            throw new InvalidOperationException("So luong ton tren pallet khong du.");
        }

        var order = new OutboundOrder
        {
            TenantId = currentUser.TenantId,
            WarehouseId = request.WarehouseId,
            CreatedByUserId = currentUser.UserId,
            AssignedToUserId = request.AssignedToUserId,
            ReferenceCode = string.IsNullOrWhiteSpace(request.ReferenceCode)
                ? $"OUT-{DateTime.UtcNow:yyyyMMddHHmmss}"
                : request.ReferenceCode,
            Status = OrderStatus.Assigned
        };

        var line = new OutboundOrderLine
        {
            TenantId = currentUser.TenantId,
            OutboundOrder = order,
            ProductId = request.ProductId,
            SourcePalletId = request.SourcePalletId,
            SourceSlotId = request.SourceSlotId,
            Quantity = request.Quantity
        };

        var task = new TaskAssignment
        {
            TenantId = currentUser.TenantId,
            AssignedToUserId = request.AssignedToUserId,
            TaskType = TaskType.Outbound,
            OutboundOrder = order,
            ExpectedTargetType = QrTargetType.Pallet,
            ExpectedTargetId = request.SourcePalletId,
            Title = $"Xuat kho {order.ReferenceCode}",
            Instruction = $"Den {BuildSlotPath(warehouse.Name, slot)}, tim pallet {pallet.Code} va quet QR pallet de xac nhan lay hang.",
            Status = TaskWorkflowStatus.Pending
        };

        dbContext.OutboundOrders.Add(order);
        dbContext.OutboundOrderLines.Add(line);
        dbContext.TaskAssignments.Add(task);
        await SaveAuditAsync("CreateOutboundOrder", nameof(OutboundOrder), order.Id, order.ReferenceCode, cancellationToken);

        return MapOutbound(await LoadOutboundOrderAsync(order.Id, cancellationToken));
    }

    public async Task CompleteOutboundOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await dbContext.OutboundOrders
            .Include(x => x.Lines)
            .Include(x => x.TaskAssignment)
            .FirstOrDefaultAsync(x => x.Id == orderId && x.TenantId == currentUser.TenantId, cancellationToken)
            ?? throw new InvalidOperationException("Khong tim thay lenh xuat.");

        EnsureTaskCompletable(order.TaskAssignment);
        if (currentUser.IsInRole(RoleNames.Staff) && order.AssignedToUserId != currentUser.UserId)
        {
            throw new InvalidOperationException("Ban khong duoc phep hoan thanh lenh nay.");
        }

        var line = order.Lines.Single();
        var pallet = await dbContext.Pallets
            .Include(x => x.InventoryItems)
            .FirstAsync(x => x.Id == line.SourcePalletId, cancellationToken);
        var inventory = pallet.InventoryItems.FirstOrDefault(x => x.ProductId == line.ProductId)
            ?? throw new InvalidOperationException("Khong con ton kho tren pallet.");

        if (inventory.Quantity < line.Quantity)
        {
            throw new InvalidOperationException("So luong ton khong du de xuat.");
        }

        inventory.Quantity -= line.Quantity;
        if (inventory.Quantity == 0)
        {
            dbContext.InventoryItems.Remove(inventory);
        }

        pallet.Status = pallet.InventoryItems.Any(x => x != inventory ? x.Quantity > 0 : inventory.Quantity > 0)
            ? PalletStatus.Occupied
            : PalletStatus.Empty;

        order.Status = OrderStatus.Completed;
        order.TaskAssignment!.Status = TaskWorkflowStatus.Completed;
        await SaveAuditAsync("CompleteOutboundOrder", nameof(OutboundOrder), order.Id, order.ReferenceCode, cancellationToken);
    }

    public async Task<IReadOnlyCollection<TaskDto>> GetMyTasksAsync(CancellationToken cancellationToken)
    {
        var query = dbContext.TaskAssignments
            .Where(x => x.TenantId == currentUser.TenantId)
            .Include(x => x.InboundOrder)
            .Include(x => x.OutboundOrder)
            .AsQueryable();

        if (currentUser.IsInRole(RoleNames.Staff))
        {
            query = query.Where(x => x.AssignedToUserId == currentUser.UserId);
        }

        var tasks = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return tasks.Select(MapTask).ToList();
    }

    public async Task<TaskDto?> GetTaskAsync(Guid taskId, CancellationToken cancellationToken)
    {
        var task = await dbContext.TaskAssignments
            .Include(x => x.InboundOrder)
            .Include(x => x.OutboundOrder)
            .FirstOrDefaultAsync(x => x.Id == taskId && x.TenantId == currentUser.TenantId, cancellationToken);

        if (task is null)
        {
            return null;
        }

        if (currentUser.IsInRole(RoleNames.Staff) && task.AssignedToUserId != currentUser.UserId)
        {
            throw new InvalidOperationException("Ban khong co quyen xem task nay.");
        }

        return MapTask(task);
    }

    public async Task<ScanVerifyResultDto> VerifyScanAsync(ScanVerifyRequest request, CancellationToken cancellationToken)
    {
        var task = await dbContext.TaskAssignments
            .Include(x => x.ScanLogs)
            .FirstOrDefaultAsync(x => x.Id == request.TaskAssignmentId && x.TenantId == currentUser.TenantId, cancellationToken)
            ?? throw new InvalidOperationException("Khong tim thay task.");

        if (currentUser.IsInRole(RoleNames.Staff) && task.AssignedToUserId != currentUser.UserId)
        {
            throw new InvalidOperationException("Ban khong co quyen thuc hien task nay.");
        }

        var qr = await dbContext.QrCodes.FirstOrDefaultAsync(
            x => x.TenantId == currentUser.TenantId && x.Payload == request.Payload,
            cancellationToken);

        var isValid = qr is not null &&
                      qr.TargetType == task.ExpectedTargetType &&
                      qr.TargetId == task.ExpectedTargetId;
        var message = isValid
            ? "Quet dung doi tuong yeu cau."
            : "QR khong khop voi doi tuong yeu cau.";

        task.Status = isValid ? TaskWorkflowStatus.Verified : TaskWorkflowStatus.Failed;
        task.LastVerifiedAtUtc = isValid ? DateTime.UtcNow : task.LastVerifiedAtUtc;

        dbContext.ScanLogs.Add(new ScanLog
        {
            TenantId = currentUser.TenantId,
            TaskAssignmentId = task.Id,
            ScannedByUserId = currentUser.UserId,
            Payload = request.Payload,
            IsSuccess = isValid,
            Message = message
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return new ScanVerifyResultDto(isValid, message, DateTime.UtcNow);
    }

    public async Task<TaskStepVerifyResultDto> VerifyTaskStepAsync(TaskStepVerifyRequest request, CancellationToken cancellationToken)
    {
        var task = await dbContext.TaskAssignments
            .Include(x => x.ScanLogs)
            .FirstOrDefaultAsync(x => x.Id == request.TaskAssignmentId && x.TenantId == currentUser.TenantId, cancellationToken)
            ?? throw new InvalidOperationException("Khong tim thay task.");

        if (currentUser.IsInRole(RoleNames.Staff) && task.AssignedToUserId != currentUser.UserId)
        {
            throw new InvalidOperationException("Ban khong co quyen thuc hien task nay.");
        }

        var requestedStepKey = request.StepKey.Trim().ToLowerInvariant();
        var requiredStepKey = GetRequiredScanStepOrder(task.TaskType)
            .FirstOrDefault(step => !GetCompletedScanSteps(task).Contains(step));

        if (requiredStepKey is null)
        {
            return new TaskStepVerifyResultDto(
                false,
                requestedStepKey,
                "Tat ca buoc quet QR da hoan thanh cho task nay.",
                null,
                false,
                DateTime.UtcNow);
        }

        if (!string.Equals(requestedStepKey, requiredStepKey, StringComparison.Ordinal))
        {
            var outOfOrderMessage = $"Sai thu tu xac nhan. Hay hoan thanh buoc {requiredStepKey} truoc.";
            dbContext.ScanLogs.Add(new ScanLog
            {
                TenantId = currentUser.TenantId,
                TaskAssignmentId = task.Id,
                ScannedByUserId = currentUser.UserId,
                Payload = request.Payload,
                IsSuccess = false,
                Message = $"{requestedStepKey}: {outOfOrderMessage}"
            });

            await dbContext.SaveChangesAsync(cancellationToken);
            return new TaskStepVerifyResultDto(
                false,
                requestedStepKey,
                outOfOrderMessage,
                requiredStepKey,
                false,
                DateTime.UtcNow);
        }

        var expectation = await ResolveStepExpectationAsync(task, requestedStepKey, cancellationToken);
        var qr = await dbContext.QrCodes.FirstOrDefaultAsync(
            x => x.TenantId == currentUser.TenantId && x.Payload == request.Payload,
            cancellationToken);

        var isValid = qr is not null &&
                      qr.TargetType == expectation.TargetType &&
                      qr.TargetId == expectation.TargetId;
        var message = isValid ? expectation.SuccessMessage : expectation.FailureMessage;

        if (expectation.MarksTaskVerified)
        {
            task.Status = isValid ? TaskWorkflowStatus.Verified : TaskWorkflowStatus.Failed;
            task.LastVerifiedAtUtc = isValid ? DateTime.UtcNow : task.LastVerifiedAtUtc;
        }

        dbContext.ScanLogs.Add(new ScanLog
        {
            TenantId = currentUser.TenantId,
            TaskAssignmentId = task.Id,
            ScannedByUserId = currentUser.UserId,
            Payload = request.Payload,
            IsSuccess = isValid,
            Message = $"{expectation.StepKey}: {message}"
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return new TaskStepVerifyResultDto(
            isValid,
            expectation.StepKey,
            message,
            expectation.NextStepKey,
            expectation.MarksTaskVerified && isValid,
            DateTime.UtcNow);
    }

    private async Task<T> EnsureTenantEntityAsync<T>(Guid id, CancellationToken cancellationToken) where T : TenantEntity
    {
        var entity = await dbContext.Set<T>().FirstOrDefaultAsync(
            x => x.Id == id && x.TenantId == currentUser.TenantId,
            cancellationToken);
        return entity ?? throw new InvalidOperationException($"Khong tim thay {typeof(T).Name}.");
    }

    private async Task<AppUser> EnsureAssignedStaffAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(
            x => x.Id == userId && x.TenantId == currentUser.TenantId && x.IsActive,
            cancellationToken)
            ?? throw new InvalidOperationException("Khong tim thay nhan vien.");

        return user;
    }

    private async Task<StepExpectation> ResolveStepExpectationAsync(TaskAssignment task, string stepKey, CancellationToken cancellationToken)
    {
        var normalizedStep = stepKey.Trim().ToLowerInvariant();

        if (task.TaskType == TaskType.Inbound)
        {
            var order = await dbContext.InboundOrders
                .Include(x => x.Warehouse)
                .Include(x => x.Lines)
                    .ThenInclude(x => x.Product)
                .Include(x => x.Lines)
                    .ThenInclude(x => x.Pallet)
                        .ThenInclude(x => x!.CurrentSlot)
                            .ThenInclude(x => x!.Rack)
                                .ThenInclude(x => x!.Area)
                .Include(x => x.Lines)
                    .ThenInclude(x => x.TargetSlot)
                        .ThenInclude(x => x!.Rack)
                            .ThenInclude(x => x!.Area)
                .FirstOrDefaultAsync(x => x.Id == task.InboundOrderId && x.TenantId == currentUser.TenantId, cancellationToken)
                ?? throw new InvalidOperationException("Inbound order was not found.");

            var line = order.Lines.Single();
            var targetPath = BuildSlotPath(order.Warehouse?.Name ?? string.Empty, line.TargetSlot);

            return normalizedStep switch
            {
                "product" => new StepExpectation(
                    "product",
                    QrTargetType.Product,
                    line.ProductId,
                    $"Dung san pham {line.Product?.Name}. Tiep tuc quet pallet {line.Pallet?.Code}.",
                    $"Sai san pham. Hay quet QR cua {line.Product?.Name}.",
                    "pallet",
                    false),
                "pallet" => new StepExpectation(
                    "pallet",
                    QrTargetType.Pallet,
                    line.PalletId,
                    $"Correct pallet {line.Pallet?.Code}. Next, move to {targetPath}.",
                    $"Wrong pallet. Please scan pallet {line.Pallet?.Code}.",
                    "slot",
                    false),
                "slot" => new StepExpectation(
                    "slot",
                    QrTargetType.Slot,
                    line.TargetSlotId,
                    $"Correct location {targetPath}. You can now finish the inbound task.",
                    $"Wrong location. Please scan {targetPath}.",
                    "complete",
                    true),
                _ => throw new InvalidOperationException("Invalid inbound step.")
            };
        }

        var outboundOrder = await dbContext.OutboundOrders
            .Include(x => x.Warehouse)
            .Include(x => x.Lines)
                .ThenInclude(x => x.SourcePallet)
                    .ThenInclude(x => x!.CurrentSlot)
                        .ThenInclude(x => x!.Rack)
                            .ThenInclude(x => x!.Area)
            .Include(x => x.Lines)
                .ThenInclude(x => x.SourceSlot)
                    .ThenInclude(x => x!.Rack)
                        .ThenInclude(x => x!.Area)
            .FirstOrDefaultAsync(x => x.Id == task.OutboundOrderId && x.TenantId == currentUser.TenantId, cancellationToken)
            ?? throw new InvalidOperationException("Outbound order was not found.");

        var outboundLine = outboundOrder.Lines.Single();
        var sourcePath = BuildSlotPath(outboundOrder.Warehouse?.Name ?? string.Empty, outboundLine.SourceSlot);

        return normalizedStep switch
        {
            "slot" => new StepExpectation(
                "slot",
                QrTargetType.Slot,
                outboundLine.SourceSlotId,
                $"Correct location {sourcePath}. Next, scan pallet {outboundLine.SourcePallet?.Code}.",
                $"Wrong pick location. Please scan {sourcePath}.",
                "pallet",
                false),
            "pallet" => new StepExpectation(
                "pallet",
                QrTargetType.Pallet,
                outboundLine.SourcePalletId,
                $"Correct pallet {outboundLine.SourcePallet?.Code}. You can now finish the outbound task.",
                $"Wrong pallet. Please scan pallet {outboundLine.SourcePallet?.Code}.",
                "complete",
                true),
            _ => throw new InvalidOperationException("Invalid outbound step.")
        };
    }

    private static void EnsureTaskCompletable(TaskAssignment? task)
    {
        if (task is null || task.Status != TaskWorkflowStatus.Verified || task.LastVerifiedAtUtc is null)
        {
            throw new InvalidOperationException("Task phai duoc quet xac minh thanh cong truoc khi hoan thanh.");
        }
    }

    private static IReadOnlyList<string> GetRequiredScanStepOrder(TaskType taskType) =>
        taskType == TaskType.Inbound
            ? ["product", "pallet", "slot"]
            : ["slot", "pallet"];

    private static HashSet<string> GetCompletedScanSteps(TaskAssignment task)
    {
        return task.ScanLogs
            .Where(x => x.IsSuccess)
            .Select(x => TryExtractStepKey(x.Message))
            .Where(static x => x is not null)
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string? TryExtractStepKey(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var separatorIndex = message.IndexOf(':');
        if (separatorIndex <= 0)
        {
            return null;
        }

        var stepKey = message[..separatorIndex].Trim().ToLowerInvariant();
        return stepKey is "product" or "pallet" or "slot"
            ? stepKey
            : null;
    }

    private async Task<InboundOrder> LoadInboundOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        return await dbContext.InboundOrders
            .Include(x => x.Warehouse)
            .Include(x => x.AssignedToUser)
            .Include(x => x.Lines)
                .ThenInclude(x => x.Product)
            .Include(x => x.Lines)
                .ThenInclude(x => x.Pallet)
                    .ThenInclude(x => x!.CurrentSlot)
                        .ThenInclude(x => x!.Rack)
                            .ThenInclude(x => x!.Area)
            .Include(x => x.Lines)
                .ThenInclude(x => x.TargetSlot)
                    .ThenInclude(x => x!.Rack)
                        .ThenInclude(x => x!.Area)
            .Include(x => x.TaskAssignment)
            .FirstAsync(x => x.Id == orderId, cancellationToken);
    }

    private async Task<OutboundOrder> LoadOutboundOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        return await dbContext.OutboundOrders
            .Include(x => x.Warehouse)
            .Include(x => x.AssignedToUser)
            .Include(x => x.Lines)
                .ThenInclude(x => x.Product)
            .Include(x => x.Lines)
                .ThenInclude(x => x.SourcePallet)
                    .ThenInclude(x => x!.CurrentSlot)
                        .ThenInclude(x => x!.Rack)
                            .ThenInclude(x => x!.Area)
            .Include(x => x.Lines)
                .ThenInclude(x => x.SourceSlot)
                    .ThenInclude(x => x!.Rack)
                        .ThenInclude(x => x!.Area)
            .Include(x => x.TaskAssignment)
            .FirstAsync(x => x.Id == orderId, cancellationToken);
    }

    private async Task<Slot> LoadSlotWithHierarchyAsync(Guid slotId, CancellationToken cancellationToken)
    {
        var slot = await dbContext.Slots
            .Include(x => x.Rack)
                .ThenInclude(x => x!.Area)
            .FirstOrDefaultAsync(x => x.Id == slotId && x.TenantId == currentUser.TenantId, cancellationToken);
        return slot ?? throw new InvalidOperationException("Khong tim thay Slot.");
    }

    private async Task SaveAuditAsync(string action, string entityName, Guid entityId, string detail, CancellationToken cancellationToken)
    {
        dbContext.AuditLogs.Add(new AuditLog
        {
            TenantId = currentUser.TenantId,
            PerformedByUserId = currentUser.UserId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Detail = detail
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static InboundOrderDto MapInbound(InboundOrder order) =>
        new(
            order.Id,
            order.ReferenceCode,
            order.WarehouseId,
            order.Warehouse?.Name ?? string.Empty,
            order.AssignedToUserId,
            order.AssignedToUser?.FullName ?? string.Empty,
            order.Status.ToString(),
            order.CreatedAtUtc,
            order.Lines.Select(line => new InboundOrderLineDto(
                line.Id,
                line.ProductId,
                line.Product?.Name ?? string.Empty,
                line.PalletId,
                line.Pallet?.Code ?? string.Empty,
                BuildPalletLocationPath(order.Warehouse?.Name ?? string.Empty, line.Pallet),
                line.TargetSlotId,
                line.TargetSlot?.Name ?? string.Empty,
                BuildSlotPath(order.Warehouse?.Name ?? string.Empty, line.TargetSlot),
                line.Quantity)).ToList(),
            order.TaskAssignment?.Id);

    private static OutboundOrderDto MapOutbound(OutboundOrder order) =>
        new(
            order.Id,
            order.ReferenceCode,
            order.WarehouseId,
            order.Warehouse?.Name ?? string.Empty,
            order.AssignedToUserId,
            order.AssignedToUser?.FullName ?? string.Empty,
            order.Status.ToString(),
            order.CreatedAtUtc,
            order.Lines.Select(line => new OutboundOrderLineDto(
                line.Id,
                line.ProductId,
                line.Product?.Name ?? string.Empty,
                line.SourcePalletId,
                line.SourcePallet?.Code ?? string.Empty,
                BuildPalletLocationPath(order.Warehouse?.Name ?? string.Empty, line.SourcePallet),
                line.SourceSlotId,
                line.SourceSlot?.Name ?? string.Empty,
                BuildSlotPath(order.Warehouse?.Name ?? string.Empty, line.SourceSlot),
                line.Quantity)).ToList(),
            order.TaskAssignment?.Id);

    private static TaskDto MapTask(TaskAssignment task) =>
        new(
            task.Id,
            task.TaskType,
            task.Title,
            task.Instruction,
            task.Status.ToString(),
            task.InboundOrderId,
            task.OutboundOrderId,
            task.InboundOrder?.ReferenceCode ?? task.OutboundOrder?.ReferenceCode,
            task.ExpectedTargetType.ToString(),
            task.ExpectedTargetId,
            task.CreatedAtUtc,
            task.LastVerifiedAtUtc);

    private static void EnsurePalletBelongsToWarehouse(Pallet pallet, Guid warehouseId)
    {
        if (pallet.WarehouseId != warehouseId)
        {
            throw new InvalidOperationException("Pallet does not belong to the selected warehouse.");
        }
    }

    private static void EnsureSlotBelongsToWarehouse(Slot slot, Guid warehouseId)
    {
        if (slot.Rack?.Area?.WarehouseId != warehouseId)
        {
            throw new InvalidOperationException("Slot does not belong to the selected warehouse.");
        }
    }

    private static string BuildPalletLocationPath(string warehouseName, Pallet? pallet)
    {
        if (pallet is null)
        {
            return string.Empty;
        }

        if (pallet.CurrentSlot is null)
        {
            return string.Join(" > ", new[] { warehouseName, "No slot assigned" }.Where(static x => !string.IsNullOrWhiteSpace(x)));
        }

        return BuildSlotPath(warehouseName, pallet.CurrentSlot);
    }

    private static string BuildSlotPath(string warehouseName, Slot? slot)
    {
        if (slot is null)
        {
            return string.Empty;
        }

        return string.Join(" > ", new[]
        {
            warehouseName,
            slot.Rack?.Area?.Name,
            slot.Rack?.Name,
            slot.Name
        }.Where(static x => !string.IsNullOrWhiteSpace(x)));
    }

    private sealed record StepExpectation(
        string StepKey,
        QrTargetType TargetType,
        Guid TargetId,
        string SuccessMessage,
        string FailureMessage,
        string? NextStepKey,
        bool MarksTaskVerified);
}
