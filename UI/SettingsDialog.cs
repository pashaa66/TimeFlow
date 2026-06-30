using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using TimeFlow;

namespace TimeFlow.UI;

public class SettingsDialog : Form
{
    // Цвета тёмной темы 
    private static readonly Color COLOR_BACKGROUND = Color.FromArgb(30, 30, 30);
    private static readonly Color COLOR_SURFACE = Color.FromArgb(45, 45, 45);
    private static readonly Color COLOR_CARD = Color.FromArgb(38, 38, 38);
    private static readonly Color COLOR_TEXT_PRIMARY = Color.FromArgb(220, 220, 220);
    private static readonly Color COLOR_TEXT_SECONDARY = Color.FromArgb(150, 150, 150);
    private static readonly Color COLOR_ACCENT = Color.FromArgb(76, 175, 80);
    private static readonly Color COLOR_BORDER = Color.FromArgb(60, 60, 60);
    private static readonly Color COLOR_DANGER = Color.FromArgb(244, 67, 54);

    // Ядро
    private readonly SettingsStore _settings;

    // Локальные буферы
    private string _currency;
    private double _hourlyRate;
    private int _pomodoroWorkMin;
    private int _pomodoroBreakMin;
    private int _pomodoroLongBreakMin;
    private int _pomodoroCyclesUntilLong;
    private bool _virtualTimeEnabled;
    private double _virtualTimeRatio;
    private bool _miniTimerEnabled;       // НОВОЕ: показывать плавающий виджет
    private readonly Dictionary<string, string> _editedCategories;
    private string _selectedColorHex = Config.ColorPalette[0];

    // UI элементы 
    private TextBox? _txtCurrency;
    private NumericUpDown? _numHourlyRate;

    private NumericUpDown? _numWorkMin;
    private NumericUpDown? _numBreakMin;
    private NumericUpDown? _numLongBreakMin;
    private NumericUpDown? _numCyclesUntilLong;

    private CheckBox? _chkVirtualTime;
    private NumericUpDown? _numVirtualRatio;

    private CheckBox? _chkMiniTimer;   // НОВОЕ: чекбокс плавающего виджета

    private FlowLayoutPanel? _categoriesList;
    private TextBox? _txtNewCategoryName;
    private FlowLayoutPanel? _palettePanel;
    private Label? _lblSelectedColorName;
    private Label? _selectedColorDot;
    private readonly List<ColorDot> _paletteDots = new();

    public SettingsDialog(SettingsStore settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        _currency = _settings.Get("currency", "₽");
        _hourlyRate = _settings.GetDouble("hourly_rate", 0);
        _pomodoroWorkMin = _settings.GetInt("pomodoro_work_min", 25);
        _pomodoroBreakMin = _settings.GetInt("pomodoro_break_min", 5);
        _pomodoroLongBreakMin = _settings.GetInt("pomodoro_long_break_min", 15);
        _pomodoroCyclesUntilLong = _settings.GetInt("pomodoro_cycles_until_long", 4);
        _virtualTimeEnabled = _settings.GetBool("virtual_time_enabled");
        _virtualTimeRatio = _settings.GetDouble("virtual_time_ratio", 1.0);
        _miniTimerEnabled = _settings.GetBool("mini_timer_enabled", true);  // НОВОЕ: по умолчанию ВКЛ
        _editedCategories = new Dictionary<string, string>(_settings.Categories());

        InitializeLayout();
        RefreshCategoriesList();
        SelectColor(_selectedColorHex);
    }

