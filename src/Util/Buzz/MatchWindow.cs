using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.RegularExpressions;
using WindowsAssistant.Core;
using WindowsAssistant.Meta;

namespace WindowsAssistant.Util.Buzz;

/// <summary>
/// Reads native window information and matches its title against the configured rules.
/// </summary>
internal sealed class MatchWindow(ILogger<MatchWindow> logger, IOptions<WindowEventOptions> options)
{
    private readonly IReadOnlyList<ObjectCreateOptions> options = options.Value.ObjectCreate;

    public ObjectCreateOptions? Execute(IntPtr hWnd, int idObject)
    {
        // core: Only native window objects can represent configured targets.
        if (hWnd == IntPtr.Zero || idObject != Win32.OBJID_WINDOW)
        {
            return null;
        }

        var title = new StringBuilder(256);
        if (Win32.GetWindowText(hWnd, title, title.Capacity) == 0)
        {
            return null;
        }

        // core: The first matching rule owns the window.
        foreach (var option in options)
        {
            try
            {
                if (option.TitleRegex.IsMatch(title.ToString()))
                {
                    logger.LogInformation("Matched '{WindowTitle}'. Rule '{TitlePattern}'.", title.ToString(), option.TitlePattern);
                    return option;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                logger.LogWarning("Rule '{TitlePattern}' timed out.", option.TitlePattern);
            }
            catch (ArgumentException exception)
            {
                logger.LogWarning("Rule '{TitlePattern}' skipped. '{Error}'", option.TitlePattern, exception.Message);
            }
        }

        return null;
    }
}
