using CommunityToolkit.Mvvm.ComponentModel;

namespace B2.Desktop.ViewModels;

/// <summary>Flattened item for ledger <see cref="CollectionView"/> (data row or daily header).
/// </summary>
public sealed partial class LedgerUiItem : ObservableObject
{
    private LedgerUiItem(
        bool isDateHeader,
        string? headerText,
        LedgerRowViewModel? row,
        DateOnly? headerDate,
        string? officerName)
    {
        IsDateHeader = isDateHeader;
        HeaderText = headerText;
        Row = row;
        HeaderDate = headerDate;
        _officerName = officerName;
    }

    public bool IsDateHeader { get; }

    public bool IsDataRow => !IsDateHeader;

    public string? HeaderText { get; }

    public LedgerRowViewModel? Row { get; }

    public DateOnly? HeaderDate { get; }

    [ObservableProperty]
    private string? _officerName;

    public static LedgerUiItem ForHeader(string headerText, DateOnly headerDate, string? officerName) =>
        new(true, headerText, null, headerDate, officerName);

    public static LedgerUiItem ForRow(LedgerRowViewModel row) => new(false, null, row, null, null);
}
