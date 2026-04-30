using FluentAssertions;

using Microsoft.Data.Sqlite;

using PromptNest.Core.Abstractions;
using PromptNest.Data.Db;
using PromptNest.Data.Migrations;

namespace PromptNest.Data.Tests;

public sealed class MigrationRunnerTests : IDisposable
{
    private readonly string _testDirectory;

    public MigrationRunnerTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "PromptNest.Tests", Guid.NewGuid().ToString("N"));
    }

    [Fact]
    public async Task MigrateAsyncCreatesInitialSchema()
    {
        var pathProvider = new TestPathProvider(_testDirectory);
        var connectionFactory = new SqliteConnectionFactory(pathProvider);
        var runner = new SqliteMigrationRunner(connectionFactory, pathProvider);

        await runner.MigrateAsync(CancellationToken.None);

        await using var connection = (SqliteConnection)await connectionFactory.OpenConnectionAsync(CancellationToken.None);

        var tables = await ReadStringsAsync(
            connection,
            "SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name;");

        tables.Should().Contain(
            [
                "folders",
                "prompts",
                "prompt_tags",
                "prompts_fts",
                "settings",
                "tags",
                "variable_values"
            ]);

        var promptColumns = await ReadStringsAsync(connection, "SELECT name FROM pragma_table_info('prompts');");
        promptColumns.Should().Contain("variables_json");

        await using var versionCommand = connection.CreateCommand();
        versionCommand.CommandText = "SELECT MAX(version) FROM schema_version;";
        var version = await versionCommand.ExecuteScalarAsync(CancellationToken.None);
        version.Should().Be(1L);
    }

    [Fact]
    public async Task MigrateAsyncIsRepeatableAndKeepsSchemaVersionStable()
    {
        var pathProvider = new TestPathProvider(_testDirectory);
        var connectionFactory = new SqliteConnectionFactory(pathProvider);
        var runner = new SqliteMigrationRunner(connectionFactory, pathProvider);

        await runner.MigrateAsync(CancellationToken.None);
        await runner.MigrateAsync(CancellationToken.None);

        await using var connection = (SqliteConnection)await connectionFactory.OpenConnectionAsync(CancellationToken.None);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT version FROM schema_version ORDER BY version;";

        var versions = new List<long>();
        await using var reader = await command.ExecuteReaderAsync(CancellationToken.None);
        while (await reader.ReadAsync(CancellationToken.None))
        {
            versions.Add(reader.GetInt64(0));
        }

        versions.Should().Equal(1L);
    }

    [Fact]
    public async Task MigrateAsyncBacksUpExistingDatabaseBeforeApplyingPendingMigrations()
    {
        Directory.CreateDirectory(_testDirectory);
        var pathProvider = new TestPathProvider(_testDirectory);

        await using (var existingConnection = new SqliteConnection($"Data Source={pathProvider.DatabasePath}"))
        {
            await existingConnection.OpenAsync(CancellationToken.None);
            await using var command = existingConnection.CreateCommand();
            command.CommandText = "CREATE TABLE existing_data (id INTEGER PRIMARY KEY); INSERT INTO existing_data(id) VALUES (1);";
            await command.ExecuteNonQueryAsync(CancellationToken.None);
        }

        var connectionFactory = new SqliteConnectionFactory(pathProvider);
        var runner = new SqliteMigrationRunner(connectionFactory, pathProvider);

        await runner.MigrateAsync(CancellationToken.None);

        Directory
            .EnumerateFiles(pathProvider.BackupsDirectory, "library.db.bak.*")
            .Should()
            .ContainSingle();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    private static async Task<IReadOnlyList<string>> ReadStringsAsync(SqliteConnection connection, string commandText)
    {
        var values = new List<string>();

        await using var command = connection.CreateCommand();
        command.CommandText = commandText;

        await using var reader = await command.ExecuteReaderAsync(CancellationToken.None);
        while (await reader.ReadAsync(CancellationToken.None))
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }

    private sealed class TestPathProvider : IPathProvider
    {
        public TestPathProvider(string dataDirectory)
        {
            DataDirectory = dataDirectory;
            DatabasePath = Path.Combine(DataDirectory, "library.db");
            LogsDirectory = Path.Combine(DataDirectory, "logs");
            BackupsDirectory = Path.Combine(DataDirectory, "Backups");
        }

        public string DataDirectory { get; }

        public string DatabasePath { get; }

        public string LogsDirectory { get; }

        public string BackupsDirectory { get; }

        public string SettingsPath => Path.Combine(DataDirectory, "settings.json");

        public string UpdateCacheDirectory => Path.Combine(DataDirectory, "Updates");

        public bool IsPackaged => false;
    }
}