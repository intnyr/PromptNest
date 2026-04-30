using PromptNest.Core.Abstractions;
using PromptNest.Core.Models;

namespace PromptNest.Core.Services;

public sealed class ImportExportService : IImportExportService
{
    private readonly IFolderRepository _folderRepository;
    private readonly ITagRepository _tagRepository;
    private readonly IPromptRepository _promptRepository;

    public ImportExportService(
        IFolderRepository folderRepository,
        ITagRepository tagRepository,
        IPromptRepository promptRepository)
    {
        _folderRepository = folderRepository;
        _tagRepository = tagRepository;
        _promptRepository = promptRepository;
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

    public async Task<OperationResult<ImportSummary>> ImportAsync(
        PromptNestExport exportData,
        ImportOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(exportData);
        ArgumentNullException.ThrowIfNull(options);

        if (exportData.SchemaVersion != 1)
        {
            return OperationResultFactory.Failure<ImportSummary>(
                "UnsupportedSchema",
                $"Export schema version {exportData.SchemaVersion} is not supported.");
        }

        var foldersCreated = 0;
        var tagsCreated = 0;
        var promptsCreated = 0;
        var promptsUpdated = 0;
        var promptsSkipped = 0;

        foreach (var folder in exportData.Folders)
        {
            await _folderRepository.CreateAsync(folder, cancellationToken);
            foldersCreated++;
        }

        foreach (var tag in exportData.Tags)
        {
            await _tagRepository.UpsertAsync(tag, cancellationToken);
            tagsCreated++;
        }

        foreach (var prompt in exportData.Prompts)
        {
            var existing = await _promptRepository.GetAsync(prompt.Id, cancellationToken);
            if (existing is null)
            {
                await _promptRepository.CreateAsync(prompt, cancellationToken);
                promptsCreated++;
                continue;
            }

            switch (options.ConflictMode)
            {
                case ImportConflictMode.Skip:
                    promptsSkipped++;
                    break;
                case ImportConflictMode.Overwrite:
                    await _promptRepository.UpdateAsync(prompt, cancellationToken);
                    promptsUpdated++;
                    break;
                case ImportConflictMode.Duplicate:
                    await _promptRepository.CreateAsync(prompt with { Id = Guid.NewGuid().ToString("N") }, cancellationToken);
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
                PromptsSkipped = promptsSkipped
            });
    }
}