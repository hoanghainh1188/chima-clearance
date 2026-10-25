using System.ComponentModel;

namespace B2.Desktop.Services;

/// <summary>
/// Which column of one report table is sorted, and which way, so its header can show ▲ / ▼
/// (#201). The first click on a column sorts ascending, the next flips it; another column
/// starts ascending again. Bound from XAML through the indexer (<c>ProcessorSort[name]</c>).
/// Pure BCL so the test project can link it.
/// </summary>
public sealed class ReportTableSort : INotifyPropertyChanged
{
    // "Every property changed": compiled indexer bindings listen for "Item[key]" and the
    // reflection path for "Item[]"; an empty name refreshes both.
    private static readonly PropertyChangedEventArgs AllChanged = new(string.Empty);

    private readonly string _ascendingGlyph;
    private readonly string _descendingGlyph;

    public ReportTableSort(string ascendingGlyph, string descendingGlyph)
    {
        _ascendingGlyph = ascendingGlyph;
        _descendingGlyph = descendingGlyph;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string? Column { get; private set; }

    public bool Ascending { get; private set; }

    /// <summary>The glyph for <paramref name="column"/>'s header; empty when it is not the sorted one.</summary>
    public string this[string column] =>
        string.Equals(column, Column, StringComparison.Ordinal)
            ? Ascending ? _ascendingGlyph : _descendingGlyph
            : string.Empty;

    /// <summary>Records a header click and returns the direction to sort in.</summary>
    public bool Toggle(string column)
    {
        Ascending = !string.Equals(column, Column, StringComparison.Ordinal) || !Ascending;
        Column = column;
        PropertyChanged?.Invoke(this, AllChanged);
        return Ascending;
    }

    /// <summary>Clears the indicator, e.g. after the table is reloaded in server order.</summary>
    public void Reset()
    {
        Column = null;
        Ascending = false;
        PropertyChanged?.Invoke(this, AllChanged);
    }
}
