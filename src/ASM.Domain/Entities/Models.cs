using ASM.Domain.Enums;

namespace ASM.Domain.Entities;

public abstract class TenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}

public class Tenant : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public ICollection<AppUser> Users { get; set; } = [];
    public ICollection<Warehouse> Warehouses { get; set; } = [];
}

public class Warehouse : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public ICollection<Area> Areas { get; set; } = [];
    public ICollection<Pallet> Pallets { get; set; } = [];
    public ICollection<InboundOrder> InboundOrders { get; set; } = [];
    public ICollection<OutboundOrder> OutboundOrders { get; set; } = [];
}

public class Area : TenantEntity
{
    public Guid WarehouseId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Warehouse? Warehouse { get; set; }
    public ICollection<Rack> Racks { get; set; } = [];
}

public class Rack : TenantEntity
{
    public Guid AreaId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Area? Area { get; set; }
    public ICollection<Slot> Slots { get; set; } = [];
}

public class Slot : TenantEntity
{
    public Guid RackId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsOccupied { get; set; }
    public Rack? Rack { get; set; }
    public ICollection<Pallet> CurrentPallets { get; set; } = [];
}

public class Pallet : TenantEntity
{
    public Guid WarehouseId { get; set; }
    public Guid? CurrentSlotId { get; set; }
    public string Code { get; set; } = string.Empty;
    public PalletStatus Status { get; set; } = PalletStatus.Empty;
    public Warehouse? Warehouse { get; set; }
    public Slot? CurrentSlot { get; set; }
    public ICollection<InventoryItem> InventoryItems { get; set; } = [];
}

public class Product : TenantEntity
{
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ICollection<InventoryItem> InventoryItems { get; set; } = [];
}

public class InventoryItem : TenantEntity
{
    public Guid ProductId { get; set; }
    public Guid PalletId { get; set; }
    public int Quantity { get; set; }
    public Product? Product { get; set; }
    public Pallet? Pallet { get; set; }
}

public class QrCode : TenantEntity
{
    public QrTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public string Payload { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class ScanLog : TenantEntity
{
    public Guid TaskAssignmentId { get; set; }
    public Guid ScannedByUserId { get; set; }
    public string Payload { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public TaskAssignment? TaskAssignment { get; set; }
    public AppUser? ScannedByUser { get; set; }
}

public class InboundOrder : TenantEntity
{
    public Guid WarehouseId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid AssignedToUserId { get; set; }
    public string ReferenceCode { get; set; } = string.Empty;
    public OrderStatus Status { get; set; } = OrderStatus.Assigned;
    public Warehouse? Warehouse { get; set; }
    public AppUser? CreatedByUser { get; set; }
    public AppUser? AssignedToUser { get; set; }
    public ICollection<InboundOrderLine> Lines { get; set; } = [];
    public TaskAssignment? TaskAssignment { get; set; }
}

public class InboundOrderLine : TenantEntity
{
    public Guid InboundOrderId { get; set; }
    public Guid ProductId { get; set; }
    public Guid PalletId { get; set; }
    public Guid TargetSlotId { get; set; }
    public int Quantity { get; set; }
    public InboundOrder? InboundOrder { get; set; }
    public Product? Product { get; set; }
    public Pallet? Pallet { get; set; }
    public Slot? TargetSlot { get; set; }
}

public class OutboundOrder : TenantEntity
{
    public Guid WarehouseId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid AssignedToUserId { get; set; }
    public string ReferenceCode { get; set; } = string.Empty;
    public OrderStatus Status { get; set; } = OrderStatus.Assigned;
    public Warehouse? Warehouse { get; set; }
    public AppUser? CreatedByUser { get; set; }
    public AppUser? AssignedToUser { get; set; }
    public ICollection<OutboundOrderLine> Lines { get; set; } = [];
    public TaskAssignment? TaskAssignment { get; set; }
}

public class OutboundOrderLine : TenantEntity
{
    public Guid OutboundOrderId { get; set; }
    public Guid ProductId { get; set; }
    public Guid SourcePalletId { get; set; }
    public Guid SourceSlotId { get; set; }
    public int Quantity { get; set; }
    public OutboundOrder? OutboundOrder { get; set; }
    public Product? Product { get; set; }
    public Pallet? SourcePallet { get; set; }
    public Slot? SourceSlot { get; set; }
}

public class TaskAssignment : TenantEntity
{
    public Guid AssignedToUserId { get; set; }
    public TaskType TaskType { get; set; }
    public Guid? InboundOrderId { get; set; }
    public Guid? OutboundOrderId { get; set; }
    public QrTargetType ExpectedTargetType { get; set; }
    public Guid ExpectedTargetId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Instruction { get; set; } = string.Empty;
    public ASM.Domain.Enums.TaskStatus Status { get; set; } = ASM.Domain.Enums.TaskStatus.Pending;
    public DateTime? LastVerifiedAtUtc { get; set; }
    public AppUser? AssignedToUser { get; set; }
    public InboundOrder? InboundOrder { get; set; }
    public OutboundOrder? OutboundOrder { get; set; }
    public ICollection<ScanLog> ScanLogs { get; set; } = [];
}

public class AuditLog : TenantEntity
{
    public Guid PerformedByUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Detail { get; set; } = string.Empty;
}

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAtUtc { get; set; }
    public AppUser? User { get; set; }
    public bool IsActive => RevokedAtUtc is null && ExpiresAtUtc > DateTime.UtcNow;
}
