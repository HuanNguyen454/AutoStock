namespace ASM.Domain.Constants;

public static class RoleNames
{
    public const string Admin = "Admin";
    public const string Owner = "Owner";
    public const string Manager = "Manager";
    public const string Staff = "Staff";

    public static readonly string[] All = [Admin, Owner, Manager, Staff];
}
