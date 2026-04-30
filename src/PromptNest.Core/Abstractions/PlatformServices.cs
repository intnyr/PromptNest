using System.Data.Common;

using PromptNest.Core.Models;

namespace PromptNest.Core.Abstractions;

public interface IPathProvider
{
    string DataDirectory { get; }

    string DatabasePath { get; }

    string LogsDirectory { get; }

    string BackupsDirectory { get; }

    string SettingsPath { get; }

    string UpdateCacheDirectory { get; }

    bool IsPackaged { get; }
}

public interface IDbConnectionFactory
{
    Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken);
}

public interface INotificationService
{
    Task ShowCopiedAsync(string promptTitle, CancellationToken cancellationToken);
}

public interface IJumpListService
{
    Task RefreshFavoritesAsync(IReadOnlyList<Prompt> favoritePrompts, CancellationToken cancellationToken);
}

public interface IUpdateService
{
    Task<OperationResult<UpdateStatus>> CheckForUpdatesAsync(AppSettings settings, CancellationToken cancellationToken);
}