namespace ASM.Application.Contracts;

public record DashboardSummaryDto(
    int WarehouseCount,
    int ProductCount,
    int ActivePalletCount,
    int PendingInboundCount,
    int PendingOutboundCount,
    int PendingTaskCount,
    IReadOnlyCollection<AlertDto> Alerts);

public record AlertDto(string Title, string Detail);

public record TeamActivityChartDto(
    Guid TenantId,
    string TenantName,
    Guid? WarehouseId,
    string WarehouseName,
    int Days,
    int ManagerMemberCount,
    int StaffMemberCount,
    int ManagerActionCount,
    int StaffActionCount,
    IReadOnlyCollection<TeamActivityWarehouseDto> Warehouses,
    IReadOnlyCollection<TeamActivityPointDto> Points);

public record TeamActivityWarehouseDto(
    Guid Id,
    string Name,
    string Code);

public record TeamActivityPointDto(
    string Label,
    int ManagerActions,
    int StaffActions);
