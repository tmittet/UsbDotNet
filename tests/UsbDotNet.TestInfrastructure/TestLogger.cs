namespace UsbDotNet.TestInfrastructure;

public class TestLogger(string _categoryName, ITestOutputHelper _output) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => default;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        var outputMessage = $"[{logLevel}] {_categoryName}: {message}";

        if (exception != null)
            outputMessage += Environment.NewLine + exception;

        _output.WriteLine(outputMessage);
    }
}
