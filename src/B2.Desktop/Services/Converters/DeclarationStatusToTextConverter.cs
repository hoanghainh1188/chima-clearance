using System.Globalization;
using B2.Desktop.Resources.Strings;

namespace B2.Desktop.Services.Converters;

/// <summary>
/// Maps a raw declaration status ("Open", "AllExited", "KVGSConfirmed",
/// "Cancelled") to its icon + Vietnamese label, matching the ledger rows so the
/// detail drawer reads consistently (icon + colour + text — WCAG 1.4.1).
/// </summary>
public sealed class DeclarationStatusToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = value as string;
        return status switch
        {
            "Open" => "🔴 " + AppResources.Ledger_Status_Open,
            "AllExited" => "🟢 " + AppResources.Ledger_Status_AllExited,
            "KVGSConfirmed" => "🔵 " + AppResources.Ledger_Status_KvgsConfirmed,
            "Cancelled" => "⛔ " + AppResources.Ledger_Status_Cancelled,
            _ => status ?? string.Empty,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
