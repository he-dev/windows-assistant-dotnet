using Microsoft.Extensions.Configuration;

namespace WindowsAssistant;

internal class Program
{
    private static void Main()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

        var objectCreateOptions =
            configuration
                .GetSection("EVENT:OBJECT_CREATE")
                .Get<IEnumerable<ObjectCreateOptions>>();

        Console.WriteLine("Listening for window events...");

        // Create a dummy form with a hidden window.
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