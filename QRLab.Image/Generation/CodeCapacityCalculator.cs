using System.Text;
using QRLab.Core.Interfaces.Generation;
using QRLab.Core.Models.Generation;
using QRLab.Core.Models.Generation.Qrcode;
using QRLab.Image.Generation.Encoders;
using ZXing;

namespace QRLab.Image.Generation;

/// <summary>Measures logical capacity with the same encoder and hints as generation.</summary>
public sealed class CodeCapacityCalculator : ICodeCapacityCalculator
{
    private readonly CodeEncoder[] _encoders = [new QrCodeEncoder(), new BarcodeEncoder()];

    public int Calculate(GenerationOptions options, string content)
    {
        if (options is Ean13GenerationOptions) return 13;
        (options with { Template = "a", Source = ContentSource.Text, Count = 1, FirstNumber = 1 }).Validate();
        var encoder = _encoders.Single(e => e.Supports(options));
        // In automatic mode the limit is the largest QR version.
        if (options is QrGenerationOptions { Version: 0 } qr) options = qr with { Version = 40 };
        var symbol = Representative(content);
        var low = 0;
        var high = GenerationLimits.MaximumContentLength + 1;
        while (high - low > 1)
        {
            var middle = (low + high) / 2;
            try
            {
                encoder.Encode(string.Concat(Enumerable.Repeat(symbol, middle)), options);
                low = middle;
            }
            catch (Exception exception) when (exception is ArgumentException or WriterException)
            {
                high = middle;
            }
        }
        return low;
    }

    private static string Representative(string content)
    {
        if (content.Length > 0 && content.All(c => c is >= '0' and <= '9')) return "1";
        const string alphanumeric = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ $%*+-./:";
        if (content.Length > 0 && content.All(alphanumeric.Contains)) return "A";
        // For byte mode use the widest Unicode character in the payload.
        var widest = content.EnumerateRunes().OrderByDescending(r => r.Utf8SequenceLength)
            .FirstOrDefault(new Rune('a'));
        return widest.Utf8SequenceLength == 1 ? "a" : widest.ToString();
    }
}
