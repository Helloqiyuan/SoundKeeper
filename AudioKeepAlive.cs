using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace SoundKeeper;

/// <summary>
/// 音频保活核心：向默认输出设备持续推流极小非零样本。
///
/// 设计要点：
/// 1. 用设备 MixFormat 构造样本，避免 WASAPI 插入重采样层导致流间断。
/// 2. 共享模式输出，不影响其它程序正常播放声音。
/// 3. 监听 PlaybackStopped，在耳机断开/重连、默认设备切换后自动重建流。
/// </summary>
public sealed class AudioKeepAlive : IDisposable
{
    private readonly object _gate = new();

    private MMDeviceEnumerator? _enumerator;
    private WasapiOut? _output;
    private bool _wantRunning;
    private bool _disposed;

    /// <summary>当前是否正在输出音频流。</summary>
    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _output?.PlaybackState == PlaybackState.Playing;
            }
        }
    }

    /// <summary>状态发生变化（启动 / 停止 / 自动重启）时触发。</summary>
    public event EventHandler? StateChanged;

    /// <summary>启动保活。已启动则无操作。</summary>
    public void Start()
    {
        lock (_gate)
        {
            if (_disposed || _wantRunning)
            {
                return;
            }

            _wantRunning = true;
        }

        try
        {
            StartCore();
        }
        catch
        {
            lock (_gate)
            {
                _wantRunning = false;
            }

            throw;
        }

        RaiseStateChanged();
    }

    /// <summary>停止保活，彻底释放音频设备，让它回到空闲状态。</summary>
    public void Stop()
    {
        lock (_gate)
        {
            _wantRunning = false;
        }

        StopCore();
        RaiseStateChanged();
    }

    private void StartCore()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _enumerator ??= new MMDeviceEnumerator();

            // 默认输出设备（多媒体角色）。不指定具体设备，只作为"一个音频源"。
            var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

            // 必须用设备自身的混合格式，否则会产生重采样，可能造成流间断。
            // 注意：设备返回的常是 WaveFormatExtensible（32bit float 包在 extensible 容器里），
            // 而 WasapiOut 要求 provider 声明纯 IeeeFloat 格式，直接使用会抛
            // "Must be already floating point"。这里统一规整为纯浮点格式。
            var mixFormat = device.AudioClient.MixFormat;
            var format = WaveFormat.CreateIeeeFloatWaveFormat(mixFormat.SampleRate, mixFormat.Channels);

            var provider = new TinySampleProvider(format);

            var output = new WasapiOut(device, AudioClientShareMode.Shared, useEventSync: false, latency: 200);
            output.PlaybackStopped += OnPlaybackStopped;
            output.Init(provider);
            output.Play();

            _output = output;
        }
    }

    private void StopCore()
    {
        WasapiOut? output;
        lock (_gate)
        {
            output = _output;
            _output = null;
        }

        if (output == null)
        {
            return;
        }

        output.PlaybackStopped -= OnPlaybackStopped;

        try
        {
            output.Stop();
        }
        catch
        {
            // 设备已消失时 Stop 可能抛异常，忽略。
        }

        try
        {
            output.Dispose();
        }
        catch
        {
            // 同上。
        }
    }

    /// <summary>
    /// 播放意外停止（耳机断开、默认设备切换、设备被禁用等）时的自愈逻辑。
    /// 若用户仍希望保活，则稍后重取默认设备并重建流。
    /// </summary>
    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        bool shouldRestart;
        lock (_gate)
        {
            shouldRestart = _wantRunning && !_disposed;
        }

        if (!shouldRestart)
        {
            return;
        }

        // 设备切换存在短暂空窗期，稍等再重试，避免连续失败。
        _ = Task.Run(async () =>
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);

                lock (_gate)
                {
                    if (!_wantRunning || _disposed)
                    {
                        return;
                    }
                }

                try
                {
                    StopCore();
                    StartCore();
                    RaiseStateChanged();
                    return;
                }
                catch
                {
                    // 设备仍不可用，继续重试。
                }
            }
        });
    }

    private void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _wantRunning = false;
        }

        StopCore();

        lock (_gate)
        {
            _enumerator?.Dispose();
            _enumerator = null;
        }
    }
}
