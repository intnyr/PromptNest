using PromptNest.Core.Models;

namespace PromptNest.Core.Abstractions;

public interface IPromptService
{
    Task<OperationResult<Prompt>> GetAsync(string id, CancellationToken cancellationToken);

    Task<PagedResult<Prompt>> ListAsync(PromptQuery query, CancellationToken cancellationToken);

    Task<OperationResult<string>> CreateAsync(Prompt prompt, CancellationToken cancellationToken);

    Task<OperationResult> UpdateAsync(Prompt prompt, CancellationToken cancellationToken);

    Task<OperationResult> SoftDeleteAsync(string id, CancellationToken cancellationToken);

    Task<OperationResult<string>> DuplicateAsync(string id, CancellationToken cancellationToken);

    Task<OperationResult> ToggleFavoriteAsync(string id, CancellationToken cancellationToken);
}

public interface IFolderService
{
    Task<IReadOnlyList<Folder>> ListAsync(CancellationToken cancellationToken);
}

public interface ITagService
{
    Task<IReadOnlyList<Tag>> ListAsync(CancellationToken cancellationToken);
}

public interface ISearchService
{
    Task<PagedResult<Prompt>> SearchAsync(string text, PromptQuery query, CancellationToken cancellationToken);
}

public interface IVariableParser
{
    IReadOnlyList<PromptVariable> Parse(string body);
}

public interface IVariableResolver
{
    Task<OperationResult<ResolvedPrompt>> ResolveAsync(
        Prompt prompt,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken);
}

public interface IClipboardService
{
    Task<OperationResult> CopyTextAsync(string text, string? html, CancellationToken cancellationToken);
}

public interface IPromptCopyService
{
    Task<OperationResult<PromptCopyForm>> CreateFormAsync(string promptId, CancellationToken cancellationToken);

    Task<OperationResult<ResolvedPrompt>> CopyAsync(
        string promptId,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken);
}

public interface IImportExportService
{
    Task<OperationResult<PromptNestExport>> ExportAsync(CancellationToken cancellationToken);

    Task<OperationResult<ImportPlan>> PreviewImportAsync(
        PromptNestExport exportData,
        ImportOptions options,
        CancellationToken cancellationToken);

    Task<OperationResult<ImportSummary>> ImportAsync(
        PromptNestExport exportData,
        ImportOptions options,
        CancellationToken cancellationToken);
}

public interface IBackupService
{
    Task<OperationResult<BackupMetadata>> CreateBackupAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<BackupMetadata>> ListBackupsAsync(CancellationToken cancellationToken);

    Task<OperationResult> ApplyRetentionAsync(int keepLast, CancellationToken cancellationToken);
}

public interface IRepositoryPromptScanner
{
    Task<RepositoryPromptScanResult> ScanAsync(RepositoryPromptScanRequest request, CancellationToken cancellationToken);
}

public interface IRepositoryPromptImportNormalizer
{
    RepositoryPromptImportDocument Normalize(
        IReadOnlyList<RepositoryPromptCandidate> candidates,
        RepositoryPromptNormalizeOptions options);
}

public interface ILinearBatchReportFormatter
{
    LinearBatchReportResult Format(LinearBatchReportRequest request);
}
