using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VRamUsage.Configuration.HardwareMonitoring;
using VRamUsage.Interfaces.HardwareMonitoring;
using VRamUsage.Models.HardwareMonitoring;

namespace VRamUsage.Controllers.HardwareMonitoring;

/// <summary>
/// ViewModel managing presentation state and polling loop for the hardware overlay window.
/// </summary>
public sealed class OverlayViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ISystemMetricsService _metricsService;
    private readonly IOptions<OverlayOptions> _options;
    private readonly ILogger<OverlayViewModel> _logger;
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private string _gpuName = "AMD Radeon(TM) 890M";
    private string _gpuLoadText = "0%";
    private double _gpuLoadValue;
    private string _vramSummaryText = "0.0 / 0.0 GB";
    private double _vramPercentage;
    private string _vramSharedText = "Shared: 0.0 / 0.0 GB";
    private string _vramDedicatedText = "Dedicated: 0 / 0 MB";
    private string _ramSummaryText = "0.0 / 0.0 GB";
    private double _ramPercentage;
    private string _cpuLoadText = "0%";
    private double _cpuLoadValue;
    private string _lastUpdatedText = "--:--:--";
    private double _opacity = 0.88;
    private bool _isLocked;
    private bool _isAlwaysOnTop = true;
    private int _refreshIntervalMs = 1000;
    private bool _isDisposed;

    public event PropertyChangedEventHandler? PropertyChanged;

    public OverlayViewModel(
        ISystemMetricsService metricsService,
        IOptions<OverlayOptions> options,
        ILogger<OverlayViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(metricsService);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _metricsService = metricsService;
        _options = options;
        _logger = logger;

        _opacity = options.Value.Opacity;
        _isLocked = options.Value.IsLocked;
        _isAlwaysOnTop = options.Value.IsAlwaysOnTop;
        _refreshIntervalMs = options.Value.RefreshIntervalMs;
    }

    public string GpuName
    {
        get => _gpuName;
        private set => SetField(ref _gpuName, value);
    }

    public string GpuLoadText
    {
        get => _gpuLoadText;
        private set => SetField(ref _gpuLoadText, value);
    }

    public double GpuLoadValue
    {
        get => _gpuLoadValue;
        private set => SetField(ref _gpuLoadValue, value);
    }

    public string VramSummaryText
    {
        get => _vramSummaryText;
        private set => SetField(ref _vramSummaryText, value);
    }

    public double VramPercentage
    {
        get => _vramPercentage;
        private set => SetField(ref _vramPercentage, value);
    }

    public string VramSharedText
    {
        get => _vramSharedText;
        private set => SetField(ref _vramSharedText, value);
    }

    public string VramDedicatedText
    {
        get => _vramDedicatedText;
        private set => SetField(ref _vramDedicatedText, value);
    }

    public string RamSummaryText
    {
        get => _ramSummaryText;
        private set => SetField(ref _ramSummaryText, value);
    }

    public double RamPercentage
    {
        get => _ramPercentage;
        private set => SetField(ref _ramPercentage, value);
    }

    public string CpuLoadText
    {
        get => _cpuLoadText;
        private set => SetField(ref _cpuLoadText, value);
    }

    public double CpuLoadValue
    {
        get => _cpuLoadValue;
        private set => SetField(ref _cpuLoadValue, value);
    }

    public string LastUpdatedText
    {
        get => _lastUpdatedText;
        private set => SetField(ref _lastUpdatedText, value);
    }

    public double Opacity
    {
        get => _opacity;
        set => SetField(ref _opacity, Math.Clamp(value, 0.1, 1.0));
    }

    public bool IsLocked
    {
        get => _isLocked;
        set => SetField(ref _isLocked, value);
    }

    public bool IsAlwaysOnTop
    {
        get => _isAlwaysOnTop;
        set => SetField(ref _isAlwaysOnTop, value);
    }

    public int RefreshIntervalMs
    {
        get => _refreshIntervalMs;
        set => SetField(ref _refreshIntervalMs, Math.Clamp(value, 200, 10000));
    }

    public void StartMonitoring()
    {
        _ = Task.Run(() => MonitorLoopAsync(_cancellationTokenSource.Token));
    }

    private async Task MonitorLoopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Hardware monitor loop started with refresh interval {IntervalMs}ms", _refreshIntervalMs);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var metrics = await _metricsService.GetMetricsAsync(cancellationToken);
                UpdateProperties(metrics);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect hardware metrics in update loop");
            }

            try
            {
                await Task.Delay(_refreshIntervalMs, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Hardware monitor loop terminated");
    }

    private void UpdateProperties(HardwareMetrics metrics)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.HasShutdownStarted)
        {
            return;
        }

        dispatcher.InvokeAsync(() =>
        {
            GpuName = metrics.GpuName;
            GpuLoadValue = metrics.GpuUtilizationPercentage;
            GpuLoadText = $"{metrics.GpuUtilizationPercentage:F0}%";

            VramPercentage = metrics.TotalVramPercentage;
            VramSummaryText = $"{metrics.TotalVramUsedGb:F1} / {metrics.TotalVramBudgetGb:F1} GB ({metrics.TotalVramPercentage:F0}%)";
            VramSharedText = $"Shared: {metrics.SharedVramUsedGb:F1} / {metrics.SharedVramTotalGb:F1} GB";
            VramDedicatedText = $"Dedicated: {metrics.DedicatedVramUsedMb:F0} / {metrics.DedicatedVramTotalMb:F0} MB";

            RamPercentage = metrics.SystemRamLoadPercentage;
            RamSummaryText = $"{metrics.SystemRamUsedGb:F1} / {metrics.SystemRamTotalGb:F1} GB ({metrics.SystemRamLoadPercentage:F0}%)";

            CpuLoadValue = metrics.CpuUtilizationPercentage;
            CpuLoadText = $"{metrics.CpuUtilizationPercentage:F0}%";

            LastUpdatedText = metrics.Timestamp.ToLocalTime().ToString("HH:mm:ss");
        });
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
        _isDisposed = true;
    }
}
