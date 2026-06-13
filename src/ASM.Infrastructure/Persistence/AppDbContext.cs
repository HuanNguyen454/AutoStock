using ASM.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ASM.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Area> Areas => Set<Area>();
    public DbSet<Rack> Racks => Set<Rack>();
    public DbSet<Slot> Slots => Set<Slot>();
    public DbSet<Pallet> Pallets => Set<Pallet>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<QrCode> QrCodes => Set<QrCode>();
    public DbSet<ScanLog> ScanLogs => Set<ScanLog>();
    public DbSet<InboundOrder> InboundOrders => Set<InboundOrder>();
    public DbSet<InboundOrderLine> InboundOrderLines => Set<InboundOrderLine>();
    public DbSet<OutboundOrder> OutboundOrders => Set<OutboundOrder>();
    public DbSet<OutboundOrderLine> OutboundOrderLines => Set<OutboundOrderLine>();
    public DbSet<TaskAssignment> TaskAssignments => Set<TaskAssignment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Tenant>().HasIndex(x => x.Code).IsUnique();
        builder.Entity<Warehouse>().HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        builder.Entity<Product>().HasIndex(x => new { x.TenantId, x.Sku }).IsUnique();
        builder.Entity<Pallet>().HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        builder.Entity<QrCode>().HasIndex(x => new { x.TenantId, x.Payload }).IsUnique();
        builder.Entity<RefreshToken>().HasIndex(x => x.Token).IsUnique();

        builder.Entity<Pallet>()
            .HasOne(x => x.CurrentSlot)
            .WithMany(x => x.CurrentPallets)
            .HasForeignKey(x => x.CurrentSlotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<InventoryItem>()
            .HasOne(x => x.Pallet)
            .WithMany(x => x.InventoryItems)
            .HasForeignKey(x => x.PalletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<InventoryItem>()
            .HasOne(x => x.Product)
            .WithMany(x => x.InventoryItems)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ScanLog>()
            .HasOne(x => x.ScannedByUser)
            .WithMany()
            .HasForeignKey(x => x.ScannedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<InboundOrder>()
            .HasOne(x => x.CreatedByUser)
            .WithMany(x => x.CreatedInboundOrders)
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<InboundOrder>()
            .HasOne(x => x.AssignedToUser)
            .WithMany()
            .HasForeignKey(x => x.AssignedToUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<OutboundOrder>()
            .HasOne(x => x.CreatedByUser)
            .WithMany(x => x.CreatedOutboundOrders)
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<OutboundOrder>()
            .HasOne(x => x.AssignedToUser)
            .WithMany()
            .HasForeignKey(x => x.AssignedToUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<TaskAssignment>()
            .HasOne(x => x.AssignedToUser)
            .WithMany(x => x.AssignedTasks)
            .HasForeignKey(x => x.AssignedToUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<TaskAssignment>()
            .HasOne(x => x.InboundOrder)
            .WithOne(x => x.TaskAssignment)
            .HasForeignKey<TaskAssignment>(x => x.InboundOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<TaskAssignment>()
            .HasOne(x => x.OutboundOrder)
            .WithOne(x => x.TaskAssignment)
            .HasForeignKey<TaskAssignment>(x => x.OutboundOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<InboundOrderLine>()
            .HasOne(x => x.InboundOrder)
            .WithMany(x => x.Lines)
            .HasForeignKey(x => x.InboundOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<InboundOrderLine>()
            .HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<InboundOrderLine>()
            .HasOne(x => x.Pallet)
            .WithMany()
            .HasForeignKey(x => x.PalletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<InboundOrderLine>()
            .HasOne(x => x.TargetSlot)
            .WithMany()
            .HasForeignKey(x => x.TargetSlotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<OutboundOrderLine>()
            .HasOne(x => x.OutboundOrder)
            .WithMany(x => x.Lines)
            .HasForeignKey(x => x.OutboundOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<OutboundOrderLine>()
            .HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<OutboundOrderLine>()
            .HasOne(x => x.SourcePallet)
            .WithMany()
            .HasForeignKey(x => x.SourcePalletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<OutboundOrderLine>()
            .HasOne(x => x.SourceSlot)
            .WithMany()
            .HasForeignKey(x => x.SourceSlotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<AppUser>()
            .HasOne(x => x.Tenant)
            .WithMany(x => x.Users)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<RefreshToken>()
            .HasOne(x => x.User)
            .WithMany(x => x.RefreshTokens)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
