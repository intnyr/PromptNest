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
}

public sealed record ImportSummary
{
    public int PromptsCreated { get; init; }

    public int PromptsUpdated { get; init; }

    public int PromptsSkipped { get; init; }

    public int FoldersCreated { get; init; }

    public int TagsCreated { get; init; }
}

public sealed record BackupMetadata
{
    public required string FilePath { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public long SizeBytes { get; init; }
}