using Dapper;

using PromptNest.Core.Abstractions;
using PromptNest.Core.Models;

namespace PromptNest.Data.Repositories;

public sealed class FolderRepository : IFolderRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public FolderRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<Folder>> ListAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<FolderRow>(
            new CommandDefinition(
                """
                SELECT f.id, f.name, f.parent_id AS ParentId, f.sort_order AS SortOrder, f.created_at AS CreatedAt,
                       COUNT(p.id) AS PromptCount
                FROM folders f
                LEFT JOIN prompts p ON p.folder_id = f.id AND p.deleted_at IS NULL
                GROUP BY f.id, f.name, f.parent_id, f.sort_order, f.created_at
                ORDER BY f.sort_order, f.name;
                """,
                cancellationToken: cancellationToken));

        return rows.Select(static row => new Folder
        {
            Id = row.Id,
            Name = row.Name,
            ParentId = row.ParentId,
            SortOrder = (int)SqliteTypeConversion.ToInt64(row.SortOrder),
            PromptCount = (int)SqliteTypeConversion.ToInt64(row.PromptCount),
            CreatedAt = SqliteTypeConversion.FromUnixTimeMilliseconds(SqliteTypeConversion.ToInt64(row.CreatedAt))
        }).ToList();
    }

    public async Task<string> CreateAsync(Folder folder, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        var id = string.IsNullOrWhiteSpace(folder.Id) ? Guid.NewGuid().ToString("N") : folder.Id;

        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO folders(id, name, parent_id, sort_order, created_at)
                VALUES ($id, $name, $parentId, $sortOrder, $createdAt);
                """,
                new
                {
                    id,
                    name = folder.Name,
                    parentId = folder.ParentId,
                    sortOrder = folder.SortOrder,
                    createdAt = folder.CreatedAt.ToUnixTimeMilliseconds()
                },
                cancellationToken: cancellationToken));

        return id;
    }

    public async Task UpdateAsync(Folder folder, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE folders
                SET name = $name, parent_id = $parentId, sort_order = $sortOrder
                WHERE id = $id;
                """,
                new { id = folder.Id, name = folder.Name, parentId = folder.ParentId, sortOrder = folder.SortOrder },
                cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(
            new CommandDefinition("DELETE FROM folders WHERE id = $id;", new { id }, cancellationToken: cancellationToken));
    }

    private sealed class FolderRow
    {
        public required string Id { get; init; }

        public required string Name { get; init; }

        public string? ParentId { get; init; }

        public required object SortOrder { get; init; }

        public required object CreatedAt { get; init; }

        public required object PromptCount { get; init; }
    }
}