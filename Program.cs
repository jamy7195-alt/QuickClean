using System.Drawing;
using System.Windows.Forms;

namespace QuickClean;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Log(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log(e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => Log(e.Exception);

        // 下面几个初始化在某些受限环境可能失败：EnableVisualStyles() 需要往 %TEMP%
        // 写一个临时清单文件；若临时目录不可写会抛 UnauthorizedAccessException。
        // 这里逐项降级处理——视觉样式失败就退回经典外观，绝不让程序启动即崩溃。
        try { Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); } catch { }
        try { Application.EnableVisualStyles(); } catch { }
        try { Application.SetCompatibleTextRenderingDefault(false); } catch { }
        try { Application.SetDefaultFont(new Font("Microsoft YaHei UI", 9f)); } catch { }

        try
        {
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            Log(ex);
            try
            {
                MessageBox.Show("QuickClean 启动失败：\n\n" + ex.Message, "QuickClean",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }
    }

    /// <summary>未处理异常写日志，便于定位问题；日志失败不影响程序运行。</summary>
    private static void Log(Exception? ex)
    {
        if (ex is null) return;
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickClean");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "error.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}{Environment.NewLine}");
        }
        catch { }
    }
}
