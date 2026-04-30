using PromptNest.Core.Abstractions;
using PromptNest.Core.Models;

namespace PromptNest.Core.Services;

public sealed class TagService : ITagService
{
    private readonly ITagRepository _tagRepository;

    public TagService(ITagRepository tagRepository)
    {
        _tagRepository = tagRepository;
    }

    public Task<IReadOnlyList<Tag>> ListAsync(CancellationToken cancellationToken) =>
        _tagRepository.ListAsync(cancellationToken);
}