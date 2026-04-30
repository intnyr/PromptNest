using Microsoft.Extensions.Logging;

namespace PromptNest.App;

public sealed class WinUiApplicationShell : IApplicationShell
{
    private static readonly Action<ILogger, Exception?> ShellActivated =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(1, nameof(ActivateAsync)),
            "PromptNest WinUI shell activated.");

    private readonly ILogger<WinUiApplicationShell> _logger;

    public WinUiApplicationShell(ILogger<WinUiApplicationShell> logger)
    {
        _logger = logger;
    }

    public Task ActivateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ShellActivated(_logger, null);
        return Task.CompletedTask;
    }
}