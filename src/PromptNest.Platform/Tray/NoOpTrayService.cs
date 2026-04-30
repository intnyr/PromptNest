using PromptNest.Core.Abstractions;

namespace PromptNest.Platform.Tray;

public sealed class NoOpTrayService : ITrayService
{
    public event EventHandler<TrayCommand>? CommandInvoked;

    public IReadOnlyList<string> MenuCommands { get; } =
    [
        "Open Library",
        "New Prompt",
        "Recent",
        "Favorites",
        "Settings",
        "Quit"
    ];

    public Task ShowAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task HideAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public void InvokeForTests(TrayCommand command)
    {
        CommandInvoked?.Invoke(this, command);
    }
}