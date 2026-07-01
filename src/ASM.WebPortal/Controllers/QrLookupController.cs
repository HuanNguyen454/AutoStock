using ASM.Application.Interfaces;
using ASM.Domain.Constants;
using ASM.WebPortal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ASM.WebPortal.Controllers;

[Authorize(Roles = $"{RoleNames.Owner},{RoleNames.Manager},{RoleNames.Staff}")]
public class QrLookupController(IQrService qrService) : Controller
{
    [HttpGet]
    public IActionResult Index() => View(new QrLookupPageViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Lookup(QrLookupPageViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", model);
        }

        try
        {
            model.Result = await qrService.LookupAsync(model.Payload, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            model.ErrorMessage = ex.Message;
        }

        return View("Index", model);
    }
}
