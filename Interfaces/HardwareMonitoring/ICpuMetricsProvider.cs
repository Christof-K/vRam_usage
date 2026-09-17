namespace VRamUsage.Interfaces.HardwareMonitoring;

/// <summary>
/// Provider for processor utilization metrics.
/// </summary>
public interface ICpuMetricsProvider
{
    ValueTask<double> GetCpuUtilizationPercentageAsync(CancellationToken cancellationToken = default);
}
