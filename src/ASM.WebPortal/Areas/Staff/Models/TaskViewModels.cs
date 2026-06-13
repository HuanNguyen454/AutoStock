using ASM.Application.Contracts;
using ASM.Domain.Enums;

namespace ASM.WebPortal.Areas.Staff.Models;

public class TaskListPageViewModel
{
    public DashboardSummaryDto Summary { get; set; } = new(0, 0, 0, 0, 0, 0, []);
    public IReadOnlyCollection<TaskDto> AllTasks { get; set; } = [];
    public IReadOnlyCollection<TaskDto> Tasks { get; set; } = [];
    public string CurrentFilter { get; set; } = "all";
}

public class TaskDetailPageViewModel
{
    public TaskDto Task { get; set; } = default!;
    public InboundOrderDto? InboundOrder { get; set; }
    public OutboundOrderDto? OutboundOrder { get; set; }
    public string? FeedbackMessage { get; set; }
    public bool? LastScanSucceeded { get; set; }
}

public class TaskExecutionPageViewModel
{
    public TaskDto Task { get; set; } = default!;
    public InboundOrderDto? InboundOrder { get; set; }
    public OutboundOrderDto? OutboundOrder { get; set; }
    public IReadOnlyCollection<TaskExecutionStepViewModel> Steps { get; set; } = [];
    public string? FeedbackMessage { get; set; }
    public bool? LastActionSucceeded { get; set; }
    public bool CanComplete { get; set; }
    public bool IsCompleted => Task.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase);
    public string WarehouseName =>
        InboundOrder?.WarehouseName
        ?? OutboundOrder?.WarehouseName
        ?? string.Empty;
}

public class TaskExecutionStepViewModel
{
    public int Number { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Instruction { get; set; } = string.Empty;
    public string ExpectedLabel { get; set; } = string.Empty;
    public string ExpectedValue { get; set; } = string.Empty;
    public string ActionLabel { get; set; } = string.Empty;
    public string CameraLabel { get; set; } = string.Empty;
    public string ManualPayload { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public bool IsCurrent { get; set; }
    public bool RequiresScan { get; set; } = true;
}

public class TaskExecutionProgressState
{
    public bool Started { get; set; }
    public Dictionary<string, string> VerifiedPayloads { get; set; } = [];

    public bool IsStepCompleted(string stepKey)
    {
        return stepKey switch
        {
            "start" => Started,
            _ => VerifiedPayloads.ContainsKey(stepKey)
        };
    }
}

public static class TaskExecutionKeys
{
    public const string Start = "start";
    public const string Product = "product";
    public const string Pallet = "pallet";
    public const string Slot = "slot";
}

public static class TaskExecutionFlow
{
    public static IReadOnlyList<string> GetStepOrder(TaskType taskType) =>
        taskType == TaskType.Inbound
            ? [TaskExecutionKeys.Start, TaskExecutionKeys.Product, TaskExecutionKeys.Pallet, TaskExecutionKeys.Slot]
            : [TaskExecutionKeys.Start, TaskExecutionKeys.Slot, TaskExecutionKeys.Pallet];
}
