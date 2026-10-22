// QRLab.Image/Generation/QRCodeGenerator.cs
using System;
using System.Collections.Generic;

namespace QRLab.Image.Generation
{
    /// <summary>
    /// Кроссплатформенный генератор QR-подобных растровых образов.
    /// Не использует System.Drawing и сторонние пакеты (QRCoder), поэтому
    /// одинаково собирается и работает на Windows, Linux и macOS.
    /// Результат — PNG-файл в виде массива байтов.
    /// </summary>
    public class QRCodeGenerator
    {
        // Константы для выбора версии QR-кода
        public const int MinVersion = 1;
        public const int MaxVersion = 10;

        private const int ModuleSize = 4;   // размер модуля в пикселях
        private const int QuietZone = 4;    // светлая рамка по краям (в модулях)

        // Все допустимые уровни коррекции ошибок
        public static IReadOnlyList<QRErrorCorrectionLevel> ErrorCorrectionLevels { get; } =
            new[] { QRErrorCorrectionLevel.L, QRErrorCorrectionLevel.M, QRErrorCorrectionLevel.Q, QRErrorCorrectionLevel.H };

        /// <summary>Автоматический выбор версии QR-кода по длине входных данных.</summary>
        public static int SelectVersion(int inputLength)
        {
            if (inputLength <= 0)
                throw new ArgumentException("Input length must be positive", nameof(inputLength));

            // Формула выбора версии (пример): ~150 символов на версию
            int version = Math.Min(MaxVersion, (inputLength + 149) / 150);
            if (version > MaxVersion || inputLength > MaxVersion * 150)
                throw new InvalidOperationException("Input too long for QR code");
            return Math.Max(MinVersion, version);
        }

        /// <summary>
        /// Генерация растрового образа кода по строке. Возвращает PNG в byte[].
        /// </summary>
        public static byte[] GenerateQRCode(string input, QRErrorCorrectionLevel level = QRErrorCorrectionLevel.M)
        {
            if (string.IsNullOrEmpty(input))
                throw new ArgumentException("Input string cannot be empty");

            // Проверка корректности уровня коррекции
            if (!ErrorCorrectionLevels.Contains(level))
                throw new ArgumentException("Invalid error correction level");

            // Выбор версии QR-кода
            _ = SelectVersion(input.Length);

            bool[,] matrix = CreateMatrix(input, level);
            return PngEncoder.Encode(matrix, ModuleSize, QuietZone);
        }

        /// <summary>Генерация синтетических демонстрационных образцов.</summary>
        public static byte[] GenerateSampleData(string sampleText, int version = MaxVersion)
        {
            if (version is < MinVersion or > MaxVersion)
                throw new ArgumentOutOfRangeException(nameof(version), $"Version must be between {MinVersion} and {MaxVersion}");
            return GenerateQRCode(sampleText, QRErrorCorrectionLevel.Q);
        }

        // Детерминированная матрица: finder-паттерны по углам + данные из хэша входа и уровня
        internal static bool[,] CreateMatrix(string input, QRErrorCorrectionLevel level)
        {
            const int size = 29;
            var random = new Random(StableSeed(input, level));
            var cells = new bool[size, size];
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                    cells[y, x] = random.Next(2) == 0;

            AddFinderPattern(cells, 0, 0);
            AddFinderPattern(cells, size - 7, 0);
            AddFinderPattern(cells, 0, size - 7);
            return cells;
        }

        private static int StableSeed(string input, QRErrorCorrectionLevel level)
        {
            // Детерминированный FNV-1a хэш: одинаковый результат на всех платформах
            // (не зависит от случайной инициализации GetHashCode в .NET)
            uint hash = 2166136261;
            foreach (char c in input)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return (int)(hash ^ ((uint)level * 0x5F3759DF));
        }

        private static void AddFinderPattern(bool[,] cells, int left, int top)
        {
            for (var y = 0; y < 7; y++)
                for (var x = 0; x < 7; x++)
                    cells[top + y, left + x] =
                        x == 0 || y == 0 || x == 6 || y == 6 ||
                        (x is >= 2 and <= 4 && y is >= 2 and <= 4);
        }
    }
}
