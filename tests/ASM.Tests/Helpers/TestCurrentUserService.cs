using ASM.Application.Interfaces;

namespace ASM.Tests.Helpers;

internal sealed class TestCurrentUserService : ICurrentUserService
{
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsAuthenticated { get; set; } = true;

    public bool IsInRole(string role) => string.Equals(Role, role, StringComparison.OrdinalIgnoreCase);
}
