using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

using PromptNest.Core.Abstractions;
using PromptNest.Core.Models;

namespace PromptNest.Core.Services;

public sealed class RepositoryPromptImportNormalizer : IRepositoryPromptImportNormalizer
{
    private static readonly Regex SecretRegex = new(
        @"(?i)(api[_-]?key|secret|token|password)\s*[:=]\s*['""]?([a-z0-9_\-]{16,})",
        RegexOptions.Compiled);

    public RepositoryPromptImportDocument Normalize(
        IReadOnlyList<RepositoryPromptCandidate> candidates,
        RepositoryPromptNormalizeOptions options)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(options);

        var now = DateTimeOffset.UtcNow;
        string rootFolderId = StableId("folder", options.RootFolderName);
        var folders = new Dictionary<string, Folder>(StringComparer.Ordinal)
        {
            [rootFolderId] = new Folder
            {
                Id = rootFolderId,
                Name = options.RootFolderName,
                CreatedAt = now
            }
        };
        var prompts = new List<Prompt>();
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var bodyHashes = new HashSet<string>(StringComparer.Ordinal);
        var warnings = new List<RepositoryPromptScanWarning>();
        var duplicatesSkipped = 0;
        var rejected = 0;
        var potentialSecrets = 0;

        foreach (RepositoryPromptCandidate candidate in candidates)
        {
            string normalizedBody = NormalizeBody(candidate.Body);
            if (string.IsNullOrWhiteSpace(normalizedBody) || Encoding.UTF8.GetByteCount(normalizedBody) > PromptLimits.MaxPromptBodyBytes)
            {
                rejected++;
                warnings.Add(Warning("CandidateRejected", "Candidate body is empty or exceeds the PromptNest body size limit.", candidate));
                continue;
            }

            bool hasSecret = SecretRegex.IsMatch(normalizedBody);
            if (hasSecret)
            {
                potentialSecrets++;
                warnings.Add(Warning("PotentialSecret", "Candidate may contain a secret; report output is redacted.", candidate));
                if (options.RedactionMode == RepositoryPromptRedactionMode.Block)
                {
                    rejected++;
                    continue;
                }

                if (options.RedactionMode == RepositoryPromptRedactionMode.RedactBody)
                {
                    normalizedBody = SecretRegex.Replace(normalizedBody, "$1=[REDACTED]");
                }
            }

            string bodyHash = HashHex(normalizedBody);
            if (!bodyHashes.Add(bodyHash))
            {
                duplicatesSkipped++;
                warnings.Add(Warning("DuplicateBody", "Candidate body matches an earlier candidate and was skipped.", candidate));
                continue;
            }

            string repositoryTag = NormalizeTag(candidate.RepositoryName);
            string repoFolderId = StableId("folder", $"{options.RootFolderName}/{candidate.RepositoryName}");
            if (!folders.ContainsKey(repoFolderId))
            {
                folders[repoFolderId] = new Folder
                {
                    Id = repoFolderId,
                    Name = candidate.RepositoryName,
                    ParentId = rootFolderId,
                    CreatedAt = now
                };
            }

            string promptId = StableId(
                "prompt",
                $"{candidate.RepositoryName}|{candidate.RelativePath}|{candidate.StartLine}|{bodyHash}");
            string title = NormalizeTitle(candidate.TitleHint, candidate.RelativePath);
            string[] promptTags =
            [
                "imported",
                "source:repo",
                repositoryTag,
                .. candidate.Tags.Select(NormalizeTag).Where(IsValidTag)
            ];

            foreach (string tag in promptTags)
            {
                tags.Add(tag);
            }

            prompts.Add(new Prompt
            {
                Id = promptId,
                Title = title,
                Body = normalizedBody,
                FolderId = repoFolderId,
                Tags = promptTags.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        return new RepositoryPromptImportDocument
        {
            Export = new PromptNestExport
            {
                ExportedAt = now,
                Folders = folders.Values.ToArray(),
                Tags = tags.Select(static tag => new Tag { Name = tag }).ToArray(),
                Prompts = prompts
            },
            Report = new RepositoryPromptImportReport
            {
                CandidatesReceived = candidates.Count,
                PromptsCreated = prompts.Count,
                DuplicatesSkipped = duplicatesSkipped,
                Rejected = rejected,
                PotentialSecrets = potentialSecrets,
                Warnings = warnings
            }
        };
    }

    private static RepositoryPromptScanWarning Warning(string code, string message, RepositoryPromptCandidate candidate) =>
        new()
        {
            Code = code,
            Message = message,
            RepositoryRoot = candidate.RepositoryRoot,
            RelativePath = candidate.RelativePath
        };

    private static string NormalizeBody(string body) =>
        body.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim();

    private static string NormalizeTitle(string? titleHint, string relativePath)
    {
        string title = string.IsNullOrWhiteSpace(titleHint)
            ? Path.GetFileNameWithoutExtension(relativePath)
            : titleHint.Trim();

        title = Regex.Replace(title, @"\s+", " ").Trim();
        return title.Length <= 120 ? title : title[..120].Trim();
    }

    private static string NormalizeTag(string tag) =>
        Regex.Replace(tag.Trim().ToLowerInvariant(), @"[^a-z0-9:_\-]+", "-").Trim('-');

    private static bool IsValidTag(string tag) => !string.IsNullOrWhiteSpace(tag) && tag.Length <= 64;

    private static string StableId(string prefix, string value) => $"{prefix}-{HashHex(value)[..24]}";

    private static string HashHex(string value)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
