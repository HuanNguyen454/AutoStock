using ASM.Application.Contracts;
using ASM.Application.Interfaces;
using ASM.Domain.Constants;
using ASM.Domain.Entities;
using ASM.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ASM.Infrastructure.Services;

public class AdminService(
    AppDbContext dbContext,
    UserManager<AppUser> userManager,
    ICurrentUserService currentUser) : IAdminService
{
    public async Task<AdminDashboardSummaryDto> GetDashboardAsync(CancellationToken cancellationToken)
    {
        EnsureAdmin();

        var last7Days = DateTime.UtcNow.AddDays(-7);
        var owners = await userManager.GetUsersInRoleAsync(RoleNames.Owner);
        var ownerIds = owners.Select(x => x.Id).ToArray();

        var activeOwnersLast7Days = ownerIds.Length == 0
            ? 0
            : await dbContext.AuditLogs
                .Where(x => ownerIds.Contains(x.PerformedByUserId) && x.CreatedAtUtc >= last7Days)
                .Select(x => x.PerformedByUserId)
                .Distinct()
                .CountAsync(cancellationToken);

        var totalOrdersLast7Days =
            await dbContext.InboundOrders.CountAsync(x => x.CreatedAtUtc >= last7Days, cancellationToken) +
            await dbContext.OutboundOrders.CountAsync(x => x.CreatedAtUtc >= last7Days, cancellationToken);

        var alerts = await dbContext.ScanLogs
            .Where(x => !x.IsSuccess)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(6)
            .Select(x => new AlertDto("Platform scan mismatch", x.Message))
            .ToListAsync(cancellationToken);

        return new AdminDashboardSummaryDto(
            await dbContext.Tenants.CountAsync(cancellationToken),
            owners.Count,
            activeOwnersLast7Days,
            await dbContext.AuditLogs.CountAsync(x => x.CreatedAtUtc >= last7Days, cancellationToken),
            totalOrdersLast7Days,
            await dbContext.ScanLogs.CountAsync(x => x.CreatedAtUtc >= last7Days, cancellationToken),
            alerts);
    }

    public async Task<IReadOnlyCollection<OwnerUsageSummaryDto>> GetOwnersAsync(CancellationToken cancellationToken)
    {
        EnsureAdmin();

        var owners = await userManager.GetUsersInRoleAsync(RoleNames.Owner);
        if (owners.Count == 0)
        {
            return [];
        }

        var ownerIds = owners.Select(x => x.Id).ToArray();
        var tenantIds = owners.Select(x => x.TenantId).Distinct().ToArray();
        var last7Days = DateTime.UtcNow.AddDays(-7);
        var last30Days = DateTime.UtcNow.AddDays(-30);

        var tenants = await dbContext.Tenants
            .Where(x => tenantIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var warehouseCounts = await dbContext.Warehouses
            .Where(x => tenantIds.Contains(x.TenantId))
            .GroupBy(x => x.TenantId)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        var userCounts = await userManager.Users
            .Where(x => tenantIds.Contains(x.TenantId))
            .GroupBy(x => x.TenantId)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        var ownerActionCounts = await dbContext.AuditLogs
            .Where(x => ownerIds.Contains(x.PerformedByUserId) && x.CreatedAtUtc >= last7Days)
            .GroupBy(x => x.PerformedByUserId)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        var tenantOrderCounts =
            (await dbContext.InboundOrders
                .Where(x => tenantIds.Contains(x.TenantId) && x.CreatedAtUtc >= last7Days)
                .GroupBy(x => x.TenantId)
                .Select(x => new { x.Key, Count = x.Count() })
                .ToListAsync(cancellationToken))
            .Concat(await dbContext.OutboundOrders
                .Where(x => tenantIds.Contains(x.TenantId) && x.CreatedAtUtc >= last7Days)
                .GroupBy(x => x.TenantId)
                .Select(x => new { x.Key, Count = x.Count() })
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.Key)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Count));

        var tenantScanCounts = await dbContext.ScanLogs
            .Where(x => tenantIds.Contains(x.TenantId) && x.CreatedAtUtc >= last7Days)
            .GroupBy(x => x.TenantId)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        var tenantLastActivity = await dbContext.AuditLogs
            .Where(x => tenantIds.Contains(x.TenantId))
            .GroupBy(x => x.TenantId)
            .Select(x => new { x.Key, LastActivityAtUtc = x.Max(y => y.CreatedAtUtc) })
            .ToDictionaryAsync(x => x.Key, x => (DateTime?)x.LastActivityAtUtc, cancellationToken);

        var lastLogins = await dbContext.RefreshTokens
            .Where(x => ownerIds.Contains(x.UserId))
            .GroupBy(x => x.UserId)
            .Select(x => new { x.Key, LastLoginAtUtc = x.Max(y => y.CreatedAtUtc) })
            .ToDictionaryAsync(x => x.Key, x => (DateTime?)x.LastLoginAtUtc, cancellationToken);

        var topActions = await dbContext.AuditLogs
            .Where(x => ownerIds.Contains(x.PerformedByUserId) && x.CreatedAtUtc >= last30Days)
            .GroupBy(x => new { x.PerformedByUserId, x.Action })
            .Select(x => new { x.Key.PerformedByUserId, x.Key.Action, Count = x.Count() })
            .ToListAsync(cancellationToken);

        return owners
            .OrderBy(x => x.FullName)
            .Select(owner =>
            {
                var topAction = topActions
                    .Where(x => x.PerformedByUserId == owner.Id)
                    .OrderByDescending(x => x.Count)
                    .Select(x => x.Action)
                    .FirstOrDefault() ?? "No recent action";

                return new OwnerUsageSummaryDto(
                    owner.Id,
                    owner.UserName ?? string.Empty,
                    owner.FullName,
                    owner.Email ?? string.Empty,
                    owner.TenantId,
                    tenants.GetValueOrDefault(owner.TenantId)?.Name ?? "Unknown tenant",
                    owner.IsActive,
                    warehouseCounts.GetValueOrDefault(owner.TenantId),
                    userCounts.GetValueOrDefault(owner.TenantId),
                    ownerActionCounts.GetValueOrDefault(owner.Id),
                    tenantOrderCounts.GetValueOrDefault(owner.TenantId),
                    tenantScanCounts.GetValueOrDefault(owner.TenantId),
                    tenantLastActivity.GetValueOrDefault(owner.TenantId),
                    lastLogins.GetValueOrDefault(owner.Id),
                    topAction);
            })
            .ToList();
    }

    public async Task<OwnerUsageDetailDto?> GetOwnerDetailsAsync(Guid ownerUserId, CancellationToken cancellationToken)
    {
        EnsureAdmin();

        var owner = await userManager.Users.FirstOrDefaultAsync(x => x.Id == ownerUserId, cancellationToken);
        if (owner is null || !(await userManager.IsInRoleAsync(owner, RoleNames.Owner)))
        {
            return null;
        }

        var summary = (await GetOwnersAsync(cancellationToken)).FirstOrDefault(x => x.OwnerUserId == ownerUserId);
        if (summary is null)
        {
            return null;
        }

        var userLookup = await userManager.Users
            .Where(x => x.TenantId == owner.TenantId)
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var recentActivities = await dbContext.AuditLogs
            .Where(x => x.TenantId == owner.TenantId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(20)
            .Select(x => new
            {
                x.CreatedAtUtc,
                x.PerformedByUserId,
                x.Action,
                x.EntityName,
                x.Detail
            })
            .ToListAsync(cancellationToken);

        var activityPoints = (await dbContext.AuditLogs
            .Where(x => x.TenantId == owner.TenantId && x.CreatedAtUtc >= DateTime.UtcNow.AddDays(-7))
            .GroupBy(x => x.CreatedAtUtc.Date)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToListAsync(cancellationToken))
            .OrderBy(x => x.Key)
            .Select(x => new UsageFrequencyPointDto(x.Key.ToString("dd/MM"), x.Count))
            .ToList();

        return new OwnerUsageDetailDto(
            summary,
            recentActivities.Select(x => new OwnerActivityDto(
                x.CreatedAtUtc,
                userLookup.GetValueOrDefault(x.PerformedByUserId)?.FullName ?? "Unknown user",
                x.Action,
                x.EntityName,
                x.Detail)).ToList(),
            activityPoints);
    }

    public async Task<OwnerEditDto?> GetOwnerEditAsync(Guid ownerUserId, CancellationToken cancellationToken)
    {
        EnsureAdmin();

        var owner = await userManager.Users
            .Include(x => x.Tenant)
            .FirstOrDefaultAsync(x => x.Id == ownerUserId, cancellationToken);

        if (owner is null || !(await userManager.IsInRoleAsync(owner, RoleNames.Owner)))
        {
            return null;
        }

        return new OwnerEditDto(
            owner.Id,
            owner.UserName ?? string.Empty,
            owner.FullName,
            owner.Email ?? string.Empty,
            owner.PhoneNumber,
            owner.Tenant?.Name ?? string.Empty,
            owner.IsActive);
    }

    public async Task UpdateOwnerAsync(UpdateOwnerProfileRequest request, CancellationToken cancellationToken)
    {
        EnsureAdmin();

        var owner = await userManager.Users
            .Include(x => x.Tenant)
            .FirstOrDefaultAsync(x => x.Id == request.OwnerUserId, cancellationToken)
            ?? throw new InvalidOperationException("Owner was not found.");

        if (!(await userManager.IsInRoleAsync(owner, RoleNames.Owner)))
        {
            throw new InvalidOperationException("Selected user is not an owner.");
        }

        owner.FullName = request.OwnerFullName.Trim();
        owner.UserName = request.OwnerUserName.Trim();
        owner.Email = request.OwnerEmail.Trim();
        owner.PhoneNumber = string.IsNullOrWhiteSpace(request.OwnerPhoneNumber) ? null : request.OwnerPhoneNumber.Trim();
        owner.IsActive = request.IsActive;

        var updateUserResult = await userManager.UpdateAsync(owner);
        if (!updateUserResult.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", updateUserResult.Errors.Select(x => x.Description)));
        }

        if (owner.Tenant is not null)
        {
            owner.Tenant.Name = request.TenantName.Trim();
            owner.Tenant.UpdatedAtUtc = DateTime.UtcNow;
        }

        dbContext.AuditLogs.Add(new AuditLog
        {
            TenantId = owner.TenantId,
            PerformedByUserId = currentUser.UserId,
            Action = "UpdateOwnerProfile",
            EntityName = nameof(AppUser),
            EntityId = owner.Id,
            Detail = $"Updated owner {owner.UserName}"
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private void EnsureAdmin()
    {
        if (!currentUser.IsInRole(RoleNames.Admin))
        {
            throw new InvalidOperationException("Only Admin can access platform administration.");
        }
    }
}
