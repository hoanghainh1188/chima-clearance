using B2.Application.Abstractions;
using B2.Application.Common;
using B2.Domain.Declarations;
using B2.Shared.Contracts.Declarations;

namespace B2.Application.Declarations.List;

public sealed class ListDeclarationsQueryHandler
{
    private readonly IDeclarationRepository _declarations;
    private readonly IClock _clock;

    public ListDeclarationsQueryHandler(IDeclarationRepository declarations, IClock clock)
    {
        _declarations = declarations;
        _clock = clock;
    }

    public async Task<PagedDeclarationsResponse> HandleAsync(ListDeclarationsQuery query, CancellationToken cancellationToken)
    {
        // Captured before reading so a write racing this query is matched again on the next poll.
        var asOf = _clock.UtcNow;
        var today = VietnamTime.BusinessDateFromUtc(asOf);
        // #164: "AllExited" with no explicit range is the KVGS queue — a declaration opened on an
        // earlier day whose last vehicle left today must still show up, so drop the lower bound.
        var isKvgsQueue = string.Equals(query.StatusFilter, nameof(DeclarationStatus.AllExited), StringComparison.OrdinalIgnoreCase);
        var from = query.FromDate ?? (isKvgsQueue ? DateOnly.MinValue : today);
        var to = query.ToDate ?? today;
        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 50 : query.PageSize;

        var (items, total) = await _declarations.ListDeclarationsAsync(
            new ListDeclarationsRepositoryQuery(
                from,
                to,
                query.StatusFilter,
                query.PlateFilter,
                query.ProcessorIds,
                query.YardIds,
                query.OfficerUserId,
                query.SinceUtc,
                query.IncludeCancelled,
                page,
                pageSize),
            cancellationToken);

        var dtos = items.Select(r => new DeclarationSummaryDto(
                r.Id,
                r.DeclarationNumber,
                r.CompanyName,
                r.GoodsName,
                r.Status,
                r.VehicleCount,
                r.UnexitedVehicleCount,
                r.ProcessorId,
                r.ProcessorName,
                r.YardId,
                r.YardCode,
                r.YardName,
                r.EnteredByUserId,
                r.EnteredByDisplayName,
                r.EnteredAt,
                r.BusinessDate,
                r.KvgsConfirmedByUserId,
                r.KvgsConfirmedAt,
                r.PrimaryPlateNumber,
                r.PrimaryTrailerOrContainer,
                r.DeclarationNumberCount))
            .ToList();

        return new PagedDeclarationsResponse(dtos, total, page, pageSize, asOf);
    }
}
