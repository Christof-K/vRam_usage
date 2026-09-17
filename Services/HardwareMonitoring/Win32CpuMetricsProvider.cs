using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using VRamUsage.Interfaces.HardwareMonitoring;

namespace VRamUsage.Services.HardwareMonitoring;

/// <summary>
/// Native Win32 provider for CPU utilization metrics using GetSystemTimes.
/// </summary>
public sealed partial class Win32CpuMetricsProvider : ICpuMetricsProvider
{
    private readonly ILogger<Win32CpuMetricsProvider> _logger;
    private readonly Lock _lock = new();

    private ulong _previousIdleTime;
    private ulong _previousKernelTime;
    private ulong _previousUserTime;
    private bool _isInitialized;
    private double _lastCalculatedLoad;

    public Win32CpuMetricsProvider(ILogger<Win32CpuMetricsProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public uint dwLowDateTime;
        public uint dwHighDateTime;

        public ulong ToUInt64() => ((ulong)dwHighDateTime << 32) | dwLowDateTime;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetSystemTimes(out FILETIME lpIdleTime, out FILETIME lpKernelTime, out FILETIME lpUserTime);

    public ValueTask<double> GetCpuUtilizationPercentageAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!GetSystemTimes(out var idleTime, out var kernelTime, out var userTime))
        {
            var errorCode = Marshal.GetLastPInvokeError();
            _logger.LogWarning("Failed to query GetSystemTimes. ErrorCode: {ErrorCode}", errorCode);
            return ValueTask.FromResult(_lastCalculatedLoad);
        }

        var currentIdle = idleTime.ToUInt64();
        var currentKernel = kernelTime.ToUInt64();
        var currentUser = userTime.ToUInt64();

        lock (_lock)
        {
            if (!_isInitialized)
            {
                _previousIdleTime = currentIdle;
                _previousKernelTime = currentKernel;
                _previousUserTime = currentUser;
                _isInitialized = true;
                return ValueTask.FromResult(0.0);
            }

            var deltaIdle = currentIdle - _previousIdleTime;
            var deltaKernel = currentKernel - _previousKernelTime;
            var deltaUser = currentUser - _previousUserTime;

            _previousIdleTime = currentIdle;
            _previousKernelTime = currentKernel;
            _previousUserTime = currentUser;

            // In Windows, kernel time includes idle time
            var totalSystemTime = deltaKernel + deltaUser;
            if (totalSystemTime == 0)
            {
                return ValueTask.FromResult(_lastCalculatedLoad);
            }

            var idleFraction = (double)deltaIdle / totalSystemTime;
            var load = Math.Clamp((1.0 - idleFraction) * 100.0, 0.0, 100.0);
            _lastCalculatedLoad = load;

            return ValueTask.FromResult(load);
        }
    }
}
