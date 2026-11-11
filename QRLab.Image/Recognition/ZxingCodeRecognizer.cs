using System.Diagnostics;
using QRLab.Core.Interfaces.Recognition;
using QRLab.Core.Models.Recognition;
using QRLab.Core.Models.Samples;
using QRLab.Core.Models.Images;
using QRLab.Core.Models.Generation;
using ZXing;
using ZXing.Common;
using QRLab.Image.Recognition.Inspection;

namespace QRLab.Image.Recognition;

public sealed class ZxingCodeRecognizer : ICodeRecognizer
{
    // Timed-out native/decoder work may finish later; keep the number of workers bounded.
    private readonly SemaphoreSlim _workers = new(2);

    public async Task<IReadOnlyList<RecognitionPreview>> RecognizeAsync(IReadOnlyList<SamplePreview> samples,
        RecognitionOptions options, CancellationToken cancellationToken = default)
    {
        options.Validate();
        var results = new List<RecognitionPreview>(samples.Count);
        foreach (var sample in samples)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var elapsed = Stopwatch.StartNew();
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(options.TimeoutMilliseconds);
            var bitmaps = new PreparedBitmaps { Original = sample.OriginalBitmap };
            try
            {
                await _workers.WaitAsync(deadline.Token);
                var work = Task.Run(() =>
                {
                    try { return Decode(sample, options, bitmaps); }
                    finally { _workers.Release(); }
                }, CancellationToken.None);
                
                var decoded = await work.WaitAsync(deadline.Token);
                var status = decoded.Error is not null || decoded.Content is null 
                    ? (bitmaps.Final is null ? Status.NotFound : Status.Failed)
                    : decoded.Content == sample.Content ? Status.Success : Status.Mismatch;
                results.Add(new(sample.Number, status, elapsed.Elapsed.TotalMilliseconds, decoded.Content, decoded.Error)
                {
                    OriginalBitmap = bitmaps.Original, FinalBitmap = bitmaps.Final,
                    CorrectedCodewords = bitmaps.CorrectedCodewords < 0 ? null : bitmaps.CorrectedCodewords,
                    ErrorCorrectionLevel = bitmaps.ErrorCorrectionLevel,
                    Inspection = bitmaps.Inspection
                });
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                results.Add(new(sample.Number, Status.Timeout, elapsed.Elapsed.TotalMilliseconds, Error: "Превышен тайм-аут декодирования.")
                { OriginalBitmap = bitmaps.Original, FinalBitmap = bitmaps.Final });
            }
        }
        return results;
    }

    private sealed class PreparedBitmaps
    {
        public volatile BinaryImage? Original;
        public volatile BinaryImage? Final;
        public int CorrectedCodewords = -1;
        public string? ErrorCorrectionLevel;
        public CodeInspection? Inspection;
    }

    private static (string? Content, string? Error) Decode(SamplePreview sample, RecognitionOptions options, PreparedBitmaps bitmaps)
    {
        try
        {
            var source = sample.Frames.LastOrDefault()?.Image ?? sample.Image;

            var reader = new BarcodeReaderGeneric(sample.Settings.Kind == CodeKind.QrCode
                ? new QrModuleReader(matrix => bitmaps.Final = matrix) 
                : null, null, null)
            {
                AutoRotate = options.Enhanced,
                Options = new DecodingOptions
                {
                    TryHarder = options.Enhanced,
                    TryInverted = options.Enhanced,
                    PossibleFormats = [BarcodeFormat.QR_CODE, BarcodeFormat.EAN_13, BarcodeFormat.CODE_128, BarcodeFormat.PDF_417]
                }
            };
            
            var luminance = BitmapReader.ReadLuminance(source);
            var result = reader.Decode(luminance);
            if (sample.OriginalBitmap is not null)
            {
                bitmaps.Inspection = CodeInspectors.For(sample.Settings).Inspect(sample, new HybridBinarizer(luminance).BlackMatrix, result, bitmaps.Final);
                bitmaps.Final ??= bitmaps.Inspection.Observed;
            }
            
            if (result?.ResultMetadata is not { } metadata)
                return result is null
                    ? (null, bitmaps.Final is null
                        ? "Код не найден."
                        : "Матрица модулей выделена, но содержимое кода не удалось декодировать.")
                    : (result.Text, null);
            
            if (metadata.TryGetValue(ResultMetadataType.ERRORS_CORRECTED, out var corrected) && corrected is int count)
                bitmaps.CorrectedCodewords = count;
            if (metadata.TryGetValue(ResultMetadataType.ERROR_CORRECTION_LEVEL, out var level))
                bitmaps.ErrorCorrectionLevel = level as string;
            
            return result is null 
                ? (null, bitmaps.Final is null 
                    ? "Код не найден."
                    : "Матрица модулей выделена, но содержимое кода не удалось декодировать.") 
                : (result.Text, null);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return (null, exception.Message);
        }
    }
}
