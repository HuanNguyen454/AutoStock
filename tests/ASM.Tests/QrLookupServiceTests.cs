using ASM.Domain.Constants;
using ASM.Domain.Entities;
using ASM.Domain.Enums;
using ASM.Infrastructure.Persistence;
using ASM.Infrastructure.Services;
using ASM.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace ASM.Tests;

public class QrLookupServiceTests
{
    [Fact]
    public async Task LookupAsync_AreaQrReturnsTenantWarehouseContents()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedWarehouseAsync(dbContext, "tenant-a");
        var currentUser = CreateCurrentUser(seed.Tenant.Id, seed.User.Id, RoleNames.Staff);
        var service = new QrService(dbContext, currentUser);

        var result = await service.LookupAsync(seed.AreaQr.Payload, CancellationToken.None);

        Assert.Equal(QrTargetType.Area, result.TargetType);
        Assert.Equal(seed.Warehouse.Name, result.Warehouse.Name);
        var area = Assert.Single(result.Areas);
        var occupiedRack = area.Racks.Single(x => x.Name == "Rack 1");
        var occupiedSlot = occupiedRack.Slots.Single(x => x.Name == "Slot 1");
        var inventory = Assert.Single(occupiedSlot.Pallets).InventoryItems;
        Assert.Equal("Test Product", Assert.Single(inventory).ProductName);
        Assert.Contains(occupiedRack.Slots, x => x.Name == "Slot 2" && !x.IsOccupied);
        Assert.Contains(area.Racks, x => x.Name == "Empty Rack" && x.Slots.Count == 0);
        Assert.Contains(dbContext.AuditLogs, x => x.Action == "LookupQr" && x.TenantId == seed.Tenant.Id);
    }

    [Fact]
    public async Task LookupAsync_QrFromAnotherTenantIsRejected()
    {
        await using var dbContext = CreateDbContext();
        var tenantA = await SeedWarehouseAsync(dbContext, "tenant-a");
        var tenantB = await SeedWarehouseAsync(dbContext, "tenant-b");
        var currentUser = CreateCurrentUser(tenantA.Tenant.Id, tenantA.User.Id, RoleNames.Manager);
        var service = new QrService(dbContext, currentUser);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.LookupAsync(tenantB.AreaQr.Payload, CancellationToken.None));

        Assert.Equal("QR code was not found in your organization.", exception.Message);
        Assert.DoesNotContain(dbContext.AuditLogs, x => x.EntityId == tenantB.AreaQr.TargetId);
    }

    [Fact]
    public async Task LookupAsync_AdminRoleIsRejected()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedWarehouseAsync(dbContext, "tenant-a");
        var currentUser = CreateCurrentUser(seed.Tenant.Id, seed.User.Id, RoleNames.Admin);
        var service = new QrService(dbContext, currentUser);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.LookupAsync(seed.AreaQr.Payload, CancellationToken.None));

        Assert.Contains("permission", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static TestCurrentUserService CreateCurrentUser(Guid tenantId, Guid userId, string role) =>
        new()
        {
            TenantId = tenantId,
            UserId = userId,
            UserName = $"{role.ToLowerInvariant()}@test.local",
            Role = role
        };

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<SeedResult> SeedWarehouseAsync(AppDbContext dbContext, string tenantCode)
    {
        var tenant = new Tenant { Name = $"Tenant {tenantCode}", Code = tenantCode };
        var user = new AppUser
        {
            TenantId = tenant.Id,
            UserName = $"staff@{tenantCode}.local",
            Email = $"staff@{tenantCode}.local",
            FullName = $"Staff {tenantCode}",
            IsActive = true
        };
        var warehouse = new Warehouse
        {
            TenantId = tenant.Id,
            Name = $"Warehouse {tenantCode}",
            Code = $"WH-{tenantCode}",
            Address = "Test address"
        };
        var area = new Area { TenantId = tenant.Id, Warehouse = warehouse, Name = "Area A" };
        var rack = new Rack { TenantId = tenant.Id, Area = area, Name = "Rack 1" };
        var slot = new Slot { TenantId = tenant.Id, Rack = rack, Name = "Slot 1", IsOccupied = true };
        var emptySlot = new Slot { TenantId = tenant.Id, Rack = rack, Name = "Slot 2", IsOccupied = false };
        var emptyRack = new Rack { TenantId = tenant.Id, Area = area, Name = "Empty Rack" };
        var pallet = new Pallet
        {
            TenantId = tenant.Id,
            Warehouse = warehouse,
            CurrentSlot = slot,
            Code = "PALLET-1",
            Status = PalletStatus.Occupied
        };
        var category = new ProductCategory
        {
            TenantId = tenant.Id,
            Code = "CAT-1",
            Name = "Test Category"
        };
        var product = new Product
        {
            TenantId = tenant.Id,
            Category = category,
            Sku = "SKU-1",
            Name = "Test Product",
            Brand = "Test Brand"
        };
        var inventory = new InventoryItem
        {
            TenantId = tenant.Id,
            Product = product,
            Pallet = pallet,
            Quantity = 12,
            LotNumber = "LOT-1"
        };
        var areaQr = new QrCode
        {
            TenantId = tenant.Id,
            TargetType = QrTargetType.Area,
            TargetId = area.Id,
            Payload = $"{tenant.Id:N}|Area|{area.Id:N}",
            Label = $"Area - {area.Name}"
        };

        dbContext.AddRange(tenant, user, warehouse, area, rack, slot, emptySlot, emptyRack, pallet, category, product, inventory, areaQr);
        await dbContext.SaveChangesAsync();

        return new SeedResult(tenant, user, warehouse, areaQr);
    }

    private sealed record SeedResult(Tenant Tenant, AppUser User, Warehouse Warehouse, QrCode AreaQr);
}
