using QRLab.Core.Models.Mutations;

namespace QRLab.View.Presentation.Mutations;

public static class MutationChoices
{
    public static IReadOnlyList<EnumOption<OcclusionShape>> OcclusionShapes { get; } = Array.AsReadOnly<EnumOption<OcclusionShape>>(
        [
            new(OcclusionShape.Rectangle, "Прямоугольник"), 
            new(OcclusionShape.Ellipse, "Эллипс")
        ]);
    public static IReadOnlyList<EnumOption<ImageInterpolation>> Interpolations { get; } = Array.AsReadOnly<EnumOption<ImageInterpolation>>(
        [
            new(ImageInterpolation.Bicubic, "Бикубическая"), 
            new(ImageInterpolation.Bilinear, "Билинейная"),
            new(ImageInterpolation.Nearest, "Ближайший сосед")
        ]);
    public static IReadOnlyList<EnumOption<MutationKind>> Operations { get; } = Array.AsReadOnly<EnumOption<MutationKind>>(
        [
            new(MutationKind.GaussianBlur, "Размытие Гаусса", "drop"),
            new(MutationKind.Perspective, "Перспектива", "selection"),
            new(MutationKind.SaltAndPepper, "Salt & Pepper", "droplets"), 
            new(MutationKind.Occlusion, "Закрытая область", "layers")
        ]);
    
    public static FrameLabel Original { get; } = new("Исходник", "Защищён");
    public static string Describe(MutationOptions options) => options switch
    {
        GaussianBlurOptions blur => $"σ = {blur.Sigma:N1} px",
        OcclusionOptions area => $"{GenerationChoices.Name(OcclusionShapes, area.Shape)} • {area.Width:N0} × {area.Height:N0}% • {area.Angle:N0}° • {area.Color}",
        PerspectiveMutationOptions perspective => $"X: {perspective.AngleX:N0}° • Y: {perspective.AngleY:N0}° • D: {perspective.Distance:N1}",
        SaltAndPepperOptions noise => $"{noise.DensityPercent:N1}% • белых {noise.SaltPercent:N0}% • seed {noise.Seed}",
        _ => throw new ArgumentOutOfRangeException(nameof(options))
    };
}
