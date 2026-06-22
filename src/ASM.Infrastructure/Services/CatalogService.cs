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
    public async Task<IReadOnlyCollection<ProductCategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken)
    {
        return await dbContext.ProductCategories
            .AsNoTracking()
            .Where(x => x.TenantId == currentUser.TenantId)
            .OrderBy(x => x.Name)
            .Select(x => new ProductCategoryDto(x.Id, x.Code, x.Name, x.Description, x.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProductCategoryDto> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Category code and name are required.");
        }

        var exists = await dbContext.ProductCategories.AnyAsync(
            x => x.TenantId == currentUser.TenantId && x.Code == code,
            cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException("Category code already exists.");
        }

        var category = new ProductCategory
        {
            TenantId = currentUser.TenantId,
            Code = code,
            Name = name,
            Description = request.Description,
            IsActive = true
        };

        dbContext.ProductCategories.Add(category);
        await SaveAuditAsync("CreateCategory", nameof(ProductCategory), category.Id, category.Code, cancellationToken);
        return MapCategory(category);
    }

    public async Task<ProductCategoryDto> UpdateCategoryAsync(UpdateCategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await dbContext.ProductCategories.FirstOrDefaultAsync(
            x => x.Id == request.Id && x.TenantId == currentUser.TenantId,
            cancellationToken)
            ?? throw new InvalidOperationException("Category was not found.");

        var code = request.Code.Trim().ToUpperInvariant();
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Category code and name are required.");
        }

        var duplicated = await dbContext.ProductCategories.AnyAsync(
            x => x.TenantId == currentUser.TenantId && x.Id != request.Id && x.Code == code,
            cancellationToken);
        if (duplicated)
        {
            throw new InvalidOperationException("Category code already exists.");
        }

        category.Code = code;
        category.Name = name;
        category.Description = request.Description;
        category.IsActive = request.IsActive;
        category.UpdatedAtUtc = DateTime.UtcNow;

        await SaveAuditAsync("UpdateCategory", nameof(ProductCategory), category.Id, category.Code, cancellationToken);
        return MapCategory(category);
    }

    public Task<IReadOnlyCollection<ProductDto>> GetProductsAsync(CancellationToken cancellationToken) =>
        GetProductsAsync(null, null, cancellationToken);

    public async Task<IReadOnlyCollection<ProductDto>> GetProductsAsync(
        Guid? categoryId,
        string? keyword,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Products
            .AsNoTracking()
            .Where(x => x.TenantId == currentUser.TenantId);

        if (categoryId.HasValue)
        {
            query = query.Where(x => x.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalizedKeyword = keyword.Trim().ToLower();
            query = query.Where(x =>
                x.Sku.ToLower().Contains(normalizedKeyword) ||
                x.Name.ToLower().Contains(normalizedKeyword) ||
                (x.Description != null && x.Description.ToLower().Contains(normalizedKeyword)));
        }

        return await query
            .OrderBy(x => x.Name)
            .Select(x => new ProductDto(
                x.Id,
                x.Sku,
                x.Name,
                x.Description,
                x.CategoryId,
                x.Category == null ? null : x.Category.Name,
                x.Brand))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProductDto> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken)
    {
        var sku = request.Sku.Trim();
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(sku) || string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Product SKU and name are required.");
        }

        if (request.CategoryId.HasValue)
        {
            var categoryExists = await dbContext.ProductCategories.AnyAsync(
                x => x.Id == request.CategoryId.Value && x.TenantId == currentUser.TenantId && x.IsActive,
                cancellationToken);
            if (!categoryExists)
            {
                throw new InvalidOperationException("Category was not found.");
            }
        }

        var product = new Product
        {
            TenantId = currentUser.TenantId,
            CategoryId = request.CategoryId,
            Sku = sku,
            Name = name,
            Description = request.Description,
            Brand = request.Brand
        };

        dbContext.Products.Add(product);
        await SaveAuditAsync("CreateProduct", nameof(Product), product.Id, product.Name, cancellationToken);
        return await ProjectProduct(product.Id, cancellationToken);
    }

    public Task<IReadOnlyList<ProductLocationSearchResultDto>> SearchProductLocationsAsync(
        string keyword,
        CancellationToken cancellationToken = default) =>
        SearchProductLocationsAsync(keyword, null, cancellationToken);

    public async Task<IReadOnlyList<ProductLocationSearchResultDto>> SearchProductLocationsAsync(
        string? keyword,
        Guid? categoryId,
        CancellationToken cancellationToken = default)
    {
        var normalizedKeyword = keyword?.Trim().ToLower();
        if (string.IsNullOrWhiteSpace(normalizedKeyword) && !categoryId.HasValue)
        {
            return [];
        }

        var rows = await (
            from product in dbContext.Products.AsNoTracking()
            where product.TenantId == currentUser.TenantId
            join category in dbContext.ProductCategories.AsNoTracking().Where(x => x.TenantId == currentUser.TenantId)
                on product.CategoryId equals (Guid?)category.Id into categories
            from category in categories.DefaultIfEmpty()
            where (!categoryId.HasValue || product.CategoryId == categoryId.Value) &&
                  (string.IsNullOrWhiteSpace(normalizedKeyword) ||
                   product.Sku.ToLower().Contains(normalizedKeyword) ||
                   product.Name.ToLower().Contains(normalizedKeyword) ||
                   (product.Description != null && product.Description.ToLower().Contains(normalizedKeyword)))
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
            orderby inventory == null ? DateTime.MaxValue : inventory.ExpiryDate ?? DateTime.MaxValue,
                product.Name,
                product.Sku,
                pallet == null ? string.Empty : pallet.Code
            select new
            {
                ProductId = product.Id,
                product.Sku,
                ProductName = product.Name,
                CategoryName = category == null ? null : category.Name,
                InventoryItemId = inventory == null ? null : (Guid?)inventory.Id,
                Quantity = inventory == null ? 0 : inventory.Quantity,
                LotNumber = inventory == null ? null : inventory.LotNumber,
                ExpiryDate = inventory == null ? null : inventory.ExpiryDate,
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
                            : BuildExpiryNote(row.ExpiryDate);

                return new ProductLocationSearchResultDto(
                    row.ProductId,
                    row.Sku,
                    row.ProductName,
                    row.CategoryName,
                    row.InventoryItemId,
                    row.Quantity,
                    row.LotNumber,
                    row.ExpiryDate,
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
            .Include(x => x.InventoryItems.OrderBy(i => i.ExpiryDate ?? DateTime.MaxValue))
            .ThenInclude(x => x.Product)
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken);

        return pallets.Select(MapPallet).ToList();
    }

    public async Task<PalletDto> CreatePalletAsync(CreatePalletRequest request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOperationException("Pallet code is required.");
        }

        var warehouseExists = await dbContext.Warehouses.AnyAsync(
            x => x.Id == request.WarehouseId && x.TenantId == currentUser.TenantId,
            cancellationToken);

        if (!warehouseExists)
        {
            throw new InvalidOperationException("Khong tim thay kho.");
        }

        var duplicated = await dbContext.Pallets.AnyAsync(
            x => x.TenantId == currentUser.TenantId &&
                 x.WarehouseId == request.WarehouseId &&
                 x.Code == code,
            cancellationToken);
        if (duplicated)
        {
            throw new InvalidOperationException("Pallet code already exists in this warehouse.");
        }

        var pallet = new Pallet
        {
            TenantId = currentUser.TenantId,
            WarehouseId = request.WarehouseId,
            Code = code,
            Status = PalletStatus.Empty
        };

        dbContext.Pallets.Add(pallet);
        await SaveAuditAsync("CreatePallet", nameof(Pallet), pallet.Id, pallet.Code, cancellationToken);
        return MapPallet(pallet);
    }

    public async Task<PalletDto> UpdatePalletAsync(UpdatePalletRequest request, CancellationToken cancellationToken)
    {
        var pallet = await dbContext.Pallets
            .Include(x => x.InventoryItems.OrderBy(i => i.ExpiryDate ?? DateTime.MaxValue))
            .ThenInclude(x => x.Product)
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.TenantId == currentUser.TenantId, cancellationToken)
            ?? throw new InvalidOperationException("Pallet was not found.");

        var code = request.Code.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOperationException("Pallet code is required.");
        }

        var duplicated = await dbContext.Pallets.AnyAsync(
            x => x.TenantId == currentUser.TenantId &&
                 x.WarehouseId == pallet.WarehouseId &&
                 x.Id != pallet.Id &&
                 x.Code == code,
            cancellationToken);
        if (duplicated)
        {
            throw new InvalidOperationException("Pallet code already exists in this warehouse.");
        }

        pallet.Code = code;
        pallet.UpdatedAtUtc = DateTime.UtcNow;

        await SaveAuditAsync("UpdatePallet", nameof(Pallet), pallet.Id, pallet.Code, cancellationToken);
        return MapPallet(pallet);
    }

    public async Task DeletePalletAsync(Guid palletId, CancellationToken cancellationToken)
    {
        var pallet = await dbContext.Pallets.FirstOrDefaultAsync(
            x => x.Id == palletId && x.TenantId == currentUser.TenantId,
            cancellationToken) ?? throw new InvalidOperationException("Pallet was not found.");

        var hasInventory = await dbContext.InventoryItems.AnyAsync(
            x => x.PalletId == pallet.Id && x.TenantId == currentUser.TenantId,
            cancellationToken);
        var hasInboundLines = await dbContext.InboundOrderLines.AnyAsync(
            x => x.PalletId == pallet.Id && x.TenantId == currentUser.TenantId,
            cancellationToken);
        var hasOutboundLines = await dbContext.OutboundOrderLines.AnyAsync(
            x => x.SourcePalletId == pallet.Id && x.TenantId == currentUser.TenantId,
            cancellationToken);
        if (hasInventory || hasInboundLines || hasOutboundLines)
        {
            throw new InvalidOperationException("Cannot delete this pallet because it has inventory or order history.");
        }

        var qrCodes = await dbContext.QrCodes
            .Where(x => x.TenantId == currentUser.TenantId &&
                        x.TargetType == QrTargetType.Pallet &&
                        x.TargetId == pallet.Id)
            .ToListAsync(cancellationToken);

        if (pallet.CurrentSlotId.HasValue)
        {
            var currentSlot = await dbContext.Slots.FirstOrDefaultAsync(
                x => x.Id == pallet.CurrentSlotId.Value && x.TenantId == currentUser.TenantId,
                cancellationToken);
            if (currentSlot is not null)
            {
                var hasOtherPallets = await dbContext.Pallets.AnyAsync(
                    x => x.TenantId == currentUser.TenantId &&
                         x.CurrentSlotId == currentSlot.Id &&
                         x.Id != pallet.Id,
                    cancellationToken);
                currentSlot.IsOccupied = hasOtherPallets;
                currentSlot.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        dbContext.QrCodes.RemoveRange(qrCodes);
        dbContext.Pallets.Remove(pallet);
        await SaveAuditAsync("DeletePallet", nameof(Pallet), pallet.Id, pallet.Code, cancellationToken);
    }

    public async Task AssignPalletToSlotAsync(AssignPalletSlotRequest request, CancellationToken cancellationToken)
    {
        var pallet = await dbContext.Pallets.FirstOrDefaultAsync(
            x => x.Id == request.PalletId && x.TenantId == currentUser.TenantId,
            cancellationToken) ?? throw new InvalidOperationException("Pallet was not found.");

        var slot = await dbContext.Slots
            .Include(x => x.Rack)
            .ThenInclude(x => x!.Area)
            .FirstOrDefaultAsync(x => x.Id == request.SlotId && x.TenantId == currentUser.TenantId, cancellationToken)
            ?? throw new InvalidOperationException("Slot was not found.");

        if (slot.Rack?.Area?.WarehouseId != pallet.WarehouseId)
        {
            throw new InvalidOperationException("Pallet and slot must belong to the same warehouse.");
        }

        var occupiedByAnotherPallet = await dbContext.Pallets.AnyAsync(
            x => x.TenantId == currentUser.TenantId &&
                 x.CurrentSlotId == slot.Id &&
                 x.Id != pallet.Id,
            cancellationToken);
        if (occupiedByAnotherPallet)
        {
            throw new InvalidOperationException("Target slot already has another pallet.");
        }

        if (pallet.CurrentSlotId.HasValue && pallet.CurrentSlotId.Value != slot.Id)
        {
            var previousSlot = await dbContext.Slots.FirstOrDefaultAsync(
                x => x.Id == pallet.CurrentSlotId.Value && x.TenantId == currentUser.TenantId,
                cancellationToken);

            if (previousSlot is not null)
            {
                var previousHasOtherPallets = await dbContext.Pallets.AnyAsync(
                    x => x.TenantId == currentUser.TenantId &&
                         x.CurrentSlotId == previousSlot.Id &&
                         x.Id != pallet.Id,
                    cancellationToken);
                previousSlot.IsOccupied = previousHasOtherPallets;
                previousSlot.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        pallet.CurrentSlotId = slot.Id;
        pallet.UpdatedAtUtc = DateTime.UtcNow;
        slot.IsOccupied = true;
        slot.UpdatedAtUtc = DateTime.UtcNow;

        await SaveAuditAsync("AssignPalletToSlot", nameof(Pallet), pallet.Id, $"{pallet.Code} -> {slot.Name}", cancellationToken);
    }

    public async Task<PalletDto> AddInitialInventoryAsync(AddInitialInventoryRequest request, CancellationToken cancellationToken)
    {
        if (request.Quantity <= 0)
        {
            throw new InvalidOperationException("Quantity must be greater than zero.");
        }

        var pallet = await dbContext.Pallets
            .Include(x => x.InventoryItems)
            .ThenInclude(x => x.Product)
            .FirstOrDefaultAsync(x => x.Id == request.PalletId && x.TenantId == currentUser.TenantId, cancellationToken)
            ?? throw new InvalidOperationException("Pallet was not found.");

        if (!pallet.CurrentSlotId.HasValue)
        {
            throw new InvalidOperationException("Place this pallet into a slot before adding initial stock.");
        }

        var productExists = await dbContext.Products.AnyAsync(
            x => x.Id == request.ProductId && x.TenantId == currentUser.TenantId,
            cancellationToken);
        if (!productExists)
        {
            throw new InvalidOperationException("Product was not found.");
        }

        var lotNumber = NormalizeOptional(request.LotNumber);
        var expiryDate = request.ExpiryDate?.Date;
        var existingItem = pallet.InventoryItems.FirstOrDefault(x =>
            x.ProductId == request.ProductId &&
            string.Equals(NormalizeOptional(x.LotNumber), lotNumber, StringComparison.OrdinalIgnoreCase) &&
            x.ExpiryDate?.Date == expiryDate);

        if (existingItem is null)
        {
            dbContext.InventoryItems.Add(new InventoryItem
            {
                TenantId = currentUser.TenantId,
                PalletId = pallet.Id,
                ProductId = request.ProductId,
                Quantity = request.Quantity,
                LotNumber = lotNumber,
                ExpiryDate = expiryDate
            });
        }
        else
        {
            existingItem.Quantity += request.Quantity;
            existingItem.UpdatedAtUtc = DateTime.UtcNow;
        }

        pallet.Status = PalletStatus.Occupied;
        pallet.UpdatedAtUtc = DateTime.UtcNow;

        await SaveAuditAsync("AddInitialInventory", nameof(Pallet), pallet.Id, $"{pallet.Code} qty {request.Quantity}", cancellationToken);

        var updatedPallet = await dbContext.Pallets
            .AsNoTracking()
            .Include(x => x.InventoryItems.OrderBy(i => i.ExpiryDate ?? DateTime.MaxValue))
            .ThenInclude(x => x.Product)
            .FirstAsync(x => x.Id == pallet.Id && x.TenantId == currentUser.TenantId, cancellationToken);

        return MapPallet(updatedPallet);
    }

    private async Task<ProductDto> ProjectProduct(Guid productId, CancellationToken cancellationToken)
    {
        return await dbContext.Products
            .AsNoTracking()
            .Where(x => x.Id == productId && x.TenantId == currentUser.TenantId)
            .Select(x => new ProductDto(
                x.Id,
                x.Sku,
                x.Name,
                x.Description,
                x.CategoryId,
                x.Category == null ? null : x.Category.Name,
                x.Brand))
            .FirstAsync(cancellationToken);
    }

    private static ProductCategoryDto MapCategory(ProductCategory category) =>
        new(category.Id, category.Code, category.Name, category.Description, category.IsActive);

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
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
                x.Quantity,
                x.LotNumber,
                x.ExpiryDate)).ToList());

    private static string? BuildExpiryNote(DateTime? expiryDate)
    {
        if (!expiryDate.HasValue)
        {
            return null;
        }

        var today = DateTime.UtcNow.Date;
        var expiry = expiryDate.Value.Date;
        if (expiry < today)
        {
            return "Da het han";
        }

        return expiry <= today.AddDays(30) ? "Sap het han" : null;
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
