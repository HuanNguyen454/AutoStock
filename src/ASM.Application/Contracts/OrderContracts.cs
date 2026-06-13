using ASM.Domain.Enums;

namespace ASM.Application.Contracts;

public record InboundOrderDto(
    Guid Id,
    string ReferenceCode,
    Guid WarehouseId,
    string WarehouseName,
    Guid AssignedToUserId,
    string AssignedToUserName,
    string Status,
    DateTime CreatedAtUtc,
    IReadOnlyCollection<InboundOrderLineDto> Lines,
    Guid? TaskAssignmentId);

public record InboundOrderLineDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    Guid PalletId,
    string PalletCode,
    string PalletLocationPath,
    Guid TargetSlotId,
    string TargetSlotName,
    string TargetSlotPath,
    int Quantity);

public record CreateInboundOrderRequest(
    Guid WarehouseId,
    Guid AssignedToUserId,
    Guid ProductId,
    Guid PalletId,
    Guid TargetSlotId,
    int Quantity,
    string ReferenceCode);

public record OutboundOrderDto(
    Guid Id,
    string ReferenceCode,
    Guid WarehouseId,
    string WarehouseName,
    Guid AssignedToUserId,
    string AssignedToUserName,
    string Status,
    DateTime CreatedAtUtc,
    IReadOnlyCollection<OutboundOrderLineDto> Lines,
    Guid? TaskAssignmentId);

public record OutboundOrderLineDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    Guid SourcePalletId,
    string SourcePalletCode,
    string SourcePalletLocationPath,
    Guid SourceSlotId,
    string SourceSlotName,
    string SourceSlotPath,
    int Quantity);

public record CreateOutboundOrderRequest(
    Guid WarehouseId,
    Guid AssignedToUserId,
    Guid ProductId,
    Guid SourcePalletId,
    Guid SourceSlotId,
    int Quantity,
    string ReferenceCode);

public record TaskDto(
    Guid Id,
    TaskType TaskType,
    string Title,
    string Instruction,
    string Status,
    Guid? InboundOrderId,
    Guid? OutboundOrderId,
    string? ReferenceCode,
    string ExpectedTargetType,
    Guid ExpectedTargetId,
    DateTime CreatedAtUtc,
    DateTime? LastVerifiedAtUtc);

public record ScanVerifyRequest(Guid TaskAssignmentId, string Payload);

public record ScanVerifyResultDto(bool IsValid, string Message, DateTime LoggedAtUtc);

public record TaskStepVerifyRequest(Guid TaskAssignmentId, string StepKey, string Payload);

public record TaskStepVerifyResultDto(
    bool IsValid,
    string StepKey,
    string Message,
    string? NextStepKey,
    bool IsTaskReadyToComplete,
    DateTime LoggedAtUtc);
