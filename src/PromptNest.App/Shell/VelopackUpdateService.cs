using PromptNest.Core.Abstractions;
using PromptNest.Core.Models;

using Velopack;

namespace PromptNest.App.Shell;

public sealed class VelopackUpdateService : IUpdateService
{
    private const string FeedUrlEnvironmentVariable = "PROMPTNEST_UPDATE_FEED_URL";
    private const string AllowDevChecksEnvironmentVariable = "PROMPTNEST_ALLOW_DEV_UPDATE_CHECKS";

    private readonly IPathProvider _pathProvider;

    public VelopackUpdateService(IPathProvider pathProvider)
    {
        _pathProvider = pathProvider;
    }

    public async Task<OperationResult<UpdateStatus>> CheckForUpdatesAsync(
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!settings.UpdateChecksEnabled)
        {
            return OperationResultFactory.Success(new UpdateStatus
            {
                IsEnabled = false,
                IsSupported = true,
                Channel = settings.UpdateChannel,
                Message = "Update checks are disabled"
            });
        }

        string? feedUrl = Environment.GetEnvironmentVariable(FeedUrlEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(feedUrl))
        {
            return OperationResultFactory.Success(new UpdateStatus
            {
                IsEnabled = true,
                IsSupported = false,
                Channel = settings.UpdateChannel,
                Message = "Update feed is not configured for this build"
            });
        }

        try
        {
            string? explicitChannel = settings.UpdateChannel == UpdateChannel.Stable
                ? null
                : settings.UpdateChannel.ToString().ToLowerInvariant();

            var manager = new UpdateManager(
                feedUrl,
                new UpdateOptions { ExplicitChannel = explicitChannel });

            if (!manager.IsInstalled && !ShouldAllowDevChecks())
            {
                return OperationResultFactory.Success(new UpdateStatus
                {
                    IsEnabled = true,
                    IsSupported = false,
                    Channel = settings.UpdateChannel,
                    CurrentVersion = manager.CurrentVersion?.ToString(),
                    Message = _pathProvider.IsPackaged
                        ? "Update checks are unavailable for this installation"
                        : "Update checks are disabled in development builds"
                });
            }

            UpdateInfo? updateInfo = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
            string? currentVersion = manager.CurrentVersion?.ToString();
            string? availableVersion = updateInfo?.TargetFullRelease.Version.ToString();

            return OperationResultFactory.Success(new UpdateStatus
            {
                IsEnabled = true,
                IsSupported = true,
                IsUpdateAvailable = updateInfo is not null,
                Channel = settings.UpdateChannel,
                CurrentVersion = currentVersion,
                AvailableVersion = availableVersion,
                CheckedAt = DateTimeOffset.UtcNow,
                Message = updateInfo is null
                    ? "PromptNest is up to date"
                    : $"Update {availableVersion} is available"
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return OperationResultFactory.Failure<UpdateStatus>(
                "UpdateCheckFailed",
                "Update check failed. See application logs for diagnostics.");
        }
    }

    private static bool ShouldAllowDevChecks()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable(AllowDevChecksEnvironmentVariable),
            "true",
            StringComparison.OrdinalIgnoreCase);
    }
}