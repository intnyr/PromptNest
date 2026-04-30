using PromptNest.App.ViewModels;
using PromptNest.Core.Abstractions;
using PromptNest.Core.Models;

namespace PromptNest.UiTests;

public sealed class PromptCopyWorkflowTests
{
    [Fact]
    public async Task PaletteCopiesSelectedPromptThroughCopyService()
    {
        var search = new FakeSearchService();
        var copy = new FakePromptCopyService();
        var viewModel = new PaletteViewModel(search, copy) { SearchText = "market" };

        await viewModel.SearchAsync(CancellationToken.None);
        OperationResult<PromptCopyForm> form = await viewModel.PrepareSelectedCopyAsync(CancellationToken.None);
        OperationResult<ResolvedPrompt> result = await viewModel.CopySelectedWithValuesAsync(
            new Dictionary<string, string> { ["industry"] = "SaaS" },
            CancellationToken.None);

        Assert.True(form.Succeeded);
        Assert.True(result.Succeeded);
        Assert.Equal("market-analysis", copy.LastPromptId);
        Assert.Equal("SaaS", copy.LastValues["industry"]);
        Assert.StartsWith("Copied", viewModel.StatusText, StringComparison.Ordinal);
    }

    private sealed class FakeSearchService : ISearchService
    {
        public Task<PagedResult<Prompt>> SearchAsync(string text, PromptQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(
                new PagedResult<Prompt>
                {
                    Items =
                    [
                        new Prompt
                        {
                            Id = "market-analysis",
                            Title = "Market Analysis",
                            Body = "Analyze {{industry}}",
                            Variables = [new PromptVariable { Name = "industry" }],
                            CreatedAt = DateTimeOffset.UtcNow,
                            UpdatedAt = DateTimeOffset.UtcNow
                        }
                    ],
                    TotalCount = 1,
                    Skip = query.Skip,
                    Take = query.Take
                });
    }

    private sealed class FakePromptCopyService : IPromptCopyService
    {
        public string? LastPromptId { get; private set; }

        public Dictionary<string, string> LastValues { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<OperationResult<PromptCopyForm>> CreateFormAsync(string promptId, CancellationToken cancellationToken) =>
            Task.FromResult(
                OperationResultFactory.Success(
                    new PromptCopyForm
                    {
                        PromptId = promptId,
                        Title = "Market Analysis",
                        Variables =
                        [
                            new PromptCopyVariable
                            {
                                Name = "industry",
                                IsRequired = true
                            }
                        ]
                    }));

        public Task<OperationResult<ResolvedPrompt>> CopyAsync(
            string promptId,
            IReadOnlyDictionary<string, string> values,
            CancellationToken cancellationToken)
        {
            LastPromptId = promptId;
            LastValues = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(
                OperationResultFactory.Success(
                    new ResolvedPrompt
                    {
                        PromptId = promptId,
                        Text = "Analyze SaaS",
                        Values = LastValues
                    }));
        }
    }
}