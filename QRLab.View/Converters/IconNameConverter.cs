using System.Globalization;
using Avalonia.Data.Converters;
using QRLab.Core.Models.Generation;

namespace QRLab.View.Converters;

public sealed class IconNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        "01" => "gen", "02" => "brush", "03" => "calc", "04" => "diff",
        CodeKind.QrCode => "qrcode", CodeKind.Barcode => "barcode",
        _ => "selection"
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
