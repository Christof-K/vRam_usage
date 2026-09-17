using VRamUsage.Models.HardwareMonitoring;

namespace VRamUsage.Interfaces.HardwareMonitoring;

/// <summary>
/// Aggregating service providing overall hardware monitoring snapshots.
/// </summary>
public interface ISystemMetricsService : IDisposable
{
    ValueTask<HardwareMetrics> GetMetricsAsync(CancellationToken cancellationToken = default);
}
