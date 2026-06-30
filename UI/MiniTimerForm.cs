using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using TimeFlow;

namespace TimeFlow.UI;

public class MiniTimerForm : Form
{
    private static readonly Color COLOR_BACKGROUND = Color.FromArgb(30, 30, 30);
    private static readonly Color COLOR_BORDER = Color.FromArgb(60, 60, 60);
    private static readonly Color COLOR_CARD_INNER = Color.FromArgb(74, 74, 74);
    private static readonly Color COLOR_TEXT_PRIMARY = Color.FromArgb(220, 220, 220);
    private static readonly Color COLOR_TEXT_SECONDARY = Color.FromArgb(170, 170, 170);
    private static readonly Color COLOR_ACCENT = Color.FromArgb(76, 175, 80);
    private static readonly Color COLOR_RUN_ORANGE = Color.FromArgb(255, 152, 0);
    private static readonly Color COLOR_DONE_BLUE = Color.FromArgb(33, 150, 243);
    private static readonly Color COLOR_DEL_RED = Color.FromArgb(244, 67, 54);

    private const int FORM_WIDTH = 400;
    private const int FORM_HEIGHT = 120;
    private const int CARD_WIDTH = 396;   
    private const int CARD_HEIGHT = 110;  

    private readonly SettingsStore _settings;
    private readonly VirtualClock _clock;

    private readonly TableLayoutPanel _cardsLayout;

    private readonly Label _emptyState;

    private readonly Dictionary<string, Color> _catColors = new();

    private bool _dragging = false;
    private Point _dragOffset = Point.Empty;

    [DllImport("user32.dll")]
    private static extern bool ShowScrollBar(IntPtr hWnd, int bar, bool show);
    private const int SB_HORZ = 0;
    private const int SB_VERT = 1;

    public event EventHandler<TaskItem>? Clicked;

    public event EventHandler<TaskItem>? StartClicked;

    public event EventHandler<TaskItem>? StopClicked;

    public event EventHandler<TaskItem>? DoneClicked;

    public event EventHandler<TaskItem>? DeleteClicked;

    public MiniTimerForm(SettingsStore settings, VirtualClock clock)
    {
        _settings = settings;
        _clock = clock;

        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;

        Size = new Size(FORM_WIDTH, FORM_HEIGHT);
        MinimumSize = new Size(FORM_WIDTH, FORM_HEIGHT);
        MaximumSize = new Size(FORM_WIDTH, FORM_HEIGHT);

        BackColor = COLOR_BACKGROUND;
        ForeColor = COLOR_TEXT_PRIMARY;
        Font = new Font("Segoe UI", 9F);
        Padding = new Padding(0);
        DoubleBuffered = true;

        _cardsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 0,           
            AutoScroll = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = COLOR_BACKGROUND,
            Padding = new Padding(2),
            Margin = new Padding(0)
        };

        _cardsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        Controls.Add(_cardsLayout);

