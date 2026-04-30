using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using PromptNest.Core.Abstractions;
using PromptNest.Core.Models;

namespace PromptNest.App.ViewModels;

public sealed partial class PaletteViewModel : ObservableObject
{
    private readonly ISearchService _searchService;
    private readonly IPromptCopyService _promptCopyService;

    public PaletteViewModel(ISearchService searchService, IPromptCopyService promptCopyService)
    {
        _searchService = searchService;
        _promptCopyService = promptCopyService;
    }

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<PalettePromptResultViewModel> results = [];

    [ObservableProperty]
    private PalettePromptResultViewModel? selectedResult;

    [ObservableProperty]
    private string statusText = "Ready";

    public async Task SearchAsync(CancellationToken cancellationToken)
    {
        PagedResult<Prompt> result = await _searchService.SearchAsync(
            SearchText,
            new PromptQuery { Take = 8, SortBy = PromptSortBy.UpdatedAt, SortDescending = true },
            cancellationToken);

        Results = result.Items.Select(MapResult).ToArray();
        SelectedResult = Results.Count == 0 ? null : Results[0];
        StatusText = Results.Count == 0 ? "No matching prompts" : $"{Results.Count} results";
    }

    [RelayCommand]
    private async Task CopySelected(CancellationToken cancellationToken)
    {
        if (SelectedResult is null)
        {
            return;
        }

        OperationResult<ResolvedPrompt> result = await _promptCopyService.CopyAsync(
            SelectedResult.Id,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            cancellationToken);
        StatusText = result.Succeeded ? $"Copied {SelectedResult.Title}" : result.Message ?? "Copy failed";
    }

    public Task<OperationResult<PromptCopyForm>> PrepareSelectedCopyAsync(CancellationToken cancellationToken) =>
        SelectedResult is null
            ? Task.FromResult(OperationResultFactory.Failure<PromptCopyForm>("NoPromptSelected", "No prompt is selected."))
            : _promptCopyService.CreateFormAsync(SelectedResult.Id, cancellationToken);

    public async Task<OperationResult<ResolvedPrompt>> CopySelectedWithValuesAsync(
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken)
    {
        if (SelectedResult is null)
        {
            return OperationResultFactory.Failure<ResolvedPrompt>("NoPromptSelected", "No prompt is selected.");
        }

        OperationResult<ResolvedPrompt> result = await _promptCopyService.CopyAsync(SelectedResult.Id, values, cancellationToken);
        StatusText = result.Succeeded ? $"Copied {SelectedResult.Title}" : result.Message ?? "Copy failed";
        return result;
    }

    [RelayCommand]
    private void SelectNext()
    {
        MoveSelection(1);
    }

    [RelayCommand]
    private void SelectPrevious()
    {
        MoveSelection(-1);
    }

    private void MoveSelection(int offset)
    {
        if (Results.Count == 0)
        {
            return;
        }

        int currentIndex = SelectedResult is null ? 0 : Results.ToList().IndexOf(SelectedResult);
        int nextIndex = Math.Clamp(currentIndex + offset, 0, Results.Count - 1);
        SelectedResult = Results[nextIndex];
    }

    private static PalettePromptResultViewModel MapResult(Prompt prompt)
    {
        return new PalettePromptResultViewModel
        {
            Id = prompt.Id,
            Title = prompt.Title,
            Preview = prompt.Body.Replace(Environment.NewLine, " ", StringComparison.Ordinal),
            HasVariables = prompt.Variables.Count > 0,
            Tags = prompt.Tags
        };
    }
}

public sealed record PalettePromptResultViewModel
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required string Preview { get; init; }

    public bool HasVariables { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];
}