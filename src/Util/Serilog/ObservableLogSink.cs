using System.Globalization;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace WindowsAssistant.Util.Serilog;

internal interface ILogMessageObserver
{
    void OnLogMessage(string message, LogEventLevel level);
}

internal interface ILogMessageObservable
{
    IDisposable Subscribe(ILogMessageObserver observer);
}

/// <summary>
/// Formats Serilog events as text and publishes them to subscribed observers.
/// </summary>
internal sealed class ObservableLogSink(string outputTemplate = ObservableLogSink.DefaultOutputTemplate) : ILogEventSink, ILogMessageObservable
{
    private const string DefaultOutputTemplate = "[{Level:u1}] [{Timestamp:HH:mm:ss}] {Message:lj}{NewLine}{Exception}";

    private readonly MessageTemplateTextFormatter formatter = new(outputTemplate, CultureInfo.InvariantCulture);
    private ILogMessageObserver? observer;

    /// <summary>
    /// Subscribes an observer until the returned subscription is disposed.
    /// </summary>
    public IDisposable Subscribe(ILogMessageObserver observer)
    {
        // meta: This application has one log target, so a new subscription replaces the previous observer.
        Interlocked.Exchange(ref this.observer, observer);
        return new Subscription(this, observer);
    }

    public void Emit(LogEvent logEvent)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        formatter.Format(logEvent, writer);
        var message = writer.ToString();

        Volatile.Read(ref observer)?.OnLogMessage(message, logEvent.Level);
    }

    private void Unsubscribe(ILogMessageObserver observer)
    {
        Interlocked.CompareExchange(ref this.observer, null, observer);
    }

    private sealed class Subscription(ObservableLogSink sink, ILogMessageObserver observer) : IDisposable
    {
        private ObservableLogSink? observable = sink;

        public void Dispose()
        {
            // meta: Atomically clear the sink so repeated disposal unsubscribes this observer only once.
            Interlocked.Exchange(ref observable, null)?.Unsubscribe(observer);
        }
    }
}
