namespace B2.Domain.Declarations;

/// <summary>
/// One of the 12-digit numbers written into cột C of a parent row. The Excel sheet allows several
/// numbers in the same cell ("có nhiều dãy số thì mỗi dãy số sẽ tách thành từng dòng riêng biệt
/// trong cùng 1 ô"), confirmed with the unit 2026-08-01, so a declaration owns an ordered list.
/// <para>
/// <see cref="IsCancelled"/> mirrors the parent's cancellation. SQLite cannot filter a unique
/// index on another table's column, and INV-1 must stay scoped to declarations that still count
/// (a cancelled number is re-enterable — see FR-022), so the flag is carried here and maintained
/// by <see cref="Declaration.Cancel"/>.
/// </para>
/// </summary>
public sealed class DeclarationNumberLine
{
#pragma warning disable CS8618
    private DeclarationNumberLine()
    {
    }
#pragma warning restore CS8618

    internal DeclarationNumberLine(Guid id, Guid declarationId, string number, int ordinal)
    {
        Id = id;
        DeclarationId = declarationId;
        Number = number;
        Ordinal = ordinal;
        IsCancelled = false;
    }

    public Guid Id { get; private set; }

    public Guid DeclarationId { get; private set; }

    public string Number { get; private set; } = null!;

    /// <summary>0 is the primary number — the one cached on <see cref="Declaration.DeclarationNumber"/>.</summary>
    public int Ordinal { get; private set; }

    public bool IsCancelled { get; private set; }

    internal void SetOrdinal(int ordinal) => Ordinal = ordinal;

    internal void MarkCancelled() => IsCancelled = true;
}
