using System;
using System.Drawing;
using System.Windows.Forms;
using TimeFlow;

namespace TimeFlow.UI;

public class TaskCard : UserControl
{

    private static readonly Color COLOR_PANEL = Color.FromArgb(30, 30, 30);
    private static readonly Color COLOR_BORDER = Color.FromArgb(60, 60, 60);
    private static readonly Color COLOR_CARD_INNER = Color.FromArgb(74, 74, 74);
    private static readonly Color COLOR_TEXT_PRIMARY = Color.FromArgb(220, 220, 220);
    private static readonly Color COLOR_TEXT_SECONDARY = Color.FromArgb(170, 170, 170);

    private static readonly Color COLOR_ACCENT = Color.FromArgb(76, 175, 80);
    private static readonly Color COLOR_RUN_BG_INNER = Color.FromArgb(122, 199, 130);
    private static readonly Color COLOR_RUN_ORANGE = Color.FromArgb(255, 152, 0);
    private static readonly Color COLOR_DONE_BLUE = Color.FromArgb(33, 150, 243);
    private static readonly Color COLOR_DEL_RED = Color.FromArgb(244, 67, 54);

    // Элементы layout
    private TableLayoutPanel? _layout;
    private Panel? _catBar;
    private Label? _lblName;
    private Label? _lblCategory;   
    private TableLayoutPanel? _namePanel;  
    private Label? _lblTime;
    private FlowLayoutPanel? _btnPanel;
    private Button? _btnRun;     // ▶ / ⏸ — переключатель Start/Stop
    private Button? _btnDone;    // ✓
    private Button? _btnDelete;  // ✕

    // Модель
    private TaskItem? _task;

    // авто-resize ширины
    private FlowLayoutPanel? _parentFlow;

    // События
    public event EventHandler<TaskItem>? StartClicked;
    public event EventHandler<TaskItem>? StopClicked;
    public event EventHandler<TaskItem>? DoneClicked;
    public event EventHandler<TaskItem>? DeleteClicked;

    public TaskItem? CurrentTask => _task;

    public TaskCard()
    {
        InitializeLayout();
    }

