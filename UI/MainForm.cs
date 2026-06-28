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

    public MainForm()
    {
        InitializeComponent();
        ApplyDarkTheme();
    }

    private void ApplyDarkTheme()
    {
        // Базовые цвета формы
        this.BackColor = COLOR_BACKGROUND;
        this.ForeColor = COLOR_TEXT_PRIMARY;
        
        // Заголовок окна 
        this.Text = "TimeFlow - Трекер задач и тайм-менеджмент"; 
        
    }
}