namespace ASM.Domain.Enums;

public enum PalletStatus
{
    Empty = 1,
    Occupied = 2,
    Reserved = 3
}

public enum OrderStatus
{
    Draft = 1,
    Assigned = 2,
    InProgress = 3,
    Completed = 4,
    Cancelled = 5
}

public enum TaskType
{
    Inbound = 1,
    Outbound = 2
}

public enum TaskStatus
{
    Pending = 1,
    Verified = 2,
    Completed = 3,
    Failed = 4
}

public enum QrTargetType
{
    Warehouse = 1,
    Area = 2,
    Rack = 3,
    Slot = 4,
    Pallet = 5,
    Product = 6
}
