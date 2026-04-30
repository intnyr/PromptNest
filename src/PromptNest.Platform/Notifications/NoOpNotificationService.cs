using PromptNest.Core.Abstractions;

namespace PromptNest.Platform.Notifications;

public sealed class NoOpNotificationService : INotificationService
{
    public Task ShowCopiedAsync(string promptTitle, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(promptTitle);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}