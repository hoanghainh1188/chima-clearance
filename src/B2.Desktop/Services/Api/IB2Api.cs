using B2.Shared.Contracts.Auth;
using B2.Shared.Contracts.Declarations;
using B2.Shared.Contracts.MasterData;
using B2.Shared.Contracts.Users;
using Refit;

namespace B2.Desktop.Services.Api;

public interface IB2Api
{
    [Post("/api/auth/login")]
    Task<LoginResponse> LoginAsync([Body] LoginRequest request, CancellationToken cancellationToken = default);

    [Post("/api/auth/change-password")]
    Task ChangePasswordAsync([Body] ChangePasswordRequest request, CancellationToken cancellationToken = default);

    [Get("/api/users")]
    Task<UserListResponse> ListUsersAsync([Query] int page, [Query] int pageSize, CancellationToken cancellationToken = default);

    [Post("/api/users")]
    Task<UserDto> CreateUserAsync([Body] UserCreateRequest request, CancellationToken cancellationToken = default);

    [Get("/api/master/processors")]
    Task<ProcessorListResponse> ListProcessorsAsync(
        [Query] bool includeInactive,
        [Query] int page,
        [Query] int pageSize,
        CancellationToken cancellationToken = default);

    [Post("/api/master/processors")]
    Task<ProcessorDto> CreateProcessorAsync([Body] ProcessorWriteRequest request, CancellationToken cancellationToken = default);

    [Put("/api/master/processors/{id}")]
    Task<ProcessorDto> UpdateProcessorAsync(
        Guid id,
        [Body] ProcessorWriteRequest request,
        CancellationToken cancellationToken = default);

    [Delete("/api/master/processors/{id}")]
    Task DeleteProcessorAsync(Guid id, CancellationToken cancellationToken = default);

    [Get("/api/master/yards")]
    Task<YardListResponse> ListYardsAsync(
        [Query] bool includeInactive,
        [Query] int page,
        [Query] int pageSize,
        CancellationToken cancellationToken = default);

    [Post("/api/master/yards")]
    Task<YardDto> CreateYardAsync([Body] YardWriteRequest request, CancellationToken cancellationToken = default);

    [Put("/api/master/yards/{id}")]
    Task<YardDto> UpdateYardAsync(Guid id, [Body] YardWriteRequest request, CancellationToken cancellationToken = default);

    [Delete("/api/master/yards/{id}")]
    Task DeleteYardAsync(Guid id, CancellationToken cancellationToken = default);

    [Post("/api/declarations")]
    Task<DeclarationDto> CreateDeclarationAsync(
        [Body] DeclarationCreateRequest request,
        [Header("Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken = default);

    [Post("/api/declarations/{id}/vehicles")]
    Task AddVehicleToDeclarationAsync(
        Guid id,
        [Body] VehicleAddRequest request,
        [Header("Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken = default);

    [Get("/api/vehicles/search")]
    Task<List<VehicleSearchResultDto>> SearchVehiclesAsync(
        [Query] string suffix,
        [Query] bool unexited,
        CancellationToken cancellationToken = default);

    [Post("/api/vehicles/{id}/exit")]
    Task<VehicleExitResponse> MarkVehicleExitAsync(
        Guid id,
        [Header("Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken = default);

    [Get("/api/admin/health")]
    Task<AdminHealthResponse> GetHealthAsync(CancellationToken cancellationToken = default);

    [Get("/api/declarations")]
    Task<PagedDeclarationsResponse> ListDeclarationsAsync(
        [Query] int? page,
        [Query] int? pageSize,
        [Query] string? status,
        [Query] string? since,
        [Query] string? fromDate,
        [Query] string? toDate,
        [Query] string? plate,
        [Query] string? yardIds,
        [Query] bool? includeCancelled,
        CancellationToken cancellationToken = default);

    [Get("/api/declarations/{id}")]
    Task<DeclarationDetailResponse> GetDeclarationDetailAsync(Guid id, CancellationToken cancellationToken = default);

    [Post("/api/declarations/{id}/kvgs-confirm")]
    Task ConfirmKvgsAsync(Guid id, CancellationToken cancellationToken = default);
}
