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
        try
        {
            var created = await warehouseService.CreateWarehouseAsync(
                new CreateWarehouseRequest(model.Name, model.Code, model.Address),
                cancellationToken);
            await qrService.GenerateAsync(
                new CreateQrRequest(QrTargetType.Warehouse, created.Id, $"Warehouse - {created.Name}"),
                cancellationToken);
            TempData["SuccessMessage"] = "Warehouse created.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Warehouses));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateWarehouse(Guid warehouseId, string name, string code, string address, CancellationToken cancellationToken)
    {
        try
        {
            await warehouseService.UpdateWarehouseAsync(
                new UpdateWarehouseRequest(warehouseId, name, code, address),
                cancellationToken);
            TempData["SuccessMessage"] = "Warehouse updated.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Warehouses));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteWarehouse(Guid warehouseId, CancellationToken cancellationToken)
    {
        try
        {
            await warehouseService.DeleteWarehouseAsync(warehouseId, cancellationToken);
            TempData["SuccessMessage"] = "Warehouse deleted.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

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
        var products = await catalogService.GetProductsAsync(cancellationToken);
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
            Products = products,
            QrCodes = qrCodes
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddArea(LayoutPageViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            var created = await warehouseService.AddAreaAsync(
                new CreateAreaRequest(model.SelectedWarehouseId, model.AreaName),
                cancellationToken);
            await qrService.GenerateAsync(
                new CreateQrRequest(QrTargetType.Area, created.Id, $"Area - {created.Name}"),
                cancellationToken);
            TempData["SuccessMessage"] = "Area created.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Layout), new { warehouseId = model.SelectedWarehouseId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateArea(Guid selectedWarehouseId, Guid areaId, string areaName, CancellationToken cancellationToken)
    {
        try
        {
            await warehouseService.UpdateAreaAsync(new UpdateAreaRequest(areaId, areaName), cancellationToken);
            TempData["SuccessMessage"] = "Area updated.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Layout), new { warehouseId = selectedWarehouseId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteArea(Guid selectedWarehouseId, Guid areaId, CancellationToken cancellationToken)
    {
        try
        {
            await warehouseService.DeleteAreaAsync(areaId, cancellationToken);
            TempData["SuccessMessage"] = "Area deleted.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Layout), new { warehouseId = selectedWarehouseId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddRack(LayoutPageViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            var created = await warehouseService.AddRackAsync(
                new CreateRackRequest(model.AreaId, model.RackName),
                cancellationToken);
            await qrService.GenerateAsync(
                new CreateQrRequest(QrTargetType.Rack, created.Id, $"Rack - {created.Name}"),
                cancellationToken);
            TempData["SuccessMessage"] = "Rack created.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Layout), new { warehouseId = model.SelectedWarehouseId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateRack(Guid selectedWarehouseId, Guid rackId, string rackName, CancellationToken cancellationToken)
    {
        try
        {
            await warehouseService.UpdateRackAsync(new UpdateRackRequest(rackId, rackName), cancellationToken);
            TempData["SuccessMessage"] = "Rack updated.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Layout), new { warehouseId = selectedWarehouseId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRack(Guid selectedWarehouseId, Guid rackId, CancellationToken cancellationToken)
    {
        try
        {
            await warehouseService.DeleteRackAsync(rackId, cancellationToken);
            TempData["SuccessMessage"] = "Rack deleted.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Layout), new { warehouseId = selectedWarehouseId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSlot(LayoutPageViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            var created = await warehouseService.AddSlotAsync(
                new CreateSlotRequest(model.RackId, model.SlotName),
                cancellationToken);
            await qrService.GenerateAsync(
                new CreateQrRequest(QrTargetType.Slot, created.Id, $"Slot - {created.Name}"),
                cancellationToken);
            TempData["SuccessMessage"] = "Slot created.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Layout), new { warehouseId = model.SelectedWarehouseId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSlot(Guid selectedWarehouseId, Guid slotId, string slotName, CancellationToken cancellationToken)
    {
        try
        {
            await warehouseService.UpdateSlotAsync(new UpdateSlotRequest(slotId, slotName), cancellationToken);
            TempData["SuccessMessage"] = "Slot updated.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Layout), new { warehouseId = selectedWarehouseId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSlot(Guid selectedWarehouseId, Guid slotId, CancellationToken cancellationToken)
    {
        try
        {
            await warehouseService.DeleteSlotAsync(slotId, cancellationToken);
            TempData["SuccessMessage"] = "Slot deleted.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Layout), new { warehouseId = selectedWarehouseId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPallet(LayoutPageViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            var created = await catalogService.CreatePalletAsync(
                new CreatePalletRequest(model.SelectedWarehouseId, model.PalletCode),
                cancellationToken);
            await qrService.GenerateAsync(
                new CreateQrRequest(QrTargetType.Pallet, created.Id, $"Pallet - {created.Code}"),
                cancellationToken);
            TempData["SuccessMessage"] = "Pallet created.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Layout), new { warehouseId = model.SelectedWarehouseId });
    }

    public async Task<IActionResult> Products(string? keyword, Guid? categoryId, CancellationToken cancellationToken)
    {
        var products = await catalogService.GetProductsAsync(categoryId, keyword, cancellationToken);
        var categories = await catalogService.GetCategoriesAsync(cancellationToken);
        var hasSearched = !string.IsNullOrWhiteSpace(keyword) || categoryId.HasValue;
        var locationResults = hasSearched
            ? await catalogService.SearchProductLocationsAsync(keyword, categoryId, cancellationToken)
            : [];
        var warehouseMaps = hasSearched
            ? await BuildProductWarehouseMapsAsync(locationResults, cancellationToken)
            : [];

        return View(new ProductsPageViewModel
        {
            Products = products,
            Categories = categories,
            Keyword = keyword,
            CategoryId = categoryId,
            HasSearched = hasSearched,
            LocationResults = locationResults,
            WarehouseMaps = warehouseMaps,
            QrCodes = await EnsureQrsAsync(
                products,
                x => x.Id,
                x => $"Product - {x.Sku}",
                QrTargetType.Product,
                cancellationToken)
        });
    }

    private async Task<IReadOnlyList<ProductWarehouseMapViewModel>> BuildProductWarehouseMapsAsync(
        IReadOnlyList<ProductLocationSearchResultDto> locationResults,
        CancellationToken cancellationToken)
    {
        var locatedResults = locationResults
            .Where(x => x.SlotId.HasValue)
            .ToList();
        if (locatedResults.Count == 0)
        {
            return [];
        }

        var matchesBySlot = locatedResults
            .GroupBy(x => x.SlotId!.Value)
            .ToDictionary(x => x.Key, x => x.ToList());
        var routeSummariesByWarehouse = locatedResults
            .Where(x => !string.IsNullOrWhiteSpace(x.WarehouseName) && !string.IsNullOrWhiteSpace(x.LocationPath))
            .GroupBy(x => x.WarehouseName!)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyCollection<string>)x
                    .Select(result => result.LocationPath)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(path => path)
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);

        var maps = new List<ProductWarehouseMapViewModel>();
        var warehouses = await warehouseService.GetWarehousesAsync(cancellationToken);
        foreach (var warehouse in warehouses)
        {
            var layout = await warehouseService.GetLayoutAsync(warehouse.Id, cancellationToken);
            if (layout is null)
            {
                continue;
            }

            var hasAnyMatch = layout.Areas
                .SelectMany(area => area.Racks)
                .SelectMany(rack => rack.Slots)
                .Any(slot => matchesBySlot.ContainsKey(slot.Id));
            if (!hasAnyMatch)
            {
                continue;
            }

            var areas = layout.Areas
                .Select(area =>
                {
                    var racks = area.Racks
                        .Select(rack => new ProductMapRackViewModel
                        {
                            Name = rack.Name,
                            Slots = rack.Slots
                                .Select(slot => BuildProductMapSlot(slot, matchesBySlot))
                                .ToList()
                        })
                        .ToList();

                    return new ProductMapAreaViewModel
                    {
                        Name = area.Name,
                        HighlightedSlotCount = racks.Sum(rack => rack.Slots.Count(slot => slot.HasMatch)),
                        Racks = racks
                    };
                })
                .ToList();

            maps.Add(new ProductWarehouseMapViewModel
            {
                WarehouseName = layout.Warehouse.Name,
                WarehouseCode = layout.Warehouse.Code,
                RouteSummaries = routeSummariesByWarehouse.GetValueOrDefault(layout.Warehouse.Name) ?? [],
                Areas = areas
            });
        }

        return maps;
    }

    private static ProductMapSlotViewModel BuildProductMapSlot(
        SlotDto slot,
        IReadOnlyDictionary<Guid, List<ProductLocationSearchResultDto>> matchesBySlot)
    {
        if (!matchesBySlot.TryGetValue(slot.Id, out var matches))
        {
            return new ProductMapSlotViewModel
            {
                Id = slot.Id,
                Name = slot.Name,
                IsOccupied = slot.IsOccupied
            };
        }

        var productNames = matches
            .Select(x => x.ProductName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();
        var palletCodes = matches
            .Select(x => x.PalletCode)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();
        var totalQuantity = matches.Sum(x => x.InventoryItemId.HasValue ? x.Quantity : 0);

        return new ProductMapSlotViewModel
        {
            Id = slot.Id,
            Name = slot.Name,
            IsOccupied = slot.IsOccupied,
            HasMatch = true,
            MatchSummary = string.Join(", ", productNames),
            QuantitySummary = totalQuantity > 0 ? $"Qty {totalQuantity}" : string.Empty,
            Note = palletCodes.Count > 0 ? $"Pallet {string.Join(", ", palletCodes)}" : string.Empty
        };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateProduct(ProductsPageViewModel model, CancellationToken cancellationToken)
    {
        var created = await catalogService.CreateProductAsync(
            new CreateProductRequest(model.Sku, model.Name, model.Description, model.CategoryId, model.Brand),
            cancellationToken);
        await qrService.GenerateAsync(
            new CreateQrRequest(QrTargetType.Product, created.Id, $"Product - {created.Sku}"),
            cancellationToken);
        return RedirectToAction(nameof(Products));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCategory(ProductsPageViewModel model, CancellationToken cancellationToken)
    {
        await catalogService.CreateCategoryAsync(
            new CreateCategoryRequest(model.CategoryCode, model.CategoryName, model.CategoryDescription),
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
        try
        {
            var created = await catalogService.CreatePalletAsync(
                new CreatePalletRequest(model.WarehouseId, model.Code),
                cancellationToken);
            await qrService.GenerateAsync(
                new CreateQrRequest(QrTargetType.Pallet, created.Id, $"Pallet - {created.Code}"),
                cancellationToken);
            TempData["SuccessMessage"] = "Pallet created.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Pallets));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePallet(Guid palletId, string code, Guid? selectedWarehouseId, CancellationToken cancellationToken)
    {
        try
        {
            await catalogService.UpdatePalletAsync(new UpdatePalletRequest(palletId, code), cancellationToken);
            TempData["SuccessMessage"] = "Pallet updated.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        if (selectedWarehouseId.HasValue)
        {
            return RedirectToAction(nameof(Layout), new { warehouseId = selectedWarehouseId.Value });
        }

        return RedirectToAction(nameof(Pallets));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePallet(Guid palletId, Guid? selectedWarehouseId, CancellationToken cancellationToken)
    {
        try
        {
            await catalogService.DeletePalletAsync(palletId, cancellationToken);
            TempData["SuccessMessage"] = "Pallet deleted.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        if (selectedWarehouseId.HasValue)
        {
            return RedirectToAction(nameof(Layout), new { warehouseId = selectedWarehouseId.Value });
        }

        return RedirectToAction(nameof(Pallets));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignPalletSlot(Guid selectedWarehouseId, Guid palletId, Guid slotId, CancellationToken cancellationToken)
    {
        try
        {
            await catalogService.AssignPalletToSlotAsync(new AssignPalletSlotRequest(palletId, slotId), cancellationToken);
            TempData["SuccessMessage"] = "Pallet location updated.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Layout), new { warehouseId = selectedWarehouseId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddInitialInventory(
        Guid selectedWarehouseId,
        Guid palletId,
        Guid productId,
        int quantity,
        string? lotNumber,
        DateTime? expiryDate,
        CancellationToken cancellationToken)
    {
        try
        {
            await catalogService.AddInitialInventoryAsync(
                new AddInitialInventoryRequest(palletId, productId, quantity, lotNumber, expiryDate),
                cancellationToken);
            TempData["SuccessMessage"] = "Initial stock added to pallet.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Layout), new { warehouseId = selectedWarehouseId });
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
