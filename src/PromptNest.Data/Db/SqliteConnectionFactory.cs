using System.Data.Common;

using Microsoft.Data.Sqlite;

using PromptNest.Core.Abstractions;

namespace PromptNest.Data.Db;

public sealed class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly IPathProvider _pathProvider;

    public SqliteConnectionFactory(IPathProvider pathProvider)
    {
        _pathProvider = pathProvider;
    }

    public async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_pathProvider.DataDirectory);

        var connection = new SqliteConnection($"Data Source={_pathProvider.DatabasePath}");
        await connection.OpenAsync(cancellationToken);

        await using var journalMode = connection.CreateCommand();
        journalMode.CommandText = "PRAGMA journal_mode = WAL;";
        await journalMode.ExecuteNonQueryAsync(cancellationToken);

        await using var foreignKeys = connection.CreateCommand();
        foreignKeys.CommandText = "PRAGMA foreign_keys = ON;";
        await foreignKeys.ExecuteNonQueryAsync(cancellationToken);

        return connection;
    }
}