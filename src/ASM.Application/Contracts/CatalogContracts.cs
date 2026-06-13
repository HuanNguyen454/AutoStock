namespace ASM.Application.Contracts;

public record ProductDto(Guid Id, string Sku, string Name, string? Description);

public record CreateProductRequest(string Sku, string Name, string? Description);

public record PalletDto(
    Guid Id,
    Guid WarehouseId,
    Guid? CurrentSlotId,
    string Code,
    string Status,
    IReadOnlyCollection<InventoryItemDto> InventoryItems);

public record CreatePalletRequest(Guid WarehouseId, string Code);

public record InventoryItemDto(Guid Id, Guid ProductId, string ProductName, int Quantity);
