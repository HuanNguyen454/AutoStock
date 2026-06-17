using ASM.Application.Contracts;
using ASM.Application.Interfaces;
using ASM.Domain.Constants;
using ASM.Domain.Enums;
using ASM.WebPortal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ASM.WebPortal.Controllers;

[Authorize(Roles = $"{RoleNames.Owner},{RoleNames.Manager}")]
public class ManagementController(
    IWarehouseService warehouseService,
    ICatalogService catalogService,
    IQrService qrService,
    IUserService userService) : Controller
{
    public async Task<IActionResult> Warehouses(CancellationToken cancellationToken)
    {
        var warehouses = await warehouseService.GetWarehousesAsync(cancellationToken);
        return View(new WarehousesPageViewModel
        {
            Warehouses = warehouses,
            QrCodes = await EnsureQrsAsync(
                warehouses,
                x => x.Id,
                x => $"Warehouse - {x.Name}",
                QrTargetType.Warehouse,
                cancellationToken)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateWarehouse(WarehousesPageViewModel model, CancellationToken cancellationToken)
    {
        var created = await warehouseService.CreateWarehouseAsync(
            new CreateWarehouseRequest(model.Name, model.Code, model.Address),
            cancellationToken);
        await qrService.GenerateAsync(
            new CreateQrRequest(QrTargetType.Warehouse, created.Id, $"Warehouse - {created.Name}"),
            cancellationToken);
        return RedirectToAction(nameof(Warehouses));
    }

    public async Task<IActionResult> Layout(Guid? warehouseId, CancellationToken cancellationToken)
    {
        var warehouses = await warehouseService.GetWarehousesAsync(cancellationToken);
        var selectedId = warehouseId ?? warehouses.FirstOrDefault()?.Id ?? Guid.Empty;
        var layout = selectedId == Guid.Empty
            ? null
            : await warehouseService.GetLayoutAsync(selectedId, cancellationToken);
        IReadOnlyCollection<PalletDto> pallets = selectedId == Guid.Empty
            ? Array.Empty<PalletDto>()
            : (await catalogService.GetPalletsAsync(cancellationToken)).Where(x => x.WarehouseId == selectedId).ToList();
        var qrCodes = new Dictionary<Guid, QrCodeDto>();

        if (layout is not null)
        {
            Merge(qrCodes, await EnsureQrsAsync(
                new[] { layout.Warehouse },
                x => x.Id,
                x => $"Warehouse - {x.Name}",
                QrTargetType.Warehouse,
                cancellationToken));

            Merge(qrCodes, await EnsureQrsAsync(
                layout.Areas,
                x => x.Id,
                x => $"Area - {x.Name}",
                QrTargetType.Area,
                cancellationToken));

            var racks = layout.Areas.SelectMany(x => x.Racks).ToList();
            Merge(qrCodes, await EnsureQrsAsync(
                racks,
                x => x.Id,
                x => $"Rack - {x.Name}",
                QrTargetType.Rack,
                cancellationToken));

            var slots = racks.SelectMany(x => x.Slots).ToList();
            Merge(qrCodes, await EnsureQrsAsync(
                slots,
                x => x.Id,
                x => $"Slot - {x.Name}",
                QrTargetType.Slot,
                cancellationToken));
        }

        Merge(qrCodes, await EnsureQrsAsync(
            pallets,
            x => x.Id,
            x => $"Pallet - {x.Code}",
            QrTargetType.Pallet,
            cancellationToken));

        return View(new LayoutPageViewModel
        {
            Warehouses = warehouses,
            SelectedWarehouseId = selectedId,
            Layout = layout,
            Pallets = pallets,
            QrCodes = qrCodes
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddArea(LayoutPageViewModel model, CancellationToken cancellationToken)
    {
        var created = await warehouseService.AddAreaAsync(
            new CreateAreaRequest(model.SelectedWarehouseId, model.AreaName),
            cancellationToken);
        await qrService.GenerateAsync(
            new CreateQrRequest(QrTargetType.Area, created.Id, $"Area - {created.Name}"),
            cancellationToken);
        return RedirectToAction(nameof(Layout), new { warehouseId = model.SelectedWarehouseId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddRack(LayoutPageViewModel model, CancellationToken cancellationToken)
    {
        var created = await warehouseService.AddRackAsync(
            new CreateRackRequest(model.AreaId, model.RackName),
            cancellationToken);
        await qrService.GenerateAsync(
            new CreateQrRequest(QrTargetType.Rack, created.Id, $"Rack - {created.Name}"),
            cancellationToken);
        return RedirectToAction(nameof(Layout), new { warehouseId = model.SelectedWarehouseId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSlot(LayoutPageViewModel model, CancellationToken cancellationToken)
    {
        var created = await warehouseService.AddSlotAsync(
            new CreateSlotRequest(model.RackId, model.SlotName),
            cancellationToken);
        await qrService.GenerateAsync(
            new CreateQrRequest(QrTargetType.Slot, created.Id, $"Slot - {created.Name}"),
            cancellationToken);
        return RedirectToAction(nameof(Layout), new { warehouseId = model.SelectedWarehouseId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPallet(LayoutPageViewModel model, CancellationToken cancellationToken)
    {
        var created = await catalogService.CreatePalletAsync(
            new CreatePalletRequest(model.SelectedWarehouseId, model.PalletCode),
            cancellationToken);
        await qrService.GenerateAsync(
            new CreateQrRequest(QrTargetType.Pallet, created.Id, $"Pallet - {created.Code}"),
            cancellationToken);
        return RedirectToAction(nameof(Layout), new { warehouseId = model.SelectedWarehouseId });
    }

    public async Task<IActionResult> Products(string? keyword, CancellationToken cancellationToken)
    {
        var products = await catalogService.GetProductsAsync(cancellationToken);
        var hasSearched = !string.IsNullOrWhiteSpace(keyword);
        return View(new ProductsPageViewModel
        {
            Products = products,
            Keyword = keyword,
            HasSearched = hasSearched,
            LocationResults = hasSearched
                ? await catalogService.SearchProductLocationsAsync(keyword!, cancellationToken)
                : [],
            QrCodes = await EnsureQrsAsync(
                products,
                x => x.Id,
                x => $"Product - {x.Sku}",
                QrTargetType.Product,
                cancellationToken)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateProduct(ProductsPageViewModel model, CancellationToken cancellationToken)
    {
        var created = await catalogService.CreateProductAsync(
            new CreateProductRequest(model.Sku, model.Name, model.Description),
            cancellationToken);
        await qrService.GenerateAsync(
            new CreateQrRequest(QrTargetType.Product, created.Id, $"Product - {created.Sku}"),
            cancellationToken);
        return RedirectToAction(nameof(Products));
    }

    public async Task<IActionResult> Pallets(CancellationToken cancellationToken)
    {
        var pallets = await catalogService.GetPalletsAsync(cancellationToken);
        return View(new PalletsPageViewModel
        {
            Pallets = pallets,
            Warehouses = await warehouseService.GetWarehousesAsync(cancellationToken),
            QrCodes = await EnsureQrsAsync(
                pallets,
                x => x.Id,
                x => $"Pallet - {x.Code}",
                QrTargetType.Pallet,
                cancellationToken)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePallet(PalletsPageViewModel model, CancellationToken cancellationToken)
    {
        var created = await catalogService.CreatePalletAsync(
            new CreatePalletRequest(model.WarehouseId, model.Code),
            cancellationToken);
        await qrService.GenerateAsync(
            new CreateQrRequest(QrTargetType.Pallet, created.Id, $"Pallet - {created.Code}"),
            cancellationToken);
        return RedirectToAction(nameof(Pallets));
    }

    public async Task<IActionResult> Users(CancellationToken cancellationToken)
    {
        return View(new UsersPageViewModel
        {
            Users = await userService.GetUsersAsync(cancellationToken)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(UsersPageViewModel model, CancellationToken cancellationToken)
    {
        await userService.CreateUserAsync(
            new CreateUserRequest(model.UserName, model.FullName, model.Email, model.Password, model.Role),
            cancellationToken);
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleUser(Guid userId, CancellationToken cancellationToken)
    {
        await userService.ToggleUserStatusAsync(userId, cancellationToken);
        return RedirectToAction(nameof(Users));
    }

    [HttpGet]
    public IActionResult Qr() => View(new QrPageViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateQr(QrPageViewModel model, CancellationToken cancellationToken)
    {
        model.GeneratedQr = await qrService.GenerateAsync(
            new CreateQrRequest(model.TargetType, model.TargetId, model.Label),
            cancellationToken);
        return View("Qr", model);
    }

    [HttpGet]
    public async Task<IActionResult> DownloadQr(Guid qrId, CancellationToken cancellationToken)
    {
        var bytes = await qrService.RenderPngAsync(qrId, cancellationToken);
        return File(bytes, "image/png", $"qr-{qrId}.png");
    }

    private async Task<IReadOnlyDictionary<Guid, QrCodeDto>> EnsureQrsAsync<T>(
        IEnumerable<T> items,
        Func<T, Guid> idSelector,
        Func<T, string> labelSelector,
        QrTargetType targetType,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, QrCodeDto>();
        foreach (var item in items)
        {
            var id = idSelector(item);
            result[id] = await qrService.GenerateAsync(
                new CreateQrRequest(targetType, id, labelSelector(item)),
                cancellationToken);
        }

        return result;
    }

    private static void Merge(IDictionary<Guid, QrCodeDto> target, IReadOnlyDictionary<Guid, QrCodeDto> source)
    {
        foreach (var kvp in source)
        {
            target[kvp.Key] = kvp.Value;
        }
    }
}
