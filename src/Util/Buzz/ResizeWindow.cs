using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using WindowsAssistant.Meta;

namespace WindowsAssistant.Util.Buzz;

/// <summary>
/// Resizes a native window by independent width and height factors, then centers it on its current monitor.
/// </summary>
internal sealed class ResizeWindow(ILogger<ResizeWindow> logger)
{
    /// <summary>
    /// Returns <see langword="true"/> when the native resize succeeds.
    /// </summary>
    public bool By(IntPtr hWnd, double widthFactor, double heightFactor)
    {
        // util: Keep Win32 sizing and monitor-coordinate calculations out of callers.
        if (!Win32.GetWindowRect(hWnd, out var windowRect))
        {
            logger.LogWarning("Window not resized. Bounds unavailable.");
            return false;
        }

        var newWidth = (int)(windowRect.Width * widthFactor);
        var newHeight = (int)(windowRect.Height * heightFactor);
        var screenBounds = Screen.FromHandle(hWnd).WorkingArea;
        var newX = screenBounds.Left + (screenBounds.Width - newWidth) / 2;
        var newY = screenBounds.Top + (screenBounds.Height - newHeight) / 2;

        var resized = Win32.SetWindowPos(hWnd, IntPtr.Zero, newX, newY, newWidth, newHeight, 0);
        var errorCode = resized ? 0 : Marshal.GetLastWin32Error();
        if (resized)
        {
            logger.LogInformation("Window resized by '{WidthFactor:0.##}×{HeightFactor:0.##}' to '{Width}×{Height}'.", widthFactor, heightFactor, newWidth, newHeight);
        }
        else
        {
            var error = new Win32Exception(errorCode);
            logger.LogError("Window not resized. '{Error}' Error code '{ErrorCode}'.", error.Message, error.NativeErrorCode);
        }

        return resized;
    }
}
