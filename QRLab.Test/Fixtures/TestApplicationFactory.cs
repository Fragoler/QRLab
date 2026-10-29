using QRLab.Image.Generation;
using QRLab.Image.Mutations;
using QRLab.Image.Recognition;
using QRLab.Image.Recognition.Reports;
using QRLab.Image.Storage;
using QRLab.View.ViewModels;
using QRLab.View.ViewModels.Workspace;

namespace QRLab.Test.Fixtures;

internal static class TestApplicationFactory
{
    public static SampleSetStorage CreateStorage() => new();
    public static MutationProcessor CreateMutations() => new([new GaussianBlurMutation(), new SaltAndPepperMutation(), new PerspectiveMutation(), new OcclusionMutation()]);
    public static MainWindowViewModel CreateMainWindow() => new(new CodeGenerator(), CreateStorage(), CreateMutations(), new ZxingCodeRecognizer(), new CsvRecognitionReportExporter());
    public static WorkspaceViewModel CreateWorkspace() => new(new CodeGenerator(), CreateStorage(), CreateMutations(), new ZxingCodeRecognizer());
}
