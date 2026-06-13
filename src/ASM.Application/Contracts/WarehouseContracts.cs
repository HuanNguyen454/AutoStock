namespace ASM.Application.Contracts;

public record WarehouseDto(Guid Id, string Name, string Code, string Address);

public record CreateWarehouseRequest(string Name, string Code, string Address);

public record AreaDto(Guid Id, Guid WarehouseId, string Name);

public record CreateAreaRequest(Guid WarehouseId, string Name);

public record RackDto(Guid Id, Guid AreaId, string Name);

public record CreateRackRequest(Guid AreaId, string Name);

public record SlotDto(Guid Id, Guid RackId, string Name, bool IsOccupied);

public record CreateSlotRequest(Guid RackId, string Name);

public record WarehouseLayoutDto(
    WarehouseDto Warehouse,
    IReadOnlyCollection<AreaDetailDto> Areas);

public record AreaDetailDto(
    Guid Id,
    string Name,
    IReadOnlyCollection<RackDetailDto> Racks);

public record RackDetailDto(
    Guid Id,
    string Name,
    IReadOnlyCollection<SlotDto> Slots);
