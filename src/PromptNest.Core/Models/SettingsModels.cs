namespace PromptNest.Core.Models;

public enum AppTheme
{
    System,
    Light,
    Dark
}

public enum UpdateChannel
{
    Stable,
    Beta,
    Dev
}

public sealed record AppSettings
{
    public AppTheme Theme { get; init; } = AppTheme.System;

    public string GlobalHotkey { get; init; } = "Win+Shift+Space";

    public bool NotificationsEnabled { get; init; } = true;

    public int BackupRetentionCount { get; init; } = 7;

    public bool UpdateChecksEnabled { get; init; } = true;

    public UpdateChannel UpdateChannel { get; init; } = UpdateChannel.Stable;
}

public sealed record UpdateStatus
{
    public bool IsEnabled { get; init; }

    public bool IsSupported { get; init; }

    public bool IsUpdateAvailable { get; init; }

    public UpdateChannel Channel { get; init; } = UpdateChannel.Stable;

    public string? CurrentVersion { get; init; }

    public string? AvailableVersion { get; init; }

    public DateTimeOffset? CheckedAt { get; init; }

    public string Message { get; init; } = "Update status unavailable";
}