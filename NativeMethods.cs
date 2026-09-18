using System.Runtime.InteropServices;

namespace SoundKeeper;

/// <summary>
/// 少量 Win32 互操作。目前只用于释放 Bitmap.GetHicon() 返回的图标句柄，
/// 避免 GDI 对象泄漏。
/// </summary>
internal static class NativeMethods
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyIcon(IntPtr hIcon);
}
