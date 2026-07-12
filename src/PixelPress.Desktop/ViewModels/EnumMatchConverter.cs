using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace PixelPress.Desktop.ViewModels;

/// <summary>
/// Binds a radio button (or any toggle) to one value of an enum: it is
/// checked when the bound property equals the converter parameter, and
/// checking it writes that value back.
///
/// The unchecking direction deliberately returns <see cref="BindingOperations.DoNothing"/>.
/// Selecting a new radio button in a group unchecks the old one, and if that
/// unchecking wrote back, it would clobber the value the new selection just
/// set — the order of the two events is not ours to control. Only the check
/// writes; the uncheck stays silent.
/// </summary>
public sealed class EnumMatchConverter : IValueConverter
{
    public static readonly EnumMatchConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && value.Equals(parameter);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true && parameter is not null
            ? parameter
            : BindingOperations.DoNothing;
}
