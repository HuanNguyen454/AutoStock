namespace ASM.WebPortal.Services;

public sealed class SessionExpiredException(string message = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.")
    : InvalidOperationException(message);
