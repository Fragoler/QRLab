using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using QRLab.Image.Generation;
using QRLab.Image.Mutations;
using QRLab.Image.Recognition;
using QRLab.Image.Recognition.Reports;
using QRLab.Image.Storage;
using QRLab.View.ViewModels;
using QRLab.View.Views;

namespace QRLab.View;

public sealed class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(new CodeGenerator(), new SampleSetStorage(),
                    new MutationProcessor(
                        [
                            new GaussianBlurMutation(), 
                            new SaltAndPepperMutation(), 
                            new PerspectiveMutation(), 
                            new OcclusionMutation()
                        ]),
                    new ZxingCodeRecognizer(), new CsvRecognitionReportExporter())
            };
        base.OnFrameworkInitializationCompleted();
    }
}
