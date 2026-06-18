using ASM.Application.Contracts;
using ASM.Application.Interfaces;
using ASM.Domain.Constants;
using ASM.WebPortal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ASM.WebPortal.Controllers;

[Authorize(Roles = $"{RoleNames.Owner},{RoleNames.Manager}")]
public class DashboardController(
    IDashboardService dashboardService,
    IOrderService orderService,
    IWarehouseService warehouseService,
    ICatalogService catalogService) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var vm = new DashboardPageViewModel
        {
            Summary = await dashboardService.GetSummaryAsync(cancellationToken),
            InboundOrders = await orderService.GetInboundOrdersAsync(cancellationToken),
            OutboundOrders = await orderService.GetOutboundOrdersAsync(cancellationToken),
            WarehouseMaps = await BuildWarehouseMapsAsync(cancellationToken)
        };
        return View(vm);
    }

    private async Task<IReadOnlyCollection<DashboardWarehouseMapViewModel>> BuildWarehouseMapsAsync(CancellationToken cancellationToken)
    {
        var warehouses = await warehouseService.GetWarehousesAsync(cancellationToken);
        var pallets = await catalogService.GetPalletsAsync(cancellationToken);
        var palletsBySlot = pallets
            .Where(pallet => pallet.CurrentSlotId.HasValue)
            .GroupBy(pallet => pallet.CurrentSlotId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        var maps = new List<DashboardWarehouseMapViewModel>();
        foreach (var warehouse in warehouses)
        {
            var layout = await warehouseService.GetLayoutAsync(warehouse.Id, cancellationToken);
            if (layout is null)
            {
                continue;
            }

            var warehousePallets = pallets
                .Where(pallet => pallet.WarehouseId == warehouse.Id)
                .ToList();

            maps.Add(new DashboardWarehouseMapViewModel
            {
                WarehouseId = warehouse.Id,
                WarehouseName = warehouse.Name,
                WarehouseCode = warehouse.Code,
                AreaCount = layout.Areas.Count,
                RackCount = layout.Areas.Sum(area => area.Racks.Count),
                SlotCount = layout.Areas.Sum(area => area.Racks.Sum(rack => rack.Slots.Count)),
                PalletCount = warehousePallets.Count,
                ProductCount = warehousePallets
                    .SelectMany(pallet => pallet.InventoryItems)
                    .Where(item => item.Quantity > 0)
                    .Select(item => item.ProductId)
                    .Distinct()
                    .Count(),
                Areas = layout.Areas.Select(area =>
                {
                    var racks = area.Racks.Select(rack => new DashboardMapRackViewModel
                    {
                        Name = rack.Name,
                        Slots = rack.Slots.Select(slot =>
                        {
                            var slotPallets = palletsBySlot.TryGetValue(slot.Id, out var matchedPallets)
                                ? matchedPallets
                                : new List<PalletDto>();

                            return new DashboardMapSlotViewModel
                            {
                                Id = slot.Id,
                                Name = slot.Name,
                                IsOccupied = slot.IsOccupied || slotPallets.Count > 0,
                                Pallets = slotPallets
                                    .OrderBy(pallet => pallet.Code)
                                    .Select(MapPallet)
                                    .ToList()
                            };
                        }).ToList()
                    }).ToList();

                    return new DashboardMapAreaViewModel
                    {
                        Name = area.Name,
                        OccupiedSlotCount = racks.Sum(rack => rack.Slots.Count(slot => slot.IsOccupied || slot.Pallets.Any())),
                        Racks = racks
                    };
                }).ToList()
            });
        }

        return maps;
    }

    private static DashboardMapPalletViewModel MapPallet(PalletDto pallet) =>
        new()
        {
            Code = pallet.Code,
            Status = pallet.Status,
            Products = pallet.InventoryItems
                .Where(item => item.Quantity > 0)
                .OrderBy(item => item.ExpiryDate ?? DateTime.MaxValue)
                .ThenBy(item => item.ProductName)
                .Select(item => new DashboardMapProductViewModel
                {
                    Name = item.ProductName,
                    Quantity = item.Quantity,
                    LotNumber = item.LotNumber,
                    ExpiryDate = item.ExpiryDate
                })
                .ToList()
        };
}
