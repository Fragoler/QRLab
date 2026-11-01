using QRLab.Core.Interfaces.Generation;
using QRLab.Core.Interfaces.Mutations;
using QRLab.Core.Interfaces.Recognition;
using QRLab.Core.Interfaces.Storage;
using QRLab.Core.Models.Generation.Qrcode;
using QRLab.Core.Models.Samples;
using QRLab.Core.Services;
using QRLab.View.ViewModels.Generation;
using QRLab.View.ViewModels.Workspace;

namespace QRLab.View.ViewModels;

/// <summary>
/// The composition root shared by the desktop app and its headless tests.
/// </summary>
public static class ApplicationViewModels
{
    public static MainWindowViewModel Create(ICodeGenerator generator, ISampleSetStorage storage,
        IEnumerable<IImageMutation> mutations, ICodeRecognizer recognizer, IRecognitionReportExporter reportExporter)
    {
        var state = new SampleWorkspace();
        var generation = new GenerationService(state, generator);
        var sampleStorage = new SampleStorageService(state, storage);
        var mutationService = new MutationService(state, mutations);
        var recognition = new RecognitionService(state, recognizer);
        generation.Generate(new QrGenerationOptions());
        var workspace = new WorkspaceViewModel(state);
        workspace.ShowNotice($"Подготовлено {workspace.SampleCount} образцов");
        return new(workspace, new GenerationViewModel(workspace, generation, sampleStorage),
            new MutationsViewModel(workspace, mutationService), new ReadingViewModel(workspace, recognition, reportExporter));
    }
}
