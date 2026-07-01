using ASM.Application.Contracts;
using ASM.Application.Interfaces;
using ASM.Domain.Constants;
using ASM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ASM.Infrastructure.Services;

public class TeamActivityService(
    AppDbContext dbContext,
    ICurrentUserService currentUser) : ITeamActivityService
{
    public async Task<TeamActivityChartDto> GetForCurrentOwnerAsync(
        Guid? warehouseId,
        int days,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsInRole(RoleNames.Owner))
        {
            throw new InvalidOperationException("Only Owner can view team activity for the current organization.");
        }

        return await BuildChartAsync(currentUser.TenantId, warehouseId, days, cancellationToken);
    }

    public async Task<TeamActivityChartDto?> GetForOwnerAsync(
        Guid ownerUserId,
        Guid? warehouseId,
        int days,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsInRole(RoleNames.Admin))
        {
            throw new InvalidOperationException("Only Admin can compare team activity by Owner.");
        }

        var ownerRoleId = await dbContext.Roles
            .Where(x => x.NormalizedName == RoleNames.Owner.ToUpperInvariant())
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var owner = await dbContext.Users
            .AsNoTracking()
            .Where(x => x.Id == ownerUserId)
            .Select(x => new { x.Id, x.TenantId })
            .FirstOrDefaultAsync(cancellationToken);

        if (owner is null || ownerRoleId == Guid.Empty || !await dbContext.UserRoles
                .AnyAsync(x => x.UserId == owner.Id && x.RoleId == ownerRoleId, cancellationToken))
        {
            return null;
        }

        return await BuildChartAsync(owner.TenantId, warehouseId, days, cancellationToken);
    }

    private async Task<TeamActivityChartDto> BuildChartAsync(
        Guid tenantId,
        Guid? requestedWarehouseId,
        int requestedDays,
        CancellationToken cancellationToken)
    {
        var days = requestedDays == 30 ? 30 : 7;
        var today = DateTime.UtcNow.Date;
        var sinceUtc = today.AddDays(-(days - 1));

        var tenantName = await dbContext.Tenants
            .Where(x => x.Id == tenantId)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken)
            ?? "Unknown tenant";

        var warehouses = await dbContext.Warehouses
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Name)
            .Select(x => new TeamActivityWarehouseDto(x.Id, x.Name, x.Code))
            .ToListAsync(cancellationToken);
        var selectedWarehouse = warehouses.FirstOrDefault(x => x.Id == requestedWarehouseId);
        var warehouseId = selectedWarehouse?.Id;

        var roleIds = await dbContext.Roles
            .Where(x => x.NormalizedName == RoleNames.Manager.ToUpperInvariant()
                || x.NormalizedName == RoleNames.Staff.ToUpperInvariant())
            .Select(x => new { x.Id, x.NormalizedName })
            .ToListAsync(cancellationToken);

        var managerRoleId = roleIds
            .FirstOrDefault(x => x.NormalizedName == RoleNames.Manager.ToUpperInvariant())?.Id ?? Guid.Empty;
        var staffRoleId = roleIds
            .FirstOrDefault(x => x.NormalizedName == RoleNames.Staff.ToUpperInvariant())?.Id ?? Guid.Empty;

        var tenantUserIds = await dbContext.Users
            .Where(x => x.TenantId == tenantId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var managerIds = managerRoleId == Guid.Empty
            ? []
            : await dbContext.UserRoles
                .Where(x => tenantUserIds.Contains(x.UserId) && x.RoleId == managerRoleId)
                .Select(x => x.UserId)
                .ToListAsync(cancellationToken);
        var staffIds = staffRoleId == Guid.Empty
            ? []
            : await dbContext.UserRoles
                .Where(x => tenantUserIds.Contains(x.UserId) && x.RoleId == staffRoleId)
                .Select(x => x.UserId)
                .ToListAsync(cancellationToken);

        var teamUserIds = managerIds.Concat(staffIds).Distinct().ToArray();
        var auditRows = teamUserIds.Length == 0
            ? []
            : await dbContext.AuditLogs
                .Where(x => x.TenantId == tenantId
                    && x.CreatedAtUtc >= sinceUtc
                    && teamUserIds.Contains(x.PerformedByUserId))
                .Select(x => new TeamAuditEvent(
                    x.PerformedByUserId,
                    x.CreatedAtUtc,
                    x.EntityName,
                    x.EntityId))
                .ToListAsync(cancellationToken);

        var auditEvents = warehouseId.HasValue
            ? await FilterAuditEventsByWarehouseAsync(auditRows, warehouseId.Value, cancellationToken)
            : auditRows.Select(x => new TeamActivityEvent(x.UserId, x.HappenedAtUtc)).ToList();

        var taskIds = warehouseId.HasValue
            ? await dbContext.TaskAssignments
                .Where(x => x.TenantId == tenantId
                    && ((x.InboundOrder != null && x.InboundOrder.WarehouseId == warehouseId.Value)
                        || (x.OutboundOrder != null && x.OutboundOrder.WarehouseId == warehouseId.Value)))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken)
            : [];

        var scanEvents = teamUserIds.Length == 0
            ? []
            : await dbContext.ScanLogs
                .Where(x => x.TenantId == tenantId
                    && x.CreatedAtUtc >= sinceUtc
                    && teamUserIds.Contains(x.ScannedByUserId)
                    && (!warehouseId.HasValue || taskIds.Contains(x.TaskAssignmentId)))
                .Select(x => new TeamActivityEvent(x.ScannedByUserId, x.CreatedAtUtc))
                .ToListAsync(cancellationToken);

        var events = auditEvents.Concat(scanEvents).ToList();
        var managerIdSet = managerIds.ToHashSet();
        var staffIdSet = staffIds.ToHashSet();
        var points = Enumerable.Range(0, days)
            .Select(offset => sinceUtc.AddDays(offset))
            .Select(date => new TeamActivityPointDto(
                date.ToString("dd/MM"),
                events.Count(x => managerIdSet.Contains(x.UserId) && x.HappenedAtUtc.Date == date),
                events.Count(x => staffIdSet.Contains(x.UserId) && x.HappenedAtUtc.Date == date)))
            .ToList();

        return new TeamActivityChartDto(
            tenantId,
            tenantName,
            warehouseId,
            selectedWarehouse?.Name ?? "All warehouses",
            days,
            managerIds.Count,
            staffIds.Count,
            points.Sum(x => x.ManagerActions),
            points.Sum(x => x.StaffActions),
            warehouses,
            points);
    }

    private async Task<List<TeamActivityEvent>> FilterAuditEventsByWarehouseAsync(
        IReadOnlyCollection<TeamAuditEvent> auditRows,
        Guid warehouseId,
        CancellationToken cancellationToken)
    {
        var areaIds = (await dbContext.Areas
            .Where(x => x.WarehouseId == warehouseId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken)).ToHashSet();
        var rackIds = (await dbContext.Racks
            .Where(x => areaIds.Contains(x.AreaId))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken)).ToHashSet();
        var slotIds = (await dbContext.Slots
            .Where(x => rackIds.Contains(x.RackId))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken)).ToHashSet();
        var palletIds = (await dbContext.Pallets
            .Where(x => x.WarehouseId == warehouseId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken)).ToHashSet();
        var inboundOrderIds = (await dbContext.InboundOrders
            .Where(x => x.WarehouseId == warehouseId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken)).ToHashSet();
        var outboundOrderIds = (await dbContext.OutboundOrders
            .Where(x => x.WarehouseId == warehouseId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken)).ToHashSet();

        return auditRows
            .Where(x => x.EntityName switch
            {
                nameof(ASM.Domain.Entities.Warehouse) => x.EntityId == warehouseId,
                nameof(ASM.Domain.Entities.Area) => areaIds.Contains(x.EntityId),
                nameof(ASM.Domain.Entities.Rack) => rackIds.Contains(x.EntityId),
                nameof(ASM.Domain.Entities.Slot) => slotIds.Contains(x.EntityId),
                nameof(ASM.Domain.Entities.Pallet) => palletIds.Contains(x.EntityId),
                nameof(ASM.Domain.Entities.InboundOrder) => inboundOrderIds.Contains(x.EntityId),
                nameof(ASM.Domain.Entities.OutboundOrder) => outboundOrderIds.Contains(x.EntityId),
                _ => false
            })
            .Select(x => new TeamActivityEvent(x.UserId, x.HappenedAtUtc))
            .ToList();
    }

    private sealed record TeamActivityEvent(Guid UserId, DateTime HappenedAtUtc);
    private sealed record TeamAuditEvent(
        Guid UserId,
        DateTime HappenedAtUtc,
        string EntityName,
        Guid EntityId);
}
