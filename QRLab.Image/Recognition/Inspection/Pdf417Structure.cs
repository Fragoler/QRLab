using QRLab.Core.Models.Images;
using QRLab.Core.Models.Recognition;

namespace QRLab.Image.Recognition.Inspection;

/// <summary>Reads the encoder matrix using PDF417 guards, row indicators and length descriptor.</summary>
internal sealed class Pdf417Structure
{
    private readonly BinaryImage _original;
    private readonly int _rotation;
    public int Width { get; }
    public int Height { get; }
    public int Left { get; private set; }
    public int Top { get; private set; }
    public int SymbolWidth { get; private set; }
    public int SymbolHeight { get; private set; }
    public int Columns { get; private set; }
    public int CorrectionLevel { get; private set; }
    public int CorrectionWords => 1 << (CorrectionLevel + 1);
    public int DataWords => Words.Count - CorrectionWords;
    public IReadOnlyList<int> Words { get; private set; } = [];
    public IReadOnlyList<(int Top, int Bottom)> Rows { get; private set; } = [];

    private Pdf417Structure(BinaryImage image, int rotation)
    {
        _original = image;
        _rotation = rotation;
        Width = rotation % 2 == 0 ? image.Width : image.Height;
        Height = rotation % 2 == 0 ? image.Height : image.Width;
    }

    public static Pdf417Structure Read(BinaryImage original)
    {
        for (var rotation = 0; rotation < 4; rotation++)
        {
            var structure = new Pdf417Structure(original, rotation);
            if (structure.TryRead()) return structure;
        }
        throw new ArgumentException("Не удалось подтвердить структуру исходного PDF417 по индикаторам строк.");
    }

    public (int X, int Y) ToImage(int x, int y) => _rotation switch
    {
        0 => (x, y),
        1 => (y, _original.Height - 1 - x),
        2 => (_original.Width - 1 - x, _original.Height - 1 - y),
        _ => (_original.Width - 1 - y, x)
    };

    public (int X, int Y) ToCanonical(int x, int y) => _rotation switch
    {
        0 => (x, y),
        1 => (_original.Height - 1 - y, x),
        2 => (_original.Width - 1 - x, _original.Height - 1 - y),
        _ => (y, _original.Width - 1 - x)
    };

    public int Pattern(BinaryImage image, int x, int y, int length = 17)
    {
        var pattern = 0;
        for (var bit = 0; bit < length; bit++)
        {
            var point = ToImage(x + bit, y);
            pattern = (pattern << 1) | (image[point.X, point.Y] ? 1 : 0);
        }
        return pattern;
    }

    public int ReadWord(BinaryImage image, int row, int column, int y) =>
        Pdf417Symbols.Decode(Pattern(image, Left + 34 + column * 17, y), row % 3 * 3);

    public CodeLayout CreateLayout()
    {
        var cells = Enumerable.Repeat(new ModuleCell(ModuleRole.QuietZone), _original.Width * _original.Height).ToArray();
        for (var row = 0; row < Rows.Count; row++)
            for (var y = Rows[row].Top; y <= Rows[row].Bottom; y++)
                for (var offset = 0; offset < SymbolWidth; offset++)
                {
                    var point = ToImage(Left + offset, y);
                    ModuleCell cell;
                    if (offset < 17 || offset >= 51 + Columns * 17) cell = new(ModuleRole.Guard);
                    else if (offset < 34 || offset >= 34 + Columns * 17) cell = new(ModuleRole.RowIndicator);
                    else
                    {
                        var word = row * Columns + (offset - 34) / 17;
                        cell = new(word < DataWords ? ModuleRole.Data : ModuleRole.ErrorCorrection, word, (offset - 34) % 17) { Block = 0 };
                    }
                    cells[point.Y * _original.Width + point.X] = cell;
                }
        return new(_original.Width, _original.Height, cells);
    }

    private bool TryRead()
    {
        var right = -1; var bottom = -1;
        Left = Width; Top = Height;
        for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
            {
                var p = ToImage(x, y);
                if (!_original[p.X, p.Y]) continue;
                Left = Math.Min(Left, x); Top = Math.Min(Top, y);
                right = Math.Max(right, x); bottom = Math.Max(bottom, y);
            }
        SymbolWidth = right - Left + 1;
        SymbolHeight = bottom - Top + 1;
        if (SymbolWidth < 86 || (SymbolWidth - 69) % 17 != 0) return false;
        Columns = (SymbolWidth - 69) / 17;
        if (Columns is < 1 or > 30) return false;
        var rows = new List<(int Top, int Bottom)>();
        var start = Top;
        for (var y = Top; y <= bottom; y++)
        {
            if (Pattern(_original, Left, y) != 0x1fea8 || Pattern(_original, right - 17, y, 18) != 0x3fa29) return false;
            // The indicator changes with every logical row, independently of repeated payloads.
            if (y > Top && Pattern(_original, Left + 17, y) != Pattern(_original, Left + 17, y - 1))
            { rows.Add((start, y - 1)); start = y; }
        }
        rows.Add((start, bottom));
        if (rows.Count is < 3 or > 90 || rows.Count * Columns > 928) return false;
        Rows = rows;
        var indicator = Pdf417Symbols.Decode(Pattern(_original, Left + 17, Rows[1].Top), 3);
        if (indicator < 0) return false;
        CorrectionLevel = indicator % 30 / 3;
        if (CorrectionLevel > 8 || CorrectionWords >= rows.Count * Columns) return false;
        var words = new List<int>();
        for (var row = 0; row < Rows.Count; row++)
        {
            var rowGroup = 30 * (row / 3);
            var rowCount = (Rows.Count - 1) / 3;
            var correction = CorrectionLevel * 3 + (Rows.Count - 1) % 3;
            var left = rowGroup + ((row % 3) switch { 0 => rowCount, 1 => correction, _ => Columns - 1 });
            var rightIndicator = rowGroup + ((row % 3) switch { 0 => Columns - 1, 1 => rowCount, _ => correction });
            var y = Rows[row].Top;
            if (Pdf417Symbols.Decode(Pattern(_original, Left + 17, y), row % 3 * 3) != left
                || Pdf417Symbols.Decode(Pattern(_original, Left + 34 + Columns * 17, y), row % 3 * 3) != rightIndicator) return false;
            for (var column = 0; column < Columns; column++)
            {
                var word = ReadWord(_original, row, column, y);
                if (word < 0) return false;
                words.Add(word);
            }
        }
        Words = words;
        return Words[0] == DataWords;
    }
}
