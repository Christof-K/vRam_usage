using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VRamUsage.Configuration.HardwareMonitoring;
using VRamUsage.Controllers.HardwareMonitoring;
using VRamUsage.Interfaces.HardwareMonitoring;
using VRamUsage.Services.HardwareMonitoring;
using VRamUsage.Views.HardwareMonitoring;

namespace VRamUsage;

/// <summary>
/// Main application entry point managing dependency injection and host lifecycle.
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;
    private TrayIconController? _trayIconController;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var hostBuilder = Host.CreateDefaultBuilder(e.Args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(Directory.GetCurrentDirectory())
                      .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                      .AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                // Configuration Options with startup validation
                services.AddOptions<OverlayOptions>()
                        .Bind(context.Configuration.GetSection(OverlayOptions.SectionName))
                        .ValidateDataAnnotations()
                        .ValidateOnStart();

                // Hardware Providers & Services
                services.AddSingleton<IGpuMetricsProvider, DxgiPdhGpuMetricsProvider>();
                services.AddSingleton<ISystemRamMetricsProvider, Win32SystemRamMetricsProvider>();
                services.AddSingleton<ICpuMetricsProvider, Win32CpuMetricsProvider>();
                services.AddSingleton<ISystemMetricsService, SystemMetricsService>();

                // ViewModels & Controllers
                services.AddSingleton<OverlayViewModel>();
                services.AddSingleton<OverlayWindow>();
                services.AddSingleton<Window>(sp => sp.GetRequiredService<OverlayWindow>());
                services.AddSingleton<TrayIconController>();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            });

        _host = hostBuilder.Build();
        await _host.StartAsync();

        var logger = _host.Services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("VRamUsage application starting");

        // Initialize tray icon controller and overlay window
        _trayIconController = _host.Services.GetRequiredService<TrayIconController>();
        var overlayWindow = _host.Services.GetRequiredService<OverlayWindow>();
        overlayWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _trayIconController?.Dispose();

        if (_host != null)
        {
            var logger = _host.Services.GetService<ILogger<App>>();
            logger?.LogInformation("VRamUsage application shutting down");

            await _host.StopAsync(TimeSpan.FromSeconds(3));
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
