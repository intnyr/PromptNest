using System.Globalization;

using PromptNest.Core.Models;

namespace PromptNest.App.ViewModels;

public enum NavigationItemKind
{
    Home,
    AllPrompts,
    Starred,
    Recent,
    Trash,
    Collection
}

public enum EditorTab
{
    Edit,
    Preview,
    Variables,
    History
}

public enum ValidationState
{
    Unknown,
    Passed,
    Failed
}

public enum LibraryQueryScope
{
    Navigation,
    Folder,
    Collection
}

public enum LibraryToolbarCommandKind
{
    None,
    NewPrompt,
    DuplicatePrompt,
    NewFolder,
    ManageTags,
    ToggleFilters,
    OpenOverflow,
    ClearSearch,
    DeletePrompt
}

public sealed record NavigationItemViewModel
{
    public required string Id { get; init; }

    public required string Label { get; init; }

    public NavigationItemKind Kind { get; init; }

    public int? Count { get; init; }

    public bool IsSelected { get; init; }

    public string CountText => Count.HasValue ? Count.Value.ToString("N0", CultureInfo.InvariantCulture) : string.Empty;
}

public sealed record FolderNodeViewModel
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public int Count { get; init; }

    public bool IsExpanded { get; init; }

    public bool IsSelected { get; init; }

    public IReadOnlyList<FolderNodeViewModel> Children { get; init; } = [];

    public string CountText => Count.ToString("N0", CultureInfo.InvariantCulture);
}

public sealed record FolderTreeRowViewModel
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public int Count { get; init; }

    public int Depth { get; init; }

    public bool HasChildren { get; init; }

    public bool IsExpanded { get; init; }

    public bool IsSelected { get; init; }

    public string CountText => Count.ToString("N0", CultureInfo.InvariantCulture);

    public double IndentPixels => Depth * 18;
}

public sealed record LibraryQueryState
{
    public LibraryQueryScope Scope { get; init; } = LibraryQueryScope.Navigation;

    public required string Id { get; init; }

    public required string Label { get; init; }
}

public sealed record LibraryToolbarState
{
    public IReadOnlyList<string> SortOptions { get; init; } =
    [
        "Relevance",
        "Newest",
        "Oldest",
        "Name A-Z",
        "Name Z-A"
    ];

    public LibraryToolbarCommandKind LastCommand { get; init; }

    public bool IsFilterOpen { get; init; }

    public bool CanDuplicate { get; init; } = true;
}

public sealed record TagChipViewModel
{
    public required string Name { get; init; }

    public string? Color { get; init; }

    public int? Count { get; init; }

    public bool IsRemovable { get; init; }
}

public sealed record PromptListItemViewModel
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required string Preview { get; init; }

    public required DateOnly Date { get; init; }

    public bool IsFavorite { get; init; }

    public bool IsSelected { get; init; }

    public IReadOnlyList<TagChipViewModel> Tags { get; init; } = [];

    public string DateText => Date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);

    public string FavoriteGlyph => IsFavorite ? "\uE735" : "\uE734";
}

public sealed record PromptDetailViewModel
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public string? FolderId { get; init; }

    public required string FolderPath { get; init; }

    public required string Body { get; init; }

    public IReadOnlyList<TagChipViewModel> Tags { get; init; } = [];

    public IReadOnlyList<VariableRowViewModel> Variables { get; init; } = [];

    public int UseCount { get; init; }

    public DateOnly? LastUsed { get; init; }

    public int CharacterCount => Body.Length;
}

public sealed record VariableRowViewModel
{
    public required string Name { get; init; }

    public string? DefaultValue { get; init; }

    public VariableValueType Type { get; init; } = VariableValueType.Text;

    public string? PreviewValue { get; init; }

    public bool CanDelete { get; init; } = true;
}

public sealed record PaginationViewModel
{
    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public int TotalCount { get; init; }

    public int FirstVisibleItem => TotalCount == 0 ? 0 : ((PageNumber - 1) * PageSize) + 1;

    public int LastVisibleItem => Math.Min(PageNumber * PageSize, TotalCount);

    public int PageCount => TotalCount == 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);

    public bool CanMovePrevious => PageNumber > 1;

    public bool CanMoveNext => PageNumber < PageCount;

    public string ResultCountText => TotalCount.ToString("N0", CultureInfo.InvariantCulture) + " results";

    public string RangeText => TotalCount == 0
        ? "0 of 0"
        : $"{FirstVisibleItem.ToString("N0", CultureInfo.InvariantCulture)}-{LastVisibleItem.ToString("N0", CultureInfo.InvariantCulture)} of {TotalCount.ToString("N0", CultureInfo.InvariantCulture)}";
}

public sealed record ValidationSummaryViewModel
{
    public ValidationState State { get; init; } = ValidationState.Unknown;

    public required string Message { get; init; }

    public int VariableCount { get; init; }

    public string SummaryText => $"{Message}   -   {VariableCount} variables";
}