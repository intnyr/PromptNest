namespace PromptNest.Core.Abstractions;

public interface ITrayService
{
    event EventHandler<TrayCommand>? CommandInvoked;

    IReadOnlyList<string> MenuCommands { get; }

    Task ShowAsync(CancellationToken cancellationToken);

    Task HideAsync(CancellationToken cancellationToken);
}