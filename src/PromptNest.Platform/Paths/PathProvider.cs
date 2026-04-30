using PromptNest.Core.Abstractions;

namespace PromptNest.Platform.Paths;

public sealed class PathProvider : IPathProvider
{
    private const string AppFolderName = "PromptNest";

    public PathProvider()
        : this(
            Environment.GetEnvironmentVariable("PROMPTNEST_LOCALAPPDATA")
                ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ResolvePackagedMode())
    {
    }

    public PathProvider(string localApplicationDataPath, bool isPackaged)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localApplicationDataPath);

        IsPackaged = isPackaged;
        DataDirectory = Path.Combine(localApplicationDataPath, AppFolderName);
        DatabasePath = Path.Combine(DataDirectory, "library.db");
        LogsDirectory = Path.Combine(DataDirectory, "logs");
        BackupsDirectory = Path.Combine(DataDirectory, "Backups");
        SettingsPath = Path.Combine(DataDirectory, "settings.json");
        UpdateCacheDirectory = Path.Combine(DataDirectory, "Updates");
    }

    public string DataDirectory { get; }

    public string DatabasePath { get; }

    public string LogsDirectory { get; }

    public string BackupsDirectory { get; }

    public string SettingsPath { get; }

    public string UpdateCacheDirectory { get; }

    public bool IsPackaged { get; }

    public PathDiagnostics GetDiagnostics()
    {
        return new PathDiagnostics(
            IsPackaged,
            DataDirectory,
            DatabasePath,
            LogsDirectory,
            BackupsDirectory,
            SettingsPath,
            UpdateCacheDirectory);
    }

    private static bool ResolvePackagedMode()
    {
        var packagedFlag = Environment.GetEnvironmentVariable("PROMPTNEST_PACKAGED");
        return string.Equals(packagedFlag, "true", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record PathDiagnostics(
    bool IsPackaged,
    string DataDirectory,
    string DatabasePath,
    string LogsDirectory,
    string BackupsDirectory,
    string SettingsPath,
    string UpdateCacheDirectory);