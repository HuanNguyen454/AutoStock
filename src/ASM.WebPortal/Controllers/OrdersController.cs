using ASM.Application.Contracts;
using ASM.Application.Interfaces;
using ASM.Domain.Constants;
using ASM.WebPortal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ASM.WebPortal.Controllers;

[Authorize(Roles = $"{RoleNames.Owner},{RoleNames.Manager}")]
public class OrdersController(
    IWarehouseService warehouseService,
    ICatalogService catalogService,
    IOrderService orderService,
    IUserService userService) : Controller
{
    public async Task<IActionResult> Inbound(CancellationToken cancellationToken)
    {
        return View(await BuildInboundModelAsync(cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateInbound(InboundPageViewModel model, CancellationToken cancellationToken)
    {
        await orderService.CreateInboundOrderAsync(
            new CreateInboundOrderRequest(
                model.WarehouseId,
                model.AssignedToUserId,
                model.ProductId,
                model.PalletId,
                model.TargetSlotId,
                model.Quantity,
                model.ReferenceCode,
                model.LotNumber,
                model.ExpiryDate),
            cancellationToken);
        return RedirectToAction(nameof(Inbound));
    }

    public async Task<IActionResult> Outbound(CancellationToken cancellationToken)
    {
        return View(await BuildOutboundModelAsync(cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateOutbound(OutboundPageViewModel model, CancellationToken cancellationToken)
    {
        await orderService.CreateOutboundOrderAsync(
            new CreateOutboundOrderRequest(
                model.WarehouseId,
                model.AssignedToUserId,
                model.ProductId,
                model.SourcePalletId,
                model.SourceSlotId,
                model.Quantity,
                model.ReferenceCode),
            cancellationToken);
        return RedirectToAction(nameof(Outbound));
    }

    private async Task<InboundPageViewModel> BuildInboundModelAsync(CancellationToken cancellationToken)
    {
        var warehouses = await warehouseService.GetWarehousesAsync(cancellationToken);
        var layouts = await LoadLayoutsAsync(warehouses, cancellationToken);
        var pallets = await catalogService.GetPalletsAsync(cancellationToken);
        var slotOptions = BuildSlotOptions(layouts);
        var palletOptions = BuildPalletOptions(warehouses, layouts, pallets);
        var users = await userService.GetUsersAsync(cancellationToken);

        return new InboundPageViewModel
        {
            Orders = await orderService.GetInboundOrdersAsync(cancellationToken),
            Warehouses = warehouses,
            Products = await catalogService.GetProductsAsync(cancellationToken),
            Users = users.Where(x => x.Role == RoleNames.Staff && x.IsActive).ToList(),
            SlotOptions = slotOptions,
            PalletOptions = palletOptions,
            WarehouseId = warehouses.FirstOrDefault()?.Id ?? Guid.Empty,
            PalletId = palletOptions.FirstOrDefault()?.Id ?? Guid.Empty,
            TargetSlotId = slotOptions.FirstOrDefault()?.Id ?? Guid.Empty
        };
    }

    private async Task<OutboundPageViewModel> BuildOutboundModelAsync(CancellationToken cancellationToken)
    {
        var warehouses = await warehouseService.GetWarehousesAsync(cancellationToken);
        var layouts = await LoadLayoutsAsync(warehouses, cancellationToken);
        var pallets = await catalogService.GetPalletsAsync(cancellationToken);
        var slotOptions = BuildSlotOptions(layouts);
        var palletOptions = BuildPalletOptions(warehouses, layouts, pallets)
            .Where(x => !x.Note.Contains("No slot assigned", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var users = await userService.GetUsersAsync(cancellationToken);

        return new OutboundPageViewModel
        {
            Orders = await orderService.GetOutboundOrdersAsync(cancellationToken),
            Warehouses = warehouses,
            Products = await catalogService.GetProductsAsync(cancellationToken),
            Users = users.Where(x => x.Role == RoleNames.Staff && x.IsActive).ToList(),
            SlotOptions = slotOptions,
            PalletOptions = palletOptions,
            WarehouseId = warehouses.FirstOrDefault()?.Id ?? Guid.Empty,
            SourcePalletId = palletOptions.FirstOrDefault()?.Id ?? Guid.Empty,
            SourceSlotId = slotOptions.FirstOrDefault()?.Id ?? Guid.Empty
        };
    }

    private async Task<Dictionary<Guid, WarehouseLayoutDto>> LoadLayoutsAsync(
        IReadOnlyCollection<WarehouseDto> warehouses,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, WarehouseLayoutDto>();
        foreach (var warehouse in warehouses)
        {
            var layout = await warehouseService.GetLayoutAsync(warehouse.Id, cancellationToken);
            if (layout is not null)
            {
                result[warehouse.Id] = layout;
            }
        }

        return result;
    }

    private static IReadOnlyCollection<HierarchySelectOptionViewModel> BuildSlotOptions(
        IReadOnlyDictionary<Guid, WarehouseLayoutDto> layouts)
    {
        return layouts.Values
            .SelectMany(layout => layout.Areas.SelectMany(area =>
                area.Racks.SelectMany(rack =>
                    rack.Slots.Select(slot => new HierarchySelectOptionViewModel
                    {
                        Id = slot.Id,
                        WarehouseId = layout.Warehouse.Id,
                        Label = slot.Name,
                        Path = $"{layout.Warehouse.Name} > {area.Name} > {rack.Name} > {slot.Name}",
                        Note = slot.IsOccupied ? "Occupied slot" : "Open slot"
                    }))))
            .OrderBy(x => x.Path)
            .ToList();
    }

    private static IReadOnlyCollection<HierarchySelectOptionViewModel> BuildPalletOptions(
        IReadOnlyCollection<WarehouseDto> warehouses,
        IReadOnlyDictionary<Guid, WarehouseLayoutDto> layouts,
        IReadOnlyCollection<PalletDto> pallets)
    {
        var slotPaths = BuildSlotPathLookup(layouts);
        var warehouseLookup = warehouses.ToDictionary(x => x.Id);

        return pallets
            .Select(pallet =>
            {
                var warehouseName = warehouseLookup.TryGetValue(pallet.WarehouseId, out var warehouse)
                    ? warehouse.Name
                    : "Unknown warehouse";
                var path = pallet.CurrentSlotId.HasValue && slotPaths.TryGetValue(pallet.CurrentSlotId.Value, out var slotPath)
                    ? slotPath
                    : $"{warehouseName} > No slot assigned";

                return new HierarchySelectOptionViewModel
                {
                    Id = pallet.Id,
                    WarehouseId = pallet.WarehouseId,
                    Label = pallet.Code,
                    Path = path,
                    Note = $"Status: {pallet.Status}"
                };
            })
            .OrderBy(x => x.Path)
            .ThenBy(x => x.Label)
            .ToList();
    }

    private static Dictionary<Guid, string> BuildSlotPathLookup(IReadOnlyDictionary<Guid, WarehouseLayoutDto> layouts)
    {
        var result = new Dictionary<Guid, string>();

        foreach (var layout in layouts.Values)
        {
            foreach (var area in layout.Areas)
            {
                foreach (var rack in area.Racks)
                {
                    foreach (var slot in rack.Slots)
                    {
                        result[slot.Id] = $"{layout.Warehouse.Name} > {area.Name} > {rack.Name} > {slot.Name}";
                    }
                }
            }
        }

        return result;
    }
}
