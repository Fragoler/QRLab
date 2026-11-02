using QRLab.Core.Models.Recognition;
using QRLab.Core.Models.Samples;

namespace QRLab.Core.Interfaces.Recognition;

public interface ICodeRecognizer
{
    Task<IReadOnlyList<RecognitionPreview>> RecognizeAsync(IReadOnlyList<SamplePreview> samples, RecognitionOptions options, CancellationToken cancellationToken = default);
}