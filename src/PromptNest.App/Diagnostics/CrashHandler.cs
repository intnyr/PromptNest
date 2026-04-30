using Microsoft.Extensions.Logging;

namespace PromptNest.App.Diagnostics;

public sealed class CrashHandler : IDisposable
{
    private static readonly Action<ILogger, string, Exception?> UnhandledExceptionLog =
        LoggerMessage.Define<string>(
            LogLevel.Critical,
            new EventId(1001, nameof(UnhandledExceptionLog)),
            "Unhandled exception occurred. IsTerminating={IsTerminating}");

    private static readonly Action<ILogger, Exception?> UnobservedTaskExceptionLog =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(1002, nameof(UnobservedTaskExceptionLog)),
            "Unobserved task exception occurred.");

    private readonly ILogger<CrashHandler> _logger;
    private bool _isRegistered;

    public CrashHandler(ILogger<CrashHandler> logger)
    {
        _logger = logger;
    }

    public void Register()
    {
        if (_isRegistered)
        {
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        _isRegistered = true;
    }

    public void Dispose()
    {
        if (!_isRegistered)
        {
            return;
        }

        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        _isRegistered = false;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        UnhandledExceptionLog(
            _logger,
            e.IsTerminating.ToString(System.Globalization.CultureInfo.InvariantCulture),
            exception);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        UnobservedTaskExceptionLog(_logger, e.Exception);
        e.SetObserved();
    }
}