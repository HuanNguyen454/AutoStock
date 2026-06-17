using ASM.Application.Contracts;
using ASM.Application.Interfaces;
using ASM.Domain.Entities;
using ASM.Domain.Enums;
using ASM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ASM.Infrastructure.Services;

public class CatalogService(
    AppDbContext dbContext,
    ICurrentUserService currentUser) : ICatalogService
{
    public async Task<IReadOnlyCollection<ProductDto>> GetProductsAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Products
            .Where(x => x.TenantId == currentUser.TenantId)
            .OrderBy(x => x.Name)
            .Select(x => new ProductDto(x.Id, x.Sku, x.Name, x.Description))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProductDto> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken)
    {
        var product = new Product
        {
            TenantId = currentUser.TenantId,
            Sku = request.Sku,
            Name = request.Name,
            Description = request.Description
        };

        dbContext.Products.Add(product);
        await SaveAuditAsync("CreateProduct", nameof(Product), product.Id, product.Name, cancellationToken);
        return new ProductDto(product.Id, product.Sku, product.Name, product.Description);
    }

    public async Task<IReadOnlyList<ProductLocationSearchResultDto>> SearchProductLocationsAsync(
        string keyword,
        CancellationToken cancellationToken = default)
    {
        var normalizedKeyword = keyword.Trim().ToLower();
        if (string.IsNullOrWhiteSpace(normalizedKeyword))
        {
            return [];
        }

        var rows = await (
            from product in dbContext.Products.AsNoTracking()
            where product.TenantId == currentUser.TenantId &&
                  (product.Sku.ToLower().Contains(normalizedKeyword) ||
                   product.Name.ToLower().Contains(normalizedKeyword))
            join inventory in dbContext.InventoryItems.AsNoTracking().Where(x => x.TenantId == currentUser.TenantId)
                on product.Id equals inventory.ProductId into inventoryItems
            from inventory in inventoryItems.DefaultIfEmpty()
            join pallet in dbContext.Pallets.AsNoTracking().Where(x => x.TenantId == currentUser.TenantId)
                on inventory == null ? null : (Guid?)inventory.PalletId equals (Guid?)pallet.Id into pallets
            from pallet in pallets.DefaultIfEmpty()
            join slot in dbContext.Slots.AsNoTracking().Where(x => x.TenantId == currentUser.TenantId)
                on pallet == null ? null : pallet.CurrentSlotId equals (Guid?)slot.Id into slots
            from slot in slots.DefaultIfEmpty()
            join rack in dbContext.Racks.AsNoTracking().Where(x => x.TenantId == currentUser.TenantId)
                on slot == null ? null : (Guid?)slot.RackId equals (Guid?)rack.Id into racks
            from rack in racks.DefaultIfEmpty()
            join area in dbContext.Areas.AsNoTracking().Where(x => x.TenantId == currentUser.TenantId)
                on rack == null ? null : (Guid?)rack.AreaId equals (Guid?)area.Id into areas
            from area in areas.DefaultIfEmpty()
            join warehouseFromSlot in dbContext.Warehouses.AsNoTracking().Where(x => x.TenantId == currentUser.TenantId)
                on area == null ? null : (Guid?)area.WarehouseId equals (Guid?)warehouseFromSlot.Id into slotWarehouses
            from warehouseFromSlot in slotWarehouses.DefaultIfEmpty()
            join warehouseFromPallet in dbContext.Warehouses.AsNoTracking().Where(x => x.TenantId == currentUser.TenantId)
                on pallet == null ? null : (Guid?)pallet.WarehouseId equals (Guid?)warehouseFromPallet.Id into palletWarehouses
            from warehouseFromPallet in palletWarehouses.DefaultIfEmpty()
            orderby product.Name, product.Sku, pallet == null ? string.Empty : pallet.Code
            select new
            {
                ProductId = product.Id,
                product.Sku,
                ProductName = product.Name,
                InventoryItemId = inventory == null ? null : (Guid?)inventory.Id,
                Quantity = inventory == null ? 0 : inventory.Quantity,
                PalletId = pallet == null ? null : (Guid?)pallet.Id,
                PalletCode = pallet == null ? null : pallet.Code,
                PalletStatus = pallet == null ? null : (PalletStatus?)pallet.Status,
                SlotId = slot == null ? null : (Guid?)slot.Id,
                SlotName = slot == null ? null : slot.Name,
                RackName = rack == null ? null : rack.Name,
                AreaName = area == null ? null : area.Name,
                WarehouseName = warehouseFromSlot == null
                    ? warehouseFromPallet == null ? null : warehouseFromPallet.Name
                    : warehouseFromSlot.Name
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row =>
            {
                var locationParts = new[]
                {
                    row.WarehouseName,
                    row.AreaName,
                    row.RackName,
                    row.SlotName
                }.Where(static x => !string.IsNullOrWhiteSpace(x));

                var note = row.InventoryItemId is null
                    ? "San pham chua co ton kho"
                    : row.PalletId is null
                        ? "Ton kho chua gan pallet"
                        : row.SlotId is null
                            ? "Pallet chua duoc dat vao vi tri"
                            : null;

                return new ProductLocationSearchResultDto(
                    row.ProductId,
                    row.Sku,
                    row.ProductName,
                    row.InventoryItemId,
                    row.Quantity,
                    row.PalletId,
                    row.PalletCode,
                    row.PalletStatus?.ToString(),
                    row.SlotId,
                    row.SlotName,
                    row.RackName,
                    row.AreaName,
                    row.WarehouseName,
                    string.Join(" / ", locationParts),
                    note);
            })
            .ToList();
    }

    public async Task<IReadOnlyCollection<PalletDto>> GetPalletsAsync(CancellationToken cancellationToken)
    {
        var pallets = await dbContext.Pallets
            .Where(x => x.TenantId == currentUser.TenantId)
            .Include(x => x.InventoryItems)
            .ThenInclude(x => x.Product)
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken);

        return pallets.Select(MapPallet).ToList();
    }

    public async Task<PalletDto> CreatePalletAsync(CreatePalletRequest request, CancellationToken cancellationToken)
    {
        var warehouseExists = await dbContext.Warehouses.AnyAsync(
            x => x.Id == request.WarehouseId && x.TenantId == currentUser.TenantId,
            cancellationToken);

        if (!warehouseExists)
        {
            throw new InvalidOperationException("Không tìm thấy kho.");
        }

        var pallet = new Pallet
        {
            TenantId = currentUser.TenantId,
            WarehouseId = request.WarehouseId,
            Code = request.Code,
            Status = PalletStatus.Empty
        };

        dbContext.Pallets.Add(pallet);
        await SaveAuditAsync("CreatePallet", nameof(Pallet), pallet.Id, pallet.Code, cancellationToken);
        return MapPallet(pallet);
    }

    private static PalletDto MapPallet(Pallet pallet) =>
        new(
            pallet.Id,
            pallet.WarehouseId,
            pallet.CurrentSlotId,
            pallet.Code,
            pallet.Status.ToString(),
            pallet.InventoryItems.Select(x => new InventoryItemDto(
                x.Id,
                x.ProductId,
                x.Product?.Name ?? string.Empty,
                x.Quantity)).ToList());

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