    private void InitializeLayout()
    {
        // === Внешний UserControl ===
        this.Height = 84;
        this.MinimumSize = new Size(0, 84);
        this.MaximumSize = new Size(int.MaxValue, 84);
        this.Margin = new Padding(0, 4, 0, 4);
        this.Padding = new Padding(0);
        this.BackColor = COLOR_BORDER;  
        this.DoubleBuffered = true;

        // === Внутренний TableLayoutPanel ===
        _layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = COLOR_BORDER,
            Padding = new Padding(2),
            Margin = new Padding(0)
        };
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 8F));
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
        _layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        _catBar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Gray,
            Margin = new Padding(0)
        };

        _lblName = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            ForeColor = COLOR_TEXT_PRIMARY,
            Font = new Font("Segoe UI", 11, FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 10, 0),
            Margin = new Padding(0),
            BackColor = COLOR_CARD_INNER,
            Text = "Задача"
        };

        _lblCategory = new Label
        {
            AutoSize = true,
            BackColor = Color.Gray,            
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            Text = "  Категория  ",
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(10, 0, 0, 4),
            Padding = new Padding(0)
        };

        _namePanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = COLOR_CARD_INNER,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        _namePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 55F));
        _namePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 45F));
        _namePanel.Controls.Add(_lblName, 0, 0);
        _namePanel.Controls.Add(_lblCategory, 0, 1);

        _lblTime = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            ForeColor = COLOR_TEXT_SECONDARY,
            Font = new Font("Consolas", 10F, FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(0, 0, 8, 0),
            Margin = new Padding(0),
            BackColor = COLOR_CARD_INNER,
            Text = "00:00:00",
            AutoEllipsis = false
        };

        _btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = COLOR_CARD_INNER,
            Padding = new Padding(4, 14, 4, 14),
            Margin = new Padding(0)
        };

        _btnRun = CreateBtn("▶", COLOR_ACCENT);
        _btnDone = CreateBtn("✓", COLOR_DONE_BLUE);
        _btnDelete = CreateBtn("✕", COLOR_DEL_RED);

        _btnRun.Click += (_, _) =>
        {
            if (_task == null) return;
            if (_task.Running) StopClicked?.Invoke(this, _task);
            else StartClicked?.Invoke(this, _task);
        };
        _btnDone.Click += (_, _) => { if (_task != null) DoneClicked?.Invoke(this, _task); };
        _btnDelete.Click += (_, _) => { if (_task != null) DeleteClicked?.Invoke(this, _task); };

        _btnPanel.Controls.Add(_btnRun);
        _btnPanel.Controls.Add(_btnDone);
        _btnPanel.Controls.Add(_btnDelete);

        _layout.Controls.Add(_catBar, 0, 0);
        _layout.Controls.Add(_namePanel, 1, 0);  // имя + chip вместо одного _lblName
        _layout.Controls.Add(_lblTime, 2, 0);
        _layout.Controls.Add(_btnPanel, 3, 0);

        this.Controls.Add(_layout);

        // Авто-resize ширины
        this.ParentChanged += TaskCard_ParentChanged;
    }

    private void TaskCard_ParentChanged(object? sender, EventArgs e)
    {

        if (_parentFlow != null)
        {
            _parentFlow.ClientSizeChanged -= ParentFlow_ClientSizeChanged;
            _parentFlow = null;
        }

        if (Parent is FlowLayoutPanel flp)
        {
            _parentFlow = flp;
            _parentFlow.ClientSizeChanged += ParentFlow_ClientSizeChanged;
            UpdateWidth();
        }
    }

    private void ParentFlow_ClientSizeChanged(object? sender, EventArgs e) => UpdateWidth();

    private void UpdateWidth()
    {
        if (_parentFlow == null) return;
        int w = _parentFlow.ClientSize.Width;
        if (_parentFlow.VerticalScroll.Visible)
            w -= SystemInformation.VerticalScrollBarWidth;
        if (w < 20) w = 20;
        this.Width = w;
    }

    private Button CreateBtn(string text, Color color)
    {
        return new Button
        {
            Text = text,
            Width = 36,
            Height = 36,
            FlatStyle = FlatStyle.Flat,
            BackColor = color,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Symbol", 12, FontStyle.Bold),
            Margin = new Padding(3, 0, 3, 0),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
    }

    public void RefreshCard(TaskItem task, VirtualClock? clock = null, string? categoryColorHex = null)
    {
        _task = task;
        if (task == null) return;
        if (_lblName == null || _lblTime == null || _catBar == null) return;
        if (_lblCategory == null || _namePanel == null) return;
        if (_btnRun == null || _btnDone == null || _btnDelete == null || _layout == null) return;

        // --- Текст ---
        _lblName.Text = task.Name;
        double secs = task.ElapsedSeconds(clock);
        TimeSpan ts = TimeSpan.FromSeconds((int)secs);
        _lblTime.Text = $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";

        // --- Цвет левой полоски (по категории) ---
        Color catColor;
        if (!string.IsNullOrEmpty(categoryColorHex))
        {
            try { catColor = ColorTranslator.FromHtml(categoryColorHex); }
            catch { catColor = GetFallbackColor(task.Category); }
        }
        else
        {
            catColor = GetFallbackColor(task.Category);
        }
        _catBar.BackColor = catColor;

        // --- Цветная категория под названием ---
        _lblCategory.Text = $"  {task.Category}  ";
        _lblCategory.BackColor = catColor;

        // --- Состояние ---
        bool isRunning = task.Running;
        bool isDone = task.Done;

        // Зелёная подсветка активной задачи
        Color borderColor = isRunning ? COLOR_ACCENT : COLOR_BORDER;
        Color innerColor  = isRunning ? COLOR_RUN_BG_INNER : COLOR_CARD_INNER;

        _layout.BackColor = borderColor;
        _lblName.BackColor = innerColor;
        _namePanel.BackColor = innerColor;
        _lblTime.BackColor = innerColor;
        if (_btnPanel != null) _btnPanel.BackColor = innerColor;

        _lblCategory.BackColor = catColor;

        _lblTime.ForeColor = isRunning ? Color.FromArgb(20, 60, 25) : COLOR_TEXT_SECONDARY;

        // --- Кнопка ▶/⏸ ---
        _btnRun.Visible = !isDone;
        if (isRunning)
        {
            _btnRun.Text = "⏸";
            _btnRun.BackColor = COLOR_RUN_ORANGE;
        }
        else
        {
            _btnRun.Text = "▶";
            _btnRun.BackColor = COLOR_ACCENT;
        }

        // --- Кнопка ✓ ---
        _btnDone.Visible = !isDone;
        _btnDone.Enabled = !isRunning && secs > 0;

        // --- Кнопка ✕ ---
        _btnDelete.Visible = true;
    }

    private Color GetFallbackColor(string? category)
    {
        return category switch
        {
            "Работа" => Color.FromArgb(230, 126, 34),
            "Учёба" => Color.FromArgb(74, 144, 217),
            "Отдых" => Color.FromArgb(39, 174, 96),
            _ => Color.Gray
        };
    }
}
