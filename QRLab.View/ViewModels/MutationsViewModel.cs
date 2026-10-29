using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRLab.Core.Models.Images;
using QRLab.Core.Models.Mutations;
using QRLab.View.Presentation;
using QRLab.View.Presentation.Mutations;
using QRLab.View.ViewModels.Mutations;

namespace QRLab.View.ViewModels;

public sealed partial class MutationsViewModel : ScreenViewModel
{
    private bool _loading;
    private readonly Stack<OperationViewModel> _undone = new();
    public Workspace.WorkspaceViewModel Workspace { get; }
    public IReadOnlyList<EnumOption<ImageInterpolation>> Interpolations => MutationChoices.Interpolations;
    public IReadOnlyList<EnumOption<OcclusionShape>> OcclusionShapes => MutationChoices.OcclusionShapes;
    [ObservableProperty] private OcclusionShape _areaShape = OcclusionShape.Rectangle;
    [ObservableProperty] private string _areaColor = "#808080";
    [ObservableProperty] private double _areaCenterX = 50;
    [ObservableProperty] private double _areaCenterY = 50;
    [ObservableProperty] private double _areaWidth = 25;
    [ObservableProperty] private double _areaHeight = 25;
    [ObservableProperty] private double _areaAngle;
    [ObservableProperty] private double _angleX = MutationDefaults.AngleX;
    [ObservableProperty] private double _angleY = MutationDefaults.AngleY;
    [ObservableProperty] private double _distance = MutationDefaults.Distance;
    [ObservableProperty] private double _noiseDensity = MutationDefaults.NoisePercent;
    [ObservableProperty] private double _saltPercent = MutationDefaults.SaltPercent;
    [ObservableProperty] private double _blur = MutationDefaults.Sigma;
    [ObservableProperty] private decimal? _seed = MutationDefaults.Seed;
    [ObservableProperty] private ImageInterpolation _interpolation = ImageInterpolation.Bicubic;
    [ObservableProperty] private OperationViewModel? _selectedOperation;
    [ObservableProperty] private bool _isBusy;
    public ObservableCollection<OperationViewModel> Operations => Workspace.SelectedSample?.Operations ?? [];
    public IReadOnlyList<OperationViewModel> Frames => Workspace.SelectedSample is { } sample
        ? new[] { sample.OriginalFrame }.Concat(Operations).ToArray() : [];
    public OperationViewModel? SelectedFrame
    {
        get => SelectedOperation ?? Workspace.SelectedSample?.OriginalFrame;
        set => SelectedOperation = value?.Options is null ? null : value;
    }
    public RasterImage? PreviewImage => SelectedOperation?.Image ?? Workspace.SelectedSample?.OriginalImage;
    public IReadOnlyList<EnumOption<MutationKind>> AvailableMutations => MutationChoices.Operations;
    public bool IsSaltAndPepper => SelectedOperation?.Options is SaltAndPepperOptions;
    public bool IsBlur => SelectedOperation?.Options is GaussianBlurOptions;
    public bool IsPerspective => SelectedOperation?.Options is PerspectiveMutationOptions;
    public bool IsOcclusion => SelectedOperation?.Options is OcclusionOptions;
    public bool CanUndo => Operations.Count > 0;
    public bool CanRedo => _undone.Count > 0;

