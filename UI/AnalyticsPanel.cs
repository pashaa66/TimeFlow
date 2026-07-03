using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using TimeFlow;

namespace TimeFlow.UI;

public class AnalyticsPanel : UserControl
{
    private static readonly Color COLOR_BACKGROUND = Color.FromArgb(30, 30, 30);
    private static readonly Color COLOR_SURFACE = Color.FromArgb(45, 45, 45);
    private static readonly Color COLOR_CARD = Color.FromArgb(38, 38, 38);
    private static readonly Color COLOR_TEXT_PRIMARY = Color.FromArgb(220, 220, 220);
    private static readonly Color COLOR_TEXT_SECONDARY = Color.FromArgb(150, 150, 150);
    private static readonly Color COLOR_ACCENT = Color.FromArgb(76, 175, 80);

    private TableLayoutPanel? _mainLayout;
    private Panel? _cardEarningsToday;
    private Panel? _cardEarningsMonth;
    private Panel? _cardActiveTask;
    private Label? _lblEarningsTodayValue;
    private Label? _lblEarningsMonthValue;
    private FlowLayoutPanel? _activeTasksFlow;
    private readonly List<string> _lastActiveTaskIds = new();
    private WeeklyChart? _weeklyChart;
    private CategoryPieChart? _pieChart;

    private SettingsStore? _settings;
    private string _currency = "₽";

    public AnalyticsPanel()
    {
        InitializeLayout();
    }

    public void BindSettings(SettingsStore settings)
    {
        _settings = settings;
        _currency = settings.Get("currency", "₽");
        UpdateEarningsText();
    }

