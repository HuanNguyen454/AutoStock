using ASM.Application.Contracts;

namespace ASM.Application.Interfaces;

public interface ICurrentUserService
{
    Guid UserId { get; }
    Guid TenantId { get; }
    string UserName { get; }
    string Role { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string role);
}

public interface IAuthService
{
    Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<TokenResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken);
    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken);
    Task<CurrentUserDto> GetCurrentUserAsync(CancellationToken cancellationToken);
}

public interface IUserService
{
    Task<IReadOnlyCollection<UserDto>> GetUsersAsync(CancellationToken cancellationToken);
    Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken);
    Task ToggleUserStatusAsync(Guid userId, CancellationToken cancellationToken);
}

public interface IWarehouseService
{
    Task<IReadOnlyCollection<WarehouseDto>> GetWarehousesAsync(CancellationToken cancellationToken);
    Task<WarehouseDto> CreateWarehouseAsync(CreateWarehouseRequest request, CancellationToken cancellationToken);
    Task<AreaDto> AddAreaAsync(CreateAreaRequest request, CancellationToken cancellationToken);
    Task<RackDto> AddRackAsync(CreateRackRequest request, CancellationToken cancellationToken);
    Task<SlotDto> AddSlotAsync(CreateSlotRequest request, CancellationToken cancellationToken);
    Task<WarehouseLayoutDto?> GetLayoutAsync(Guid warehouseId, CancellationToken cancellationToken);
}

public interface ICatalogService
{
    Task<IReadOnlyCollection<ProductCategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken);
    Task<ProductCategoryDto> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken cancellationToken);
    Task<ProductCategoryDto> UpdateCategoryAsync(UpdateCategoryRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ProductDto>> GetProductsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ProductDto>> GetProductsAsync(Guid? categoryId, string? keyword, CancellationToken cancellationToken);
    Task<ProductDto> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<PalletDto>> GetPalletsAsync(CancellationToken cancellationToken);
    Task<PalletDto> CreatePalletAsync(CreatePalletRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProductLocationSearchResultDto>> SearchProductLocationsAsync(
        string keyword,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductLocationSearchResultDto>> SearchProductLocationsAsync(
        string? keyword,
        Guid? categoryId,
        CancellationToken cancellationToken = default);
}

public interface IQrService
{
    Task<QrCodeDto> GenerateAsync(CreateQrRequest request, CancellationToken cancellationToken);
    Task<QrCodeDto?> GetAsync(Guid qrId, CancellationToken cancellationToken);
    Task<byte[]> RenderPngAsync(Guid qrId, CancellationToken cancellationToken);
}

public interface IOrderService
{
    Task<IReadOnlyCollection<InboundOrderDto>> GetInboundOrdersAsync(CancellationToken cancellationToken);
    Task<InboundOrderDto?> GetInboundOrderAsync(Guid orderId, CancellationToken cancellationToken);
    Task<InboundOrderDto> CreateInboundOrderAsync(CreateInboundOrderRequest request, CancellationToken cancellationToken);
    Task CompleteInboundOrderAsync(Guid orderId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<OutboundOrderDto>> GetOutboundOrdersAsync(CancellationToken cancellationToken);
    Task<OutboundOrderDto?> GetOutboundOrderAsync(Guid orderId, CancellationToken cancellationToken);
    Task<OutboundOrderDto> CreateOutboundOrderAsync(CreateOutboundOrderRequest request, CancellationToken cancellationToken);
    Task CompleteOutboundOrderAsync(Guid orderId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<TaskDto>> GetMyTasksAsync(CancellationToken cancellationToken);
    Task<TaskDto?> GetTaskAsync(Guid taskId, CancellationToken cancellationToken);
    Task<ScanVerifyResultDto> VerifyScanAsync(ScanVerifyRequest request, CancellationToken cancellationToken);
    Task<TaskStepVerifyResultDto> VerifyTaskStepAsync(TaskStepVerifyRequest request, CancellationToken cancellationToken);
}

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken);
}

public interface IAdminService
{
    Task<AdminDashboardSummaryDto> GetDashboardAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<OwnerUsageSummaryDto>> GetOwnersAsync(CancellationToken cancellationToken);
    Task<OwnerUsageDetailDto?> GetOwnerDetailsAsync(Guid ownerUserId, CancellationToken cancellationToken);
    Task<OwnerEditDto?> GetOwnerEditAsync(Guid ownerUserId, CancellationToken cancellationToken);
    Task UpdateOwnerAsync(UpdateOwnerProfileRequest request, CancellationToken cancellationToken);
}
