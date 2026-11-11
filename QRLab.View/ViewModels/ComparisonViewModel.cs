using System.ComponentModel;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRLab.Core.Models.Images;
using QRLab.Core.Models.Generation;
using QRLab.Core.Models.Recognition;
using QRLab.View.ViewModels.Samples;

namespace QRLab.View.ViewModels;

public sealed record DiagnosticValue(string Name, string Value);

public sealed partial class ComparisonViewModel : ScreenViewModel, IDisposable
{
    public Workspace.WorkspaceViewModel Workspace { get; }
    public ReadingViewModel Reading { get; }
    private SampleViewModel? _observed;
    public BinaryImage? OriginalBitmap => _observed?.State.OriginalBitmap;
    public BinaryImage? FinalBitmap => _observed?.State.FinalBitmap;
    public CodeInspection? Inspection => _observed?.Result?.Inspection;
    public CodeLayout? Layout => Inspection?.Layout ?? _observed?.Preview.Layout;
    public bool IsQr => _observed?.Preview.Settings.Kind == CodeKind.QrCode;
    public bool HasInspection => Inspection is not null;
    public IReadOnlyList<DiagnosticValue> DiagnosticValues
    {
        get
        {
            if (Inspection is not { } report) return [];
            var values = new List<DiagnosticValue>
            {
                new("Формат", report.Format),
                new("Основа анализа", report.IsReferenceEstimate ? "Сетка исходника" : "Считанная матрица"),
                new("Повреждено слов / всего", $"{report.DamagedCodewords:N0} / {report.TotalCodewords:N0}"),
                new("Предел коррекции ошибок", report.CorrectionCapacity.ToString("N0")),
                new("Стирания", report.Blocks.Sum(block => block.Erasures).ToString("N0")),
                new("Лимит превышен", (report.Blocks.Count > 0 ? report.Blocks.Any(block => block.ExceedsCapacity) : report.DamagedCodewords > 0) ? "Да" : "Нет")
            };
            if (_observed?.Preview.Settings is QRLab.Core.Models.Generation.Qrcode.QrGenerationOptions qr)
            {
                values.Insert(1, new("Версия QR", ((OriginalBitmap!.Width - 17) / 4).ToString()));
                values.Insert(2, new("Уровень коррекции", qr.Correction switch
                {
                    Core.Models.Generation.Qrcode.QrCorrectionLevel.Low => "L",
                    Core.Models.Generation.Qrcode.QrCorrectionLevel.Medium => "M",
                    Core.Models.Generation.Qrcode.QrCorrectionLevel.Quartile => "Q", 
                    _ => "H"
                }));
            }
            else if (report.Format == "PDF417" && report.Blocks.Count > 0)
                values.Insert(1, new("Уровень коррекции", ((int)Math.Log2(report.Blocks[0].CorrectionWords) - 1).ToString()));
            if (_observed?.Result?.CorrectedCodewords is { } corrected)
                values.Add(new("Исправлено кодовых слов", corrected.ToString("N0")));
            foreach (var block in report.Blocks)
            {
                values.Add(new($"Блок {block.Number} · ошибки / лимит", $"{block.DamagedWords - block.Erasures} / {block.Capacity}"));
                if (block.Erasures > 0) values.Add(new($"Блок {block.Number} · стирания", block.Erasures.ToString()));
            }
            foreach (var region in report.Regions)
                values.Add(new(
                    region.Name == "Кодовые слова" 
                        ? "Модули данных" 
                        : $"{region.Name} · модули", $"{region.Changed:N0} / {region.Total:N0}"));
            return values;
        }
    }
    public bool HasBitmaps => OriginalBitmap is not null && FinalBitmap is not null;
    public bool HasOriginalBitmap => OriginalBitmap is not null;
    public bool HasFinalBitmap => FinalBitmap is not null;
    public string OriginalDimensions => OriginalBitmap is { } matrix ? $"{matrix.Width} × {matrix.Height} модулей" : "Нет матрицы энкодера";
    public string FinalDimensions => FinalBitmap is { } matrix ? $"{matrix.Width} × {matrix.Height} модулей" : "Нет матрицы считывания";
    public string FinalPlaceholder => _observed is null ? "Выберите образец"
        : _observed.Result is null ? "Запустите считывание, чтобы выделить модули кода"
        : "Декодер не смог выделить матрицу";
    public bool CanCompare => Comparison is not null;
    public BitmapComparison? Comparison { get; private set; }
    public string Summary => Comparison is { } c ? $"Изменено {c.Changed:N0} из {c.Total:N0} модулей ({c.ChangedPercent:N2} %)"
        : HasBitmaps ? "Размеры матриц различаются — сравнение модулей недоступно" : "Нет данных для сравнения";
    [ObservableProperty] private bool _highlightChanges;
    [ObservableProperty] private bool _highlightZones;
    [ObservableProperty] private bool _highlightCodewords;
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
                     nameof(OriginalDimensions), nameof(FinalDimensions), nameof(FinalPlaceholder), nameof(CanCompare), nameof(Comparison), nameof(Summary),
                     nameof(Inspection), nameof(Layout), nameof(IsQr), nameof(HasInspection), nameof(DiagnosticValues) })
            OnPropertyChanged(name);
    }

    [RelayCommand] private void ResetZoom() { Zoom = 1; PanOffset = default; }
    public void Dispose()
    {
        Workspace.PropertyChanged -= WorkspaceChanged;
        if (_observed is not null) _observed.PropertyChanged -= SampleChanged;
    }
}
