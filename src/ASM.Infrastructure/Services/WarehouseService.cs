using ASM.Application.Contracts;
using ASM.Application.Interfaces;
using ASM.Domain.Entities;
using ASM.Domain.Enums;
using ASM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ASM.Infrastructure.Services;

public class WarehouseService(
    AppDbContext dbContext,
    ICurrentUserService currentUser) : IWarehouseService
{
    public async Task<IReadOnlyCollection<WarehouseDto>> GetWarehousesAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Warehouses
            .Where(x => x.TenantId == currentUser.TenantId)
            .OrderBy(x => x.Name)
            .Select(x => new WarehouseDto(x.Id, x.Name, x.Code, x.Address))
            .ToListAsync(cancellationToken);
    }

    public async Task<WarehouseDto> CreateWarehouseAsync(CreateWarehouseRequest request, CancellationToken cancellationToken)
    {
        var name = NormalizeRequired(request.Name, "Warehouse name");
        var code = NormalizeRequired(request.Code, "Warehouse code").ToUpperInvariant();
        var address = NormalizeRequired(request.Address, "Warehouse address");
        await EnsureWarehouseCodeIsUniqueAsync(code, null, cancellationToken);

        var warehouse = new Warehouse
        {
            TenantId = currentUser.TenantId,
            Name = name,
            Code = code,
            Address = address
        };

        dbContext.Warehouses.Add(warehouse);
        await SaveAuditAsync("CreateWarehouse", nameof(Warehouse), warehouse.Id, warehouse.Name, cancellationToken);
        return new WarehouseDto(warehouse.Id, warehouse.Name, warehouse.Code, warehouse.Address);
    }

    public async Task<WarehouseDto> UpdateWarehouseAsync(UpdateWarehouseRequest request, CancellationToken cancellationToken)
    {
        var warehouse = await dbContext.Warehouses.FirstOrDefaultAsync(
            x => x.Id == request.Id && x.TenantId == currentUser.TenantId,
            cancellationToken) ?? throw new InvalidOperationException("Warehouse was not found.");

        var name = NormalizeRequired(request.Name, "Warehouse name");
        var code = NormalizeRequired(request.Code, "Warehouse code").ToUpperInvariant();
        var address = NormalizeRequired(request.Address, "Warehouse address");
        await EnsureWarehouseCodeIsUniqueAsync(code, warehouse.Id, cancellationToken);

        warehouse.Name = name;
        warehouse.Code = code;
        warehouse.Address = address;
        warehouse.UpdatedAtUtc = DateTime.UtcNow;

        await SaveAuditAsync("UpdateWarehouse", nameof(Warehouse), warehouse.Id, warehouse.Name, cancellationToken);
        return new WarehouseDto(warehouse.Id, warehouse.Name, warehouse.Code, warehouse.Address);
    }

    public async Task DeleteWarehouseAsync(Guid warehouseId, CancellationToken cancellationToken)
    {
        var warehouse = await dbContext.Warehouses.FirstOrDefaultAsync(
            x => x.Id == warehouseId && x.TenantId == currentUser.TenantId,
            cancellationToken) ?? throw new InvalidOperationException("Warehouse was not found.");

        var hasAreas = await dbContext.Areas.AnyAsync(x => x.WarehouseId == warehouse.Id && x.TenantId == currentUser.TenantId, cancellationToken);
        var hasPallets = await dbContext.Pallets.AnyAsync(x => x.WarehouseId == warehouse.Id && x.TenantId == currentUser.TenantId, cancellationToken);
        var hasInboundOrders = await dbContext.InboundOrders.AnyAsync(x => x.WarehouseId == warehouse.Id && x.TenantId == currentUser.TenantId, cancellationToken);
        var hasOutboundOrders = await dbContext.OutboundOrders.AnyAsync(x => x.WarehouseId == warehouse.Id && x.TenantId == currentUser.TenantId, cancellationToken);
        if (hasAreas || hasPallets || hasInboundOrders || hasOutboundOrders)
        {
            throw new InvalidOperationException("Cannot delete this warehouse because it still has layout, pallets, or orders.");
        }

        await RemoveQrCodesAsync(QrTargetType.Warehouse, warehouse.Id, cancellationToken);
        dbContext.Warehouses.Remove(warehouse);
        await SaveAuditAsync("DeleteWarehouse", nameof(Warehouse), warehouse.Id, warehouse.Name, cancellationToken);
    }

    public async Task<AreaDto> AddAreaAsync(CreateAreaRequest request, CancellationToken cancellationToken)
    {
        await EnsureWarehouseInTenantAsync(request.WarehouseId, cancellationToken);
        var area = new Area
        {
            TenantId = currentUser.TenantId,
            WarehouseId = request.WarehouseId,
            Name = NormalizeRequired(request.Name, "Area name")
        };

        dbContext.Areas.Add(area);
        await SaveAuditAsync("CreateArea", nameof(Area), area.Id, area.Name, cancellationToken);
        return new AreaDto(area.Id, area.WarehouseId, area.Name);
    }

    public async Task<AreaDto> UpdateAreaAsync(UpdateAreaRequest request, CancellationToken cancellationToken)
    {
        var area = await dbContext.Areas.FirstOrDefaultAsync(
            x => x.Id == request.Id && x.TenantId == currentUser.TenantId,
            cancellationToken) ?? throw new InvalidOperationException("Area was not found.");

        area.Name = NormalizeRequired(request.Name, "Area name");
        area.UpdatedAtUtc = DateTime.UtcNow;

        await SaveAuditAsync("UpdateArea", nameof(Area), area.Id, area.Name, cancellationToken);
        return new AreaDto(area.Id, area.WarehouseId, area.Name);
    }

    public async Task DeleteAreaAsync(Guid areaId, CancellationToken cancellationToken)
    {
        var area = await dbContext.Areas.FirstOrDefaultAsync(
            x => x.Id == areaId && x.TenantId == currentUser.TenantId,
            cancellationToken) ?? throw new InvalidOperationException("Area was not found.");

        var hasRacks = await dbContext.Racks.AnyAsync(x => x.AreaId == area.Id && x.TenantId == currentUser.TenantId, cancellationToken);
        if (hasRacks)
        {
            throw new InvalidOperationException("Cannot delete this area because it still has racks.");
        }

        await RemoveQrCodesAsync(QrTargetType.Area, area.Id, cancellationToken);
        dbContext.Areas.Remove(area);
        await SaveAuditAsync("DeleteArea", nameof(Area), area.Id, area.Name, cancellationToken);
    }

    public async Task<RackDto> AddRackAsync(CreateRackRequest request, CancellationToken cancellationToken)
    {
        var area = await dbContext.Areas.FirstOrDefaultAsync(
            x => x.Id == request.AreaId && x.TenantId == currentUser.TenantId,
            cancellationToken) ?? throw new InvalidOperationException("Area was not found.");

        var rack = new Rack
        {
            TenantId = currentUser.TenantId,
            AreaId = area.Id,
            Name = NormalizeRequired(request.Name, "Rack name")
        };

        dbContext.Racks.Add(rack);
        await SaveAuditAsync("CreateRack", nameof(Rack), rack.Id, rack.Name, cancellationToken);
        return new RackDto(rack.Id, rack.AreaId, rack.Name);
    }

    public async Task<RackDto> UpdateRackAsync(UpdateRackRequest request, CancellationToken cancellationToken)
    {
        var rack = await dbContext.Racks.FirstOrDefaultAsync(
            x => x.Id == request.Id && x.TenantId == currentUser.TenantId,
            cancellationToken) ?? throw new InvalidOperationException("Rack was not found.");

        rack.Name = NormalizeRequired(request.Name, "Rack name");
        rack.UpdatedAtUtc = DateTime.UtcNow;

        await SaveAuditAsync("UpdateRack", nameof(Rack), rack.Id, rack.Name, cancellationToken);
        return new RackDto(rack.Id, rack.AreaId, rack.Name);
    }

    public async Task DeleteRackAsync(Guid rackId, CancellationToken cancellationToken)
    {
        var rack = await dbContext.Racks.FirstOrDefaultAsync(
            x => x.Id == rackId && x.TenantId == currentUser.TenantId,
            cancellationToken) ?? throw new InvalidOperationException("Rack was not found.");

        var hasSlots = await dbContext.Slots.AnyAsync(x => x.RackId == rack.Id && x.TenantId == currentUser.TenantId, cancellationToken);
        if (hasSlots)
        {
            throw new InvalidOperationException("Cannot delete this rack because it still has slots.");
        }

        await RemoveQrCodesAsync(QrTargetType.Rack, rack.Id, cancellationToken);
        dbContext.Racks.Remove(rack);
        await SaveAuditAsync("DeleteRack", nameof(Rack), rack.Id, rack.Name, cancellationToken);
    }

    public async Task<SlotDto> AddSlotAsync(CreateSlotRequest request, CancellationToken cancellationToken)
    {
        var rack = await dbContext.Racks.FirstOrDefaultAsync(
            x => x.Id == request.RackId && x.TenantId == currentUser.TenantId,
            cancellationToken) ?? throw new InvalidOperationException("Rack was not found.");

        var slot = new Slot
        {
            TenantId = currentUser.TenantId,
            RackId = rack.Id,
            Name = NormalizeRequired(request.Name, "Slot name"),
            IsOccupied = false
        };

        dbContext.Slots.Add(slot);
        await SaveAuditAsync("CreateSlot", nameof(Slot), slot.Id, slot.Name, cancellationToken);
        return new SlotDto(slot.Id, slot.RackId, slot.Name, slot.IsOccupied);
    }

    public async Task<SlotDto> UpdateSlotAsync(UpdateSlotRequest request, CancellationToken cancellationToken)
    {
        var slot = await dbContext.Slots.FirstOrDefaultAsync(
            x => x.Id == request.Id && x.TenantId == currentUser.TenantId,
            cancellationToken) ?? throw new InvalidOperationException("Slot was not found.");

        slot.Name = NormalizeRequired(request.Name, "Slot name");
        slot.UpdatedAtUtc = DateTime.UtcNow;

        await SaveAuditAsync("UpdateSlot", nameof(Slot), slot.Id, slot.Name, cancellationToken);
        return new SlotDto(slot.Id, slot.RackId, slot.Name, slot.IsOccupied);
    }

    public async Task DeleteSlotAsync(Guid slotId, CancellationToken cancellationToken)
    {
        var slot = await dbContext.Slots.FirstOrDefaultAsync(
            x => x.Id == slotId && x.TenantId == currentUser.TenantId,
            cancellationToken) ?? throw new InvalidOperationException("Slot was not found.");

        var hasCurrentPallets = await dbContext.Pallets.AnyAsync(x => x.CurrentSlotId == slot.Id && x.TenantId == currentUser.TenantId, cancellationToken);
        var hasInboundLines = await dbContext.InboundOrderLines.AnyAsync(x => x.TargetSlotId == slot.Id && x.TenantId == currentUser.TenantId, cancellationToken);
        var hasOutboundLines = await dbContext.OutboundOrderLines.AnyAsync(x => x.SourceSlotId == slot.Id && x.TenantId == currentUser.TenantId, cancellationToken);
        if (hasCurrentPallets || hasInboundLines || hasOutboundLines)
        {
            throw new InvalidOperationException("Cannot delete this slot because it still has pallets or order history.");
        }

        await RemoveQrCodesAsync(QrTargetType.Slot, slot.Id, cancellationToken);
        dbContext.Slots.Remove(slot);
        await SaveAuditAsync("DeleteSlot", nameof(Slot), slot.Id, slot.Name, cancellationToken);
    }

    public async Task<WarehouseLayoutDto?> GetLayoutAsync(Guid warehouseId, CancellationToken cancellationToken)
    {
        var warehouse = await dbContext.Warehouses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == warehouseId && x.TenantId == currentUser.TenantId, cancellationToken);
        if (warehouse is null)
        {
            return null;
        }

        var areas = await dbContext.Areas
            .AsNoTracking()
            .Where(x => x.WarehouseId == warehouseId && x.TenantId == currentUser.TenantId)
            .Include(x => x.Racks)
            .ThenInclude(x => x.Slots)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return new WarehouseLayoutDto(
            new WarehouseDto(warehouse.Id, warehouse.Name, warehouse.Code, warehouse.Address),
            areas.Select(area => new AreaDetailDto(
                area.Id,
                area.Name,
                area.Racks
                    .OrderBy(r => r.Name)
                    .Select(rack => new RackDetailDto(
                        rack.Id,
                        rack.Name,
                        rack.Slots
                            .OrderBy(slot => slot.Name)
                            .Select(slot => new SlotDto(slot.Id, slot.RackId, slot.Name, slot.IsOccupied))
                            .ToList()))
                    .ToList()))
            .ToList());
    }

    private async Task EnsureWarehouseInTenantAsync(Guid warehouseId, CancellationToken cancellationToken)
    {
        var exists = await dbContext.Warehouses.AnyAsync(
            x => x.Id == warehouseId && x.TenantId == currentUser.TenantId,
            cancellationToken);

        if (!exists)
        {
            throw new InvalidOperationException("Warehouse was not found.");
        }
    }

    private async Task EnsureWarehouseCodeIsUniqueAsync(string code, Guid? currentWarehouseId, CancellationToken cancellationToken)
    {
        var exists = await dbContext.Warehouses.AnyAsync(
            x => x.TenantId == currentUser.TenantId &&
                 x.Code == code &&
                 (!currentWarehouseId.HasValue || x.Id != currentWarehouseId.Value),
            cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException("Warehouse code already exists.");
        }
    }

    private async Task RemoveQrCodesAsync(QrTargetType targetType, Guid targetId, CancellationToken cancellationToken)
    {
        var qrCodes = await dbContext.QrCodes
            .Where(x => x.TenantId == currentUser.TenantId && x.TargetType == targetType && x.TargetId == targetId)
            .ToListAsync(cancellationToken);

        dbContext.QrCodes.RemoveRange(qrCodes);
    }

    private static string NormalizeRequired(string value, string fieldName)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException($"{fieldName} is required.");
        }

        return normalized;
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
}
