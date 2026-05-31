using B2.Application.Admin.Audit;
using B2.Shared.Contracts.Admin;

namespace B2.Application.Abstractions;

/// <summary>
/// Read-only access to the append-only audit log (F-16). There is deliberately no
/// update/delete surface — the log is immutable (FR-003).
/// </summary>
public interface IAuditRepository
{
    Task<(IReadOnlyList<AuditLogItemDto> Items, int Total)> ListAsync(
        AuditLogFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
