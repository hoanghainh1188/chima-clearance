using B2.Shared.Contracts.MasterData;

namespace B2.Shared.Contracts.Declarations;

public sealed record DeclarationDto(
    Guid Id,
    string DeclarationNumber,
    ProcessorDto? Processor,
    string CompanyName,
    string GoodsName,
    YardDto? Yard,
    string Status,
    Guid EnteredByUserId,
    DateTimeOffset EnteredAt,
    DateOnly BusinessDate,
    Guid? KvgsConfirmedByUserId,
    DateTimeOffset? KvgsConfirmedAt,
    Guid? CancelledByUserId,
    DateTimeOffset? CancelledAt,
    string? CancellationReason);
