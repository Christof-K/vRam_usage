using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VRamUsage.Configuration.HardwareMonitoring;
using VRamUsage.Interfaces.HardwareMonitoring;

namespace VRamUsage.Services.HardwareMonitoring;

/// <summary>
/// GPU metrics provider utilizing DXGI for hardware topology and Windows PDH for real-time engine/memory metrics.
/// </summary>
public sealed class DxgiPdhGpuMetricsProvider : IGpuMetricsProvider
{
    private static readonly Guid IidIdxgiFactory1 = new("770aae78-f26f-4dba-a829-253c83d1b387");
    private const uint PdhFmtDouble = 0x00000200;
    private const uint PdhMoreData = 0x800007D2;
    private const uint ErrorSuccess = 0x00000000;

    private readonly ILogger<DxgiPdhGpuMetricsProvider> _logger;
    private readonly IOptions<OverlayOptions> _options;
    private readonly Lock _lock = new();

    private string _gpuName = "AMD Radeon Graphics";
    private string _luidString = string.Empty;
    private ulong _dedicatedTotalBytes;
    private ulong _sharedTotalBytes;

    private nint _hPdhQuery = nint.Zero;
    private nint _hCounterDedicated = nint.Zero;
    private nint _hCounterShared = nint.Zero;
    private nint _hCounterCommit = nint.Zero;
    private readonly List<nint> _engineCounters = [];

    private DateTimeOffset _lastEngineCounterRefresh = DateTimeOffset.MinValue;
    private bool _isDisposed;

    public DxgiPdhGpuMetricsProvider(
        ILogger<DxgiPdhGpuMetricsProvider> logger,
        IOptions<OverlayOptions> options)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        _logger = logger;
        _options = options;

        InitializeGpuTopology();
        InitializePdhCounters();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DXGI_ADAPTER_DESC1
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Description;
        public uint VendorId;
        public uint DeviceId;
        public uint SubSysId;
        public uint Revision;
        public nuint DedicatedVideoMemory;
        public nuint DedicatedSystemMemory;
        public nuint SharedSystemMemory;
        public LUID AdapterLuid;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PDH_FMT_COUNTERVALUE_DOUBLE
    {
        public uint CStatus;
        public uint Dummy;
        public double DoubleValue;
    }

    [DllImport("dxgi.dll", ExactSpelling = true)]
    private static extern int CreateDXGIFactory1(in Guid riid, out nint ppFactory);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint PdhOpenQueryW(string? szDataSource, nuint dwUserData, out nint phQuery);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint PdhAddEnglishCounterW(nint hQuery, string szFullCounterPath, nuint dwUserData, out nint phCounter);

    [DllImport("pdh.dll", ExactSpelling = true)]
    private static extern uint PdhRemoveCounter(nint hCounter);

    [DllImport("pdh.dll", ExactSpelling = true)]
    private static extern uint PdhCollectQueryData(nint hQuery);

    [DllImport("pdh.dll", ExactSpelling = true)]
    private static extern uint PdhCloseQuery(nint hQuery);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint PdhExpandWildCardPathW(string? szDataSource, string szWildCardPath, [Out] char[]? mszExpandedPathList, ref uint pcchPathListLength, uint dwFlags);

    [DllImport("pdh.dll", ExactSpelling = true)]
    private static extern uint PdhGetFormattedCounterValue(nint hCounter, uint dwFormat, nint lpdwType, out PDH_FMT_COUNTERVALUE_DOUBLE pValue);

