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
                DataContext = ApplicationViewModels.Create(new CodeGenerator(), new SampleSetStorage(),
                    [
                        new GaussianBlurMutation(),
                        new SaltAndPepperMutation(),
                        new OcclusionMutation(),
                        new JpegCompressionMutation(),
                        new ContrastMutation(),
                        new MarkerMutation()
                    ],
                    new ZxingCodeRecognizer(), new CsvRecognitionReportExporter())
            };
        base.OnFrameworkInitializationCompleted();
    }
}
