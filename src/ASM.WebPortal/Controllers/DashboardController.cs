using ASM.Application.Interfaces;
using ASM.Domain.Constants;
using ASM.WebPortal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ASM.WebPortal.Controllers;

[Authorize(Roles = $"{RoleNames.Owner},{RoleNames.Manager}")]
public class DashboardController(
    IDashboardService dashboardService,
    IOrderService orderService) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var vm = new DashboardPageViewModel
        {
            Summary = await dashboardService.GetSummaryAsync(cancellationToken),
            InboundOrders = await orderService.GetInboundOrdersAsync(cancellationToken),
            OutboundOrders = await orderService.GetOutboundOrdersAsync(cancellationToken)
        };
        return View(vm);
    }
}
