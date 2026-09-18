using NAudio.Wave;

namespace SoundKeeper;

/// <summary>
/// 生成"极小但严格非零"的音频样本。
///
/// 为什么不能输出纯 0：
///   部分蓝牙协议栈 / 耳机固件会把"全零静音帧"判定为空闲（idle），
///   从而不重置休眠计时器。输出一个恒定但极小的非零值，
///   帧内数据非零，链路被判定为"有活跃音频流"，才能起到保活作用。
///
/// 为什么人耳听不到：
///   float 样本范围 [-1.0, 1.0]，默认幅度 1/32768 ≈ 0.0000305，
///   约合 -90 dBFS，远低于任何耳机可闻阈值。
/// </summary>
public sealed class TinySampleProvider : ISampleProvider
{
    /// <summary>默认幅度：1/32768，约 -90 dBFS。人耳完全不可闻，但严格非零。</summary>
    public const float DefaultAmplitude = 1f / 32768f;

    /// <summary>增强幅度：1/1000，约 -60 dBFS。用于对信号能量有要求的耳机固件。</summary>
    public const float StrongAmplitude = 1f / 1000f;

    private readonly float _amplitude;

    public WaveFormat WaveFormat { get; }

    public TinySampleProvider(WaveFormat waveFormat, float amplitude = DefaultAmplitude)
    {
        WaveFormat = waveFormat ?? throw new ArgumentNullException(nameof(waveFormat));
        _amplitude = amplitude;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        // 填满请求的样本数。返回 count 表示"永远不会播完"，
        // NAudio 会持续回调，从而保持音频会话长期存活。
        var end = offset + count;
        for (var i = offset; i < end; i++)
        {
            buffer[i] = _amplitude;
        }

        return count;
    }
}
