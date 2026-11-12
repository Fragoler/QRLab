using QRLab.Core.Models.Mutations;
using QRLab.View.ViewModels.Common;

namespace QRLab.View.ViewModels.Mutations;

public static class MutationChoices
{
    public static IReadOnlyList<EnumOption<OcclusionShape>> OcclusionShapes { get; } = Array.AsReadOnly<EnumOption<OcclusionShape>>(
        [
            new(OcclusionShape.Rectangle, "Прямоугольник"), 
            new(OcclusionShape.Ellipse, "Эллипс")
        ]);

    public static IReadOnlyList<EnumOption<MutationKind>> Operations { get; } = Array.AsReadOnly<EnumOption<MutationKind>>(
        [
            new(MutationKind.GaussianBlur, "Размытие Гаусса", "drop"),
            new(MutationKind.SaltAndPepper, "Соль и Перец", "droplets"), 
            new(MutationKind.Occlusion, "Закрытая область", "layers"),
            new(MutationKind.JpegCompression, "Сжатие JPEG", "reload"),
            new(MutationKind.Contrast, "Контраст", "config")
        ]);
    
    public static FrameLabel Original { get; } = new("Исходник", "Защищён");
    public static string Name(MutationKind kind) => Operations.Single(operation => operation.Id == kind).Name;
    public static string Describe(MutationOptions options) => options switch
    {
        GaussianBlurOptions blur => $"σ = {blur.Sigma:N1} px",
        JpegCompressionOptions jpeg => $"JPEG → PNG • качество {jpeg.Quality}",
        ContrastOptions contrast => $"Снижение {contrast.Reduction:N2} • центр {contrast.Center:N2}",
        OcclusionOptions area => $"{OcclusionShapes.Single(shape => shape.Id == area.Shape).Name} • {area.Width:N0} × {area.Height:N0}% • {area.Angle:N0}° • {area.Color}",
        SaltAndPepperOptions noise => $"{noise.DensityPercent:N1}% • белых {noise.SaltPercent:N0}% • seed {noise.Seed}",
        _ => throw new ArgumentOutOfRangeException(nameof(options))
    };
}

public sealed record FrameLabel(string Name, string Description);
