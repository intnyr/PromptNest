using PromptNest.Core.Abstractions;
using PromptNest.Core.Models;

namespace PromptNest.Core.Services;

public sealed class PromptCopyService : IPromptCopyService
{
    private readonly IPromptRepository _promptRepository;
    private readonly IVariableValueRepository _variableValueRepository;
    private readonly IVariableResolver _variableResolver;
    private readonly IClipboardService _clipboardService;
    private readonly INotificationService _notificationService;
    private readonly ISettingsRepository _settingsRepository;

    public PromptCopyService(
        IPromptRepository promptRepository,
        IVariableValueRepository variableValueRepository,
        IVariableResolver variableResolver,
        IClipboardService clipboardService,
        INotificationService notificationService,
        ISettingsRepository settingsRepository)
    {
        _promptRepository = promptRepository;
        _variableValueRepository = variableValueRepository;
        _variableResolver = variableResolver;
        _clipboardService = clipboardService;
        _notificationService = notificationService;
        _settingsRepository = settingsRepository;
    }

    public async Task<OperationResult<PromptCopyForm>> CreateFormAsync(string promptId, CancellationToken cancellationToken)
    {
        Prompt? prompt = await _promptRepository.GetAsync(promptId, cancellationToken);
        if (prompt is null)
        {
            return OperationResultFactory.Failure<PromptCopyForm>("PromptNotFound", $"Prompt '{promptId}' was not found.");
        }

        IReadOnlyDictionary<string, string> lastUsedValues =
            await _variableValueRepository.GetLastUsedValuesAsync(prompt.Id, cancellationToken);

        PromptCopyVariable[] variables = prompt.Variables
            .Select(variable => new PromptCopyVariable
            {
                Name = variable.Name,
                DefaultValue = variable.DefaultValue,
                CurrentValue = lastUsedValues.TryGetValue(variable.Name, out string? value) ? value : variable.DefaultValue,
                Type = variable.Type,
                IsRequired = variable.IsRequired
            })
            .ToArray();

        return OperationResultFactory.Success(
            new PromptCopyForm
            {
                PromptId = prompt.Id,
                Title = prompt.Title,
                Variables = variables
            });
    }

    public async Task<OperationResult<ResolvedPrompt>> CopyAsync(
        string promptId,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken)
    {
        Prompt? prompt = await _promptRepository.GetAsync(promptId, cancellationToken);
        if (prompt is null)
        {
            return OperationResultFactory.Failure<ResolvedPrompt>("PromptNotFound", $"Prompt '{promptId}' was not found.");
        }

        OperationResult<ResolvedPrompt> resolved = await _variableResolver.ResolveAsync(prompt, values, cancellationToken);
        if (!resolved.Succeeded || resolved.Value is null)
        {
            return resolved;
        }

        OperationResult copied = await _clipboardService.CopyTextAsync(resolved.Value.Text, html: null, cancellationToken);
        if (!copied.Succeeded)
        {
            return OperationResultFactory.Failure<ResolvedPrompt>(
                copied.ErrorCode ?? "CopyFailed",
                copied.Message ?? "Prompt could not be copied.");
        }

        await _promptRepository.IncrementUsageAsync(prompt.Id, cancellationToken);

        AppSettings settings = await _settingsRepository.GetAsync(cancellationToken);
        if (settings.NotificationsEnabled)
        {
            await _notificationService.ShowCopiedAsync(prompt.Title, cancellationToken);
        }

        return resolved;
    }
}