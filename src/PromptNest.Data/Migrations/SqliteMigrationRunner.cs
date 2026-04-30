using System.Data.Common;
using System.Globalization;

using Microsoft.Data.Sqlite;

using PromptNest.Core.Abstractions;

namespace PromptNest.Data.Migrations;

public sealed class SqliteMigrationRunner : IMigrationRunner
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IPathProvider _pathProvider;

    public SqliteMigrationRunner(IDbConnectionFactory connectionFactory, IPathProvider pathProvider)
    {
        _connectionFactory = connectionFactory;
        _pathProvider = pathProvider;
    }

    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.OpenConnectionAsync(cancellationToken);

        await EnsureSchemaVersionTableAsync(connection, cancellationToken);

        var currentVersion = await GetCurrentVersionAsync(connection, cancellationToken);
        var migrations = GetMigrationFiles()
            .Where(migration => migration.Version > currentVersion)
            .OrderBy(migration => migration.Version)
            .ToList();

        if (migrations.Count == 0)
        {
            return;
        }

        BackupExistingDatabase();

        foreach (var migration in migrations)
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await ExecuteMigrationAsync(connection, transaction, migration, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
    }

    private static async Task EnsureSchemaVersionTableAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE IF NOT EXISTS schema_version (version INTEGER PRIMARY KEY);";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> GetCurrentVersionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_version;";

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    private static async Task ExecuteMigrationAsync(
        SqliteConnection connection,
        DbTransaction transaction,
        MigrationFile migration,
        CancellationToken cancellationToken)
    {
        await using var migrationCommand = connection.CreateCommand();
        migrationCommand.Transaction = (SqliteTransaction)transaction;
        migrationCommand.CommandText = await File.ReadAllTextAsync(migration.Path, cancellationToken);
        await migrationCommand.ExecuteNonQueryAsync(cancellationToken);

        await using var versionCommand = connection.CreateCommand();
        versionCommand.Transaction = (SqliteTransaction)transaction;
        versionCommand.CommandText = "INSERT INTO schema_version(version) VALUES ($version);";
        versionCommand.Parameters.AddWithValue("$version", migration.Version);
        await versionCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static List<MigrationFile> GetMigrationFiles()
    {
        var migrationsDirectory = Path.Combine(AppContext.BaseDirectory, "Migrations");

        if (!Directory.Exists(migrationsDirectory))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(migrationsDirectory, "*.sql")
            .Select(ParseMigrationFile)
            .Where(static migration => migration is not null)
            .Cast<MigrationFile>()
            .ToList();
    }

    private static MigrationFile? ParseMigrationFile(string path)
    {
        var fileName = Path.GetFileName(path);
        var separatorIndex = fileName.IndexOf('_', StringComparison.Ordinal);

        if (separatorIndex <= 0)
        {
            return null;
        }

        var versionText = fileName[..separatorIndex];
        return int.TryParse(versionText, NumberStyles.None, CultureInfo.InvariantCulture, out var version)
            ? new MigrationFile(version, path)
            : null;
    }

    private void BackupExistingDatabase()
    {
        if (!File.Exists(_pathProvider.DatabasePath))
        {
            return;
        }

        var database = new FileInfo(_pathProvider.DatabasePath);
        if (database.Length == 0)
        {
            return;
        }

        Directory.CreateDirectory(_pathProvider.BackupsDirectory);

        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var backupPath = Path.Combine(_pathProvider.BackupsDirectory, $"library.db.bak.{timestamp}");
        File.Copy(_pathProvider.DatabasePath, backupPath, overwrite: false);
    }

    private sealed record MigrationFile(int Version, string Path);
}