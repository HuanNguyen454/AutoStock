using ASM.Domain.Enums;

namespace ASM.Application.Contracts;

public record QrCodeDto(
    Guid Id,
    QrTargetType TargetType,
    Guid TargetId,
    string Payload,
    string Label);

public record CreateQrRequest(QrTargetType TargetType, Guid TargetId, string Label);

public record QrLookupResultDto(
    QrTargetType TargetType,
    Guid TargetId,
    string TargetName,
    QrLookupWarehouseDto Warehouse,
    string LocationPath,
    IReadOnlyCollection<QrLookupAreaDto> Areas,
    IReadOnlyCollection<QrLookupPalletDto> UnplacedPallets);

public record QrLookupWarehouseDto(
    Guid Id,
    string Name,
    string Code,
    string Address);

public record QrLookupAreaDto(
    Guid Id,
    string Name,
    IReadOnlyCollection<QrLookupRackDto> Racks);

public record QrLookupRackDto(
    Guid Id,
    string Name,
    IReadOnlyCollection<QrLookupSlotDto> Slots);

public record QrLookupSlotDto(
    Guid Id,
    string Name,
    bool IsOccupied,
    IReadOnlyCollection<QrLookupPalletDto> Pallets);

public record QrLookupPalletDto(
    Guid Id,
    string Code,
    string Status,
    IReadOnlyCollection<QrLookupInventoryDto> InventoryItems);

public record QrLookupInventoryDto(
    Guid Id,
    string Sku,
    string ProductName,
    string? CategoryName,
    string? Brand,
    int Quantity,
    string? LotNumber,
    DateTime? ExpiryDate);
