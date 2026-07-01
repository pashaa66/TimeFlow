using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TimeFlow;

namespace TimeFlow.UI;

public partial class MainForm : Form
{
    private static readonly Color COLOR_BACKGROUND = Color.FromArgb(30, 30, 30);
    private static readonly Color COLOR_SURFACE = Color.FromArgb(45, 45, 45);
    private static readonly Color COLOR_TEXT_PRIMARY = Color.FromArgb(220, 220, 220);
    private static readonly Color COLOR_TEXT_SECONDARY = Color.FromArgb(150, 150, 150);
    private static readonly Color COLOR_ACCENT = Color.FromArgb(76, 175, 80);
    private static readonly Color COLOR_BORDER = Color.FromArgb(60, 60, 60);

    private static readonly Color COLOR_POMODORO_BG = Color.FromArgb(55, 231, 76, 60);

    private static readonly Color COLOR_POMODORO_TEXT = Color.FromArgb(231, 76, 60);

    private static readonly Color COLOR_VTIME_BG = Color.FromArgb(55, 76, 175, 80);

    private static readonly Color COLOR_VTIME_TEXT = Color.FromArgb(76, 175, 80);

    private readonly SettingsStore _settings;
    private readonly VirtualClock _clock;
    private readonly TaskManager _tasks;
    private readonly PomodoroTimer _pomodoro;

    private TableLayoutPanel? _rootLayout;
    private TableLayoutPanel? _topBar;
    private SplitContainer? _mainSplit;

    private TasksPanel? _tasksPanel;
    private AnalyticsPanel? _analyticsPanel;

    private Label? _lblPomodoroHeader;
    private Panel? _pnlPomodoroTimerArea;
    private Label? _lblPomodoroTime;
    private Button? _btnPomodoroToggle;

    private Label? _lblVTimeHeader;
    private Panel? _pnlVTimeDisplay;
    private Label? _lblVTimeValue;
    private Button? _btnVTimeToggle;

    private NotifyIcon? _trayIcon;

    private readonly System.Windows.Forms.Timer _uiTimer = new() { Interval = 1000 };

    private MiniTimerForm? _miniTimer;

    private bool _isAdjustingSplitter = false;

    public MainForm(SettingsStore settings, VirtualClock clock,
                    TaskManager tasks, PomodoroTimer pomodoro)
    {
        _settings = settings;
        _clock = clock;
        _tasks = tasks;
        _pomodoro = pomodoro;

        InitializeComponent();
        InitializeLayout();
        ApplyDarkTheme();
        WireEvents();         
        StartUiTimer();       
        UpdatePomodoroDisplay();
        UpdateVTimeDisplay();
        InitMiniTimer();

        this.Load += OnFormLoad;

        SetupTray();
    }

    private void WireEvents()
    {

        _tasks.TasksChanged += OnTasksChanged;
        _tasks.ActiveChanged += OnActiveChanged;
        _clock.ConfigChanged += OnClockConfigChanged;

        _pomodoro.Tick += OnPomodoroTick;
        _pomodoro.PhaseChanged += OnPomodoroPhaseChanged;
        _pomodoro.FinishedPhase += OnPomodoroFinishedPhase;

        if (_tasksPanel != null)
        {
            _tasksPanel.TaskStartRequested += (_, task) => _tasks.StartTask(task.Id);
            _tasksPanel.TaskStopRequested += (_, task) => _tasks.StopTask(task.Id);
            _tasksPanel.TaskDoneRequested += (_, task) => _tasks.CompleteTask(task.Id);
            _tasksPanel.TaskDeleteRequested += (_, task) => _tasks.RemoveTask(task.Id);
            _tasksPanel.AddTaskRequested += (_, _) => ShowAddTaskDialog();
        }

        if (_btnPomodoroToggle != null)
        {
            _btnPomodoroToggle.Click += (_, _) =>
            {
                if (_pomodoro.Running) _pomodoro.Stop();
                else _pomodoro.Start();
                UpdatePomodoroDisplay();
            };
        }

        if (_btnVTimeToggle != null)
        {
            _btnVTimeToggle.Click += (_, _) => ToggleVirtualTime();
        }

        if (_mainSplit != null)
            _mainSplit.SplitterMoved += OnSplitterMoved;
    }