    public MutationsViewModel(Workspace.WorkspaceViewModel workspace) : base("02", "Мутации", "Редактор искажений / цепочка операций")
    {
        Workspace = workspace;
        _selectedOperation = Operations.LastOrDefault();
        Workspace.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(Workspace.SelectedSample)) return;
            _undone.Clear();
            Refresh();
            SelectedOperation = Operations.LastOrDefault();
            OnPropertyChanged(nameof(SelectedFrame));
        };
    }

    partial void OnAngleXChanged(double value) => UpdatePreview();
    partial void OnAreaShapeChanged(OcclusionShape value) => UpdatePreview();
    partial void OnAreaColorChanged(string value) => UpdatePreview();
    partial void OnAreaCenterXChanged(double value) => UpdatePreview();
    partial void OnAreaCenterYChanged(double value) => UpdatePreview();
    partial void OnAreaWidthChanged(double value) => UpdatePreview();
    partial void OnAreaHeightChanged(double value) => UpdatePreview();
    partial void OnAreaAngleChanged(double value) => UpdatePreview();
    partial void OnAngleYChanged(double value) => UpdatePreview();
    partial void OnDistanceChanged(double value) => UpdatePreview();
    partial void OnNoiseDensityChanged(double value) => UpdatePreview();
    partial void OnSaltPercentChanged(double value) => UpdatePreview();
    partial void OnSeedChanged(decimal? value) => UpdatePreview();
    partial void OnBlurChanged(double value) => UpdatePreview();
    partial void OnInterpolationChanged(ImageInterpolation value) => UpdatePreview();

    private MutationOptions CurrentOptions(MutationKind kind) => kind switch
    {
        MutationKind.GaussianBlur => new GaussianBlurOptions { Sigma = Blur },
        MutationKind.Occlusion => new OcclusionOptions
        {
            Shape = AreaShape, Color = AreaColor, CenterX = AreaCenterX, CenterY = AreaCenterY,
            Width = AreaWidth, Height = AreaHeight, Angle = AreaAngle
        },
        MutationKind.SaltAndPepper => new SaltAndPepperOptions
        {
            DensityPercent = NoiseDensity, SaltPercent = SaltPercent, Seed = checked((int)(Seed ?? MutationDefaults.Seed))
        },
        MutationKind.Perspective => new PerspectiveMutationOptions
        {
            AngleX = AngleX, AngleY = AngleY, Distance = Distance, Interpolation = Interpolation
        },
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private void UpdatePreview()
    {
        if (_loading || SelectedOperation?.Options is not { } options) return;
        var index = Operations.IndexOf(SelectedOperation);
        if (index < 0 || Workspace.SelectedSample is not { } sample) return;
        var chain = Operations.Select(o => o.Options!).ToArray();

        try
        {
            chain[index] = CurrentOptions(options.Kind);
            var frames = Workspace.Mutate(sample.OriginalImage, chain);
            Workspace.Error = "";
            _loading = true;
            try
            {
                ReplaceOperations(sample, frames);
                _undone.Clear();
                Refresh();
                SelectedOperation = sample.Operations[index];
                OnPropertyChanged(nameof(SelectedFrame));
                OnPropertyChanged(nameof(PreviewImage));
            }
            finally { _loading = false; }
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException) { Workspace.ShowError(exception.Message); }
    }

    partial void OnSelectedOperationChanged(OperationViewModel? value)
    {
        if (_loading) return;
        _loading = true;
        switch (value?.Options)
        {
            case OcclusionOptions area:
                AreaShape = area.Shape; AreaColor = area.Color; AreaCenterX = area.CenterX; AreaCenterY = area.CenterY;
                AreaWidth = area.Width; AreaHeight = area.Height; AreaAngle = area.Angle; break;
            case GaussianBlurOptions blur: Blur = blur.Sigma; break;
            case SaltAndPepperOptions noise:
                NoiseDensity = noise.DensityPercent; SaltPercent = noise.SaltPercent; Seed = noise.Seed; break;
            case PerspectiveMutationOptions perspective:
                AngleX = perspective.AngleX; AngleY = perspective.AngleY; Distance = perspective.Distance; Interpolation = perspective.Interpolation; break;
        }
        _loading = false;
        foreach (var name in new[] { nameof(SelectedFrame), nameof(PreviewImage), nameof(IsBlur), nameof(IsSaltAndPepper), nameof(IsPerspective), nameof(IsOcclusion) })
            OnPropertyChanged(name);
    }



    [RelayCommand]
    private void Add(MutationKind kind)
    {
        if (Workspace.SelectedSample is not { } sample) return;
        try
        {
            var options = CurrentOptions(kind);
            var frame = Workspace.Mutate(sample.Image, [options]).Single();
            Operations.Add(new($"{Operations.Count + 1:00}", frame.Options, frame.Image));
            _undone.Clear(); Refresh();
            SelectedOperation = Operations.Last();
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException) { Workspace.ShowError(exception.Message); }
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        _undone.Push(Operations[^1]); Operations.RemoveAt(Operations.Count - 1);
        Refresh(); SelectedOperation = Operations.LastOrDefault();
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        Operations.Add(_undone.Pop()); Refresh(); SelectedOperation = Operations.Last();
    }

    [RelayCommand]
    private void Reset()
    {
        Operations.Clear(); _undone.Clear(); Refresh(); SelectedOperation = null;
    }

    [RelayCommand]
    private void Randomize()
    {
        Seed = Seed >= int.MaxValue ? 0 : (Seed ?? MutationDefaults.Seed) + 1;

    }

    [RelayCommand]
    private async Task Apply()
    {
        var chain = Operations.Select(o => o.Options!).ToArray();
        var targets = Workspace.Samples.Where(s => s != Workspace.SelectedSample).ToArray();
        IsBusy = true;
        try
        {
            var results = await Task.Run(() => targets.Select(sample =>
                (Sample: sample, Frames: Workspace.Mutate(sample.OriginalImage, chain))).ToArray());
            foreach (var result in results) ReplaceOperations(result.Sample, result.Frames);
            Refresh();
            Workspace.Notice = $"Цепочка из {chain.Length} операций назначена {targets.Length + 1} образцам.";
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException) { Workspace.ShowError(exception.Message); }
        finally { IsBusy = false; }
    }

    private static void ReplaceOperations(Samples.SampleViewModel sample, IReadOnlyList<MutationFrame> frames)
    {
        sample.Operations.Clear();
        foreach (var (frame, index) in frames.Select((frame, index) => (frame, index)))
            sample.Operations.Add(new($"{index + 1:00}", frame.Options, frame.Image));
    }

    private void Refresh()
    {
        foreach (var name in new[] { nameof(Operations), nameof(Frames), nameof(SelectedFrame), nameof(PreviewImage), nameof(CanUndo), nameof(CanRedo) })
            OnPropertyChanged(name);
        UndoCommand.NotifyCanExecuteChanged(); RedoCommand.NotifyCanExecuteChanged();
    }
}
