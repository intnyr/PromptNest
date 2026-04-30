using System.Windows.Input;

using H.NotifyIcon;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

using PromptNest.Core.Abstractions;

namespace PromptNest.App.Shell;

public sealed class WinUiTrayService : ITrayService, INotificationService, IDisposable
{
    private TaskbarIcon? taskbarIcon;
    private bool disposed;

    public event EventHandler<TrayCommand>? CommandInvoked;

    public IReadOnlyList<string> MenuCommands { get; } =
    [
        "Open Library",
        "New Prompt",
        "Recent 5",
        "Favorites",
        "Settings",
        "Quit"
    ];

    public Task ShowAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (taskbarIcon is not null)
        {
            return Task.CompletedTask;
        }

        taskbarIcon = new TaskbarIcon
        {
            ToolTipText = "PromptNest",
            IconSource = new GeneratedIconSource
            {
                Text = "PN",
                Background = (Brush)Application.Current.Resources["PromptNestAccentBrush"],
                Foreground = (Brush)Application.Current.Resources["PromptNestIconSelectedBrush"]
            },
            ContextFlyout = BuildMenu(),
            LeftClickCommand = new TrayRelayCommand(() => Raise(TrayCommandKind.ToggleMainWindow)),
            DoubleClickCommand = new TrayRelayCommand(() => Raise(TrayCommandKind.OpenPalette))
        };
        taskbarIcon.ForceCreate();
        return Task.CompletedTask;
    }

    public Task HideAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        taskbarIcon?.Dispose();
        taskbarIcon = null;
        return Task.CompletedTask;
    }

    public Task ShowCopiedAsync(string promptTitle, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(promptTitle);
        cancellationToken.ThrowIfCancellationRequested();
        taskbarIcon?.ShowNotification("PromptNest", $"Copied {promptTitle}", H.NotifyIcon.Core.NotificationIcon.Info);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        taskbarIcon?.Dispose();
        disposed = true;
    }

    private MenuFlyout BuildMenu()
    {
        var menu = new MenuFlyout();
        AddItem(menu, "Open Library", TrayCommandKind.OpenLibrary);
        AddItem(menu, "New Prompt", TrayCommandKind.NewPrompt);
        menu.Items.Add(new MenuFlyoutSeparator());
        AddItem(menu, "Recent 5", TrayCommandKind.Recent);
        AddItem(menu, "Favorites", TrayCommandKind.Favorites);
        menu.Items.Add(new MenuFlyoutSeparator());
        AddItem(menu, "Settings", TrayCommandKind.Settings);
        AddItem(menu, "Quit", TrayCommandKind.Quit);
        return menu;
    }

    private void AddItem(MenuFlyout menu, string text, TrayCommandKind command)
    {
        var item = new MenuFlyoutItem { Text = text };
        item.Click += (_, _) => Raise(command);
        menu.Items.Add(item);
    }

    private void Raise(TrayCommandKind command)
    {
        CommandInvoked?.Invoke(this, new TrayCommand(command));
    }

    private sealed class TrayRelayCommand : ICommand
    {
        private readonly Action action;

        public TrayRelayCommand(Action action)
        {
            this.action = action;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
            action();
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}