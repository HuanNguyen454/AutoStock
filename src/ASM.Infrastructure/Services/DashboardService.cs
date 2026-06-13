using ASM.Application.Contracts;
using ASM.Application.Interfaces;
using ASM.Domain.Enums;
using ASM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using TaskWorkflowStatus = ASM.Domain.Enums.TaskStatus;

namespace ASM.Infrastructure.Services;

public class DashboardService(
    AppDbContext dbContext,
    ICurrentUserService currentUser) : IDashboardService
{
    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var warehouseCount = await dbContext.Warehouses.CountAsync(x => x.TenantId == currentUser.TenantId, cancellationToken);
        var productCount = await dbContext.Products.CountAsync(x => x.TenantId == currentUser.TenantId, cancellationToken);
        var activePalletCount = await dbContext.Pallets.CountAsync(x => x.TenantId == currentUser.TenantId && x.Status == PalletStatus.Occupied, cancellationToken);
        var pendingInboundCount = await dbContext.InboundOrders.CountAsync(x => x.TenantId == currentUser.TenantId && x.Status != OrderStatus.Completed, cancellationToken);
        var pendingOutboundCount = await dbContext.OutboundOrders.CountAsync(x => x.TenantId == currentUser.TenantId && x.Status != OrderStatus.Completed, cancellationToken);

        var taskQuery = dbContext.TaskAssignments.Where(x => x.TenantId == currentUser.TenantId);
        if (currentUser.IsInRole("Staff"))
        {
            taskQuery = taskQuery.Where(x => x.AssignedToUserId == currentUser.UserId);
        }

        var pendingTaskCount = await taskQuery.CountAsync(x => x.Status != TaskWorkflowStatus.Completed, cancellationToken);

        var alerts = await dbContext.ScanLogs
            .Where(x => x.TenantId == currentUser.TenantId && !x.IsSuccess)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(5)
            .Select(x => new AlertDto("Scan mismatch", x.Message))
            .ToListAsync(cancellationToken);

        return new DashboardSummaryDto(
            warehouseCount,
            productCount,
            activePalletCount,
            pendingInboundCount,
            pendingOutboundCount,
            pendingTaskCount,
            alerts);
    }
}
