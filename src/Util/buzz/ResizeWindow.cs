using Serilog;
using WindowsAssistant.Meta;

namespace WindowsAssistant.Util.Buzz;

/// <summary>
/// Resizes a native window by independent width and height factors, then centers it on its current monitor.
/// </summary>
internal static class ResizeWindow
{
    private static readonly ILogger Logger = Log.ForContext(typeof(ResizeWindow));

    /// <summary>
    /// Returns <see langword="true"/> when the native resize succeeds.
    /// </summary>
    public static bool Execute(IntPtr hWnd, double widthFactor, double heightFactor)
    {
        // util: Keep Win32 sizing and monitor-coordinate calculations out of callers.
        if (!Win32.GetWindowRect(hWnd, out var windowRect))
        {
            Logger.Warning("Could not read the bounds of window {WindowHandle}", hWnd);
            return false;
        }

        var newWidth = (int)(windowRect.Width * widthFactor);
        var newHeight = (int)(windowRect.Height * heightFactor);
        var screenBounds = Screen.FromHandle(hWnd).WorkingArea;
        var newX = screenBounds.Left + (screenBounds.Width - newWidth) / 2;
        var newY = screenBounds.Top + (screenBounds.Height - newHeight) / 2;

        var resized = Win32.SetWindowPos(hWnd, IntPtr.Zero, newX, newY, newWidth, newHeight, 0);
        Logger.Information("Resize of window {WindowHandle} to {Width}x{Height} completed with result {Result}", hWnd, newWidth, newHeight, resized);
        return resized;
    }
}
