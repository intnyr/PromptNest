using PromptNest.Core.Abstractions;
using PromptNest.Core.Models;

namespace PromptNest.Core.Services;

public sealed class BackupService : IBackupService
{
    private readonly IPathProvider _pathProvider;

    public BackupService(IPathProvider pathProvider)
    {
        _pathProvider = pathProvider;
    }

    public Task<OperationResult<BackupMetadata>> CreateBackupAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(_pathProvider.DatabasePath))
        {
            return Task.FromResult(OperationResultFactory.Failure<BackupMetadata>("DatabaseMissing", "Database file does not exist."));
        }

        Directory.CreateDirectory(_pathProvider.BackupsDirectory);

        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var backupPath = Path.Combine(_pathProvider.BackupsDirectory, $"library.db.bak.{timestamp}");
        File.Copy(_pathProvider.DatabasePath, backupPath, overwrite: false);

        var file = new FileInfo(backupPath);
        return Task.FromResult(
            OperationResultFactory.Success(
                new BackupMetadata
                {
                    FilePath = backupPath,
                    CreatedAt = DateTimeOffset.UtcNow,
                    SizeBytes = file.Length
                }));
    }

    public Task<IReadOnlyList<BackupMetadata>> ListBackupsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(_pathProvider.BackupsDirectory))
        {
            return Task.FromResult<IReadOnlyList<BackupMetadata>>([]);
        }

        var backups = Directory
            .EnumerateFiles(_pathProvider.BackupsDirectory, "library.db.bak.*")
            .Select(static path =>
            {
                var file = new FileInfo(path);
                return new BackupMetadata { FilePath = path, CreatedAt = file.CreationTimeUtc, SizeBytes = file.Length };
            })
            .OrderByDescending(static backup => backup.CreatedAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<BackupMetadata>>(backups);
    }

    public async Task<OperationResult> ApplyRetentionAsync(int keepLast, CancellationToken cancellationToken)
    {
        if (keepLast < 0)
        {
            return OperationResult.Failure("InvalidRetention", "Backup retention count cannot be negative.");
        }

        var backups = await ListBackupsAsync(cancellationToken);

        foreach (var backup in backups.Skip(keepLast))
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.Delete(backup.FilePath);
        }

        return OperationResult.Success();
    }
}