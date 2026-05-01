using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using PromptNest.Core.Abstractions;
using PromptNest.Core.Models;

namespace PromptNest.Core.Services;

public sealed class RepositoryPromptScanner : IRepositoryPromptScanner
{
    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".vs",
        ".idea",
        ".vscode",
        "bin",
        "obj",
        "node_modules",
        "packages",
        "dist",
        "build",
        "coverage",
        "artifacts"
    };

    private static readonly HashSet<string> DefaultExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md",
        ".txt",
        ".prompt",
        ".json",
        ".jsonl",
        ".yaml",
        ".yml",
        ".cs",
        ".ts",
        ".tsx",
        ".js",
        ".jsx",
        ".py"
    };

    private static readonly Regex PromptKeywordRegex = new(
        @"\b(system\s+prompt|prompt|instruction|instructions|assistant|developer\s+message|user\s+message)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<RepositoryPromptScanResult> ScanAsync(RepositoryPromptScanRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var candidates = new List<RepositoryPromptCandidate>();
        var warnings = new List<RepositoryPromptScanWarning>();
        var filesScanned = 0;
        var filesSkipped = 0;
        var repositoriesScanned = 0;

        foreach (string root in request.RepositoryRoots.Where(static root => !string.IsNullOrWhiteSpace(root)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string fullRoot = Path.GetFullPath(root);
            if (!Directory.Exists(fullRoot))
            {
                warnings.Add(new RepositoryPromptScanWarning
                {
                    Code = "RepositoryMissing",
                    Message = "Repository root does not exist.",
                    RepositoryRoot = fullRoot
                });
                continue;
            }

            repositoriesScanned++;
            foreach (string filePath in EnumerateCandidateFiles(fullRoot, request, warnings, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string relativePath = Path.GetRelativePath(fullRoot, filePath);
                var file = new FileInfo(filePath);
                if (file.Length > request.MaxFileBytes)
                {
                    filesSkipped++;
                    warnings.Add(new RepositoryPromptScanWarning
                    {
                        Code = "FileTooLarge",
                        Message = "File skipped because it exceeds the configured scanner size limit.",
                        RepositoryRoot = fullRoot,
                        RelativePath = relativePath
                    });
                    continue;
                }

                string text;
                try
                {
                    byte[] bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
                    if (bytes.Contains((byte)0))
                    {
                        filesSkipped++;
                        continue;
                    }

                    text = Encoding.UTF8.GetString(bytes);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    filesSkipped++;
                    warnings.Add(new RepositoryPromptScanWarning
                    {
                        Code = "FileReadFailed",
                        Message = "File could not be read.",
                        RepositoryRoot = fullRoot,
                        RelativePath = relativePath
                    });
                    continue;
                }

                filesScanned++;
                candidates.AddRange(ExtractCandidates(fullRoot, relativePath, text, request));
            }
        }

        return new RepositoryPromptScanResult
        {
            Summary = new RepositoryPromptScanSummary
            {
                RepositoriesScanned = repositoriesScanned,
                FilesScanned = filesScanned,
                FilesSkipped = filesSkipped,
                CandidatesFound = candidates.Count,
                Warnings = warnings.Count
            },
            Candidates = candidates,
            Warnings = warnings
        };
    }

    private static IEnumerable<string> EnumerateCandidateFiles(
        string root,
        RepositoryPromptScanRequest request,
        List<RepositoryPromptScanWarning> warnings,
        CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string directory = pending.Pop();
            IEnumerable<string> childDirectories;
            IEnumerable<string> files;

            try
            {
                childDirectories = Directory.EnumerateDirectories(directory);
                files = Directory.EnumerateFiles(directory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                warnings.Add(new RepositoryPromptScanWarning
                {
                    Code = "DirectoryReadFailed",
                    Message = "Directory could not be read.",
                    RepositoryRoot = root,
                    RelativePath = Path.GetRelativePath(root, directory)
                });
                continue;
            }

            foreach (string childDirectory in childDirectories)
            {
                string name = Path.GetFileName(childDirectory);
                if (!IgnoredDirectories.Contains(name))
                {
                    pending.Push(childDirectory);
                }
            }

            foreach (string file in files)
            {
                string relativePath = Path.GetRelativePath(root, file);
                if (IsExcluded(relativePath, request.ExcludeGlobs) || !IsIncluded(relativePath, request.IncludeGlobs))
                {
                    continue;
                }

                yield return file;
            }
        }
    }

    private static bool IsIncluded(string relativePath, IReadOnlyList<string> includeGlobs)
    {
        if (includeGlobs.Count > 0)
        {
            return includeGlobs.Any(pattern => WildcardMatch(relativePath, pattern));
        }

        return DefaultExtensions.Contains(Path.GetExtension(relativePath));
    }

    private static bool IsExcluded(string relativePath, IReadOnlyList<string> excludeGlobs) =>
        excludeGlobs.Any(pattern => WildcardMatch(relativePath, pattern));

    private static bool WildcardMatch(string value, string pattern)
    {
        string normalizedValue = value.Replace('\\', '/');
        string normalizedPattern = pattern.Replace('\\', '/');
        string regex = "^" + Regex.Escape(normalizedPattern).Replace("\\*", ".*", StringComparison.Ordinal).Replace("\\?", ".", StringComparison.Ordinal) + "$";
        return Regex.IsMatch(normalizedValue, regex, RegexOptions.IgnoreCase);
    }

    private static List<RepositoryPromptCandidate> ExtractCandidates(
        string repositoryRoot,
        string relativePath,
        string text,
        RepositoryPromptScanRequest request)
    {
        string extension = Path.GetExtension(relativePath);
        string repositoryName = Path.GetFileName(repositoryRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var candidates = new List<RepositoryPromptCandidate>();

        if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase) || extension.Equals(".jsonl", StringComparison.OrdinalIgnoreCase))
        {
            candidates.AddRange(ExtractJsonCandidates(repositoryRoot, repositoryName, relativePath, text, request));
        }
        else if (extension.Equals(".md", StringComparison.OrdinalIgnoreCase))
        {
            candidates.AddRange(ExtractMarkdownCandidates(repositoryRoot, repositoryName, relativePath, text, request));
        }
        else if (extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) || extension.Equals(".yml", StringComparison.OrdinalIgnoreCase))
        {
            candidates.AddRange(ExtractYamlCandidates(repositoryRoot, repositoryName, relativePath, text, request));
        }

        if (candidates.Count == 0 && ShouldTreatWholeFileAsPrompt(extension, text))
        {
            candidates.Add(CreateCandidate(repositoryRoot, repositoryName, relativePath, text, GuessTitle(relativePath, text), 1, CountLines(text), "text", 0.55, request));
        }

        return candidates;
    }

    private static IEnumerable<RepositoryPromptCandidate> ExtractMarkdownCandidates(
        string repositoryRoot,
        string repositoryName,
        string relativePath,
        string text,
        RepositoryPromptScanRequest request)
    {
        string[] lines = SplitLines(text);
        string? currentHeading = null;
        for (var index = 0; index < lines.Length; index++)
        {
            string line = lines[index];
            if (line.StartsWith('#'))
            {
                currentHeading = line.TrimStart('#').Trim();
            }

            if (!line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                continue;
            }

            string fence = line.Trim();
            bool promptFence = PromptKeywordRegex.IsMatch(fence) || (currentHeading is not null && PromptKeywordRegex.IsMatch(currentHeading));
            var block = new List<string>();
            int start = index + 2;
            index++;
            while (index < lines.Length && !lines[index].TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                block.Add(lines[index]);
                index++;
            }

            string body = string.Join("\n", block).Trim();
            if (promptFence && IsCandidateBody(body))
            {
                yield return CreateCandidate(repositoryRoot, repositoryName, relativePath, body, currentHeading, start, index, "markdown-fence", 0.82, request);
            }
        }
    }

    private static IEnumerable<RepositoryPromptCandidate> ExtractYamlCandidates(
        string repositoryRoot,
        string repositoryName,
        string relativePath,
        string text,
        RepositoryPromptScanRequest request)
    {
        string[] lines = SplitLines(text);
        for (var index = 0; index < lines.Length; index++)
        {
            string line = lines[index];
            if (!PromptKeywordRegex.IsMatch(line) || !line.Contains(':', StringComparison.Ordinal))
            {
                continue;
            }

            string[] parts = line.Split(':', 2);
            string title = parts[0].Trim();
            string value = parts[1].Trim();
            int startLine = index + 1;
            if (value == "|")
            {
                var block = new List<string>();
                index++;
                while (index < lines.Length && (lines[index].StartsWith(' ') || string.IsNullOrWhiteSpace(lines[index])))
                {
                    block.Add(lines[index].TrimStart());
                    index++;
                }

                value = string.Join("\n", block).Trim();
            }

            if (IsCandidateBody(value))
            {
                yield return CreateCandidate(repositoryRoot, repositoryName, relativePath, value, title, startLine, index + 1, "yaml", 0.75, request);
            }
        }
    }

    private static IEnumerable<RepositoryPromptCandidate> ExtractJsonCandidates(
        string repositoryRoot,
        string repositoryName,
        string relativePath,
        string text,
        RepositoryPromptScanRequest request)
    {
        if (Path.GetExtension(relativePath).Equals(".jsonl", StringComparison.OrdinalIgnoreCase))
        {
            string[] lines = SplitLines(text);
            for (var index = 0; index < lines.Length; index++)
            {
                foreach (RepositoryPromptCandidate candidate in ExtractJsonElementCandidates(repositoryRoot, repositoryName, relativePath, lines[index], index + 1, request))
                {
                    yield return candidate;
                }
            }

            yield break;
        }

        foreach (RepositoryPromptCandidate candidate in ExtractJsonElementCandidates(repositoryRoot, repositoryName, relativePath, text, 1, request))
        {
            yield return candidate;
        }
    }

    private static IEnumerable<RepositoryPromptCandidate> ExtractJsonElementCandidates(
        string repositoryRoot,
        string repositoryName,
        string relativePath,
        string text,
        int startLine,
        RepositoryPromptScanRequest request)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            yield break;
        }

        using (document)
        {
            foreach ((string? title, string body) in WalkJson(document.RootElement, null))
            {
                if (IsCandidateBody(body))
                {
                    yield return CreateCandidate(repositoryRoot, repositoryName, relativePath, body, title, startLine, null, "json", 0.78, request);
                }
            }
        }
    }

    private static IEnumerable<(string? Title, string Body)> WalkJson(JsonElement element, string? title)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                string? objectTitle = title;
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (property.NameEquals("title") || property.NameEquals("name"))
                    {
                        objectTitle = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : objectTitle;
                    }
                }

                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.String && PromptKeywordRegex.IsMatch(property.Name))
                    {
                        yield return (objectTitle ?? property.Name, property.Value.GetString() ?? string.Empty);
                    }

                    foreach ((string? childTitle, string body) in WalkJson(property.Value, objectTitle))
                    {
                        yield return (childTitle, body);
                    }
                }

                break;
            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    foreach ((string? childTitle, string body) in WalkJson(item, title))
                    {
                        yield return (childTitle, body);
                    }
                }

                break;
        }
    }

    private static bool ShouldTreatWholeFileAsPrompt(string extension, string text) =>
        extension.Equals(".prompt", StringComparison.OrdinalIgnoreCase) ||
        (extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) && PromptKeywordRegex.IsMatch(text)) ||
        (DefaultExtensions.Contains(extension) && PromptKeywordRegex.Matches(text).Count >= 2);

    private static bool IsCandidateBody(string body) =>
        !string.IsNullOrWhiteSpace(body) && body.Trim().Length >= 20;

    private static RepositoryPromptCandidate CreateCandidate(
        string repositoryRoot,
        string repositoryName,
        string relativePath,
        string body,
        string? titleHint,
        int? startLine,
        int? endLine,
        string format,
        double confidence,
        RepositoryPromptScanRequest request)
    {
        string normalizedBody = body.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        if (Encoding.UTF8.GetByteCount(normalizedBody) > request.MaxCandidateBodyBytes)
        {
            normalizedBody = normalizedBody[..Math.Min(normalizedBody.Length, request.MaxCandidateBodyBytes)];
        }

        return new RepositoryPromptCandidate
        {
            RepositoryRoot = repositoryRoot,
            RepositoryName = repositoryName,
            RelativePath = relativePath,
            Body = normalizedBody,
            TitleHint = string.IsNullOrWhiteSpace(titleHint) ? GuessTitle(relativePath, normalizedBody) : titleHint.Trim(),
            StartLine = startLine,
            EndLine = endLine,
            Format = format,
            Confidence = confidence,
            Tags = [format, Path.GetExtension(relativePath).TrimStart('.').ToLowerInvariant()]
        };
    }

    private static string GuessTitle(string relativePath, string text)
    {
        string? heading = SplitLines(text).FirstOrDefault(static line => line.StartsWith('#'));
        if (!string.IsNullOrWhiteSpace(heading))
        {
            return heading.TrimStart('#').Trim();
        }

        return Path.GetFileNameWithoutExtension(relativePath).Replace('-', ' ').Replace('_', ' ');
    }

    private static string[] SplitLines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

    private static int CountLines(string text) => SplitLines(text).Length;
}
