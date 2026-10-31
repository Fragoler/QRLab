using QRLab.Core.Models.Recognition;

namespace QRLab.Core.Interfaces.Recognition;

public interface IRecognitionReportExporter
{
    Task ExportAsync(string path, IReadOnlyList<RecognitionReportRow> rows, CancellationToken cancellationToken = default);
}