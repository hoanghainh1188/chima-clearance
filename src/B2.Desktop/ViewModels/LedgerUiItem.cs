namespace B2.Desktop.ViewModels;

/// <summary>Flattened item for ledger <see cref="CollectionView"/> (data row or daily header).
/// </summary>
public sealed class LedgerUiItem
{
    private LedgerUiItem(bool isDateHeader, string? headerText, LedgerRowViewModel? row)
    {
        IsDateHeader = isDateHeader;
        HeaderText = headerText;
        Row = row;
    }

    public bool IsDateHeader { get; }

    public bool IsDataRow => !IsDateHeader;

    public string? HeaderText { get; }

    public LedgerRowViewModel? Row { get; }

    public static LedgerUiItem ForHeader(string headerText) => new(true, headerText, null);

    public static LedgerUiItem ForRow(LedgerRowViewModel row) => new(false, null, row);
}
