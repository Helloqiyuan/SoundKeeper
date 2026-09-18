using Microsoft.Win32;

namespace SoundKeeper;

/// <summary>
/// 基于注册表的设置持久化。全部写在 HKCU 下，无需管理员权限、不弹 UAC。
///
/// 键位布局：
///   HKCU\Software\SoundKeeper
///       Enabled = 1 / 0                      音频保活开关（记住上次状态）
///   HKCU\Software\Microsoft\Windows\CurrentVersion\Run
///       SoundKeeper = "&lt;exe路径&gt;" --silent   开机自启（存在性即勾选状态）
/// </summary>
public static class AppSettings
{
    private const string AppKeyPath = @"Software\SoundKeeper";
    private const string EnabledValueName = "Enabled";

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "SoundKeeper";
    private const string SilentArgument = "--silent";

    /// <summary>读取上次的音频保活开关状态。读取失败时返回 false。</summary>
    public static bool ReadEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(AppKeyPath, writable: false);
            var raw = key?.GetValue(EnabledValueName);

            return raw switch
            {
                int i => i != 0,
                string s => s == "1" || string.Equals(s, "true", StringComparison.OrdinalIgnoreCase),
                _ => false,
            };
        }
        catch
        {
            return false;
        }
    }

    /// <summary>写入音频保活开关状态。</summary>
    public static void WriteEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(AppKeyPath, writable: true);
            key?.SetValue(EnabledValueName, enabled ? 1 : 0, RegistryValueKind.DWord);
        }
        catch
        {
            // 注册表不可写时静默降级：功能仍可用，只是不记忆状态。
        }
    }

    /// <summary>判断开机自启是否已注册（以 Run 键是否存在为准）。</summary>
    public static bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(RunValueName) != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 注册 / 注销开机自启。
    /// 写入当前 exe 的完整路径并附带 --silent，开机时静默进入托盘，不闪窗口。
    /// </summary>
    public static bool SetAutoStart(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

            if (key == null)
            {
                return false;
            }

            if (enabled)
            {
                var exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath))
                {
                    return false;
                }

                key.SetValue(RunValueName, $"\"{exePath}\" {SilentArgument}", RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(RunValueName, throwOnMissingValue: false);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 判断自启注册的路径是否与当前 exe 一致。
    /// 用于提示用户：程序被移动位置后需要重新勾选一次。
    /// </summary>
    public static bool IsAutoStartPathCurrent()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            if (key?.GetValue(RunValueName) is not string registered)
            {
                return false;
            }

            var current = Environment.ProcessPath;
            return !string.IsNullOrEmpty(current)
                   && registered.Contains(current, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
