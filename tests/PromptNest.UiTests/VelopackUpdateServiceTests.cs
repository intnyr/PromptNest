using FluentAssertions;

using PromptNest.App.Shell;
using PromptNest.Core.Abstractions;
using PromptNest.Core.Models;

namespace PromptNest.UiTests;

public sealed class VelopackUpdateServiceTests
{
    [Fact]
    public async Task CheckForUpdatesNoFeedConfiguredReturnsSafeNoOp()
    {
        using var environment = new EnvironmentVariableScope("PROMPTNEST_UPDATE_FEED_URL", null);
        var service = new VelopackUpdateService(new FakePathProvider(isPackaged: false));

        OperationResult<UpdateStatus> result = await service.CheckForUpdatesAsync(new AppSettings(), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.IsSupported.Should().BeFalse();
        result.Value.Message.Should().Contain("not configured");
    }

    [Fact]
    public async Task CheckForUpdatesDisabledDoesNotRequireFeed()
    {
        using var environment = new EnvironmentVariableScope("PROMPTNEST_UPDATE_FEED_URL", "https://updates.example.invalid");
        var service = new VelopackUpdateService(new FakePathProvider(isPackaged: true));

        OperationResult<UpdateStatus> result = await service.CheckForUpdatesAsync(
            new AppSettings { UpdateChecksEnabled = false, UpdateChannel = UpdateChannel.Beta },
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.IsEnabled.Should().BeFalse();
        result.Value.Channel.Should().Be(UpdateChannel.Beta);
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _previousValue;

        public EnvironmentVariableScope(string name, string? value)
        {
            _name = name;
            _previousValue = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(_name, _previousValue);
        }
    }

    private sealed class FakePathProvider : IPathProvider
    {
        public FakePathProvider(bool isPackaged)
        {
            IsPackaged = isPackaged;
        }

        public string DataDirectory => string.Empty;

        public string DatabasePath => string.Empty;

        public string LogsDirectory => string.Empty;

        public string BackupsDirectory => string.Empty;

        public string SettingsPath => string.Empty;

        public string UpdateCacheDirectory => string.Empty;

        public bool IsPackaged { get; }
    }
}