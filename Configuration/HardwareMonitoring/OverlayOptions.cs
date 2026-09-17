using System.ComponentModel.DataAnnotations;

namespace VRamUsage.Configuration.HardwareMonitoring;

/// <summary>
/// Configuration options for the overlay window and hardware monitoring service.
/// </summary>
public sealed class OverlayOptions
{
    public static readonly string SectionName = "Overlay";

    [Range(200, 10000)]
    public int RefreshIntervalMs { get; set; } = 1000;

    [Range(0.1, 1.0)]
    public double Opacity { get; set; } = 0.88;

    [Range(0.0, 10000.0)]
    public double InitialLeft { get; set; } = 16.0;

    [Range(0.0, 10000.0)]
    public double InitialTop { get; set; } = 16.0;

    public bool IsLocked { get; set; } = false;

    public bool IsAlwaysOnTop { get; set; } = true;

    public uint PreferredGpuVendorId { get; set; } = 0x1002; // AMD
}
