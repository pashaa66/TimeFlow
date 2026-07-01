using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using TimeFlow;

namespace TimeFlow.UI;

public class HistoryForm : Form
{
    private static readonly Color COLOR_BACKGROUND = Color.FromArgb(30, 30, 30);
    private static readonly Color COLOR_SURFACE = Color.FromArgb(45, 45, 45);
    private static readonly Color COLOR_TEXT_PRIMARY = Color.FromArgb(220, 220, 220);
    private static readonly Color COLOR_TEXT_SECONDARY = Color.FromArgb(150, 150, 150);
    private static readonly Color COLOR_ACCENT = Color.FromArgb(76, 175, 80);
    private static readonly Color COLOR_BORDER = Color.FromArgb(60, 60, 60);

    private static readonly Color STATUS_DONE = Color.FromArgb(76, 175, 80);
    private static readonly Color STATUS_DELETED = Color.FromArgb(244, 67, 54);
    private static readonly Color STATUS_ACTIVE = Color.FromArgb(255, 152, 0);

    private ComboBox? _cmbFilter;
    private FlowLayoutPanel? _flowList;
    private Label? _lblEmptyState;
    private CategoryPieChart? _pieChart;
    private SplitContainer? _rootSplit;

    private SettingsStore? _settings;
    private List<HistoryRecord> _allRecords = new();

    public HistoryForm(SettingsStore settings, List<HistoryRecord> records)
    {
        _settings = settings;
        _allRecords = records ?? new List<HistoryRecord>();

        InitializeLayout();
        ApplyTheme();
        RefreshData("Все категории");
    }

    private void InitializeLayout()
    {
        this.Text = "История задач";
        this.Size = new Size(1100, 650);
        this.StartPosition = FormStartPosition.CenterParent;
        this.BackColor = COLOR_BACKGROUND;
        this.MinimumSize = new Size(720, 450);
        _rootSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            BackColor = COLOR_BORDER,
            SplitterWidth = 4
        };

