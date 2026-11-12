using CommunityToolkit.Mvvm.ComponentModel;
using QRLab.Core.Models.Mutations;
using QRLab.View.ViewModels.Common;

namespace QRLab.View.ViewModels.Mutations;

public sealed partial class OcclusionSettingsViewModel(OcclusionOptions options) : MutationSettingsViewModel
{
    [ObservableProperty] private OcclusionShape _shape = options.Shape;
    [ObservableProperty] private string _color = options.Color;
    [ObservableProperty] private double _centerX = options.CenterX;
    [ObservableProperty] private double _centerY = options.CenterY;
    [ObservableProperty] private double _width = options.Width;
    [ObservableProperty] private double _height = options.Height;
    [ObservableProperty] private double _angle = options.Angle;
    
    public IReadOnlyList<EnumOption<OcclusionShape>> OcclusionShapes => MutationChoices.OcclusionShapes;
    public bool IsUpdatingGeometry { get; private set; }
    public void SetGeometry(double centerX, double centerY, double width, double height)
    {
        IsUpdatingGeometry = true;
        try { CenterX = centerX; CenterY = centerY; Width = width; Height = height; }
        finally { IsUpdatingGeometry = false; }

        OnPropertyChanged(string.Empty);
    }
    public override MutationOptions ToOptions() => new OcclusionOptions
    {
        Shape = Shape, Color = Color, CenterX = CenterX, CenterY = CenterY,
        Width = Width, Height = Height, Angle = Angle
    };
}
