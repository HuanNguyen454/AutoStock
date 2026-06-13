using ASM.Application.Contracts;
using ASM.Domain.Constants;
using ASM.Domain.Entities;
using ASM.Domain.Enums;
using ASM.Infrastructure.Persistence;
using ASM.Infrastructure.Services;
using ASM.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace ASM.Tests;

public class OrderWorkflowTests
{
    [Fact]
    public async Task VerifyScanAndCompleteInbound_CreatesInventory()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedScenarioAsync(dbContext);
        var currentUser = new TestCurrentUserService
        {
            UserId = seed.Manager.Id,
            TenantId = seed.Tenant.Id,
            UserName = seed.Manager.UserName!,
            Role = RoleNames.Manager
        };

        var orderService = new OrderService(dbContext, currentUser);
        var created = await orderService.CreateInboundOrderAsync(
            new CreateInboundOrderRequest(
                seed.Warehouse.Id,
                seed.Staff.Id,
                seed.Product.Id,
                seed.Pallet.Id,
                seed.Slot.Id,
                12,
                "IN-TEST"),
            CancellationToken.None);

        var taskId = created.TaskAssignmentId!.Value;
        currentUser.UserId = seed.Staff.Id;
        currentUser.UserName = seed.Staff.UserName!;
        currentUser.Role = RoleNames.Staff;

        var verifyProduct = await orderService.VerifyTaskStepAsync(
            new TaskStepVerifyRequest(taskId, "product", seed.ProductQr.Payload),
            CancellationToken.None);
        var verifyPallet = await orderService.VerifyTaskStepAsync(
            new TaskStepVerifyRequest(taskId, "pallet", seed.PalletQr.Payload),
            CancellationToken.None);
        var verifySlot = await orderService.VerifyTaskStepAsync(
            new TaskStepVerifyRequest(taskId, "slot", seed.SlotQr.Payload),
            CancellationToken.None);

        Assert.True(verifyProduct.IsValid);
        Assert.True(verifyPallet.IsValid);
        Assert.True(verifySlot.IsValid);

        await orderService.CompleteInboundOrderAsync(created.Id, CancellationToken.None);

