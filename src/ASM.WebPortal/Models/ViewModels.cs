using ASM.Application.Contracts;
using ASM.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace ASM.WebPortal.Models;

public class LoginViewModel
{
    [Required]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }
    public string? ReturnUrl { get; set; }
}

public class DashboardPageViewModel
{
    public DashboardSummaryDto Summary { get; set; } = new(0, 0, 0, 0, 0, 0, []);
    public IReadOnlyCollection<InboundOrderDto> InboundOrders { get; set; } = [];
    public IReadOnlyCollection<OutboundOrderDto> OutboundOrders { get; set; } = [];
    public IReadOnlyCollection<DashboardWarehouseMapViewModel> WarehouseMaps { get; set; } = [];
}

public class DashboardWarehouseMapViewModel
{
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public string WarehouseCode { get; set; } = string.Empty;
    public int AreaCount { get; set; }
    public int RackCount { get; set; }
    public int SlotCount { get; set; }
    public int PalletCount { get; set; }
    public int ProductCount { get; set; }
    public IReadOnlyCollection<DashboardMapAreaViewModel> Areas { get; set; } = [];
}

public class DashboardMapAreaViewModel
{
    public string Name { get; set; } = string.Empty;
    public int OccupiedSlotCount { get; set; }
    public IReadOnlyCollection<DashboardMapRackViewModel> Racks { get; set; } = [];
}

public class DashboardMapRackViewModel
{
    public string Name { get; set; } = string.Empty;
    public IReadOnlyCollection<DashboardMapSlotViewModel> Slots { get; set; } = [];
}

public class DashboardMapSlotViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsOccupied { get; set; }
    public IReadOnlyCollection<DashboardMapPalletViewModel> Pallets { get; set; } = [];
}

public class DashboardMapPalletViewModel
{
    public string Code { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public IReadOnlyCollection<DashboardMapProductViewModel> Products { get; set; } = [];
}

public class DashboardMapProductViewModel
{
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string? LotNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
}

public class AdminDashboardPageViewModel
{
    public AdminDashboardSummaryDto Summary { get; set; } = new(0, 0, 0, 0, 0, 0, []);
    public IReadOnlyCollection<OwnerUsageSummaryDto> Owners { get; set; } = [];
}

public class AdminOwnersPageViewModel
{
    public IReadOnlyCollection<OwnerUsageSummaryDto> Owners { get; set; } = [];
}

public class AdminOwnerDetailPageViewModel
{
    public OwnerUsageDetailDto Detail { get; set; } = default!;
    public AdminOwnerEditViewModel Edit { get; set; } = new();
}

public class AdminOwnerEditViewModel
{
    [Required]
    public Guid OwnerUserId { get; set; }

    [Required]
    public string OwnerUserName { get; set; } = string.Empty;

    [Required]
    public string OwnerFullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string OwnerEmail { get; set; } = string.Empty;

    public string? OwnerPhoneNumber { get; set; }

