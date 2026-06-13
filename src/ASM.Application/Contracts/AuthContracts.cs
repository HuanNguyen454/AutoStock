namespace ASM.Application.Contracts;

public record LoginRequest(string UserName, string Password);

public record RefreshTokenRequest(string RefreshToken);

public record TokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAtUtc,
    CurrentUserDto User);

public record CurrentUserDto(
    Guid UserId,
    Guid TenantId,
    string UserName,
    string FullName,
    string Role);
