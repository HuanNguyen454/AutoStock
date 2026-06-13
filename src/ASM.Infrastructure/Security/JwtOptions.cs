namespace ASM.Infrastructure.Security;

public class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "ASM.Api";
    public string Audience { get; set; } = "ASM.Clients";
    public string SigningKey { get; set; } = "AsmJwtSigningKey1234567890!ChangeMe";
    public int AccessTokenMinutes { get; set; } = 120;
    public int RefreshTokenDays { get; set; } = 14;
}
