using QRLab.Core.Interfaces.Mutations;
using QRLab.Core.Models.Samples;
using QRLab.Core.Services;
using QRLab.Image.Generation;
using QRLab.Image.Mutations;
using QRLab.Image.Recognition;
using QRLab.Image.Recognition.Reports;
using QRLab.Image.Storage;
using QRLab.View.ViewModels;

namespace QRLab.Test.Fixtures;

internal static class TestApplicationFactory
{
    public static SampleSetStorage CreateStorage() => new();
    public static IImageMutation[] CreateImageMutations() => [new GaussianBlurMutation(), new SaltAndPepperMutation(), new OcclusionMutation(), new JpegCompressionMutation(), new ContrastMutation(), new MarkerMutation(), new DilationMutation(), new ErosionMutation(), new PrintDefectsMutation()];
    public static MutationService CreateMutations() => new(new SampleWorkspace(), CreateImageMutations());
    public static MainWindowViewModel CreateMainWindow() => ApplicationViewModels.Create(new CodeGenerator(), CreateStorage(), CreateImageMutations(), new ZxingCodeRecognizer(), new CsvRecognitionReportExporter());
}
