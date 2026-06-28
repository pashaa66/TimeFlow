using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using TimeFlow;

namespace TimeFlow.UI;

public class TasksPanel : UserControl
{
    // Цвета темы
    private static readonly Color COLOR_SURFACE = Color.FromArgb(45, 45, 45);
    private static readonly Color COLOR_TEXT_PRIMARY = Color.FromArgb(220, 220, 220);
    private static readonly Color COLOR_ACCENT = Color.FromArgb(76, 175, 80);
    private static readonly Color COLOR_BORDER = Color.FromArgb(60, 60, 60);

    // Элементы UI
    private Panel _toolbar;
    private Button _btnAdd;
    private ComboBox _cmbFilter;
    private FlowLayoutPanel _flowList;
    private Label _lblEmptyState;

    // Данные
    private List<TaskItem> _tasks = new();
    private SettingsStore? _settings; 

    // События для MainForm
    public event EventHandler<TaskItem>? TaskStartRequested;
    public event EventHandler<TaskItem>? TaskStopRequested;
    public event EventHandler<TaskItem>? TaskDoneRequested;
    public event EventHandler<TaskItem>? TaskDeleteRequested;
    public event EventHandler? AddTaskRequested;

    public TasksPanel()
    {
        InitializeLayout();
        ApplyTheme();
    }

    public void BindSettings(SettingsStore settings)
    {
        _settings = settings;
        RefreshFilters();
        RefreshList(); 
    }

    public void SetTasks(List<TaskItem> tasks)
    {
        _tasks = tasks ?? new List<TaskItem>();
        RefreshList();
    }

	public void UpdateTimers(VirtualClock? clock = null)
	{
		foreach (Control ctrl in _flowList.Controls)
		{
			if (ctrl is TaskCard card && card.CurrentTask != null)
			{
				string colorHex = _settings?.ColorFor(card.CurrentTask.Category);
				card.RefreshCard(card.CurrentTask, clock, colorHex);
			}
		}
	}

    private void InitializeLayout()
    {
        this.Dock = DockStyle.Fill;
        this.BackColor = COLOR_SURFACE;

        // 1. Toolbar (верхняя панель)
        _toolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 48,
            BackColor = COLOR_SURFACE,
            Padding = new Padding(10, 6, 10, 6)
        };

        _btnAdd = new Button
        {
            Text = "+ Новая задача",
            Dock = DockStyle.Left,
            Width = 140,
            FlatStyle = FlatStyle.Flat,
            BackColor = COLOR_ACCENT,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnAdd.Click += (_, _) => AddTaskRequested?.Invoke(this, EventArgs.Empty);

        _cmbFilter = new ComboBox
        {
            Dock = DockStyle.Right,
            Width = 180,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = COLOR_TEXT_PRIMARY,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9)
        };
        _cmbFilter.SelectedIndexChanged += (_, _) => RefreshList();

        _toolbar.Controls.Add(_cmbFilter);
        _toolbar.Controls.Add(_btnAdd);

        // 2. FlowLayoutPanel (список карточек)
        _flowList = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = COLOR_SURFACE,
            Padding = new Padding(0)
        };

        // Пустое состояние
        _lblEmptyState = new Label
        {
            Text = "Нет задач. Нажмите «+ Новая задача»",
            AutoSize = true,
            ForeColor = Color.FromArgb(100, 100, 100),
            Font = new Font("Segoe UI", 11, FontStyle.Italic),
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        };
        _flowList.Controls.Add(_lblEmptyState);

        this.Controls.Add(_flowList);
        this.Controls.Add(_toolbar);
        _toolbar.BringToFront();
    }

    private void ApplyTheme()
    {
        this.BackColor = COLOR_SURFACE;
        _flowList.BackColor = COLOR_SURFACE;
    }

    private void RefreshFilters()
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
            // если настройки ещё не загружены
            _cmbFilter.Items.AddRange(new object[] { "Учёба", "Работа", "Отдых" });
        }
        _cmbFilter.SelectedIndex = 0;
    }

    private void RefreshList()
    {
        _flowList.Controls.Clear();
        _flowList.Controls.Add(_lblEmptyState); 

        string filter = _cmbFilter.SelectedItem?.ToString() ?? "Все категории";
        bool showAll = filter == "Все категории";

        var filtered = showAll 
            ? _tasks 
            : _tasks.FindAll(t => t.Category == filter);

        // Сортировка
        filtered.Sort((a, b) => 
        {
            if (a.Running && !b.Running) return -1;
            if (!a.Running && b.Running) return 1;
            return string.Compare(b.CreatedAt, a.CreatedAt);
        });

        if (filtered.Count == 0)
        {
            _lblEmptyState.Visible = true;
            return;
        }

        _lblEmptyState.Visible = false;

        foreach (var task in filtered)
        {
            var card = new TaskCard();
            
            // Подписка на события карточки
            card.StartClicked += (_, t) => TaskStartRequested?.Invoke(this, t);
            card.StopClicked += (_, t) => TaskStopRequested?.Invoke(this, t);
            card.DoneClicked += (_, t) => TaskDoneRequested?.Invoke(this, t);
            card.DeleteClicked += (_, t) => TaskDeleteRequested?.Invoke(this, t);

            // Получаем цвет категории
            string colorHex = _settings?.ColorFor(task.Category);
            card.RefreshCard(task, null, colorHex);

            _flowList.Controls.Add(card);
        }
    }

    // Вспомогательный метод для поиска задачи по контролу
    private string GetTaskIdFromCard(TaskCard card)
    {

        return ""; 
    }
}