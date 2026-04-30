using FluentAssertions;

using PromptNest.Core.Abstractions;
using PromptNest.Core.Models;
using PromptNest.Core.Services;
using PromptNest.Core.Variables;

namespace PromptNest.Core.Tests;

public sealed class PromptCopyServiceTests
{
    [Fact]
    public async Task CreateFormAsyncUsesDefaultsAndLastUsedValues()
    {
        var repository = new InMemoryPromptRepository();
        var variables = new InMemoryVariableValueRepository();
        variables.Values["audience"] = "Executives";
        repository.Prompt = NewPrompt();
        var service = CreateService(repository, variables);

        OperationResult<PromptCopyForm> result = await service.CreateFormAsync("prompt", CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.Variables.Should().Contain(variable => variable.Name == "industry" && variable.CurrentValue == "SaaS");
        result.Value!.Variables.Should().Contain(variable => variable.Name == "audience" && variable.CurrentValue == "Executives");
    }

    [Fact]
    public async Task CopyAsyncWritesClipboardCachesVariablesIncrementsUsageAndNotifies()
    {
        var repository = new InMemoryPromptRepository { Prompt = NewPrompt() };
        var variables = new InMemoryVariableValueRepository();
        var clipboard = new FakeClipboardService();
        var notifications = new FakeNotificationService();
        var service = CreateService(repository, variables, clipboard, notifications);

        OperationResult<ResolvedPrompt> result = await service.CopyAsync(
            "prompt",
            new Dictionary<string, string> { ["audience"] = "Founders" },
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        clipboard.Text.Should().Be("Analyze SaaS for Founders.");
        variables.Values.Should().Contain("industry", "SaaS");
        variables.Values.Should().Contain("audience", "Founders");
        repository.IncrementUsageCalls.Should().Be(1);
        notifications.CopiedTitles.Should().ContainSingle().Which.Should().Be("Market Analysis");
    }

    [Fact]
    public async Task CopyAsyncRejectsMissingRequiredVariablesBeforeClipboardWrite()
    {
        var repository = new InMemoryPromptRepository
        {
            Prompt = NewPrompt() with
            {
                Body = "Hello {{name}}",
                Variables = [new PromptVariable { Name = "name" }]
            }
        };
        var clipboard = new FakeClipboardService();
        var service = CreateService(repository, new InMemoryVariableValueRepository(), clipboard);

        OperationResult<ResolvedPrompt> result = await service.CopyAsync(
            "prompt",
            new Dictionary<string, string>(),
            CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be("VariableRequired");
        clipboard.Text.Should().BeNull();
    }

    private static PromptCopyService CreateService(
        InMemoryPromptRepository repository,
        InMemoryVariableValueRepository variables,
        FakeClipboardService? clipboard = null,
        FakeNotificationService? notifications = null)
    {
        return new PromptCopyService(
            repository,
            variables,
            new VariableResolver(variables),
            clipboard ?? new FakeClipboardService(),
            notifications ?? new FakeNotificationService(),
            new FakeSettingsRepository());
    }

    private static Prompt NewPrompt() => new()
    {
        Id = "prompt",
        Title = "Market Analysis",
        Body = "Analyze {{industry|SaaS}} for {{audience}}.",
        Variables =
        [
            new PromptVariable { Name = "industry", DefaultValue = "SaaS" },
            new PromptVariable { Name = "audience" }
        ],
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private sealed class InMemoryPromptRepository : IPromptRepository
    {
        public Prompt? Prompt { get; set; }

        public int IncrementUsageCalls { get; private set; }

        public Task<string> CreateAsync(Prompt prompt, CancellationToken cancellationToken) => Task.FromResult(prompt.Id);

        public Task<Prompt?> GetAsync(string id, CancellationToken cancellationToken) => Task.FromResult(Prompt);

        public Task IncrementUsageAsync(string id, CancellationToken cancellationToken)
        {
            IncrementUsageCalls++;
            return Task.CompletedTask;
        }

        public Task<PagedResult<Prompt>> ListAsync(PromptQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<Prompt>());

        public Task<PagedResult<Prompt>> SearchAsync(string text, PromptQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<Prompt>());

        public Task SoftDeleteAsync(string id, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task UpdateAsync(Prompt prompt, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class InMemoryVariableValueRepository : IVariableValueRepository
    {
        public Dictionary<string, string> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyDictionary<string, string>> GetLastUsedValuesAsync(string promptId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(Values);

        public Task SaveLastUsedValuesAsync(
            string promptId,
            IReadOnlyDictionary<string, string> values,
            CancellationToken cancellationToken)
        {
            Values.Clear();
            foreach (KeyValuePair<string, string> value in values)
            {
                Values[value.Key] = value.Value;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeClipboardService : IClipboardService
    {
        public string? Text { get; private set; }

        public Task<OperationResult> CopyTextAsync(string text, string? html, CancellationToken cancellationToken)
        {
            Text = text;
            return Task.FromResult(OperationResult.Success());
        }
    }

    private sealed class FakeNotificationService : INotificationService
    {
        public List<string> CopiedTitles { get; } = [];

        public Task ShowCopiedAsync(string promptTitle, CancellationToken cancellationToken)
        {
            CopiedTitles.Add(promptTitle);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSettingsRepository : ISettingsRepository
    {
        public Task<AppSettings> GetAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new AppSettings { NotificationsEnabled = true });

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}