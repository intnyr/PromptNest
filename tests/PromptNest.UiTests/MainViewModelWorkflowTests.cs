using FluentAssertions;

using PromptNest.App.ViewModels;
using PromptNest.Core.Abstractions;
using PromptNest.Core.Models;

namespace PromptNest.UiTests;

public sealed class MainViewModelWorkflowTests
{
    [Fact]
    public async Task LoadAsyncBuildsShellStateFromServices()
    {
        var services = new ViewModelServiceFixture();
        var viewModel = services.CreateViewModel();

        await viewModel.LoadAsync(CancellationToken.None);

        viewModel.Library.PromptList.Should().ContainSingle(prompt => prompt.Id == "market-analysis");
        viewModel.Library.SelectedPrompt?.Title.Should().Be("Market Analysis - Industry Overview");
        viewModel.Library.TagSuggestions.Should().Contain(["analysis", "market", "research"]);
        viewModel.Library.Pagination.TotalCount.Should().Be(1);
        viewModel.Library.NavigationItems.Single(item => item.Id == "all").CountText.Should().Be("3");
        viewModel.Library.VisibleFolders.Should().Contain(row => row.Id == "market-research" && row.Depth == 1);
    }

    [Fact]
    public async Task DebouncedSearchAndSortRefreshQueryComposition()
    {
        var services = new ViewModelServiceFixture();
        var viewModel = services.CreateViewModel();
        await viewModel.LoadAsync(CancellationToken.None);

        viewModel.UpdateSearchCommand.Execute("m");
        viewModel.UpdateSearchCommand.Execute("ma");
        viewModel.UpdateSearchCommand.Execute("market");
        await ViewModelServiceFixture.WaitForAsync(() => services.SearchService.LastSearchText == "market");

        services.SearchService.SearchTexts.Should().ContainSingle().Which.Should().Be("market");
        services.SearchService.LastQuery.Should().NotBeNull();
        services.SearchService.LastQuery!.Take.Should().Be(20);

        viewModel.ChangeSortCommand.Execute("Name A-Z");
        await ViewModelServiceFixture.WaitForAsync(() => services.SearchService.LastQuery?.SortBy == PromptSortBy.Title);

        services.SearchService.LastQuery!.SortDescending.Should().BeFalse();
    }

    [Fact]
    public async Task NavigationFolderPaginationAndEmptyStatesUpdateQueries()
    {
        var services = new ViewModelServiceFixture();
        var viewModel = services.CreateViewModel();
        await viewModel.LoadAsync(CancellationToken.None);

        viewModel.SelectNavigationCommand.Execute("starred");
        await ViewModelServiceFixture.WaitForAsync(() => services.PromptService.LastListQuery?.IsFavorite == true);

        viewModel.SelectFolderCommand.Execute("market-research");
        await ViewModelServiceFixture.WaitForAsync(() => services.PromptService.LastListQuery?.FolderId == "market-research");
        viewModel.Library.ActiveQuery.Scope.Should().Be(LibraryQueryScope.Folder);

        viewModel.ToggleFolderCommand.Execute("research");
        viewModel.Library.VisibleFolders.Should().NotContain(row => row.Id == "market-research");
        viewModel.ToggleFolderCommand.Execute("research");
        viewModel.Library.VisibleFolders.Should().Contain(row => row.Id == "market-research");

        services.PromptService.Prompts.Clear();
        viewModel.SelectNavigationCommand.Execute("trash");
        await ViewModelServiceFixture.WaitForAsync(() => services.PromptService.LastListQuery?.IncludeDeleted == true);

        viewModel.Library.PromptList.Should().BeEmpty();
        viewModel.Library.IsPromptListStateVisible.Should().BeTrue();
        viewModel.Library.PromptListStateMessage.Should().Be("No prompts match this view");
        viewModel.Library.Pagination.RangeText.Should().Be("0 of 0");
    }

    [Fact]
    public async Task SearchFailureShowsErrorStateWithoutThrowing()
    {
        var services = new ViewModelServiceFixture();
        var viewModel = services.CreateViewModel();
        await viewModel.LoadAsync(CancellationToken.None);

        services.SearchService.ThrowOnSearch = true;

        viewModel.UpdateSearchCommand.Execute("broken");
        await ViewModelServiceFixture.WaitForAsync(() => viewModel.Library.PromptListErrorMessage is not null);

        viewModel.Library.PromptList.Should().BeEmpty();
        viewModel.Library.SelectedPrompt.Should().BeNull();
        viewModel.Library.IsPromptListStateVisible.Should().BeTrue();
        viewModel.Library.PromptListStateMessage.Should().Contain("Unable to load prompts");
    }

