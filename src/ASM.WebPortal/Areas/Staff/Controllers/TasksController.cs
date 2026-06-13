using ASM.Application.Contracts;
using ASM.Application.Interfaces;
using ASM.Domain.Constants;
using ASM.Domain.Enums;
using ASM.WebPortal.Areas.Staff.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace ASM.WebPortal.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize(Roles = RoleNames.Staff)]
[Route("staff/[controller]/[action]/{id?}")]
public class TasksController(
    IOrderService orderService,
    IDashboardService dashboardService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? status, CancellationToken cancellationToken)
    {
        var allTasks = await orderService.GetMyTasksAsync(cancellationToken);
        var currentFilter = string.IsNullOrWhiteSpace(status) ? "all" : status.Trim().ToLowerInvariant();
        IReadOnlyCollection<TaskDto> filteredTasks = currentFilter switch
        {
            "pending" => allTasks.Where(task => task.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase)).ToArray(),
            "verified" => allTasks.Where(task => task.Status.Equals("Verified", StringComparison.OrdinalIgnoreCase)).ToArray(),
            "completed" => allTasks.Where(task => task.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase)).ToArray(),
            _ => allTasks
        };

        return View(new TaskListPageViewModel
        {
            Summary = await dashboardService.GetSummaryAsync(cancellationToken),
            AllTasks = allTasks,
            Tasks = filteredTasks,
            CurrentFilter = currentFilter
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var task = await orderService.GetTaskAsync(id, cancellationToken);
        if (task is null)
        {
            return NotFound();
        }

        var inboundOrder = task.TaskType == TaskType.Inbound && task.InboundOrderId.HasValue
            ? await orderService.GetInboundOrderAsync(task.InboundOrderId.Value, cancellationToken)
            : null;
        var outboundOrder = task.TaskType == TaskType.Outbound && task.OutboundOrderId.HasValue
            ? await orderService.GetOutboundOrderAsync(task.OutboundOrderId.Value, cancellationToken)
            : null;

        return View(new TaskDetailPageViewModel
        {
            Task = task,
            InboundOrder = inboundOrder,
            OutboundOrder = outboundOrder,
            FeedbackMessage = TempData["TaskFeedback"] as string,
            LastScanSucceeded = TempData["TaskSuccess"] is string success ? bool.Parse(success) : null
        });
    }

    [HttpGet]
    public async Task<IActionResult> Scan(Guid id, CancellationToken cancellationToken)
    {
        var task = await orderService.GetTaskAsync(id, cancellationToken);
        if (task is null)
        {
            return NotFound();
        }

        var progress = GetProgressState(id);
        return View(await BuildExecutionViewModelAsync(
            task,
            progress,
            TempData["TaskFeedback"] as string,
            TempData["TaskSuccess"] is string success ? bool.Parse(success) : null,
            cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Start(Guid id)
    {
        var progress = GetProgressState(id);
        progress.Started = true;
        SaveProgressState(id, progress);

        TempData["TaskFeedback"] = "Da xac nhan bat dau. Tiep tuc quet dung ma QR theo huong dan ben duoi.";
        TempData["TaskSuccess"] = bool.TrueString;
        return RedirectToAction(nameof(Scan), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Verify(Guid id, string stepKey, string? manualPayload, string? cameraPayload, CancellationToken cancellationToken)
    {
        var task = await orderService.GetTaskAsync(id, cancellationToken);
        if (task is null)
        {
            return NotFound();
        }

        var payload = string.IsNullOrWhiteSpace(manualPayload) ? cameraPayload : manualPayload;
        if (string.IsNullOrWhiteSpace(payload))
        {
            TempData["TaskFeedback"] = "Chua co ma QR de xac minh. Hay quet bang camera hoac nhap thu cong.";
            TempData["TaskSuccess"] = bool.FalseString;
            return RedirectToAction(nameof(Scan), new { id });
        }

        var result = await orderService.VerifyTaskStepAsync(new TaskStepVerifyRequest(id, stepKey, payload), cancellationToken);
        var progress = GetProgressState(id);
        if (result.IsValid)
        {
            progress.VerifiedPayloads[stepKey] = payload;
            SaveProgressState(id, progress);
        }

        TempData["TaskFeedback"] = result.Message;
        TempData["TaskSuccess"] = result.IsValid.ToString();
        return RedirectToAction(nameof(Scan), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(Guid id, CancellationToken cancellationToken)
    {
        var task = await orderService.GetTaskAsync(id, cancellationToken);
        if (task is null)
        {
            return NotFound();
        }

        if (task.TaskType == TaskType.Inbound && task.InboundOrderId.HasValue)
        {
            await orderService.CompleteInboundOrderAsync(task.InboundOrderId.Value, cancellationToken);
        }
        else if (task.TaskType == TaskType.Outbound && task.OutboundOrderId.HasValue)
        {
            await orderService.CompleteOutboundOrderAsync(task.OutboundOrderId.Value, cancellationToken);
        }
        else
        {
            throw new InvalidOperationException("Task khong co order hop le de hoan thanh.");
        }

        ClearProgressState(id);
        TempData["TaskFeedback"] = "Da hoan thanh task va cap nhat ton kho thanh cong.";
        TempData["TaskSuccess"] = bool.TrueString;
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<TaskExecutionPageViewModel> BuildExecutionViewModelAsync(
        TaskDto task,
        TaskExecutionProgressState progress,
        string? feedbackMessage,
        bool? lastActionSucceeded,
        CancellationToken cancellationToken)
    {
        InboundOrderDto? inboundOrder = null;
        OutboundOrderDto? outboundOrder = null;

        if (task.TaskType == TaskType.Inbound && task.InboundOrderId.HasValue)
        {
            inboundOrder = await orderService.GetInboundOrderAsync(task.InboundOrderId.Value, cancellationToken)
                ?? throw new InvalidOperationException("Khong tim thay lenh nhap cho task nay.");
        }

        if (task.TaskType == TaskType.Outbound && task.OutboundOrderId.HasValue)
        {
            outboundOrder = await orderService.GetOutboundOrderAsync(task.OutboundOrderId.Value, cancellationToken)
                ?? throw new InvalidOperationException("Khong tim thay lenh xuat cho task nay.");
        }

        var steps = BuildSteps(task, inboundOrder, outboundOrder, progress);
        return new TaskExecutionPageViewModel
        {
            Task = task,
            InboundOrder = inboundOrder,
            OutboundOrder = outboundOrder,
            Steps = steps,
            FeedbackMessage = feedbackMessage,
            LastActionSucceeded = lastActionSucceeded,
            CanComplete = task.Status.Equals("Verified", StringComparison.OrdinalIgnoreCase) &&
                          steps.All(step => step.IsCompleted)
        };
    }

    private static IReadOnlyCollection<TaskExecutionStepViewModel> BuildSteps(
        TaskDto task,
        InboundOrderDto? inboundOrder,
        OutboundOrderDto? outboundOrder,
        TaskExecutionProgressState progress)
    {
        var line = inboundOrder?.Lines.SingleOrDefault();
        var outboundLine = outboundOrder?.Lines.SingleOrDefault();
        var stepOrder = TaskExecutionFlow.GetStepOrder(task.TaskType);

        var steps = new List<TaskExecutionStepViewModel>
        {
            new()
            {
                Number = 1,
                Key = TaskExecutionKeys.Start,
                Title = task.TaskType == TaskType.Inbound ? "Xac nhan bat dau nhap kho" : "Xac nhan bat dau xuat kho",
                Instruction = task.TaskType == TaskType.Inbound
                    ? $"Nhan nhiem vu {task.ReferenceCode}, chuan bi xu ly {line?.Quantity} san pham {line?.ProductName} den {line?.TargetSlotPath}."
                    : $"Nhan nhiem vu {task.ReferenceCode}, chuan bi lay {outboundLine?.Quantity} san pham {outboundLine?.ProductName} tai {outboundLine?.SourceSlotPath}.",
                ExpectedLabel = "Warehouse",
                ExpectedValue = inboundOrder?.WarehouseName ?? outboundOrder?.WarehouseName ?? string.Empty,
                ActionLabel = "Xac nhan bat dau",
                CameraLabel = string.Empty,
                IsCompleted = progress.Started,
                RequiresScan = false
            }
        };

        if (task.TaskType == TaskType.Inbound && line is not null)
        {
            steps.Add(new TaskExecutionStepViewModel
            {
                Number = 2,
                Key = TaskExecutionKeys.Product,
                Title = "Quet san pham nhap kho",
                Instruction = $"Quet QR san pham {line.ProductName} truoc khi xac nhan pallet dua vao kho.",
                ExpectedLabel = "Expected product",
                ExpectedValue = $"{line.ProductName} x {line.Quantity}",
                ActionLabel = "Xac nhan san pham",
                CameraLabel = "Quet san pham bang camera",
                ManualPayload = progress.VerifiedPayloads.GetValueOrDefault(TaskExecutionKeys.Product) ?? string.Empty,
                IsCompleted = progress.IsStepCompleted(TaskExecutionKeys.Product)
            });
            steps.Add(new TaskExecutionStepViewModel
            {
                Number = 3,
                Key = TaskExecutionKeys.Pallet,
                Title = "Quet pallet nhap kho",
                Instruction = $"Dam bao ban dang day dung pallet {line.PalletCode} chua hang {line.ProductName}.",
                ExpectedLabel = "Expected pallet",
                ExpectedValue = line.PalletCode,
                ActionLabel = "Xac nhan pallet",
                CameraLabel = "Quet pallet bang camera",
                ManualPayload = progress.VerifiedPayloads.GetValueOrDefault(TaskExecutionKeys.Pallet) ?? string.Empty,
                IsCompleted = progress.IsStepCompleted(TaskExecutionKeys.Pallet)
            });
            steps.Add(new TaskExecutionStepViewModel
            {
                Number = 4,
                Key = TaskExecutionKeys.Slot,
                Title = "Quet slot dich de put-away",
                Instruction = $"Dua pallet den dung vi tri {line.TargetSlotPath}, sau do quet QR vi tri de xac nhan dat hang.",
                ExpectedLabel = "Target slot",
                ExpectedValue = line.TargetSlotPath,
                ActionLabel = "Xac nhan slot",
                CameraLabel = "Quet slot bang camera",
                ManualPayload = progress.VerifiedPayloads.GetValueOrDefault(TaskExecutionKeys.Slot) ?? string.Empty,
                IsCompleted = progress.IsStepCompleted(TaskExecutionKeys.Slot) || task.Status.Equals("Verified", StringComparison.OrdinalIgnoreCase)
            });
        }

        if (task.TaskType == TaskType.Outbound && outboundLine is not null)
        {
            steps.Add(new TaskExecutionStepViewModel
            {
                Number = 2,
                Key = TaskExecutionKeys.Slot,
                Title = "Quet slot nguon de tim hang",
                Instruction = $"Di den {outboundLine.SourceSlotPath} va quet ma vi tri truoc khi lay pallet.",
                ExpectedLabel = "Source slot",
                ExpectedValue = outboundLine.SourceSlotPath,
                ActionLabel = "Xac nhan slot nguon",
                CameraLabel = "Quet slot bang camera",
                ManualPayload = progress.VerifiedPayloads.GetValueOrDefault(TaskExecutionKeys.Slot) ?? string.Empty,
                IsCompleted = progress.IsStepCompleted(TaskExecutionKeys.Slot)
            });
            steps.Add(new TaskExecutionStepViewModel
            {
                Number = 3,
                Key = TaskExecutionKeys.Pallet,
                Title = "Quet pallet can xuat",
                Instruction = $"Sau khi den dung vi tri {outboundLine.SourceSlotPath}, quet pallet {outboundLine.SourcePalletCode} de xac nhan lay dung hang.",
                ExpectedLabel = "Source pallet",
                ExpectedValue = outboundLine.SourcePalletCode,
                ActionLabel = "Xac nhan pallet",
                CameraLabel = "Quet pallet bang camera",
                ManualPayload = progress.VerifiedPayloads.GetValueOrDefault(TaskExecutionKeys.Pallet) ?? string.Empty,
                IsCompleted = progress.IsStepCompleted(TaskExecutionKeys.Pallet) || task.Status.Equals("Verified", StringComparison.OrdinalIgnoreCase)
            });
        }

        var currentStepKey = stepOrder.FirstOrDefault(stepKey => !progress.IsStepCompleted(stepKey));
        if (task.Status.Equals("Verified", StringComparison.OrdinalIgnoreCase))
        {
            currentStepKey = null;
        }

        foreach (var step in steps)
        {
            step.IsCurrent = currentStepKey == step.Key;
        }

        return steps;
    }

    private TaskExecutionProgressState GetProgressState(Guid taskId)
    {
        var raw = HttpContext.Session.GetString(BuildProgressKey(taskId));
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new TaskExecutionProgressState();
        }

        return JsonSerializer.Deserialize<TaskExecutionProgressState>(raw) ?? new TaskExecutionProgressState();
    }

    private void SaveProgressState(Guid taskId, TaskExecutionProgressState state)
    {
        HttpContext.Session.SetString(BuildProgressKey(taskId), JsonSerializer.Serialize(state));
    }

    private void ClearProgressState(Guid taskId)
    {
        HttpContext.Session.Remove(BuildProgressKey(taskId));
    }

    private static string BuildProgressKey(Guid taskId) => $"staff-task-progress:{taskId}";
}
