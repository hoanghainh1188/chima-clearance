using B2.Shared.Contracts.Admin;
using B2.Shared.Contracts.Auth;
using B2.Shared.Contracts.Declarations;
using B2.Shared.Contracts.MasterData;
using B2.Shared.Contracts.Reports;
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

    [Get("/api/vehicles/suggest")]
    Task<VehicleSuggestResponse> SuggestVehiclePairAsync(
        [Query] string plate,
        [Query] string field,
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

    /// <summary>Cột C — a row may carry several numbers and the cell stays editable.</summary>
    [Post("/api/declarations/{id}/numbers")]
    Task<List<string>> AddDeclarationNumberAsync(
        Guid id,
        [Body] AddDeclarationNumberRequest request,
        CancellationToken cancellationToken = default);

    [Delete("/api/declarations/{id}/numbers/{number}")]
    Task<List<string>> RemoveDeclarationNumberAsync(
        Guid id,
        string number,
        CancellationToken cancellationToken = default);

    /// <summary>Ghi chú (cột O) — editable at the exit gate per business rules § 3.</summary>
    [Patch("/api/vehicles/{id}/note")]
    Task UpdateVehicleNoteAsync(
        Guid id,
        [Body] UpdateVehicleNoteRequest request,
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

    [Patch("/api/daily-headers/{date}/officer")]
    Task UpdateOfficerNameAsync(string date, [Body] UpdateOfficerNameRequest request, CancellationToken cancellationToken = default);

    [Post("/api/daily-headers/{date}/recompute")]
    Task RecomputeDailySummaryAsync(string date, [Body] RecomputeDailySummaryRequest request, CancellationToken cancellationToken = default);

    [Get("/api/daily-headers/")]
    Task<List<DailyHeaderDto>> ListDailyHeadersAsync(
        [Query] string from,
        [Query] string to,
        CancellationToken cancellationToken = default);

    [Get("/api/reports/by-processor")]
    Task<List<ProcessorReportItemDto>> GetByProcessorReportAsync(
        [Query] string from,
        [Query] string to,
        [Query] bool includeCancelled = false,
        CancellationToken cancellationToken = default);

    [Get("/api/reports/by-yard")]
    Task<List<YardReportItemDto>> GetByYardReportAsync(
        [Query] string from,
        [Query] string to,
        [Query] bool includeCancelled = false,
        CancellationToken cancellationToken = default);

    [Get("/api/reports/by-hour")]
    Task<List<HourReportItemDto>> GetByHourReportAsync(
        [Query] string date,
        [Query] int? bucketMinutes = null,
        [Query] bool includeCancelled = false,
        CancellationToken cancellationToken = default);

    [Get("/api/reports/by-officer")]
    Task<List<OfficerReportItemDto>> GetByOfficerReportAsync(
        [Query] string from,
        [Query] string to,
        [Query] bool includeCancelled = false,
        CancellationToken cancellationToken = default);

    [Get("/api/reports/pending")]
    Task<List<PendingReportItemDto>> GetPendingReportAsync(
        [Query] DateTimeOffset? asOf = null,
        [Query] bool includeCancelled = false,
        CancellationToken cancellationToken = default);

    /// <summary>F-13 Sổ B2 export — returns the raw .xlsx response (bytes +
    /// Content-Disposition file name); the binary bypasses JSON deserialization.</summary>
    [Get("/api/reports/excel")]
    Task<HttpResponseMessage> ExportSoB2Async(
        [Query] string from,
        [Query] string to,
        CancellationToken cancellationToken = default);

    [Get("/api/admin/audit")]
    Task<AuditLogListResponse> ListAuditAsync(
        [Query] string? entityType,
        [Query] string? action,
        [Query] string? from,
        [Query] string? to,
        [Query] int page,
        [Query] int pageSize,
        CancellationToken cancellationToken = default);

    [Get("/api/admin/health/detail")]
    Task<HealthReportDto> GetHealthDetailAsync(CancellationToken cancellationToken = default);

    [Get("/api/admin/logs")]
    Task<LogListResponse> ListLogsAsync(
        [Query] string? level,
        [Query] int page,
        [Query] int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>F-15 — downloads a fresh encrypted snapshot (raw .bak response).</summary>
    [Post("/api/admin/backup-now")]
    Task<HttpResponseMessage> BackupNowAsync(CancellationToken cancellationToken = default);

    /// <summary>F-15 — uploads a snapshot to restore; the secret is the install password
    /// OR the recovery code. The server stages it and restarts to apply.</summary>
    [Multipart]
    [Post("/api/admin/restore")]
    Task<HttpResponseMessage> RestoreAsync(
        [AliasAs("file")] ByteArrayPart file,
        [AliasAs("password")] string password,
        CancellationToken cancellationToken = default);
}
