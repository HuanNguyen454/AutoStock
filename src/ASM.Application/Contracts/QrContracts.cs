using ASM.Domain.Enums;

namespace ASM.Application.Contracts;

public record QrCodeDto(
    Guid Id,
    QrTargetType TargetType,
    Guid TargetId,
    string Payload,
    string Label);

public record CreateQrRequest(QrTargetType TargetType, Guid TargetId, string Label);
