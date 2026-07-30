using Serilog;
using WindowsAssistant.Meta;

namespace WindowsAssistant.Util.Buzz;

/// <summary>
/// Delays and sends a key sequence to a native window without depending on configuration objects.
/// </summary>
internal static class SendKeys
{
    private static readonly ILogger Logger = Log.ForContext(typeof(SendKeys));

    /// <summary>
    /// Sends the sequence after an optional delay and returns whether it reached the intended foreground window.
    /// </summary>
    public static async Task<bool> ExecuteAsync(IntPtr hWnd, string sequence, int? delayMs = null)
    {
        // util: Null, zero, and negative delay values mean immediate delivery.
        if (delayMs is > 0)
        {
            await Task.Delay(delayMs.Value);
        }

        if (!Win32.IsWindow(hWnd))
        {
            Logger.Warning("Could not send keys because window {WindowHandle} no longer exists", hWnd);
            return false;
        }

        // core: Never inject input unless the intended window owns the foreground.
        if (!Win32.SetForegroundWindow(hWnd) || Win32.GetForegroundWindow() != hWnd)
        {
            Logger.Warning("Could not send keys because window {WindowHandle} did not become the foreground window", hWnd);
            return false;
        }

        System.Windows.Forms.SendKeys.SendWait(sequence);
        Logger.Information("Sent key sequence {Sequence} to window {WindowHandle}", sequence, hWnd);
        return true;
    }
}
