using QRLab.Core.Services;
using QRLab.Image.Generation;
namespace QRLab.Image.Demo;
/// <summary>Synthetic preview patterns and outcomes, not a QR encoder or decoder.</summary>
public sealed class PrototypeData : IPrototypeData
{
    public IReadOnlyList<LibrarySet> LibrarySets { get; } = [new("Тестовый набор / 12 образцов", 12, "TEST-{num}"), new("Лабораторная работа / 120 образцов", 120, "LAB-2026-{num}")];
    public string Export(IReadOnlyList<SamplePreview> samples, ExportOptions options) =>
        $"Демоэкспорт: {samples.Count} образцов • {options.Format} • {options.Destination}. Файлы не создавались.";
    public IReadOnlyList<SamplePreview> CreateSamples(GenerationOptions options) =>
        Enumerable.Range(options.FirstNumber, options.Count).Select(n => new SamplePreview(n,
            $"{(options.Format == "QR-код" ? "QR" : "BAR")}-{n:0000}",
            options.UseTemplate ? options.Template.Replace("{num}", n.ToString("0000")) : options.Template, options.Format,
            CreateMatrix(n, options.Format == "Штрихкод"),
            [new("01", "Размытие Гаусса", "σ = 1,2 px", 1), new("02", "Кляксы", "3 маски • 6,4% площади", 2), new("03", "Перспектива", "X: 8° • Y: −4°", 3)], options)).ToArray();

    public IReadOnlyList<RecognitionPreview> Recognize(IReadOnlyList<SamplePreview> samples, RecognitionOptions options)
    {
        return samples.Select(s => new RecognitionPreview(s.Number,
            s.Number % 5 != 2 ? Status.Success : s.Number % 3 != 0 ? Status.Failed : Status.Timeout,
            Math.Round(18 + s.Number * 7.13 % 33, 1),
            string.Join(" → ", s.Operations.Select(o => o.Name)))).ToArray();
    }

    public FrameOptions CreateVariant(int seed, FrameOptions current) => current with { AngleX = seed % 31 - 15, AngleY = seed * 3 % 21 - 10, Seed = seed };
    public bool[,] CreateFrame(bool[,] original, FrameOptions options, bool difference = false)
    {
        var rows = original.GetLength(0); var cols = original.GetLength(1);
        var result = (bool[,])original.Clone();
        if (options.Stage >= 1)
        {
            for (var y = 0; y < rows; y++)
            for (var x = 0; x < cols; x++)
            {
                var sx = options.Stage >= 3 ? (int)Math.Round(x - (rows / 2d - y) * options.AngleX / 65) : x;
                var sy = options.Stage >= 3 ? (int)Math.Round(y - (cols / 2d - x) * options.AngleY / 100) : y;
                var ink = options.Stage >= 2 && Math.Pow((x - cols * options.CenterX) / (options.BrushSize / 12), 2) + Math.Pow((y - rows * options.CenterY) / (options.BrushSize / 16), 2) < 1;
                var source = sx >= 0 && sx < cols && sy >= 0 && sy < rows && original[sy,sx];
                result[y,x] = ink && (x * 17 + y * 31) % 100 < options.Opacity ? !options.Erase : source;
                if (options.Stage == 1 && options.Blur > 2 && (x + y) % 7 == 0) result[y,x] = false;
            }
        }
        if (difference)
            for (var y = 0; y < rows; y++)
            for (var x = 0; x < cols; x++) result[y,x] ^= original[y,x];
        return result;
    }
    private static bool[,] CreateMatrix(int seed, bool barcode)
    {
        var random = new Random(seed);
        var cells = new bool[29, barcode ? 65 : 29];
        for (var y = 0; y < cells.GetLength(0); y++)
        for (var x = 0; x < cells.GetLength(1); x++)
            cells[y, x] = barcode ? new Random(seed + x).Next(2) == 0 : random.Next(2) == 0;
        if (!barcode) { Finder(0, 0); Finder(22, 0); Finder(0, 22); }
        return cells;
        void Finder(int left, int top)
        {
            for (var y = -1; y <= 7; y++)
            for (var x = -1; x <= 7; x++)
                if (left + x >= 0 && top + y >= 0 && left + x < 29 && top + y < 29)
                    cells[top + y, left + x] = x >= 0 && y >= 0 && x <= 6 && y <= 6 &&
                        (x == 0 || y == 0 || x == 6 || y == 6 || x is >= 2 and <= 4 && y is >= 2 and <= 4);
        }
    }

    public byte[] GenerateQRCode(string input)
    {
        return QRCodeGenerator.GenerateQRCode(input);
    }

    public byte[] GenerateSampleData()
    {
        return QRCodeGenerator.GenerateSampleData("DemoText1234567890");
    }
}


