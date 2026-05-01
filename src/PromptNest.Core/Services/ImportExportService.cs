using System.Text;

using PromptNest.Core.Abstractions;
using PromptNest.Core.Models;

namespace PromptNest.Core.Services;

public sealed class ImportExportService : IImportExportService
{
    private readonly IFolderRepository _folderRepository;
    private readonly ITagRepository _tagRepository;
    private readonly IPromptRepository _promptRepository;
    private readonly IVariableParser _variableParser;

    public ImportExportService(
        IFolderRepository folderRepository,
        ITagRepository tagRepository,
        IPromptRepository promptRepository,
        IVariableParser variableParser)
    {
        _folderRepository = folderRepository;
        _tagRepository = tagRepository;
        _promptRepository = promptRepository;
        _variableParser = variableParser;
    }

    public async Task<OperationResult<PromptNestExport>> ExportAsync(CancellationToken cancellationToken)
    {
        var folders = await _folderRepository.ListAsync(cancellationToken);
        var tags = await _tagRepository.ListAsync(cancellationToken);
        var prompts = await _promptRepository.ListAsync(
            new PromptQuery { IncludeDeleted = true, Take = int.MaxValue, SortBy = PromptSortBy.CreatedAt, SortDescending = false },
            cancellationToken);

        return OperationResultFactory.Success(
            new PromptNestExport
            {
                ExportedAt = DateTimeOffset.UtcNow,
                Folders = folders,
                Tags = tags,
                Prompts = prompts.Items
            });
    }

    public async Task<OperationResult<ImportPlan>> PreviewImportAsync(
        PromptNestExport exportData,
        ImportOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(exportData);
        ArgumentNullException.ThrowIfNull(options);

        ImportPlan plan = await BuildPlanAsync(exportData, options with { DryRun = true }, cancellationToken);
        return OperationResultFactory.Success(plan);
    }

    public async Task<OperationResult<ImportSummary>> ImportAsync(
        PromptNestExport exportData,
        ImportOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(exportData);
        ArgumentNullException.ThrowIfNull(options);

        ImportPlan plan = await BuildPlanAsync(exportData, options, cancellationToken);
        if (plan.HasErrors)
        {
            return OperationResultFactory.Failure<ImportSummary>(
                "ImportValidationFailed",
                $"Import validation failed with {plan.Summary.ValidationErrors} error(s).");
        }

        if (options.DryRun)
        {
            return OperationResultFactory.Success(plan.Summary);
        }

        var foldersCreated = 0;
        var tagsCreated = 0;
        var promptsCreated = 0;
        var promptsUpdated = 0;
        var promptsSkipped = 0;

        foreach (Folder folder in exportData.Folders)
        {
            if (string.IsNullOrWhiteSpace(folder.Id))
            {
                continue;
            }

            await _folderRepository.CreateAsync(folder, cancellationToken);
            foldersCreated++;
        }

        foreach (Tag tag in exportData.Tags)
        {
            if (!IsValidTagName(tag.Name))
            {
                continue;
            }

            await _tagRepository.UpsertAsync(tag with { Name = NormalizeTag(tag.Name) }, cancellationToken);
            tagsCreated++;
        }

        foreach (Prompt prompt in exportData.Prompts)
        {
            Prompt? existing = await _promptRepository.GetAsync(prompt.Id, cancellationToken);
            Prompt preparedPrompt = PreparePrompt(prompt);
            if (existing is null)
            {
                await _promptRepository.CreateAsync(preparedPrompt, cancellationToken);
                promptsCreated++;
                continue;
            }

            switch (options.ConflictMode)
            {
                case ImportConflictMode.Skip:
                    promptsSkipped++;
                    break;
                case ImportConflictMode.Overwrite:
                    await _promptRepository.UpdateAsync(preparedPrompt, cancellationToken);
                    promptsUpdated++;
                    break;
                case ImportConflictMode.Duplicate:
                    await _promptRepository.CreateAsync(preparedPrompt with { Id = Guid.NewGuid().ToString("N") }, cancellationToken);
                    promptsCreated++;
                    break;
                default:
                    return OperationResultFactory.Failure<ImportSummary>("InvalidConflictMode", "Import conflict mode is invalid.");
            }
        }

        return OperationResultFactory.Success(
            new ImportSummary
            {
                FoldersCreated = foldersCreated,
                TagsCreated = tagsCreated,
                PromptsCreated = promptsCreated,
                PromptsUpdated = promptsUpdated,
                PromptsSkipped = promptsSkipped,
                ValidationWarnings = plan.Summary.ValidationWarnings
            });
    }

