using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using TimeFlow;

namespace TimeFlow.UI;

public partial class MainForm : Form
{
    // === Константы тёмной темы ===
    private static readonly Color COLOR_BACKGROUND = Color.FromArgb(30, 30, 30);
    private static readonly Color COLOR_SURFACE = Color.FromArgb(45, 45, 45);
    private static readonly Color COLOR_TEXT_PRIMARY = Color.FromArgb(220, 220, 220);
    private static readonly Color COLOR_TEXT_SECONDARY = Color.FromArgb(150, 150, 150);
    private static readonly Color COLOR_ACCENT = Color.FromArgb(76, 175, 80);
    private static readonly Color COLOR_BORDER = Color.FromArgb(60, 60, 60);

    // Элементы каркаса
    private TableLayoutPanel? _rootLayout;
    private TableLayoutPanel? _topBar;
    private SplitContainer? _mainSplit;

    // Компоненты UI
    private TasksPanel? _tasksPanel;
    private AnalyticsPanel? _analyticsPanel;

    // Ядро приложения
    private SettingsStore? _settings;
    private VirtualClock? _clock;
    private List<TaskItem> _tasks = new();

    public MainForm()
    {
        InitializeComponent();
        InitializeCore();
        InitializeLayout();
        ApplyDarkTheme();
        LoadTestData();

        if (_tasksPanel != null)
        {
            _tasksPanel.TaskStartRequested += (_, task) => HandleTaskStart(task);
            _tasksPanel.TaskStopRequested += (_, task) => HandleTaskStop(task);
            _tasksPanel.TaskDoneRequested += (_, task) => HandleTaskDone(task);
            _tasksPanel.TaskDeleteRequested += (_, task) => HandleTaskDelete(task);
            _tasksPanel.AddTaskRequested += (_, _) => ShowAddTaskDialog();
        }

        if (_clock != null)
        {
            _clock.Ticked += (ratio) =>
            {
                if (this.IsDisposed || !this.IsHandleCreated) return;
                try
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        if (this.IsDisposed) return;
                        if (_tasks.Exists(t => t.Running))
                        {
                            _tasksPanel?.UpdateTimers(_clock);
                            RefreshAnalytics();
                        }
                    }));
                }
                catch (InvalidOperationException)
                {
                    
                }
            };
            this.FormClosing += (_, _) => _clock.Stop();
        }

        this.Load += OnFormLoad;
    }

    private void InitializeCore()
    {
        Config.EnsureDirs();
        _settings = new SettingsStore();
        _clock = new VirtualClock();

        bool vTimeEnabled = _settings.GetBool("virtual_time_enabled");
        double vRatio = _settings.GetDouble("virtual_time_ratio", 1.0);
        _clock.Configure(vTimeEnabled, vRatio);
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

        // --- TopBar ---
        _topBar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = COLOR_SURFACE,
            Padding = new Padding(10, 0, 10, 0),
            Margin = new Padding(0)
        };
        _topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
        _topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62F));
        _topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 4F));
		_topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 4F));
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
        btnHistory.Click += (_, _) =>
        {

            var history = new List<HistoryRecord>();
            if (_tasks != null && _clock != null)
            {
                foreach (var t in _tasks)
                {
                    history.Add(HistoryRecord.FromTask(
                        t, _clock,
                        t.Done ? "done" : "active"));
                }
            }

            try
            {
                var hdata = JsonStore.Load(Config.HistoryFile);
                if (hdata.ValueKind == JsonValueKind.Object &&
                    hdata.TryGetProperty("history", out var histArr))
                {
                    foreach (var h in histArr.EnumerateArray())
                    {
                        var rec = JsonSerializer.Deserialize<HistoryRecord>(
                            h.GetRawText());
                        if (rec != null) history.Add(rec);
                    }
                }
            }
            catch (Exception ex)
            {

                MessageBox.Show(this,
                    "Не удалось прочитать историю удалённых задач:\n" + ex.Message,
                    "TimeFlow", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            history = history
                .GroupBy(r => r.Id ?? "")
                .Select(g => g
                    .OrderByDescending(r => r.SortDate())
                    .First())
                .ToList();

            history.Sort((a, b) =>
                string.Compare(b.SortDate(), a.SortDate(), StringComparison.Ordinal));

            try
            {
                using var frm = new HistoryForm(_settings!, history);
                frm.ShowDialog(this);
            }
            catch (InvalidOperationException)
            {

                MessageBox.Show(this,
                    "Не удалось открыть окно истории. Попробуйте ещё раз.",
                    "TimeFlow",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
        _topBar.Controls.Add(btnHistory, 3, 0);
		
		// Кнопка настроек
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
		};
		_topBar.Controls.Add(btnSettings, 2, 0);  

        _rootLayout.Controls.Add(_topBar, 0, 0);

        // --- SplitContainer ---
        _mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 4,
            FixedPanel = FixedPanel.None,
            BackColor = COLOR_BORDER
        };

        _mainSplit.SplitterMoved += OnSplitterMoved;

        // Левая панель: TasksPanel
        _tasksPanel = new TasksPanel();
        _tasksPanel.BindSettings(_settings!);
        _mainSplit.Panel1.Controls.Add(_tasksPanel);

        // --- Правая панель: Аналитика ---
        _analyticsPanel = new AnalyticsPanel();
        _analyticsPanel.BindSettings(_settings!);
        _mainSplit.Panel2.Controls.Add(_analyticsPanel);

        _rootLayout.Controls.Add(_mainSplit, 0, 1);

        this.Controls.Add(_rootLayout);
    }

    private void OnFormLoad(object? sender, EventArgs e)
    {
        _clock?.Start();

        if (_mainSplit != null)
        {
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
            catch (InvalidOperationException)
            {
               
            }
        }

        _tasksPanel?.SetTasks(_tasks);
        RefreshAnalytics();
    }

    private bool _isAdjustingSplitter = false;

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

    private void ApplyDarkTheme()
    {
        this.BackColor = COLOR_BACKGROUND;
        this.ForeColor = COLOR_TEXT_PRIMARY;
        this.Text = "TimeFlow";
    }

    private void HandleTaskStart(TaskItem task)
    {
        task.StartSession();
        _tasksPanel?.SetTasks(_tasks);
        _clock?.Start();
        RefreshAnalytics();
    }

    private void HandleTaskStop(TaskItem task)
    {
        task.StopSession(_clock);
        _tasksPanel?.SetTasks(_tasks);
        RefreshAnalytics();
    }

    private void HandleTaskDone(TaskItem task)
    {
        if (!task.Running) task.StopSession(_clock);
        task.Done = true;
        task.CompletedAt = Config.NowIso();
        _tasksPanel?.SetTasks(_tasks);
        RefreshAnalytics();
    }

    private void HandleTaskDelete(TaskItem task)
    {

        if (task.Running) task.StopSession(_clock);

        Exception saveError = null;
        try
        {
            var removed = HistoryRecord.FromTask(task, _clock, "deleted");
            removed.RemovedAt = Config.NowIso();

            var existing = new List<HistoryRecord>();
            try
            {
                var hdata = JsonStore.Load(Config.HistoryFile);
                if (hdata.ValueKind == JsonValueKind.Object &&
                    hdata.TryGetProperty("history", out var histArr))
                {
                    foreach (var h in histArr.EnumerateArray())
                    {
                        var rec = JsonSerializer.Deserialize<HistoryRecord>(
                            h.GetRawText());
                        if (rec != null) existing.Add(rec);
                    }
                }
            }
            catch (Exception ex)
            {
                saveError = ex;
                existing = new List<HistoryRecord>();
            }

            existing.RemoveAll(r => r.Id == removed.Id);
            existing.Add(removed);

            JsonStore.Save(Config.HistoryFile, new { history = existing },
                encrypt: true);
        }
        catch (Exception ex)
        {

            if (saveError == null) saveError = ex;
        }

        _tasks.Remove(task);
        _tasksPanel?.SetTasks(_tasks);
        RefreshAnalytics();

        if (saveError != null)
        {
            MessageBox.Show(this,
                "Задача удалена из списка, но не удалось сохранить её в историю:\n" +
                saveError.Message,
                "TimeFlow", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ShowAddTaskDialog()
    {
        // === Диалог "Добавление новой задачи" ===
        using var dlg = new Form();
        dlg.Text = "Добавление новой задачи";
        dlg.Size = new Size(480, 360);
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
            RowCount = 6,
            Padding = new Padding(20, 20, 20, 20),
            Margin = new Padding(0),
            BackColor = COLOR_SURFACE
        };
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));

        // --- подпись над полем названия ---
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

        // --- TextBox для названия ---
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

        // --- подпись над выпадающим списком ---
        var lblCat = new Label
        {
            Text = "Выберите категорию:",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = COLOR_TEXT_PRIMARY,
            TextAlign = ContentAlignment.BottomLeft,
            Margin = new Padding(0, 0, 0, 4)
        };
        tlp.Controls.Add(lblCat, 0, 2);

        // --- ComboBox категорий ---
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
        foreach (var cat in _settings!.Categories().Keys) cmbCat.Items.Add(cat);
        cmbCat.SelectedIndex = 0;
        tlp.Controls.Add(cmbCat, 0, 3);

        // --- пустой разделитель ---
        tlp.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = COLOR_SURFACE }, 0, 4);

        // --- панель с кнопками ---
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
                var newTask = new TaskItem(txtName.Text, cmbCat.SelectedItem?.ToString() ?? "Учёба", 60);
                _tasks.Add(newTask);
                _tasksPanel?.SetTasks(_tasks);
                RefreshAnalytics();
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

        tlp.Controls.Add(btnRow, 0, 5);

        dlg.Controls.Add(tlp);
        dlg.AcceptButton = btnOk;
        dlg.CancelButton = btnCancel;

        txtName.Focus();
        dlg.ShowDialog(this);
    }

    private void LoadTestData()
    {
        _tasks.Add(new TaskItem("Изучить C# WinForms", "Учёба", 120));
        _tasks.Add(new TaskItem("Написать TaskCard", "Работа", 90));
        _tasks.Add(new TaskItem("Отдых и прогулка", "Отдых", 30));

        _tasks[0].StartSession();

        _tasksPanel?.SetTasks(_tasks);
        RefreshAnalytics();
    }

    // Расчёт статистики для аналитики из текущего списка задач
    private void RefreshAnalytics()
    {
        if (_analyticsPanel == null || _settings == null) return;

        // Заработок
        double rate = _settings.GetDouble("hourly_rate", 0);
        string today = Config.TodayStr();

        double todaySeconds = 0;
        foreach (var t in _tasks)
        {
            foreach (var s in t.Sessions)
            {
                if (s.Start <= 0) continue;
                var dt = DateTimeOffset.FromUnixTimeSeconds((long)s.Start).LocalDateTime;
                if (dt.ToString("yyyy-MM-dd") == today)
                {
                    todaySeconds += s.End == 0 && _clock != null
                        ? _clock.ElapsedVirtualSeconds(s.Start)
                        : s.Vsec;
                }
            }
        }
        double earningsToday = todaySeconds / 3600.0 * rate;
        double earningsMonth = earningsToday;

        // Часы по дням недели
        double[] weekly = new double[7];
        foreach (var t in _tasks)
        {
            foreach (var s in t.Sessions)
            {
                if (s.Start <= 0) continue;
                var dt = DateTimeOffset.FromUnixTimeSeconds((long)s.Start).LocalDateTime;
                int dayIndex = (int)dt.DayOfWeek == 0 ? 6 : (int)dt.DayOfWeek - 1;
                weekly[dayIndex] += s.End == 0 && _clock != null
                    ? _clock.ElapsedVirtualSeconds(s.Start)
                    : s.Vsec;
            }
        }

        // По категориям за сегодня
        var byCategory = new Dictionary<string, double>();
        foreach (var t in _tasks)
        {
            foreach (var s in t.Sessions)
            {
                if (s.Start <= 0) continue;
                var dt = DateTimeOffset.FromUnixTimeSeconds((long)s.Start).LocalDateTime;
                if (dt.ToString("yyyy-MM-dd") == today)
                {
                    double sec = s.End == 0 && _clock != null
                        ? _clock.ElapsedVirtualSeconds(s.Start)
                        : s.Vsec;
                    byCategory[t.Category] = byCategory.GetValueOrDefault(t.Category, 0) + sec;
                }
            }
        }

        _analyticsPanel.RefreshAll(earningsToday, earningsMonth, _tasks, weekly, byCategory);
    }
}
