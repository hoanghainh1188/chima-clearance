using B2.Application.Abstractions;
using B2.Shared.Contracts.Admin;

namespace B2.Application.Admin.Audit;

/// <summary>F-16 — paginated, filtered read over the immutable audit log (Admin only).</summary>
public sealed class ListAuditQueryHandler
{
    private readonly IAuditRepository _audit;

    public ListAuditQueryHandler(IAuditRepository audit) => _audit = audit;

    public async Task<AuditLogListResponse> HandleAsync(
        AuditLogFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var (items, total) = await _audit.ListAsync(filter, page, pageSize, cancellationToken);
        return new AuditLogListResponse(page, pageSize, total, items);
    }
}
