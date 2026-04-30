using System.Text;

using PromptNest.Core.Abstractions;
using PromptNest.Core.Models;

namespace PromptNest.Core.Services;

public sealed class PromptService : IPromptService
{
    private readonly IPromptRepository _promptRepository;
    private readonly IVariableParser _variableParser;

    public PromptService(IPromptRepository promptRepository, IVariableParser variableParser)
    {
        _promptRepository = promptRepository;
        _variableParser = variableParser;
    }

    public async Task<OperationResult<Prompt>> GetAsync(string id, CancellationToken cancellationToken)
    {
        var prompt = await _promptRepository.GetAsync(id, cancellationToken);
        return prompt is null
            ? OperationResultFactory.Failure<Prompt>("PromptNotFound", $"Prompt '{id}' was not found.")
            : OperationResultFactory.Success(prompt);
    }

    public Task<PagedResult<Prompt>> ListAsync(PromptQuery query, CancellationToken cancellationToken) =>
        _promptRepository.ListAsync(query, cancellationToken);

    public async Task<OperationResult<string>> CreateAsync(Prompt prompt, CancellationToken cancellationToken)
    {
        var validation = Validate(prompt);
        if (!validation.Succeeded)
        {
            return OperationResultFactory.Failure<string>(validation.ErrorCode ?? "ValidationFailed", validation.Message ?? "Prompt is invalid.");
        }

        var id = await _promptRepository.CreateAsync(WithParsedVariables(prompt), cancellationToken);
        return OperationResultFactory.Success(id);
    }

    public async Task<OperationResult> UpdateAsync(Prompt prompt, CancellationToken cancellationToken)
    {
        var validation = Validate(prompt);
        if (!validation.Succeeded)
        {
            return validation;
        }

        await _promptRepository.UpdateAsync(WithParsedVariables(prompt), cancellationToken);
        return OperationResult.Success();
    }

    public async Task<OperationResult> SoftDeleteAsync(string id, CancellationToken cancellationToken)
    {
        await _promptRepository.SoftDeleteAsync(id, cancellationToken);
        return OperationResult.Success();
    }

    public async Task<OperationResult<string>> DuplicateAsync(string id, CancellationToken cancellationToken)
    {
        var prompt = await _promptRepository.GetAsync(id, cancellationToken);
        if (prompt is null)
        {
            return OperationResultFactory.Failure<string>("PromptNotFound", $"Prompt '{id}' was not found.");
        }

        var now = DateTimeOffset.UtcNow;
        var duplicate = prompt with
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = $"{prompt.Title} Copy",
            UseCount = 0,
            LastUsedAt = null,
            CreatedAt = now,
            UpdatedAt = now,
            DeletedAt = null
        };

        var duplicateId = await _promptRepository.CreateAsync(duplicate, cancellationToken);
        return OperationResultFactory.Success(duplicateId);
    }

    public async Task<OperationResult> ToggleFavoriteAsync(string id, CancellationToken cancellationToken)
    {
        var prompt = await _promptRepository.GetAsync(id, cancellationToken);
        if (prompt is null)
        {
            return OperationResult.Failure("PromptNotFound", $"Prompt '{id}' was not found.");
        }

        await _promptRepository.UpdateAsync(prompt with { IsFavorite = !prompt.IsFavorite, UpdatedAt = DateTimeOffset.UtcNow }, cancellationToken);
        return OperationResult.Success();
    }

    private Prompt WithParsedVariables(Prompt prompt) => prompt with
    {
        Tags = prompt.Tags
            .Select(static tag => tag.Trim().ToLowerInvariant())
            .Where(static tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray(),
        Variables = _variableParser.Parse(prompt.Body)
    };

    private static OperationResult Validate(Prompt prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt.Title))
        {
            return OperationResult.Failure("TitleRequired", "Prompt title is required.");
        }

        if (string.IsNullOrWhiteSpace(prompt.Body))
        {
            return OperationResult.Failure("BodyRequired", "Prompt body is required.");
        }

        if (Encoding.UTF8.GetByteCount(prompt.Body) > PromptLimits.MaxPromptBodyBytes)
        {
            return OperationResult.Failure("BodyTooLarge", "Prompt body exceeds the 64KB limit.");
        }

        return OperationResult.Success();
    }
}