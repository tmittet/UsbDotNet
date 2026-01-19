namespace UsbDotNet.TestInfrastructure;

public sealed class TestLoggerFactory(ITestOutputHelper _output) : ILoggerFactory
{
    private readonly TestLoggerProvider _provider = new(_output);

    public void AddProvider(ILoggerProvider provider)
    {
        throw new NotSupportedException("Adding providers is not supported in this factory.");
    }

    public ILogger CreateLogger(string categoryName)
    {
        return _provider.CreateLogger(categoryName);
    }

    public void Dispose()
    {
        _provider.Dispose();
    }
}
