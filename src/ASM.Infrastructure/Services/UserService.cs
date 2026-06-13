using ASM.Application.Contracts;
using ASM.Application.Interfaces;
using ASM.Domain.Constants;
using ASM.Domain.Entities;
using ASM.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ASM.Infrastructure.Services;

public class UserService(
    UserManager<AppUser> userManager,
    AppDbContext dbContext,
    ICurrentUserService currentUser) : IUserService
{
    public async Task<IReadOnlyCollection<UserDto>> GetUsersAsync(CancellationToken cancellationToken)
    {
        var users = await userManager.Users
            .Where(x => x.TenantId == currentUser.TenantId)
            .OrderBy(x => x.UserName)
            .ToListAsync(cancellationToken);

        var result = new List<UserDto>();
        foreach (var user in users)
        {
            var role = (await userManager.GetRolesAsync(user)).FirstOrDefault() ?? string.Empty;
            result.Add(new UserDto(user.Id, user.UserName ?? string.Empty, user.FullName, user.Email ?? string.Empty, role, user.IsActive));
        }

        return result;
    }

    public async Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        if (!RoleNames.All.Contains(request.Role))
        {
            throw new InvalidOperationException("Role khong hop le.");
        }

        if (request.Role == RoleNames.Admin && !currentUser.IsInRole(RoleNames.Admin))
        {
            throw new InvalidOperationException("Only system admin can create admin accounts.");
        }

        var user = new AppUser
        {
            TenantId = currentUser.TenantId,
            UserName = request.UserName,
            Email = request.Email,
            FullName = request.FullName,
            IsActive = true
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", createResult.Errors.Select(x => x.Description)));
        }

        await userManager.AddToRoleAsync(user, request.Role);
        await AddAuditAsync("CreateUser", nameof(AppUser), user.Id, $"Created user {request.UserName}", cancellationToken);
        return new UserDto(user.Id, user.UserName ?? string.Empty, user.FullName, user.Email ?? string.Empty, request.Role, user.IsActive);
    }

    public async Task ToggleUserStatusAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userManager.Users.FirstOrDefaultAsync(
            x => x.Id == userId && x.TenantId == currentUser.TenantId,
            cancellationToken) ?? throw new InvalidOperationException("Khong tim thay user.");

        user.IsActive = !user.IsActive;
        await userManager.UpdateAsync(user);
        await AddAuditAsync("ToggleUserStatus", nameof(AppUser), user.Id, $"User active = {user.IsActive}", cancellationToken);
    }

    private async Task AddAuditAsync(string action, string entityName, Guid entityId, string detail, CancellationToken cancellationToken)
    {
        dbContext.AuditLogs.Add(new AuditLog
        {
            TenantId = currentUser.TenantId,
            PerformedByUserId = currentUser.UserId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Detail = detail
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
