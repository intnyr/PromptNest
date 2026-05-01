using System.Text.Json;

using FluentAssertions;

using Microsoft.Data.Sqlite;

using PromptNest.Cli;
using PromptNest.Core.Models;

namespace PromptNest.Cli.Tests;

public sealed class CliWorkflowTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string tempDirectory = Path.Combine(Path.GetTempPath(), "PromptNest.Cli.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ValidateImportAndExportRoundTripAgainstTemporaryDataRoot()
    {
        Directory.CreateDirectory(tempDirectory);
        string importFile = Path.Combine(tempDirectory, "import.json");
        string exportFile = Path.Combine(tempDirectory, "export.json");
        await File.WriteAllTextAsync(importFile, JsonSerializer.Serialize(NewExport(), JsonOptions));

        int validateExit = await Program.Main(["validate", "--file", importFile, "--data-root", tempDirectory]);
        int dryRunExit = await Program.Main(["import", "--file", importFile, "--data-root", tempDirectory, "--dry-run"]);
        int importExit = await Program.Main(["import", "--file", importFile, "--data-root", tempDirectory]);
        int exportExit = await Program.Main(["export", "--out", exportFile, "--data-root", tempDirectory]);

        validateExit.Should().Be(0);
        dryRunExit.Should().Be(0);
        importExit.Should().Be(0);
        exportExit.Should().Be(0);
        File.Exists(exportFile).Should().BeTrue();

        string exportedJson = await File.ReadAllTextAsync(exportFile);
        exportedJson.Should().Contain("cli-prompt");
    }

    [Fact]
    public async Task ScanCommandWritesImportJsonAndRedactedReport()
    {
        Directory.CreateDirectory(tempDirectory);
        string repo = Path.Combine(tempDirectory, "Repo");
        Directory.CreateDirectory(repo);
        await File.WriteAllTextAsync(
            Path.Combine(repo, "prompt.md"),
            """
            # Commit Prompt

            ```prompt
            Write a concise commit message for the staged changes.
            ```
            """);

        string outFile = Path.Combine(tempDirectory, "scan.json");
        string reportFile = Path.Combine(tempDirectory, "report.md");

        int exitCode = await Program.Main(["scan", "--repo", repo, "--out", outFile, "--report", reportFile]);

        exitCode.Should().Be(0);
        File.Exists(outFile).Should().BeTrue();
        File.Exists(reportFile).Should().BeTrue();
        string report = await File.ReadAllTextAsync(reportFile);
        report.Should().Contain("Raw prompt bodies are intentionally omitted");
        report.Should().NotContain("Write a concise commit message");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static PromptNestExport NewExport() => new()
    {
        ExportedAt = DateTimeOffset.UtcNow,
        Prompts =
        [
            new Prompt
            {
                Id = "cli-prompt",
                Title = "CLI Prompt",
                Body = "Hello from the CLI {{name|friend}}.",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Tags = ["cli"]
            }
        ],
        Tags = [new Tag { Name = "cli" }]
    };
}
