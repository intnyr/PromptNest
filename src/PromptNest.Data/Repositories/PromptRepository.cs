using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;

using Dapper;

using Microsoft.Data.Sqlite;

using PromptNest.Core.Abstractions;
using PromptNest.Core.Models;

namespace PromptNest.Data.Repositories;

public sealed class PromptRepository : IPromptRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex FtsTokenRegex = new(@"[\p{L}\p{N}_]+", RegexOptions.Compiled);
    private static readonly HashSet<string> FtsOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "AND",
        "NEAR",
        "NOT",
        "OR"
    };

    private readonly IDbConnectionFactory _connectionFactory;

    public PromptRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Prompt?> GetAsync(string id, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<PromptRow>(
            new CommandDefinition(
                """
                SELECT id, title, body, variables_json AS VariablesJson, folder_id AS FolderId,
                       is_favorite AS IsFavorite, use_count AS UseCount, last_used_at AS LastUsedAt,
                       created_at AS CreatedAt, updated_at AS UpdatedAt, deleted_at AS DeletedAt
                FROM prompts
                WHERE id = $id;
                """,
                new { id },
                cancellationToken: cancellationToken));

        return row is null ? null : await MapPromptAsync(connection, row, cancellationToken);
    }

    public async Task<PagedResult<Prompt>> ListAsync(PromptQuery query, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        return await QueryPromptsAsync(connection, query, searchText: null, useFts: false, cancellationToken);
    }

    public async Task<PagedResult<Prompt>> SearchAsync(string text, PromptQuery query, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        try
        {
            return await QueryPromptsAsync(connection, query, NormalizeFtsQuery(text), useFts: true, cancellationToken);
        }
        catch (SqliteException)
        {
            return await QueryPromptsAsync(connection, query, text, useFts: false, cancellationToken);
        }
    }

    public async Task<string> CreateAsync(Prompt prompt, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var id = string.IsNullOrWhiteSpace(prompt.Id) ? Guid.NewGuid().ToString("N") : prompt.Id;
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO prompts (
                    id, title, body, variables_json, folder_id, is_favorite, use_count,
                    last_used_at, created_at, updated_at, deleted_at
                )
                VALUES (
                    $id, $title, $body, $variablesJson, $folderId, $isFavorite, $useCount,
                    $lastUsedAt, $createdAt, $updatedAt, $deletedAt
                );
                """,
                ToParameters(prompt with { Id = id }),
                transaction,
                cancellationToken: cancellationToken));

        await ReplaceTagsAsync(connection, transaction, id, prompt.Tags, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return id;
    }

    public async Task UpdateAsync(Prompt prompt, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE prompts
                SET title = $title,
                    body = $body,
                    variables_json = $variablesJson,
                    folder_id = $folderId,
                    is_favorite = $isFavorite,
                    use_count = $useCount,
                    last_used_at = $lastUsedAt,
                    updated_at = $updatedAt,
                    deleted_at = $deletedAt
                WHERE id = $id;
                """,
                ToParameters(prompt),
                transaction,
                cancellationToken: cancellationToken));

        await ReplaceTagsAsync(connection, transaction, prompt.Id, prompt.Tags, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SoftDeleteAsync(string id, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE prompts SET deleted_at = $deletedAt, updated_at = $deletedAt WHERE id = $id;",
                new { id, deletedAt = now },
                cancellationToken: cancellationToken));
    }

    public async Task IncrementUsageAsync(string id, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE prompts SET use_count = use_count + 1, last_used_at = $lastUsedAt WHERE id = $id;",
                new { id, lastUsedAt = now },
                cancellationToken: cancellationToken));
    }

    private static async Task<PagedResult<Prompt>> QueryPromptsAsync(
        IDbConnection connection,
        PromptQuery query,
        string? searchText,
        bool useFts,
        CancellationToken cancellationToken)
    {
        var where = new List<string>();
        var parameters = new DynamicParameters();

        if (!query.IncludeDeleted)
        {
            where.Add("p.deleted_at IS NULL");
        }

        if (!string.IsNullOrWhiteSpace(query.FolderId))
        {
            where.Add("p.folder_id = $folderId");
            parameters.Add("folderId", query.FolderId);
        }

        if (query.IsFavorite is not null)
        {
            where.Add("p.is_favorite = $isFavorite");
            parameters.Add("isFavorite", SqliteTypeConversion.ToSqliteBoolean(query.IsFavorite.Value));
        }

        for (var index = 0; index < query.Tags.Count; index++)
        {
            var parameterName = $"tag{index}";
            where.Add($"EXISTS (SELECT 1 FROM prompt_tags pt WHERE pt.prompt_id = p.id AND pt.tag_name = ${parameterName})");
            parameters.Add(parameterName, query.Tags[index]);
        }

        var from = "prompts p";
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            if (useFts)
            {
                from = "prompts_fts f JOIN prompts p ON p.id = f.prompt_id";
                where.Add("prompts_fts MATCH $searchText");
                parameters.Add("searchText", searchText);
            }
            else
            {
                var escaped = SqliteTypeConversion.EscapeLike(searchText);
                where.Add("(p.title LIKE $likeText ESCAPE '\\' OR p.body LIKE $likeText ESCAPE '\\')");
                parameters.Add("likeText", $"%{escaped}%");
            }
        }

        parameters.Add("skip", query.Skip);
        parameters.Add("take", query.Take);

        var whereSql = where.Count == 0 ? string.Empty : $"WHERE {string.Join(" AND ", where)}";
        var orderSql = BuildOrderBy(query.SortBy, query.SortDescending);

        var rows = await connection.QueryAsync<PromptRow>(
            new CommandDefinition(
                $"""
                SELECT p.id, p.title, p.body, p.variables_json AS VariablesJson, p.folder_id AS FolderId,
                       p.is_favorite AS IsFavorite, p.use_count AS UseCount, p.last_used_at AS LastUsedAt,
                       p.created_at AS CreatedAt, p.updated_at AS UpdatedAt, p.deleted_at AS DeletedAt
                FROM {from}
                {whereSql}
                {orderSql}
                LIMIT $take OFFSET $skip;
                """,
                parameters,
                cancellationToken: cancellationToken));

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                $"SELECT COUNT(*) FROM {from} {whereSql};",
                parameters,
                cancellationToken: cancellationToken));

        var prompts = new List<Prompt>();
        foreach (var row in rows)
        {
            prompts.Add(await MapPromptAsync(connection, row, cancellationToken));
        }

        return new PagedResult<Prompt> { Items = prompts, TotalCount = totalCount, Skip = query.Skip, Take = query.Take };
    }

    private static string BuildOrderBy(PromptSortBy sortBy, bool descending)
    {
        var direction = descending ? "DESC" : "ASC";
        var column = sortBy switch
        {
            PromptSortBy.Title => "p.title",
            PromptSortBy.CreatedAt => "p.created_at",
            PromptSortBy.LastUsedAt => "p.last_used_at",
            PromptSortBy.UseCount => "p.use_count",
            _ => "p.updated_at"
        };

        return $"ORDER BY {column} {direction}, p.title ASC";
    }

    private static string NormalizeFtsQuery(string text)
    {
        var terms = FtsTokenRegex
            .Matches(text)
            .Select(static match => match.Value)
            .Where(static term => !FtsOperators.Contains(term));

        return string.Join(' ', terms.Select(static term => $"{term.Replace("\"", "\"\"", StringComparison.Ordinal)}*"));
    }

    private static async Task<Prompt> MapPromptAsync(IDbConnection connection, PromptRow row, CancellationToken cancellationToken)
    {
        var tags = await connection.QueryAsync<string>(
            new CommandDefinition(
                "SELECT tag_name FROM prompt_tags WHERE prompt_id = $promptId ORDER BY tag_name;",
                new { promptId = row.Id },
                cancellationToken: cancellationToken));

        var variables = JsonSerializer.Deserialize<IReadOnlyList<PromptVariable>>(row.VariablesJson, JsonOptions) ?? [];

        return new Prompt
        {
            Id = row.Id,
            Title = row.Title,
            Body = row.Body,
            FolderId = row.FolderId,
            IsFavorite = SqliteTypeConversion.FromSqliteBoolean(row.IsFavorite),
            UseCount = (int)row.UseCount,
            LastUsedAt = SqliteTypeConversion.FromNullableUnixTimeMilliseconds(row.LastUsedAt),
            CreatedAt = SqliteTypeConversion.FromUnixTimeMilliseconds(row.CreatedAt),
            UpdatedAt = SqliteTypeConversion.FromUnixTimeMilliseconds(row.UpdatedAt),
            DeletedAt = SqliteTypeConversion.FromNullableUnixTimeMilliseconds(row.DeletedAt),
            Tags = tags.ToList(),
            Variables = variables
        };
    }

    private static object ToParameters(Prompt prompt)
    {
        return new
        {
            id = prompt.Id,
            title = prompt.Title,
            body = prompt.Body,
            variablesJson = JsonSerializer.Serialize(prompt.Variables, JsonOptions),
            folderId = prompt.FolderId,
            isFavorite = SqliteTypeConversion.ToSqliteBoolean(prompt.IsFavorite),
            useCount = prompt.UseCount,
            lastUsedAt = prompt.LastUsedAt?.ToUnixTimeMilliseconds(),
            createdAt = prompt.CreatedAt.ToUnixTimeMilliseconds(),
            updatedAt = prompt.UpdatedAt.ToUnixTimeMilliseconds(),
            deletedAt = prompt.DeletedAt?.ToUnixTimeMilliseconds()
        };
    }

    private static async Task ReplaceTagsAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        string promptId,
        IReadOnlyList<string> tags,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM prompt_tags WHERE prompt_id = $promptId;",
                new { promptId },
                transaction,
                cancellationToken: cancellationToken));

        foreach (var tag in tags.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    "INSERT OR IGNORE INTO tags(name, color, count) VALUES ($name, NULL, 0);",
                    new { name = tag },
                    transaction,
                    cancellationToken: cancellationToken));

            await connection.ExecuteAsync(
                new CommandDefinition(
                    "INSERT INTO prompt_tags(prompt_id, tag_name) VALUES ($promptId, $tagName);",
                    new { promptId, tagName = tag },
                    transaction,
                    cancellationToken: cancellationToken));
        }
    }

    private sealed record PromptRow(
        string Id,
        string Title,
        string Body,
        string VariablesJson,
        string? FolderId,
        long IsFavorite,
        long UseCount,
        long? LastUsedAt,
        long CreatedAt,
        long UpdatedAt,
        long? DeletedAt);
}