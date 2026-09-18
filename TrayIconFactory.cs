using System.Drawing;
using System.Drawing.Drawing2D;

namespace SoundKeeper;

/// <summary>
/// 用代码绘制托盘图标，避免依赖外部 .ico 资源文件。
/// 两态：
///   运行中 = 绿色实心圆（带轻微光晕）
///   已停止 = 灰色实心圆
/// </summary>
internal static class TrayIconFactory
{
    private static readonly Color RunningColor = Color.FromArgb(46, 204, 113);
    private static readonly Color StoppedColor = Color.FromArgb(150, 150, 150);

    private static Icon? _running;
    private static Icon? _stopped;

    /// <summary>运行中图标（绿色）。</summary>
    public static Icon Running => _running ??= CreateCircleIcon(RunningColor);

    /// <summary>已停止图标（灰色）。</summary>
    public static Icon Stopped => _stopped ??= CreateCircleIcon(StoppedColor);

    private static Icon CreateCircleIcon(Color color)
    {
        // 32x32 覆盖托盘常见的高 DPI 缩放，保证清晰。
        const int size = 32;
        using var bitmap = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            // 外圈：稍暗的同色描边，提升在浅色/深色任务栏上的辨识度。
            var ringRect = new Rectangle(1, 1, size - 3, size - 3);
            using (var ringPen = new Pen(Color.FromArgb(160, color), 2f))
            {
                g.DrawEllipse(ringPen, ringRect);
            }

            // 内芯：实心圆。
            var coreRect = new Rectangle(6, 6, size - 13, size - 13);
            using var coreBrush = new SolidBrush(color);
            g.FillEllipse(coreBrush, coreRect);

            // 高光：左上角一点亮色，让图标有立体感。
            var highlightRect = new Rectangle(10, 9, 7, 7);
            using (var highlightBrush = new SolidBrush(Color.FromArgb(90, Color.White)))
            {
                g.FillEllipse(highlightBrush, highlightRect);
            }
        }

        // 从 Bitmap 句柄创建 Icon，创建后即可释放 Bitmap。
        var hIcon = bitmap.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(hIcon);
            return (Icon)temp.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(hIcon);
        }
    }
}
