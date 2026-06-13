using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ASM.WebPortal.Services;

public sealed class SessionExpiredExceptionFilter : IAsyncExceptionFilter
{
    public async Task OnExceptionAsync(ExceptionContext context)
    {
        if (context.Exception is not SessionExpiredException)
        {
            return;
        }

        context.HttpContext.Session.Clear();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        context.Result = new RedirectToActionResult("Login", "Account", new { expired = true });
        context.ExceptionHandled = true;
    }
}