    // Каркас формы 
    private void InitializeLayout()
    {
        this.Text = "Настройки";
        this.Size = new Size(740, 880);
        this.MinimumSize = new Size(640, 600);
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.Sizable;
        this.BackColor = COLOR_BACKGROUND;
        this.ForeColor = COLOR_TEXT_PRIMARY;
        this.Font = new Font("Segoe UI", 9F);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = COLOR_BACKGROUND,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));

        var scrollPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = COLOR_BACKGROUND,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };

        var innerStack = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 9,
            BackColor = COLOR_BACKGROUND,
            Padding = new Padding(20, 20, 20, 20),
            Margin = new Padding(0)
        };
        innerStack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));


        innerStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // 0: "1. Заработок"
        innerStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 110F)); // 1: earnings card
        innerStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 8F));   // 2: gap
        innerStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // 3: "2. Pomodoro"
        innerStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 180F)); // 4: pomodoro card
        innerStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 8F));   // 5: gap
        innerStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // 6: "3. Виртуальное время"
        innerStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F)); // 7: vtime card
        innerStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 8F));   // 8: gap
        innerStack.RowCount = 17;
        innerStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // 9: "4. Категории"
        innerStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 520F)); // 10: categories card
        innerStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 8F));   // 11: gap
        innerStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 0F));   // 12: reserve
        innerStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // 13: "5. Плавающий виджет"
        innerStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));  // 14: mini-timer card
        innerStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 8F));   // 15: gap
        innerStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 0F));   // 16: reserve

        innerStack.Controls.Add(MakeHeader("1. Заработок"), 0, 0);
        innerStack.Controls.Add(BuildEarningsCard(), 0, 1);

        innerStack.Controls.Add(MakeHeader("2. Таймер Pomodoro"), 0, 3);
        innerStack.Controls.Add(BuildPomodoroCard(), 0, 4);

        innerStack.Controls.Add(MakeHeader("3. Виртуальное время"), 0, 6);
        innerStack.Controls.Add(BuildVirtualTimeCard(), 0, 7);

        innerStack.Controls.Add(MakeHeader("4. Настройка категорий"), 0, 9);
        innerStack.Controls.Add(BuildCategoriesCard(), 0, 10);

        innerStack.Controls.Add(MakeHeader("5. Плавающий виджет"), 0, 13);
        innerStack.Controls.Add(BuildMiniTimerCard(), 0, 14);

        scrollPanel.Controls.Add(innerStack);
        root.Controls.Add(scrollPanel, 0, 0);
        root.Controls.Add(BuildButtonPanel(), 0, 1);

        this.Controls.Add(root);
        this.AcceptButton = null;
    }

    private Label MakeHeader(string text)
    {
        return new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            ForeColor = COLOR_TEXT_PRIMARY,
            AutoSize = true,
            Margin = new Padding(0, 6, 0, 6),
            Dock = DockStyle.Top
        };
    }

    private TableLayoutPanel MakeCardContainer(int rowCount)
    {
        var card = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = rowCount,
            BackColor = COLOR_CARD,
            Padding = new Padding(16, 14, 16, 14),
            Margin = new Padding(0, 0, 0, 0),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280F));
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        return card;
    }

    // Заработок 
    private TableLayoutPanel BuildEarningsCard()
    {
        var card = MakeCardContainer(2);
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));

        // Валюта
        var lblCurrency = new Label
        {
            Text = "Валюта:",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10F),
            ForeColor = COLOR_TEXT_PRIMARY,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 4, 12, 4)
        };

        _txtCurrency = new TextBox
        {
            Dock = DockStyle.Fill,
            BackColor = COLOR_BACKGROUND,
            ForeColor = COLOR_TEXT_PRIMARY,
            Font = new Font("Segoe UI", 10F),
            BorderStyle = BorderStyle.FixedSingle,
            Text = _currency,
            Margin = new Padding(0, 4, 0, 4)
        };
        _txtCurrency.TextChanged += (_, _) => _currency = _txtCurrency.Text;

        // Ставка 
        var lblRate = new Label
        {
            Text = "Ставка за час:",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10F),
            ForeColor = COLOR_TEXT_PRIMARY,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 4, 12, 4)
        };

        _numHourlyRate = new NumericUpDown
        {
            Dock = DockStyle.Fill,
            Minimum = 0,                     
            Maximum = 1000000,
            DecimalPlaces = 2,
            Increment = 50M,
            BackColor = COLOR_BACKGROUND,
            ForeColor = COLOR_TEXT_PRIMARY,
            Font = new Font("Segoe UI", 10F),
            Value = (decimal)Math.Max(0, _hourlyRate),
            Margin = new Padding(0, 4, 0, 4)
        };
        _numHourlyRate.ValueChanged += (_, _) => _hourlyRate = (double)_numHourlyRate.Value;

        card.Controls.Add(lblCurrency, 0, 0);
        card.Controls.Add(_txtCurrency, 1, 0);
        card.Controls.Add(lblRate, 0, 1);
        card.Controls.Add(_numHourlyRate, 1, 1);

        return card;
    }

    // Pomodoro 
    private TableLayoutPanel BuildPomodoroCard()
    {
        var card = MakeCardContainer(4);
        for (int i = 0; i < 4; i++)
            card.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));

        _numWorkMin = AddPomodoroRow(card, 0, "Длительность работы (мин):", _pomodoroWorkMin, 1, 480);
        _numBreakMin = AddPomodoroRow(card, 1, "Короткий перерыв (мин):", _pomodoroBreakMin, 1, 120);
        _numLongBreakMin = AddPomodoroRow(card, 2, "Длинный перерыв (мин):", _pomodoroLongBreakMin, 1, 240);
        _numCyclesUntilLong = AddPomodoroRow(card, 3, "Циклов до длинного перерыва:", _pomodoroCyclesUntilLong, 1, 20);

        return card;
    }

    private NumericUpDown AddPomodoroRow(TableLayoutPanel card, int row, string label, int value, int min, int max)
    {
        var lbl = new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10F),
            ForeColor = COLOR_TEXT_PRIMARY,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 4, 12, 4)
        };

        var num = new NumericUpDown
        {
            Dock = DockStyle.Fill,
            Minimum = min,
            Maximum = max,
            DecimalPlaces = 0,
            Increment = 1M,
            BackColor = COLOR_BACKGROUND,
            ForeColor = COLOR_TEXT_PRIMARY,
            Font = new Font("Segoe UI", 10F),
            Value = Math.Max(min, Math.Min(max, value)),
            Margin = new Padding(0, 4, 0, 4)
        };
        num.ValueChanged += (_, _) =>
        {

        };

        card.Controls.Add(lbl, 0, row);
        card.Controls.Add(num, 1, row);
        return num;
    }

    // Виртуальное время 
    private TableLayoutPanel BuildVirtualTimeCard()
    {
        var card = MakeCardContainer(2);
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));

        var lblSpacer1 = new Label
        {
            Text = "Включить виртуальное время:",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10F),
            ForeColor = COLOR_TEXT_PRIMARY,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 4, 12, 4)
        };

        _chkVirtualTime = new CheckBox
        {
            Dock = DockStyle.Fill,
            Checked = _virtualTimeEnabled,
            BackColor = COLOR_CARD,
            ForeColor = COLOR_TEXT_PRIMARY,
            Font = new Font("Segoe UI", 10F),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 4, 0, 4)
        };
        _chkVirtualTime.CheckedChanged += (_, _) => _virtualTimeEnabled = _chkVirtualTime.Checked;

        var lblRatio = new Label
        {
            Text = "Коэффициент ускорения:",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10F),
            ForeColor = COLOR_TEXT_PRIMARY,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 4, 12, 4)
        };

        _numVirtualRatio = new NumericUpDown
        {
            Dock = DockStyle.Fill,
            Minimum = 0.01M,
            Maximum = 100M,
            DecimalPlaces = 2,
            Increment = 0.5M,
            BackColor = COLOR_BACKGROUND,
            ForeColor = COLOR_TEXT_PRIMARY,
            Font = new Font("Segoe UI", 10F),
            Value = (decimal)Math.Max(0.01, _virtualTimeRatio),
            Margin = new Padding(0, 4, 0, 4)
        };
        _numVirtualRatio.ValueChanged += (_, _) => _virtualTimeRatio = (double)_numVirtualRatio.Value;

        card.Controls.Add(lblSpacer1, 0, 0);
        card.Controls.Add(_chkVirtualTime, 1, 0);
        card.Controls.Add(lblRatio, 0, 1);
        card.Controls.Add(_numVirtualRatio, 1, 1);

        return card;
    }

    // Плавающий виджет (мини-таймер) 
    private TableLayoutPanel BuildMiniTimerCard()
    {
        var card = MakeCardContainer(1);
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));

        var lblMiniTimer = new Label
        {
            Text = "Показывать плавающий мини-таймер:",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10F),
            ForeColor = COLOR_TEXT_PRIMARY,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 4, 12, 4)
        };

        _chkMiniTimer = new CheckBox
        {
            Dock = DockStyle.Fill,
            Checked = _miniTimerEnabled,
            BackColor = COLOR_CARD,
            ForeColor = COLOR_TEXT_PRIMARY,
            Font = new Font("Segoe UI", 10F),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 4, 0, 4)
        };
        _chkMiniTimer.CheckedChanged += (_, _) => _miniTimerEnabled = _chkMiniTimer.Checked;

        card.Controls.Add(lblMiniTimer, 0, 0);
        card.Controls.Add(_chkMiniTimer, 1, 0);

        return card;
    }

    // Категории CRUD 
    private TableLayoutPanel BuildCategoriesCard()
    {
        var card = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            BackColor = COLOR_CARD,
            Padding = new Padding(16, 14, 16, 14),
            Margin = new Padding(0)
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));  // 0: "Существующие"
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 150F)); // 1: список
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));  // 2: "Добавить"
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));  // 3: ввод + кнопка
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));  // 4: выбранный цвет
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));  // 5: "Палитра"
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 180F)); // 6: палитра

        // "Существующие категории"
        card.Controls.Add(new Label
        {
            Text = "Существующие категории",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = COLOR_TEXT_PRIMARY,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            Margin = new Padding(0, 0, 0, 4)
        }, 0, 0);

        // Список существующих категорий
        _categoriesList = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = COLOR_BACKGROUND,
            Padding = new Padding(6, 6, 6, 6),
            Margin = new Padding(0, 0, 0, 8),
            BorderStyle = BorderStyle.FixedSingle
        };
        _categoriesList.ClientSizeChanged += (_, _) => UpdateCategoryRowWidths();
        card.Controls.Add(_categoriesList, 0, 1);

        // "Добавить новую категорию"
        card.Controls.Add(new Label
        {
            Text = "Добавить новую категорию",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = COLOR_TEXT_PRIMARY,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            Margin = new Padding(0, 0, 0, 4)
        }, 0, 2);

        // Строка ввода + кнопка "Добавить"
        var addRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = COLOR_CARD,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        addRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        addRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));
        addRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        _txtNewCategoryName = new TextBox
        {
            Dock = DockStyle.Fill,
            BackColor = COLOR_BACKGROUND,
            ForeColor = COLOR_TEXT_PRIMARY,
            Font = new Font("Segoe UI", 10F),
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 4, 8, 4),
            PlaceholderText = "Название категории"
        };

        var btnAddCategory = new Button
        {
            Text = "+ Добавить",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            BackColor = COLOR_ACCENT,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 4, 0, 4)
        };
        btnAddCategory.Click += (_, _) => OnAddCategory();

        addRow.Controls.Add(_txtNewCategoryName, 0, 0);
        addRow.Controls.Add(btnAddCategory, 1, 0);
        card.Controls.Add(addRow, 0, 3);

        // Превью выбранного цвета: dot + hex 
        var selectedRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = COLOR_CARD,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        selectedRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40F));
        selectedRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        selectedRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        _selectedColorDot = new Label
        {
            Size = new Size(24, 24),
            BackColor = ColorTranslator.FromHtml(_selectedColorHex),
            Margin = new Padding(0, 4, 8, 4),
            BorderStyle = BorderStyle.FixedSingle,
            Dock = DockStyle.Fill
        };

        _lblSelectedColorName = new Label
        {
            Text = _selectedColorHex,
            AutoSize = false,
            Font = new Font("Consolas", 10F),
            ForeColor = COLOR_TEXT_SECONDARY,
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 4)
        };

        selectedRow.Controls.Add(_selectedColorDot, 0, 0);
        selectedRow.Controls.Add(_lblSelectedColorName, 1, 0);
        card.Controls.Add(selectedRow, 0, 4);

        // Палитра
        card.Controls.Add(new Label
        {
            Text = "Палитра (50 цветов) — кликните, чтобы выбрать цвет для новой категории",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = COLOR_TEXT_SECONDARY,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            Margin = new Padding(0, 0, 0, 4)
        }, 0, 5);

        // Палитра 50 цветов 
        _palettePanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            AutoScroll = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = COLOR_BACKGROUND,
            Padding = new Padding(6, 6, 6, 6),
            Margin = new Padding(0),
            BorderStyle = BorderStyle.FixedSingle
        };
        BuildPalette();
        card.Controls.Add(_palettePanel, 0, 6);

        return card;
    }

    // Палитра 50 цветов 
    private void BuildPalette()
    {
        if (_palettePanel == null) return;
        _palettePanel.Controls.Clear();
        _paletteDots.Clear();

        foreach (var hex in Config.ColorPalette)
        {
            var dot = new ColorDot(hex)
            {
                Size = new Size(26, 26),
                Margin = new Padding(3, 3, 3, 3)
            };
            dot.Click += (_, _) => SelectColor(hex);
            _paletteDots.Add(dot);
            _palettePanel.Controls.Add(dot);
        }
    }

    private void SelectColor(string hex)
    {
        _selectedColorHex = hex;
        if (_lblSelectedColorName != null)
            _lblSelectedColorName.Text = hex;
        if (_selectedColorDot != null)
        {
            try { _selectedColorDot.BackColor = ColorTranslator.FromHtml(hex); }
            catch { /* игнорируем */ }
        }

        foreach (var dot in _paletteDots)
            dot.IsSelected = string.Equals(dot.Hex, hex, StringComparison.OrdinalIgnoreCase);
    }

    // CRUD категорий 
    private void RefreshCategoriesList()
    {
        if (_categoriesList == null) return;
        _categoriesList.SuspendLayout();
        _categoriesList.Controls.Clear();

        if (_editedCategories.Count == 0)
        {
            _categoriesList.Controls.Add(new Label
            {
                Text = "Нет категорий. Добавьте новую ниже.",
                AutoSize = true,
                ForeColor = COLOR_TEXT_SECONDARY,
                Font = new Font("Segoe UI", 9F, FontStyle.Italic),
                Margin = new Padding(4, 4, 4, 4)
            });
        }
        else
        {
            foreach (var kv in _editedCategories
                         .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
            {
                _categoriesList.Controls.Add(CreateCategoryRow(kv.Key, kv.Value));
            }
        }
        _categoriesList.ResumeLayout(true);
        UpdateCategoryRowWidths();
    }

    private void UpdateCategoryRowWidths()
    {
        if (_categoriesList == null) return;
        int w = Math.Max(100, _categoriesList.ClientSize.Width - 8);
        foreach (Control c in _categoriesList.Controls)
        {
            if (c is Panel row)
                row.Width = w;
        }
    }

    private Panel CreateCategoryRow(string name, string colorHex)
    {
        var row = new Panel
        {
            Height = 32,
            Width = Math.Max(100, _categoriesList!.ClientSize.Width - 8),
            BackColor = COLOR_SURFACE,
            Margin = new Padding(0, 0, 0, 4),
            Padding = new Padding(0)
        };

        Color catColor;
        try { catColor = ColorTranslator.FromHtml(colorHex); }
        catch { catColor = Color.Gray; }

        var tlp = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = COLOR_SURFACE,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 4F));
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36F));
        tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var colorBar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = catColor,
            Margin = new Padding(0)
        };

        var lblName = new Label
        {
            Dock = DockStyle.Fill,
            Text = name,
            Font = new Font("Segoe UI", 10F),
            ForeColor = COLOR_TEXT_PRIMARY,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 6, 0),
            Margin = new Padding(0)
        };

        var btnRemove = new Button
        {
            Text = "✕",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            BackColor = COLOR_DANGER,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(4, 4, 4, 4),
            Tag = name
        };
        btnRemove.Click += (_, _) =>
        {
            if (btnRemove.Tag is string catName)
                OnRemoveCategory(catName);
        };

        tlp.Controls.Add(colorBar, 0, 0);
        tlp.Controls.Add(lblName, 1, 0);
        tlp.Controls.Add(btnRemove, 2, 0);

        row.Controls.Add(tlp);

        return row;
    }

    private void OnAddCategory()
    {
        if (_txtNewCategoryName == null) return;

        string name = _txtNewCategoryName.Text.Trim();

        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show(this, "Введите название категории.",
                "Настройки", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _txtNewCategoryName.Focus();
            return;
        }

        bool exists = _editedCategories.Keys
            .Any(k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase));
        if (exists)
        {
            MessageBox.Show(this, $"Категория «{name}» уже существует.",
                "Настройки", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _txtNewCategoryName.Focus();
            _txtNewCategoryName.SelectAll();
            return;
        }

        _editedCategories[name] = _selectedColorHex;
        _txtNewCategoryName.Text = "";
        RefreshCategoriesList();
    }

    private void OnRemoveCategory(string name)
    {
        var result = MessageBox.Show(this,
            $"Удалить категорию «{name}»?\n" +
            "Задачи с этой категорией останутся, но их цвет станет серым.",
            "Настройки", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (result != DialogResult.Yes) return;

        string? keyToRemove = _editedCategories.Keys
            .FirstOrDefault(k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase));
        if (keyToRemove != null)
            _editedCategories.Remove(keyToRemove);
        RefreshCategoriesList();
    }

    // Панель кнопок Save / Cancel 
    private Panel BuildButtonPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = COLOR_SURFACE,
            Padding = new Padding(20, 10, 20, 10)
        };

        var btnRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = COLOR_SURFACE,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        btnRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        btnRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160F));
        btnRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
        btnRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var btnCancel = new Button
        {
            Text = "Отмена",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(70, 70, 70),
            ForeColor = COLOR_TEXT_PRIMARY,
            Font = new Font("Segoe UI", 9F),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 10, 0),
            DialogResult = DialogResult.Cancel
        };
        btnCancel.Click += (_, _) => OnCancel();

        var btnSave = new Button
        {
            Text = "💾 Сохранить",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            BackColor = COLOR_ACCENT,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(10, 0, 0, 0),
            DialogResult = DialogResult.OK
        };
        btnSave.Click += (_, _) => OnSave();

        btnRow.Controls.Add(new Panel { BackColor = COLOR_SURFACE, Dock = DockStyle.Fill }, 0, 0);
        btnRow.Controls.Add(btnCancel, 1, 0);
        btnRow.Controls.Add(btnSave, 2, 0);

        panel.Controls.Add(btnRow);
        return panel;
    }

    // Save / Cancel 
    private void OnSave()
    {

        if (_numHourlyRate != null) _hourlyRate = (double)_numHourlyRate.Value;
        if (_numWorkMin != null) _pomodoroWorkMin = (int)_numWorkMin.Value;
        if (_numBreakMin != null) _pomodoroBreakMin = (int)_numBreakMin.Value;
        if (_numLongBreakMin != null) _pomodoroLongBreakMin = (int)_numLongBreakMin.Value;
        if (_numCyclesUntilLong != null) _pomodoroCyclesUntilLong = (int)_numCyclesUntilLong.Value;
        if (_chkVirtualTime != null) _virtualTimeEnabled = _chkVirtualTime.Checked;
        if (_numVirtualRatio != null) _virtualTimeRatio = (double)_numVirtualRatio.Value;
        if (_chkMiniTimer != null) _miniTimerEnabled = _chkMiniTimer.Checked;
        if (_txtCurrency != null) _currency = _txtCurrency.Text;

        // Валидация
        if (_hourlyRate < 0)
        {
            MessageBox.Show(this, "Ставка за час не может быть отрицательной.",
                "Настройки", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string currency = (_currency ?? "").Trim();
        if (string.IsNullOrEmpty(currency))
        {
            MessageBox.Show(this, "Укажите символ валюты (например, ₽, $, €).",
                "Настройки", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtCurrency?.Focus();
            return;
        }

        try
        {
            _settings.Set("currency", currency);
            _settings.Set("hourly_rate", _hourlyRate.ToString(System.Globalization.CultureInfo.InvariantCulture));
            _settings.Set("pomodoro_work_min", _pomodoroWorkMin);
            _settings.Set("pomodoro_break_min", _pomodoroBreakMin);
            _settings.Set("pomodoro_long_break_min", _pomodoroLongBreakMin);
            _settings.Set("pomodoro_cycles_until_long", _pomodoroCyclesUntilLong);
            _settings.Set("virtual_time_enabled", _virtualTimeEnabled);
            _settings.Set("virtual_time_ratio", _virtualTimeRatio.ToString(System.Globalization.CultureInfo.InvariantCulture));
            _settings.Set("mini_timer_enabled", _miniTimerEnabled);

            string catsJson = System.Text.Json.JsonSerializer.Serialize(_editedCategories);
            _settings.Set("categories", catsJson);

            _settings.Save();

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Не удалось сохранить настройки:\n" + ex.Message,
                "Настройки", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnCancel()
    {
        this.DialogResult = DialogResult.Cancel;
        this.Close();
    }
}

// ColorDot — цветной квадрат палитры
public class ColorDot : Label
{
    public string Hex { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            Invalidate();
        }
    }

    public ColorDot(string hex)
    {
        Hex = hex;
        try { this.BackColor = ColorTranslator.FromHtml(hex); }
        catch { this.BackColor = Color.Gray; }
        this.DoubleBuffered = true;
        this.Cursor = Cursors.Hand;
        this.TextAlign = ContentAlignment.MiddleCenter;
        this.Text = "";
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (var brush = new SolidBrush(this.BackColor))
            g.FillRectangle(brush, 0, 0, this.Width, this.Height);

        if (_isSelected)
        {
            using var pen = new Pen(Color.White, 2);
            var r = new Rectangle(1, 1, this.Width - 3, this.Height - 3);
            g.DrawRectangle(pen, r);
        }
    }
}
