using ASM.Domain.Constants;
using ASM.Domain.Entities;
using ASM.Infrastructure.Persistence;
using ASM.Infrastructure.Services;
using ASM.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ASM.Tests;

public class TeamActivityServiceTests
{
    [Fact]
    public async Task GetForCurrentOwnerAsync_CountsOnlyOwnManagerAndStaffActivity()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedAsync(dbContext);
        var currentUser = new TestCurrentUserService
        {
            UserId = seed.Owner.Id,
            TenantId = seed.Tenant.Id,
            UserName = seed.Owner.UserName!,
            Role = RoleNames.Owner
        };
        var service = new TeamActivityService(dbContext, currentUser);

        var chart = await service.GetForCurrentOwnerAsync(null, 7, CancellationToken.None);

        Assert.Equal(seed.Tenant.Id, chart.TenantId);
        Assert.Equal(1, chart.ManagerMemberCount);
        Assert.Equal(1, chart.StaffMemberCount);
        Assert.Equal(2, chart.ManagerActionCount);
        Assert.Equal(2, chart.StaffActionCount);
        Assert.Equal(7, chart.Points.Count);
        Assert.Equal(4, chart.Points.Sum(x => x.ManagerActions + x.StaffActions));
    }

    [Fact]
    public async Task GetForCurrentOwnerAsync_WarehouseFilterExcludesOtherWarehouseActivity()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedAsync(dbContext);
        var currentUser = new TestCurrentUserService
        {
            UserId = seed.Owner.Id,
            TenantId = seed.Tenant.Id,
            UserName = seed.Owner.UserName!,
            Role = RoleNames.Owner
        };
        var service = new TeamActivityService(dbContext, currentUser);

        var chart = await service.GetForCurrentOwnerAsync(seed.Warehouse.Id, 7, CancellationToken.None);

        Assert.Equal(seed.Warehouse.Id, chart.WarehouseId);
        Assert.Equal(seed.Warehouse.Name, chart.WarehouseName);
        Assert.Equal(1, chart.ManagerActionCount);
        Assert.Equal(2, chart.StaffActionCount);
        Assert.Equal(2, chart.Warehouses.Count);
    }

    [Fact]
    public async Task GetForOwnerAsync_AdminCanSelectOwnerButNotManager()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedAsync(dbContext);
        var currentUser = new TestCurrentUserService
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            UserName = "admin@test.local",
            Role = RoleNames.Admin
        };
        var service = new TeamActivityService(dbContext, currentUser);

        var chart = await service.GetForOwnerAsync(seed.Owner.Id, null, 30, CancellationToken.None);
        var managerResult = await service.GetForOwnerAsync(seed.Manager.Id, null, 30, CancellationToken.None);

        Assert.NotNull(chart);
        Assert.Equal(seed.Tenant.Id, chart.TenantId);
        Assert.Equal(30, chart.Days);
        Assert.Null(managerResult);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<SeedResult> SeedAsync(AppDbContext dbContext)
    {
        var ownerRole = new IdentityRole<Guid> { Name = RoleNames.Owner, NormalizedName = RoleNames.Owner.ToUpperInvariant() };
        var managerRole = new IdentityRole<Guid> { Name = RoleNames.Manager, NormalizedName = RoleNames.Manager.ToUpperInvariant() };
        var staffRole = new IdentityRole<Guid> { Name = RoleNames.Staff, NormalizedName = RoleNames.Staff.ToUpperInvariant() };
        var tenant = new Tenant { Name = "Owner Tenant", Code = "owner-tenant" };
        var otherTenant = new Tenant { Name = "Other Tenant", Code = "other-tenant" };
        var owner = CreateUser(tenant.Id, "owner@test.local", "Owner");
        var manager = CreateUser(tenant.Id, "manager@test.local", "Manager");
        var staff = CreateUser(tenant.Id, "staff@test.local", "Staff");
        var otherManager = CreateUser(otherTenant.Id, "manager@other.local", "Other Manager");
        var warehouse = new Warehouse { TenantId = tenant.Id, Name = "Warehouse A", Code = "WH-A", Address = "A" };
        var otherWarehouse = new Warehouse { TenantId = tenant.Id, Name = "Warehouse B", Code = "WH-B", Address = "B" };
        var inboundOrder = new InboundOrder
        {
            TenantId = tenant.Id,
            Warehouse = warehouse,
            CreatedByUserId = manager.Id,
            AssignedToUserId = staff.Id,
            ReferenceCode = "IN-A"
        };
        var outboundOrder = new OutboundOrder
        {
            TenantId = tenant.Id,
            Warehouse = otherWarehouse,
            CreatedByUserId = manager.Id,
            AssignedToUserId = staff.Id,
            ReferenceCode = "OUT-B"
        };
        var pallet = new Pallet
        {
            TenantId = tenant.Id,
            Warehouse = warehouse,
            Code = "PALLET-A"
        };
        var task = new TaskAssignment
        {
            TenantId = tenant.Id,
            AssignedToUserId = staff.Id,
            InboundOrder = inboundOrder,
            InboundOrderId = inboundOrder.Id,
            Title = "Inbound A",
            Instruction = "Scan A"
        };

        dbContext.AddRange(
            ownerRole,
            managerRole,
            staffRole,
            tenant,
            otherTenant,
            owner,
            manager,
            staff,
            otherManager,
            warehouse,
            otherWarehouse,
            inboundOrder,
            outboundOrder,
            pallet,
            task);
        dbContext.UserRoles.AddRange(
            new IdentityUserRole<Guid> { UserId = owner.Id, RoleId = ownerRole.Id },
            new IdentityUserRole<Guid> { UserId = manager.Id, RoleId = managerRole.Id },
            new IdentityUserRole<Guid> { UserId = staff.Id, RoleId = staffRole.Id },
            new IdentityUserRole<Guid> { UserId = otherManager.Id, RoleId = managerRole.Id });

        var now = DateTime.UtcNow;
        dbContext.AuditLogs.AddRange(
            CreateAudit(tenant.Id, manager.Id, now, "CreateInboundOrder", nameof(InboundOrder), inboundOrder.Id),
            CreateAudit(tenant.Id, manager.Id, now, "CreateOutboundOrder", nameof(OutboundOrder), outboundOrder.Id),
            CreateAudit(tenant.Id, staff.Id, now, "LookupQr", nameof(Pallet), pallet.Id),
            CreateAudit(otherTenant.Id, otherManager.Id, now, "CreateOutboundOrder"));
        dbContext.ScanLogs.Add(new ScanLog
        {
            TenantId = tenant.Id,
            TaskAssignment = task,
            TaskAssignmentId = task.Id,
            ScannedByUserId = staff.Id,
            Payload = "test-payload",
            IsSuccess = true,
            Message = "Verified",
            CreatedAtUtc = now
        });

        await dbContext.SaveChangesAsync();
        return new SeedResult(tenant, owner, manager, warehouse);
    }

    private static AppUser CreateUser(Guid tenantId, string userName, string fullName) =>
        new()
        {
            TenantId = tenantId,
            UserName = userName,
            Email = userName,
            FullName = fullName,
            IsActive = true
        };

    private static AuditLog CreateAudit(
        Guid tenantId,
        Guid userId,
        DateTime createdAtUtc,
        string action,
        string entityName = "TestEntity",
        Guid? entityId = null) =>
        new()
        {
            TenantId = tenantId,
            PerformedByUserId = userId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId ?? Guid.NewGuid(),
            Detail = "Test activity",
            CreatedAtUtc = createdAtUtc
        };

    private sealed record SeedResult(Tenant Tenant, AppUser Owner, AppUser Manager, Warehouse Warehouse);
}
