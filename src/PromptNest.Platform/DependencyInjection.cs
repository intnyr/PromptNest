using Microsoft.Extensions.DependencyInjection;

using PromptNest.Core.Abstractions;
using PromptNest.Platform.Clipboard;
using PromptNest.Platform.Hotkeys;
using PromptNest.Platform.Notifications;
using PromptNest.Platform.Paths;
using PromptNest.Platform.Tray;

namespace PromptNest.Platform;

public static class DependencyInjection
{
    public static IServiceCollection AddPromptNestPlatform(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IPathProvider, PathProvider>();
        services.AddSingleton<IGlobalHotkeyService, Win32GlobalHotkeyService>();
        services.AddSingleton<IClipboardService, Win32ClipboardService>();
        services.AddSingleton<INotificationService, NoOpNotificationService>();
        services.AddSingleton<ITrayService, NoOpTrayService>();

        return services;
    }
}