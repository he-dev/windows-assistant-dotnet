using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using WindowsAssistant.Core.Views;

using Serilog;
using WindowsAssistant.Util.Serilog;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Buzz = WindowsAssistant.Util.Buzz;

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

        // meta: WinEvent hooks require a Windows message loop, which this form provides.
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var observableLogSink = new ObservableLogSink();
        var serilogLogger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Sink(observableLogSink).CreateLogger();
        var services = new ServiceCollection();
        services.AddOptions<WindowEventOptions>().Bind(configuration.GetSection("EVENT"));
        services.AddSingleton(observableLogSink);
        services.AddLogging(logging => logging.ClearProviders().AddSerilog(serilogLogger, dispose: true));
        services.AddSingleton<Buzz.MatchWindow>();
        services.AddSingleton<Buzz.ResizeWindow>();
        services.AddSingleton<Buzz.SendKeys>();
        services.AddSingleton<HookForm>();

        using var serviceProvider = services.BuildServiceProvider();
        var hookForm = serviceProvider.GetRequiredService<HookForm>();
        serviceProvider.GetRequiredService<ILogger<Program>>().LogInformation("Listening for window events...");
        Application.Run(hookForm);
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

public sealed class WindowEventOptions
{
    [ConfigurationKeyName("OBJECT_CREATE")]
    public List<ObjectCreateOptions> ObjectCreate { get; init; } = [];
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
