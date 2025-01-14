using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace UiT.ChatUiT2.MaintenanceFunctions.Tests.TestStaging;

internal sealed class TestLogger<T> : ILogger<T>, IDisposable
{
    private readonly List<LoggedMessage> _messages = [];

    public List<LoggedMessage> LoggedMessages {
        get => _messages;
    }

    public IDisposable BeginScope<TState>(TState state)
    {
        return this;
    }

    public void Dispose()
    {
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        _messages.Add(new LoggedMessage(logLevel, eventId, exception!, message));
    }
}


public sealed class LoggedMessage
{
    public LogLevel LogLevel { get; }
    public EventId EventId { get; }
    public Exception Exception { get; }
    public string Message { get; }

    public LoggedMessage(LogLevel logLevel, EventId eventId, Exception exception, string message)
    {
        LogLevel = logLevel;
        EventId = eventId;
        Exception = exception;
        Message = message;
    }
}
