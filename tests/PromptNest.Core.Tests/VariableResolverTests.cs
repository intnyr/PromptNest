using FluentAssertions;

using PromptNest.Core.Abstractions;
using PromptNest.Core.Models;
using PromptNest.Core.Variables;

namespace PromptNest.Core.Tests;

public sealed class VariableResolverTests
{
    [Fact]
    public async Task ResolveAsyncUsesSuppliedDefaultAndLastUsedValues()
    {
        var repository = new InMemoryVariableValueRepository(new Dictionary<string, string> { ["audience"] = "Executives" });
        var resolver = new VariableResolver(repository);
        var prompt = new Prompt
        {
            Id = "prompt",
            Title = "Title",
            Body = "Tone {{tone|friendly}} for {{audience}} in {{industry}}.",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Variables =
            [
                new PromptVariable { Name = "tone", DefaultValue = "friendly" },
                new PromptVariable { Name = "audience" },
                new PromptVariable { Name = "industry" }
            ]
        };

        var result = await resolver.ResolveAsync(
            prompt,
            new Dictionary<string, string> { ["industry"] = "SaaS" },
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.Text.Should().Be("Tone friendly for Executives in SaaS.");
        repository.SavedValues.Should().Contain("industry", "SaaS");
    }

    [Fact]
    public async Task ResolveAsyncFailsWhenRequiredVariableIsMissing()
    {
        var resolver = new VariableResolver(new InMemoryVariableValueRepository());
        var prompt = new Prompt
        {
            Id = "prompt",
            Title = "Title",
            Body = "Hello {{name}}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Variables = [new PromptVariable { Name = "name" }]
        };

        var result = await resolver.ResolveAsync(prompt, new Dictionary<string, string>(), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be("VariableRequired");
    }

    [Fact]
    public async Task ResolveAsyncStoresDefaultValuesUsedForCopy()
    {
        var repository = new InMemoryVariableValueRepository();
        var resolver = new VariableResolver(repository);
        var prompt = new Prompt
        {
            Id = "prompt",
            Title = "Title",
            Body = "Tone {{tone|friendly}}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Variables = [new PromptVariable { Name = "tone", DefaultValue = "friendly" }]
        };

        var result = await resolver.ResolveAsync(prompt, new Dictionary<string, string>(), CancellationToken.None);

        result.Value!.Text.Should().Be("Tone friendly");
        repository.SavedValues.Should().Contain("tone", "friendly");
    }

    private sealed class InMemoryVariableValueRepository : IVariableValueRepository
    {
        private readonly IReadOnlyDictionary<string, string> _lastUsedValues;

        public InMemoryVariableValueRepository()
            : this(new Dictionary<string, string>())
        {
        }

        public InMemoryVariableValueRepository(IReadOnlyDictionary<string, string> lastUsedValues)
        {
            _lastUsedValues = lastUsedValues;
        }

        public IReadOnlyDictionary<string, string> SavedValues { get; private set; } = new Dictionary<string, string>();

        public Task<IReadOnlyDictionary<string, string>> GetLastUsedValuesAsync(
            string promptId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_lastUsedValues);
        }

        public Task SaveLastUsedValuesAsync(
            string promptId,
            IReadOnlyDictionary<string, string> values,
            CancellationToken cancellationToken)
        {
            SavedValues = values;
            return Task.CompletedTask;
        }
    }
}