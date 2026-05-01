using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using PromptNest.App.DeepLinks;
using PromptNest.Core.Abstractions;
using PromptNest.Core.Models;

namespace PromptNest.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IPromptService? promptService;
    private readonly IFolderService? folderService;
    private readonly ITagService? tagService;
    private readonly ISearchService? searchService;
    private readonly IPromptCopyService? promptCopyService;
    private readonly IJumpListService? jumpListService;
    private readonly ISettingsRepository? settingsRepository;
    private readonly IUpdateService? updateService;
    private readonly IImportExportService? importExportService;
    private readonly IBackupService? backupService;
    private IReadOnlyList<Folder> serviceFolders = [];
    private Dictionary<string, Prompt> promptSnapshots = [];
    private CancellationTokenSource? searchRefreshCancellation;

    public MainViewModel()
    {
    }

    public MainViewModel(
        IPromptService promptService,
        IFolderService folderService,
        ITagService tagService,
        ISearchService searchService,
        IPromptCopyService promptCopyService,
        IJumpListService jumpListService,
        ISettingsRepository settingsRepository,
        IUpdateService updateService,
        IImportExportService? importExportService = null,
        IBackupService? backupService = null)
    {
        this.promptService = promptService;
        this.folderService = folderService;
        this.tagService = tagService;
        this.searchService = searchService;
        this.promptCopyService = promptCopyService;
        this.jumpListService = jumpListService;
        this.settingsRepository = settingsRepository;
        this.updateService = updateService;
        this.importExportService = importExportService;
        this.backupService = backupService;
        library = LibraryDesignData.Create() with { IsPromptListLoading = true };
    }

    [ObservableProperty]
    private LibraryViewState library = LibraryDesignData.Create();

    [ObservableProperty]
    private string updateStatusText = "Update status unavailable";

    [ObservableProperty]
    private string dataManagementStatusText = "Import, export, and backup are available";

    [ObservableProperty]
    private string updateChannelText = "Stable";

    [ObservableProperty]
    private bool updateChecksEnabled = true;

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (!HasServices)
        {
            return;
        }

        await LoadSettingsAsync(cancellationToken);
        serviceFolders = await folderService!.ListAsync(cancellationToken);
        IReadOnlyList<Tag> tags = await tagService!.ListAsync(cancellationToken);
        Library = BuildShellState(serviceFolders, tags, Library) with { IsPromptListLoading = true };
        await RefreshPromptListAsync(cancellationToken);
    }

    public void Dispose()
    {
        searchRefreshCancellation?.Cancel();
        searchRefreshCancellation?.Dispose();
    }

    public async Task CheckForUpdatesAsync(CancellationToken cancellationToken)
    {
        if (settingsRepository is null || updateService is null)
        {
            UpdateStatusText = "Update checks are unavailable";
            return;
        }

        AppSettings settings = await settingsRepository.GetAsync(cancellationToken);
        UpdateChecksEnabled = settings.UpdateChecksEnabled;
        UpdateChannelText = settings.UpdateChannel.ToString();

        OperationResult<UpdateStatus> result = await updateService.CheckForUpdatesAsync(settings, cancellationToken);
        UpdateStatusText = result.Succeeded && result.Value is not null
            ? FormatUpdateStatus(result.Value)
            : result.Message ?? "Update check failed";
    }

    public async Task<OperationResult<ImportPlan>> PreviewImportAsync(
        PromptNestExport exportData,
        ImportOptions options,
        CancellationToken cancellationToken)
    {
        if (importExportService is null)
        {
            DataManagementStatusText = "Import is unavailable";
            return OperationResultFactory.Failure<ImportPlan>("ImportUnavailable", "Import service is unavailable.");
        }

        OperationResult<ImportPlan> result = await importExportService.PreviewImportAsync(exportData, options, cancellationToken);
        DataManagementStatusText = result.Succeeded && result.Value is not null
            ? FormatImportSummary(result.Value.Summary)
            : result.Message ?? "Import validation failed";
        return result;
    }

    public async Task<OperationResult<ImportSummary>> ImportAsync(
        PromptNestExport exportData,
        ImportOptions options,
        CancellationToken cancellationToken)
    {
        if (importExportService is null)
        {
            DataManagementStatusText = "Import is unavailable";
            return OperationResultFactory.Failure<ImportSummary>("ImportUnavailable", "Import service is unavailable.");
        }

        OperationResult<ImportSummary> result = await importExportService.ImportAsync(exportData, options, cancellationToken);
        DataManagementStatusText = result.Succeeded && result.Value is not null
            ? FormatImportSummary(result.Value)
            : result.Message ?? "Import failed";

        if (result.Succeeded)
        {
            serviceFolders = folderService is null ? serviceFolders : await folderService.ListAsync(cancellationToken);
            await RefreshPromptListAsync(cancellationToken);
        }

        return result;
    }

    public async Task<OperationResult<PromptNestExport>> ExportPromptsAsync(CancellationToken cancellationToken)
    {
        if (importExportService is null)
        {
            DataManagementStatusText = "Export is unavailable";
            return OperationResultFactory.Failure<PromptNestExport>("ExportUnavailable", "Export service is unavailable.");
        }

        OperationResult<PromptNestExport> result = await importExportService.ExportAsync(cancellationToken);
        DataManagementStatusText = result.Succeeded && result.Value is not null
            ? $"Export prepared with {result.Value.Prompts.Count} prompts"
            : result.Message ?? "Export failed";
        return result;
    }

    public async Task<OperationResult<BackupMetadata>> CreateBackupAsync(CancellationToken cancellationToken)
    {
        if (backupService is null)
        {
            DataManagementStatusText = "Backup is unavailable";
            return OperationResultFactory.Failure<BackupMetadata>("BackupUnavailable", "Backup service is unavailable.");
        }

        OperationResult<BackupMetadata> result = await backupService.CreateBackupAsync(cancellationToken);
        DataManagementStatusText = result.Succeeded && result.Value is not null
            ? $"Backup created at {result.Value.CreatedAt.LocalDateTime:g}"
            : result.Message ?? "Backup failed";
        return result;
    }

    public async Task ApplyDeepLinkAsync(DeepLinkRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        switch (request.Action)
        {
            case DeepLinkAction.OpenLibrary:
                SelectNavigationCommand.Execute("all");
                break;
            case DeepLinkAction.CreatePrompt:
                InvokeToolbarCommandCommand.Execute(LibraryToolbarCommandKind.NewPrompt);
                break;
            case DeepLinkAction.Search:
                Library = Library with
                {
                    SearchText = request.SearchText ?? string.Empty,
                    IsPromptListLoading = true,
                    PromptListErrorMessage = null
                };
                await RefreshPromptListAsync(cancellationToken);
                break;
            case DeepLinkAction.OpenPrompt:
                await OpenPromptFromDeepLinkAsync(request.PromptId, cancellationToken);
                break;
        }
    }

    private async Task OpenPromptFromDeepLinkAsync(string? promptId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(promptId))
        {
            return;
        }

        if (promptSnapshots.ContainsKey(promptId) || Library.PromptList.Any(prompt => prompt.Id == promptId))
        {
            SelectPromptCommand.Execute(promptId);
            return;
        }

        if (!HasServices)
        {
            return;
        }

        OperationResult<Prompt> result = await promptService!.GetAsync(promptId, cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            Library = Library with
            {
                Validation = new ValidationSummaryViewModel
                {
                    State = ValidationState.Failed,
                    Message = result.Message ?? "Prompt link could not be opened"
                }
            };
            return;
        }

        Prompt prompt = result.Value;
        promptSnapshots[prompt.Id] = prompt;
        PromptListItemViewModel row = MapPromptListItem(prompt) with { IsSelected = true };
        Library = Library with
        {
            PromptList = [row, .. Library.PromptList.Where(item => item.Id != prompt.Id).Select(item => item with { IsSelected = false })],
            SelectedPrompt = MapPromptDetail(prompt),
            Validation = ValidatePromptBody(prompt.Body, prompt.Variables.Count)
        };
    }

    private bool HasServices => promptService is not null && folderService is not null && tagService is not null && searchService is not null;

    private bool HasCopyService => promptCopyService is not null;

    private async Task LoadSettingsAsync(CancellationToken cancellationToken)
    {
        if (settingsRepository is null)
        {
            return;
        }

        AppSettings settings = await settingsRepository.GetAsync(cancellationToken);
        UpdateChecksEnabled = settings.UpdateChecksEnabled;
        UpdateChannelText = settings.UpdateChannel.ToString();
        UpdateStatusText = settings.UpdateChecksEnabled
            ? "Update check pending"
            : "Update checks are disabled";
    }

    private static string FormatUpdateStatus(UpdateStatus status)
    {
        string checkedAt = status.CheckedAt is null
            ? string.Empty
            : $" Last checked {status.CheckedAt.Value.LocalDateTime:g}.";

        string version = string.IsNullOrWhiteSpace(status.AvailableVersion)
            ? string.Empty
            : $" Version {status.AvailableVersion}.";

        return $"{status.Message}{version}{checkedAt}";
    }

    private static string FormatImportSummary(ImportSummary summary) =>
        summary.DryRun
            ? $"Dry run: {summary.PromptsCreated} create, {summary.PromptsUpdated} update, {summary.PromptsSkipped} skip"
            : $"Import complete: {summary.PromptsCreated} created, {summary.PromptsUpdated} updated, {summary.PromptsSkipped} skipped";

    private void QueueRefresh()
    {
        if (HasServices)
        {
            _ = RefreshPromptListAsync(CancellationToken.None);
        }
    }

    private void QueueSearchRefresh()
    {
        if (!HasServices)
        {
            return;
        }

        searchRefreshCancellation?.Cancel();
        searchRefreshCancellation?.Dispose();
        searchRefreshCancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = searchRefreshCancellation.Token;

        _ = RefreshSearchAfterDelayAsync(cancellationToken);
    }

    private async Task RefreshSearchAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
            await RefreshPromptListAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task RefreshPromptListAsync(CancellationToken cancellationToken)
    {
        if (!HasServices)
        {
            return;
        }

        try
        {
            PromptQuery query = CreatePromptQuery();
            PagedResult<Prompt> result = string.IsNullOrWhiteSpace(Library.SearchText)
                ? await promptService!.ListAsync(query, cancellationToken)
                : await searchService!.SearchAsync(Library.SearchText, query, cancellationToken);

            PromptListItemViewModel[] rows = result.Items
                .Select(MapPromptListItem)
                .ToArray();
            promptSnapshots = result.Items.ToDictionary(prompt => prompt.Id, StringComparer.Ordinal);

            string? selectedId = Library.SelectedPrompt?.Id ?? rows.FirstOrDefault()?.Id;
            rows = rows.Select(row => row with { IsSelected = row.Id == selectedId }).ToArray();
            PromptDetailViewModel? selectedPrompt = rows.FirstOrDefault(row => row.IsSelected) is { } selectedRow
                ? MapPromptDetail(result.Items.First(prompt => prompt.Id == selectedRow.Id))
                : null;

            Library = Library with
            {
                PromptList = rows,
                SelectedPrompt = selectedPrompt,
                Pagination = new PaginationViewModel
                {
                    PageNumber = (result.Skip / Math.Max(1, result.Take)) + 1,
                    PageSize = result.Take,
                    TotalCount = result.TotalCount
                },
                IsPromptListLoading = false,
                PromptListErrorMessage = null,
                Validation = selectedPrompt is null
                    ? new ValidationSummaryViewModel { State = ValidationState.Unknown, Message = "No prompt selected" }
                    : ValidatePromptBody(selectedPrompt.Body, selectedPrompt.Variables.Count),
                CanSave = false,
                CanCancel = false
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            promptSnapshots = [];
            Library = Library with
            {
                PromptList = [],
                SelectedPrompt = null,
                Pagination = new PaginationViewModel { PageSize = Library.Pagination.PageSize },
                IsPromptListLoading = false,
                PromptListErrorMessage = $"Unable to load prompts: {ex.Message}",
                Validation = new ValidationSummaryViewModel { State = ValidationState.Failed, Message = "Unable to load prompts" },
                CanSave = false,
                CanCancel = false
            };
        }
    }

    private static LibraryViewState BuildShellState(
        IReadOnlyList<Folder> folders,
        IReadOnlyList<Tag> tags,
        LibraryViewState current)
    {
        int totalCount = folders.Sum(folder => folder.PromptCount);
        FolderNodeViewModel[] folderTree = BuildFolderTree(folders, parentId: null);

        return current with
        {
            NavigationItems =
            [
                new NavigationItemViewModel { Id = "home", Label = "Home", Kind = NavigationItemKind.Home },
                new NavigationItemViewModel { Id = "all", Label = "All Prompts", Kind = NavigationItemKind.AllPrompts, Count = totalCount, IsSelected = true },
                new NavigationItemViewModel { Id = "starred", Label = "Starred", Kind = NavigationItemKind.Starred },
                new NavigationItemViewModel { Id = "recent", Label = "Recent", Kind = NavigationItemKind.Recent },
                new NavigationItemViewModel { Id = "trash", Label = "Trash", Kind = NavigationItemKind.Trash }
            ],
            FolderTree = folderTree,
            VisibleFolders = FlattenFolders(folderTree),
            Collections = [],
            ActiveQuery = new LibraryQueryState { Scope = LibraryQueryScope.Navigation, Id = "all", Label = "All Prompts" },
            TagSuggestions = tags.Select(tag => tag.Name).ToArray()
        };
    }

    private static FolderNodeViewModel[] BuildFolderTree(IReadOnlyList<Folder> folders, string? parentId)
    {
        return folders
            .Where(folder => folder.ParentId == parentId)
            .OrderBy(folder => folder.SortOrder)
            .ThenBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase)
            .Select(folder => new FolderNodeViewModel
            {
                Id = folder.Id,
                Name = folder.Name,
                Count = folder.PromptCount,
                IsExpanded = parentId is null,
                Children = BuildFolderTree(folders, folder.Id)
            })
            .ToArray();
    }

    private PromptQuery CreatePromptQuery()
    {
        int pageSize = Math.Max(1, Library.Pagination.PageSize);
        int pageNumber = Math.Max(1, Library.Pagination.PageNumber);
        return new PromptQuery
        {
            FolderId = Library.ActiveQuery.Scope == LibraryQueryScope.Folder ? Library.ActiveQuery.Id : null,
            IsFavorite = Library.ActiveQuery.Id == "starred" ? true : null,
            IncludeDeleted = Library.ActiveQuery.Id == "trash",
            SortBy = MapSortBy(Library.SortLabel),
            SortDescending = Library.SortLabel is not "Name A-Z",
            Skip = (pageNumber - 1) * pageSize,
            Take = pageSize
        };
    }

    private static PromptSortBy MapSortBy(string sortLabel) =>
        sortLabel switch
        {
            "Name A-Z" or "Name Z-A" => PromptSortBy.Title,
            "Oldest" or "Newest" => PromptSortBy.UpdatedAt,
            _ => PromptSortBy.UpdatedAt
        };

    private PromptListItemViewModel MapPromptListItem(Prompt prompt)
    {
        return new PromptListItemViewModel
        {
            Id = prompt.Id,
            Title = prompt.Title,
            Preview = prompt.Body.Replace(Environment.NewLine, " ", StringComparison.Ordinal),
            Date = DateOnly.FromDateTime(prompt.UpdatedAt.LocalDateTime),
            IsFavorite = prompt.IsFavorite,
            Tags = prompt.Tags.Select(tag => new TagChipViewModel { Name = tag, IsRemovable = true }).ToArray()
        };
    }

    private PromptDetailViewModel MapPromptDetail(Prompt prompt)
    {
        return new PromptDetailViewModel
        {
            Id = prompt.Id,
            Title = prompt.Title,
            FolderId = prompt.FolderId,
            FolderPath = ResolveFolderPath(prompt.FolderId),
            Body = prompt.Body,
            Tags = prompt.Tags.Select(tag => new TagChipViewModel { Name = tag, IsRemovable = true }).ToArray(),
            Variables = prompt.Variables.Select(variable => new VariableRowViewModel
            {
                Name = variable.Name,
                DefaultValue = variable.DefaultValue,
                PreviewValue = string.IsNullOrWhiteSpace(variable.DefaultValue) ? variable.Name : variable.DefaultValue
            }).ToArray(),
            UseCount = prompt.UseCount,
            LastUsed = prompt.LastUsedAt.HasValue ? DateOnly.FromDateTime(prompt.LastUsedAt.Value.LocalDateTime) : null
        };
    }

    private string ResolveFolderPath(string? folderId)
    {
        if (string.IsNullOrWhiteSpace(folderId))
        {
            return "No folder";
        }

        Folder? folder = serviceFolders.FirstOrDefault(item => item.Id == folderId);
        return folder?.Name ?? folderId;
    }

    public static IReadOnlyList<FolderTreeRowViewModel> FlattenFolders(IReadOnlyList<FolderNodeViewModel> folders)
    {
        ArgumentNullException.ThrowIfNull(folders);

        var rows = new List<FolderTreeRowViewModel>();
        foreach (FolderNodeViewModel folder in folders)
        {
            AppendFolder(rows, folder, depth: 0);
        }

        return rows;
    }

    [RelayCommand]
    private void SelectNavigation(string id)
    {
        NavigationItemViewModel? selected = Library.NavigationItems.FirstOrDefault(item => item.Id == id);
        if (selected is null)
        {
            return;
        }

        Library = Library with
        {
            NavigationItems = Library.NavigationItems
                .Select(item => item with { IsSelected = item.Id == id })
                .ToArray(),
            FolderTree = SetFolderSelection(Library.FolderTree, selectedId: null),
            VisibleFolders = FlattenFolders(SetFolderSelection(Library.FolderTree, selectedId: null)),
            Collections = Library.Collections
                .Select(item => item with { IsSelected = false })
                .ToArray(),
            ActiveQuery = new LibraryQueryState
            {
                Scope = LibraryQueryScope.Navigation,
                Id = selected.Id,
                Label = selected.Label
            }
        };
        QueueRefresh();
    }

    [RelayCommand]
    private void SelectCollection(string id)
    {
        NavigationItemViewModel? selected = Library.Collections.FirstOrDefault(item => item.Id == id);
        if (selected is null)
        {
            return;
        }

        Library = Library with
        {
            NavigationItems = Library.NavigationItems
                .Select(item => item with { IsSelected = false })
                .ToArray(),
            FolderTree = SetFolderSelection(Library.FolderTree, selectedId: null),
            VisibleFolders = FlattenFolders(SetFolderSelection(Library.FolderTree, selectedId: null)),
            Collections = Library.Collections
                .Select(item => item with { IsSelected = item.Id == id })
                .ToArray(),
            ActiveQuery = new LibraryQueryState
            {
                Scope = LibraryQueryScope.Collection,
                Id = selected.Id,
                Label = selected.Label
            }
        };
        QueueRefresh();
    }

    [RelayCommand]
    private void SelectFolder(string id)
    {
        FolderNodeViewModel? selected = FindFolder(Library.FolderTree, id);
        if (selected is null)
        {
            return;
        }

        FolderNodeViewModel[] folders = SetFolderSelection(Library.FolderTree, id);

        Library = Library with
        {
            NavigationItems = Library.NavigationItems
                .Select(item => item with { IsSelected = false })
                .ToArray(),
            FolderTree = folders,
            VisibleFolders = FlattenFolders(folders),
            Collections = Library.Collections
                .Select(item => item with { IsSelected = false })
                .ToArray(),
            ActiveQuery = new LibraryQueryState
            {
                Scope = LibraryQueryScope.Folder,
                Id = selected.Id,
                Label = selected.Name
            }
        };
        QueueRefresh();
    }

    [RelayCommand]
    private void ToggleFolder(string id)
    {
        FolderNodeViewModel[] folders = ToggleFolderExpansion(Library.FolderTree, id);

        Library = Library with
        {
            FolderTree = folders,
            VisibleFolders = FlattenFolders(folders)
        };
    }

    [RelayCommand]
    private void UpdateSearch(string searchText)
    {
        Library = Library with
        {
            SearchText = searchText,
            IsPromptListLoading = true,
            PromptListErrorMessage = null
        };
        QueueSearchRefresh();
    }

    [RelayCommand]
    private void ClearSearch()
    {
        searchRefreshCancellation?.Cancel();
        Library = Library with
        {
            SearchText = string.Empty,
            IsPromptListLoading = true,
            Toolbar = Library.Toolbar with { LastCommand = LibraryToolbarCommandKind.ClearSearch }
        };
        QueueRefresh();
    }

    [RelayCommand]
    private void ChangeSort(string sortLabel)
    {
        if (string.IsNullOrWhiteSpace(sortLabel) || !Library.Toolbar.SortOptions.Contains(sortLabel, StringComparer.Ordinal))
        {
            return;
        }

        Library = Library with
        {
            SortLabel = sortLabel
        };
        QueueRefresh();
    }

    [RelayCommand]
    private void SelectPrompt(string id)
    {
        PromptListItemViewModel? selected = Library.PromptList.FirstOrDefault(prompt => prompt.Id == id);
        if (selected is null)
        {
            return;
        }

        Library = Library with
        {
            PromptList = Library.PromptList
                .Select(prompt => prompt with { IsSelected = prompt.Id == id })
                .ToArray(),
            SelectedPrompt = promptSnapshots.TryGetValue(id, out Prompt? prompt) ? MapPromptDetail(prompt) : CreatePromptDetail(selected)
        };
    }

    [RelayCommand]
    private void TogglePromptFavorite(string id)
    {
        PromptListItemViewModel? selected = Library.PromptList.FirstOrDefault(prompt => prompt.Id == id);
        if (selected is null)
        {
            return;
        }

        Library = Library with
        {
            PromptList = Library.PromptList
                .Select(prompt => prompt.Id == id ? prompt with { IsFavorite = !prompt.IsFavorite } : prompt)
                .ToArray()
        };
        if (HasServices)
        {
            _ = ToggleFavoriteAndRefreshJumpListAsync(id);
        }
    }

    private async Task ToggleFavoriteAndRefreshJumpListAsync(string id)
    {
        await promptService!.ToggleFavoriteAsync(id, CancellationToken.None);
        if (jumpListService is not null)
        {
            PagedResult<Prompt> favorites = await promptService.ListAsync(
                new PromptQuery { IsFavorite = true, Take = 5, SortBy = PromptSortBy.UpdatedAt, SortDescending = true },
                CancellationToken.None);
            await jumpListService.RefreshFavoritesAsync(favorites.Items, CancellationToken.None);
        }
    }

    [RelayCommand]
    private async Task DuplicateSelectedPrompt(CancellationToken cancellationToken)
    {
        if (!HasServices || Library.SelectedPrompt is null)
        {
            Library = Library with { Toolbar = Library.Toolbar with { LastCommand = LibraryToolbarCommandKind.DuplicatePrompt } };
            return;
        }

        Library = Library with { Toolbar = Library.Toolbar with { LastCommand = LibraryToolbarCommandKind.DuplicatePrompt } };
        OperationResult<string> result = await promptService!.DuplicateAsync(Library.SelectedPrompt.Id, cancellationToken);
        if (result.Succeeded)
        {
            await RefreshPromptListAsync(cancellationToken);
        }
    }

    [RelayCommand]
    private async Task DeleteSelectedPrompt(CancellationToken cancellationToken)
    {
        if (!HasServices || Library.SelectedPrompt is null)
        {
            Library = Library with { Toolbar = Library.Toolbar with { LastCommand = LibraryToolbarCommandKind.DeletePrompt } };
            return;
        }

        Library = Library with { Toolbar = Library.Toolbar with { LastCommand = LibraryToolbarCommandKind.DeletePrompt } };
        OperationResult result = await promptService!.SoftDeleteAsync(Library.SelectedPrompt.Id, cancellationToken);
        if (result.Succeeded)
        {
            await RefreshPromptListAsync(cancellationToken);
        }
    }

    public async Task<OperationResult<PromptCopyForm>> PrepareCopySelectedPromptAsync(CancellationToken cancellationToken)
    {
        if (!HasCopyService || Library.SelectedPrompt is null)
        {
            return OperationResultFactory.Failure<PromptCopyForm>("NoPromptSelected", "No prompt is selected.");
        }

        return await promptCopyService!.CreateFormAsync(Library.SelectedPrompt.Id, cancellationToken);
    }

    public async Task<OperationResult<ResolvedPrompt>> CopySelectedPromptAsync(
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken)
    {
        if (!HasCopyService || Library.SelectedPrompt is null)
        {
            return OperationResultFactory.Failure<ResolvedPrompt>("NoPromptSelected", "No prompt is selected.");
        }

        OperationResult<ResolvedPrompt> result = await promptCopyService!.CopyAsync(Library.SelectedPrompt.Id, values, cancellationToken);
        Library = Library with
        {
            Validation = result.Succeeded
                ? new ValidationSummaryViewModel
                {
                    State = ValidationState.Passed,
                    Message = $"Copied {Library.SelectedPrompt.Title}",
                    VariableCount = Library.SelectedPrompt.Variables.Count
                }
                : new ValidationSummaryViewModel
                {
                    State = ValidationState.Failed,
                    Message = result.Message ?? "Copy failed",
                    VariableCount = Library.SelectedPrompt.Variables.Count
                }
        };

        if (result.Succeeded)
        {
            QueueRefresh();
        }

        return result;
    }

    [RelayCommand]
    private void GoToPreviousPage()
    {
        if (!Library.Pagination.CanMovePrevious)
        {
            return;
        }

        Library = Library with
        {
            Pagination = Library.Pagination with { PageNumber = Library.Pagination.PageNumber - 1 }
        };
        QueueRefresh();
    }

    [RelayCommand]
    private void GoToNextPage()
    {
        if (!Library.Pagination.CanMoveNext)
        {
            return;
        }

        Library = Library with
        {
            Pagination = Library.Pagination with { PageNumber = Library.Pagination.PageNumber + 1 }
        };
        QueueRefresh();
    }

    [RelayCommand]
    private void InvokeToolbarCommand(LibraryToolbarCommandKind command)
    {
        bool isFilterOpen = command == LibraryToolbarCommandKind.ToggleFilters
            ? !Library.Toolbar.IsFilterOpen
            : Library.Toolbar.IsFilterOpen;

        Library = Library with
        {
            Toolbar = Library.Toolbar with
            {
                LastCommand = command,
                IsFilterOpen = isFilterOpen
            }
        };
    }

    [RelayCommand]
    private void MarkEditorDirty()
    {
        Library = Library with
        {
            CanSave = true,
            CanCancel = true
        };
    }

    [RelayCommand]
    private void UpdatePromptTitle(string title)
    {
        if (Library.SelectedPrompt is null)
        {
            return;
        }

        PromptDetailViewModel selectedPrompt = Library.SelectedPrompt with { Title = title };
        Library = Library with
        {
            SelectedPrompt = selectedPrompt,
            PromptList = Library.PromptList.Select(prompt => prompt.Id == selectedPrompt.Id ? prompt with { Title = title } : prompt).ToArray(),
            CanSave = true,
            CanCancel = true
        };
    }

    [RelayCommand]
    private async Task SaveEditor(CancellationToken cancellationToken)
    {
        if (HasServices && Library.SelectedPrompt is not null)
        {
            PromptDetailViewModel selected = Library.SelectedPrompt;
            DateTimeOffset createdAt = promptSnapshots.TryGetValue(selected.Id, out Prompt? snapshot)
                ? snapshot.CreatedAt
                : DateTimeOffset.UtcNow;
            var prompt = new Prompt
            {
                Id = selected.Id,
                Title = selected.Title,
                Body = selected.Body,
                FolderId = selected.FolderId,
                IsFavorite = Library.PromptList.FirstOrDefault(item => item.Id == selected.Id)?.IsFavorite ?? false,
                Tags = selected.Tags.Select(tag => tag.Name).ToArray(),
                Variables = selected.Variables.Select(variable => new PromptVariable
                {
                    Name = variable.Name,
                    DefaultValue = variable.DefaultValue
                }).ToArray(),
                UseCount = selected.UseCount,
                CreatedAt = createdAt,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            OperationResult result = await promptService!.UpdateAsync(prompt, cancellationToken);
            if (!result.Succeeded)
            {
                Library = Library with
                {
                    Validation = new ValidationSummaryViewModel
                    {
                        State = ValidationState.Failed,
                        Message = result.Message ?? "Save failed",
                        VariableCount = selected.Variables.Count
                    }
                };
                return;
            }
        }

        Library = Library with
        {
            CanSave = false,
            CanCancel = false
        };
    }

    [RelayCommand]
    private async Task CancelEditor(CancellationToken cancellationToken)
    {
        if (HasServices && Library.SelectedPrompt is not null)
        {
            OperationResult<Prompt> result = await promptService!.GetAsync(Library.SelectedPrompt.Id, cancellationToken);
            if (result.Succeeded && result.Value is not null)
            {
                Library = Library with
                {
                    SelectedPrompt = MapPromptDetail(result.Value),
                    CanSave = false,
                    CanCancel = false
                };
                return;
            }
        }

        PromptListItemViewModel? selected = Library.PromptList.FirstOrDefault(prompt => prompt.IsSelected);
        Library = Library with
        {
            SelectedPrompt = selected is null ? Library.SelectedPrompt : CreatePromptDetail(selected),
            CanSave = false,
            CanCancel = false
        };
    }

    [RelayCommand]
    private void UpdatePromptBody(string body)
    {
        if (Library.SelectedPrompt is null)
        {
            return;
        }

        VariableRowViewModel[] variables = ExtractVariableRows(body);
        ValidationSummaryViewModel validation = ValidatePromptBody(body, variables.Length);
        PromptDetailViewModel selectedPrompt = Library.SelectedPrompt with
        {
            Body = body,
            Variables = variables
        };

        Library = Library with
        {
            SelectedPrompt = selectedPrompt,
            Validation = validation,
            CanSave = true,
            CanCancel = true
        };
    }

    [RelayCommand]
    private void UpdateTagInput(string text)
    {
        Library = Library with
        {
            TagInputText = text
        };
    }

    [RelayCommand]
    private void AddTagToSelectedPrompt(string tagName)
    {
        string normalized = NormalizeTagName(tagName);
        if (string.IsNullOrWhiteSpace(normalized) || Library.SelectedPrompt is null)
        {
            return;
        }

        if (Library.SelectedPrompt.Tags.Any(tag => string.Equals(tag.Name, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            Library = Library with { TagInputText = string.Empty };
            return;
        }

        var tag = new TagChipViewModel { Name = normalized, Color = "#303173", IsRemovable = true };
        PromptDetailViewModel selectedPrompt = Library.SelectedPrompt with
        {
            Tags = [.. Library.SelectedPrompt.Tags, tag]
        };

        Library = Library with
        {
            SelectedPrompt = selectedPrompt,
            PromptList = UpdatePromptListTags(selectedPrompt),
            TagInputText = string.Empty,
            CanSave = true,
            CanCancel = true
        };
    }

    [RelayCommand]
    private void RemoveTagFromSelectedPrompt(string tagName)
    {
        if (Library.SelectedPrompt is null)
        {
            return;
        }

        PromptDetailViewModel selectedPrompt = Library.SelectedPrompt with
        {
            Tags = Library.SelectedPrompt.Tags
                .Where(tag => !string.Equals(tag.Name, tagName, StringComparison.OrdinalIgnoreCase))
                .ToArray()
        };

        Library = Library with
        {
            SelectedPrompt = selectedPrompt,
            PromptList = UpdatePromptListTags(selectedPrompt),
            CanSave = true,
            CanCancel = true
        };
    }

    private static PromptDetailViewModel CreatePromptDetail(PromptListItemViewModel prompt)
    {
        return new PromptDetailViewModel
        {
            Id = prompt.Id,
            Title = prompt.Title,
            FolderPath = "01_Research / Market Research",
            Body = prompt.Preview.Replace("...", Environment.NewLine + Environment.NewLine + "Add detailed instructions here.", StringComparison.Ordinal),
            Tags = prompt.Tags,
            Variables = ExtractVariableRows(prompt.Preview),
            UseCount = 0,
            LastUsed = prompt.Date
        };
    }

    private static VariableRowViewModel[] ExtractVariableRows(string text)
    {
        return text
            .Split(["{{", "}}"], StringSplitOptions.None)
            .Where((_, index) => index % 2 == 1)
            .Select(CreateVariableRow)
            .Where(variable => !string.IsNullOrWhiteSpace(variable.Name))
            .DistinctBy(variable => variable.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static VariableRowViewModel CreateVariableRow(string token)
    {
        string[] parts = token.Split('|', 2, StringSplitOptions.TrimEntries);
        string? defaultValue = parts.Length > 1 ? parts[1] : null;
        return new VariableRowViewModel
        {
            Name = parts[0],
            DefaultValue = defaultValue,
            PreviewValue = string.IsNullOrWhiteSpace(defaultValue) ? parts[0] : defaultValue
        };
    }

    private static ValidationSummaryViewModel ValidatePromptBody(string body, int variableCount)
    {
        int openCount = body.Split("{{").Length - 1;
        int closeCount = body.Split("}}").Length - 1;
        bool valid = openCount == closeCount;

        return new ValidationSummaryViewModel
        {
            State = valid ? ValidationState.Passed : ValidationState.Failed,
            Message = valid ? "Validation passed" : "Invalid placeholder syntax",
            VariableCount = variableCount
        };
    }

    private PromptListItemViewModel[] UpdatePromptListTags(PromptDetailViewModel selectedPrompt)
    {
        return Library.PromptList
            .Select(prompt => prompt.Id == selectedPrompt.Id ? prompt with { Tags = selectedPrompt.Tags } : prompt)
            .ToArray();
    }

    private static string NormalizeTagName(string tagName)
    {
        return tagName.Trim().ToLowerInvariant();
    }

    private static void AppendFolder(ICollection<FolderTreeRowViewModel> rows, FolderNodeViewModel folder, int depth)
    {
        rows.Add(new FolderTreeRowViewModel
        {
            Id = folder.Id,
            Name = folder.Name,
            Count = folder.Count,
            Depth = depth,
            HasChildren = folder.Children.Count > 0,
            IsExpanded = folder.IsExpanded,
            IsSelected = folder.IsSelected
        });

        if (!folder.IsExpanded)
        {
            return;
        }

        foreach (FolderNodeViewModel child in folder.Children)
        {
            AppendFolder(rows, child, depth + 1);
        }
    }

    private static FolderNodeViewModel? FindFolder(IReadOnlyList<FolderNodeViewModel> folders, string id)
    {
        foreach (FolderNodeViewModel folder in folders)
        {
            if (folder.Id == id)
            {
                return folder;
            }

            FolderNodeViewModel? child = FindFolder(folder.Children, id);
            if (child is not null)
            {
                return child;
            }
        }

        return null;
    }

    private static FolderNodeViewModel[] SetFolderSelection(IReadOnlyList<FolderNodeViewModel> folders, string? selectedId)
    {
        return folders
            .Select(folder => folder with
            {
                IsSelected = folder.Id == selectedId,
                Children = SetFolderSelection(folder.Children, selectedId)
            })
            .ToArray();
    }

    private static FolderNodeViewModel[] ToggleFolderExpansion(IReadOnlyList<FolderNodeViewModel> folders, string id)
    {
        return folders
            .Select(folder => folder with
            {
                IsExpanded = folder.Id == id ? !folder.IsExpanded : folder.IsExpanded,
                Children = ToggleFolderExpansion(folder.Children, id)
            })
            .ToArray();
    }
}
