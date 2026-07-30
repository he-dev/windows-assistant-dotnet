using System.Globalization;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace WindowsAssistant.Util.Serilog;

internal interface ILogMessageObserver
{
    void OnLogMessage(string message);
}

internal interface ILogMessageObservable
{
    IDisposable Subscribe(ILogMessageObserver observer);
}

/// <summary>
/// Formats Serilog events as text and publishes them to subscribed observers.
/// </summary>
internal sealed class ObservableLogSink : ILogEventSink, ILogMessageObservable
{
    private readonly MessageTemplateTextFormatter formatter = new("[{Level:u1}] [{Timestamp:HH:mm:ss}] {Message:lj}{NewLine}{Exception}", CultureInfo.InvariantCulture);
    private readonly List<ILogMessageObserver> observers = [];

    /// <summary>
    /// Subscribes an observer until the returned subscription is disposed.
    /// </summary>
    public IDisposable Subscribe(ILogMessageObserver observer)
    {
        lock (observers)
        {
            observers.Add(observer);
        }

        return new Subscription(this, observer);
    }

    public void Emit(LogEvent logEvent)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        formatter.Format(logEvent, writer);
        var message = writer.ToString();

        ILogMessageObserver[] subscribers;
        lock (observers)
        {
            subscribers = observers.ToArray();
        }

        // core: Publish a stable snapshot so observers may unsubscribe during notification.
        foreach (var observer in subscribers)
        {
            observer.OnLogMessage(message);
        }
    }

    private void Unsubscribe(ILogMessageObserver observer)
    {
        lock (observers)
        {
            observers.Remove(observer);
        }
    }

    private sealed class Subscription(ObservableLogSink sink, ILogMessageObserver observer) : IDisposable
    {
        private ObservableLogSink? observable = sink;

        public void Dispose()
        {
            Interlocked.Exchange(ref observable, null)?.Unsubscribe(observer);
        }
    }
}
