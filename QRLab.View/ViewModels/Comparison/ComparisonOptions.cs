using QRLab.Core.Models.Recognition;

namespace QRLab.View.ViewModels.Comparison;

/// <summary>
/// Describes overlays available for a layout, independently of the screen and code format.
/// </summary>
public interface IComparisonOptions
{
    bool Zones { get; }
    string ZonesLabel { get; }
    string ZonesDescription { get; }
    bool Codewords { get; }
    bool Blocks { get; }
    string BlocksLabel { get; }
    string BlocksDescription { get; }
}

public sealed class LayoutComparisonOptions(CodeLayout? layout) : IComparisonOptions
{
    public bool Zones { get; } = layout?.Cells.Any(cell => cell.Role is ModuleRole.Finder or ModuleRole.Guard or ModuleRole.RowIndicator) == true;
    public string ZonesLabel { get; } = "Служебные зоны";
    public string ZonesDescription { get; } = layout?.HasErrorCorrectionWords == true
        ? "Выделяет старт и стоп синим, левый и правый индикатор строки — фиолетовым."
        : "Выделяет служебные зоны QR-кода.";
    public bool Codewords { get; } = layout?.Cells.Any(cell => cell.Codeword >= 0) == true;
    public bool Blocks { get; } = layout is { HasErrorCorrectionWords: false } && layout.Cells.Any(cell => cell.Block >= 0);
    public string BlocksLabel => "Блоки коррекции";
    public string BlocksDescription => "Каждый блок имеет свой цвет с тёмным и светлым оттенками модулей. Отключает подсветку кодовых слов.";
}
