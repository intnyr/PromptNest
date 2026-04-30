using FluentAssertions;

using PromptNest.Core.Abstractions;
using PromptNest.Core.Models;
using PromptNest.Core.Services;
using PromptNest.Core.Variables;

namespace PromptNest.Core.Tests;

public sealed class PromptServiceTests
{
    [Fact]
    public async Task CreateAsyncRejectsPromptBodyOverLimit()
    {
        var service = new PromptService(new InMemoryPromptRepository(), new VariableParser());
        var prompt = NewPrompt() with { Body = new string('x', PromptLimits.MaxPromptBodyBytes + 1) };

        var result = await service.CreateAsync(prompt, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be("BodyTooLarge");
    }

    [Fact]
    public async Task CreateAsyncParsesVariablesBeforePersisting()
    {
        var repository = new InMemoryPromptRepository();
        var service = new PromptService(repository, new VariableParser());

        await service.CreateAsync(NewPrompt() with { Body = "Hello {{name}}" }, CancellationToken.None);

        repository.Prompts.Single().Value.Variables.Should().ContainSingle(variable => variable.Name == "name");
    }

    [Fact]
    public async Task CreateAsyncRejectsMissingTitleAndBody()
    {
        var service = new PromptService(new InMemoryPromptRepository(), new VariableParser());

        var missingTitle = await service.CreateAsync(NewPrompt() with { Title = " " }, CancellationToken.None);
        var missingBody = await service.CreateAsync(NewPrompt() with { Body = " " }, CancellationToken.None);

        missingTitle.ErrorCode.Should().Be("TitleRequired");
        missingBody.ErrorCode.Should().Be("BodyRequired");
    }

    [Fact]
    public async Task CreateAsyncNormalizesTagsBeforePersisting()
    {
        var repository = new InMemoryPromptRepository();
        var service = new PromptService(repository, new VariableParser());

        await service.CreateAsync(NewPrompt() with { Tags = [" Research ", "research", "MARKET"] }, CancellationToken.None);

        repository.Prompts["prompt"].Tags.Should().Equal("research", "market");
    }

    [Fact]
    public async Task UpdateAsyncParsesVariablesAndUpdatesExistingPrompt()
    {
        var repository = new InMemoryPromptRepository();
        var service = new PromptService(repository, new VariableParser());
        await service.CreateAsync(NewPrompt(), CancellationToken.None);

        var result = await service.UpdateAsync(NewPrompt() with { Body = "Updated {{topic}}" }, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        repository.Prompts["prompt"].Body.Should().Be("Updated {{topic}}");
        repository.Prompts["prompt"].Variables.Should().ContainSingle(variable => variable.Name == "topic");
    }

    [Fact]
    public async Task SoftDeleteDuplicateAndFavoriteToggleUseRepository()
    {
        var repository = new InMemoryPromptRepository();
        var service = new PromptService(repository, new VariableParser());
        await service.CreateAsync(NewPrompt() with { IsFavorite = false }, CancellationToken.None);

        await service.ToggleFavoriteAsync("prompt", CancellationToken.None);
        var duplicate = await service.DuplicateAsync("prompt", CancellationToken.None);
        await service.SoftDeleteAsync("prompt", CancellationToken.None);

        repository.Prompts["prompt"].IsFavorite.Should().BeTrue();
        duplicate.Succeeded.Should().BeTrue();
        repository.Prompts.Should().ContainKey(duplicate.Value!);
        repository.Prompts["prompt"].DeletedAt.Should().NotBeNull();
    }

    private static Prompt NewPrompt()
    {
        return new Prompt
        {
            Id = "prompt",
            Title = "Title",
            Body = "Body",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private sealed class InMemoryPromptRepository : IPromptRepository
    {
        public Dictionary<string, Prompt> Prompts { get; } = [];

        public Task<string> CreateAsync(Prompt prompt, CancellationToken cancellationToken)
        {
            Prompts[prompt.Id] = prompt;
            return Task.FromResult(prompt.Id);
        }

        public Task<Prompt?> GetAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(Prompts.GetValueOrDefault(id));

        public Task IncrementUsageAsync(string id, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<PagedResult<Prompt>> ListAsync(PromptQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<Prompt> { Items = Prompts.Values.ToList(), TotalCount = Prompts.Count });

        public Task<PagedResult<Prompt>> SearchAsync(string text, PromptQuery query, CancellationToken cancellationToken) =>
            ListAsync(query, cancellationToken);

        public Task SoftDeleteAsync(string id, CancellationToken cancellationToken)
        {
            Prompts[id] = Prompts[id] with { DeletedAt = DateTimeOffset.UtcNow };
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Prompt prompt, CancellationToken cancellationToken)
        {
            Prompts[prompt.Id] = prompt;
            return Task.CompletedTask;
        }
    }
}