using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using TimeFlow;

namespace TimeFlow.UI;

public class TasksPanel : UserControl
{
    private static readonly Color COLOR_SURFACE = Color.FromArgb(45, 45, 45);
    private static readonly Color COLOR_BACKGROUND = Color.FromArgb(30, 30, 30);
    private static readonly Color COLOR_TEXT_PRIMARY = Color.FromArgb(220, 220, 220);
    private static readonly Color COLOR_ACCENT = Color.FromArgb(76, 175, 80);

    private TableLayoutPanel? _toolbar;
    private Button? _btnAdd;
    private ComboBox? _cmbFilter;
    private FlowLayoutPanel? _flowList;
    private Label? _lblEmptyState;

    private List<TaskItem> _tasks = new();
    private SettingsStore? _settings;
    private bool _suppressFilterEvent = false;

    public event EventHandler<TaskItem>? TaskStartRequested;
    public event EventHandler<TaskItem>? TaskStopRequested;
    public event EventHandler<TaskItem>? TaskDoneRequested;
    public event EventHandler<TaskItem>? TaskDeleteRequested;
    public event EventHandler? AddTaskRequested;

    public TasksPanel()
    {
        InitializeLayout();
    }

    public void BindSettings(SettingsStore settings)
    {
        _settings = settings;
        RefreshFilters();
        RefreshList();
    }

    public void RefreshCategories()
    {
        RefreshFilters();
    }

    public void SetTasks(List<TaskItem> tasks)
    {
        _tasks = tasks ?? new List<TaskItem>();
        RefreshList();
    }

    public void UpdateTimers(VirtualClock? clock = null)
    {
        if (_flowList == null) return;
        foreach (Control ctrl in _flowList.Controls)
        {
            if (ctrl is TaskCard card && card.CurrentTask != null)
            {
                string colorHex = _settings?.ColorFor(card.CurrentTask.Category) ?? "";
                card.RefreshCard(card.CurrentTask, clock, colorHex);
            }
        }
    }

    private void InitializeLayout()
    {
        this.Dock = DockStyle.Fill;
        this.BackColor = COLOR_BACKGROUND;

        _toolbar = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 48,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = COLOR_SURFACE,
            Padding = new Padding(10, 6, 10, 6),
            Margin = new Padding(0)
        };
        _toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
        _toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        _btnAdd = new Button
        {
            Text = "+ Новая задача",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            BackColor = COLOR_ACCENT,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 2, 6, 2)
        };
        _btnAdd.Click += (_, _) => AddTaskRequested?.Invoke(this, EventArgs.Empty);

        _cmbFilter = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = COLOR_BACKGROUND,
            ForeColor = COLOR_TEXT_PRIMARY,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9),
            Margin = new Padding(6, 2, 0, 2)
        };
        _cmbFilter.SelectedIndexChanged += CmbFilter_SelectedIndexChanged;

        _toolbar.Controls.Add(_btnAdd, 0, 0);
        _toolbar.Controls.Add(_cmbFilter, 1, 0);

        _flowList = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = COLOR_BACKGROUND,
            Padding = new Padding(0, 8, 0, 0),
            Margin = new Padding(0)
        };

        _lblEmptyState = new Label
        {
            Text = "Нет задач. Нажмите «+ Новая задача»",
            AutoSize = true,
            ForeColor = Color.FromArgb(120, 120, 120),
            Font = new Font("Segoe UI", 11, FontStyle.Italic),
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(20, 40, 20, 0)
        };

        this.Controls.Add(_flowList);
        this.Controls.Add(_toolbar);
    }

    private void CmbFilter_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_suppressFilterEvent) return;
        RefreshList();
    }

    private void RefreshFilters()
    {
        if (_cmbFilter == null) return;

        string? prevSelected = _cmbFilter.SelectedItem?.ToString();

        _suppressFilterEvent = true;
        try
        {
            _cmbFilter.Items.Clear();
            _cmbFilter.Items.Add("Все категории");

            if (_settings != null)
            {
                foreach (var cat in _settings.Categories().Keys)
                {
                    _cmbFilter.Items.Add(cat);
                }
            }
            else
            {
                _cmbFilter.Items.AddRange(new object[] { "Учёба", "Работа", "Отдых" });
            }

            if (prevSelected != null && _cmbFilter.Items.Contains(prevSelected))
                _cmbFilter.SelectedItem = prevSelected;
            else
                _cmbFilter.SelectedIndex = 0;
        }
        finally
        {
            _suppressFilterEvent = false;
        }
    }

    private void RefreshList()
    {
        if (_flowList == null || _lblEmptyState == null) return;

        _flowList.SuspendLayout();
        _flowList.Controls.Clear();

        string filter = _cmbFilter?.SelectedItem?.ToString() ?? "Все категории";
        bool showAll = filter == "Все категории";

        List<TaskItem> filtered = showAll
            ? new List<TaskItem>(_tasks)
            : _tasks.FindAll(t => t.Category == filter);

        filtered.Sort((a, b) =>
        {
            if (a.Running && !b.Running) return -1;
            if (!a.Running && b.Running) return 1;
            return string.Compare(b.CreatedAt, a.CreatedAt, StringComparison.Ordinal);
        });

        if (filtered.Count == 0)
        {
            _lblEmptyState.Visible = true;
            _flowList.Controls.Add(_lblEmptyState);
            _flowList.ResumeLayout(true);
            _flowList.PerformLayout();
            return;
        }

        _lblEmptyState.Visible = false;

        foreach (var task in filtered)
        {
            var card = new TaskCard();

            card.StartClicked += (_, t) => TaskStartRequested?.Invoke(this, t);
            card.StopClicked += (_, t) => TaskStopRequested?.Invoke(this, t);
            card.DoneClicked += (_, t) => TaskDoneRequested?.Invoke(this, t);
            card.DeleteClicked += (_, t) => TaskDeleteRequested?.Invoke(this, t);

            string colorHex = _settings?.ColorFor(task.Category) ?? "";
            card.RefreshCard(task, null, colorHex);

            _flowList.Controls.Add(card);
        }

        _flowList.ResumeLayout(true);
        _flowList.PerformLayout();
    }
}