    private void OnTasksChanged()
    {
        if (this.IsDisposed || !this.IsHandleCreated) return;
        try
        {
            this.BeginInvoke(new Action(() =>
            {
                if (this.IsDisposed) return;
                _tasksPanel?.SetTasks(_tasks.Tasks.ToList());
                RefreshAnalytics();
                _miniTimer?.RefreshTasks(_tasks.Tasks);
            }));
        }
        catch (InvalidOperationException) { }
    }

    private void OnActiveChanged(string? activeId)
    {
        if (this.IsDisposed || !this.IsHandleCreated) return;
        try
        {
            this.BeginInvoke(new Action(() =>
            {
                if (this.IsDisposed) return;
                _tasksPanel?.SetTasks(_tasks.Tasks.ToList());
                RefreshAnalytics();
                _miniTimer?.RefreshTasks(_tasks.Tasks);
            }));
        }
        catch (InvalidOperationException) { }
    }

    private void OnClockConfigChanged()
    {

        if (this.IsDisposed || !this.IsHandleCreated) return;
        try
        {
            this.BeginInvoke(new Action(() =>
            {
                if (this.IsDisposed) return;
                UpdateVTimeDisplay();
            }));
        }
        catch (InvalidOperationException) { }
    }

    private void OnPomodoroTick(int remaining)
    {
        if (this.IsDisposed || !this.IsHandleCreated) return;
        try
        {
            this.BeginInvoke(new Action(() =>
            {
                if (this.IsDisposed) return;
                UpdatePomodoroDisplay();
            }));
        }
        catch (InvalidOperationException) { }
    }

    private void OnPomodoroPhaseChanged(string phase)
    {
        if (this.IsDisposed || !this.IsHandleCreated) return;
        try
        {
            this.BeginInvoke(new Action(() =>
            {
                if (this.IsDisposed) return;
                UpdatePomodoroDisplay();
            }));
        }
        catch (InvalidOperationException) { }
    }

    private void OnPomodoroFinishedPhase(string finishedPhase)
    {
        if (this.IsDisposed || !this.IsHandleCreated) return;
        try
        {
            this.BeginInvoke(new Action(() =>
            {
                if (this.IsDisposed) return;
                string msg = finishedPhase == PomodoroTimer.PhaseWork
                    ? "Фаза работы завершена. Перерыв!"
                    : "Перерыв окончен. За работу!";
                _trayIcon?.ShowBalloonTip(4000, "TimeFlow — Pomodoro", msg, ToolTipIcon.Info);
                MessageBox.Show(this, msg, "Pomodoro",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                UpdatePomodoroDisplay();
            }));
        }
        catch (InvalidOperationException) { }
    }

    private void StartUiTimer()
    {
        _uiTimer.Tick += (_, _) =>
        {
            if (this.IsDisposed || !this.IsHandleCreated) return;
            try
            {
                if (_tasks.Tasks.Any(t => t.Running))
                {
                    _tasksPanel?.UpdateTimers(_clock);
                    RefreshAnalytics();
                }
                UpdatePomodoroDisplay();
                UpdateVTimeDisplay();

                _miniTimer?.RefreshTasks(_tasks.Tasks);
            }
            catch (InvalidOperationException) { }
        };
        _uiTimer.Start();
    }

    private void ToggleVirtualTime()
    {
        bool newEnabled = !_clock.Enabled;
        double ratio = _settings.GetDouble("virtual_time_ratio", 1.0);
        _clock.Configure(newEnabled, ratio);
        _settings.Set("virtual_time_enabled", newEnabled);
        _settings.Save();
        UpdateVTimeDisplay();
    }

    private void UpdateVTimeDisplay()
    {
        if (_lblVTimeValue == null || _btnVTimeToggle == null) return;

        if (_clock.Enabled)
        {
            _lblVTimeValue.Text = _clock.NowDisplay();
            _btnVTimeToggle.Text = "ВКЛ";
            _btnVTimeToggle.BackColor = COLOR_VTIME_TEXT;
            _btnVTimeToggle.ForeColor = Color.White;
        }
        else
        {
            _lblVTimeValue.Text = DateTime.Now.ToString("HH:mm:ss");
            _btnVTimeToggle.Text = "ВЫКЛ";
            _btnVTimeToggle.BackColor = Color.FromArgb(70, 70, 70);
            _btnVTimeToggle.ForeColor = Color.White;
        }
    }

    private void UpdatePomodoroDisplay()
    {
        if (_lblPomodoroTime == null || _btnPomodoroToggle == null) return;

        int total = Math.Max(0, _pomodoro.Remaining);
        int mm = total / 60;
        int ss = total % 60;
        _lblPomodoroTime.Text = $"{mm:D2}:{ss:D2}";

        if (_pomodoro.Running)
        {
            _btnPomodoroToggle.Text = "Стоп";
            _btnPomodoroToggle.BackColor = Color.FromArgb(192, 57, 43);
            _btnPomodoroToggle.ForeColor = Color.White;
        }
        else
        {
            _btnPomodoroToggle.Text = "Старт";
            _btnPomodoroToggle.BackColor = COLOR_POMODORO_TEXT;
            _btnPomodoroToggle.ForeColor = Color.White;
        }
    }

    private void InitializeLayout()
    {
        _rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = COLOR_BACKGROUND,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        _rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90F));
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        BuildTopBar();
        BuildMainSplit();

