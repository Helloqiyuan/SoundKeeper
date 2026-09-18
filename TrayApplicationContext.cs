using System.Windows.Forms;

namespace SoundKeeper;

/// <summary>
/// 纯托盘应用的生命周期宿主。
/// 不创建任何 Form，因此不会出现在任务栏，也不会有可见窗口。
///
/// 右键菜单：
///   ☑ 音频保活
///   ─────────────
///   ☑ 开机自动启动
///   ─────────────
///      退出
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu;

    private readonly ToolStripMenuItem _keepAliveItem;
    private readonly ToolStripMenuItem _autoStartItem;
    private readonly ToolStripMenuItem _exitItem;

    private readonly AudioKeepAlive _audio = new();

    private bool _lastAutoStartState;
    private Icon? _currentIcon;

    public TrayApplicationContext(bool silent)
    {
        _ = silent; // 纯托盘程序本就不显示窗口，此参数保留以明确自启语义。

        _keepAliveItem = new ToolStripMenuItem("音频保活")
        {
            CheckOnClick = false, // 手动控制勾选，避免与真实状态脱节
        };
        _keepAliveItem.Click += OnToggleKeepAlive;

        _autoStartItem = new ToolStripMenuItem("开机自动启动")
        {
            CheckOnClick = false,
        };
        _autoStartItem.Click += OnToggleAutoStart;

        _exitItem = new ToolStripMenuItem("退出");
        _exitItem.Click += OnExit;

        _menu = new ContextMenuStrip();
        _menu.Items.Add(_keepAliveItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(_autoStartItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(_exitItem);

        // 菜单每次展开前刷新勾选状态，保证显示与实际永远一致。
        _menu.Opening += (_, _) => RefreshMenuState();

        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = _menu,
            Visible = true,
            Text = "SoundKeeper — 蓝牙音频保活",
            Icon = TrayIconFactory.Stopped,
        };
        _currentIcon = _notifyIcon.Icon;

        _notifyIcon.DoubleClick += OnToggleKeepAlive;

        _audio.StateChanged += (_, _) => ApplyAudioStateToUi();

        // 记住上次状态：若上次是开启，则自动恢复保活。
        if (AppSettings.ReadEnabled())
        {
            TryStartAudio(showError: false);
        }

        ApplyAudioStateToUi();
        RefreshMenuState();
    }

    private void OnToggleKeepAlive(object? sender, EventArgs e)
    {
        if (_audio.IsRunning)
        {
            _audio.Stop();
            AppSettings.WriteEnabled(false);
        }
        else
        {
            if (TryStartAudio(showError: true))
            {
                AppSettings.WriteEnabled(true);
            }
        }

        ApplyAudioStateToUi();
    }

    private bool TryStartAudio(bool showError)
    {
        try
        {
            _audio.Start();
            return true;
        }
        catch (Exception ex)
        {
            if (showError)
            {
                _notifyIcon.BalloonTipTitle = "SoundKeeper";
                _notifyIcon.BalloonTipText = "启动音频保活失败，可能没有可用的音频输出设备。\r\n\r\n" + ex.Message;
                _notifyIcon.BalloonTipIcon = ToolTipIcon.Warning;
                _notifyIcon.ShowBalloonTip(4000);
            }

            return false;
        }
    }

    private void OnToggleAutoStart(object? sender, EventArgs e)
    {
        var target = !AppSettings.IsAutoStartEnabled();

        if (AppSettings.SetAutoStart(target))
        {
            _lastAutoStartState = target;
        }
        else
        {
            _notifyIcon.BalloonTipTitle = "SoundKeeper";
            _notifyIcon.BalloonTipText = "修改开机自启设置失败，请检查注册表权限。";
            _notifyIcon.BalloonTipIcon = ToolTipIcon.Warning;
            _notifyIcon.ShowBalloonTip(3000);
        }

        RefreshMenuState();
    }

    private void RefreshMenuState()
    {
        _keepAliveItem.Checked = _audio.IsRunning;

        var autoStartRegistered = AppSettings.IsAutoStartEnabled();
        _autoStartItem.Checked = autoStartRegistered;

        // 程序被移动位置后，注册表里的路径会失效，这里给出明确提示。
        if (autoStartRegistered && !AppSettings.IsAutoStartPathCurrent())
        {
            _autoStartItem.ToolTipText = "自启路径已失效（程序被移动过），请取消后重新勾选以刷新路径。";
        }
        else
        {
            _autoStartItem.ToolTipText = null;
        }

        _lastAutoStartState = autoStartRegistered;
    }

    private void ApplyAudioStateToUi()
    {
        var running = _audio.IsRunning;

        var icon = running ? TrayIconFactory.Running : TrayIconFactory.Stopped;
        if (!ReferenceEquals(_currentIcon, icon))
        {
            _notifyIcon.Icon = icon;
            _currentIcon = icon;
        }

        _notifyIcon.Text = running
            ? "SoundKeeper — 音频保活运行中"
            : "SoundKeeper — 已停止";

        _keepAliveItem.Checked = running;
    }

    private void OnExit(object? sender, EventArgs e)
    {
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // 停止音频并释放设备，让输出回到真正的空闲状态。
            try
            {
                _audio.Dispose();
            }
            catch
            {
                // 退出路径上忽略清理异常。
            }

            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _menu.Dispose();
        }

        base.Dispose(disposing);
    }
}
