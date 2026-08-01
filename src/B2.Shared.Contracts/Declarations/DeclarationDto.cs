using B2.Shared.Contracts.MasterData;

namespace B2.Shared.Contracts.Declarations;

public sealed record DeclarationDto(
    Guid Id,
    /// <summary>The primary number (ordinal 0 of <see cref="DeclarationNumbers"/>).</summary>
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
    string? CancellationReason,
    /// <summary>Every number on cột C of this row, ordered; always contains at least one.</summary>
    IReadOnlyList<string> DeclarationNumbers);
