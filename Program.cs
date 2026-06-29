using System;
using System.Windows.Forms;
using TimeFlow.UI;

static class Program
{
    [STAThread]
    static void Main()
    {
        // Перехватываем все скрытые ошибки WinForms
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (s, e) => 
        {
            MessageBox.Show(e.Exception.ToString(), "Ошибка UI потока", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.Exit(1);
        };
        AppDomain.CurrentDomain.UnhandledException += (s, e) => 
        {
            MessageBox.Show(e.ExceptionObject.ToString(), "Критическая ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.Exit(1);
        };

        try
        {
            ApplicationConfiguration.Initialize();
            Config.EnsureDirs();
            var settings = new SettingsStore();
            var clock = new VirtualClock();
            var tasks = new TaskManager(settings, clock);
            var pomodoro = new PomodoroTimer(settings);
            clock.Start();
            Application.Run(new MainForm(settings, clock, tasks, pomodoro));
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "Ошибка запуска", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}