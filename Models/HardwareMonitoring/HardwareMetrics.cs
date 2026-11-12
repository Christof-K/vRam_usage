namespace VRamUsage.Models.HardwareMonitoring;

/// <summary>
/// Snapshot of hardware utilization and memory metrics.
/// </summary>
public sealed record HardwareMetrics
{
    public required string GpuName { get; init; }

    public required double GpuUtilizationPercentage { get; init; }

    public required ulong? DedicatedVramUsedBytes { get; init; }

    public required ulong DedicatedVramTotalBytes { get; init; }

    public required ulong? SharedVramUsedBytes { get; init; }

    public required ulong SharedVramTotalBytes { get; init; }

    public required ulong? TotalVramCommittedBytes { get; init; }

    public required ulong SystemRamUsedBytes { get; init; }

    public required ulong SystemRamTotalBytes { get; init; }

    public required double SystemRamLoadPercentage { get; init; }

    public required double CpuUtilizationPercentage { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    // Computed display values
    public double? DedicatedVramUsedMb => DedicatedVramUsedBytes / (1024.0 * 1024.0);

    public double DedicatedVramTotalMb => DedicatedVramTotalBytes / (1024.0 * 1024.0);

    public double? SharedVramUsedGb => SharedVramUsedBytes / (1024.0 * 1024.0 * 1024.0);

    public double SharedVramTotalGb => SharedVramTotalBytes / (1024.0 * 1024.0 * 1024.0);

    public double? TotalVramUsedGb => DedicatedVramUsedBytes.HasValue && SharedVramUsedBytes.HasValue
        ? (DedicatedVramUsedBytes.Value + SharedVramUsedBytes.Value) / (1024.0 * 1024.0 * 1024.0)
        : null;

    public double TotalVramBudgetGb => (DedicatedVramTotalBytes + SharedVramTotalBytes) / (1024.0 * 1024.0 * 1024.0);

    public double? TotalVramPercentage => TotalVramUsedGb.HasValue && TotalVramBudgetGb > 0.0
        ? (TotalVramUsedGb.Value / TotalVramBudgetGb) * 100.0
        : null;

    public double SystemRamUsedGb => SystemRamUsedBytes / (1024.0 * 1024.0 * 1024.0);

    public double SystemRamTotalGb => SystemRamTotalBytes / (1024.0 * 1024.0 * 1024.0);
}
