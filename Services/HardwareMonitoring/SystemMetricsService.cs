using Microsoft.Extensions.Logging;
using VRamUsage.Interfaces.HardwareMonitoring;
using VRamUsage.Models.HardwareMonitoring;

namespace VRamUsage.Services.HardwareMonitoring;

/// <summary>
/// Aggregates hardware monitoring providers into unified metrics snapshots.
/// </summary>
public sealed class SystemMetricsService : ISystemMetricsService
{
    private readonly IGpuMetricsProvider _gpuMetricsProvider;
    private readonly ISystemRamMetricsProvider _ramMetricsProvider;
    private readonly ICpuMetricsProvider _cpuMetricsProvider;
    private readonly ILogger<SystemMetricsService> _logger;

    public SystemMetricsService(
        IGpuMetricsProvider gpuMetricsProvider,
        ISystemRamMetricsProvider ramMetricsProvider,
        ICpuMetricsProvider cpuMetricsProvider,
        ILogger<SystemMetricsService> logger)
    {
        ArgumentNullException.ThrowIfNull(gpuMetricsProvider);
        ArgumentNullException.ThrowIfNull(ramMetricsProvider);
        ArgumentNullException.ThrowIfNull(cpuMetricsProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _gpuMetricsProvider = gpuMetricsProvider;
        _ramMetricsProvider = ramMetricsProvider;
        _cpuMetricsProvider = cpuMetricsProvider;
        _logger = logger;
    }

    public async ValueTask<HardwareMetrics> GetMetricsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var (gpuName, gpuLoad, dedicatedUsed, dedicatedTotal, sharedUsed, sharedTotal, totalCommitted) =
                await _gpuMetricsProvider.GetGpuMetricsAsync(cancellationToken);

            var (ramUsed, ramTotal, ramPercentage) =
                await _ramMetricsProvider.GetSystemRamMetricsAsync(cancellationToken);

            var cpuLoad = await _cpuMetricsProvider.GetCpuUtilizationPercentageAsync(cancellationToken);

            return new HardwareMetrics
            {
                GpuName = gpuName,
                GpuUtilizationPercentage = gpuLoad,
                DedicatedVramUsedBytes = dedicatedUsed,
                DedicatedVramTotalBytes = dedicatedTotal,
                SharedVramUsedBytes = sharedUsed,
                SharedVramTotalBytes = sharedTotal,
                TotalVramCommittedBytes = totalCommitted,
                SystemRamUsedBytes = ramUsed,
                SystemRamTotalBytes = ramTotal,
                SystemRamLoadPercentage = ramPercentage,
                CpuUtilizationPercentage = cpuLoad,
                Timestamp = DateTimeOffset.UtcNow
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while gathering hardware metrics");
            throw;
        }
    }

    public void Dispose()
    {
        _gpuMetricsProvider.Dispose();
    }
}