    [Fact]
    public async Task SelectionDirtySaveAndCancelUseServiceState()
    {
        var services = new ViewModelServiceFixture();
        var viewModel = services.CreateViewModel();
        await viewModel.LoadAsync(CancellationToken.None);

        viewModel.UpdatePromptTitleCommand.Execute("Updated title");
        viewModel.UpdatePromptBodyCommand.Execute("Hello {{industry|SaaS}} for {{audience}}");

        viewModel.Library.CanSave.Should().BeTrue();
        viewModel.Library.SelectedPrompt?.Variables.Should().Contain(variable => variable.Name == "industry" && variable.DefaultValue == "SaaS");
        viewModel.Library.SelectedPrompt?.Variables.Should().Contain(variable => variable.Name == "audience");
        viewModel.Library.Validation.State.Should().Be(ValidationState.Passed);

        viewModel.UpdatePromptBodyCommand.Execute("Hello {{industry");
        viewModel.Library.Validation.State.Should().Be(ValidationState.Failed);
        viewModel.UpdatePromptBodyCommand.Execute("Hello {{industry|SaaS}} for {{audience}}");

        await viewModel.SaveEditorCommand.ExecuteAsync(null);

        services.PromptService.UpdatedPrompt?.Title.Should().Be("Updated title");
        services.PromptService.UpdatedPrompt?.Variables.Should().ContainSingle(variable => variable.Name == "industry");
        viewModel.Library.CanSave.Should().BeFalse();

        viewModel.UpdatePromptTitleCommand.Execute("Temporary title");
        await viewModel.CancelEditorCommand.ExecuteAsync(null);

        viewModel.Library.SelectedPrompt?.Title.Should().Be("Updated title");
        viewModel.Library.CanCancel.Should().BeFalse();
    }

    [Fact]
    public async Task TagAndFavoriteFlowsUpdatePromptState()
    {
        var services = new ViewModelServiceFixture();
        var viewModel = services.CreateViewModel();
        await viewModel.LoadAsync(CancellationToken.None);

        viewModel.UpdateTagInputCommand.Execute("str");
        viewModel.Library.IsTagSuggestionOpen.Should().BeTrue();
        viewModel.Library.FilteredTagSuggestions.Should().ContainSingle().Which.Should().Be("strategy");

        viewModel.AddTagToSelectedPromptCommand.Execute("Strategy");
        viewModel.Library.SelectedPrompt?.Tags.Should().Contain(tag => tag.Name == "strategy");
        viewModel.Library.TagInputText.Should().BeEmpty();

        viewModel.RemoveTagFromSelectedPromptCommand.Execute("strategy");
        viewModel.Library.SelectedPrompt?.Tags.Should().NotContain(tag => tag.Name == "strategy");

        viewModel.TogglePromptFavoriteCommand.Execute("market-analysis");

        services.PromptService.ToggleFavoriteCalls.Should().ContainSingle().Which.Should().Be("market-analysis");
        viewModel.Library.PromptList.Single().IsFavorite.Should().BeFalse();
    }

    [Fact]
    public async Task DuplicateDeleteAndToolbarCommandsAreRouted()
    {
        var services = new ViewModelServiceFixture();
        var viewModel = services.CreateViewModel();
        await viewModel.LoadAsync(CancellationToken.None);

        viewModel.InvokeToolbarCommandCommand.Execute(LibraryToolbarCommandKind.NewPrompt);
        viewModel.Library.Toolbar.LastCommand.Should().Be(LibraryToolbarCommandKind.NewPrompt);

        viewModel.InvokeToolbarCommandCommand.Execute(LibraryToolbarCommandKind.NewFolder);
        viewModel.Library.Toolbar.LastCommand.Should().Be(LibraryToolbarCommandKind.NewFolder);

        viewModel.InvokeToolbarCommandCommand.Execute(LibraryToolbarCommandKind.ManageTags);
        viewModel.Library.Toolbar.LastCommand.Should().Be(LibraryToolbarCommandKind.ManageTags);

        viewModel.InvokeToolbarCommandCommand.Execute(LibraryToolbarCommandKind.ToggleFilters);
        viewModel.Library.Toolbar.IsFilterOpen.Should().BeTrue();

        await viewModel.DuplicateSelectedPromptCommand.ExecuteAsync(null);

        services.PromptService.DuplicateCalls.Should().ContainSingle().Which.Should().Be("market-analysis");
        services.PromptService.Prompts.Should().Contain(prompt => prompt.Id == "market-analysis-copy");
        viewModel.Library.Toolbar.LastCommand.Should().Be(LibraryToolbarCommandKind.DuplicatePrompt);

        string deletedId = viewModel.Library.SelectedPrompt!.Id;

        await viewModel.DeleteSelectedPromptCommand.ExecuteAsync(null);

        services.PromptService.DeleteCalls.Should().ContainSingle().Which.Should().Be(deletedId);
        viewModel.Library.Toolbar.LastCommand.Should().Be(LibraryToolbarCommandKind.DeletePrompt);
    }