    private void InitializeLayout()
    {
        this.Dock = DockStyle.Fill;
        this.BackColor = COLOR_BACKGROUND;
        this.AutoScroll = true;
        this.Padding = new Padding(15);

        _mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(0)
        };
        _mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var cardsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 100,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 15)
        };
        cardsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        cardsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        cardsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));

        _cardEarningsToday = CreateStatCard("💰 Заработано сегодня", $"0 {_currency}", out _lblEarningsTodayValue);
        _cardEarningsMonth = CreateStatCard("📅 За месяц", $"0 {_currency}", out _lblEarningsMonthValue);
        _cardActiveTask = CreateActiveTasksCard();

        cardsPanel.Controls.Add(_cardEarningsToday, 0, 0);
        cardsPanel.Controls.Add(_cardEarningsMonth, 1, 0);
        cardsPanel.Controls.Add(_cardActiveTask, 2, 0);

        _weeklyChart = new WeeklyChart { Dock = DockStyle.Top, Height = 220, Margin = new Padding(0, 0, 0, 15) };
        _pieChart = new CategoryPieChart { Dock = DockStyle.Top, Height = 250 };

        _mainLayout.Controls.Add(cardsPanel, 0, 0);
        _mainLayout.Controls.Add(_weeklyChart, 0, 1);
        _mainLayout.Controls.Add(_pieChart, 0, 2);

        this.Controls.Add(_mainLayout);
    }

    private Panel CreateStatCard(string title, string defaultValue, out Label lblValue)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = COLOR_CARD,
            Margin = new Padding(5),
            Padding = new Padding(15, 10, 15, 10)
        };

        var lblTitle = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 25,
            ForeColor = COLOR_TEXT_SECONDARY,
            Font = new Font("Segoe UI", 9, FontStyle.Regular)
        };

        lblValue = new Label
        {
            Text = defaultValue,
            Dock = DockStyle.Fill,
            ForeColor = COLOR_TEXT_PRIMARY,
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };

        card.Controls.Add(lblValue);
        card.Controls.Add(lblTitle);
        return card;
    }

    private Panel CreateActiveTasksCard()
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = COLOR_CARD,
            Margin = new Padding(5),
            Padding = new Padding(15, 10, 15, 10)
        };

        var lblTitle = new Label
        {
            Text = "Активные задачи:",
            Dock = DockStyle.Top,
            Height = 25,
            ForeColor = COLOR_TEXT_SECONDARY,
            Font = new Font("Segoe UI", 9, FontStyle.Regular)
        };

        _activeTasksFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = COLOR_CARD,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };

        _activeTasksFlow.ClientSizeChanged += ActiveTasksFlow_ClientSizeChanged;

        card.Controls.Add(_activeTasksFlow);
        card.Controls.Add(lblTitle);

        return card;
    }

    private void ActiveTasksFlow_ClientSizeChanged(object? sender, EventArgs e)
    {
        if (_activeTasksFlow == null) return;
        int maxW = Math.Max(20, _activeTasksFlow.ClientSize.Width - 4);
        foreach (Control c in _activeTasksFlow.Controls)
        {
            if (c is Label lbl && lbl.AutoSize)
            {
                lbl.MaximumSize = new Size(maxW, 0);
            }
        }
    }

    private void UpdateEarningsText()
    {

    }

    public void UpdateEarnings(double today, double month)
    {
        if (_lblEarningsTodayValue != null) _lblEarningsTodayValue.Text = $"{today:F0} {_currency}";
        if (_lblEarningsMonthValue != null) _lblEarningsMonthValue.Text = $"{month:F0} {_currency}";
    }

    public void UpdateActiveTask(List<TaskItem> allTasks)
    {
        if (_cardActiveTask == null || _activeTasksFlow == null) return;

        var activeTasks = allTasks.Where(t => t.Running).ToList();
        var newIds = activeTasks.Select(t => t.Id).ToList();
        bool sameSet = _lastActiveTaskIds.Count == newIds.Count
                       && !_lastActiveTaskIds.Except(newIds).Any();
        if (sameSet) return;

        _lastActiveTaskIds.Clear();
        _lastActiveTaskIds.AddRange(newIds);

        _activeTasksFlow.SuspendLayout();
        _activeTasksFlow.Controls.Clear();

        int maxW = Math.Max(20, _activeTasksFlow.ClientSize.Width - 4);

        if (activeTasks.Count == 0)
        {
            var lbl = new Label
            {
                Text = "Нет",
                AutoSize = false,
                Width = maxW,
                Height = 30,
                ForeColor = COLOR_TEXT_SECONDARY,
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 3, 0, 3)
            };
            _activeTasksFlow.Controls.Add(lbl);
        }
        else
        {
            foreach (var task in activeTasks)
            {

                var taskLabel = new Label
                {
                    Text = $"• {task.Name}",
                    AutoSize = true,
                    MaximumSize = new Size(maxW, 0),
                    ForeColor = COLOR_ACCENT,
                    Font = new Font("Segoe UI", 10, FontStyle.Regular),
                    Margin = new Padding(0, 3, 0, 3),
                    TextAlign = ContentAlignment.MiddleLeft
                };
                _activeTasksFlow.Controls.Add(taskLabel);
            }
        }

        _activeTasksFlow.ResumeLayout(true);
    }

    public void UpdateWeeklyChart(double[] secondsByWeekday)
    {
        _weeklyChart?.SetData(secondsByWeekday);
    }

    public void UpdateWeeklyChart(double[] secondsByWeekday, string[] dayLabels)
    {
        _weeklyChart?.SetData(secondsByWeekday, dayLabels);
    }

    public void UpdateWeeklyChart(double[] secondsByWeekday, string[] dayLabels, bool virtualTimeActive)
    {
        _weeklyChart?.SetData(secondsByWeekday, dayLabels, virtualTimeActive);
    }

    public void UpdateCategoryChart(Dictionary<string, double> secondsByCategory)
    {
        var coloredData = new List<(string Name, double Value, Color Color)>();
        foreach (var kv in secondsByCategory)
        {
            string hex = _settings?.ColorFor(kv.Key) ?? "#999999";
            Color color;
            try { color = ColorTranslator.FromHtml(hex); }
            catch { color = Color.Gray; }
            coloredData.Add((kv.Key, kv.Value, color));
        }
        _pieChart?.SetData(coloredData);
    }

    public void RefreshAll(double earningsToday, double earningsMonth, List<TaskItem> allTasks,
                           double[] secondsByWeekday, string[] dayLabels,
                           Dictionary<string, double> secondsByCategory,
                           bool virtualTimeActive = false)
    {
        UpdateEarnings(earningsToday, earningsMonth);
        UpdateActiveTask(allTasks);
        UpdateWeeklyChart(secondsByWeekday, dayLabels, virtualTimeActive);
        UpdateCategoryChart(secondsByCategory);
    }
}

