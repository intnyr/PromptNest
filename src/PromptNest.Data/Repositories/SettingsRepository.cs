using System.Text.Json;

using Dapper;

using PromptNest.Core.Abstractions;
using PromptNest.Core.Models;

namespace PromptNest.Data.Repositories;

public sealed class SettingsRepository : ISettingsRepository
{
    private const string SettingsKey = "app";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IDbConnectionFactory _connectionFactory;

    public SettingsRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<AppSettings> GetAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        var json = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                "SELECT value FROM settings WHERE key = $key;",
                new { key = SettingsKey },
                cancellationToken: cancellationToken));

        return string.IsNullOrWhiteSpace(json)
            ? new AppSettings()
            : JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        var updatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var value = JsonSerializer.Serialize(settings, JsonOptions);

        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO settings(key, value, updated_at)
                VALUES ($key, $value, $updatedAt)
                ON CONFLICT(key) DO UPDATE
                SET value = excluded.value,
                    updated_at = excluded.updated_at;
                """,
                new { key = SettingsKey, value, updatedAt },
                cancellationToken: cancellationToken));
    }
}