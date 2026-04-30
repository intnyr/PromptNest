using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

using PromptNest.App.DeepLinks;
using PromptNest.App.ViewModels;
using PromptNest.App.Views;

using Velopack;

namespace PromptNest.App;

public partial class App : Application
{
    private readonly PromptNestApplication _application;
    private readonly string[] _launchArgs;
    private Window? _mainWindow;
    private AppInstance? _mainInstance;
    private DispatcherQueue? _dispatcherQueue;

    public App()
    {
        if (string.Equals(
            Environment.GetEnvironmentVariable("PROMPTNEST_RUN_VELOPACK_HOOKS"),
            "true",
            StringComparison.OrdinalIgnoreCase))
        {
            VelopackApp.Build().Run();
        }

        InitializeComponent();
        _launchArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();
        _application = PromptNestApplication.Create(_launchArgs);
    }

    public IServiceProvider Services => _application.Services;

    public Window? MainWindow => _mainWindow;

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        try
        {
            AppActivationArguments activationArguments = AppInstance.GetCurrent().GetActivatedEventArgs();
            _mainInstance = AppInstance.FindOrRegisterForKey("PromptNest.Main");
            if (!_mainInstance.IsCurrent)
            {
                await _mainInstance.RedirectActivationToAsync(activationArguments);
                Environment.Exit(0);
                return;
            }

            _mainInstance.Activated += OnAppInstanceActivated;
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            await _application.StartAsync().ConfigureAwait(true);

            _mainWindow = _application.Services.GetRequiredService<MainWindow>();
            _mainWindow.Closed += OnMainWindowClosed;
            _mainWindow.Activate();

            await _application.RunAsync().ConfigureAwait(true);
            await RouteActivationAsync(activationArguments, _launchArgs).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            string logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PromptNest",
                "logs",
                "launch-error.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.AppendAllText(logPath, ex.ToString());
            throw;
        }
    }

    private void OnAppInstanceActivated(object? sender, AppActivationArguments args)
    {
        ArgumentNullException.ThrowIfNull(args);
        _dispatcherQueue?.TryEnqueue(async () =>
        {
            ActivateMainWindow();
            await RouteActivationAsync(args, []).ConfigureAwait(true);
        });
    }

    private async Task RouteActivationAsync(AppActivationArguments activationArguments, IReadOnlyList<string> fallbackArgs)
    {
        DeepLinkRequest? request = ExtractDeepLink(activationArguments) ?? DeepLinkParser.TryParseFromArguments(fallbackArgs);
        if (request is not null)
        {
            MainViewModel viewModel = _application.Services.GetRequiredService<MainViewModel>();
            await viewModel.ApplyDeepLinkAsync(request, CancellationToken.None).ConfigureAwait(true);
        }

        ActivateMainWindow();
    }

    private static DeepLinkRequest? ExtractDeepLink(AppActivationArguments activationArguments)
    {
        if (activationArguments.Kind == ExtendedActivationKind.Protocol
            && activationArguments.Data is Windows.ApplicationModel.Activation.IProtocolActivatedEventArgs protocolArgs
            && DeepLinkParser.TryParse(protocolArgs.Uri?.AbsoluteUri, out DeepLinkRequest protocolRequest))
        {
            return protocolRequest;
        }

        if (activationArguments.Kind == ExtendedActivationKind.Launch
            && activationArguments.Data is Windows.ApplicationModel.Activation.ILaunchActivatedEventArgs launchArgs)
        {
            string[] args = launchArgs.Arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return DeepLinkParser.TryParseFromArguments(args);
        }

        return null;
    }

    private void ActivateMainWindow()
    {
        _mainWindow ??= _application.Services.GetRequiredService<MainWindow>();
        _mainWindow.Activate();
    }

    private async void OnMainWindowClosed(object sender, WindowEventArgs args)
    {
        try
        {
            await _application.StopAsync().ConfigureAwait(true);
        }
        finally
        {
            await _application.DisposeAsync().ConfigureAwait(true);
        }
    }
}