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

public record ProductLocationSearchResultDto(
    Guid ProductId,
    string Sku,
    string ProductName,
    Guid? InventoryItemId,
    int Quantity,
    Guid? PalletId,
    string? PalletCode,
    string? PalletStatus,
    Guid? SlotId,
    string? SlotName,
    string? RackName,
    string? AreaName,
    string? WarehouseName,
    string LocationPath,
    string? Note);
