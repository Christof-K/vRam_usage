namespace VRamUsage.Interfaces.HardwareMonitoring;

/// <summary>
/// Provider for GPU utilization and VRAM metrics.
/// </summary>
public interface IGpuMetricsProvider : IDisposable
{
    ValueTask<(string GpuName, double LoadPercentage, ulong? DedicatedUsed, ulong DedicatedTotal, ulong? SharedUsed, ulong SharedTotal, ulong? TotalCommitted)> GetGpuMetricsAsync(CancellationToken cancellationToken = default);
}
