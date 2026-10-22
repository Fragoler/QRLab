// QRLab.Test/QRCodeGeneratorTests.cs
using System;
using System.IO;
using System.Linq;
using QRLab.Image.Demo;
using QRLab.Image.Generation;
using Xunit;

namespace QRLab.Test;

/// <summary>
/// Тесты кроссплатформенного генератора QR-образов.
/// Проверяют, что код не зависит от System.Drawing/QRCoder и выдаёт
/// валидный, детерминированный PNG на Windows, Linux и macOS.
/// </summary>
public sealed class QRCodeGeneratorTests
{
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    [Fact]
    public void GenerateReturnsValidPngWithRequiredChunks()
    {
        var png = QRCodeGenerator.GenerateQRCode("QRLab");

        Assert.NotNull(png);
        Assert.NotEmpty(png);
        // Сигнатура PNG
        Assert.True(png.Length > 8);
        Assert.True(PngSignature.AsSpan().SequenceEqual(png.AsSpan(0, 8)));
        // Обязательные чанки
        Assert.Contains("IHDR", ReadChunkTypes(png));
        Assert.Contains("IDAT", ReadChunkTypes(png));
        Assert.Equal("IEND", ReadChunkTypes(png).Last());
    }

    [Fact]
    public void AllErrorCorrectionLevelsAreSupported()
    {
        foreach (var level in new[] { QRErrorCorrectionLevel.L, QRErrorCorrectionLevel.M,
                                      QRErrorCorrectionLevel.Q, QRErrorCorrectionLevel.H })
        {
            var png = QRCodeGenerator.GenerateQRCode("Тест 123", level);
            Assert.True(png.Length > 0, $"Пустой результат для уровня {level}");
            Assert.True(PngSignature.AsSpan().SequenceEqual(png.AsSpan(0, 8)));
        }
        Assert.Equal(4, QRCodeGenerator.ErrorCorrectionLevels.Count);
    }

    [Fact]
    public void GenerationIsDeterministic()
    {
        var first = QRCodeGenerator.GenerateQRCode("DemoText1234567890", QRErrorCorrectionLevel.Q);
        var second = QRCodeGenerator.GenerateQRCode("DemoText1234567890", QRErrorCorrectionLevel.Q);
        Assert.True(first.AsSpan().SequenceEqual(second));

        var other = QRCodeGenerator.GenerateQRCode("Другой текст", QRErrorCorrectionLevel.Q);
        Assert.False(first.AsSpan().SequenceEqual(other));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void GenerateRejectsEmptyInput(string? input)
    {
        Assert.Throws<ArgumentException>(() => QRCodeGenerator.GenerateQRCode(input!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SelectVersionRejectsNonPositiveLength(int length)
    {
        Assert.Throws<ArgumentException>(() => QRCodeGenerator.SelectVersion(length));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(150, 1)]
    [InlineData(151, 2)]
    [InlineData(1500, 10)]
    public void SelectVersionFollowsFormula(int inputLength, int expectedVersion)
    {
        Assert.Equal(expectedVersion, QRCodeGenerator.SelectVersion(inputLength));
    }

    [Fact]
    public void SelectVersionRejectsTooLongInput()
    {
        Assert.Throws<InvalidOperationException>(() => QRCodeGenerator.SelectVersion(1501));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    [InlineData(-5)]
    public void GenerateSampleDataRejectsInvalidVersion(int version)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => QRCodeGenerator.GenerateSampleData("DemoText1234567890", version));
    }

    [Fact]
    public void GenerateSampleDataProducesPng()
    {
        var png = QRCodeGenerator.GenerateSampleData("DemoText1234567890");
        Assert.True(PngSignature.AsSpan().SequenceEqual(png.AsSpan(0, 8)));
    }

    [Fact]
    public void PrototypeDataExposesWorkingGenerationMethods()
    {
        var data = new PrototypeData();
        var qr = data.GenerateQRCode("QRLab");
        var sample = data.GenerateSampleData();

        Assert.True(PngSignature.AsSpan().SequenceEqual(qr.AsSpan(0, 8)));
        Assert.True(PngSignature.AsSpan().SequenceEqual(sample.AsSpan(0, 8)));
    }

    [Fact]
    public void ImageProjectDoesNotReferenceSystemDrawingOrQrcoder()
    {
        // Кроссплатформенность: в QRLab.Image не должно быть пакетных зависимостей
        var csproj = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "QRLab.Image", "QRLab.Image.csproj");
        if (File.Exists(csproj))
        {
            var content = File.ReadAllText(csproj);
            Assert.DoesNotContain("QRCoder", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("System.Drawing.Common", content, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string[] ReadChunkTypes(byte[] png)
    {
        var types = new System.Collections.Generic.List<string>();
        int offset = 8; // после сигнатуры
        while (offset + 8 <= png.Length)
        {
            int length = (png[offset] << 24) | (png[offset + 1] << 16) | (png[offset + 2] << 8) | png[offset + 3];
            types.Add(System.Text.Encoding.ASCII.GetString(png, offset + 4, 4));
            offset += 12 + length; // длина + тип + данные + CRC
        }
        return types.ToArray();
    }
}
