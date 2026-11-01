using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRLab.Core.Interfaces.Recognition;
using QRLab.Core.Models.Recognition;
using QRLab.Core.Services;
using QRLab.View.ViewModels.Reading;

namespace QRLab.View.ViewModels;

public sealed partial class ReadingViewModel : ScreenViewModel, IDisposable
{
    private readonly RecognitionService _recognition;
    private readonly IRecognitionReportExporter _exporter;
    public Workspace.WorkspaceViewModel Workspace { get; }
    public RecognitionSettingsViewModel Settings { get; } = new();
    public RecognitionResultsViewModel Results { get; }
    [ObservableProperty] private bool _isExportOpen;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isExporting;
    [ObservableProperty] private string _exportDestination = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "QRLab", "results.csv");

    public ReadingViewModel(Workspace.WorkspaceViewModel workspace, RecognitionService recognition, IRecognitionReportExporter exporter)
        : base("03", "Считывание", "Декодирование изображений и экспорт результатов")
    {
        Workspace = workspace;
        _recognition = recognition;
        _exporter = exporter;
        Results = new(workspace);
    }
    [RelayCommand(IncludeCancelCommand = true)] private async Task Run(CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            var count = await _recognition.RecognizeAsync(Settings.ToOptions(), cancellationToken);
            Workspace.ShowNotice($"Обработано {count} образцов");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        { Workspace.Notice = "Распознавание отменено."; }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        { Workspace.ShowError(exception.Message); }
        finally { IsBusy = false; }
    }
    [RelayCommand] private async Task ConfirmExport()
    {
        var rows = Workspace.Samples.Select(sample => new RecognitionReportRow(sample.Preview.Number, sample.Name,
            sample.Format, sample.Content, sample.Result?.DecodedContent, sample.Result?.Status,
            sample.Result?.Milliseconds, sample.Chain, sample.Result?.Error)).ToArray();
        IsExporting = true;
        try
        {
            await _exporter.ExportAsync(ExportDestination, rows);
            Workspace.ShowNotice($"CSV сохранён: {ExportDestination}");
            IsExportOpen = false;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        { Workspace.ShowError(exception.Message); }
        finally { IsExporting = false; }
    }
    [RelayCommand] private void Export() => IsExportOpen = !IsExportOpen;
    public void Dispose()
    {
        RunCommand.Cancel();
        Results.Dispose();
    }
}
