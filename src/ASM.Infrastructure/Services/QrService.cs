using ASM.Application.Contracts;
using ASM.Application.Interfaces;
using ASM.Domain.Entities;
using ASM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using QRCoder;

namespace ASM.Infrastructure.Services;

public class QrService(
    AppDbContext dbContext,
    ICurrentUserService currentUser) : IQrService
{
    public async Task<QrCodeDto> GenerateAsync(CreateQrRequest request, CancellationToken cancellationToken)
    {
        var existing = await dbContext.QrCodes.FirstOrDefaultAsync(
            x => x.TenantId == currentUser.TenantId &&
                 x.TargetType == request.TargetType &&
                 x.TargetId == request.TargetId,
            cancellationToken);

        if (existing is not null)
        {
            return Map(existing);
        }

        var qrCode = new QrCode
        {
            TenantId = currentUser.TenantId,
            TargetType = request.TargetType,
            TargetId = request.TargetId,
            Label = request.Label,
            Payload = $"{currentUser.TenantId:N}|{request.TargetType}|{request.TargetId:N}"
        };

        dbContext.QrCodes.Add(qrCode);
        dbContext.AuditLogs.Add(new AuditLog
        {
            TenantId = currentUser.TenantId,
            PerformedByUserId = currentUser.UserId,
            Action = "GenerateQr",
            EntityName = nameof(QrCode),
            EntityId = qrCode.Id,
            Detail = qrCode.Payload
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(qrCode);
    }

    public async Task<QrCodeDto?> GetAsync(Guid qrId, CancellationToken cancellationToken)
    {
        var qr = await dbContext.QrCodes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == qrId && x.TenantId == currentUser.TenantId, cancellationToken);
        return qr is null ? null : Map(qr);
    }

    public async Task<byte[]> RenderPngAsync(Guid qrId, CancellationToken cancellationToken)
    {
        var qr = await dbContext.QrCodes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == qrId && x.TenantId == currentUser.TenantId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy QR.");

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(qr.Payload, QRCodeGenerator.ECCLevel.Q);
        var pngQrCode = new PngByteQRCode(data);
        return pngQrCode.GetGraphic(20);
    }

    private static QrCodeDto Map(QrCode qr) =>
        new(qr.Id, qr.TargetType, qr.TargetId, qr.Payload, qr.Label);
}