        var topPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = COLOR_BACKGROUND
        };
        topPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
        topPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var toolbar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = COLOR_SURFACE,
            Padding = new Padding(10, 6, 10, 6)
        };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180F));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        var lblTitle = new Label
        {
            Text = "📋 История",
            ForeColor = COLOR_TEXT_PRIMARY,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 0),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _cmbFilter = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = COLOR_BACKGROUND,
            ForeColor = COLOR_TEXT_PRIMARY,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9)
        };
        _cmbFilter.SelectedIndexChanged += (_, _) =>
            RefreshData(_cmbFilter.SelectedItem?.ToString() ?? "Все категории");

        toolbar.Controls.Add(lblTitle, 0, 0);
        toolbar.Controls.Add(_cmbFilter, 1, 0);

        _flowList = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = COLOR_BACKGROUND,
            Padding = new Padding(10, 10, 10, 10)
        };

        _flowList.ClientSizeChanged += (_, _) => UpdateCardWidths();

        _lblEmptyState = new Label
        {
            Text = "Нет записей в истории",
            AutoSize = true,
            ForeColor = Color.FromArgb(120, 120, 120),
            Font = new Font("Segoe UI", 11, FontStyle.Italic),
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(20, 40, 20, 0)
        };

        topPanel.Controls.Add(toolbar, 0, 0);
        topPanel.Controls.Add(_flowList, 0, 1);

        _pieChart = new CategoryPieChart
        {
            Dock = DockStyle.Fill,
            BackColor = COLOR_SURFACE
        };

        _rootSplit.Panel1.Controls.Add(topPanel);
        _rootSplit.Panel2.Controls.Add(_pieChart);

        this.Controls.Add(_rootSplit);
        this.Load += OnFormLoad;
    }

    private void OnFormLoad(object? sender, EventArgs e)
    {
        if (_rootSplit == null) return;

        try
        {
            int sw = _rootSplit.SplitterWidth;
            int w = _rootSplit.Width;

            if (w <= 0)
            {
                return;
            }

            int min1 = Math.Min(300, Math.Max(80, w / 3));
            int min2 = Math.Min(360, Math.Max(120, w / 3));

            while (min1 + min2 + sw > w && min1 > 50 && min2 > 50)
            {
                min1 = Math.Max(50, min1 - 20);
                min2 = Math.Max(50, min2 - 20);
            }

            _rootSplit.Panel1MinSize = min1;
            _rootSplit.Panel2MinSize = min2;

            int maxDistance = Math.Max(min1, w - min2 - sw);
            int desired = Math.Max(min1, w * 55 / 100);
            int distance = Math.Max(min1, Math.Min(desired, maxDistance));
            _rootSplit.SplitterDistance = distance;
        }
        catch (InvalidOperationException)
        {

        }

        UpdateCardWidths();
    }

    private void UpdateCardWidths()
    {
        if (_flowList == null) return;
        int w = Math.Max(100, _flowList.ClientSize.Width - 25);
        foreach (Control c in _flowList.Controls)
        {

            if (c == _lblEmptyState) continue;
            c.Width = w;
        }
    }

    private void ApplyTheme()
    {
        if (_cmbFilter != null)
        {
            _cmbFilter.Items.Clear();
            _cmbFilter.Items.Add("Все категории");

            var cats = new HashSet<string>(StringComparer.Ordinal);
            if (_settings != null)
            {
                foreach (var c in _settings.Categories().Keys) cats.Add(c);
            }
            foreach (var r in _allRecords)
            {
                if (!string.IsNullOrEmpty(r.Category)) cats.Add(r.Category);
            }

            foreach (var cat in cats.OrderBy(c => c, StringComparer.Ordinal))
                _cmbFilter.Items.Add(cat);
            _cmbFilter.SelectedIndex = 0;
        }
    }

    private void RefreshData(string filter)
    {
        if (_flowList == null || _pieChart == null || _lblEmptyState == null) return;

        _flowList.SuspendLayout();
        _flowList.Controls.Clear();

        var filtered = filter == "Все категории"
            ? _allRecords
            : _allRecords.FindAll(r => r.Category == filter);

        filtered.Sort((a, b) => string.Compare(b.SortDate(), a.SortDate(), StringComparison.Ordinal));

        if (filtered.Count == 0)
        {
            _lblEmptyState.Visible = true;
            _flowList.Controls.Add(_lblEmptyState);
        }
        else
        {
            _lblEmptyState.Visible = false;
            foreach (var record in filtered)
            {
                var item = CreateHistoryItem(record);
                _flowList.Controls.Add(item);
            }
        }

        _flowList.ResumeLayout(true);

        var byCategory = new Dictionary<string, double>();
        foreach (var r in filtered)
            byCategory[r.Category] = byCategory.GetValueOrDefault(r.Category, 0) + r.TotalSeconds;

        var coloredData = new List<(string Name, double Value, Color Color)>();
        foreach (var kv in byCategory)
        {
            string hex = _settings?.ColorFor(kv.Key) ?? "#999999";
            try { coloredData.Add((kv.Key, kv.Value, ColorTranslator.FromHtml(hex))); }
            catch { coloredData.Add((kv.Key, kv.Value, Color.Gray)); }
        }
        _pieChart.SetData(coloredData);
    }

    private Panel CreateHistoryItem(HistoryRecord record)
    {

        var panel = new Panel
        {
            Height = 84,
            Width = _flowList!.ClientSize.Width - 25,
            BackColor = COLOR_SURFACE,
            Margin = new Padding(0, 0, 0, 8),
            Padding = new Padding(0)
        };

        Color statusColor = record.Status switch
        {
            "done" => STATUS_DONE,
            "deleted" => STATUS_DELETED,
            _ => STATUS_ACTIVE
        };

        var statusBar = new Panel
        {
            Dock = DockStyle.Left,
            Width = 4,
            BackColor = statusColor
        };

        string statusIcon = record.Status switch
        {
            "done" => "✓",
            "deleted" => "🗑",
            _ => "▶"
        };

        string statusText = record.Status switch
        {
            "done" => "DONE",
            "deleted" => "DELETED",
            _ => "ACTIVE"
        };

        var lblInfo = new Label
        {
            Dock = DockStyle.Fill,
            Text = $"{statusIcon} {record.Name}  •  {TimeSpan.FromSeconds((int)record.TotalSeconds):hh\\:mm\\:ss}  •  {statusText}",
            ForeColor = COLOR_TEXT_PRIMARY,
            Font = new Font("Segoe UI", 10),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0)
        };

        string catColorHex = _settings?.ColorFor(record.Category) ?? "#999999";
        Color catColor;
        try { catColor = ColorTranslator.FromHtml(catColorHex); }
        catch { catColor = Color.Gray; }

        var lblCategory = new Label
        {
            AutoSize = true,
            BackColor = catColor,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            Text = $"  {record.Category}  ",
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 0, 0, 0)
        };

        var lblCreated = new Label
        {
            AutoSize = true,
            ForeColor = COLOR_TEXT_SECONDARY,
            Font = new Font("Segoe UI", 8, FontStyle.Regular),
            Text = "Дата создания: " + FormatCreatedAt(record.CreatedAt),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 2, 0, 0),
            BackColor = COLOR_SURFACE
        };

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = COLOR_SURFACE,
            Margin = new Padding(0),
            Padding = new Padding(10, 4, 10, 4)
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));  // info
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));  // category
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));  // created (НОВОЕ)

        content.Controls.Add(lblInfo, 0, 0);
        content.Controls.Add(lblCategory, 0, 1);
        content.Controls.Add(lblCreated, 0, 2);

        panel.Controls.Add(content);
        panel.Controls.Add(statusBar);

        panel.Width = Math.Max(100, _flowList!.ClientSize.Width - 25);

        return panel;
    }

    private static string FormatCreatedAt(string? createdAt)
    {
        if (string.IsNullOrEmpty(createdAt)) return "--";
        if (DateTime.TryParse(createdAt, out var dt))
            return dt.ToString("dd.MM.yyyy, HH:mm:ss");
        return createdAt;
    }
}
