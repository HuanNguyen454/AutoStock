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
