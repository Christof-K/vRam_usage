namespace VRamUsage.Interfaces.HardwareMonitoring;

/// <summary>
/// Provider for physical system RAM metrics.
/// </summary>
public interface ISystemRamMetricsProvider
{
    ValueTask<(ulong UsedBytes, ulong TotalBytes, double LoadPercentage)> GetSystemRamMetricsAsync(CancellationToken cancellationToken = default);
}
