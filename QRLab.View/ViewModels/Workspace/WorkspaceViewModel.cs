using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRLab.Core.Models.Samples;
using QRLab.View.ViewModels.Samples;

namespace QRLab.View.ViewModels.Workspace;

public sealed partial class WorkspaceViewModel : ObservableObject, IDisposable
{
    private readonly ObservableCollection<SampleViewModel> _samples = [];
    public SampleWorkspace State { get; }
    public ReadOnlyObservableCollection<SampleViewModel> Samples { get; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasError))] private string _error = "";
    [ObservableProperty] private string _notice = "";
    [ObservableProperty] private SampleViewModel? _selectedSample;
    public string SetName => State.Name;
    public bool HasError => !string.IsNullOrEmpty(Error);
    public string Position => $"{SampleNumber ?? 0} / {Samples.Count}";
    public int SampleCount => Samples.Count;
    public decimal SampleNumberMaximum => Math.Max(1, Samples.Count);
    public decimal? SampleNumber
    {
        get => SelectedSample is null ? null : _samples.IndexOf(SelectedSample) + 1;
        set
        {
            if (value is null || value != decimal.Truncate(value.Value) || value < 1 || value > Samples.Count) return;
            SelectedSample = Samples[(int)value.Value - 1];
        }
    }
    public bool CanMovePrevious => SelectedSample is not null && _samples.IndexOf(SelectedSample) > 0;
    public bool CanMoveNext => SelectedSample is not null && _samples.IndexOf(SelectedSample) < Samples.Count - 1;

    public WorkspaceViewModel(SampleWorkspace state)
    {
        State = state;
        Samples = new(_samples);
        State.Changed += StateChanged;
        SynchronizeSamples();
    }

    private void StateChanged(object? sender, EventArgs args) => SynchronizeSamples();
    private void SynchronizeSamples()
    {
        if (!State.Samples.SequenceEqual(Samples.Select(sample => sample.State)))
        {
            foreach (var sample in Samples) sample.Dispose();
            _samples.Clear();
            foreach (var sample in State.Samples) _samples.Add(new(sample));
            SelectedSample = Samples.FirstOrDefault();
            OnPropertyChanged(nameof(SampleCount));
            OnPropertyChanged(nameof(SampleNumberMaximum));
            NotifySelection();
        }
        OnPropertyChanged(nameof(SetName));
    }

    partial void OnSelectedSampleChanged(SampleViewModel? value) => NotifySelection();
    private void NotifySelection()
    {
        OnPropertyChanged(nameof(Position));
        OnPropertyChanged(nameof(SampleNumber));
        OnPropertyChanged(nameof(CanMovePrevious));
        OnPropertyChanged(nameof(CanMoveNext));
        PreviousCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
    }
    public void ShowError(string message) { Error = message; Notice = message; }
    public void ShowNotice(string message) { Error = ""; Notice = message; }
    [RelayCommand(CanExecute = nameof(CanMovePrevious))] private void Previous() => Move(-1);
    [RelayCommand(CanExecute = nameof(CanMoveNext))] private void Next() => Move(1);
    private void Move(int delta)
    {
        if (Samples.Count > 0)
            SelectedSample = Samples[Math.Clamp(_samples.IndexOf(SelectedSample!) + delta, 0, Samples.Count - 1)];
    }
    public void Dispose()
    {
        State.Changed -= StateChanged;
        foreach (var sample in Samples) sample.Dispose();
    }
}
