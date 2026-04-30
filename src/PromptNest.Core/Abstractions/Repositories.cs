using PromptNest.Core.Models;

namespace PromptNest.Core.Abstractions;

public interface IPromptRepository
{
    Task<Prompt?> GetAsync(string id, CancellationToken cancellationToken);

    Task<PagedResult<Prompt>> ListAsync(PromptQuery query, CancellationToken cancellationToken);

    Task<PagedResult<Prompt>> SearchAsync(string text, PromptQuery query, CancellationToken cancellationToken);

    Task<string> CreateAsync(Prompt prompt, CancellationToken cancellationToken);

    Task UpdateAsync(Prompt prompt, CancellationToken cancellationToken);

    Task SoftDeleteAsync(string id, CancellationToken cancellationToken);

    Task IncrementUsageAsync(string id, CancellationToken cancellationToken);
}

public interface IFolderRepository
{
    Task<IReadOnlyList<Folder>> ListAsync(CancellationToken cancellationToken);

    Task<string> CreateAsync(Folder folder, CancellationToken cancellationToken);

    Task UpdateAsync(Folder folder, CancellationToken cancellationToken);

    Task DeleteAsync(string id, CancellationToken cancellationToken);
}

public interface ITagRepository
{
    Task<IReadOnlyList<Tag>> ListAsync(CancellationToken cancellationToken);

    Task UpsertAsync(Tag tag, CancellationToken cancellationToken);

    Task DeleteAsync(string name, CancellationToken cancellationToken);
}

public interface IVariableValueRepository
{
    Task<IReadOnlyDictionary<string, string>> GetLastUsedValuesAsync(string promptId, CancellationToken cancellationToken);

    Task SaveLastUsedValuesAsync(string promptId, IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken);
}

public interface ISettingsRepository
{
    Task<AppSettings> GetAsync(CancellationToken cancellationToken);

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken);
}