        _emptyState = new Label
        {
            Text = "Активных задач нет.\nАктивируйте задачу для работы виджета",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10F, FontStyle.Regular),
            ForeColor = COLOR_TEXT_SECONDARY,
            BackColor = COLOR_BACKGROUND,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoSize = false,
            Margin = new Padding(0),
            Padding = new Padding(8)
        };

        Controls.Add(_emptyState);
        _emptyState.BringToFront();

        HookDragEvents(_cardsLayout);
        HookDragEvents(_emptyState);

        _cardsLayout.Layout += (_, _) => HideHorizontalScrollbar();
        _cardsLayout.Scroll += (_, _) => HideHorizontalScrollbar();

        UpdateEmptyState(true);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_TOPMOST = 0x00000008;
            const int WS_EX_TOOLWINDOW = 0x00000080;
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOPMOST | WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    private void HookDragEvents(Control parent)
    {
        parent.MouseDown += OnDragMouseDown;
        parent.MouseMove += OnDragMouseMove;
        parent.MouseUp   += OnDragMouseUp;

        foreach (Control c in parent.Controls)
            HookDragEvents(c);
    }

    private void OnDragMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        if (sender is Button) return;

        _dragging = true;
        Point screen = Cursor.Position;
        _dragOffset = new Point(screen.X - this.Left, screen.Y - this.Top);
    }

    private void OnDragMouseMove(object? sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        if (sender is Button) return;

        Point screen = Cursor.Position;
        this.Left = screen.X - _dragOffset.X;
        this.Top  = screen.Y - _dragOffset.Y;
    }

    private void OnDragMouseUp(object? sender, MouseEventArgs e)
    {
        _dragging = false;
    }

    private void HideHorizontalScrollbar()
    {
        if (_cardsLayout.IsHandleCreated)
        {
            ShowScrollBar(_cardsLayout.Handle, SB_HORZ, false);
        }
    }

    public void RefreshTasks(IEnumerable<TaskItem> tasks)
    {
        var running = tasks.Where(t => t.Running).ToList();

        var existingCards = new List<(MiniCard card, int rowIndex)>();
        for (int i = 0; i < _cardsLayout.RowCount; i++)
        {
            if (_cardsLayout.GetControlFromPosition(0, i) is MiniCard card)
                existingCards.Add((card, i));
        }

        var rowsToRemove = new List<int>();
        foreach (var (card, rowIndex) in existingCards)
        {
            if (card.CurrentTask != null && !running.Exists(t => t.Id == card.CurrentTask.Id))
                rowsToRemove.Add(rowIndex);
        }
        rowsToRemove.Sort((a, b) => b.CompareTo(a));
        foreach (var rowIndex in rowsToRemove)
        {
            var ctrl = _cardsLayout.GetControlFromPosition(0, rowIndex);
            if (ctrl is MiniCard card)
            {
                _cardsLayout.Controls.Remove(card);
                card.Dispose();
            }
            _cardsLayout.RowStyles.RemoveAt(rowIndex);
            _cardsLayout.RowCount--;
        }

        var currentCardById = new Dictionary<string, MiniCard>();
        for (int i = 0; i < _cardsLayout.RowCount; i++)
        {
            if (_cardsLayout.GetControlFromPosition(0, i) is MiniCard card && card.CurrentTask != null)
                currentCardById[card.CurrentTask.Id] = card;
        }

        for (int i = 0; i < running.Count; i++)
        {
            var task = running[i];
            if (currentCardById.TryGetValue(task.Id, out var card))
            {
                card.RefreshCard(task, _clock, GetCatColor(task.Category));
            }
            else
            {
               
                AddCardAt(task, i);
            }
        }

        UpdateEmptyState(running.Count == 0);
    }

    private void AddCardAt(TaskItem task, int index)
    {
        var card = new MiniCard(
            task,
            GetCatColor(task.Category),
            _clock,
            CARD_WIDTH,
            CARD_HEIGHT,
            OnCardClicked,
            OnCardStart,
            OnCardStop,
            OnCardDone,
            OnCardDelete);

        _cardsLayout.RowCount++;
        _cardsLayout.RowStyles.Insert(index, new RowStyle(SizeType.Absolute, CARD_HEIGHT));

        for (int i = _cardsLayout.RowCount - 1; i > index; i--)
        {
            var ctrl = _cardsLayout.GetControlFromPosition(0, i - 1);
            if (ctrl != null)
            {
                _cardsLayout.Controls.Remove(ctrl);
                _cardsLayout.Controls.Add(ctrl, 0, i);
            }
        }

        _cardsLayout.Controls.Add(card, 0, index);

        HookDragEvents(card);
    }

    private void UpdateEmptyState(bool isEmpty)
    {
        _emptyState.Visible = isEmpty;
        if (isEmpty)
            _emptyState.BringToFront();
    }

    private Color GetCatColor(string category)
    {
        if (_catColors.TryGetValue(category, out var c))
            return c;

        string hex = _settings.ColorFor(category);
        try { c = ColorTranslator.FromHtml(hex); }
        catch { c = Color.Gray; }
        _catColors[category] = c;
        return c;
    }

    private void OnCardClicked(TaskItem task) => Clicked?.Invoke(this, task);
    private void OnCardStart(TaskItem task)   => StartClicked?.Invoke(this, task);
    private void OnCardStop(TaskItem task)    => StopClicked?.Invoke(this, task);
    private void OnCardDone(TaskItem task)    => DoneClicked?.Invoke(this, task);
    private void OnCardDelete(TaskItem task)  => DeleteClicked?.Invoke(this, task);

    private class MiniCard : UserControl
    {
        private readonly TaskItem _task;
        private readonly VirtualClock _clock;
        private readonly Action<TaskItem> _onClicked;
        private readonly Action<TaskItem> _onStart;
        private readonly Action<TaskItem> _onStop;
        private readonly Action<TaskItem> _onDone;
        private readonly Action<TaskItem> _onDelete;

        private readonly TableLayoutPanel _layout;
        private readonly Panel _catBar;
        private readonly Label _lblName;
        private readonly Label _lblTime;
        private readonly Label _lblGoal;
        private readonly Label _lblCategory;
        private readonly Panel _btnPanel;
        private readonly Button _btnRun;
        private readonly Button _btnDone;
        private readonly Button _btnDelete;

        public TaskItem CurrentTask => _task;

        public MiniCard(
            TaskItem task,
            Color catColor,
            VirtualClock clock,
            int width,
            int height,
            Action<TaskItem> onClicked,
            Action<TaskItem> onStart,
            Action<TaskItem> onStop,
            Action<TaskItem> onDone,
            Action<TaskItem> onDelete)
        {
            _task = task;
            _clock = clock;
            _onClicked = onClicked;
            _onStart = onStart;
            _onStop = onStop;
            _onDone = onDone;
            _onDelete = onDelete;

            Width = width;
            Height = height;
            MaximumSize = new Size(width, 0);
            MinimumSize = new Size(0, height);
            Margin = new Padding(0);
            Padding = new Padding(0);
            BackColor = COLOR_BORDER;
            DoubleBuffered = true;
            Dock = DockStyle.Fill;

            _layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = COLOR_BORDER,
                Padding = new Padding(2),
                Margin = new Padding(0)
            };
            _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 6F));     
            _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));    
            _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50F));    
            _layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _catBar = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = catColor,
                Margin = new Padding(0)
            };

            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = COLOR_CARD_INNER,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));  
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));  
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));  
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));  

            _lblName = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                ForeColor = COLOR_TEXT_PRIMARY,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 4, 0),
                Margin = new Padding(0),
                BackColor = COLOR_CARD_INNER,
                Text = task.Name,
                AutoEllipsis = true
            };

            _lblTime = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                ForeColor = COLOR_TEXT_PRIMARY,
                Font = new Font("Consolas", 12F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 4, 0),
                Margin = new Padding(0),
                BackColor = COLOR_CARD_INNER,
                Text = "00:00:00"
            };

            _lblGoal = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                ForeColor = COLOR_TEXT_SECONDARY,
                Font = new Font("Consolas", 9F, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 4, 0),
                Margin = new Padding(0),
                BackColor = COLOR_CARD_INNER,
                Text = "Цель: 00:00:00"
            };

            _lblCategory = new Label
            {
                AutoSize = true,
                BackColor = catColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                Text = $"  {task.Category}  ",
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(8, 2, 0, 2),
                Padding = new Padding(0)
            };

            content.Controls.Add(_lblName, 0, 0);
            content.Controls.Add(_lblTime, 0, 1);
            content.Controls.Add(_lblGoal, 0, 2);
            content.Controls.Add(_lblCategory, 0, 3);

            _btnPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = COLOR_CARD_INNER,
                Padding = new Padding(4, 4, 4, 4),
                Margin = new Padding(0)
            };

            _btnRun    = CreateBtn("▶", COLOR_ACCENT);
            _btnDone   = CreateBtn("✓", COLOR_DONE_BLUE);
            _btnDelete = CreateBtn("✕", COLOR_DEL_RED);

            _btnRun.Dock    = DockStyle.Top;
            _btnDone.Dock   = DockStyle.Top;
            _btnDelete.Dock = DockStyle.Bottom;

            _btnRun.Click += (_, _) =>
            {
                if (_task.Running) _onStop(_task);
                else _onStart(_task);
            };
            _btnDone.Click   += (_, _) => _onDone(_task);
            _btnDelete.Click += (_, _) => _onDelete(_task);

            _btnPanel.Controls.Add(_btnDelete);  
            _btnPanel.Controls.Add(_btnRun);     
            _btnPanel.Controls.Add(_btnDone);    

            _layout.Controls.Add(_catBar, 0, 0);
            _layout.Controls.Add(content, 1, 0);
            _layout.Controls.Add(_btnPanel, 2, 0);

            this.Controls.Add(_layout);

            HookClick(this);

            RefreshCard(task, clock, catColor);
        }

        private void HookClick(Control parent)
        {
            if (parent is not Button)
            {
                parent.Click += (_, _) => _onClicked(_task);
            }
            foreach (Control c in parent.Controls)
                HookClick(c);
        }

        private Button CreateBtn(string text, Color color)
        {
            return new Button
            {
                Text = text,
                Height = 28,
                Width = 42,
                FlatStyle = FlatStyle.Flat,
                BackColor = color,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Symbol", 11F, FontStyle.Bold),
                Margin = new Padding(0, 2, 0, 2),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                FlatAppearance = { BorderSize = 0 }
            };
        }

        public void RefreshCard(TaskItem task, VirtualClock clock, Color catColor)
        {
            _lblName.Text = task.Name;
            double secs = task.ElapsedSeconds(clock);
            TimeSpan ts = TimeSpan.FromSeconds((int)secs);
            _lblTime.Text = $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";

            _lblGoal.Text = "Цель: " + FormatGoal(task.EstimatedSeconds);

            _lblCategory.Text = $"  {task.Category}  ";
            _lblCategory.BackColor = catColor;
            _catBar.BackColor = catColor;

            bool isRunning = task.Running;
            bool isDone = task.Done;

            _layout.BackColor = COLOR_BORDER;
            _lblName.BackColor = COLOR_CARD_INNER;
            _lblTime.BackColor = COLOR_CARD_INNER;
            _lblGoal.BackColor = COLOR_CARD_INNER;
            _btnPanel.BackColor = COLOR_CARD_INNER;

            _lblName.ForeColor = COLOR_TEXT_PRIMARY;
            _lblTime.ForeColor = COLOR_TEXT_PRIMARY;
            _lblGoal.ForeColor = COLOR_TEXT_SECONDARY;

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

            _btnDone.Visible = !isDone;
            _btnDone.Enabled = !isRunning && secs > 0;

            _btnDelete.Visible = true;
        }

        private static string FormatGoal(int estimatedSeconds)
        {
            long totalSec = (long)estimatedSeconds;
            long secPerMin = 60;
            long secPerHour = 3600;
            long secPerDay = 86400;
            long secPerMonth = 30 * secPerDay;
            long secPerYear = 365 * secPerDay;

            long years = totalSec / secPerYear;
            long rem = totalSec % secPerYear;
            long months = rem / secPerMonth;
            rem = rem % secPerMonth;
            long days = rem / secPerDay;
            rem = rem % secPerDay;
            long hours = rem / secPerHour;
            rem = rem % secPerHour;
            long mins = rem / secPerMin;
            long secs = rem % secPerMin;

            string time = $"{hours:D2}:{mins:D2}:{secs:D2}";
            if (years > 0)
                return $"{years:D2}.{months:D2}.{days:D2}.{time}";
            if (months > 0)
                return $"{months:D2}.{days:D2}.{time}";
            if (days > 0)
                return $"{days:D2}.{time}";
            return time;
        }
    }
}
