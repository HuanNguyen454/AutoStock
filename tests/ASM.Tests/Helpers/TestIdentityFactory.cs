using ASM.Domain.Entities;
using ASM.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ASM.Tests.Helpers;

internal static class TestIdentityFactory
{
    public static UserManager<AppUser> CreateUserManager(AppDbContext dbContext)
    {
        var store = new UserStore<AppUser, IdentityRole<Guid>, AppDbContext, Guid>(dbContext);
        return new UserManager<AppUser>(
            store,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<AppUser>(),
            [new UserValidator<AppUser>()],
            [new PasswordValidator<AppUser>()],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new ServiceCollection().BuildServiceProvider(),
            new Logger<UserManager<AppUser>>(LoggerFactory.Create(builder => { })));
    }
}
