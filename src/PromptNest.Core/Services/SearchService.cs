using PromptNest.Core.Abstractions;
using PromptNest.Core.Models;

namespace PromptNest.Core.Services;

public sealed class SearchService : ISearchService
{
    private readonly IPromptRepository _promptRepository;

    public SearchService(IPromptRepository promptRepository)
    {
        _promptRepository = promptRepository;
    }

    public Task<PagedResult<Prompt>> SearchAsync(string text, PromptQuery query, CancellationToken cancellationToken)
    {
        return string.IsNullOrWhiteSpace(text)
            ? _promptRepository.ListAsync(query, cancellationToken)
            : _promptRepository.SearchAsync(text.Trim(), query, cancellationToken);
    }
}