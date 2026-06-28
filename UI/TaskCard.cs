using System;
using System.Drawing;
using System.Windows.Forms;
using TimeFlow;

namespace TimeFlow.UI;

public class TaskCard : UserControl 
{
    // Цвета темы
    private static readonly Color COLOR_SURFACE = Color.FromArgb(45, 45, 45);
    private static readonly Color COLOR_TEXT_PRIMARY = Color.FromArgb(220, 220, 220);
    private static readonly Color COLOR_ACCENT = Color.FromArgb(76, 175, 80);
    
    // Элементы layout 
    private Panel? _catBar;
    private TableLayoutPanel? _centerPanel;
    private Label? _lblName;
    private Label? _lblTime;
    private FlowLayoutPanel? _btnPanel;
    private Button? _btnStart;
    private Button? _btnStop;
    private Button? _btnDone;
    private Button? _btnDelete;

    // Модель и часы
    private TaskItem? _task;
    private VirtualClock? _clock;

    // Сигналы действий
    public event EventHandler<TaskItem>? StartClicked;
    public event EventHandler<TaskItem>? StopClicked;
    public event EventHandler<TaskItem>? DoneClicked;
    public event EventHandler<TaskItem>? DeleteClicked;

    public TaskCard()
    {

        InitializeLayout();
        ApplyTheme();
        
        // Подписка на события кнопок
        if (_btnStart != null) _btnStart.Click += (_, _) => StartClicked?.Invoke(this, _task!);
        if (_btnStop != null) _btnStop.Click += (_, _) => StopClicked?.Invoke(this, _task!);
        if (_btnDone != null) _btnDone.Click += (_, _) => DoneClicked?.Invoke(this, _task!);
        if (_btnDelete != null) _btnDelete.Click += (_, _) => DeleteClicked?.Invoke(this, _task!);
    }

    private void InitializeLayout()
    {
        this.Dock = DockStyle.Top;
        this.Height = 64;
        this.MinimumSize = new Size(0, 64);
        this.Padding = new Padding(0);
        this.DoubleBuffered = true;

        // 1. CatBar
        _catBar = new Panel { Dock = DockStyle.Left, Width = 8, BackColor = Color.Gray };

        // 2. Центр
        _centerPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
            BackColor = COLOR_SURFACE, Padding = new Padding(12, 0, 12, 0)
        };
        _centerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));
        _centerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));

        _lblName = new Label
        {
            AutoSize = false, Dock = DockStyle.Fill, ForeColor = COLOR_TEXT_PRIMARY,
            Font = new Font("Segoe UI", 11, FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(0, 0, 10, 0)
        };

        _lblTime = new Label
        {
            AutoSize = false, Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(150, 150, 150),
            Font = new Font("Consolas", 10, FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleRight
        };

        _centerPanel.Controls.Add(_lblName, 0, 0);
        _centerPanel.Controls.Add(_lblTime, 1, 0);

        // 3. Кнопки
        _btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Right, Width = 190, BackColor = COLOR_SURFACE,
            FlowDirection = FlowDirection.LeftToRight, WrapContents = false,
            Padding = new Padding(8, 14, 8, 14)
        };

        _btnStart = CreateBtn("▶", COLOR_ACCENT);
        _btnStop = CreateBtn("⏸", Color.FromArgb(255, 152, 0));
        _btnDone = CreateBtn("✓", Color.FromArgb(33, 150, 243));
        _btnDelete = CreateBtn("✕", Color.FromArgb(244, 67, 54));

        _btnPanel.Controls.AddRange(new Control[] { _btnStart!, _btnStop!, _btnDone!, _btnDelete! });

        // Сборка
        this.Controls.Add(_centerPanel);
        this.Controls.Add(_btnPanel);
        this.Controls.Add(_catBar);
        _btnPanel.BringToFront();
        _catBar.BringToFront();
    }

    private Button CreateBtn(string text, Color color)
    {
        return new Button
        {
            Text = text, Width = 36, Height = 36, FlatStyle = FlatStyle.Flat,
            BackColor = color, ForeColor = Color.White,
            Font = new Font("Segoe UI Symbol", 12, FontStyle.Bold),
            Margin = new Padding(3, 0, 3, 0), Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
    }

    private void ApplyTheme() => this.BackColor = COLOR_SURFACE;

    public void RefreshCard(TaskItem task, VirtualClock? clock = null, string? categoryColorHex = null)
    {
        _task = task;
        _clock = clock;
        if (task == null) return;

        _lblName!.Text = task.Name;
        double secs = task.ElapsedSeconds(clock);
        TimeSpan ts = TimeSpan.FromSeconds((int)secs);
        _lblTime!.Text = $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";

        // Цвет категории
        if (!string.IsNullOrEmpty(categoryColorHex))
        {
            try { _catBar!.BackColor = ColorTranslator.FromHtml(categoryColorHex); }
            catch { _catBar!.BackColor = Color.Gray; }
        }
        else
        {
            _catBar!.BackColor = GetFallbackColor(task.Category);
        }

        // Логика кнопок
        bool isRunning = task.Running;
        bool isDone = task.Done;

        _btnStart!.Visible = !isRunning && !isDone;
        _btnStop!.Visible = isRunning;
        _btnDone!.Enabled = !isRunning && !isDone && secs > 0;
        _btnDone!.Visible = !isDone;
        _btnDelete!.Visible = true;

        // Перерисовка рамки
        this.Invalidate(); 
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_task != null && _task.Running)
        {
            using var pen = new Pen(COLOR_ACCENT, 2);
            e.Graphics.DrawRectangle(pen, 1, 1, this.Width - 3, this.Height - 3);
        }
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
	
	// Свойство чтобы UpdateTimers работал корректно без перебора каждый тик
	public TaskItem? CurrentTask => _task;
}