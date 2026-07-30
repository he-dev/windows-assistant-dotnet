using System.Globalization;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;
using WindowsAssistant.Core.Views;

namespace WindowsAssistant.Util.Serilog;

/// <summary>
/// Routes Serilog events into the application's rounded log text box.
/// </summary>
/// <remarks>
/// Serilog can emit events from any thread, so the sink marshals text-box updates onto the WinForms UI thread.
/// </remarks>
internal sealed class TextBoxSink(RoundedTextBox textBox) : ILogEventSink
{
    private readonly MessageTemplateTextFormatter formatter = new("[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}", CultureInfo.InvariantCulture);

    public void Emit(LogEvent logEvent)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        formatter.Format(logEvent, writer);
        var text = writer.ToString();

        // meta: Ignore events emitted while the form is shutting down.
        if (textBox.IsDisposed || textBox.Disposing)
        {
            return;
        }

        // core: Logging threads never update WinForms controls directly.
        if (textBox.InvokeRequired)
        {
            try
            {
                textBox.BeginInvoke(new Action(() => textBox.AppendText(text)));
            }
            catch (InvalidOperationException)
            {
                // util: The control lost its handle while the application was shutting down.
            }

            return;
        }

        textBox.AppendText(text);
    }
}
