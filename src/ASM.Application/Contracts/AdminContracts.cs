namespace ASM.Application.Contracts;

public record AdminDashboardSummaryDto(
    int TotalTenants,
    int TotalOwners,
    int ActiveOwnersLast7Days,
    int TotalAuditEventsLast7Days,
    int TotalOrdersLast7Days,
    int TotalScansLast7Days,
    IReadOnlyCollection<AlertDto> Alerts);

public record OwnerUsageSummaryDto(
    Guid OwnerUserId,
    string OwnerUserName,
    string OwnerFullName,
    string OwnerEmail,
    Guid TenantId,
    string TenantName,
    bool IsActive,
    int WarehouseCount,
    int TotalUsers,
    int OwnerActionsLast7Days,
    int TenantOrdersLast7Days,
    int TenantScansLast7Days,
    DateTime? LastActivityAtUtc,
    DateTime? LastLoginAtUtc,
    string TopAction);

public record OwnerActivityDto(
    DateTime HappenedAtUtc,
    string ActorName,
    string Action,
    string EntityName,
    string Detail);

public record UsageFrequencyPointDto(
    string Label,
    int Count);

public record OwnerUsageDetailDto(
    OwnerUsageSummaryDto Summary,
    IReadOnlyCollection<OwnerActivityDto> RecentActivities,
    IReadOnlyCollection<UsageFrequencyPointDto> DailyActivity);

public record OwnerEditDto(
    Guid OwnerUserId,
    string OwnerUserName,
    string OwnerFullName,
    string OwnerEmail,
    string? OwnerPhoneNumber,
    string TenantName,
    bool IsActive);

public record UpdateOwnerProfileRequest(
    Guid OwnerUserId,
    string OwnerUserName,
    string OwnerFullName,
    string OwnerEmail,
    string? OwnerPhoneNumber,
    string TenantName,
    bool IsActive);
