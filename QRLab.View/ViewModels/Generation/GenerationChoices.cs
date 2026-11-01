using QRLab.Core.Models.Generation;
using QRLab.Core.Models.Generation.Barcode;
using QRLab.Core.Models.Generation.Qrcode;
using QRLab.View.ViewModels.Common;

namespace QRLab.View.ViewModels.Generation;

public static class GenerationChoices
{
    public static IReadOnlyList<EnumOption<GenerationMode>> Modes { get; } = Array.AsReadOnly<EnumOption<GenerationMode>>(
    [
        new(GenerationMode.Single, "Один код"),
        new(GenerationMode.Batch, "Пакет")
    ]);

    public static IReadOnlyList<EnumOption<CodeKind>> Formats { get; } = Array.AsReadOnly<EnumOption<CodeKind>>(
    [
        new(CodeKind.QrCode, "QR-код", "qrcode"),
        new(CodeKind.Barcode, "Штрихкод", "barcode")
    ]);

    public static IReadOnlyList<EnumOption<BarcodeType>> BarcodeTypes { get; } = Array.AsReadOnly<EnumOption<BarcodeType>>(
    [
        new(BarcodeType.Ean13, "EAN-13"),
        new(BarcodeType.Code128, "Code 128"),
        new(BarcodeType.Pdf417, "PDF417")
    ]);

    public static IReadOnlyList<EnumOption<QrCorrectionLevel>> Corrections { get; } = Array.AsReadOnly<EnumOption<QrCorrectionLevel>>(
    [
        new(QrCorrectionLevel.Low, "L • 7%"),
        new(QrCorrectionLevel.Medium, "M • 15%"),
        new(QrCorrectionLevel.Quartile, "Q • 25%"),
        new(QrCorrectionLevel.High, "H • 30%")
    ]);

    public static IReadOnlyList<EnumOption<ContentSource>> Sources { get; } = Array.AsReadOnly<EnumOption<ContentSource>>(
    [
        new(ContentSource.NumberedTemplate, "Строка-шаблон с номером"),
        new(ContentSource.Text, "Произвольный текст")
    ]);

    public static string Name<T>(IReadOnlyList<EnumOption<T>> options, T value) where T : struct, Enum =>
        options.Single(option => EqualityComparer<T>.Default.Equals(option.Id, value)).Name;

    public static string CodeName(GenerationOptions options) => options is BarcodeGenerationOptions barcode
        ? Name(BarcodeTypes, barcode.BarcodeType) : Name(Formats, options.Kind);
}
