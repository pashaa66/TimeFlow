# Сборка и запуск TimeFlow

## Требования

- **.NET 8 SDK** — https://dotnet.microsoft.com/download
- **Visual Studio 2022** с компонентом «.NET desktop development»

## Сборка

### Visual Studio 2022 (рекомендуется)
1. Открой `TimeFlow.sln`
2. Выбери **Release | x64**
3. F5 — запуск с отладкой, Ctrl+F5 — без отладки
4. `.exe` появится в `bin\x64\Release\net8.0-windows\`

### Командная строка
```bat
dotnet build -c Release
dotnet run -c Release
```

### Один клик
```bat
scripts\build.bat
```

## Форматы файлов

### Исходные (папка `data/`)

| Файл | Формат | Шифрование | Описание |
|------|--------|-----------|----------|
| `settings.ini` | INI | Нет | Настройки приложения |
| `tasks.json` | JSON-контейнер | RC4 | Активные задачи |
| `history.json` | JSON-контейнер | RC4 | История задач |

### INI-секции (`settings.ini`)

| Секция | Ключи |
|--------|-------|
| `[Main]` | `hourly_rate`, `currency` |
| `[Pomodoro]` | `pomodoro_work_min`, `pomodoro_break_min`, `pomodoro_long_break_min`, `pomodoro_cycles_until_long` |
| `[VirtualTime]` | `virtual_time_enabled`, `virtual_time_ratio` |
| `[Categories]` | `items` (JSON-словарь `{name: color}`) |

### JSON-контейнер (зашифрованный)

```json
{
  "enc": true,
  "payload": "<base64 RC4(JSON)>"
}
```

### JSON задач (расшифрованный)

```json
{
  "tasks": [
    {
      "Id": "abc123",
      "Name": "Название",
      "Category": "Работа",
      "EstimatedMinutes": 30,
      "Done": false,
      "CreatedAt": "2026-06-30T10:00:00",
      "Sessions": [
        { "Start": 1751000000, "End": 1751003600, "Vsec": 3600 }
      ]
    }
  ]
}
```

### Результирующие (папка `data/exports/`)

| Файл | Формат | Шифрование |
|------|--------|-----------|
| `report_YYYY-MM.json` | JSON | Нет (открытый текст) |

### Схема отчёта

```json
{
  "generated_at": "2026-06-30T10:00:00",
  "period": "2026-06",
  "hourly_rate": 500.0,
  "currency": "₽",
  "total_seconds": 72000,
  "total_hours": 20.0,
  "total_earnings": 2777.78,
  "tasks": [
    {
      "name": "Название",
      "category": "Работа",
      "status": "done",
      "seconds": 3600,
      "hours": 1.0,
      "earnings": 138.89
    }
  ]
}
```

## Возможные проблемы

### «Не найден .NET SDK»
Установи .NET 8 SDK с https://dotnet.microsoft.com/download

### «Windows Forms не найден»
В Visual Studio Installer отметь «.NET desktop development».

### Пустое окно / нет данных
Проверь что рядом с `TimeFlow.exe` лежит папка `data/` с файлами.
`csproj` копирует её автоматически при сборке.