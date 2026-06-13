using ASM.Application.Contracts;
using ASM.Application.Interfaces;
using ASM.Domain.Entities;
using ASM.Infrastructure.Persistence;
using ASM.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace ASM.Infrastructure.Services;

public class AuthService(
    UserManager<AppUser> userManager,
    AppDbContext dbContext,
    ICurrentUserService currentUser,
    IOptions<JwtOptions> jwtOptions) : IAuthService
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public async Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.Users.FirstOrDefaultAsync(
            x => x.UserName == request.UserName,
            cancellationToken);

        if (user is null || !user.IsActive)
        {
            throw new InvalidOperationException("Thông tin đăng nhập không hợp lệ.");
        }

        var passwordValid = await userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            throw new InvalidOperationException("Thông tin đăng nhập không hợp lệ.");
        }

        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<TokenResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var storedToken = await dbContext.RefreshTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Token == request.RefreshToken, cancellationToken);

        if (storedToken is null || !storedToken.IsActive || storedToken.User is null)
        {
            throw new InvalidOperationException("Refresh token không hợp lệ.");
        }

        storedToken.RevokedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return await IssueTokensAsync(storedToken.User, cancellationToken);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var storedToken = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(x => x.Token == refreshToken, cancellationToken);

        if (storedToken is not null)
        {
            storedToken.RevokedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<CurrentUserDto> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
        {
            throw new InvalidOperationException("User chưa đăng nhập.");
        }

        var user = await userManager.Users.FirstAsync(x => x.Id == currentUser.UserId, cancellationToken);
        var roles = await userManager.GetRolesAsync(user);
        return new CurrentUserDto(user.Id, user.TenantId, user.UserName ?? string.Empty, user.FullName, roles.FirstOrDefault() ?? string.Empty);
    }

    private async Task<TokenResponse> IssueTokensAsync(AppUser user, CancellationToken cancellationToken)
    {
        var roles = await userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? string.Empty;
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new(ClaimTypes.Role, role),
            new("tenant_id", user.TenantId.ToString()),
            new("full_name", user.FullName)
        };

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenDays)
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return new TokenResponse(
            accessToken,
            refreshToken,
            expiresAtUtc,
            new CurrentUserDto(user.Id, user.TenantId, user.UserName ?? string.Empty, user.FullName, role));
    }
}
