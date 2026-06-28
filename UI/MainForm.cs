using System.Drawing;
using System.Windows.Forms;

namespace TimeFlow.UI;

public partial class MainForm : Form
{
    // === Константы тёмной темы ===
    private static readonly Color COLOR_BACKGROUND = Color.FromArgb(30, 30, 30);      // #1E1E1E
    private static readonly Color COLOR_SURFACE = Color.FromArgb(45, 45, 45);         // #2D2D2D
    private static readonly Color COLOR_TEXT_PRIMARY = Color.FromArgb(220, 220, 220); // #DCDCDC
    private static readonly Color COLOR_TEXT_SECONDARY = Color.FromArgb(150, 150, 150);// #969696
    private static readonly Color COLOR_ACCENT = Color.FromArgb(76, 175, 80);         // #4CAF50
    private static readonly Color COLOR_BORDER = Color.FromArgb(60, 60, 60);          // #3C3C3C

    // Элементы каркаса 
    private TableLayoutPanel? _topBar;
    private SplitContainer? _mainSplit;
    private Panel? _leftPanelPlaceholder;
    private Panel? _rightPanelPlaceholder;

    public MainForm()
    {
        InitializeComponent();
        InitializeLayout();
        ApplyDarkTheme();
    }

    private void InitializeLayout()
    {
        // --- Верхняя панель (TopBar) ---
        _topBar = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 90,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = COLOR_SURFACE,
            Padding = new Padding(10, 0, 10, 0)
        };
        _topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
        _topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
        _topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));

        var lblTitle = new Label
        {
            Text = "TimeFlow",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = COLOR_TEXT_PRIMARY,
            AutoSize = true,
            Anchor = AnchorStyles.Left | AnchorStyles.Top,
            Margin = new Padding(0, 12, 0, 0)
        };
        _topBar.Controls.Add(lblTitle, 0, 0);

        // --- Основной разделитель (SplitContainer) ---
        _mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal, 
            SplitterWidth = 4,
            FixedPanel = FixedPanel.None,
            Panel1MinSize = 100,
            Panel2MinSize = 100
        };

        // Заглушки для панелей
        _leftPanelPlaceholder = new Panel { Dock = DockStyle.Fill, BackColor = COLOR_SURFACE };
        _rightPanelPlaceholder = new Panel { Dock = DockStyle.Fill, BackColor = COLOR_SURFACE };

        _mainSplit.Panel1.Controls.Add(_leftPanelPlaceholder);
        _mainSplit.Panel2.Controls.Add(_rightPanelPlaceholder);

        // Добавляем элементы на форму
        this.Controls.Add(_mainSplit);
        this.Controls.Add(_topBar);
        _topBar.BringToFront();
    }

	private void ApplyDarkTheme()
	{
		// Базовые цвета формы
		this.BackColor = COLOR_BACKGROUND;
		this.ForeColor = COLOR_TEXT_PRIMARY;
		this.Text = "TimeFlow";

		// Стили для SplitContainer
		if (_mainSplit != null)
		{
			
			_mainSplit.BackColor = COLOR_BORDER; 
			_mainSplit.Panel1.BackColor = COLOR_BACKGROUND;
			_mainSplit.Panel2.BackColor = COLOR_BACKGROUND;
		}

		// Цвета заглушек
		if (_leftPanelPlaceholder != null) 
			_leftPanelPlaceholder.BackColor = COLOR_SURFACE; // #2D2D2D

		if (_rightPanelPlaceholder != null) 
			_rightPanelPlaceholder.BackColor = COLOR_SURFACE; // #2D2D2D
	}
}