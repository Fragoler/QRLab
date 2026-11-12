using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRLab.Core.Models.Mutations;

namespace QRLab.View.ViewModels.Mutations;

public sealed partial class MarkerSettingsViewModel(MarkerOptions options) : MutationSettingsViewModel
{
    [ObservableProperty] private string _color = options.Color;
    [ObservableProperty] private double _thickness = options.Thickness;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StrokeCount))]
    private IReadOnlyList<MarkerStroke> _strokes = options.Strokes;
    
    public int StrokeCount => Strokes.Count;
    public override MutationOptions ToOptions() => new MarkerOptions { Color = Color, Thickness = Thickness, Strokes = Strokes };
    public void AddStroke(IEnumerable<MarkerPoint> points) =>
        Strokes = Array.AsReadOnly(Strokes.Append(new MarkerStroke(Array.AsReadOnly(points.ToArray()))).ToArray());
    
    [RelayCommand] private void Undo() => Strokes = [.. Strokes.SkipLast(1)];
    [RelayCommand] private void Clear() => Strokes = [];
}