    [Required]
    public string TenantName { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}

public class WarehousesPageViewModel
{
    public IReadOnlyCollection<WarehouseDto> Warehouses { get; set; } = [];
    public IReadOnlyDictionary<Guid, QrCodeDto> QrCodes { get; set; } = new Dictionary<Guid, QrCodeDto>();
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
}

public class LayoutPageViewModel
{
    public IReadOnlyCollection<WarehouseDto> Warehouses { get; set; } = [];
    public WarehouseLayoutDto? Layout { get; set; }
    public IReadOnlyCollection<PalletDto> Pallets { get; set; } = [];
    public IReadOnlyDictionary<Guid, QrCodeDto> QrCodes { get; set; } = new Dictionary<Guid, QrCodeDto>();
    public Guid SelectedWarehouseId { get; set; }
    public string AreaName { get; set; } = string.Empty;
    public Guid AreaId { get; set; }
    public string RackName { get; set; } = string.Empty;
    public Guid RackId { get; set; }
    public string SlotName { get; set; } = string.Empty;
    public string PalletCode { get; set; } = string.Empty;
}

public class ProductsPageViewModel
{
    public IReadOnlyCollection<ProductDto> Products { get; set; } = [];
    public IReadOnlyCollection<ProductCategoryDto> Categories { get; set; } = [];
    public IReadOnlyDictionary<Guid, QrCodeDto> QrCodes { get; set; } = new Dictionary<Guid, QrCodeDto>();
    public string? Keyword { get; set; }
    public Guid? CategoryId { get; set; }
    public bool HasSearched { get; set; }
    public IReadOnlyList<ProductLocationSearchResultDto> LocationResults { get; set; } = [];
    public IReadOnlyList<ProductWarehouseMapViewModel> WarehouseMaps { get; set; } = [];
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Brand { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string? CategoryDescription { get; set; }
}

public class ProductWarehouseMapViewModel
{
    public string WarehouseName { get; set; } = string.Empty;
    public string WarehouseCode { get; set; } = string.Empty;
    public IReadOnlyCollection<string> RouteSummaries { get; set; } = [];
    public IReadOnlyCollection<ProductMapAreaViewModel> Areas { get; set; } = [];
}

public class ProductMapAreaViewModel
{
    public string Name { get; set; } = string.Empty;
    public int HighlightedSlotCount { get; set; }
    public IReadOnlyCollection<ProductMapRackViewModel> Racks { get; set; } = [];
}

public class ProductMapRackViewModel
{
    public string Name { get; set; } = string.Empty;
    public IReadOnlyCollection<ProductMapSlotViewModel> Slots { get; set; } = [];
}

public class ProductMapSlotViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsOccupied { get; set; }
    public bool HasMatch { get; set; }
    public string MatchSummary { get; set; } = string.Empty;
    public string QuantitySummary { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
}

public class PalletsPageViewModel
{
    public IReadOnlyCollection<PalletDto> Pallets { get; set; } = [];
    public IReadOnlyCollection<WarehouseDto> Warehouses { get; set; } = [];
    public IReadOnlyDictionary<Guid, QrCodeDto> QrCodes { get; set; } = new Dictionary<Guid, QrCodeDto>();
    public Guid WarehouseId { get; set; }
    public string Code { get; set; } = string.Empty;
}

public class UsersPageViewModel
{
    public IReadOnlyCollection<UserDto> Users { get; set; } = [];
    public string UserName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = "Staff";
}

public class QrPageViewModel
{
    public Guid TargetId { get; set; }
    public QrTargetType TargetType { get; set; } = QrTargetType.Pallet;
    public string Label { get; set; } = string.Empty;
    public QrCodeDto? GeneratedQr { get; set; }
}

public class InboundPageViewModel
{
    public IReadOnlyCollection<InboundOrderDto> Orders { get; set; } = [];
    public IReadOnlyCollection<WarehouseDto> Warehouses { get; set; } = [];
    public IReadOnlyCollection<ProductDto> Products { get; set; } = [];
    public IReadOnlyCollection<UserDto> Users { get; set; } = [];
    public IReadOnlyCollection<HierarchySelectOptionViewModel> SlotOptions { get; set; } = [];
    public IReadOnlyCollection<HierarchySelectOptionViewModel> PalletOptions { get; set; } = [];
    public Guid WarehouseId { get; set; }
    public Guid ProductId { get; set; }
    public Guid PalletId { get; set; }
    public Guid TargetSlotId { get; set; }
    public Guid AssignedToUserId { get; set; }
    public int Quantity { get; set; }
    public string? LotNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string ReferenceCode { get; set; } = string.Empty;
}

public class OutboundPageViewModel
{
    public IReadOnlyCollection<OutboundOrderDto> Orders { get; set; } = [];
    public IReadOnlyCollection<WarehouseDto> Warehouses { get; set; } = [];
    public IReadOnlyCollection<ProductDto> Products { get; set; } = [];
    public IReadOnlyCollection<UserDto> Users { get; set; } = [];
    public IReadOnlyCollection<HierarchySelectOptionViewModel> SlotOptions { get; set; } = [];
    public IReadOnlyCollection<HierarchySelectOptionViewModel> PalletOptions { get; set; } = [];
    public Guid WarehouseId { get; set; }
    public Guid ProductId { get; set; }
    public Guid SourcePalletId { get; set; }
    public Guid SourceSlotId { get; set; }
    public Guid AssignedToUserId { get; set; }
    public int Quantity { get; set; }
    public string ReferenceCode { get; set; } = string.Empty;
}

public class HierarchySelectOptionViewModel
{
    public Guid Id { get; set; }
    public Guid WarehouseId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
}
