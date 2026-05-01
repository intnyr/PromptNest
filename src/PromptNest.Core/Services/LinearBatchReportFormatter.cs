using System.Text;
using System.Globalization;

using PromptNest.Core.Abstractions;
using PromptNest.Core.Models;

namespace PromptNest.Core.Services;

public sealed class LinearBatchReportFormatter : ILinearBatchReportFormatter
{
    public LinearBatchReportResult Format(LinearBatchReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var builder = new StringBuilder();
        AppendInvariant(builder, $"## PromptNest import batch: {request.BatchName}");
        builder.AppendLine();
        builder.AppendLine("Raw prompt bodies are intentionally omitted from this report.");
        builder.AppendLine();

        if (request.Repositories.Count > 0)
        {
            builder.AppendLine("### Repositories");
            foreach (string repository in request.Repositories)
            {
                AppendInvariant(builder, $"* `{repository}`");
            }

            builder.AppendLine();
        }

        if (request.ScanResult is not null)
        {
            RepositoryPromptScanSummary summary = request.ScanResult.Summary;
            builder.AppendLine("### Scan summary");
            AppendInvariant(builder, $"* Repositories scanned: {summary.RepositoriesScanned}");
            AppendInvariant(builder, $"* Files scanned: {summary.FilesScanned}");
            AppendInvariant(builder, $"* Files skipped: {summary.FilesSkipped}");
            AppendInvariant(builder, $"* Candidates found: {summary.CandidatesFound}");
            AppendInvariant(builder, $"* Warnings: {summary.Warnings}");
            builder.AppendLine();
        }

        if (request.ImportReport is not null)
        {
            RepositoryPromptImportReport report = request.ImportReport;
            builder.AppendLine("### Normalization summary");
            AppendInvariant(builder, $"* Candidates received: {report.CandidatesReceived}");
            AppendInvariant(builder, $"* Prompts prepared: {report.PromptsCreated}");
            AppendInvariant(builder, $"* Duplicates skipped: {report.DuplicatesSkipped}");
            AppendInvariant(builder, $"* Rejected: {report.Rejected}");
            AppendInvariant(builder, $"* Potential secrets flagged: {report.PotentialSecrets}");
            builder.AppendLine();
        }

        if (request.ImportSummary is not null)
        {
            ImportSummary summary = request.ImportSummary;
            builder.AppendLine("### Import summary");
            AppendInvariant(builder, $"* Dry run: {summary.DryRun}");
            AppendInvariant(builder, $"* Prompts created: {summary.PromptsCreated}");
            AppendInvariant(builder, $"* Prompts updated: {summary.PromptsUpdated}");
            AppendInvariant(builder, $"* Prompts skipped: {summary.PromptsSkipped}");
            AppendInvariant(builder, $"* Folders created: {summary.FoldersCreated}");
            AppendInvariant(builder, $"* Tags created: {summary.TagsCreated}");
            AppendInvariant(builder, $"* Validation errors: {summary.ValidationErrors}");
            AppendInvariant(builder, $"* Validation warnings: {summary.ValidationWarnings}");
            builder.AppendLine();
        }

        if (request.Notes.Count > 0)
        {
            builder.AppendLine("### Notes");
            foreach (string note in request.Notes)
            {
                AppendInvariant(builder, $"* {note}");
            }
        }

        return new LinearBatchReportResult
        {
            Markdown = builder.ToString().TrimEnd(),
            Message = "Formatted redacted Linear batch report."
        };
    }

    private static void AppendInvariant(StringBuilder builder, FormattableString line)
    {
        builder.AppendLine(string.Format(CultureInfo.InvariantCulture, line.Format, line.GetArguments()));
    }
}
