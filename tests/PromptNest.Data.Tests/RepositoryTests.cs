using FluentAssertions;

using Microsoft.Data.Sqlite;

using PromptNest.Core.Abstractions;
using PromptNest.Core.Models;
using PromptNest.Data.Db;
using PromptNest.Data.Migrations;
using PromptNest.Data.Repositories;

namespace PromptNest.Data.Tests;

public sealed class RepositoryTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly TestPathProvider _pathProvider;
    private readonly SqliteConnectionFactory _connectionFactory;

    public RepositoryTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "PromptNest.Tests", Guid.NewGuid().ToString("N"));
        _pathProvider = new TestPathProvider(_testDirectory);
        _connectionFactory = new SqliteConnectionFactory(_pathProvider);
    }

    [Fact]
    public async Task PromptRepositoryPersistsTagsVariablesSearchesAndSoftDeletesPrompts()
    {
        await MigrateAsync();

        var folderRepository = new FolderRepository(_connectionFactory);
        var promptRepository = new PromptRepository(_connectionFactory);
        var tagRepository = new TagRepository(_connectionFactory);
        var variableRepository = new VariableValueRepository(_connectionFactory);
        var now = DateTimeOffset.UtcNow;

        var folderId = await folderRepository.CreateAsync(
            new Folder { Id = "research", Name = "Research", CreatedAt = now },
            CancellationToken.None);

        var promptId = await promptRepository.CreateAsync(
            new Prompt
            {
                Id = "market-analysis",
                Title = "Market Analysis - Industry Overview",
                Body = "Provide a comprehensive overview of the {{industry}} industry.",
                FolderId = folderId,
                IsFavorite = true,
                CreatedAt = now,
                UpdatedAt = now,
                Tags = ["research", "market", "analysis"],
                Variables =
                [
                    new PromptVariable { Name = "industry", DefaultValue = "SaaS" }
                ]
            },
            CancellationToken.None);

        var list = await promptRepository.ListAsync(new PromptQuery { FolderId = folderId }, CancellationToken.None);
        list.TotalCount.Should().Be(1);
        list.Items[0].Tags.Should().BeEquivalentTo("analysis", "market", "research");
        list.Items[0].Variables.Should().ContainSingle(variable => variable.Name == "industry");

        var search = await promptRepository.SearchAsync("industry", new PromptQuery(), CancellationToken.None);
        search.Items.Should().ContainSingle(prompt => prompt.Id == promptId);

        var tags = await tagRepository.ListAsync(CancellationToken.None);
        tags.Single(tag => tag.Name == "research").Count.Should().Be(1);

        await variableRepository.SaveLastUsedValuesAsync(
            promptId,
            new Dictionary<string, string> { ["industry"] = "SaaS" },
            CancellationToken.None);

        var values = await variableRepository.GetLastUsedValuesAsync(promptId, CancellationToken.None);
        values.Should().Contain("industry", "SaaS");

        await promptRepository.SoftDeleteAsync(promptId, CancellationToken.None);

        var afterDelete = await promptRepository.SearchAsync("industry", new PromptQuery(), CancellationToken.None);
        afterDelete.Items.Should().BeEmpty();

        var includeDeleted = await promptRepository.ListAsync(new PromptQuery { IncludeDeleted = true }, CancellationToken.None);
        includeDeleted.Items.Should().ContainSingle(prompt => prompt.Id == promptId && prompt.DeletedAt != null);
    }

    [Fact]
    public async Task FolderRepositoryCreatesUpdatesDeletesAndNullsPromptFolders()
    {
        await MigrateAsync();

        var folderRepository = new FolderRepository(_connectionFactory);
        var promptRepository = new PromptRepository(_connectionFactory);
        var now = DateTimeOffset.UtcNow;

        var parentId = await folderRepository.CreateAsync(
            new Folder { Id = "root", Name = "Root", SortOrder = 1, CreatedAt = now },
            CancellationToken.None);
        var childId = await folderRepository.CreateAsync(
            new Folder { Id = "child", Name = "Child", ParentId = parentId, SortOrder = 2, CreatedAt = now },
            CancellationToken.None);

        await promptRepository.CreateAsync(NewPrompt("prompt-1", "Foldered Prompt", "Body", childId, now), CancellationToken.None);

        var beforeUpdate = await folderRepository.ListAsync(CancellationToken.None);
        beforeUpdate.Should().Contain(folder => folder.Id == childId && folder.PromptCount == 1);

        await folderRepository.UpdateAsync(
            new Folder { Id = childId, Name = "Updated Child", ParentId = parentId, SortOrder = 3, CreatedAt = now },
            CancellationToken.None);

        var afterUpdate = await folderRepository.ListAsync(CancellationToken.None);
        afterUpdate.Should().Contain(folder => folder.Id == childId && folder.Name == "Updated Child" && folder.SortOrder == 3);

        await folderRepository.DeleteAsync(childId, CancellationToken.None);

        var prompt = await promptRepository.GetAsync("prompt-1", CancellationToken.None);
        prompt.Should().NotBeNull();
        prompt!.FolderId.Should().BeNull();

        var afterDelete = await folderRepository.ListAsync(CancellationToken.None);
        afterDelete.Should().NotContain(folder => folder.Id == childId);
    }

    [Fact]
    public async Task TagRepositoryTracksCountsAcrossPromptTagChangesAndDelete()
    {
        await MigrateAsync();

        var promptRepository = new PromptRepository(_connectionFactory);
        var tagRepository = new TagRepository(_connectionFactory);
        var now = DateTimeOffset.UtcNow;

        await promptRepository.CreateAsync(
            NewPrompt("prompt-1", "First", "Alpha body", null, now) with { Tags = ["alpha", "shared"] },
            CancellationToken.None);
        await promptRepository.CreateAsync(
            NewPrompt("prompt-2", "Second", "Beta body", null, now) with { Tags = ["beta", "shared"] },
            CancellationToken.None);

        var initialTags = await tagRepository.ListAsync(CancellationToken.None);
        initialTags.Single(tag => tag.Name == "shared").Count.Should().Be(2);

        var prompt = await promptRepository.GetAsync("prompt-1", CancellationToken.None);
        await promptRepository.UpdateAsync(prompt! with { Tags = ["beta", "shared"] }, CancellationToken.None);

        var afterUpdate = await tagRepository.ListAsync(CancellationToken.None);
        afterUpdate.Single(tag => tag.Name == "alpha").Count.Should().Be(0);
        afterUpdate.Single(tag => tag.Name == "beta").Count.Should().Be(2);
        afterUpdate.Single(tag => tag.Name == "shared").Count.Should().Be(2);

        await tagRepository.UpsertAsync(new Tag { Name = "shared", Color = "#44AAFF", Count = 999 }, CancellationToken.None);
        var afterColorUpdate = await tagRepository.ListAsync(CancellationToken.None);
        afterColorUpdate.Single(tag => tag.Name == "shared").Color.Should().Be("#44AAFF");
        afterColorUpdate.Single(tag => tag.Name == "shared").Count.Should().Be(2);

        await tagRepository.DeleteAsync("shared", CancellationToken.None);

        var promptAfterTagDelete = await promptRepository.GetAsync("prompt-1", CancellationToken.None);
        promptAfterTagDelete!.Tags.Should().NotContain("shared");
    }

    [Fact]
    public async Task PromptRepositoryUpdatesUsageMetadata()
    {
        await MigrateAsync();

        var promptRepository = new PromptRepository(_connectionFactory);
        var now = DateTimeOffset.UtcNow.AddMinutes(-5);

        await promptRepository.CreateAsync(NewPrompt("usage", "Usage", "Body", null, now), CancellationToken.None);

        await promptRepository.IncrementUsageAsync("usage", CancellationToken.None);
        await promptRepository.IncrementUsageAsync("usage", CancellationToken.None);

        var prompt = await promptRepository.GetAsync("usage", CancellationToken.None);
        prompt.Should().NotBeNull();
        prompt!.UseCount.Should().Be(2);
        prompt.LastUsedAt.Should().NotBeNull();
        prompt.LastUsedAt.Should().BeAfter(now);
    }

    [Fact]
    public async Task SearchSupportsFtsFallbackFilteringSortingAndPagination()
    {
        await MigrateAsync();

        var folderRepository = new FolderRepository(_connectionFactory);
        var promptRepository = new PromptRepository(_connectionFactory);
        var now = DateTimeOffset.UtcNow;

        var folderId = await folderRepository.CreateAsync(
            new Folder { Id = "analysis", Name = "Analysis", CreatedAt = now },
            CancellationToken.None);

        for (var index = 0; index < 30; index++)
        {
            var tag = index % 2 == 0 ? "strategy" : "ops";
            var favorite = index % 3 == 0;
            await promptRepository.CreateAsync(
                NewPrompt(
                    $"prompt-{index:D2}",
                    $"Market Strategy {index:D2}",
                    $"Scenario alpha beta {index:D2}",
                    folderId,
                    now.AddMinutes(index)) with
                {
                    IsFavorite = favorite,
                    UseCount = index,
                    LastUsedAt = now.AddMinutes(index),
                    Tags = [tag]
                },
                CancellationToken.None);
        }

        var firstPage = await promptRepository.SearchAsync(
            "strategy",
            new PromptQuery
            {
                FolderId = folderId,
                Tags = ["strategy"],
                IsFavorite = true,
                SortBy = PromptSortBy.UseCount,
                SortDescending = true,
                Skip = 0,
                Take = 3
            },
            CancellationToken.None);

        firstPage.TotalCount.Should().Be(5);
        firstPage.Items.Should().HaveCount(3);
        firstPage.Items.Select(prompt => prompt.UseCount).Should().BeInDescendingOrder();
        firstPage.Items.Should().OnlyContain(prompt => prompt.Tags.Contains("strategy") && prompt.IsFavorite);

        var secondPage = await promptRepository.SearchAsync(
            "strategy",
            new PromptQuery
            {
                FolderId = folderId,
                Tags = ["strategy"],
                IsFavorite = true,
                SortBy = PromptSortBy.UseCount,
                SortDescending = true,
                Skip = 3,
                Take = 3
            },
            CancellationToken.None);

        secondPage.Items.Should().HaveCount(2);
        secondPage.Items.Should().NotIntersectWith(firstPage.Items);

        var malformedQuery = await promptRepository.SearchAsync(
            "alpha OR",
            new PromptQuery { Skip = 0, Take = 50 },
            CancellationToken.None);

        malformedQuery.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SettingsRepositoryPersistsAppSettings()
    {
        await MigrateAsync();

        var repository = new SettingsRepository(_connectionFactory);
        var settings = new AppSettings
        {
            Theme = AppTheme.Dark,
            GlobalHotkey = "Ctrl+Space",
            NotificationsEnabled = false,
            UpdateChannel = UpdateChannel.Beta
        };

        await repository.SaveAsync(settings, CancellationToken.None);

        var saved = await repository.GetAsync(CancellationToken.None);
        saved.Should().Be(settings);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    private async Task MigrateAsync()
    {
        var runner = new SqliteMigrationRunner(_connectionFactory, _pathProvider);
        await runner.MigrateAsync(CancellationToken.None);
    }

    private static Prompt NewPrompt(string id, string title, string body, string? folderId, DateTimeOffset now)
    {
        return new Prompt
        {
            Id = id,
            Title = title,
            Body = body,
            FolderId = folderId,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private sealed class TestPathProvider : IPathProvider
    {
        public TestPathProvider(string dataDirectory)
        {
            DataDirectory = dataDirectory;
            DatabasePath = Path.Combine(DataDirectory, "library.db");
            LogsDirectory = Path.Combine(DataDirectory, "logs");
            BackupsDirectory = Path.Combine(DataDirectory, "Backups");
        }

        public string DataDirectory { get; }

        public string DatabasePath { get; }

        public string LogsDirectory { get; }

        public string BackupsDirectory { get; }

        public string SettingsPath => Path.Combine(DataDirectory, "settings.json");

        public string UpdateCacheDirectory => Path.Combine(DataDirectory, "Updates");

        public bool IsPackaged => false;
    }
}