    private void InitializeGpuTopology()
    {
        var hr = CreateDXGIFactory1(in IidIdxgiFactory1, out var pFactory);
        if (hr != 0 || pFactory == nint.Zero)
        {
            _logger.LogError("CreateDXGIFactory1 failed with HRESULT: 0x{Hr:X8}", hr);
            return;
        }

        try
        {
            var factoryVtbl = Marshal.ReadIntPtr(pFactory);
            // EnumAdapters1 is index 12 in IDXGIFactory1
            var pEnumAdapters1 = Marshal.ReadIntPtr(factoryVtbl, 12 * nint.Size);
            var enumAdapters1 = Marshal.GetDelegateForFunctionPointer<EnumAdapters1Delegate>(pEnumAdapters1);

            uint index = 0;
            DXGI_ADAPTER_DESC1 bestDesc = default;
            var foundPreferred = false;

            while (true)
            {
                var enumHr = enumAdapters1(pFactory, index, out var pAdapter);
                if (enumHr != 0 || pAdapter == nint.Zero)
                {
                    break;
                }

                try
                {
                    var adapterVtbl = Marshal.ReadIntPtr(pAdapter);
                    // GetDesc1 is index 10 in IDXGIAdapter1
                    var pGetDesc1 = Marshal.ReadIntPtr(adapterVtbl, 10 * nint.Size);
                    var getDesc1 = Marshal.GetDelegateForFunctionPointer<GetDesc1Delegate>(pGetDesc1);

                    var desc = new DXGI_ADAPTER_DESC1();
                    if (getDesc1(pAdapter, ref desc) == 0)
                    {
                        var isSoftware = (desc.Flags & 2) != 0;
                        if (!isSoftware)
                        {
                            if (desc.VendorId == _options.Value.PreferredGpuVendorId && !foundPreferred)
                            {
                                bestDesc = desc;
                                foundPreferred = true;
                            }
                            else if (!foundPreferred && (bestDesc.VendorId == 0 || (ulong)desc.SharedSystemMemory > (ulong)bestDesc.SharedSystemMemory))
                            {
                                bestDesc = desc;
                            }
                        }
                    }
                }
                finally
                {
                    Marshal.Release(pAdapter);
                }

                index++;
            }

            if (bestDesc.VendorId != 0)
            {
                _gpuName = string.IsNullOrWhiteSpace(bestDesc.Description) ? "AMD Radeon(TM) 890M Graphics" : bestDesc.Description;
                _luidString = $"0x{(uint)bestDesc.AdapterLuid.HighPart:x8}_0x{bestDesc.AdapterLuid.LowPart:x8}";
                _dedicatedTotalBytes = (ulong)bestDesc.DedicatedVideoMemory;
                _sharedTotalBytes = (ulong)bestDesc.SharedSystemMemory;

                _logger.LogInformation("Selected GPU: {GpuName}, LUID: {Luid}, Dedicated: {DedicatedMb} MB, Shared: {SharedGb:F1} GB",
                    _gpuName, _luidString, _dedicatedTotalBytes / (1024.0 * 1024.0), _sharedTotalBytes / (1024.0 * 1024.0 * 1024.0));
            }
        }
        finally
        {
            Marshal.Release(pFactory);
        }
    }

    private void InitializePdhCounters()
    {
        if (string.IsNullOrEmpty(_luidString))
        {
            return;
        }

        var openStatus = PdhOpenQueryW(null, nuint.Zero, out _hPdhQuery);
        if (openStatus != ErrorSuccess)
        {
            _logger.LogError("PdhOpenQueryW failed with error status: 0x{Status:X8}", openStatus);
            return;
        }

        var instancePattern = $"luid_{_luidString}_phys_0";
        var pathDedicated = $@"\GPU Adapter Memory({instancePattern})\Dedicated Usage";
        var pathShared = $@"\GPU Adapter Memory({instancePattern})\Shared Usage";
        var pathCommit = $@"\GPU Adapter Memory({instancePattern})\Total Committed";

        _hCounterDedicated = AddMemoryCounter(pathDedicated);
        _hCounterShared = AddMemoryCounter(pathShared);
        _hCounterCommit = AddMemoryCounter(pathCommit);

        RefreshEngineCounters();

        // Initial collection to prime counter values
        PdhCollectQueryData(_hPdhQuery);
    }

    private void RefreshEngineCounters()
    {
        if (_hPdhQuery == nint.Zero || string.IsNullOrEmpty(_luidString))
        {
            return;
        }

        // Clean up old engine counters
        foreach (var counter in _engineCounters)
        {
            PdhRemoveCounter(counter);
        }
        _engineCounters.Clear();

        var wildcardPath = $@"\GPU Engine(*luid_{_luidString}*)\Utilization Percentage";
        uint bufferSize = 0;
        var expandStatus = PdhExpandWildCardPathW(null, wildcardPath, null, ref bufferSize, 0);

        if (expandStatus != PdhMoreData && expandStatus != ErrorSuccess)
        {
            _logger.LogDebug("PdhExpandWildCardPathW returned status: 0x{Status:X8}", expandStatus);
            return;
        }

        var buffer = new char[bufferSize];
        expandStatus = PdhExpandWildCardPathW(null, wildcardPath, buffer, ref bufferSize, 0);
        if (expandStatus != ErrorSuccess)
        {
            return;
        }

        var rawString = new string(buffer, 0, (int)bufferSize);
        var paths = rawString.Split('\0', StringSplitOptions.RemoveEmptyEntries);

        foreach (var path in paths)
        {
            var addStatus = PdhAddEnglishCounterW(_hPdhQuery, path, nuint.Zero, out var hCounter);
            if (addStatus == ErrorSuccess && hCounter != nint.Zero)
            {
                _engineCounters.Add(hCounter);
            }
            else
            {
                _logger.LogDebug("Failed to add GPU engine counter {Path}. Status: 0x{Status:X8}", path, addStatus);
            }
        }

        _lastEngineCounterRefresh = DateTimeOffset.UtcNow;
    }

