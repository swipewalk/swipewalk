using System.Globalization;

namespace Swipewalk.Desktop.Controls;

/// <summary>
/// True when the bound value is non-null (and, for a string, non-empty). Used to show a row's app name only
/// when one is known (Pages/AppPickerPage): Android's listing has no name, only the id.
/// </summary>
public sealed class IsNotNullConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not (null or (string { Length: 0 }));

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
