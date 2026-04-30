namespace PromptNest.App;

public interface IApplicationShell
{
    Task ActivateAsync(CancellationToken cancellationToken);
}