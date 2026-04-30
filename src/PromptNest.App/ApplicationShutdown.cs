using PromptNest.App.Diagnostics;
using PromptNest.Core.Abstractions;

namespace PromptNest.App;

public sealed class ApplicationShutdown
{
    private readonly IGlobalHotkeyService _globalHotkeyService;
    private readonly ITrayService _trayService;
    private readonly CrashHandler _crashHandler;

    public ApplicationShutdown(IGlobalHotkeyService globalHotkeyService, ITrayService trayService, CrashHandler crashHandler)
    {
        _globalHotkeyService = globalHotkeyService;
        _trayService = trayService;
        _crashHandler = crashHandler;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _globalHotkeyService.UnregisterAsync(cancellationToken);
        await _trayService.HideAsync(cancellationToken);
        _crashHandler.Dispose();
    }
}