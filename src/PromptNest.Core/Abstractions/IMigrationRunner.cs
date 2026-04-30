namespace PromptNest.Core.Abstractions;

public interface IMigrationRunner
{
    Task MigrateAsync(CancellationToken cancellationToken);
}