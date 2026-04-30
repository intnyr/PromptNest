using System.Globalization;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using PromptNest.App.Diagnostics;
using PromptNest.App.Shell;
using PromptNest.App.ViewModels;
using PromptNest.App.Views;
using PromptNest.Core;
using PromptNest.Core.Abstractions;
using PromptNest.Data;
using PromptNest.Platform;

using Serilog;
using Serilog.Events;

namespace PromptNest.App;

public sealed class PromptNestApplication : IAsyncDisposable
{
    private readonly IHost _host;

    private PromptNestApplication(IHost host)
    {
        _host = host;
    }

    public IServiceProvider Services => _host.Services;

    public static PromptNestApplication Create(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var host = Host
            .CreateDefaultBuilder(args)
            .UseSerilog((_, loggerConfiguration) =>
            {
                var logDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PromptNest",
                    "logs");

                loggerConfiguration
                    .MinimumLevel.Information()
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                    .WriteTo.File(
                        Path.Combine(logDirectory, "app-.log"),
                        formatProvider: CultureInfo.InvariantCulture,
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 7);
            })
            .ConfigureServices(ConfigureServices)
            .Build();

        return new PromptNestApplication(host);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _host.StartAsync(cancellationToken);

        var startup = _host.Services.GetRequiredService<ApplicationStartup>();
        await startup.StartAsync(cancellationToken);
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var shell = _host.Services.GetRequiredService<IApplicationShell>();
        await shell.ActivateAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        var shutdown = _host.Services.GetRequiredService<ApplicationShutdown>();
        await shutdown.StopAsync(cancellationToken);

        await _host.StopAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else
        {
            _host.Dispose();
        }
    }

    private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddPromptNestCore()
            .AddPromptNestData()
            .AddPromptNestPlatform();

        services.AddSingleton<MainViewModel>();
        services.AddTransient<EditorViewModel>();
        services.AddSingleton<PaletteViewModel>();
        services.AddSingleton<MainWindow>();
        services.AddSingleton<PaletteWindow>();
        services.AddSingleton<WinUiTrayService>();
        services.AddSingleton<ITrayService>(services => services.GetRequiredService<WinUiTrayService>());
        services.AddSingleton<INotificationService>(services => services.GetRequiredService<WinUiTrayService>());
        services.AddSingleton<IJumpListService, WinUiJumpListService>();
        services.AddSingleton<IUpdateService, VelopackUpdateService>();
        services.AddSingleton<IApplicationShell, WinUiApplicationShell>();
        services.AddSingleton<CrashHandler>();
        services.AddSingleton<ApplicationStartup>();
        services.AddSingleton<ApplicationShutdown>();
    }
}