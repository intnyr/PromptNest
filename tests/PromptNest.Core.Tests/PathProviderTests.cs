using FluentAssertions;

using PromptNest.Platform.Paths;

namespace PromptNest.Core.Tests;

public sealed class PathProviderTests
{
    [Fact]
    public void ConstructorUsesLocalApplicationDataPromptNestFolder()
    {
        var provider = new PathProvider("C:\\Users\\Person\\AppData\\Local", isPackaged: false);

        provider.DataDirectory.Should().Be("C:\\Users\\Person\\AppData\\Local\\PromptNest");
        provider.DatabasePath.Should().EndWith("PromptNest\\library.db");
        provider.LogsDirectory.Should().EndWith("PromptNest\\logs");
        provider.BackupsDirectory.Should().EndWith("PromptNest\\Backups");
        provider.SettingsPath.Should().EndWith("PromptNest\\settings.json");
        provider.UpdateCacheDirectory.Should().EndWith("PromptNest\\Updates");
        provider.IsPackaged.Should().BeFalse();
    }
}