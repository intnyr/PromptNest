using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;

using PromptNest.App.Diagnostics;
using PromptNest.App.ViewModels;
using PromptNest.Core.Abstractions;
using PromptNest.Core.Models;

namespace PromptNest.App;

public sealed class ApplicationStartup
{
    private static readonly TimeSpan UpdateCheckInterval = TimeSpan.FromHours(6);

    private readonly IMigrationRunner _migrationRunner;
    private readonly IGlobalHotkeyService _globalHotkeyService;
    private readonly ITrayService _trayService;
    private readonly IJumpListService _jumpListService;
    private readonly IPromptService _promptService;
    private readonly CrashHandler _crashHandler;
    private readonly MainViewModel _mainViewModel;
    private readonly IServiceProvider _services;
    private DispatcherQueue? dispatcherQueue;

    public ApplicationStartup(
        IMigrationRunner migrationRunner,
        IGlobalHotkeyService globalHotkeyService,
        ITrayService trayService,
        IJumpListService jumpListService,
        IPromptService promptService,
        CrashHandler crashHandler,
        MainViewModel mainViewModel,
        IServiceProvider services)
    {
        _migrationRunner = migrationRunner;
        _globalHotkeyService = globalHotkeyService;
        _trayService = trayService;
        _jumpListService = jumpListService;
        _promptService = promptService;
        _crashHandler = crashHandler;
        _mainViewModel = mainViewModel;
        _services = services;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _crashHandler.Register();
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        await _migrationRunner.MigrateAsync(cancellationToken);
        await _mainViewModel.LoadAsync(cancellationToken);
        _globalHotkeyService.HotkeyPressed += OnHotkeyPressed;
        _trayService.CommandInvoked += OnTrayCommandInvoked;
        await _globalHotkeyService.RegisterAsync(cancellationToken);
        await _trayService.ShowAsync(cancellationToken);
        await RefreshJumpListAsync(cancellationToken);
        _ = RunUpdateChecksAsync(cancellationToken);
    }

    private void OnHotkeyPressed(object? sender, EventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        dispatcherQueue?.TryEnqueue(() => _services.GetRequiredService<Views.PaletteWindow>().Toggle());
    }

    private void OnTrayCommandInvoked(object? sender, TrayCommand command)
    {
        dispatcherQueue?.TryEnqueue(async () =>
        {
            Views.MainWindow mainWindow = _services.GetRequiredService<Views.MainWindow>();
            switch (command.Kind)
            {
                case TrayCommandKind.OpenLibrary:
                case TrayCommandKind.ToggleMainWindow:
                    mainWindow.Activate();
                    break;
                case TrayCommandKind.NewPrompt:
                    mainWindow.Activate();
                    _mainViewModel.InvokeToolbarCommandCommand.Execute(LibraryToolbarCommandKind.NewPrompt);
                    break;
                case TrayCommandKind.Settings:
                    mainWindow.ShowSettingsPane();
                    break;
                case TrayCommandKind.OpenPalette:
                    _services.GetRequiredService<Views.PaletteWindow>().Toggle();
                    break;
                case TrayCommandKind.Favorites:
                    mainWindow.Activate();
                    _mainViewModel.SelectNavigationCommand.Execute("starred");
                    break;
                case TrayCommandKind.Recent:
                    mainWindow.Activate();
                    _mainViewModel.SelectNavigationCommand.Execute("recent");
                    break;
                case TrayCommandKind.Quit:
                    mainWindow.Close();
                    break;
            }

            await RefreshJumpListAsync(CancellationToken.None);
        });
    }

    private async Task RefreshJumpListAsync(CancellationToken cancellationToken)
    {
        PagedResult<Prompt> favorites = await _promptService.ListAsync(
            new PromptQuery { IsFavorite = true, Take = 5, SortBy = PromptSortBy.UpdatedAt, SortDescending = true },
            cancellationToken);
        await _jumpListService.RefreshFavoritesAsync(favorites.Items, cancellationToken);
    }

    private async Task RunUpdateChecksAsync(CancellationToken cancellationToken)
    {
        await CheckForUpdatesOnDispatcherAsync(cancellationToken);

        using var timer = new PeriodicTimer(UpdateCheckInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            await CheckForUpdatesOnDispatcherAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private Task CheckForUpdatesOnDispatcherAsync(CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool enqueued = dispatcherQueue?.TryEnqueue(async () =>
        {
            try
            {
                await _mainViewModel.CheckForUpdatesAsync(cancellationToken);
                completion.SetResult();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                completion.SetCanceled(cancellationToken);
            }
            catch (Exception)
            {
                completion.SetResult();
            }
        }) ?? false;

        if (!enqueued)
        {
            completion.SetResult();
        }

        return completion.Task;
    }
}