    [Fact]
    public async Task EditorCommandStateReflectsDirtyAndCleanTransitions()
    {
        var services = new ViewModelServiceFixture();
        var viewModel = services.CreateViewModel();
        await viewModel.LoadAsync(CancellationToken.None);

        viewModel.Library.CanSave.Should().BeFalse();
        viewModel.Library.CanCancel.Should().BeFalse();
        viewModel.Library.SelectedPrompt.Should().NotBeNull();

        viewModel.MarkEditorDirtyCommand.Execute(null);

        viewModel.Library.CanSave.Should().BeTrue();
        viewModel.Library.CanCancel.Should().BeTrue();

        await viewModel.CancelEditorCommand.ExecuteAsync(null);

        viewModel.Library.CanSave.Should().BeFalse();
        viewModel.Library.CanCancel.Should().BeFalse();
    }

    [Fact]
    public async Task DataManagementMethodsUseImportExportAndBackupServices()
    {
        var services = new ViewModelServiceFixture();
        var viewModel = services.CreateViewModel();
        await viewModel.LoadAsync(CancellationToken.None);

        PromptNestExport export = new()
        {
            ExportedAt = DateTimeOffset.UtcNow,
            Prompts = [SamplePrompt]
        };

        OperationResult<ImportPlan> preview = await viewModel.PreviewImportAsync(export, new ImportOptions { DryRun = true }, CancellationToken.None);
        OperationResult<ImportSummary> import = await viewModel.ImportAsync(export, new ImportOptions(), CancellationToken.None);
        OperationResult<PromptNestExport> exported = await viewModel.ExportPromptsAsync(CancellationToken.None);
        OperationResult<BackupMetadata> backup = await viewModel.CreateBackupAsync(CancellationToken.None);

        preview.Succeeded.Should().BeTrue();
        import.Succeeded.Should().BeTrue();
        exported.Value?.Prompts.Should().ContainSingle(prompt => prompt.Id == SamplePrompt.Id);
        backup.Succeeded.Should().BeTrue();
        services.ImportExportService.ImportCalls.Should().Be(1);
        services.BackupService.BackupCalls.Should().Be(1);
        viewModel.DataManagementStatusText.Should().Contain("Backup created");
    }

    private sealed class ViewModelServiceFixture
    {
        public FakePromptService PromptService { get; } = new();

        public FakeFolderService FolderService { get; } = new();

        public FakeTagService TagService { get; } = new();

        public FakeSearchService SearchService { get; } = new();

        public FakePromptCopyService PromptCopyService { get; } = new();

        public FakeJumpListService JumpListService { get; } = new();

        public FakeSettingsRepository SettingsRepository { get; } = new();

        public FakeUpdateService UpdateService { get; } = new();

        public FakeImportExportService ImportExportService { get; } = new();

        public FakeBackupService BackupService { get; } = new();

        public MainViewModel CreateViewModel() => new(
            PromptService,
            FolderService,
            TagService,
            SearchService,
            PromptCopyService,
            JumpListService,
            SettingsRepository,
            UpdateService,
            ImportExportService,
            BackupService);

        public static async Task WaitForAsync(Func<bool> condition)
        {
            for (var attempt = 0; attempt < 40; attempt++)
            {
                if (condition())
                {
                    return;
                }

                await Task.Delay(25);
            }

            condition().Should().BeTrue();
        }
    }

    private sealed class FakePromptService : IPromptService
    {
        public List<Prompt> Prompts { get; } = [SamplePrompt];

        public List<string> ToggleFavoriteCalls { get; } = [];

        public List<string> DuplicateCalls { get; } = [];

        public List<string> DeleteCalls { get; } = [];

        public Prompt? UpdatedPrompt { get; private set; }

        public PromptQuery? LastListQuery { get; private set; }

