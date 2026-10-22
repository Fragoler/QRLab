// QRLab.Image/Generation/QRErrorCorrectionLevel.cs
namespace QRLab.Image.Generation;

/// <summary>
/// Уровни коррекции ошибок QR-кода. Собственный кроссплатформенный тип —
/// не зависит от System.Drawing и сторонних пакетов, поэтому проект
/// одинаково собирается на Windows, Linux и macOS.
/// </summary>
public enum QRErrorCorrectionLevel
{
    /// <summary>Low — восстанавливается до ~7% повреждённых кодовых слов.</summary>
    L,

    /// <summary>Medium — восстанавливается до ~15% повреждённых кодовых слов.</summary>
    M,

    /// <summary>Quartile — восстанавливается до ~25% повреждённых кодовых слов.</summary>
    Q,

    /// <summary>High — восстанавливается до ~30% повреждённых кодовых слов.</summary>
    H
}
