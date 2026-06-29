# TimeFlow (C# / Windows Forms)

Трекер задач и тайм-менеджмент с Pomodoro-таймером, виртуальным временем,
аналитикой и историей задач. Полная переработка исходного Python/PyQt проекта
на **C# / .NET 8 / Windows Forms**.

## Команда: Петров Павел, Тимонов Артём
## Группа: 2МОАИС, РБД-1

## Стек технологий

| Компонент | Технология |
|-----------|------------|
| Язык | C# 12 / .NET 8 |
| GUI | Windows Forms (WinForms) |
| Графика | GDI+ (System.Drawing) |
| Хранилище | JSON + INI (локальные файлы) |
| Шифрование | RC4 + SHA-256 |
| Среда разработки | Visual Studio 2022 |

## Архитектура

Проект разделён на два слоя, граница — события C#:

```
┌─────────────────────────────────┐
│            UI Layer             │
│  MainForm, TasksPanel,          │
│  AnalyticsPanel, Dialogs...     │
└──────────┬──────────────────────┘
           │ события (TasksChanged,
           │ Ticked, PhaseChanged...)
┌──────────▼──────────────────────┐
│           Core Layer            │
│  TaskManager, VirtualClock,     │
│  PomodoroTimer, SettingsStore,  │
│  JsonStore, Crypto              │
└─────────────────────────────────┘
```

UI никогда не обращается к файлам напрямую — только через Core.

## Функциональные блоки

**TaskManager** — единственный источник правды для задач. CRUD, запуск/остановка
таймеров, аналитика (заработок, секунды по дням/категориям), история с
дедупликацией, экспорт отчётов в JSON.

**VirtualClock** — виртуальное время с настраиваемым коэффициентом ускорения.
Все сессии хранятся в Unix-секундах реального времени, виртуальные секунды
вычисляются на лету через `ElapsedVirtualSeconds`.

**PomodoroTimer** — автомат фаз work → break → long_break. Работает на
`System.Windows.Forms.Timer` (UI-поток, без Invoke). События: `Tick`,
`PhaseChanged`, `FinishedPhase`.

**SettingsStore** — INI-парсер с секциями Main/Pomodoro/VirtualTime/Categories.
CRUD категорий с цветами. Все значения с дефолтами.

**Crypto + JsonStore** — RC4 с ключом через SHA-256. Файлы задач и истории
хранятся в зашифрованном контейнере. Открытый текст определяется автоматически
при загрузке.

## Особенности

- **C# ~100%** — правило «C# >= 70%» выполнено
- **Без внешних библиотек** — графики через GDI+
- **Шифрование RC4** — tasks.json и history.json защищены
- **Виртуальное время** — ускорение в N раз для демонстрации
- **Системный трей** — уведомления о смене фаз Pomodoro
- **CRUD категорий** — с цветами и валидацией

## Требования

- **.NET 8 SDK** — https://dotnet.microsoft.com/download
- **Visual Studio 2022** с компонентом «.NET desktop development»
- **Windows 10/11**

## Сборка и запуск

Инструкции по сборке, запуску и форматам файлов: [docs/BUILD.md](docs/BUILD.md)

## Структура проекта

```
TimeFlow/
├── TimeFlow.sln / TimeFlow.csproj
├── Program.cs                       # точка входа, DI
├── Core/
│   ├── Config.cs                    # пути, константы
│   ├── Crypto.cs                    # RC4 + SHA-256
│   ├── JsonStore.cs                 # JSON с шифрованием
│   ├── SettingsStore.cs             # INI настройки
│   ├── VirtualClock.cs              # виртуальное время
│   ├── Pomodoro.cs                  # таймер Pomodoro
│   ├── TaskItem.cs                  # модель задачи
│   └── TaskManager.cs               # CRUD + аналитика
├── UI/
│   ├── MainForm.cs                  # главное окно
│   ├── TaskCard.cs                  # карточка задачи
│   ├── TasksPanel.cs                # список задач
│   ├── AnalyticsPanel.cs            # графики GDI+
│   ├── AddTaskDialog.cs             # диалог добавления
│   ├── SettingsDialog.cs            # настройки
│   ├── HistoryForm.cs               # история задач
│   └── MiniTimerForm.cs             # мини-виджет
├── data/
│   ├── settings.ini
│   ├── tasks.json
│   ├── history.json
│   └── exports/
├── docs/
└── scripts/
```