        public Task<OperationResult<string>> CreateAsync(Prompt prompt, CancellationToken cancellationToken) =>
            Task.FromResult(OperationResultFactory.Success(prompt.Id));

        public Task<OperationResult<string>> DuplicateAsync(string id, CancellationToken cancellationToken)
        {
            DuplicateCalls.Add(id);
            Prompt? prompt = Prompts.FirstOrDefault(item => item.Id == id);
            string copyId = id + "-copy";
            if (prompt is not null)
            {
                Prompts.Add(prompt with { Id = copyId, Title = prompt.Title + " Copy" });
            }

            return Task.FromResult(OperationResultFactory.Success(copyId));
        }

        public Task<OperationResult<Prompt>> GetAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(OperationResultFactory.Success(Prompts.First(prompt => prompt.Id == id)));

        public Task<PagedResult<Prompt>> ListAsync(PromptQuery query, CancellationToken cancellationToken)
        {
            LastListQuery = query;
            IReadOnlyList<Prompt> prompts = Prompts
                .Where(prompt => query.FolderId is null || prompt.FolderId == query.FolderId)
                .Where(prompt => query.IsFavorite is null || prompt.IsFavorite == query.IsFavorite)
                .Skip(query.Skip)
                .Take(query.Take)
                .ToArray();
            return Task.FromResult(new PagedResult<Prompt> { Items = prompts, TotalCount = Prompts.Count, Skip = query.Skip, Take = query.Take });
        }

        public Task<OperationResult> SoftDeleteAsync(string id, CancellationToken cancellationToken)
        {
            DeleteCalls.Add(id);
            Prompts.RemoveAll(prompt => prompt.Id == id);
            return Task.FromResult(OperationResult.Success());
        }

        public Task<OperationResult> ToggleFavoriteAsync(string id, CancellationToken cancellationToken)
        {
            ToggleFavoriteCalls.Add(id);
            int index = Prompts.FindIndex(prompt => prompt.Id == id);
            if (index >= 0)
            {
                Prompts[index] = Prompts[index] with { IsFavorite = !Prompts[index].IsFavorite };
            }

            return Task.FromResult(OperationResult.Success());
        }

        public Task<OperationResult> UpdateAsync(Prompt prompt, CancellationToken cancellationToken)
        {
            UpdatedPrompt = prompt;
            int index = Prompts.FindIndex(item => item.Id == prompt.Id);
            if (index >= 0)
            {
                Prompts[index] = prompt;
            }

            return Task.FromResult(OperationResult.Success());
        }
    }

    private sealed class FakeFolderService : IFolderService
    {
        public Task<IReadOnlyList<Folder>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Folder>>(
            [
                new Folder
                {
                    Id = "research",
                    Name = "Research",
                    PromptCount = 2,
                    CreatedAt = DateTimeOffset.UtcNow
                },
                new Folder
                {
                    Id = "market-research",
                    Name = "Market Research",
                    ParentId = "research",
                    PromptCount = 1,
                    CreatedAt = DateTimeOffset.UtcNow
                }
            ]);
    }

    private sealed class FakeTagService : ITagService
    {
        public Task<IReadOnlyList<Tag>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Tag>>(
            [
                new Tag { Name = "analysis", Count = 1 },
                new Tag { Name = "market", Count = 1 },
                new Tag { Name = "research", Count = 1 },
                new Tag { Name = "strategy", Count = 0 }
            ]);
    }

    private sealed class FakeSearchService : ISearchService
    {
        public string? LastSearchText { get; private set; }

        public List<string> SearchTexts { get; } = [];

        public PromptQuery? LastQuery { get; private set; }

        public bool ThrowOnSearch { get; set; }

        public Task<PagedResult<Prompt>> SearchAsync(string text, PromptQuery query, CancellationToken cancellationToken)
        {
            if (ThrowOnSearch)
            {
                throw new InvalidOperationException("search failed");
            }

            LastSearchText = text;
            SearchTexts.Add(text);
            LastQuery = query;
            return Task.FromResult(new PagedResult<Prompt> { Items = [SamplePrompt], TotalCount = 1, Skip = query.Skip, Take = query.Take });
        }
    }

