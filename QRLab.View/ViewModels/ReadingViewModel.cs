using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRLab.Core.Interfaces.Recognition;
using QRLab.Core.Models.Recognition;
using QRLab.View.Presentation;
using QRLab.View.Presentation.Recognition;

namespace QRLab.View.ViewModels;

public sealed partial class ReadingViewModel : ScreenViewModel
{
    private readonly IRecognitionReportExporter _exporter;
    private readonly HashSet<Samples.SampleViewModel> _observed = [];
    public Workspace.WorkspaceViewModel Workspace { get; }
    public IReadOnlyList<EnumOption<RecognitionFilter>> Filters => RecognitionChoices.Filters;
    public NumericRange TimeoutRange { get; } = new(RecognitionDefaults.MinimumTimeoutMilliseconds, RecognitionDefaults.MaximumTimeoutMilliseconds);
    [ObservableProperty] private RecognitionFilter _filter = RecognitionFilter.All;
    [ObservableProperty] private string _search = "";
    [ObservableProperty] private decimal? _timeout = RecognitionDefaults.TimeoutMilliseconds;
    [ObservableProperty] private bool _enhanced = RecognitionDefaults.Enhanced;
    [ObservableProperty] private bool _isExportOpen;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isExporting;
    [ObservableProperty] private string _exportDestination = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "QRLab", "results.csv");
    [ObservableProperty] private Samples.SampleViewModel? _selectedResult;

    public IReadOnlyList<Samples.SampleViewModel> Results => Workspace.Samples.Where(sample =>
        (Filter == RecognitionFilter.All || Filter == RecognitionFilter.Successful && sample.IsSuccess
            || Filter == RecognitionFilter.Failed && sample.IsFailure)
        && (sample.Name.Contains(Search, StringComparison.OrdinalIgnoreCase)
            || sample.Content.Contains(Search, StringComparison.OrdinalIgnoreCase)
            || (sample.Result?.DecodedContent?.Contains(Search, StringComparison.OrdinalIgnoreCase) ?? false))).ToArray();
    public bool NoResults => Results.Count == 0;
    public int ResultsChecked => Workspace.Samples.Count(sample => sample.Result is not null);
    public int ResultsSuccess => Workspace.Samples.Count(sample => sample.IsSuccess);
    public int ResultsFailed => Workspace.Samples.Count(sample => sample.Result?.Status is Status.Failed or Status.Mismatch);
    public int ResultsTimeout => Workspace.Samples.Count(sample => sample.Result?.Status == Status.Timeout);
    public string Average => ResultsChecked == 0 ? "—" : $"{Workspace.Samples.Where(s => s.Result is not null).Average(s => s.Result!.Milliseconds):N1} мс";
    public string Rate => ResultsChecked == 0 ? "Нет результатов" : $"{100d * ResultsSuccess / ResultsChecked:N1}% от проверенных";

    public ReadingViewModel(Workspace.WorkspaceViewModel workspace, IRecognitionReportExporter exporter)
        : base("03", "Считывание", "Декодирование изображений и экспорт результатов")
    {
        Workspace = workspace;
        _exporter = exporter;
        _selectedResult = workspace.SelectedSample;
        Workspace.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(Workspace.SelectedSample)) SelectedResult = Workspace.SelectedSample;
        };
        Workspace.Samples.CollectionChanged += SamplesChanged;
        SynchronizeSamples();
    }

    partial void OnSelectedResultChanged(Samples.SampleViewModel? value)
    {
        if (value is not null) Workspace.SelectedSample = value;
    }
    partial void OnFilterChanged(RecognitionFilter value) => Refresh();
    partial void OnSearchChanged(string value) => Refresh();
    private void SamplesChanged(object? sender, NotifyCollectionChangedEventArgs args) => SynchronizeSamples();
    private void SynchronizeSamples()
    {
        foreach (var removed in _observed.Where(sample => !Workspace.Samples.Contains(sample)).ToArray())
        {
            removed.PropertyChanged -= SampleChanged;
            _observed.Remove(removed);
        }
        foreach (var sample in Workspace.Samples)
            if (_observed.Add(sample)) sample.PropertyChanged += SampleChanged;
        Refresh();
    }
    private void SampleChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(Samples.SampleViewModel.Result)) Refresh();
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task Run(CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            if (Timeout is null || Timeout != decimal.Truncate(Timeout.Value))
                throw new ArgumentException("Укажите целый тайм-аут в миллисекундах.");
            await Workspace.RecognizeAsync(new(checked((int)Timeout.Value), Enhanced), cancellationToken);
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        { Workspace.ShowError(exception.Message); }
        finally { IsBusy = false; Refresh(); }
    }

    [RelayCommand]
    private async Task ConfirmExport()
    {
        var rows = Workspace.Samples.Select(sample => new RecognitionReportRow(sample.Preview.Number, sample.Name,
            sample.Format, sample.Content, sample.Result?.DecodedContent, sample.Result?.Status,
            sample.Result?.Milliseconds, sample.Chain, sample.Result?.Error)).ToArray();
        IsExporting = true;
        try
        {
            await _exporter.ExportAsync(ExportDestination, rows);
            Workspace.Error = "";
            Workspace.Notice = $"CSV сохранён: {ExportDestination}";
            IsExportOpen = false;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        { Workspace.ShowError(exception.Message); }
        finally { IsExporting = false; }
    }

    [RelayCommand] private void Export() => IsExportOpen = !IsExportOpen;
    private void Refresh()
    {
        foreach (var name in new[] { nameof(Results), nameof(NoResults), nameof(ResultsChecked), nameof(ResultsSuccess),
            nameof(ResultsFailed), nameof(ResultsTimeout), nameof(Average), nameof(Rate) }) OnPropertyChanged(name);
    }
}
