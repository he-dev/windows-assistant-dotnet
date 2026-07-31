using WindowsAssistant.Core;

namespace WindowsAssistant.Util.Buzz;

/// <summary>
/// Adapts configured key sequences to the plain-parameter send-keys buzz.
/// </summary>
internal static class SendKeysExtensions
{
    public static async Task ExecuteAsync(this SendKeys sendKeys, IntPtr hWnd, IEnumerable<SendKeysOptions>? sendKeysOptions, CancellationToken cancellationToken = default)
    {
        foreach (var keys in sendKeysOptions ?? [])
        {
            if (!await sendKeys.ExecuteAsync(hWnd, keys.Sequence, keys.Description, keys.DelayMs, cancellationToken))
            {
                break;
            }
        }
    }
}
