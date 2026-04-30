namespace PromptNest.Core.Abstractions;

public interface IGlobalHotkeyService
{
    event EventHandler? HotkeyPressed;

    bool IsRegistered { get; }

    string? RegistrationError { get; }

    Task RegisterAsync(CancellationToken cancellationToken);

    Task UnregisterAsync(CancellationToken cancellationToken);
}

public enum TrayCommandKind
{
    OpenLibrary,
    NewPrompt,
    Recent,
    Favorites,
    Settings,
    Quit,
    ToggleMainWindow,
    OpenPalette
}

public sealed record TrayCommand(TrayCommandKind Kind, string? PromptId = null);