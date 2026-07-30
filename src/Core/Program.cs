using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using WindowsAssistant.Core.Views;

using Serilog;
using WindowsAssistant.Util.Serilog;

namespace WindowsAssistant.Core;

internal class Program
{
    [STAThread]
    private static void Main()
    {
        // meta: Render controls at each monitor's native DPI instead of bitmap-scaling the form.
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        // meta: Configuration is bound once at startup; restart the utility to apply changes.
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        var objectCreateOptions =
            configuration
                .GetSection("EVENT:OBJECT_CREATE")
                .Get<IReadOnlyList<ObjectCreateOptions>>()
            ?? [];

        // meta: WinEvent hooks require a Windows message loop, which this form provides.
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var hookForm = new HookForm(objectCreateOptions)
        {
            Visible = true,
            // StartPosition = FormStartPosition.CenterScreen // Does not work.
        };

        // meta: Configure logging once after the form has created its log control.
        Log.Logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Sink(new TextBoxSink(hookForm.LogTextBox)).CreateLogger();

        try
        {
            Log.Information("Listening for window events...");
            Application.Run(hookForm);
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}

// Cannot use primary constructor because of the nullable SendKeys. It breaks deserialization.
public record ObjectCreateOptions
{
    public string TitlePattern { get; init; } = null!;
    public SizeFactorOptions SizeFactor { get; init; } = null!;
    public IEnumerable<SendKeysOptions>? SendKeys { get; init; }

    /// <summary>
    /// Gets the configured title pattern as a compiled, timeout-limited regular expression.
    /// The expression is created once and reused for every window event.
    /// </summary>
    public Regex TitleRegex
    {
        get
        {
            return field ??= new Regex
            (
                TitlePattern,
                RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
                TimeSpan.FromSeconds(1)
            );
        }
    }
}

public record SizeFactorOptions
(
    double Width,
    double Height
);

public record SendKeysOptions
(
    string Sequence,
    string Description,
    int? DelayMs = null
);
