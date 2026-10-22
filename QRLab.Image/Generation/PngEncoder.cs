// QRLab.Image/Generation/PngEncoder.cs
using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;

namespace QRLab.Image.Generation;

/// <summary>
/// Минимальный кроссплатформенный кодировщик монохромных изображений в PNG.
/// Использует только базовую библиотеку .NET (без System.Drawing / SkiaSharp),
/// поэтому результат побайтово идентичен на Windows, Linux и macOS.
/// </summary>
public static class PngEncoder
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];
    private const byte Black = 0;
    private const byte White = 255;

    /// <summary>
    /// Кодирует матрицу модулей (true = тёмный) в PNG: grayscale, 8 бит на канал.
    /// Каждый модуль занимает scale×scale пикселей, вокруг — светлая зона шириной quietZone модулей.
    /// </summary>
    public static byte[] Encode(bool[,] matrix, int scale = 4, int quietZone = 4)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        if (scale <= 0) throw new ArgumentOutOfRangeException(nameof(scale));
        if (quietZone < 0) throw new ArgumentOutOfRangeException(nameof(quietZone));

        int rows = matrix.GetLength(0);
        int cols = matrix.GetLength(1);
        int width = (cols + 2 * quietZone) * scale;
        int height = (rows + 2 * quietZone) * scale;

        using var output = new MemoryStream();
        output.Write(Signature);
        WriteChunk(output, "IHDR"u8, BuildIhdr(width, height));
        WriteChunk(output, "IDAT"u8, CompressRawData(matrix, scale, quietZone, rows, cols, width, height));
        WriteChunk(output, "IEND"u8, []);
        return output.ToArray();
    }

    private static byte[] BuildIhdr(int width, int height)
    {
        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(0), width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), height);
        ihdr[8] = 8; // бит на канал
        ihdr[9] = 0; // grayscale
        // ihdr[10..13]: compression / filter / interlace = 0
        return ihdr;
    }

    private static byte[] CompressRawData(bool[,] matrix, int scale, int quietZone,
        int rows, int cols, int width, int height)
    {
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            var scanline = new byte[width];
            for (var y = 0; y < height; y++)
            {
                scanline.AsSpan().Fill(White); // по умолчанию белый фон
                int my = y / scale - quietZone;
                if (my >= 0 && my < rows)
                {
                    for (int x = 0; x < width; x += scale)
                    {
                        int mx = x / scale - quietZone;
                        if (mx >= 0 && mx < cols && matrix[my, mx])
                            scanline.AsSpan(x, scale).Fill(Black);
                    }
                }
                zlib.WriteByte(0); // фильтр None для каждой строки
                zlib.Write(scanline);
            }
        }
        return compressed.ToArray();
    }

    private static void WriteChunk(Stream stream, ReadOnlySpan<byte> type, byte[] data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        stream.Write(length);

        var chunk = new byte[type.Length + data.Length];
        type.CopyTo(chunk);
        data.CopyTo(chunk.AsSpan(type.Length));
        stream.Write(chunk);

        Span<byte> crc = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(crc, (int)ComputeCrc32(chunk));
        stream.Write(crc);
    }

    // Стандартный CRC-32 (полином 0xEDB88320), как требует спецификация PNG
    private static uint ComputeCrc32(byte[] bytes)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in bytes)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
        }
        return crc ^ 0xFFFFFFFF;
    }
}
