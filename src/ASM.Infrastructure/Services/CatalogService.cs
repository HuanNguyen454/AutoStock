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