public class WeeklyChart : Control
{
    private static readonly Color COLOR_BACKGROUND = Color.FromArgb(45, 45, 45);
    private static readonly Color COLOR_TEXT = Color.FromArgb(150, 150, 150);
    private static readonly Color COLOR_BAR = Color.FromArgb(76, 175, 80);
    private static readonly Color COLOR_GRID = Color.FromArgb(60, 60, 60);
    private static readonly Color COLOR_TODAY = Color.FromArgb(255, 152, 0);

    private double[] _data = new double[7];
    private string[] _dayLabels = { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс" };
    private int _todayIndex = 6;
    private bool _virtualTimeActive = false;

    public WeeklyChart()
    {
        this.DoubleBuffered = true;
        this.BackColor = COLOR_BACKGROUND;
        this.Padding = new Padding(55, 75, 25, 30);
    }

    public void SetData(double[] seconds)
    {
        if (seconds != null && seconds.Length == 7) _data = seconds;
        this.Invalidate();
    }

    public void SetData(double[] seconds, string[]? dayLabels, int todayIndex = 6)
    {
        if (seconds != null && seconds.Length == 7) _data = seconds;
        if (dayLabels != null && dayLabels.Length == 7) _dayLabels = dayLabels;
        _todayIndex = todayIndex;
        this.Invalidate();
    }

    public void SetData(double[] seconds, string[]? dayLabels, bool virtualTimeActive)
    {
        if (seconds != null && seconds.Length == 7) _data = seconds;
        if (dayLabels != null && dayLabels.Length == 7) _dayLabels = dayLabels;
        _virtualTimeActive = virtualTimeActive;
        this.Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        int chartLeft = this.Padding.Left;
        int chartTop = this.Padding.Top;
        int chartWidth = this.Width - this.Padding.Left - this.Padding.Right;
        int chartHeight = this.Height - this.Padding.Top - this.Padding.Bottom;

        using var titleFont = new Font("Segoe UI", 11, FontStyle.Bold);
        using var titleBrush = new SolidBrush(Color.FromArgb(220, 220, 220));
        g.DrawString("Часы по дням недели", titleFont, titleBrush, chartLeft, 15);

        double max = _data.Length > 0 ? _data.Max() : 0;
        if (max <= 0) max = 3600;

        using var gridPen = new Pen(COLOR_GRID, 1);
        using var labelFont = new Font("Segoe UI", 8);
        using var labelBrush = new SolidBrush(COLOR_TEXT);

        for (int i = 0; i <= 4; i++)
        {
            int y = chartTop + (int)(chartHeight * i / 4.0);
            g.DrawLine(gridPen, chartLeft, y, chartLeft + chartWidth, y);
            double hours = max * (4 - i) / 4.0 / 3600.0;
            string text = $"{hours:F2}ч";
            var size = g.MeasureString(text, labelFont);
            g.DrawString(text, labelFont, labelBrush, chartLeft - size.Width - 8, y - size.Height / 2);
        }

        int barCount = 7;
        float totalBarWidth = chartWidth / (float)barCount;
        float barWidth = totalBarWidth * 0.6f;
        float gap = totalBarWidth * 0.4f;

        using var barBrush = new SolidBrush(_virtualTimeActive ? COLOR_TODAY : COLOR_BAR);
        using var dayFont = new Font("Segoe UI", 9, FontStyle.Regular);
        using var dayBrush = new SolidBrush(COLOR_TEXT);

        for (int i = 0; i < barCount; i++)
        {
            float barHeight = (float)(_data[i] / max * chartHeight);
            float x = chartLeft + i * totalBarWidth + gap / 2;
            float y = chartTop + chartHeight - barHeight;

            var fillBrush = barBrush;

            var rect = new RectangleF(x, y, barWidth, barHeight);
            if (barHeight > 4)
            {
                using var path = CreateRoundedRect(rect, 4);
                g.FillPath(fillBrush, path);
            }
            else if (barHeight > 0)
            {
                g.FillRectangle(fillBrush, rect);
            }

            string dayLabel = _dayLabels[i];
            var daySize = g.MeasureString(dayLabel, dayFont);
            float dayX = x + (barWidth - daySize.Width) / 2;
            float dayY = chartTop + chartHeight + 5;
            g.DrawString(dayLabel, dayFont, dayBrush, dayX, dayY);

            if (_data[i] > 0)
            {
                string valText = $"{_data[i] / 3600.0:F2}";
                var valSize = g.MeasureString(valText, labelFont);
                g.DrawString(valText, labelFont, labelBrush, x + (barWidth - valSize.Width) / 2, y - valSize.Height - 2);
            }
        }
    }

    private GraphicsPath CreateRoundedRect(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddLine(rect.Right, rect.Bottom, rect.X, rect.Bottom);
        path.CloseFigure();
        return path;
    }
}

public class CategoryPieChart : Control
{
    private static readonly Color COLOR_BACKGROUND = Color.FromArgb(45, 45, 45);
    private static readonly Color COLOR_TEXT = Color.FromArgb(220, 220, 220);
    private static readonly Color COLOR_TEXT_SECONDARY = Color.FromArgb(150, 150, 150);

