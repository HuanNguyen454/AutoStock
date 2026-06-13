using ASM.Application.Interfaces;
using ASM.Domain.Constants;
using ASM.WebPortal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ASM.WebPortal.Controllers;

[Authorize(Roles = RoleNames.Admin)]
public class AdminController(IAdminService adminService) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var owners = await adminService.GetOwnersAsync(cancellationToken);
        return View(new AdminDashboardPageViewModel
        {
            Summary = await adminService.GetDashboardAsync(cancellationToken),
            Owners = owners.OrderByDescending(x => x.LastActivityAtUtc).Take(5).ToList()
        });
    }

    public async Task<IActionResult> Owners(CancellationToken cancellationToken)
    {
        return View(new AdminOwnersPageViewModel
        {
            Owners = await adminService.GetOwnersAsync(cancellationToken)
        });
    }

    public async Task<IActionResult> Owner(Guid id, CancellationToken cancellationToken)
    {
        var detail = await adminService.GetOwnerDetailsAsync(id, cancellationToken);
        if (detail is null)
        {
            return NotFound();
        }

        var edit = await adminService.GetOwnerEditAsync(id, cancellationToken);
        if (edit is null)
        {
            return NotFound();
        }

        return View(new AdminOwnerDetailPageViewModel
        {
            Detail = detail,
            Edit = new AdminOwnerEditViewModel
            {
                OwnerUserId = edit.OwnerUserId,
                OwnerUserName = edit.OwnerUserName,
                OwnerFullName = edit.OwnerFullName,
                OwnerEmail = edit.OwnerEmail,
                OwnerPhoneNumber = edit.OwnerPhoneNumber,
                TenantName = edit.TenantName,
                IsActive = edit.IsActive
            }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateOwner([Bind(Prefix = "Edit")] AdminOwnerEditViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var detail = await adminService.GetOwnerDetailsAsync(model.OwnerUserId, cancellationToken);
            if (detail is null)
            {
                return NotFound();
            }

            return View("Owner", new AdminOwnerDetailPageViewModel
            {
                Detail = detail,
                Edit = model
            });
        }

        try
        {
            await adminService.UpdateOwnerAsync(
                new ASM.Application.Contracts.UpdateOwnerProfileRequest(
                    model.OwnerUserId,
                    model.OwnerUserName,
                    model.OwnerFullName,
                    model.OwnerEmail,
                    model.OwnerPhoneNumber,
                    model.TenantName,
                    model.IsActive),
                cancellationToken);

            TempData["SuccessMessage"] = "Owner information updated successfully.";
            return RedirectToAction(nameof(Owner), new { id = model.OwnerUserId });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var detail = await adminService.GetOwnerDetailsAsync(model.OwnerUserId, cancellationToken);
            if (detail is null)
            {
                return NotFound();
            }

            return View("Owner", new AdminOwnerDetailPageViewModel
            {
                Detail = detail,
                Edit = model
            });
        }
    }
}
