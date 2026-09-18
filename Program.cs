using System.Windows.Forms;

namespace SoundKeeper;

internal static class Program
{
    /// <summary>
    /// 程序入口。
    /// --silent  ：由开机自启注册表项传入，表示静默进入托盘（纯托盘程序本就无窗口）。
    /// --selftest：自检模式。尝试启动音频保活并输出结果，然后退出。用于部署验证。
    /// 另外使用"单实例互斥"：重复启动时直接退出，避免托盘出现多个图标。
    /// </summary>
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Any(a => string.Equals(a, "--selftest", StringComparison.OrdinalIgnoreCase)))
        {
            RunSelfTest();
            return;
        }

        // 单实例保护。
        using var mutex = new Mutex(initiallyOwned: true, name: @"Local\SoundKeeper.SingleInstance", out var isNew);
        if (!isNew)
        {
            return;
        }

        ApplicationConfiguration.Initialize();

        var silent = args.Any(a => string.Equals(a, "--silent", StringComparison.OrdinalIgnoreCase));

        Application.Run(new TrayApplicationContext(silent));
    }

    /// <summary>
    /// 自检：验证能否取到默认音频设备、能否用其 MixFormat 建立推流。
    /// 结果写入 exe 同目录的 selftest.log，便于部署时确认音频链路可用。
    /// </summary>
    private static void RunSelfTest()
    {
        var log = new List<string> { $"SoundKeeper self-test @ {DateTime.Now:yyyy-MM-dd HH:mm:ss}" };

        try
        {
            using var audio = new AudioKeepAlive();
            audio.Start();

            log.Add($"AudioKeepAlive.IsRunning = {audio.IsRunning}");

            // 再验证一次注册表读写的往返。
            var original = AppSettings.ReadEnabled();
            AppSettings.WriteEnabled(true);
            var afterWrite = AppSettings.ReadEnabled();
            AppSettings.WriteEnabled(original);
            log.Add($"Registry roundtrip: original={original}, afterWrite={afterWrite}, restored={AppSettings.ReadEnabled()}");

            log.Add($"AutoStart registered = {AppSettings.IsAutoStartEnabled()}");
            log.Add("RESULT: OK");

            Thread.Sleep(1500);
            audio.Stop();
            log.Add($"After Stop, IsRunning = {audio.IsRunning}");
        }
        catch (Exception ex)
        {
            log.Add($"RESULT: FAILED — {ex.GetType().Name}: {ex.Message}");
        }

        var path = Path.Combine(AppContext.BaseDirectory, "selftest.log");
        try
        {
            File.WriteAllLines(path, log);
        }
        catch
        {
            // 自检日志写不出也不影响主程序。
        }
    }
}
