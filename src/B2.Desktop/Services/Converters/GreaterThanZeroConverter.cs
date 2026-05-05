using System.Globalization;

namespace B2.Desktop.Services.Converters;

/// <summary>Returns true when an int value is greater than zero. Used to gate
/// section visibility in ReportsPage when its collection is non-empty.</summary>
public sealed class GreaterThanZeroConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int n && n > 0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
