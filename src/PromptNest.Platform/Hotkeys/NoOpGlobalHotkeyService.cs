using PromptNest.Core.Abstractions;

namespace PromptNest.Platform.Hotkeys;

public sealed class NoOpGlobalHotkeyService : IGlobalHotkeyService
{
    public event EventHandler? HotkeyPressed
    {
        add { }
        remove { }
    }

    public bool IsRegistered { get; private set; }

    public string? RegistrationError { get; private set; }

    public Task RegisterAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IsRegistered = false;
        RegistrationError = "Global hotkeys are not available on this platform.";
        return Task.CompletedTask;
    }

    public Task UnregisterAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IsRegistered = false;
        return Task.CompletedTask;
    }
}