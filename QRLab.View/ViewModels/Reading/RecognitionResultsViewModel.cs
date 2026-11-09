using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using QRLab.Core.Models.Recognition;
using QRLab.View.ViewModels.Common;
using QRLab.View.ViewModels.Samples;
using QRLab.View.ViewModels.Workspace;

namespace QRLab.View.ViewModels.Reading;

public sealed partial class RecognitionResultsViewModel : ObservableObject, IDisposable
{
    private readonly WorkspaceViewModel _workspace;
    private readonly HashSet<SampleViewModel> _observed = [];
    public IReadOnlyList<EnumOption<RecognitionFilter>> Filters => RecognitionChoices.Filters;
    [ObservableProperty] private RecognitionFilter _filter = RecognitionFilter.All;
    [ObservableProperty] private string _search = "";
    [ObservableProperty] private SampleViewModel? _selectedResult;

    public IReadOnlyList<SampleViewModel> Items => _workspace.Samples.Where(sample =>
        (Filter switch
        {
            RecognitionFilter.All => true,
            RecognitionFilter.NotRecognized => sample.Result is null,
            RecognitionFilter.NotFound => sample.Result?.Status == Status.NotFound,
            RecognitionFilter.Failed => sample.Result?.Status == Status.Failed,
            RecognitionFilter.Successful => sample.IsSuccess,
            RecognitionFilter.Mismatch => sample.IsMismatch,
            RecognitionFilter.Timeout => sample.Result?.Status == Status.Timeout,
            _ => false
        })
        && (sample.Name.Contains(Search, StringComparison.OrdinalIgnoreCase)
            || sample.Content.Contains(Search, StringComparison.OrdinalIgnoreCase)
            || (sample.Result?.DecodedContent?.Contains(Search, StringComparison.OrdinalIgnoreCase) ?? false))).ToArray();
    public bool NoResults => Items.Count == 0;
    public int Checked => _workspace.Samples.Count(sample => sample.Result is not null);
    public int Success => _workspace.Samples.Count(sample => sample.IsSuccess);
    public int Failed => _workspace.Samples.Count(sample => sample.IsFailure || sample.IsMismatch);
    public int NotRecognized => _workspace.Samples.Count - Checked;
    public int NotFound => _workspace.Samples.Count(sample => sample.Result?.Status == Status.NotFound);
    public int Undecoded => _workspace.Samples.Count(sample => sample.Result?.Status == Status.Failed);
    public int Mismatch => _workspace.Samples.Count(sample => sample.IsMismatch);
    public int CorrectedCodewords => _workspace.Samples.Sum(sample => sample.Result?.CorrectedCodewords ?? 0);
    public int SamplesWithCorrection => _workspace.Samples.Count(sample => sample.Result?.CorrectedCodewords > 0);
    public bool HasCorrectionStatistics => _workspace.Samples.Any(sample => sample.Result?.CorrectedCodewords is not null);
    public int Timeout => _workspace.Samples.Count(sample => sample.Result?.Status == Status.Timeout);
    public string Average => Checked == 0 ? "—" : $"{_workspace.Samples.Where(sample => sample.Result is not null).Average(sample => sample.Result!.Milliseconds):N1} мс";
    public string Rate => Checked == 0 ? "Нет результатов" : $"{100d * Success / Checked:N1}% от проверенных";

    public RecognitionResultsViewModel(WorkspaceViewModel workspace)
    {
        _workspace = workspace;
        _selectedResult = workspace.SelectedSample;
        _workspace.PropertyChanged += WorkspaceChanged;
        ((INotifyCollectionChanged)_workspace.Samples).CollectionChanged += SamplesChanged;
        SynchronizeSamples();
    }
    private void WorkspaceChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(_workspace.SelectedSample)) SelectedResult = _workspace.SelectedSample;
    }
    partial void OnSelectedResultChanged(SampleViewModel? value)
    {
        if (value is not null) _workspace.SelectedSample = value;
    }
    partial void OnFilterChanged(RecognitionFilter value) => Refresh();
    partial void OnSearchChanged(string value) => Refresh();
    private void SamplesChanged(object? sender, NotifyCollectionChangedEventArgs args) => SynchronizeSamples();
    private void SynchronizeSamples()
    {
        foreach (var removed in _observed.Where(sample => !_workspace.Samples.Contains(sample)).ToArray())
        {
            removed.PropertyChanged -= SampleChanged;
            _observed.Remove(removed);
        }
        foreach (var sample in _workspace.Samples)
            if (_observed.Add(sample)) sample.PropertyChanged += SampleChanged;
        Refresh();
    }
    private void SampleChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(SampleViewModel.Result)) Refresh();
    }
    private void Refresh()
    {
        OnPropertyChanged(nameof(Items));
        OnPropertyChanged(nameof(NoResults));
        OnPropertyChanged(nameof(Checked));
        OnPropertyChanged(nameof(Success));
        OnPropertyChanged(nameof(Failed));
        OnPropertyChanged(nameof(NotRecognized));
        OnPropertyChanged(nameof(NotFound));
        OnPropertyChanged(nameof(Undecoded));
        OnPropertyChanged(nameof(Mismatch));
        OnPropertyChanged(nameof(CorrectedCodewords));
        OnPropertyChanged(nameof(SamplesWithCorrection));
        OnPropertyChanged(nameof(HasCorrectionStatistics));
        OnPropertyChanged(nameof(Timeout));
        OnPropertyChanged(nameof(Average));
        OnPropertyChanged(nameof(Rate));
    }
    public void Dispose()
    {
        _workspace.PropertyChanged -= WorkspaceChanged;
        ((INotifyCollectionChanged)_workspace.Samples).CollectionChanged -= SamplesChanged;
        foreach (var sample in _observed) sample.PropertyChanged -= SampleChanged;
        _observed.Clear();
    }
}
