using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.Extensions.DependencyInjection;

using PromptNest.Core;
using PromptNest.Core.Abstractions;
using PromptNest.Core.Models;
using PromptNest.Data;
using PromptNest.Platform.Paths;

namespace PromptNest.Cli;

public static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static async Task<int> Main(string[] args)
    {
        try
        {
            CliArguments parsed = CliArguments.Parse(args);
            if (parsed.Command is null || parsed.HasFlag("--help") || parsed.HasFlag("-h"))
            {
                WriteUsage();
                return parsed.Command is null ? ExitCodes.InvalidArguments : ExitCodes.Success;
            }

            await using ServiceProvider services = BuildServices(parsed);
            return parsed.Command switch
            {
                "validate" => await ValidateAsync(parsed, services),
                "import" => await ImportAsync(parsed, services),
                "export" => await ExportAsync(parsed, services),
                "scan" => await ScanAsync(parsed, services),
                _ => WriteError($"Unknown command '{parsed.Command}'.", ExitCodes.InvalidArguments)
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            WriteJson(new CliResult(false, "UnexpectedError", ex.Message, null));
            return ExitCodes.UnexpectedError;
        }
    }

    private static ServiceProvider BuildServices(CliArguments args)
    {
        var services = new ServiceCollection();
        string? dataRoot = args.GetOption("--data-root");
        services.AddSingleton<IPathProvider>(
            string.IsNullOrWhiteSpace(dataRoot)
                ? new PathProvider()
                : new PathProvider(Path.GetFullPath(dataRoot), isPackaged: false));
        services.AddPromptNestCore();
        services.AddPromptNestData();
        return services.BuildServiceProvider();
    }

    private static async Task<int> ValidateAsync(CliArguments args, IServiceProvider services)
    {
        PromptNestExport export = await ReadExportAsync(args.RequireOption("--file"));
        await services.GetRequiredService<IMigrationRunner>().MigrateAsync(CancellationToken.None);

        OperationResult<ImportPlan> result = await services
            .GetRequiredService<IImportExportService>()
            .PreviewImportAsync(export, CreateImportOptions(args) with { DryRun = true }, CancellationToken.None);

        WriteJson(new CliResult(result.Succeeded && result.Value is { HasErrors: false }, result.ErrorCode, result.Message, result.Value));
        return result.Value is { HasErrors: false } ? ExitCodes.Success : ExitCodes.ValidationFailed;
    }

    private static async Task<int> ImportAsync(CliArguments args, IServiceProvider services)
    {
        PromptNestExport export = await ReadExportAsync(args.RequireOption("--file"));
        await services.GetRequiredService<IMigrationRunner>().MigrateAsync(CancellationToken.None);

        bool dryRun = args.HasFlag("--dry-run");
        if (!dryRun && args.HasFlag("--backup-before-apply"))
        {
            OperationResult<BackupMetadata> backup = await services.GetRequiredService<IBackupService>().CreateBackupAsync(CancellationToken.None);
            if (!backup.Succeeded)
            {
                WriteJson(new CliResult(false, backup.ErrorCode, backup.Message, null));
                return ExitCodes.ImportFailed;
            }
        }

        ImportOptions options = CreateImportOptions(args) with { DryRun = dryRun };
        OperationResult<ImportSummary> result = await services
            .GetRequiredService<IImportExportService>()
            .ImportAsync(export, options, CancellationToken.None);

        ImportSummary? summary = result.Value;
        await PublishLinearReportIfRequestedAsync(args, services, null, null, summary, ["import command"]);
        WriteJson(new CliResult(result.Succeeded, result.ErrorCode, result.Message, summary));
        return result.Succeeded ? ExitCodes.Success : ExitCodes.ImportFailed;
    }

    private static async Task<int> ExportAsync(CliArguments args, IServiceProvider services)
    {
        string outPath = args.RequireOption("--out");
        await services.GetRequiredService<IMigrationRunner>().MigrateAsync(CancellationToken.None);

        OperationResult<PromptNestExport> result = await services.GetRequiredService<IImportExportService>().ExportAsync(CancellationToken.None);
        if (!result.Succeeded || result.Value is null)
        {
            WriteJson(new CliResult(false, result.ErrorCode, result.Message, null));
            return ExitCodes.ExportFailed;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)) ?? Directory.GetCurrentDirectory());
        await File.WriteAllTextAsync(outPath, JsonSerializer.Serialize(result.Value, JsonOptions), Encoding.UTF8);
        WriteJson(new CliResult(true, null, "Export written.", new { outPath }));
        return ExitCodes.Success;
    }

    private static async Task<int> ScanAsync(CliArguments args, IServiceProvider services)
    {
        IReadOnlyList<string> roots = args.GetMany("--repo").Concat(args.GetMany("--repos").SelectMany(SplitList)).ToArray();
        if (roots.Count == 0)
        {
            return WriteError("scan requires at least one --repo or --repos value.", ExitCodes.InvalidArguments);
        }

        var request = new RepositoryPromptScanRequest
        {
            RepositoryRoots = roots,
            IncludeGlobs = args.GetMany("--include"),
            ExcludeGlobs = args.GetMany("--exclude")
        };

        RepositoryPromptScanResult scanResult = await services.GetRequiredService<IRepositoryPromptScanner>().ScanAsync(request, CancellationToken.None);
        RepositoryPromptImportDocument document = services
            .GetRequiredService<IRepositoryPromptImportNormalizer>()
            .Normalize(scanResult.Candidates, new RepositoryPromptNormalizeOptions());

        string? outPath = args.GetOption("--out");
        if (!string.IsNullOrWhiteSpace(outPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)) ?? Directory.GetCurrentDirectory());
            await File.WriteAllTextAsync(outPath, JsonSerializer.Serialize(document.Export, JsonOptions), Encoding.UTF8);
        }

        LinearBatchReportResult report = await PublishLinearReportIfRequestedAsync(
            args,
            services,
            scanResult,
            document.Report,
            null,
            outPath is null ? [] : [$"import JSON written to `{outPath}`"]);

        string? reportPath = args.GetOption("--report");
        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath)) ?? Directory.GetCurrentDirectory());
            await File.WriteAllTextAsync(reportPath, report.Markdown, Encoding.UTF8);
        }

        WriteJson(new CliResult(true, null, "Scan complete.", new { scanResult.Summary, document.Report, outPath, reportPath, report.Published }));
        return ExitCodes.Success;
    }

    private static ImportOptions CreateImportOptions(CliArguments args)
    {
        string conflict = args.GetOption("--conflict") ?? "skip";
        ImportConflictMode mode = conflict.ToLowerInvariant() switch
        {
            "skip" => ImportConflictMode.Skip,
            "overwrite" => ImportConflictMode.Overwrite,
            "duplicate" => ImportConflictMode.Duplicate,
            _ => throw new ArgumentException($"Unsupported conflict mode '{conflict}'.")
        };

        return new ImportOptions { ConflictMode = mode };
    }

    private static async Task<PromptNestExport> ReadExportAsync(string filePath)
    {
        await using FileStream stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<PromptNestExport>(stream, JsonOptions, CancellationToken.None)
            ?? throw new InvalidOperationException("Import file did not contain a PromptNest export payload.");
    }

    private static async Task<LinearBatchReportResult> PublishLinearReportIfRequestedAsync(
        CliArguments args,
        IServiceProvider services,
        RepositoryPromptScanResult? scanResult,
        RepositoryPromptImportReport? importReport,
        ImportSummary? importSummary,
        IReadOnlyList<string> notes)
    {
        string? issueId = args.GetOption("--linear-batch-issue");
        string batchName = args.GetOption("--batch-name") ?? "Repository prompt import";
        var request = new LinearBatchReportRequest
        {
            IssueId = issueId,
            BatchName = batchName,
            ScanResult = scanResult,
            ImportReport = importReport,
            ImportSummary = importSummary,
            Repositories = args.GetMany("--repo").Concat(args.GetMany("--repos").SelectMany(SplitList)).ToArray(),
            Notes = notes
        };

        LinearBatchReportResult formatted = services.GetRequiredService<ILinearBatchReportFormatter>().Format(request);
        if (string.IsNullOrWhiteSpace(issueId))
        {
            return formatted;
        }

        string? apiKey = Environment.GetEnvironmentVariable("LINEAR_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return formatted with { Message = "LINEAR_API_KEY was not set; report was formatted but not published." };
        }

        try
        {
            await PublishLinearCommentAsync(issueId, formatted.Markdown, apiKey);
            return formatted with { Published = true, Message = "Linear batch report published." };
        }
        catch (HttpRequestException ex)
        {
            return formatted with { Message = "Linear publish failed: " + ex.Message };
        }
    }

    private static async Task PublishLinearCommentAsync(string issueIdentifier, string markdown, string apiKey)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        string issueQuery = """
            query Issue($id: String!) {
              issue(id: $id) { id }
            }
            """;
        JsonNode issueResponse = await PostLinearGraphQlAsync(client, issueQuery, new JsonObject { ["id"] = issueIdentifier });
        string? issueId = issueResponse["data"]?["issue"]?["id"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(issueId))
        {
            throw new HttpRequestException("Linear issue was not found.");
        }

        string mutation = """
            mutation CommentCreate($input: CommentCreateInput!) {
              commentCreate(input: $input) { success }
            }
            """;
        var variables = new JsonObject
        {
            ["input"] = new JsonObject
            {
                ["issueId"] = issueId,
                ["body"] = markdown
            }
        };
        JsonNode commentResponse = await PostLinearGraphQlAsync(client, mutation, variables);
        bool success = commentResponse["data"]?["commentCreate"]?["success"]?.GetValue<bool>() ?? false;
        if (!success)
        {
            throw new HttpRequestException("Linear comment mutation did not succeed.");
        }
    }

    private static async Task<JsonNode> PostLinearGraphQlAsync(HttpClient client, string query, JsonObject variables)
    {
        var payload = new JsonObject
        {
            ["query"] = query,
            ["variables"] = variables
        };
        using var response = await client.PostAsync(
            "https://api.linear.app/graphql",
            new StringContent(payload.ToJsonString(JsonOptions), Encoding.UTF8, "application/json"));
        string body = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();
        return JsonNode.Parse(body) ?? throw new HttpRequestException("Linear returned an empty response.");
    }

    private static IEnumerable<string> SplitList(string value) =>
        value.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static void WriteJson(CliResult result)
    {
        Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
    }

    private static int WriteError(string message, int exitCode)
    {
        WriteJson(new CliResult(false, "InvalidArguments", message, null));
        return exitCode;
    }

    private static void WriteUsage()
    {
        Console.WriteLine(
            """
            PromptNest.Cli

            Commands:
              validate --file <export.json> [--data-root <dir>]
              import --file <export.json> [--conflict skip|overwrite|duplicate] [--dry-run] [--backup-before-apply] [--data-root <dir>]
              export --out <export.json> [--data-root <dir>]
              scan --repo <path> [--repo <path>] [--out <export.json>] [--report <report.md>] [--linear-batch-issue TAS-123]

            JSON output is written to stdout. Raw prompt bodies are not written to Linear reports.
            """);
    }

    private sealed record CliResult(bool Succeeded, string? ErrorCode, string? Message, object? Value);

    private sealed class CliArguments
    {
        private readonly Dictionary<string, List<string>> options = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> flags = new(StringComparer.OrdinalIgnoreCase);

        public string? Command { get; private init; }

        public static CliArguments Parse(string[] args)
        {
            var parsed = new CliArguments { Command = args.FirstOrDefault() };
            for (var index = 1; index < args.Length; index++)
            {
                string token = args[index];
                if (!token.StartsWith("--", StringComparison.Ordinal) && !token.StartsWith('-'))
                {
                    continue;
                }

                if (index == args.Length - 1 || args[index + 1].StartsWith('-'))
                {
                    parsed.flags.Add(token);
                    continue;
                }

                parsed.options.TryAdd(token, []);
                parsed.options[token].Add(args[++index]);
            }

            return parsed;
        }

        public bool HasFlag(string name) => flags.Contains(name);

        public string? GetOption(string name) => options.TryGetValue(name, out List<string>? values) ? values.LastOrDefault() : null;

        public List<string> GetMany(string name) => options.TryGetValue(name, out List<string>? values) ? values : [];

        public string RequireOption(string name) =>
            GetOption(name) ?? throw new ArgumentException(string.Create(CultureInfo.InvariantCulture, $"Missing required option {name}."));
    }

    private static class ExitCodes
    {
        public const int Success = 0;
        public const int InvalidArguments = 2;
        public const int ValidationFailed = 3;
        public const int ImportFailed = 4;
        public const int ExportFailed = 5;
        public const int UnexpectedError = 99;
    }
}
