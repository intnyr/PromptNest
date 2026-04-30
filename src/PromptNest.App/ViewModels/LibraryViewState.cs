namespace PromptNest.App.ViewModels;

public sealed record LibraryViewState
{
    public IReadOnlyList<NavigationItemViewModel> NavigationItems { get; init; } = [];

    public IReadOnlyList<FolderNodeViewModel> FolderTree { get; init; } = [];

    public IReadOnlyList<FolderTreeRowViewModel> VisibleFolders { get; init; } = [];

    public IReadOnlyList<NavigationItemViewModel> Collections { get; init; } = [];

    public LibraryQueryState ActiveQuery { get; init; } = new()
    {
        Id = "all",
        Label = "All Prompts"
    };

    public IReadOnlyList<PromptListItemViewModel> PromptList { get; init; } = [];

    public bool IsPromptListLoading { get; init; }

    public string? PromptListErrorMessage { get; init; }

    public string PromptListStateMessage => IsPromptListLoading
        ? "Loading prompts"
        : !string.IsNullOrWhiteSpace(PromptListErrorMessage)
            ? PromptListErrorMessage
        : "No prompts match this view";

    public bool IsPromptListStateVisible => IsPromptListLoading || !string.IsNullOrWhiteSpace(PromptListErrorMessage) || PromptList.Count == 0;

    public PromptDetailViewModel? SelectedPrompt { get; init; }

    public IReadOnlyList<string> TagSuggestions { get; init; } = [];

    public string TagInputText { get; init; } = string.Empty;

    public IReadOnlyList<string> FilteredTagSuggestions => TagSuggestions
        .Where(tag => string.IsNullOrWhiteSpace(TagInputText) || tag.Contains(TagInputText, StringComparison.OrdinalIgnoreCase))
        .Where(tag => SelectedPrompt?.Tags.All(chip => !string.Equals(chip.Name, tag, StringComparison.OrdinalIgnoreCase)) != false)
        .Take(4)
        .ToArray();

    public bool IsTagSuggestionOpen => !string.IsNullOrWhiteSpace(TagInputText) && FilteredTagSuggestions.Count > 0;

    public string SearchText { get; init; } = string.Empty;

    public string SortLabel { get; init; } = "Relevance";

    public LibraryToolbarState Toolbar { get; init; } = new();

    public EditorTab ActiveEditorTab { get; init; } = EditorTab.Edit;

    public PaginationViewModel Pagination { get; init; } = new();

    public ValidationSummaryViewModel Validation { get; init; } = new() { Message = "Not validated" };

    public bool CanSave { get; init; }

    public bool CanCancel { get; init; }
}