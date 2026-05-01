using FluentAssertions;

using PromptNest.Core.Abstractions;
using PromptNest.Core.Models;
using PromptNest.Core.Services;
using PromptNest.Core.Variables;

namespace PromptNest.Core.Tests;

public sealed class ImportExportServiceTests
{
    [Fact]
    public async Task PreviewImportAsyncReportsValidationErrorsWithoutWriting()
    {
        var repositories = new ImportFixture();
        var service = repositories.CreateService();
        var export = NewExport() with
        {
            Folders = [NewFolder("known")],
            Prompts =
            [
                NewPrompt("missing-folder") with { FolderId = "unknown" },
                NewPrompt("too-large") with { Body = new string('x', PromptLimits.MaxPromptBodyBytes + 1) }
            ]
        };

        OperationResult<ImportPlan> result = await service.PreviewImportAsync(
            export,
            new ImportOptions { DryRun = true },
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.HasErrors.Should().BeTrue();
        result.Value.Issues.Should().Contain(issue => issue.Code == "FolderNotFound");
        result.Value.Issues.Should().Contain(issue => issue.Code == "BodyTooLarge");
        repositories.PromptRepository.Prompts.Should().BeEmpty();
    }

    [Fact]
    public async Task ImportAsyncNormalizesTagsParsesVariablesAndHonorsConflictModes()
    {
        var repositories = new ImportFixture();
        var service = repositories.CreateService();
        var export = NewExport() with
        {
            Tags = [new Tag { Name = " Research " }],
            Prompts = [NewPrompt("prompt") with { Body = "Hello {{name|friend}}", Tags = [" Research ", "research"] }]
        };

        OperationResult<ImportSummary> created = await service.ImportAsync(export, new ImportOptions(), CancellationToken.None);
        OperationResult<ImportSummary> skipped = await service.ImportAsync(export, new ImportOptions(), CancellationToken.None);
        OperationResult<ImportSummary> dryRun = await service.ImportAsync(export, new ImportOptions { ConflictMode = ImportConflictMode.Overwrite, DryRun = true }, CancellationToken.None);

        created.Succeeded.Should().BeTrue();
        created.Value!.PromptsCreated.Should().Be(1);
        repositories.PromptRepository.Prompts["prompt"].Tags.Should().Equal("research");
        repositories.PromptRepository.Prompts["prompt"].Variables.Should().ContainSingle(variable => variable.Name == "name");
        skipped.Value!.PromptsSkipped.Should().Be(1);
        dryRun.Value!.DryRun.Should().BeTrue();
        dryRun.Value.PromptsUpdated.Should().Be(1);
        repositories.PromptRepository.Updates.Should().Be(0);
    }

    private static PromptNestExport NewExport() => new()
    {
        ExportedAt = DateTimeOffset.UtcNow
    };

    private static Folder NewFolder(string id) => new()
    {
        Id = id,
        Name = id,
        CreatedAt = DateTimeOffset.UtcNow
    };

    private static Prompt NewPrompt(string id) => new()
    {
        Id = id,
        Title = "Prompt " + id,
        Body = "Prompt body",
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private sealed class ImportFixture
    {
        public FakeFolderRepository FolderRepository { get; } = new();

        public FakeTagRepository TagRepository { get; } = new();

        public FakePromptRepository PromptRepository { get; } = new();

        public ImportExportService CreateService() => new(FolderRepository, TagRepository, PromptRepository, new VariableParser());
    }

    private sealed class FakeFolderRepository : IFolderRepository
    {
        public List<Folder> Folders { get; } = [];

        public Task<string> CreateAsync(Folder folder, CancellationToken cancellationToken)
        {
            Folders.Add(folder);
            return Task.FromResult(folder.Id);
        }

        public Task DeleteAsync(string id, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<Folder>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Folder>>(Folders);

        public Task UpdateAsync(Folder folder, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeTagRepository : ITagRepository
    {
        public List<Tag> Tags { get; } = [];

        public Task DeleteAsync(string name, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<Tag>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Tag>>(Tags);

        public Task UpsertAsync(Tag tag, CancellationToken cancellationToken)
        {
            Tags.RemoveAll(existing => string.Equals(existing.Name, tag.Name, StringComparison.OrdinalIgnoreCase));
            Tags.Add(tag);
            return Task.CompletedTask;
        }
    }

    private sealed class FakePromptRepository : IPromptRepository
    {
        public Dictionary<string, Prompt> Prompts { get; } = [];

        public int Updates { get; private set; }

        public Task<string> CreateAsync(Prompt prompt, CancellationToken cancellationToken)
        {
            Prompts[prompt.Id] = prompt;
            return Task.FromResult(prompt.Id);
        }

        public Task<Prompt?> GetAsync(string id, CancellationToken cancellationToken) => Task.FromResult(Prompts.GetValueOrDefault(id));

        public Task IncrementUsageAsync(string id, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<PagedResult<Prompt>> ListAsync(PromptQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<Prompt> { Items = Prompts.Values.ToArray(), TotalCount = Prompts.Count });

        public Task<PagedResult<Prompt>> SearchAsync(string text, PromptQuery query, CancellationToken cancellationToken) => ListAsync(query, cancellationToken);

        public Task SoftDeleteAsync(string id, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task UpdateAsync(Prompt prompt, CancellationToken cancellationToken)
        {
            Updates++;
            Prompts[prompt.Id] = prompt;
            return Task.CompletedTask;
        }
    }
}