    private sealed class FakePromptCopyService : IPromptCopyService
    {
        public Dictionary<string, string> LastValues { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<OperationResult<PromptCopyForm>> CreateFormAsync(string promptId, CancellationToken cancellationToken) =>
            Task.FromResult(
                OperationResultFactory.Success(
                    new PromptCopyForm
                    {
                        PromptId = promptId,
                        Title = SamplePrompt.Title,
                        Variables = SamplePrompt.Variables
                            .Select(variable => new PromptCopyVariable
                            {
                                Name = variable.Name,
                                DefaultValue = variable.DefaultValue,
                                CurrentValue = variable.DefaultValue,
                                Type = variable.Type,
                                IsRequired = variable.IsRequired
                            })
                            .ToArray()
                    }));

        public Task<OperationResult<ResolvedPrompt>> CopyAsync(
            string promptId,
            IReadOnlyDictionary<string, string> values,
            CancellationToken cancellationToken)
        {
            LastValues = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(
                OperationResultFactory.Success(
                    new ResolvedPrompt
                    {
                        PromptId = promptId,
                        Text = "Copied text",
                        Values = LastValues
                    }));
        }
    }

    private sealed class FakeJumpListService : IJumpListService
    {
        public IReadOnlyList<Prompt> LastFavorites { get; private set; } = [];

        public Task RefreshFavoritesAsync(IReadOnlyList<Prompt> favoritePrompts, CancellationToken cancellationToken)
        {
            LastFavorites = favoritePrompts;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSettingsRepository : ISettingsRepository
    {
        public AppSettings Settings { get; set; } = new();

        public Task<AppSettings> GetAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Settings);

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
        {
            Settings = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUpdateService : IUpdateService
    {
        public AppSettings? LastSettings { get; private set; }

        public Task<OperationResult<UpdateStatus>> CheckForUpdatesAsync(
            AppSettings settings,
            CancellationToken cancellationToken)
        {
            LastSettings = settings;
            return Task.FromResult(
                OperationResultFactory.Success(
                    new UpdateStatus
                    {
                        IsEnabled = settings.UpdateChecksEnabled,
                        IsSupported = true,
                        Channel = settings.UpdateChannel,
                        CheckedAt = DateTimeOffset.UtcNow,
                        Message = "PromptNest is up to date"
                    }));
        }
    }

    private sealed class FakeImportExportService : IImportExportService
    {
        public int ImportCalls { get; private set; }

        public Task<OperationResult<PromptNestExport>> ExportAsync(CancellationToken cancellationToken) =>
            Task.FromResult(OperationResultFactory.Success(
                new PromptNestExport
                {
                    ExportedAt = DateTimeOffset.UtcNow,
                    Prompts = [SamplePrompt]
                }));

        public Task<OperationResult<ImportSummary>> ImportAsync(
            PromptNestExport exportData,
            ImportOptions options,
            CancellationToken cancellationToken)
        {
            ImportCalls++;
            return Task.FromResult(OperationResultFactory.Success(
                new ImportSummary
                {
                    PromptsCreated = exportData.Prompts.Count,
                    DryRun = options.DryRun
                }));
        }

        public Task<OperationResult<ImportPlan>> PreviewImportAsync(
            PromptNestExport exportData,
            ImportOptions options,
            CancellationToken cancellationToken) =>
            Task.FromResult(OperationResultFactory.Success(
                new ImportPlan
                {
                    Summary = new ImportSummary
                    {
                        PromptsCreated = exportData.Prompts.Count,
                        DryRun = true
                    }
                }));
    }

    private sealed class FakeBackupService : IBackupService
    {
        public int BackupCalls { get; private set; }

        public Task<OperationResult> ApplyRetentionAsync(int keepLast, CancellationToken cancellationToken) =>
            Task.FromResult(OperationResult.Success());

        public Task<OperationResult<BackupMetadata>> CreateBackupAsync(CancellationToken cancellationToken)
        {
            BackupCalls++;
            return Task.FromResult(OperationResultFactory.Success(
                new BackupMetadata
                {
                    FilePath = "backup.db",
                    CreatedAt = DateTimeOffset.UtcNow,
                    SizeBytes = 1
                }));
        }

        public Task<IReadOnlyList<BackupMetadata>> ListBackupsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<BackupMetadata>>([]);
    }

    private static Prompt SamplePrompt => new()
    {
        Id = "market-analysis",
        Title = "Market Analysis - Industry Overview",
        Body = "Analyze the {{industry}} market for {{audience|executives}}.",
        FolderId = "market-research",
        IsFavorite = true,
        Tags = ["research", "market", "analysis"],
        Variables =
        [
            new PromptVariable { Name = "industry" },
            new PromptVariable { Name = "audience", DefaultValue = "executives" }
        ],
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };
}
