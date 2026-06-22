namespace ASM.Application.Contracts;

public record ProductCategoryDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive);

public record CreateCategoryRequest(string Code, string Name, string? Description);

public record UpdateCategoryRequest(Guid Id, string Code, string Name, string? Description, bool IsActive);

public record ProductDto(
    Guid Id,
    string Sku,
    string Name,
    string? Description,
    Guid? CategoryId,
    string? CategoryName,
    string? Brand);

public record CreateProductRequest(string Sku, string Name, string? Description, Guid? CategoryId, string? Brand);

public record PalletDto(
    Guid Id,
    Guid WarehouseId,
    Guid? CurrentSlotId,
    string Code,
    string Status,
    IReadOnlyCollection<InventoryItemDto> InventoryItems);

public record CreatePalletRequest(Guid WarehouseId, string Code);

public record UpdatePalletRequest(Guid Id, string Code);

public record AssignPalletSlotRequest(Guid PalletId, Guid SlotId);

public record AddInitialInventoryRequest(
    Guid PalletId,
    Guid ProductId,
    int Quantity,
    string? LotNumber,
    DateTime? ExpiryDate);

public record InventoryItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    int Quantity,
    string? LotNumber,
    DateTime? ExpiryDate);

public record ProductLocationSearchResultDto(
    Guid ProductId,
    string Sku,
    string ProductName,
    string? CategoryName,
    Guid? InventoryItemId,
    int Quantity,
    string? LotNumber,
    DateTime? ExpiryDate,
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