    private async Task<ImportPlan> BuildPlanAsync(PromptNestExport exportData, ImportOptions options, CancellationToken cancellationToken)
    {
        var issues = new List<ImportValidationIssue>();

        if (exportData.SchemaVersion != 1)
        {
            issues.Add(Error("UnsupportedSchema", $"Export schema version {exportData.SchemaVersion} is not supported."));
        }

        if (options.MaxPromptCount <= 0 || exportData.Prompts.Count > options.MaxPromptCount)
        {
            issues.Add(Error("PromptCountLimit", $"Import contains {exportData.Prompts.Count} prompts; the limit is {options.MaxPromptCount}."));
        }

        if (options.MaxFolderCount <= 0 || exportData.Folders.Count > options.MaxFolderCount)
        {
            issues.Add(Error("FolderCountLimit", $"Import contains {exportData.Folders.Count} folders; the limit is {options.MaxFolderCount}."));
        }

        if (options.MaxTagCount <= 0 || exportData.Tags.Count > options.MaxTagCount)
        {
            issues.Add(Error("TagCountLimit", $"Import contains {exportData.Tags.Count} tags; the limit is {options.MaxTagCount}."));
        }

        HashSet<string> folderIds = new(StringComparer.Ordinal);
        foreach (Folder folder in exportData.Folders)
        {
            if (string.IsNullOrWhiteSpace(folder.Id))
            {
                issues.Add(Error("FolderIdRequired", "Folder id is required.", folder.Id, "folder"));
                continue;
            }

            if (!folderIds.Add(folder.Id))
            {
                issues.Add(Error("DuplicateFolderId", "Folder ids must be unique.", folder.Id, "folder"));
            }

            if (string.IsNullOrWhiteSpace(folder.Name))
            {
                issues.Add(Error("FolderNameRequired", "Folder name is required.", folder.Id, "folder"));
            }
        }

        HashSet<string> tagNames = new(StringComparer.OrdinalIgnoreCase);
        foreach (Tag tag in exportData.Tags)
        {
            if (!IsValidTagName(tag.Name))
            {
                issues.Add(Error("InvalidTagName", "Tag name is required and must be 64 characters or less.", tag.Name, "tag"));
                continue;
            }

            if (!tagNames.Add(NormalizeTag(tag.Name)))
            {
                issues.Add(Warning("DuplicateTagName", "Duplicate tag name will be merged during import.", tag.Name, "tag"));
            }
        }

        HashSet<string> promptIds = new(StringComparer.Ordinal);
        foreach (Prompt prompt in exportData.Prompts)
        {
            ValidatePrompt(prompt, folderIds, issues);
            if (!string.IsNullOrWhiteSpace(prompt.Id) && !promptIds.Add(prompt.Id))
            {
                issues.Add(Error("DuplicatePromptId", "Prompt ids must be unique within the import file.", prompt.Id, "prompt"));
            }
        }

        var promptsCreated = 0;
        var promptsUpdated = 0;
        var promptsSkipped = 0;
        if (!issues.Any(issue => issue.Severity == ImportValidationSeverity.Error))
        {
            foreach (Prompt prompt in exportData.Prompts)
            {
                Prompt? existing = await _promptRepository.GetAsync(prompt.Id, cancellationToken);
                if (existing is null || options.ConflictMode == ImportConflictMode.Duplicate)
                {
                    promptsCreated++;
                }
                else if (options.ConflictMode == ImportConflictMode.Overwrite)
                {
                    promptsUpdated++;
                }
                else
                {
                    promptsSkipped++;
                }
            }
        }

        return new ImportPlan
        {
            Issues = issues,
            Summary = new ImportSummary
            {
                FoldersCreated = exportData.Folders.Count,
                TagsCreated = exportData.Tags.Count,
                PromptsCreated = promptsCreated,
                PromptsUpdated = promptsUpdated,
                PromptsSkipped = promptsSkipped,
                ValidationErrors = issues.Count(issue => issue.Severity == ImportValidationSeverity.Error),
                ValidationWarnings = issues.Count(issue => issue.Severity == ImportValidationSeverity.Warning),
                DryRun = true
            }
        };
    }

    private Prompt PreparePrompt(Prompt prompt) => prompt with
    {
        Title = prompt.Title.Trim(),
        Body = NormalizeBody(prompt.Body),
        Tags = prompt.Tags
            .Select(NormalizeTag)
            .Where(IsValidTagName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray(),
        Variables = _variableParser.Parse(prompt.Body)
    };

    private static void ValidatePrompt(Prompt prompt, HashSet<string> folderIds, List<ImportValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(prompt.Id))
        {
            issues.Add(Error("PromptIdRequired", "Prompt id is required.", prompt.Id, "prompt"));
        }

        if (string.IsNullOrWhiteSpace(prompt.Title))
        {
            issues.Add(Error("TitleRequired", "Prompt title is required.", prompt.Id, "prompt"));
        }

        if (string.IsNullOrWhiteSpace(prompt.Body))
        {
            issues.Add(Error("BodyRequired", "Prompt body is required.", prompt.Id, "prompt"));
        }

        if (Encoding.UTF8.GetByteCount(prompt.Body ?? string.Empty) > PromptLimits.MaxPromptBodyBytes)
        {
            issues.Add(Error("BodyTooLarge", "Prompt body exceeds the 64KB limit.", prompt.Id, "prompt"));
        }

        if (!string.IsNullOrWhiteSpace(prompt.FolderId) && !folderIds.Contains(prompt.FolderId))
        {
            issues.Add(Error("FolderNotFound", "Prompt references a folder id that is not present in this import.", prompt.Id, "prompt"));
        }

        foreach (string tag in prompt.Tags)
        {
            if (!IsValidTagName(tag))
            {
                issues.Add(Error("InvalidTagName", "Prompt tag is required and must be 64 characters or less.", prompt.Id, "prompt"));
            }
        }
    }

    private static bool IsValidTagName(string? tagName) =>
        !string.IsNullOrWhiteSpace(tagName) && tagName.Trim().Length <= 64;

    private static string NormalizeTag(string tagName) => tagName.Trim().ToLowerInvariant();

    private static string NormalizeBody(string body) => body.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();

    private static ImportValidationIssue Error(string code, string message, string? entityId = null, string? entityType = null) =>
        new() { Code = code, Message = message, EntityId = entityId, EntityType = entityType };

    private static ImportValidationIssue Warning(string code, string message, string? entityId = null, string? entityType = null) =>
        new()
        {
            Severity = ImportValidationSeverity.Warning,
            Code = code,
            Message = message,
            EntityId = entityId,
            EntityType = entityType
        };
}
