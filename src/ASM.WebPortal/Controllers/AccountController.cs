using ASM.Domain.Entities;
using ASM.Domain.Constants;
using ASM.WebPortal.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ASM.WebPortal.Controllers;

public class AccountController(UserManager<AppUser> userManager) : Controller
{
    [HttpGet]
    public IActionResult Login(string? returnUrl = null, bool expired = false) => View(new LoginViewModel
    {
        ErrorMessage = expired ? "Phien dang nhap da het han. Vui long dang nhap lai." : null,
        ReturnUrl = returnUrl
    });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var user = await userManager.FindByNameAsync(model.UserName);
            if (user is null || !user.IsActive)
            {
                model.ErrorMessage = "Thong tin dang nhap khong hop le.";
                return View(model);
            }

            var passwordValid = await userManager.CheckPasswordAsync(user, model.Password);
            if (!passwordValid)
            {
                model.ErrorMessage = "Thong tin dang nhap khong hop le.";
                return View(model);
            }

            var role = (await userManager.GetRolesAsync(user)).FirstOrDefault() ?? string.Empty;
            if (role is not RoleNames.Admin and not RoleNames.Owner and not RoleNames.Manager and not RoleNames.Staff)
            {
                model.ErrorMessage = "Tai khoan nay khong duoc phep dang nhap vao cong ASM.";
                return View(model);
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.UserName ?? string.Empty),
                new(ClaimTypes.Role, role),
                new("full_name", user.FullName),
                new("tenant_id", user.TenantId.ToString())
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));

            if (IsAllowedReturnUrl(model.ReturnUrl, role))
            {
                return Redirect(model.ReturnUrl!);
            }

            return RedirectToRoleHome(role);
        }
        catch (Exception ex)
        {
            model.ErrorMessage = ex.Message;
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        HttpContext.Session.Clear();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied(string? returnUrl = null)
    {
        return View(model: returnUrl);
    }

    private IActionResult RedirectToRoleHome(string role)
    {
        return role switch
        {
            RoleNames.Admin => RedirectToAction("Index", "Admin"),
            RoleNames.Staff => RedirectToAction("Index", "Tasks", new { area = "Staff" }),
            _ => RedirectToAction("Index", "Dashboard")
        };
    }

    private bool IsAllowedReturnUrl(string? returnUrl, string role)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) || !Url.IsLocalUrl(returnUrl))
        {
            return false;
        }

        if (returnUrl.StartsWith("/staff", StringComparison.OrdinalIgnoreCase))
        {
            return role == RoleNames.Staff;
        }

        if (returnUrl.StartsWith("/admin", StringComparison.OrdinalIgnoreCase))
        {
            return role == RoleNames.Admin;
        }

        if (role == RoleNames.Staff)
        {
            return false;
        }

        return true;
    }
}