    public ValueTask<(string GpuName, double LoadPercentage, ulong? DedicatedUsed, ulong DedicatedTotal, ulong? SharedUsed, ulong SharedTotal, ulong? TotalCommitted)> GetGpuMetricsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            if (_hPdhQuery == nint.Zero)
            {
                return ValueTask.FromResult((_gpuName, 0.0, (ulong?)null, _dedicatedTotalBytes, (ulong?)null, _sharedTotalBytes, (ulong?)null));
            }

            // Periodically refresh engine counters in case new 3D/Compute processes launched
            if ((DateTimeOffset.UtcNow - _lastEngineCounterRefresh).TotalSeconds > 10.0)
            {
                RefreshEngineCounters();
            }

            var collectStatus = PdhCollectQueryData(_hPdhQuery);
            if (collectStatus != ErrorSuccess)
            {
                _logger.LogDebug("PdhCollectQueryData returned status: 0x{Status:X8}", collectStatus);
            }

            var dedicatedUsed = ReadCounterDouble(_hCounterDedicated, "dedicated memory");
            var sharedUsed = ReadCounterDouble(_hCounterShared, "shared memory");
            var totalCommit = ReadCounterDouble(_hCounterCommit, "total committed memory");

            var totalGpuLoad = 0.0;
            foreach (var engineCounter in _engineCounters)
            {
                var engineVal = ReadCounterDouble(engineCounter, "GPU engine utilization");
                if (engineVal is > 0.0)
                {
                    totalGpuLoad = Math.Max(totalGpuLoad, engineVal.Value);
                }
            }

            totalGpuLoad = Math.Clamp(totalGpuLoad, 0.0, 100.0);

            return ValueTask.FromResult((
                _gpuName,
                totalGpuLoad,
                ToUnsignedBytes(dedicatedUsed),
                _dedicatedTotalBytes,
                ToUnsignedBytes(sharedUsed),
                _sharedTotalBytes,
                ToUnsignedBytes(totalCommit)
            ));
        }
    }

    private nint AddMemoryCounter(string path)
    {
        var status = PdhAddEnglishCounterW(_hPdhQuery, path, nuint.Zero, out var hCounter);
        if (status != ErrorSuccess || hCounter == nint.Zero)
        {
            _logger.LogWarning("Failed to add GPU memory counter {Path}. Status: 0x{Status:X8}", path, status);
            return nint.Zero;
        }

        return hCounter;
    }

    private double? ReadCounterDouble(nint hCounter, string counterName)
    {
        if (hCounter == nint.Zero)
        {
            return null;
        }

        var status = PdhGetFormattedCounterValue(hCounter, PdhFmtDouble, nint.Zero, out var counterValue);
        if (status != ErrorSuccess || counterValue.CStatus != ErrorSuccess)
        {
            _logger.LogDebug("Failed to read {CounterName} counter. Status: 0x{Status:X8}, counter status: 0x{CounterStatus:X8}",
                counterName, status, counterValue.CStatus);
            return null;
        }

        return counterValue.DoubleValue;
    }

    private static ulong? ToUnsignedBytes(double? value)
    {
        return value.HasValue ? (ulong)Math.Max(0.0, value.Value) : null;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        lock (_lock)
        {
            if (_hPdhQuery != nint.Zero)
            {
                PdhCloseQuery(_hPdhQuery);
                _hPdhQuery = nint.Zero;
            }
            _engineCounters.Clear();
            _isDisposed = true;
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int EnumAdapters1Delegate(nint pFactory, uint adapterIndex, out nint ppAdapter);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetDesc1Delegate(nint pAdapter, ref DXGI_ADAPTER_DESC1 pDesc);
}
