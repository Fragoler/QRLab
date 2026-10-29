<div align="center">

<img src="QRLab.View/Assets/svg/logo.svg" width="72" height="72" alt="Логотип QRLab" />

# QRLab

[![Тесты](https://github.com/Fragoler/QRLab/actions/workflows/tests.yml/badge.svg)](https://github.com/Fragoler/QRLab/actions/workflows/tests.yml)
[![Последний релиз](https://img.shields.io/github/v/release/Fragoler/QRLab?color=76D9BD&label=release)](https://github.com/Fragoler/QRLab/releases/latest)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Avalonia UI](https://img.shields.io/badge/UI-Avalonia-8B44AC)](https://avaloniaui.net/)

[Скачать](https://github.com/Fragoler/QRLab/releases/latest) · [Возможности](#возможности) · [Запуск из исходников](#запуск-из-исходников)

</div>

**QRLab** — учебное настольное приложение для лабораторной работы по курсу «Теория информации» для НИЯУ МИФИ (MEPhI). 
Оно позволяет создавать QR-коды и штрихкоды и исследовать, как различные мутации изображений влияют на возможность их сканирования и корректность распознавания.

## Возможности

- **Генерация:** Наборы образцов: QR, EAN-13, Code 128 и PDF417.
- **Мутации:** гауссово размытие, шум «соль и перец», перспективные искажения и последовательности этих преобразований.
- **Анализ:** распознавание декодерами, проверка совпадения результата с исходными данными.
- **Наборы:** сохранение образцов и результатов преобразований для повторных экспериментов.

## Установка последней версии

Откройте **[последний релиз](https://github.com/Fragoler/QRLab/releases/latest)** и скачайте файл из раздела **Assets** для своей системы и архитектуры:

| Система | Архитектура | Файл в релизе |
| --- | --- | --- |
| Windows | x64 — Intel / AMD | [`QRLab.exe`](https://github.com/Fragoler/QRLab/releases/latest/download/QRLab.exe) |
| Linux | x64 — Intel / AMD | `QRLab-vX.Y.Z-linux-x64` |
| macOS | x64 — Intel | `QRLab-vX.Y.Z-osx-x64` |
| macOS | ARM64 — Apple Silicon | `QRLab-vX.Y.Z-osx-arm64` |

`vX.Y.Z` — номер выбранного релиза. Для Windows ARM64 и Linux ARM64 готовые сборки пока не предусмотрены.

Приложение распространяется одним портативным исполняемым файлом со встроенной средой .NET: отдельно устанавливать .NET или запускать установщик не нужно.
На минимальных установках могут потребоваться системные библиотеки, см. [зависимости Avalonia для Linux](https://docs.avaloniaui.net/docs/deployment/linux).

## Запуск из исходников

Понадобятся [Git](https://git-scm.com/) и [.NET SDK 10.0](https://dotnet.microsoft.com/download/dotnet/10.0):

```bash
git clone https://github.com/Fragoler/QRLab.git
cd QRLab
dotnet run --project QRLab.View/QRLab.View.csproj --configuration Release
```

Зависимости NuGet восстанавливаются автоматически. На Linux также нужны системные библиотеки, указанные выше.

Для запуска всех тестов из корня репозитория:

```bash
dotnet test QRLab.sln --configuration Release
```

## Архитектура

| Компонент | Назначение |
| --- | --- |
| `QRLab.Core` | Модели, параметры экспериментов и интерфейсы |
| `QRLab.Image` | Генерация, преобразование, распознавание и хранение изображений |
| `QRLab.View` | Avalonia UI и CommunityToolkit.Mvvm |
| `QRLab.Test` | Модульные тесты и проверки интерфейса |

Обработка изображений — **SkiaSharp**, работа с кодами — **ZXing.Net**.

## Лицензия 
MIT, [LICENSE](LICENSE.md)