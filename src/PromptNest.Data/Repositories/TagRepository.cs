using Dapper;

using PromptNest.Core.Abstractions;
using PromptNest.Core.Models;

namespace PromptNest.Data.Repositories;

public sealed class TagRepository : ITagRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public TagRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<Tag>> ListAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        var tags = await connection.QueryAsync<Tag>(
            new CommandDefinition(
                "SELECT name, color, count FROM tags ORDER BY name;",
                cancellationToken: cancellationToken));

        return tags.ToList();
    }

    public async Task UpsertAsync(Tag tag, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO tags(name, color, count)
                VALUES ($name, $color, $count)
                ON CONFLICT(name) DO UPDATE SET color = excluded.color;
                """,
                new { name = tag.Name, color = tag.Color, count = tag.Count },
                cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(string name, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(
            new CommandDefinition("DELETE FROM tags WHERE name = $name;", new { name }, cancellationToken: cancellationToken));
    }
}