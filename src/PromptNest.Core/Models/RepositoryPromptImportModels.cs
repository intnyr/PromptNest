namespace PromptNest.Core.Models;

public enum RepositoryPromptRedactionMode
{
    ReportOnly,
    RedactBody,
    Block
}

public sealed record RepositoryPromptScanRequest
{
    public IReadOnlyList<string> RepositoryRoots { get; init; } = [];

    public IReadOnlyList<string> IncludeGlobs { get; init; } = [];

    public IReadOnlyList<string> ExcludeGlobs { get; init; } = [];

    public int MaxFileBytes { get; init; } = 512 * 1024;

    public int MaxCandidateBodyBytes { get; init; } = PromptLimits.MaxPromptBodyBytes;
}

public sealed record RepositoryPromptCandidate
{
    public required string RepositoryRoot { get; init; }

    public required string RepositoryName { get; init; }

    public required string RelativePath { get; init; }

    public required string Body { get; init; }

    public string? TitleHint { get; init; }

    public int? StartLine { get; init; }

    public int? EndLine { get; init; }

    public string Format { get; init; } = "text";

    public double Confidence { get; init; } = 0.5;

    public IReadOnlyList<string> Tags { get; init; } = [];
}

public sealed record RepositoryPromptScanWarning
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public string? RepositoryRoot { get; init; }

    public string? RelativePath { get; init; }
}

public sealed record RepositoryPromptScanSummary
{
    public int RepositoriesScanned { get; init; }

    public int FilesScanned { get; init; }

    public int FilesSkipped { get; init; }

    public int CandidatesFound { get; init; }

    public int Warnings { get; init; }
}

public sealed record RepositoryPromptScanResult
{
    public required RepositoryPromptScanSummary Summary { get; init; }

    public IReadOnlyList<RepositoryPromptCandidate> Candidates { get; init; } = [];

    public IReadOnlyList<RepositoryPromptScanWarning> Warnings { get; init; } = [];
}

public sealed record RepositoryPromptNormalizeOptions
{
    public string RootFolderName { get; init; } = "Repository Imports";

    public RepositoryPromptRedactionMode RedactionMode { get; init; } = RepositoryPromptRedactionMode.ReportOnly;
}

public sealed record RepositoryPromptImportReport
{
    public int CandidatesReceived { get; init; }

    public int PromptsCreated { get; init; }

    public int DuplicatesSkipped { get; init; }

    public int Rejected { get; init; }

    public int PotentialSecrets { get; init; }

    public IReadOnlyList<RepositoryPromptScanWarning> Warnings { get; init; } = [];
}

public sealed record RepositoryPromptImportDocument
{
    public required PromptNestExport Export { get; init; }

    public required RepositoryPromptImportReport Report { get; init; }
}

public sealed record LinearBatchReportRequest
{
    public string? IssueId { get; init; }

    public required string BatchName { get; init; }

    public RepositoryPromptScanResult? ScanResult { get; init; }

    public RepositoryPromptImportReport? ImportReport { get; init; }

    public ImportSummary? ImportSummary { get; init; }

    public IReadOnlyList<string> Repositories { get; init; } = [];

    public IReadOnlyList<string> Notes { get; init; } = [];
}

public sealed record LinearBatchReportResult
{
    public required string Markdown { get; init; }

    public bool Published { get; init; }

    public string? Message { get; init; }
}
