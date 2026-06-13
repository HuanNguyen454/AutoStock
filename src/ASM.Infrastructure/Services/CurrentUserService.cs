using ASM.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace ASM.Infrastructure.Services;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal Principal => httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal();

    public Guid UserId => GetGuid(ClaimTypes.NameIdentifier);
    public Guid TenantId => GetGuid("tenant_id");
    public string UserName => Principal.Identity?.Name ?? string.Empty;
    public string Role => Principal.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
    public bool IsAuthenticated => Principal.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(string role) => Principal.IsInRole(role);

    private Guid GetGuid(string claimType)
    {
        var value = Principal.FindFirstValue(claimType);
        return Guid.TryParse(value, out var parsed) ? parsed : Guid.Empty;
    }
}
