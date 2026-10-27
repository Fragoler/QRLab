# QRLab

**QRLab** — кроссплатформенное приложение на C# и Avalonia UI для генерации, мутации, обработки и распознавания QR-кодов с визуализацией этапов преобразования.

Проект построен с разделением логики данных и представления:

- `QRLab.Core.Services` — доменные модели, интерфейсы и контракты данных;
- `QRLab.View` — пользовательский интерфейс на Avalonia UI и MVVM-слой;
- native-библиотеки обеспечивают работу на Linux, Windows и macOS.

---

## Технологии

- .NET SDK 10.0+
- C#
- Avalonia UI
- CommunityToolkit.Mvvm
- SkiaSharp
- ZXing

---

## Требования

- [.NET SDK 10.0+](https://dotnet.microsoft.com/download)
- Linux:
  - `libgl1`
  - `libx11`
  - `libxrender`
- macOS:
  - `XQuartz`, если используется X11
- Windows:
  - дополнительные системные зависимости обычно не требуются

---

## Установка зависимостей

### Linux

Для Debian/Ubuntu:

```bash
sudo apt-get update
sudo apt-get install libgl1 libx11 libxrender
```

### macOS

```bash
brew install xquartz
```

### Windows

Системные зависимости устанавливать не требуется.

---

## Сборка и запуск

### Проверка версии .NET

```bash
dotnet --version
```

### Запуск в режиме разработки

Linux:

```bash
dotnet run --runtime linux-x64
```

Windows:

```bash
dotnet run --runtime win-x64
```

macOS ARM:

```bash
dotnet run --runtime osx-arm64
```

macOS Intel:

```bash
dotnet run --runtime osx-x64
```

### Публикация для развертывания

Linux:

```bash
dotnet publish -r linux-x64 -c Release
```

Windows:

```bash
dotnet publish -r win-x64 -c Release
```

macOS ARM:

```bash
dotnet publish -r osx-arm64 -c Release
```

macOS Intel:

```bash
dotnet publish -r osx-x64 -c Release
```

### Запуск опубликованного приложения

После публикации исполняемый файл обычно находится в каталоге вида:

```text
bin/Release/net10.0/<rid>/publish/
```

Запуск на Linux/macOS:

```bash
./QRLab.View
```

Запуск на Windows:

```powershell
.\QRLab.View.exe
```

---

## Структура проекта

Основные элементы проекта:

```text
QRLab/
├── MainWindow.axaml
├── ViewModels/
├── Converters/
├── Styles/
├── runtimes/
└── QRLab.Core.Services/
```

Описание:

- `MainWindow.axaml` — главное окно приложения;
- `ViewModels/` — MVVM-логика интерфейса;
- `Converters/` — конвертеры значений для отображения в UI;
- `Styles/` — стили оформления Avalonia;
- `runtimes/` — native-библиотеки для разных платформ;
- `QRLab.Core.Services` — модели, интерфейсы и сервисы данных;
- `ViewModels/DataExportViewModel.cs` — реализация экспорта данных.

---

## Архитектура

### Core Services

Ядро приложения описывает данные и операции над QR-образцами через интерфейс `IPrototypeData`.

Основные методы:

```csharp
IReadOnlyList<LibrarySet> LibrarySets { get; }

IReadOnlyList<SamplePreview> CreateSamples(GenerationOptions options);

IReadOnlyList<RecognitionPreview> Recognize(
    IReadOnlyList<SamplePreview> samples,
    RecognitionOptions options);

string Export(
    IReadOnlyList<SamplePreview> samples,
    ExportOptions options);

FrameOptions CreateVariant(int seed, FrameOptions current);

bool[,] CreateFrame(
    bool[,] original,
    FrameOptions options,
    bool difference = false);
```

### Основные модели

| Тип | Назначение |
| --- | --- |
| `SamplePreview` | Готовый QR-код с метаданными: номер, имя, содержимое, формат, матрица модулей, операции мутации и настройки генерации |
| `MutationPreview` | Описание операции мутации: номер, имя, детали и этап применения |
| `FrameOptions` | Параметры кадровой обработки: поворот, размытие, размер кисти, непрозрачность, центр, режим стирания и seed |
| `GenerationOptions` | Настройки генерации: шаблон, начальный номер, количество, формат, размеры, уровень коррекции ошибок и зона покоя |
| `ExportOptions` | Параметры экспорта: путь, формат и включение метаданных |
| `RecognitionOptions` | Настройки распознавания: декодер, таймаут и улучшенная обработка |
| `LibrarySet` | Набор шаблонов: имя, количество и путь к шаблону |
| `RecognitionPreview` | Результат распознавания образца |

### Статусы распознавания

```csharp
ResultStatus.Success  // Успешное считывание
ResultStatus.Failed   // Не удалось распознать
ResultStatus.Timeout  // Превышено время ожидания
```

---

## ViewModels

Приложение использует MVVM-паттерн через `CommunityToolkit.Mvvm`.

### `MainWindowViewModel`

Отвечает за главное окно и навигацию по вкладкам.

Основные свойства:

| Свойство | Описание |
| --- | --- |
| `Workspace` | Модель рабочей области с коллекцией образцов |
| `Generation` | Модель вкладки генерации |
| `Mutations` | Модель вкладки мутаций |
| `Reading` | Модель вкладки распознавания |
| `Comparison` | Модель вкладки сравнения |
| `Screens` | Список всех вкладок |
| `SelectedScreen` | Активная вкладка, по умолчанию `Generation` |
| `IsAboutOpen` | Открыто ли окно «О программе» |

### `WorkspaceViewModel`

Хранит коллекцию образцов и команды над ними.

Основные свойства:

| Свойство | Описание |
| --- | --- |
| `Samples` | Коллекция образцов |
| `SelectedSample` | Текущий выбранный образец |
| `Notice` | Уведомления и статусы |
| `Position` | Текущая позиция в формате `X / Y` |

Основные методы:

| Метод | Описание |
| --- | --- |
| `Generate(GenerationOptions)` | Создание новой выборки образцов |
| `Recognize(RecognitionOptions?)` | Запуск распознавания всех образцов |
| `Export(ExportOptions)` | Экспорт данных в файл |
| `CreateVariant(int, FrameOptions)` | Создание варианта настроек кадра |
| `Frame(SampleViewModel?, FrameOptions, bool)` | Получение обработанного кадра |
| `Previous()` | Переход к предыдущему образцу |
| `Next()` | Переход к следующему образцу |

### `SampleViewModel`

Представляет один QR-код.

Основные свойства:

| Свойство | Описание |
| --- | --- |
| `Preview` | Исходные данные образца |
| `Name` | Имя образца |
| `Content` | Содержимое QR-кода |
| `Dimensions` | Размеры в пикселях |
| `Correction` | Уровень коррекции ошибок |
| `Format` | Формат |
| `Modules` | Матрица модулей |
| `Operations` | Коллекция операций мутации |
| `Result` | Результат распознавания |

Свойства на основе результата распознавания:

| Свойство | Описание |
| --- | --- |
| `Status` | Статус распознавания: не обработан, считан, не считан, таймаут |
| `IsFailure` | Признак неудачного распознавания |
| `Time` | Время обработки в миллисекундах |
| `Chain` | Цепочка декодирования |
| `Decoded` | Декодированное содержимое или `—` при неудаче |

### `OperationViewModel`

Представляет одну операцию мутации.

| Свойство | Описание |
| --- | --- |
| `Number` | Номер операции |
| `Name` | Имя операции |
| `Details` | Детали операции |
| `Stage` | Этап внедрения |
| `Modules` | Обработанная матрица, может быть `null` |
| `Options` | Настройки обработки |

---

## Экспорт данных

Экспорт реализован в файле:

```text
ViewModels/DataExportViewModel.cs
```

Поддерживаемые форматы:

- CSV;
- JSON;
- PDF.

Пример экспорта:

```csharp
Workspace.Export(new ExportOptions(
    destination: "path/to/file",
    format: "json",
    includeMetadata: true
));
```

---

## Примеры использования

### Генерация образцов

```csharp
Workspace.Generate(new GenerationOptions(
    template: "LAB-2026-{num}",
    startNumber: 1,
    count: 120,
    format: "QR-код",
    width: 1920,
    height: 1080,
    correction: "H • 30%"
));
```

### Распознавание

```csharp
Workspace.Recognize(new RecognitionOptions(
    decoder: "ZXing • 2.3.0",
    timeout: 500,
    enhanced: true
));
```

### Экспорт

```csharp
Workspace.Export(new ExportOptions(
    destination: "path/to/file",
    format: "json",
    includeMetadata: true
));
```

---

## Важные замечания

- Убедитесь, что native-библиотеки, например `libSkiaSharp.so`, попадают в выходную сборку.
  Обычно они должны находиться в каталоге:

  ```text
  runtimes/<rid>/native/
  ```

  либо в корне опубликованного приложения, если проект настроен таким образом.

- Для работы графики обязательны системные зависимости, указанные в разделе «Требования».
- `IPrototypeData` используется как контракт. Реальный движок генерации и распознавания можно подключить, заменив поставщик данных.
- Навигация между вкладками выполняется командой:

  ```csharp
  Navigate(string number)
  ```

- При переключении на вкладку сравнения автоматически вызывается:

  ```csharp
  Comparison.Refresh()
  ```

---

## Отладка

### Просмотр логов приложения на Linux

Для трассировки системных вызовов можно использовать:

```bash
strace -f ./QRLab.View
```

### Проверка содержимого README

```bash
cat README.md
```

---

## Кратко

Используйте:

```bash
dotnet run
```

для тестирования и разработки.

Используйте:

```bash
dotnet publish -r <rid> -c Release
```

для подготовки приложения к развертыванию.
