using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using QRLab.Core.Models.Recognition;

namespace QRLab.View.Converters;

public sealed class RecognitionStatusBrushConverter : IValueConverter
{
    public static IBrush Success { get; } = Brush.Parse("#75D9BE");
    public static IBrush Mismatch { get; } = Brush.Parse("#F1CD70");
    public static IBrush NotFound { get; } = Brush.Parse("#E58A86");
    public static IBrush Undecoded { get; } = Brush.Parse("#fcbd7c");
    public static IBrush NotRecognized { get; } = Brushes.White;
    public static IBrush Timeout { get; } = Brush.Parse("#B94A48");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = value is RecognitionPreview result ? result.Status : value;
        return status switch
        {
            Status.Success => Success,
            Status.Mismatch => Mismatch,
            Status.NotFound => NotFound,
            Status.Failed => Undecoded,
            Status.Timeout => Timeout,
            _ => NotRecognized
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