        _rootLayout.Controls.Add(_topBar, 0, 0);
        _rootLayout.Controls.Add(_mainSplit, 0, 1);

        this.Controls.Add(_rootLayout);
    }

    private void BuildTopBar()
    {
        _topBar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1,
            BackColor = COLOR_SURFACE,
            Padding = new Padding(10, 0, 10, 0),
            Margin = new Padding(0)
        };

        _topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220F));
        _topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220F));
        _topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60F));
        _topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60F));
        _topBar.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));


        var lblTitle = new Label
        {
            Text = "TimeFlow",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = COLOR_TEXT_PRIMARY,
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        _topBar.Controls.Add(lblTitle, 0, 0);

        BuildVTimeUi();
        _topBar.Controls.Add(BuildVTimeContainer(), 1, 0);

        BuildPomodoroUi();
        _topBar.Controls.Add(BuildPomodoroContainer(), 2, 0);

        var btnSettings = new Button
        {
            Text = "⚙️",
            Size = new Size(50, 50),
            FlatStyle = FlatStyle.Flat,
            BackColor = COLOR_SURFACE,
            ForeColor = COLOR_TEXT_PRIMARY,
            Font = new Font("Segoe UI", 14),
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Margin = new Padding(0, 20, 5, 0)
        };
        btnSettings.Click += (_, _) =>
        {
            using var settingsDialog = new SettingsDialog(_settings);
            settingsDialog.ShowDialog(this);
            _pomodoro.ReloadSettings();
            bool vEnabled = _settings.GetBool("virtual_time_enabled");
            double vRatio = _settings.GetDouble("virtual_time_ratio", 1.0);
            _clock.Configure(vEnabled, vRatio);

            _tasksPanel?.RefreshCategories();

            _analyticsPanel?.BindSettings(_settings);
            RefreshAnalytics();

            UpdateVTimeDisplay();

            ApplyMiniTimerVisibility();
        };
        _topBar.Controls.Add(btnSettings, 3, 0);
        var btnHistory = new Button
        {
            Text = "📋",
            Size = new Size(50, 50),
            FlatStyle = FlatStyle.Flat,
            BackColor = COLOR_SURFACE,
            ForeColor = COLOR_TEXT_PRIMARY,
            Font = new Font("Segoe UI", 14),
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Margin = new Padding(0, 20, 5, 0)
        };
        btnHistory.Click += (_, _) => ShowHistoryDialog();
        _topBar.Controls.Add(btnHistory, 4, 0);
    }

    private void BuildPomodoroUi()
    {
        _lblPomodoroHeader = new Label
        {
            Text = "Запуск Pomodoro",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = COLOR_TEXT_PRIMARY,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            AutoSize = false,
            Margin = new Padding(0)
        };

        _pnlPomodoroTimerArea = new PomodoroTimerPanel
        {
            Dock = DockStyle.Fill,
            BackColor = COLOR_POMODORO_BG,
            Margin = new Padding(0)
        };

        _lblPomodoroTime = new Label
        {
            Text = "25:00",
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            ForeColor = COLOR_POMODORO_TEXT,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoSize = false,
            BackColor = Color.Transparent
        };
        _pnlPomodoroTimerArea.Controls.Add(_lblPomodoroTime);

        _btnPomodoroToggle = new Button
        {
            Text = "Старт",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            BackColor = COLOR_POMODORO_TEXT,
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            FlatAppearance = { BorderSize = 0 }
        };
    }

    private TableLayoutPanel BuildPomodoroContainer()
    {
        var outer = new TableLayoutPanel
        {
            Width = 208,
            Height = 70,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = COLOR_SURFACE,
            Margin = new Padding(0),
            Padding = new Padding(0),
            Dock = DockStyle.None,
            Anchor = AnchorStyles.Top | AnchorStyles.Left
        };
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 208F));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));  // надпись
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));  // таймер+кнопка

        var bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = COLOR_SURFACE,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));  // таймер
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 8F));     // зазор
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));  // кнопка
        bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        bottom.Controls.Add(_pnlPomodoroTimerArea!, 0, 0);
        bottom.Controls.Add(new Panel { BackColor = COLOR_SURFACE, Dock = DockStyle.Fill }, 1, 0);
        bottom.Controls.Add(_btnPomodoroToggle!, 2, 0);

        outer.Controls.Add(_lblPomodoroHeader!, 0, 0);
        outer.Controls.Add(bottom, 0, 1);

        return outer;
    }

    private void BuildVTimeUi()
    {
        _lblVTimeHeader = new Label
        {
            Text = "Виртуальные часы:",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = COLOR_TEXT_PRIMARY,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            AutoSize = false,
            Margin = new Padding(0)
        };

        _pnlVTimeDisplay = new VTimeDisplayPanel
        {
            Dock = DockStyle.Fill,
            BackColor = COLOR_VTIME_BG,
            Margin = new Padding(0)
        };

        _lblVTimeValue = new Label
        {
            Text = "00:00:00",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = COLOR_VTIME_TEXT,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoSize = false,
            BackColor = Color.Transparent
        };
        _pnlVTimeDisplay.Controls.Add(_lblVTimeValue);
        _btnVTimeToggle = new Button
        {
            Text = "ВЫКЛ",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(70, 70, 70),
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            FlatAppearance = { BorderSize = 0 }
        };
    }

    private TableLayoutPanel BuildVTimeContainer()
    {
        var outer = new TableLayoutPanel
        {
            Width = 208,
            Height = 70,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = COLOR_SURFACE,
            Margin = new Padding(0),
            Padding = new Padding(0),
            Dock = DockStyle.None,
            Anchor = AnchorStyles.Top | AnchorStyles.Left
        };
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 208F));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));  // надпись
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));  // время+кнопка

        var bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = COLOR_SURFACE,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));  // время
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 8F));     // зазор
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));  // кнопка
        bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        bottom.Controls.Add(_pnlVTimeDisplay!, 0, 0);
        bottom.Controls.Add(new Panel { BackColor = COLOR_SURFACE, Dock = DockStyle.Fill }, 1, 0);
        bottom.Controls.Add(_btnVTimeToggle!, 2, 0);

        outer.Controls.Add(_lblVTimeHeader!, 0, 0);
        outer.Controls.Add(bottom, 0, 1);

        return outer;
    }

    private void BuildMainSplit()
    {
        _mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 4,
            FixedPanel = FixedPanel.None,
            BackColor = COLOR_BORDER
        };

        _tasksPanel = new TasksPanel();
        _tasksPanel.BindSettings(_settings);

        _analyticsPanel = new AnalyticsPanel();
        _analyticsPanel.BindSettings(_settings);

        _mainSplit.Panel1.Controls.Add(_tasksPanel);
        _mainSplit.Panel2.Controls.Add(_analyticsPanel);
    }

    private void OnFormLoad(object? sender, EventArgs e)
    {
        _clock.Start();
        ApplySplitterConstraints();
        _tasksPanel?.SetTasks(_tasks.Tasks.ToList());
        RefreshAnalytics();
        UpdatePomodoroDisplay();
    }

    private void ApplySplitterConstraints()
    {
        if (_mainSplit == null) return;

        int sw = _mainSplit.SplitterWidth;
        int width = _mainSplit.Width;
        int min1 = 300;
        int min2 = 450;
        int desired = 480;

        if (width < min1 + min2 + sw + 10)
        {
            min1 = Math.Max(120, width / 3);
            min2 = Math.Max(120, width / 3);
            desired = width / 2;
        }

        try
        {
            _mainSplit.Panel1MinSize = min1;
            _mainSplit.Panel2MinSize = min2;
            int maxDistance = Math.Max(min1, width - min2 - sw);
            int distance = Math.Max(min1, Math.Min(desired, maxDistance));
            _mainSplit.SplitterDistance = distance;

            int maxSplitterDistance = _mainSplit.Width - min2 - sw;
            if (_mainSplit.SplitterDistance > maxSplitterDistance)
            {
                _mainSplit.SplitterDistance = maxSplitterDistance;
            }
        }
        catch (InvalidOperationException) { }
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);

        if (WindowState == FormWindowState.Minimized){
            this.ShowInTaskbar = false;
            Hide();
            _trayIcon?.ShowBalloonTip(2000, "TimeFlow", "Приложение свёрнуто в трей", ToolTipIcon.Info);
        }

        if (_mainSplit == null || _isAdjustingSplitter) return;

        try
        {
            _isAdjustingSplitter = true;
            ApplySplitterConstraints();
        }
        finally
        {
            _isAdjustingSplitter = false;
        }
    }

    private void OnSplitterMoved(object? sender, EventArgs e)
    {
        if (_isAdjustingSplitter || _mainSplit == null) return;

        try
        {
            _isAdjustingSplitter = true;

            int sw = _mainSplit.SplitterWidth;
            int width = _mainSplit.Width;
            int min2 = 450;

            int maxDistance = width - min2 - sw;

            if (maxDistance > 0 && _mainSplit.SplitterDistance > maxDistance)
            {
                _mainSplit.SplitterDistance = maxDistance;
            }
            else if (_mainSplit.SplitterDistance < 300)
            {
                _mainSplit.SplitterDistance = 300;
            }
        }
        finally
        {
            _isAdjustingSplitter = false;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        try
        {
            _uiTimer.Stop();
            _pomodoro.Stop();
            _clock.Stop();
            try { _miniTimer?.Dispose(); _miniTimer = null; }
            catch { /* не блокируем закрытие формы */ }
            try { _tasks.SaveTasks(); }
            catch { /* не блокируем закрытие формы */ }
        }
        catch { /* не блокируем закрытие формы */ }
        _trayIcon?.Dispose();
        base.OnFormClosing(e);
    }

    private void InitMiniTimer()
    {
        _miniTimer = new MiniTimerForm(_settings, _clock);

        _miniTimer.StartClicked   += (_, t) => _tasks.StartTask(t.Id);
        _miniTimer.StopClicked    += (_, t) => _tasks.StopTask(t.Id);
        _miniTimer.DoneClicked    += (_, t) => _tasks.CompleteTask(t.Id);
        _miniTimer.DeleteClicked  += (_, t) => _tasks.RemoveTask(t.Id);
        _miniTimer.Clicked        += (_, t) =>
        {
            try
            {
                if (this.WindowState == FormWindowState.Minimized)
                    this.WindowState = FormWindowState.Normal;
                this.Activate();
            }
            catch { /* игнорируем */ }
        };

        try
        {
            var screen = Screen.PrimaryScreen?.WorkingArea
                ?? SystemInformation.WorkingArea;
            _miniTimer.Location = new Point(screen.Right - _miniTimer.Width - 20,
                                           screen.Top + 20);
        }
        catch { /* позиция по умолчанию */ }

        this.Resize += OnMainFormResize;
        this.SizeChanged += (_, _) => ApplyMiniTimerVisibility();
    }

    private void OnMainFormResize(object? sender, EventArgs e)
    {
        ApplyMiniTimerVisibility();
    }

    private void ApplyMiniTimerVisibility()
    {
        if (_miniTimer == null) return;

        bool enabled = _settings.GetBool("mini_timer_enabled", true);
        if (!enabled)
        {
            if (_miniTimer.Visible) _miniTimer.Hide();
            return;
        }

        bool mainMinimized = (this.WindowState == FormWindowState.Minimized);
        if (mainMinimized)
        {
            if (!_miniTimer.Visible)
            {
                _miniTimer.Show(this);
                _miniTimer.RefreshTasks(_tasks.Tasks);
            }
        }
        else
        {
            if (_miniTimer.Visible) _miniTimer.Hide();
        }
    }

    private void ApplyDarkTheme()
    {
        this.BackColor = COLOR_BACKGROUND;
        this.ForeColor = COLOR_TEXT_PRIMARY;
        this.Text = "TimeFlow";
        this.Icon = MakeTrayIcon();
    }

    private void SetupTray()
    {
        _trayIcon = new NotifyIcon
        {
            Icon = MakeTrayIcon(),
            Visible = true,
            Text = "TimeFlow"
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Открыть", null, (_, _) => RestoreFromTray());
       menu.Items.Add("Выход", null, (_, _) =>
        {
            try
            {
                _uiTimer.Stop();
                _pomodoro.Stop();
                _clock.Stop();
                _tasks.SaveTasks();
            }
            catch { /* игнорируем ошибки при сохранении */ }

            _trayIcon!.Visible = false;
            _trayIcon.Dispose();


            Environment.Exit(0);
        });
        _trayIcon.ContextMenuStrip = menu;

        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();
    }

    private void RestoreFromTray()
    {
        this.ShowInTaskbar = true;
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private static Icon MakeTrayIcon()
    {
        var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(COLOR_ACCENT);
            g.FillEllipse(brush, 0, 0, 32, 32);
            using var font = new Font("Segoe UI", 16F, FontStyle.Bold);
            using var textBrush = new SolidBrush(Color.White);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("T", font, textBrush, new RectangleF(0, 0, 32, 32), sf);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    // === Расчёт статистики для аналитики ===
    private void RefreshAnalytics()
    {
        if (_analyticsPanel == null) return;

        double earningsToday = _tasks.EarningsToday();
        double earningsMonth = _tasks.EarningsMonth();
        double[] weekly = _tasks.SecondsByWeekday();
        var byCategory = _tasks.SecondsByCategoryToday();

        _analyticsPanel.RefreshAll(
            earningsToday, earningsMonth,
            _tasks.Tasks.ToList(),
            weekly, byCategory);
    }

    private void ShowHistoryDialog()
    {
        var history = _tasks.FullHistory();

        try
        {
            using var frm = new HistoryForm(_settings, history);
            frm.ShowDialog(this);
        }
        catch (InvalidOperationException)
        {
            MessageBox.Show(this,
                "Не удалось открыть окно истории. Попробуйте ещё раз.",
                "TimeFlow",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ShowAddTaskDialog()
    {
        using var dlg = new Form();
        dlg.Text = "Добавление новой задачи";
        dlg.Size = new Size(480, 520);
        dlg.StartPosition = FormStartPosition.CenterParent;
        dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
        dlg.MaximizeBox = false;
        dlg.MinimizeBox = false;
        dlg.BackColor = COLOR_SURFACE;
        dlg.ForeColor = COLOR_TEXT_PRIMARY;
        dlg.Font = new Font("Segoe UI", 9F);

        var tlp = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            Padding = new Padding(20, 20, 20, 20),
            Margin = new Padding(0),
            BackColor = COLOR_SURFACE
        };
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));

        var lblName = new Label
        {
            Text = "Введите название новой задачи:",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = COLOR_TEXT_PRIMARY,
            TextAlign = ContentAlignment.BottomLeft,
            Margin = new Padding(0, 0, 0, 4)
        };
        tlp.Controls.Add(lblName, 0, 0);

        var txtName = new TextBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 12),
            BackColor = COLOR_BACKGROUND,
            ForeColor = COLOR_TEXT_PRIMARY,
            Font = new Font("Segoe UI", 10F),
            BorderStyle = BorderStyle.FixedSingle
        };
        tlp.Controls.Add(txtName, 0, 1);
        var goalPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 8,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 12),
            Padding = new Padding(0),
            BackColor = COLOR_SURFACE
        };
        goalPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60F));   // «Цель:» label
        goalPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70F));   // часы
        goalPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 22F));   // «ч.»
        goalPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70F));   // минуты
        goalPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42F));   // «мин.»
        goalPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70F));   // секунды
        goalPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42F));   // «сек.»
        goalPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));   // filler
        goalPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var lblGoalInline = new Label
        {
            Text = "Цель:",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10F, FontStyle.Regular),
            ForeColor = COLOR_TEXT_PRIMARY,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0)
        };
        var numHours = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 9999,
            Value = 1,
            Dock = DockStyle.Fill,
            BackColor = COLOR_BACKGROUND,
            ForeColor = COLOR_TEXT_PRIMARY,
            Font = new Font("Segoe UI", 10F),
            Margin = new Padding(0, 2, 4, 2)
        };
        var lblH = new Label
        {
            Text = "ч.",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9F),
            ForeColor = COLOR_TEXT_PRIMARY,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0)
        };
        var numMinutes = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 59,
            Value = 0,
            Dock = DockStyle.Fill,
            BackColor = COLOR_BACKGROUND,
            ForeColor = COLOR_TEXT_PRIMARY,
            Font = new Font("Segoe UI", 10F),
            Margin = new Padding(4, 2, 4, 2)
        };
        var lblM = new Label
        {
            Text = "мин.",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9F),
            ForeColor = COLOR_TEXT_PRIMARY,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0)
        };
        var numSeconds = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 59,
            Value = 0,
            Dock = DockStyle.Fill,
            BackColor = COLOR_BACKGROUND,
            ForeColor = COLOR_TEXT_PRIMARY,
            Font = new Font("Segoe UI", 10F),
            Margin = new Padding(4, 2, 4, 2)
        };
        var lblS = new Label
        {
            Text = "сек.",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9F),
            ForeColor = COLOR_TEXT_PRIMARY,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0)
        };
        goalPanel.Controls.Add(lblGoalInline, 0, 0);
        goalPanel.Controls.Add(numHours, 1, 0);
        goalPanel.Controls.Add(lblH, 2, 0);
        goalPanel.Controls.Add(numMinutes, 3, 0);
        goalPanel.Controls.Add(lblM, 4, 0);
        goalPanel.Controls.Add(numSeconds, 5, 0);
        goalPanel.Controls.Add(lblS, 6, 0);
        goalPanel.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = COLOR_SURFACE }, 7, 0);
        tlp.Controls.Add(goalPanel, 0, 2);

        var lblCat = new Label
        {
            Text = "Выберите категорию:",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = COLOR_TEXT_PRIMARY,
            TextAlign = ContentAlignment.BottomLeft,
            Margin = new Padding(0, 0, 0, 4)
        };
        tlp.Controls.Add(lblCat, 0, 3);

        var cmbCat = new ComboBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 12),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = COLOR_BACKGROUND,
            ForeColor = COLOR_TEXT_PRIMARY,
            Font = new Font("Segoe UI", 10F),
            FlatStyle = FlatStyle.Flat
        };
        foreach (var cat in _settings.Categories().Keys) cmbCat.Items.Add(cat);
        cmbCat.SelectedIndex = 0;
        tlp.Controls.Add(cmbCat, 0, 4);

        tlp.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = COLOR_SURFACE }, 0, 5);

        var btnRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            Padding = new Padding(0),
            Margin = new Padding(0),
            BackColor = COLOR_SURFACE
        };
        btnRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
        btnRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 12F));
        btnRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
        btnRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        btnRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var btnOk = new Button
        {
            Text = "Создать",
            Dock = DockStyle.Fill,
            BackColor = COLOR_ACCENT,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0)
        };
        btnOk.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(txtName.Text))
            {
                int goalSeconds = (int)numHours.Value * 3600
                                + (int)numMinutes.Value * 60
                                + (int)numSeconds.Value;
                _tasks.AddTask(txtName.Text, cmbCat.SelectedItem?.ToString() ?? "Учёба", goalSeconds);
                dlg.DialogResult = DialogResult.OK;
                dlg.Close();
            }
            else
            {
                MessageBox.Show(dlg, "Введите название задачи.", dlg.Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                txtName.Focus();
            }
        };

        var btnCancel = new Button
        {
            Text = "Отмена",
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(70, 70, 70),
            ForeColor = COLOR_TEXT_PRIMARY,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F),
            Cursor = Cursors.Hand,
            Margin = new Padding(0),
            DialogResult = DialogResult.Cancel
        };
        btnCancel.Click += (_, _) =>
        {
            dlg.DialogResult = DialogResult.Cancel;
            dlg.Close();
        };

        btnRow.Controls.Add(btnOk, 0, 0);
        btnRow.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = COLOR_SURFACE }, 1, 0);
        btnRow.Controls.Add(btnCancel, 2, 0);
        btnRow.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = COLOR_SURFACE }, 3, 0);

        tlp.Controls.Add(btnRow, 0, 6);

        dlg.Controls.Add(tlp);
        dlg.AcceptButton = btnOk;
        dlg.CancelButton = btnCancel;

        txtName.Focus();
        dlg.ShowDialog(this);
    }
}

internal class PomodoroTimerPanel : Panel
{
    public PomodoroTimerPanel()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        DoubleBuffered = true;
    }
}

internal class VTimeDisplayPanel : Panel
{
    public VTimeDisplayPanel()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        DoubleBuffered = true;
    }
}
