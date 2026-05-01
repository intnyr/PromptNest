namespace PromptNest.Core.Models;

public enum ImportConflictMode
{
    Skip,
    Overwrite,
    Duplicate
}

public sealed record PromptNestExport
{
    public int SchemaVersion { get; init; } = 1;

    public required DateTimeOffset ExportedAt { get; init; }

    public IReadOnlyList<Folder> Folders { get; init; } = [];

    public IReadOnlyList<Tag> Tags { get; init; } = [];

    public IReadOnlyList<Prompt> Prompts { get; init; } = [];
}

public sealed record ImportOptions
{
    public ImportConflictMode ConflictMode { get; init; } = ImportConflictMode.Skip;

    public bool DryRun { get; init; }

    public int MaxPromptCount { get; init; } = 10_000;

    public int MaxFolderCount { get; init; } = 2_000;

    public int MaxTagCount { get; init; } = 2_000;
}

public sealed record ImportSummary
{
    public int PromptsCreated { get; init; }

    public int PromptsUpdated { get; init; }

    public int PromptsSkipped { get; init; }

    public int FoldersCreated { get; init; }

    public int TagsCreated { get; init; }

    public int ValidationErrors { get; init; }

    public int ValidationWarnings { get; init; }

    public bool DryRun { get; init; }
}

public enum ImportValidationSeverity
{
    Error,
    Warning
}

public sealed record ImportValidationIssue
{
    public ImportValidationSeverity Severity { get; init; } = ImportValidationSeverity.Error;

    public required string Code { get; init; }

    public required string Message { get; init; }

    public string? EntityId { get; init; }

    public string? EntityType { get; init; }
}

public sealed record ImportPlan
{
    public required ImportSummary Summary { get; init; }

    public IReadOnlyList<ImportValidationIssue> Issues { get; init; } = [];

    public bool HasErrors => Issues.Any(issue => issue.Severity == ImportValidationSeverity.Error);
}

public sealed record BackupMetadata
{
    public required string FilePath { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public long SizeBytes { get; init; }
}
