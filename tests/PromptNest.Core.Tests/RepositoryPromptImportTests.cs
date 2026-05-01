using FluentAssertions;

using PromptNest.Core.Models;
using PromptNest.Core.Services;

namespace PromptNest.Core.Tests;

public sealed class RepositoryPromptImportTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(Path.GetTempPath(), "PromptNest.Scanner.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ScannerFindsPromptCandidatesAndSkipsIgnoredFolders()
    {
        Directory.CreateDirectory(tempDirectory);
        Directory.CreateDirectory(Path.Combine(tempDirectory, "node_modules"));
        await File.WriteAllTextAsync(
            Path.Combine(tempDirectory, "review.md"),
            """
            # Review Prompt

            ```prompt
            Review this pull request for correctness and missing tests.
            ```
            """);
        await File.WriteAllTextAsync(
            Path.Combine(tempDirectory, "config.json"),
            """
            { "title": "JSON Prompt", "prompt": "Summarize the repository architecture for a new contributor." }
            """);
        await File.WriteAllTextAsync(Path.Combine(tempDirectory, "node_modules", "ignored.prompt"), "Prompt that should be ignored.");

        var scanner = new RepositoryPromptScanner();
        RepositoryPromptScanResult result = await scanner.ScanAsync(
            new RepositoryPromptScanRequest { RepositoryRoots = [tempDirectory] },
            CancellationToken.None);

        result.Summary.RepositoriesScanned.Should().Be(1);
        result.Candidates.Should().HaveCount(2);
        result.Candidates.Should().Contain(candidate => candidate.RelativePath == "review.md" && candidate.StartLine == 4);
        result.Candidates.Should().OnlyContain(candidate => !candidate.RelativePath.Contains("node_modules", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NormalizerCreatesPromptNestExportWithStableFoldersTagsAndRedactedReportWarnings()
    {
        var normalizer = new RepositoryPromptImportNormalizer();
        RepositoryPromptCandidate candidate = NewCandidate("prompts/review.md", "Review {{topic}} with care.");
        RepositoryPromptCandidate duplicate = candidate with { RelativePath = "copy.md" };
        RepositoryPromptCandidate secret = NewCandidate("prompts/secret.md", "Use api_key=\"abcdef1234567890\" only in local tests.");

        RepositoryPromptImportDocument document = normalizer.Normalize(
            [candidate, duplicate, secret],
            new RepositoryPromptNormalizeOptions { RedactionMode = RepositoryPromptRedactionMode.ReportOnly });

        document.Export.Folders.Should().Contain(folder => folder.Name == "Repository Imports");
        document.Export.Folders.Should().Contain(folder => folder.Name == "RepoA");
        document.Export.Tags.Select(tag => tag.Name).Should().Contain(["imported", "source:repo", "repoa"]);
        document.Export.Prompts.Should().HaveCount(2);
        document.Export.Prompts.Single(prompt => prompt.Body.Contains("{{topic}}", StringComparison.Ordinal)).Title.Should().Be("review");
        document.Report.DuplicatesSkipped.Should().Be(1);
        document.Report.PotentialSecrets.Should().Be(1);
        document.Report.Warnings.Should().Contain(warning => warning.Code == "PotentialSecret");
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static RepositoryPromptCandidate NewCandidate(string relativePath, string body) => new()
    {
        RepositoryRoot = "D:\\Repos\\RepoA",
        RepositoryName = "RepoA",
        RelativePath = relativePath,
        TitleHint = Path.GetFileNameWithoutExtension(relativePath),
        Body = body,
        Format = "markdown-fence",
        Confidence = 0.8,
        Tags = ["markdown-fence", "md"]
    };
}