        var pallet = await dbContext.Pallets.Include(x => x.InventoryItems).FirstAsync(x => x.Id == seed.Pallet.Id);
        var order = await dbContext.InboundOrders.Include(x => x.TaskAssignment).FirstAsync(x => x.Id == created.Id);
        Assert.Equal(PalletStatus.Occupied, pallet.Status);
        Assert.Single(pallet.InventoryItems);
        Assert.Equal(12, pallet.InventoryItems.Single().Quantity);
        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.Equal(ASM.Domain.Enums.TaskStatus.Completed, order.TaskAssignment!.Status);
    }

    [Fact]
    public async Task VerifyScanAndCompleteOutbound_DecrementsInventory()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedScenarioAsync(dbContext, seededInventoryQuantity: 10, palletPlacedInSlot: true);
        var currentUser = new TestCurrentUserService
        {
            UserId = seed.Manager.Id,
            TenantId = seed.Tenant.Id,
            UserName = seed.Manager.UserName!,
            Role = RoleNames.Manager
        };

        var orderService = new OrderService(dbContext, currentUser);
        var created = await orderService.CreateOutboundOrderAsync(
            new CreateOutboundOrderRequest(
                seed.Warehouse.Id,
                seed.Staff.Id,
                seed.Product.Id,
                seed.Pallet.Id,
                seed.Slot.Id,
                4,
                "OUT-TEST"),
            CancellationToken.None);

        currentUser.UserId = seed.Staff.Id;
        currentUser.UserName = seed.Staff.UserName!;
        currentUser.Role = RoleNames.Staff;

        var verify = await orderService.VerifyScanAsync(
            new ScanVerifyRequest(created.TaskAssignmentId!.Value, seed.PalletQr.Payload),
            CancellationToken.None);

        Assert.True(verify.IsValid);

        await orderService.CompleteOutboundOrderAsync(created.Id, CancellationToken.None);

        var pallet = await dbContext.Pallets.Include(x => x.InventoryItems).FirstAsync(x => x.Id == seed.Pallet.Id);
        var inventory = pallet.InventoryItems.Single();
        Assert.Equal(6, inventory.Quantity);
    }

    [Fact]
    public async Task VerifyInbound_OutOfOrderScanFails()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedScenarioAsync(dbContext);
        var currentUser = new TestCurrentUserService
        {
            UserId = seed.Manager.Id,
            TenantId = seed.Tenant.Id,
            UserName = seed.Manager.UserName!,
            Role = RoleNames.Manager
        };

        var orderService = new OrderService(dbContext, currentUser);
        var created = await orderService.CreateInboundOrderAsync(
            new CreateInboundOrderRequest(
                seed.Warehouse.Id,
                seed.Staff.Id,
                seed.Product.Id,
                seed.Pallet.Id,
                seed.Slot.Id,
                12,
                "IN-ORDER"),
            CancellationToken.None);

        currentUser.UserId = seed.Staff.Id;
        currentUser.UserName = seed.Staff.UserName!;
        currentUser.Role = RoleNames.Staff;

        var verify = await orderService.VerifyTaskStepAsync(
            new TaskStepVerifyRequest(created.TaskAssignmentId!.Value, "slot", seed.SlotQr.Payload),
            CancellationToken.None);

        Assert.False(verify.IsValid);
        Assert.Contains("product", verify.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<SeedResult> SeedScenarioAsync(AppDbContext dbContext, int seededInventoryQuantity = 0, bool palletPlacedInSlot = false)
    {
        var tenant = new Tenant { Name = "Tenant", Code = "tenant" };
        var manager = new AppUser { TenantId = tenant.Id, UserName = "manager@test", FullName = "Manager", Email = "manager@test", IsActive = true };
        var staff = new AppUser { TenantId = tenant.Id, UserName = "staff@test", FullName = "Staff", Email = "staff@test", IsActive = true };
        var warehouse = new Warehouse { TenantId = tenant.Id, Name = "Main Warehouse", Code = "WH1", Address = "Address" };
        var area = new Area { TenantId = tenant.Id, Warehouse = warehouse, Name = "Area A" };
        var rack = new Rack { TenantId = tenant.Id, Area = area, Name = "Rack 1" };
        var slot = new Slot { TenantId = tenant.Id, Rack = rack, Name = "Slot 1", IsOccupied = palletPlacedInSlot };
        var pallet = new Pallet
        {
            TenantId = tenant.Id,
            Warehouse = warehouse,
            CurrentSlot = palletPlacedInSlot ? slot : null,
            CurrentSlotId = palletPlacedInSlot ? slot.Id : null,
            Code = "PALLET-1",
            Status = seededInventoryQuantity > 0 ? PalletStatus.Occupied : PalletStatus.Empty
        };
        var product = new Product { TenantId = tenant.Id, Sku = "SKU-1", Name = "Product 1" };
        var productQr = new QrCode { TenantId = tenant.Id, TargetType = QrTargetType.Product, TargetId = product.Id, Payload = $"{tenant.Id:N}|Product|{product.Id:N}", Label = "Product QR" };
        var slotQr = new QrCode { TenantId = tenant.Id, TargetType = QrTargetType.Slot, TargetId = slot.Id, Payload = $"{tenant.Id:N}|Slot|{slot.Id:N}", Label = "Slot QR" };
        var palletQr = new QrCode { TenantId = tenant.Id, TargetType = QrTargetType.Pallet, TargetId = pallet.Id, Payload = $"{tenant.Id:N}|Pallet|{pallet.Id:N}", Label = "Pallet QR" };

        dbContext.AddRange(tenant, manager, staff, warehouse, area, rack, slot, pallet, product, productQr, slotQr, palletQr);

        if (seededInventoryQuantity > 0)
        {
            dbContext.InventoryItems.Add(new InventoryItem
            {
                TenantId = tenant.Id,
                Product = product,
                Pallet = pallet,
                Quantity = seededInventoryQuantity
            });
        }

        await dbContext.SaveChangesAsync();

        return new SeedResult(tenant, manager, staff, warehouse, slot, pallet, product, productQr, slotQr, palletQr);
    }

    private sealed record SeedResult(
        Tenant Tenant,
        AppUser Manager,
        AppUser Staff,
        Warehouse Warehouse,
        Slot Slot,
        Pallet Pallet,
        Product Product,
        QrCode ProductQr,
        QrCode SlotQr,
        QrCode PalletQr);
}
