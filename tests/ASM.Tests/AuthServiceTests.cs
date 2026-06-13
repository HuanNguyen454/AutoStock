using ASM.Application.Contracts;
using ASM.Domain.Constants;
using ASM.Domain.Entities;
using ASM.Infrastructure.Persistence;
using ASM.Infrastructure.Security;
using ASM.Infrastructure.Services;
using ASM.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ASM.Tests;

public class AuthServiceTests
{
    [Fact]
    public async Task LoginAsync_IssuesAccessAndRefreshTokens()
    {
        await using var dbContext = CreateDbContext();
        var userManager = TestIdentityFactory.CreateUserManager(dbContext);
        var tenant = new Tenant { Name = "Test Tenant", Code = "tenant-test" };
        dbContext.Tenants.Add(tenant);
        dbContext.Roles.Add(new IdentityRole<Guid>
        {
            Name = RoleNames.Owner,
            NormalizedName = RoleNames.Owner.ToUpperInvariant()
        });
        await dbContext.SaveChangesAsync();

        var user = new AppUser
        {
            TenantId = tenant.Id,
            UserName = "owner@test.local",
            Email = "owner@test.local",
            FullName = "Test Owner",
            IsActive = true
        };

        var createResult = await userManager.CreateAsync(user, "Asm123$");
        Assert.True(createResult.Succeeded);
        var addRoleResult = await userManager.AddToRoleAsync(user, RoleNames.Owner);
        Assert.True(addRoleResult.Succeeded);

        var authService = new AuthService(
            userManager,
            dbContext,
            new TestCurrentUserService(),
            Options.Create(new JwtOptions()));

        var response = await authService.LoginAsync(new LoginRequest("owner@test.local", "Asm123$"), CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(response.RefreshToken));
        Assert.Equal(RoleNames.Owner, response.User.Role);
        Assert.Single(dbContext.RefreshTokens);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}
