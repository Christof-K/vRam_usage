using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using VRamUsage.Interfaces.HardwareMonitoring;

namespace VRamUsage.Services.HardwareMonitoring;

/// <summary>
/// Native Win32 provider for system RAM metrics via GlobalMemoryStatusEx.
/// </summary>
public sealed partial class Win32SystemRamMetricsProvider : ISystemRamMetricsProvider
{
    private readonly ILogger<Win32SystemRamMetricsProvider> _logger;

    public Win32SystemRamMetricsProvider(ILogger<Win32SystemRamMetricsProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    public ValueTask<(ulong UsedBytes, ulong TotalBytes, double LoadPercentage)> GetSystemRamMetricsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var memStatus = new MEMORYSTATUSEX
        {
            dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>()
        };

        if (!GlobalMemoryStatusEx(ref memStatus))
        {
            var errorCode = Marshal.GetLastPInvokeError();
            _logger.LogWarning("Failed to query GlobalMemoryStatusEx. ErrorCode: {ErrorCode}", errorCode);
            return ValueTask.FromResult<(ulong, ulong, double)>((0UL, 0UL, 0.0));
        }

        var total = memStatus.ullTotalPhys;
        var avail = memStatus.ullAvailPhys;
        var used = total > avail ? total - avail : 0UL;
        var percentage = total > 0 ? ((double)used / total) * 100.0 : 0.0;

        return ValueTask.FromResult((used, total, percentage));
    }
}
