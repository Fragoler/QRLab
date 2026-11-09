using System.ComponentModel;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRLab.Core.Models.Images;
using QRLab.Core.Models.Generation;
using QRLab.View.ViewModels.Samples;

namespace QRLab.View.ViewModels;

public sealed partial class ComparisonViewModel : ScreenViewModel, IDisposable
{
    public Workspace.WorkspaceViewModel Workspace { get; }
    public ReadingViewModel Reading { get; }
    private SampleViewModel? _observed;
    public BinaryImage? OriginalBitmap => _observed?.State.OriginalBitmap;
    public BinaryImage? FinalBitmap => _observed?.State.FinalBitmap;
    public bool HasBitmaps => OriginalBitmap is not null && FinalBitmap is not null;
    public bool HasOriginalBitmap => OriginalBitmap is not null;
    public bool HasFinalBitmap => FinalBitmap is not null;
    public string OriginalDimensions => OriginalBitmap is { } matrix ? $"{matrix.Width} × {matrix.Height} модулей" : "Нет матрицы энкодера";
    public string FinalDimensions => FinalBitmap is { } matrix ? $"{matrix.Width} × {matrix.Height} модулей" : "Нет матрицы детектора";
    public string FinalPlaceholder => _observed is null ? "Выберите образец"
        : _observed.Preview.Settings.Kind != CodeKind.QrCode ? "Выделение матрицы модулей доступно для QR-кодов"
        : _observed.Result is null ? "Запустите считывание, чтобы выделить модули QR"
        : "Детектор не смог выделить матрицу модулей QR";
    public bool CanCompare => Comparison is not null;
    public BitmapComparison? Comparison { get; private set; }
    public string Summary => Comparison is { } c ? $"Изменено {c.Changed:N0} из {c.Total:N0} модулей ({c.ChangedPercent:N2} %)"
        : HasBitmaps ? "Размеры матриц различаются — сравнение модулей недоступно" : "Нет данных для сравнения";
    [ObservableProperty] private bool _highlightChanges;
    [ObservableProperty] private double _zoom = 1;
    [ObservableProperty] private Vector _panOffset;
    public string ZoomLabel => $"{Zoom:P0}";
    partial void OnZoomChanged(double value) => OnPropertyChanged(nameof(ZoomLabel));

    public ComparisonViewModel(Workspace.WorkspaceViewModel workspace, ReadingViewModel reading)
        : base("04", "Сравнение")
    {
        Workspace = workspace;
        Reading = reading;
        workspace.PropertyChanged += WorkspaceChanged;
        ObserveSample();
    }

    private void WorkspaceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Workspace.SelectedSample)) ObserveSample();
    }

    private void ObserveSample()
    {
        if (_observed is not null) _observed.PropertyChanged -= SampleChanged;
        _observed = Workspace.SelectedSample;
        ResetZoom();
        if (_observed is not null) _observed.PropertyChanged += SampleChanged;
        Refresh();
    }

    private void SampleChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SampleViewModel.Result)) Refresh();
    }

    private void Refresh()
    {
        Comparison = OriginalBitmap is { } original && FinalBitmap is { } final
            && original.Width == final.Width && original.Height == final.Height ? new(original, final) : null;
        foreach (var name in new[] { nameof(OriginalBitmap), nameof(FinalBitmap), nameof(HasBitmaps), nameof(HasOriginalBitmap), nameof(HasFinalBitmap),
                     nameof(OriginalDimensions), nameof(FinalDimensions), nameof(FinalPlaceholder), nameof(CanCompare), nameof(Comparison), nameof(Summary) })
            OnPropertyChanged(name);
    }

    [RelayCommand] private void ResetZoom() { Zoom = 1; PanOffset = default; }
    public void Dispose()
    {
        Workspace.PropertyChanged -= WorkspaceChanged;
        if (_observed is not null) _observed.PropertyChanged -= SampleChanged;
    }
}
