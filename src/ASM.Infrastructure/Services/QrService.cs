using ASM.Application.Contracts;
using ASM.Application.Interfaces;
using ASM.Domain.Constants;
using ASM.Domain.Entities;
using ASM.Domain.Enums;
using ASM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using QRCoder;

namespace ASM.Infrastructure.Services;

public class QrService(
    AppDbContext dbContext,
    ICurrentUserService currentUser) : IQrService
{
    public async Task<QrCodeDto> GenerateAsync(CreateQrRequest request, CancellationToken cancellationToken)
    {
        var existing = await dbContext.QrCodes.FirstOrDefaultAsync(
            x => x.TenantId == currentUser.TenantId &&
                 x.TargetType == request.TargetType &&
                 x.TargetId == request.TargetId,
            cancellationToken);

        if (existing is not null)
        {
            return Map(existing);
        }

        var qrCode = new QrCode
        {
            TenantId = currentUser.TenantId,
            TargetType = request.TargetType,
            TargetId = request.TargetId,
            Label = request.Label,
            Payload = $"{currentUser.TenantId:N}|{request.TargetType}|{request.TargetId:N}"
        };

        dbContext.QrCodes.Add(qrCode);
        dbContext.AuditLogs.Add(new AuditLog
        {
            TenantId = currentUser.TenantId,
            PerformedByUserId = currentUser.UserId,
            Action = "GenerateQr",
            EntityName = nameof(QrCode),
            EntityId = qrCode.Id,
            Detail = qrCode.Payload
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(qrCode);
    }

    public async Task<QrCodeDto?> GetAsync(Guid qrId, CancellationToken cancellationToken)
    {
        var qr = await dbContext.QrCodes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == qrId && x.TenantId == currentUser.TenantId, cancellationToken);
        return qr is null ? null : Map(qr);
    }

    public async Task<byte[]> RenderPngAsync(Guid qrId, CancellationToken cancellationToken)
    {
        var qr = await dbContext.QrCodes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == qrId && x.TenantId == currentUser.TenantId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy QR.");

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(qr.Payload, QRCodeGenerator.ECCLevel.Q);
        var pngQrCode = new PngByteQRCode(data);
        return pngQrCode.GetGraphic(20);
    }

    public async Task<QrLookupResultDto> LookupAsync(string payload, CancellationToken cancellationToken)
    {
        EnsureCanLookup();

        var normalizedPayload = payload.Trim();
        var qr = await dbContext.QrCodes
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.TenantId == currentUser.TenantId && x.Payload == normalizedPayload,
                cancellationToken);

        if (qr is null)
        {
            throw new InvalidOperationException("QR code was not found in your organization.");
        }

        var result = qr.TargetType switch
        {
            QrTargetType.Area => await LookupAreaAsync(qr, cancellationToken),
            QrTargetType.Rack => await LookupRackAsync(qr, cancellationToken),
            QrTargetType.Slot => await LookupSlotAsync(qr, cancellationToken),
            QrTargetType.Pallet => await LookupPalletAsync(qr, cancellationToken),
            _ => throw new InvalidOperationException("Only Area, Rack, Slot, and Pallet QR codes can be viewed here.")
        };

        dbContext.AuditLogs.Add(new AuditLog
        {
            TenantId = currentUser.TenantId,
            PerformedByUserId = currentUser.UserId,
            Action = "LookupQr",
            EntityName = qr.TargetType.ToString(),
            EntityId = qr.TargetId,
            Detail = $"Viewed {qr.Label} by QR"
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return result;
    }

    private async Task<QrLookupResultDto> LookupAreaAsync(QrCode qr, CancellationToken cancellationToken)
    {
        var area = await dbContext.Areas
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Warehouse)
            .Include(x => x.Racks)
                .ThenInclude(x => x.Slots)
                    .ThenInclude(x => x.CurrentPallets)
                        .ThenInclude(x => x.InventoryItems)
                            .ThenInclude(x => x.Product)
                                .ThenInclude(x => x!.Category)
            .FirstOrDefaultAsync(
                x => x.Id == qr.TargetId && x.TenantId == currentUser.TenantId,
                cancellationToken)
            ?? throw TargetNotFound();

        var warehouse = area.Warehouse ?? throw TargetNotFound();
        return new QrLookupResultDto(
            qr.TargetType,
            area.Id,
            area.Name,
            MapWarehouse(warehouse),
            $"{warehouse.Name} > {area.Name}",
            [MapArea(area)],
            []);
    }

    private async Task<QrLookupResultDto> LookupRackAsync(QrCode qr, CancellationToken cancellationToken)
    {
        var rack = await dbContext.Racks
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Area)
                .ThenInclude(x => x!.Warehouse)
            .Include(x => x.Slots)
                .ThenInclude(x => x.CurrentPallets)
                    .ThenInclude(x => x.InventoryItems)
                        .ThenInclude(x => x.Product)
                            .ThenInclude(x => x!.Category)
            .FirstOrDefaultAsync(
                x => x.Id == qr.TargetId && x.TenantId == currentUser.TenantId,
                cancellationToken)
            ?? throw TargetNotFound();

        var area = rack.Area ?? throw TargetNotFound();
        var warehouse = area.Warehouse ?? throw TargetNotFound();
        return new QrLookupResultDto(
            qr.TargetType,
            rack.Id,
            rack.Name,
            MapWarehouse(warehouse),
            $"{warehouse.Name} > {area.Name} > {rack.Name}",
            [new QrLookupAreaDto(area.Id, area.Name, [MapRack(rack)])],
            []);
    }

    private async Task<QrLookupResultDto> LookupSlotAsync(QrCode qr, CancellationToken cancellationToken)
    {
        var slot = await dbContext.Slots
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Rack)
                .ThenInclude(x => x!.Area)
                    .ThenInclude(x => x!.Warehouse)
            .Include(x => x.CurrentPallets)
                .ThenInclude(x => x.InventoryItems)
                    .ThenInclude(x => x.Product)
                        .ThenInclude(x => x!.Category)
            .FirstOrDefaultAsync(
                x => x.Id == qr.TargetId && x.TenantId == currentUser.TenantId,
                cancellationToken)
            ?? throw TargetNotFound();

        var rack = slot.Rack ?? throw TargetNotFound();
        var area = rack.Area ?? throw TargetNotFound();
        var warehouse = area.Warehouse ?? throw TargetNotFound();
        return new QrLookupResultDto(
            qr.TargetType,
            slot.Id,
            slot.Name,
            MapWarehouse(warehouse),
            $"{warehouse.Name} > {area.Name} > {rack.Name} > {slot.Name}",
            [new QrLookupAreaDto(area.Id, area.Name, [new QrLookupRackDto(rack.Id, rack.Name, [MapSlot(slot)])])],
            []);
    }

    private async Task<QrLookupResultDto> LookupPalletAsync(QrCode qr, CancellationToken cancellationToken)
    {
        var pallet = await dbContext.Pallets
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Warehouse)
            .Include(x => x.CurrentSlot)
                .ThenInclude(x => x!.Rack)
                    .ThenInclude(x => x!.Area)
            .Include(x => x.InventoryItems)
                .ThenInclude(x => x.Product)
                    .ThenInclude(x => x!.Category)
            .FirstOrDefaultAsync(
                x => x.Id == qr.TargetId && x.TenantId == currentUser.TenantId,
                cancellationToken)
            ?? throw TargetNotFound();

        var warehouse = pallet.Warehouse ?? throw TargetNotFound();
        var mappedPallet = MapPallet(pallet);
        if (pallet.CurrentSlot?.Rack?.Area is not { } area || pallet.CurrentSlot.Rack is not { } rack)
        {
            return new QrLookupResultDto(
                qr.TargetType,
                pallet.Id,
                pallet.Code,
                MapWarehouse(warehouse),
                $"{warehouse.Name} > No slot assigned",
                [],
                [mappedPallet]);
        }

        var slot = pallet.CurrentSlot;
        return new QrLookupResultDto(
            qr.TargetType,
            pallet.Id,
            pallet.Code,
            MapWarehouse(warehouse),
            $"{warehouse.Name} > {area.Name} > {rack.Name} > {slot.Name} > {pallet.Code}",
            [new QrLookupAreaDto(
                area.Id,
                area.Name,
                [new QrLookupRackDto(
                    rack.Id,
                    rack.Name,
                    [new QrLookupSlotDto(slot.Id, slot.Name, slot.IsOccupied, [mappedPallet])])])],
            []);
    }

    private static QrLookupWarehouseDto MapWarehouse(Warehouse warehouse) =>
        new(warehouse.Id, warehouse.Name, warehouse.Code, warehouse.Address);

    private static QrLookupAreaDto MapArea(Area area) =>
        new(
            area.Id,
            area.Name,
            area.Racks.OrderBy(x => x.Name).Select(MapRack).ToList());

    private static QrLookupRackDto MapRack(Rack rack) =>
        new(
            rack.Id,
            rack.Name,
            rack.Slots.OrderBy(x => x.Name).Select(MapSlot).ToList());

    private static QrLookupSlotDto MapSlot(Slot slot) =>
        new(
            slot.Id,
            slot.Name,
            slot.IsOccupied,
            slot.CurrentPallets.OrderBy(x => x.Code).Select(MapPallet).ToList());

    private static QrLookupPalletDto MapPallet(Pallet pallet) =>
        new(
            pallet.Id,
            pallet.Code,
            pallet.Status.ToString(),
            pallet.InventoryItems
                .OrderBy(x => x.Product!.Name)
                .Select(x => new QrLookupInventoryDto(
                    x.Id,
                    x.Product?.Sku ?? "Unknown SKU",
                    x.Product?.Name ?? "Unknown product",
                    x.Product?.Category?.Name,
                    x.Product?.Brand,
                    x.Quantity,
                    x.LotNumber,
                    x.ExpiryDate))
                .ToList());

    private void EnsureCanLookup()
    {
        if (!currentUser.IsInRole(RoleNames.Owner)
            && !currentUser.IsInRole(RoleNames.Manager)
            && !currentUser.IsInRole(RoleNames.Staff))
        {
            throw new InvalidOperationException("You do not have permission to view warehouse QR information.");
        }
    }

    private static InvalidOperationException TargetNotFound() =>
        new("QR code was not found in your organization.");

    private static QrCodeDto Map(QrCode qr) =>
        new(qr.Id, qr.TargetType, qr.TargetId, qr.Payload, qr.Label);
}
