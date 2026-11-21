using QRLab.Core.Models.Generation;
using QRLab.Core.Models.Generation.Barcode;
using ZXing;

namespace QRLab.Image.Generation.Encoders;

public sealed class Pdf417Encoder : CodeEncoder
{
    public override bool Supports(GenerationOptions options)
    {
        return options is Pdf417GenerationOptions;
    }

    protected override BarcodeFormat GetFormat(GenerationOptions options) 
        => BarcodeFormat.PDF_417;

    protected override IDictionary<EncodeHintType, object> CreateHints(GenerationOptions options)
    {
        var hints = base.CreateHints(options);
        if (options is not Pdf417GenerationOptions pdf)
            throw new ArgumentException($"Pdf417Encoder doesn't support options type ${options.GetType()}");    
            
        hints[EncodeHintType.CHARACTER_SET] = "UTF-8";
        hints[EncodeHintType.ERROR_CORRECTION] = pdf.CorrectionLevel;
        hints[EncodeHintType.PDF417_DIMENSIONS] = new ZXing.PDF417.Internal.Dimensions(
            pdf.MinimumColumns, pdf.MaximumColumns, pdf.MinimumRows, pdf.MaximumRows);
        hints[EncodeHintType.PDF417_ASPECT_RATIO] = pdf.RowHeight;
        hints[EncodeHintType.PDF417_COMPACTION] = pdf.Compaction switch
        {
            Pdf417Compaction.Auto => ZXing.PDF417.Internal.Compaction.AUTO,
            Pdf417Compaction.Text => ZXing.PDF417.Internal.Compaction.TEXT,
            Pdf417Compaction.Byte => ZXing.PDF417.Internal.Compaction.BYTE,
            Pdf417Compaction.Numeric => ZXing.PDF417.Internal.Compaction.NUMERIC,
            _ => throw new ArgumentOutOfRangeException(nameof(options))
        };
        
        return hints;
    }
}
