using QRLab.Core.Models.Generation;

namespace QRLab.Image.Recognition.Inspection;

/// <summary>
/// Composition point for format implementation
/// </summary>
public sealed class CodeInspectorRegistry(IEnumerable<ICodeInspector> inspectors)
{
    private readonly IReadOnlyList<ICodeInspector> _inspectors = [.. inspectors];

    public ICodeInspector For(GenerationOptions settings) => _inspectors.FirstOrDefault(inspector => inspector.Supports(settings))
                                                             ?? throw new ArgumentException("Анализатор для выбранного типа кода не зарегистрирован.");

    public static CodeInspectorRegistry Default { get; } = new(
    [
        new CategorizedCodeInspector(new QrCodeInspector(new QrStructureProvider()), new InspectionReportBuilder()),
        new CategorizedCodeInspector(new Pdf417Inspector(), new InspectionReportBuilder())
    ]);
}
