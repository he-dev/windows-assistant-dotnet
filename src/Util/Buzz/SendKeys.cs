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
    public async Task<bool> ExecuteAsync(IntPtr hWnd, string sequence, string description, int? delayMs = null, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // util: Null, zero, and negative delay values mean immediate delivery.
            if (delayMs is > 0)
            {
                await Task.Delay(delayMs.Value, cancellationToken);
            }

            if (!Win32.IsWindow(hWnd))
            {
                logger.LogWarning("Keys '{Description}' not sent. Window no longer exists.", description);
                return false;
            }

            // core: Never inject input unless the intended window owns the foreground.
            if (!Win32.SetForegroundWindow(hWnd) || Win32.GetForegroundWindow() != hWnd)
            {
                logger.LogWarning("Keys '{Description}' not sent. Window not active.", description);
                return false;
            }

            System.Windows.Forms.SendKeys.SendWait(sequence);
            logger.LogInformation("Keys '{Description}' sent.", description);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception)
        {
            logger.LogError("Keys '{Description}' not sent. '{Error}'", description, exception.Message);
            return false;
        }
    }
}
