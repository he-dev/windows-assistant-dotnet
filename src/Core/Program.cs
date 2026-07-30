using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;

namespace WindowsAssistant;

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

        Console.WriteLine("Listening for window events...");

        // meta: WinEvent hooks require a Windows message loop, which this form provides.
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new HookForm(objectCreateOptions)
        {
            Visible = true,
            // StartPosition = FormStartPosition.CenterScreen // Does not work.
        });
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
        get => field ??= new Regex(
            TitlePattern,
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));
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
    string Description
);
