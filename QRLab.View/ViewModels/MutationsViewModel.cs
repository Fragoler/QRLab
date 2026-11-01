using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRLab.Core.Models.Images;
using QRLab.Core.Models.Mutations;
using QRLab.Core.Services;
using QRLab.View.ViewModels.Common;
using QRLab.View.ViewModels.Mutations;
using QRLab.View.ViewModels.Samples;

namespace QRLab.View.ViewModels;

public sealed partial class MutationsViewModel : ScreenViewModel, IDisposable
{
    private readonly MutationService _service;
    private readonly Dictionary<MutationKind, MutationSettingsViewModel> _settings = new()
    {
        [MutationKind.GaussianBlur] = new GaussianBlurSettingsViewModel(new()),
        [MutationKind.Perspective] = new PerspectiveSettingsViewModel(new()),
        [MutationKind.SaltAndPepper] = new SaltAndPepperSettingsViewModel(new()),
        [MutationKind.Occlusion] = new OcclusionSettingsViewModel(new())
    };
    private SampleViewModel? _sample;
    private int _selectedIndex = -1;
    private bool _updating;
    private IReadOnlyList<OperationViewModel> _frames = [];
    public Workspace.WorkspaceViewModel Workspace { get; }
    public GaussianBlurSettingsViewModel GaussianBlur => (GaussianBlurSettingsViewModel)_settings[MutationKind.GaussianBlur];
    public PerspectiveSettingsViewModel Perspective => (PerspectiveSettingsViewModel)_settings[MutationKind.Perspective];
    public SaltAndPepperSettingsViewModel Noise => (SaltAndPepperSettingsViewModel)_settings[MutationKind.SaltAndPepper];
    public OcclusionSettingsViewModel Occlusion => (OcclusionSettingsViewModel)_settings[MutationKind.Occlusion];
    [ObservableProperty] private MutationSettingsViewModel? _selectedSettings;
    [ObservableProperty] private bool _isBusy;
    public IReadOnlyList<OperationViewModel> Operations => _sample?.Operations ?? (IReadOnlyList<OperationViewModel>)Array.Empty<OperationViewModel>();
    public IReadOnlyList<OperationViewModel> Frames => _frames;
    public OperationViewModel? SelectedOperation
    {
        get => _selectedIndex >= 0 && _selectedIndex < Operations.Count ? Operations[_selectedIndex] : null;
        set
        {
            if (!_updating) SelectOperation(value is null ? -1 : Operations.ToList().IndexOf(value));
        }
    }
    public OperationViewModel? SelectedFrame
    {
        get => SelectedOperation ?? _sample?.OriginalFrame;
        set => SelectedOperation = value?.Options is null ? null : value;
    }
    public RasterImage? PreviewImage => SelectedFrame?.Image;
    public IReadOnlyList<EnumOption<MutationKind>> AvailableMutations => MutationChoices.Operations;
    public bool CanUndo => _sample?.State.Mutations.CanUndo == true;
    public bool CanRedo => _sample?.State.Mutations.CanRedo == true;

    public MutationsViewModel(Workspace.WorkspaceViewModel workspace, MutationService service)
        : base("02", "Мутации", "Редактор искажений / цепочка операций")
    {
        Workspace = workspace;
        _service = service;
        Workspace.PropertyChanged += WorkspaceChanged;
        SelectSample();
    }
    private void WorkspaceChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(Workspace.SelectedSample)) SelectSample();
    }
    private void SelectSample()
    {
        if (_sample is not null)
        {
            _sample.PropertyChanged -= SampleChanged;
            _sample.State.Mutations.ClearRedo();
        }
        _sample = Workspace.SelectedSample;
        if (_sample is not null) _sample.PropertyChanged += SampleChanged;
        SelectOperation(Operations.Count - 1);
        Refresh();
    }
    private void SampleChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(SampleViewModel.Image)) Refresh();
    }
    private void SelectOperation(int index)
    {
        _selectedIndex = index;
        if (SelectedSettings is not null) SelectedSettings.PropertyChanged -= SettingsChanged;
        SelectedSettings = SelectedOperation?.Options is { } options ? MutationSettingsViewModel.FromOptions(options) : null;
        if (SelectedSettings is not null)
        {
            _settings[SelectedOperation!.Options!.Kind] = SelectedSettings;
            SelectedSettings.PropertyChanged += SettingsChanged;
        }
        OnPropertyChanged(nameof(GaussianBlur));
        OnPropertyChanged(nameof(Perspective));
        OnPropertyChanged(nameof(Noise));
        OnPropertyChanged(nameof(Occlusion));
        NotifySelection();
    }
    private void SettingsChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (_sample is null || SelectedSettings is null || _selectedIndex < 0) return;
        TryExecute(() => _service.Edit(_sample.State, _selectedIndex, SelectedSettings.ToOptions()));
    }
    private void TryExecute(Action action)
    {
        // Ignore transient deselection from two-way list bindings while frames are replaced.
        _updating = true;
        try { action(); Workspace.Error = ""; }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        { Workspace.ShowError(exception.Message); }
        finally { _updating = false; }
    }
    [RelayCommand] private void Add(MutationKind kind)
    {
        if (_sample is null) return;
        TryExecute(() =>
        {
            _service.Add(_sample.State, _settings[kind].ToOptions());
            SelectOperation(Operations.Count - 1);
        });
    }
    [RelayCommand(CanExecute = nameof(CanUndo))] private void Undo()
    {
        TryExecute(() =>
        {
            _sample!.State.Mutations.Undo();
            SelectOperation(Operations.Count - 1);
        });
    }
    [RelayCommand(CanExecute = nameof(CanRedo))] private void Redo()
    {
        TryExecute(() =>
        {
            _sample!.State.Mutations.Redo();
            SelectOperation(Operations.Count - 1);
        });
    }
    [RelayCommand] private void Reset()
    {
        TryExecute(() =>
        {
            _sample?.State.Mutations.Reset();
            SelectOperation(-1);
        });
    }
    [RelayCommand] private async Task Apply()
    {
        if (_sample is null) return;
        var count = Operations.Count;
        IsBusy = true;
        try
        {
            var applied = await _service.ApplyToOthersAsync(_sample.State);
            Workspace.ShowNotice($"Цепочка из {count} операций назначена {applied + 1} образцам.");
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        { Workspace.ShowError(exception.Message); }
        finally { IsBusy = false; }
    }
    private void NotifySelection()
    {
        OnPropertyChanged(nameof(SelectedOperation));
        OnPropertyChanged(nameof(SelectedFrame));
        OnPropertyChanged(nameof(PreviewImage));
    }
    private void Refresh()
    {
        _frames = _sample is null ? [] : new[] { _sample.OriginalFrame }.Concat(Operations).ToArray();
        OnPropertyChanged(nameof(Operations));
        OnPropertyChanged(nameof(Frames));
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        NotifySelection();
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }
    public void Dispose()
    {
        Workspace.PropertyChanged -= WorkspaceChanged;
        if (_sample is not null) _sample.PropertyChanged -= SampleChanged;
        if (SelectedSettings is not null) SelectedSettings.PropertyChanged -= SettingsChanged;
    }
}