    private List<(string Name, double Value, Color Color)> _data = new();

    public CategoryPieChart()
    {
        this.DoubleBuffered = true;
        this.BackColor = COLOR_BACKGROUND;
    }

    public void SetData(List<(string Name, double Value, Color Color)> data)
    {
        _data = data ?? new();
        this.Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        using var titleFont = new Font("Segoe UI", 11, FontStyle.Bold);
        using var titleBrush = new SolidBrush(COLOR_TEXT);
        g.DrawString("Распределение по категориям", titleFont, titleBrush, 15, 8);

        if (_data.Count == 0 || _data.Sum(d => d.Value) <= 0)
        {
            using var emptyFont = new Font("Segoe UI", 10, FontStyle.Italic);
            using var emptyBrush = new SolidBrush(COLOR_TEXT_SECONDARY);
            g.DrawString("Нет данных", emptyFont, emptyBrush, this.Width / 2 - 40, this.Height / 2);
            return;
        }

        double total = _data.Sum(d => d.Value);

        int pieSize = Math.Min(this.Width / 2, this.Height - 60);
        int pieX = 40;
        int pieY = 50;
        var pieRect = new Rectangle(pieX, pieY, pieSize, pieSize);

        float startAngle = -90;
        foreach (var item in _data)
        {
            float sweep = (float)(item.Value / total * 360);
            using var brush = new SolidBrush(item.Color);
            g.FillPie(brush, pieRect, startAngle, sweep);
            startAngle += sweep;
        }

        int innerSize = (int)(pieSize * 0.55);
        int innerX = pieX + (pieSize - innerSize) / 2;
        int innerY = pieY + (pieSize - innerSize) / 2;
        g.FillEllipse(new SolidBrush(COLOR_BACKGROUND), innerX, innerY, innerSize, innerSize);

        using var centerFont = new Font("Segoe UI", 11, FontStyle.Bold);
        using var centerBrush = new SolidBrush(COLOR_TEXT);
        string totalText = $"{total / 3600.0:F2}ч";
        var centerSize = g.MeasureString(totalText, centerFont);
        g.DrawString(totalText, centerFont, centerBrush,
            innerX + (innerSize - centerSize.Width) / 2,
            innerY + (innerSize - centerSize.Height) / 2);

        int legendX = pieX + pieSize + 30;
        int legendY = pieY + 20;
        using var legendFont = new Font("Segoe UI", 10);
        using var legendBrush = new SolidBrush(COLOR_TEXT);
        using var percentFont = new Font("Segoe UI", 9);
        using var percentBrush = new SolidBrush(COLOR_TEXT_SECONDARY);

        foreach (var item in _data)
        {
            double percent = item.Value / total * 100;
            string hours = $"{item.Value / 3600.0:F2}ч";

            using var colorBrush = new SolidBrush(item.Color);
            g.FillRectangle(colorBrush, legendX, legendY, 14, 14);

            g.DrawString(item.Name, legendFont, legendBrush, legendX + 22, legendY - 2);

            string percentText = $"{percent:F0}% · {hours}";
            g.DrawString(percentText, percentFont, percentBrush, legendX + 22, legendY + 16);

            legendY += 45;
        }
    }
}
