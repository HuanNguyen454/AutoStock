using ASM.Domain.Constants;
using Microsoft.AspNetCore.Identity;

namespace ASM.Domain.Entities;

public class AppUser : IdentityUser<Guid>
{
    public Guid TenantId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public Tenant? Tenant { get; set; }
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public ICollection<TaskAssignment> AssignedTasks { get; set; } = [];
    public ICollection<InboundOrder> CreatedInboundOrders { get; set; } = [];
    public ICollection<OutboundOrder> CreatedOutboundOrders { get; set; } = [];

    public bool IsWarehouseOperator() =>
        UserName is not null &&
        !string.Equals(UserName, RoleNames.Owner, StringComparison.OrdinalIgnoreCase);
}
