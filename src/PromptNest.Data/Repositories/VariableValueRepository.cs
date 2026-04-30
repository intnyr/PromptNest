using Dapper;

using PromptNest.Core.Abstractions;

namespace PromptNest.Data.Repositories;

public sealed class VariableValueRepository : IVariableValueRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public VariableValueRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetLastUsedValuesAsync(
        string promptId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<VariableValueRow>(
            new CommandDefinition(
                """
                SELECT variable_name AS VariableName, value
                FROM variable_values
                WHERE prompt_id = $promptId
                ORDER BY variable_name;
                """,
                new { promptId },
                cancellationToken: cancellationToken));

        return rows.ToDictionary(static row => row.VariableName, static row => row.Value, StringComparer.OrdinalIgnoreCase);
    }

    public async Task SaveLastUsedValuesAsync(
        string promptId,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var updatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        foreach (var value in values)
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO variable_values(prompt_id, variable_name, value, updated_at)
                    VALUES ($promptId, $variableName, $value, $updatedAt)
                    ON CONFLICT(prompt_id, variable_name) DO UPDATE
                    SET value = excluded.value,
                        updated_at = excluded.updated_at;
                    """,
                    new { promptId, variableName = value.Key, value = value.Value, updatedAt },
                    transaction,
                    cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private sealed record VariableValueRow(string VariableName, string Value);
}