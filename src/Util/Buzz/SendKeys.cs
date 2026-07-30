using Microsoft.Extensions.Logging;
using WindowsAssistant.Meta;

namespace WindowsAssistant.Util.Buzz;

/// <summary>
/// Delays and sends a key sequence to a native window without depending on configuration objects.
/// </summary>
internal sealed class SendKeys(ILogger<SendKeys> logger)
{
    /// <summary>
    /// Sends the sequence after an optional delay and returns whether it reached the intended foreground window.
    /// </summary>
    public async Task<bool> ExecuteAsync(IntPtr hWnd, string sequence, int? delayMs = null)
    {
        try
        {
            // util: Null, zero, and negative delay values mean immediate delivery.
            if (delayMs is > 0)
            {
                await Task.Delay(delayMs.Value);
            }

            if (!Win32.IsWindow(hWnd))
            {
                logger.LogWarning("Keys '{Sequence}' not sent. Window no longer exists.", sequence);
                return false;
            }

            // core: Never inject input unless the intended window owns the foreground.
            if (!Win32.SetForegroundWindow(hWnd) || Win32.GetForegroundWindow() != hWnd)
            {
                logger.LogWarning("Keys '{Sequence}' not sent. Window not active.", sequence);
                return false;
            }

            System.Windows.Forms.SendKeys.SendWait(sequence);
            logger.LogInformation("Keys '{Sequence}' sent.", sequence);
            return true;
        }
        catch (Exception exception)
        {
            logger.LogError("Keys '{Sequence}' not sent. '{Error}'", sequence, exception.Message);
            return false;
        }
    }
}
