using ASM.Application.Interfaces;
using ASM.Domain.Constants;
using ASM.Domain.Entities;
using ASM.Infrastructure.Persistence;
using ASM.Infrastructure.Security;
using ASM.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace ASM.Infrastructure.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Server=(localdb)\\MyLocalDB;Database=AsmWmsDb;Trusted_Connection=True;TrustServerCertificate=True;";

        services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));

        services.AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 6;
                options.Password.RequireDigit = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey))
                };
            });

        services.AddAuthorization();
        services.AddHttpContextAccessor();

        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IWarehouseService, WarehouseService>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<IQrService, QrService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IAdminService, AdminService>();

        return services;
    }

    public static async Task SeedAsync(
        this IServiceProvider serviceProvider,
        bool applyMigrationsOnStartup = true,
        bool seedSystemData = true,
        bool seedDemoData = true)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        if (applyMigrationsOnStartup)
        {
            await dbContext.Database.MigrateAsync();
        }

        if (seedSystemData || seedDemoData)
        {
            foreach (var roleName in RoleNames.All)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
                }
            }
        }

        if (seedSystemData)
        {
            var adminTenant = await dbContext.Tenants.FirstOrDefaultAsync(x => x.Code == "asm-platform", default);
            if (adminTenant is null)
            {
                adminTenant = new Tenant
                {
                    Name = "ASM Platform",
                    Code = "asm-platform"
                };
                dbContext.Tenants.Add(adminTenant);
                await dbContext.SaveChangesAsync();
            }

            await EnsureUserAsync(userManager, adminTenant.Id, "System Admin", "admin@asm.local", RoleNames.Admin);
        }

        if (!seedDemoData)
        {
            return;
        }

        var demoTenant = await dbContext.Tenants.FirstOrDefaultAsync(x => x.Code == "asm-demo", default);
        if (demoTenant is null)
        {
            demoTenant = new Tenant
            {
                Name = "ASM Demo Tenant",
                Code = "asm-demo"
            };
            dbContext.Tenants.Add(demoTenant);
            await dbContext.SaveChangesAsync();
        }

        var owner = await EnsureUserAsync(userManager, demoTenant.Id, "Demo Owner", "owner@asm.local", RoleNames.Owner);
        await EnsureUserAsync(userManager, demoTenant.Id, "Demo Manager", "manager@asm.local", RoleNames.Manager);
        await EnsureUserAsync(userManager, demoTenant.Id, "Demo Staff", "staff@asm.local", RoleNames.Staff);

        if (!await dbContext.Warehouses.AnyAsync(x => x.TenantId == demoTenant.Id))
        {
            var warehouse = new Warehouse
            {
                TenantId = demoTenant.Id,
                Name = "Kho Trung Tam",
                Code = "WH-HCM-01",
                Address = "Thu Duc, Ho Chi Minh City"
            };
            var area = new Area
            {
                TenantId = demoTenant.Id,
                Warehouse = warehouse,
                Name = "Area A"
            };
            var rack = new Rack
            {
                TenantId = demoTenant.Id,
                Area = area,
                Name = "Rack A1"
            };
            var slot = new Slot
            {
                TenantId = demoTenant.Id,
                Rack = rack,
                Name = "A1-S01"
            };
            var pallet = new Pallet
            {
                TenantId = demoTenant.Id,
                Warehouse = warehouse,
                Code = "PALLET-001",
                Status = ASM.Domain.Enums.PalletStatus.Empty
            };
            var product = new Product
            {
                TenantId = demoTenant.Id,
                Sku = "SP-001",
                Name = "Thung Sua Tuoi",
                Description = "Seed product"
            };

            dbContext.AddRange(warehouse, area, rack, slot, pallet, product);
            await dbContext.SaveChangesAsync();

            dbContext.QrCodes.AddRange(
                new QrCode
                {
                    TenantId = demoTenant.Id,
                    TargetType = ASM.Domain.Enums.QrTargetType.Slot,
                    TargetId = slot.Id,
                    Payload = $"{demoTenant.Id:N}|Slot|{slot.Id:N}",
                    Label = "Slot A1-S01"
                },
                new QrCode
                {
                    TenantId = demoTenant.Id,
                    TargetType = ASM.Domain.Enums.QrTargetType.Pallet,
                    TargetId = pallet.Id,
                    Payload = $"{demoTenant.Id:N}|Pallet|{pallet.Id:N}",
                    Label = "Pallet 001"
                },
                new QrCode
                {
                    TenantId = demoTenant.Id,
                    TargetType = ASM.Domain.Enums.QrTargetType.Product,
                    TargetId = product.Id,
                    Payload = $"{demoTenant.Id:N}|Product|{product.Id:N}",
                    Label = "Product SP-001"
                });

            dbContext.AuditLogs.Add(new AuditLog
            {
                TenantId = demoTenant.Id,
                PerformedByUserId = owner.Id,
                Action = "SeedOwnerActivity",
                EntityName = nameof(Tenant),
                EntityId = demoTenant.Id,
                Detail = "Initial demo owner activity"
            });

            await dbContext.SaveChangesAsync();
        }
    }

    private static async Task<AppUser> EnsureUserAsync(
        UserManager<AppUser> userManager,
        Guid tenantId,
        string fullName,
        string userName,
        string roleName)
    {
        var normalizedUserName = userName.ToUpperInvariant();
        var user = await userManager.Users.FirstOrDefaultAsync(
            x => x.NormalizedUserName == normalizedUserName || x.NormalizedEmail == normalizedUserName);
        if (user is null)
        {
            user = new AppUser
            {
                TenantId = tenantId,
                FullName = fullName,
                UserName = userName,
                Email = userName,
                IsActive = true
            };

            var createResult = await userManager.CreateAsync(user, "Asm123$");
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(string.Join("; ", createResult.Errors.Select(x => x.Description)));
            }
        }
        else
        {
            user.TenantId = tenantId;
            user.FullName = fullName;
            user.IsActive = true;

            var updateResult = await userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                throw new InvalidOperationException(string.Join("; ", updateResult.Errors.Select(x => x.Description)));
            }
        }

        if (!await userManager.IsInRoleAsync(user, roleName))
        {
            await userManager.AddToRoleAsync(user, roleName);
        }

        return user;
    }
}
