using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using LibreHardwareMonitor.Hardware;

namespace WindowsMonitorBLE.Services;

/// <summary>
/// Thu thập thông số hệ thống tối ưu hiệu năng:
/// 
/// ✅ RAM  → GlobalMemoryStatusEx P/Invoke  (~0.01ms)
/// ✅ CPU  → PerformanceCounter             (~0.1ms)
/// ✅ Temp → LibreHardwareMonitor           (update mỗi 3s, hỗ trợ Intel / AMD / Motherboard)
/// ✅ Disk → Cho phép chọn ổ đĩa cụ thể hoặc _Total, chọn ký tự ổ đĩa (C, D...)
/// ✅ Net  → Cho phép chọn card mạng cụ thể hoặc Tự động
/// </summary>
public sealed class SystemMetricsService : IDisposable
{
    // ── LibreHardwareMonitor ──────────────────────────────────────
    private Computer? _computer;

    // ── PerformanceCounters ───────────────────────────────────────
    private PerformanceCounter? _cpuCounter;
    private PerformanceCounter? _diskReadCounter;
    private PerformanceCounter? _diskWriteCounter;
    private PerformanceCounter? _netSentCounter;
    private PerformanceCounter? _netRecvCounter;

    // ── Device selections ─────────────────────────────────────────
    public string CurrentNetworkAdapter { get; private set; } = "Auto";
    public string CurrentDiskInstance   { get; private set; } = "_Total";
    public string CurrentDriveLetter    { get; private set; } = "C";

    // ── Cached / throttled values ─────────────────────────────────
    private float? _cachedCpuTemp;
    private float? _cachedGpuLoad;
    private float? _cachedGpuTemp;
    private float? _cachedGpuVramMB;
    private int    _cachedCpuFreq;
    private long   _lastTempUpdateTick;
    private const long TempUpdateIntervalMs = 3000;

    private float _cachedDiskCPercent;
    private long  _lastDiskUpdateTick;
    private const long DiskUpdateIntervalMs = 10_000;

    private readonly long _totalRamBytes;
    private bool _disposed;

    // ─────────────────────────────────────────────────────────────
    // P/Invoke: GlobalMemoryStatusEx
    // ─────────────────────────────────────────────────────────────
    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint  dwLength;
        public uint  dwMemoryLoad;       // % RAM đang dùng
        public ulong ullTotalPhys;       // Tổng RAM (bytes)
        public ulong ullAvailPhys;       // RAM còn trống (bytes)
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    // ─────────────────────────────────────────────────────────────

    public SystemMetricsService(string? networkAdapter = "Auto", string? diskInstance = "_Total", string? driveLetter = "C")
    {
        // LibreHardwareMonitor
        try
        {
            _computer = new Computer
            {
                IsCpuEnabled         = true,
                IsGpuEnabled         = true,
                IsMotherboardEnabled = true,
                IsControllerEnabled  = true,
                IsMemoryEnabled      = false
            };
            _computer.Open();
        }
        catch
        {
            _computer = null;
        }

        // CPU Counter
        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
            _cpuCounter.NextValue();
        }
        catch { _cpuCounter = null; }

        // Khởi tạo Disk, Network & Drive letter theo cấu hình
        SetDiskInstance(diskInstance ?? "_Total");
        SetNetworkAdapter(networkAdapter ?? "Auto");
        SetDriveLetter(driveLetter ?? "C");

        _totalRamBytes = GetTotalRam();
        _cachedDiskCPercent = ReadDiskPercent(CurrentDriveLetter);
        _lastDiskUpdateTick = Environment.TickCount64;

        RefreshHardwareSensors();
        _lastTempUpdateTick = Environment.TickCount64;
    }

    /// <summary>
    /// Kiểm tra ứng dụng có đang chạy dưới quyền Administrator hay không.
    /// </summary>
    public static bool IsRunningAsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    // ─────────────────────────────────────────────────────────────
    // Configuration Setters & Getters
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Lấy danh sách tất cả các card mạng có trong hệ thống.
    /// </summary>
    public static List<string> GetAvailableNetworkAdapters()
    {
        var list = new List<string>();
        try
        {
            var cat = new PerformanceCounterCategory("Network Interface");
            var names = cat.GetInstanceNames();
            foreach (var n in names)
            {
                if (!string.IsNullOrWhiteSpace(n))
                    list.Add(n);
            }
        }
        catch { }
        return list;
    }

    /// <summary>
    /// Lấy danh sách tất cả các ổ đĩa vật lý có thể đo tốc độ.
    /// </summary>
    public static List<string> GetAvailableDisks()
    {
        var list = new List<string>();
        try
        {
            var cat = new PerformanceCounterCategory("PhysicalDisk");
            var names = cat.GetInstanceNames();
            foreach (var n in names)
            {
                if (!string.IsNullOrWhiteSpace(n))
                    list.Add(n);
            }
        }
        catch { }
        if (!list.Contains("_Total"))
            list.Insert(0, "_Total");
        return list;
    }

    /// <summary>
    /// Lấy danh sách các phân vùng ổ đĩa logic (C:, D:, E:...).
    /// </summary>
    public static List<string> GetAvailableLogicalDrives()
    {
        var list = new List<string>();
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady && drive.DriveType == DriveType.Fixed)
                {
                    string letter = drive.Name.TrimEnd('\\').TrimEnd(':');
                    list.Add(letter);
                }
            }
        }
        catch { }
        if (list.Count == 0) list.Add("C");
        return list;
    }

    /// <summary>
    /// Thay đổi card mạng cần đọc tốc độ.
    /// </summary>
    public void SetNetworkAdapter(string adapterName)
    {
        try { _netSentCounter?.Dispose(); } catch { }
        try { _netRecvCounter?.Dispose(); } catch { }
        _netSentCounter = null;
        _netRecvCounter = null;

        string target = adapterName;
        if (string.IsNullOrEmpty(target) || target.Equals("Auto", StringComparison.OrdinalIgnoreCase))
        {
            target = GetFirstNetworkAdapter();
            CurrentNetworkAdapter = "Auto";
        }
        else
        {
            CurrentNetworkAdapter = target;
        }

        if (!string.IsNullOrEmpty(target))
        {
            try
            {
                _netSentCounter = new PerformanceCounter("Network Interface", "Bytes Sent/sec",     target, true);
                _netRecvCounter = new PerformanceCounter("Network Interface", "Bytes Received/sec", target, true);
                _netSentCounter.NextValue();
                _netRecvCounter.NextValue();
            }
            catch
            {
                _netSentCounter = null;
                _netRecvCounter = null;
            }
        }
    }

    /// <summary>
    /// Thay đổi ổ cứng cần đọc tốc độ (vd: "_Total", "0 C:", "1 D:").
    /// </summary>
    public void SetDiskInstance(string diskInstance)
    {
        try { _diskReadCounter?.Dispose(); } catch { }
        try { _diskWriteCounter?.Dispose(); } catch { }
        _diskReadCounter = null;
        _diskWriteCounter = null;

        string target = string.IsNullOrEmpty(diskInstance) ? "_Total" : diskInstance;
        CurrentDiskInstance = target;

        try
        {
            _diskReadCounter  = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec",  target, true);
            _diskWriteCounter = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", target, true);
            _diskReadCounter.NextValue();
            _diskWriteCounter.NextValue();
        }
        catch
        {
            _diskReadCounter = null;
            _diskWriteCounter = null;
        }
    }

    /// <summary>
    /// Thay đổi ký tự phân vùng ổ đĩa để theo dõi dung lượng % (C, D, E...).
    /// </summary>
    public void SetDriveLetter(string driveLetter)
    {
        CurrentDriveLetter = string.IsNullOrWhiteSpace(driveLetter) ? "C" : driveLetter.Trim().TrimEnd(':');
        _cachedDiskCPercent = ReadDiskPercent(CurrentDriveLetter);
    }

    // ─────────────────────────────────────────────────────────────
    // Public: Collect
    // ─────────────────────────────────────────────────────────────

    public Models.SystemMetrics Collect()
    {
        long now = Environment.TickCount64;

        if (now - _lastTempUpdateTick >= TempUpdateIntervalMs)
        {
            RefreshHardwareSensors();
            _lastTempUpdateTick = now;
        }

        if (now - _lastDiskUpdateTick >= DiskUpdateIntervalMs)
        {
            _cachedDiskCPercent = ReadDiskPercent(CurrentDriveLetter);
            _lastDiskUpdateTick = now;
        }

        float cpuUsage = 0;
        try { cpuUsage = _cpuCounter?.NextValue() ?? 0; } catch { }

        GetRamInfo(out long ramUsed, out float ramPercent);

        float diskRead = 0, diskWrite = 0;
        try
        {
            diskRead  = (_diskReadCounter?.NextValue()  ?? 0) / 1024f;
            diskWrite = (_diskWriteCounter?.NextValue() ?? 0) / 1024f;
        }
        catch { }

        float netSent = 0, netRecv = 0;
        try
        {
            netSent = (_netSentCounter?.NextValue() ?? 0) / 1024f;
            netRecv = (_netRecvCounter?.NextValue() ?? 0) / 1024f;
        }
        catch { }

        return new Models.SystemMetrics
        {
            Timestamp             = DateTime.Now,
            CpuUsagePercent       = cpuUsage,
            CpuTemperatureCelsius = _cachedCpuTemp,
            CpuFrequencyMHz       = _cachedCpuFreq,
            LogicalProcessors     = Environment.ProcessorCount,
            RamUsagePercent       = ramPercent,
            RamUsedMB             = ramUsed / (1024 * 1024),
            RamTotalMB            = _totalRamBytes / (1024 * 1024),
            GpuUsagePercent       = _cachedGpuLoad,
            GpuTemperatureCelsius = _cachedGpuTemp,
            GpuVramUsedMB         = _cachedGpuVramMB.HasValue ? (long)_cachedGpuVramMB.Value : null,
            DiskReadKBps          = diskRead,
            DiskWriteKBps         = diskWrite,
            DiskCUsagePercent     = _cachedDiskCPercent,
            NetworkSentKBps       = netSent,
            NetworkReceivedKBps   = netRecv,
            UptimeSeconds         = Environment.TickCount64 / 1000
        };
    }

    // ─────────────────────────────────────────────────────────────
    // LibreHardwareMonitor Sensor Reader
    // ─────────────────────────────────────────────────────────────

    private void RefreshHardwareSensors()
    {
        if (_computer == null) return;
        try
        {
            foreach (var hw in _computer.Hardware)
            {
                hw.Update();
                foreach (var sub in hw.SubHardware) sub.Update();
            }

            _cachedCpuTemp = ReadCpuTemperature();
            _cachedCpuFreq = ReadCpuFrequency();

            _cachedGpuLoad  = ReadSensorAcrossAll(SensorType.Load, [HardwareType.GpuNvidia, HardwareType.GpuAmd, HardwareType.GpuIntel], "Core")
                           ?? ReadSensorAcrossAll(SensorType.Load, [HardwareType.GpuNvidia, HardwareType.GpuAmd, HardwareType.GpuIntel]);

            _cachedGpuTemp  = ReadSensorAcrossAll(SensorType.Temperature, [HardwareType.GpuNvidia, HardwareType.GpuAmd, HardwareType.GpuIntel], "Core")
                           ?? ReadSensorAcrossAll(SensorType.Temperature, [HardwareType.GpuNvidia, HardwareType.GpuAmd, HardwareType.GpuIntel], "GPU")
                           ?? ReadSensorAcrossAll(SensorType.Temperature, [HardwareType.GpuNvidia, HardwareType.GpuAmd, HardwareType.GpuIntel]);

            _cachedGpuVramMB = ReadSensorAcrossAll(SensorType.SmallData, [HardwareType.GpuNvidia, HardwareType.GpuAmd], "Used")
                            ?? ReadSensorAcrossAll(SensorType.SmallData, [HardwareType.GpuNvidia, HardwareType.GpuAmd], "GPU Memory");
        }
        catch { }
    }

    private float? ReadCpuTemperature()
    {
        if (_computer == null) return null;
        try
        {
            // Các tên sensor phổ biến trên Intel / AMD
            string[] priorityNames = ["Package", "Tctl", "Average", "Core Max", "CCD", "Core #1", "CPU Core", "Core"];

            foreach (var nameHint in priorityNames)
            {
                foreach (var hw in _computer.Hardware)
                {
                    if (hw.HardwareType != HardwareType.Cpu) continue;
                    foreach (var s in hw.Sensors)
                    {
                        if (s.SensorType == SensorType.Temperature &&
                            s.Name.Contains(nameHint, StringComparison.OrdinalIgnoreCase) &&
                            s.Value.HasValue && s.Value > 0)
                            return s.Value.Value;
                    }
                    foreach (var sub in hw.SubHardware)
                    {
                        foreach (var s in sub.Sensors)
                        {
                            if (s.SensorType == SensorType.Temperature &&
                                s.Name.Contains(nameHint, StringComparison.OrdinalIgnoreCase) &&
                                s.Value.HasValue && s.Value > 0)
                                return s.Value.Value;
                        }
                    }
                }
            }

            // Fallback 1: Bất kỳ sensor nhiệt độ nào của CPU
            foreach (var hw in _computer.Hardware)
            {
                if (hw.HardwareType != HardwareType.Cpu) continue;
                foreach (var s in hw.Sensors)
                {
                    if (s.SensorType == SensorType.Temperature && s.Value.HasValue && s.Value > 0)
                        return s.Value.Value;
                }
                foreach (var sub in hw.SubHardware)
                {
                    foreach (var s in sub.Sensors)
                    {
                        if (s.SensorType == SensorType.Temperature && s.Value.HasValue && s.Value > 0)
                            return s.Value.Value;
                    }
                }
            }

            // Fallback 2: Sensor CPU trên Motherboard
            foreach (var hw in _computer.Hardware)
            {
                if (hw.HardwareType != HardwareType.Motherboard) continue;
                foreach (var s in hw.Sensors)
                {
                    if (s.SensorType == SensorType.Temperature &&
                        s.Name.Contains("CPU", StringComparison.OrdinalIgnoreCase) &&
                        s.Value.HasValue && s.Value > 0)
                        return s.Value.Value;
                }
                foreach (var sub in hw.SubHardware)
                {
                    foreach (var s in sub.Sensors)
                    {
                        if (s.SensorType == SensorType.Temperature &&
                            s.Name.Contains("CPU", StringComparison.OrdinalIgnoreCase) &&
                            s.Value.HasValue && s.Value > 0)
                            return s.Value.Value;
                    }
                }
            }
        }
        catch { }
        return null;
    }

    private int ReadCpuFrequency()
    {
        if (_computer == null) return 0;
        try
        {
            foreach (var hw in _computer.Hardware)
            {
                if (hw.HardwareType != HardwareType.Cpu) continue;
                foreach (var s in hw.Sensors)
                {
                    if (s.SensorType == SensorType.Clock && s.Value.HasValue && s.Value > 100)
                        return (int)s.Value.Value;
                }
                foreach (var sub in hw.SubHardware)
                {
                    foreach (var s in sub.Sensors)
                    {
                        if (s.SensorType == SensorType.Clock && s.Value.HasValue && s.Value > 100)
                            return (int)s.Value.Value;
                    }
                }
            }
        }
        catch { }
        return 0;
    }

    private float? ReadSensorAcrossAll(SensorType sensorType, HardwareType[] hwTypes, string? nameHint = null)
    {
        if (_computer == null) return null;
        try
        {
            foreach (var hw in _computer.Hardware)
            {
                if (!hwTypes.Contains(hw.HardwareType)) continue;

                foreach (var s in hw.Sensors)
                {
                    if (s.SensorType != sensorType) continue;
                    if (nameHint != null && !s.Name.Contains(nameHint, StringComparison.OrdinalIgnoreCase)) continue;
                    if (s.Value.HasValue) return s.Value.Value;
                }

                foreach (var sub in hw.SubHardware)
                {
                    foreach (var s in sub.Sensors)
                    {
                        if (s.SensorType != sensorType) continue;
                        if (nameHint != null && !s.Name.Contains(nameHint, StringComparison.OrdinalIgnoreCase)) continue;
                        if (s.Value.HasValue) return s.Value.Value;
                    }
                }
            }
        }
        catch { }
        return null;
    }

    // ─────────────────────────────────────────────────────────────
    // RAM via P/Invoke
    // ─────────────────────────────────────────────────────────────

    private static long GetTotalRam()
    {
        try
        {
            var ms = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            return GlobalMemoryStatusEx(ref ms) ? (long)ms.ullTotalPhys : 0;
        }
        catch { return 0; }
    }

    private static void GetRamInfo(out long usedBytes, out float percentUsed)
    {
        try
        {
            var ms = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (GlobalMemoryStatusEx(ref ms))
            {
                usedBytes    = (long)(ms.ullTotalPhys - ms.ullAvailPhys);
                percentUsed  = ms.dwMemoryLoad;
                return;
            }
        }
        catch { }
        usedBytes   = 0;
        percentUsed = 0;
    }

    // ─────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────

    private static float ReadDiskPercent(string driveLetter)
    {
        try
        {
            string name = driveLetter.EndsWith(":") ? driveLetter : driveLetter + ":";
            var d = new DriveInfo(name);
            return d.TotalSize > 0
                ? (float)(d.TotalSize - d.AvailableFreeSpace) / d.TotalSize * 100f
                : 0f;
        }
        catch { return 0f; }
    }

    private static string GetFirstNetworkAdapter()
    {
        try
        {
            foreach (string name in new PerformanceCounterCategory("Network Interface").GetInstanceNames())
            {
                if (name.Contains("Loopback", StringComparison.OrdinalIgnoreCase)) continue;
                if (name.Contains("Virtual",  StringComparison.OrdinalIgnoreCase)) continue;
                if (name.Contains("WAN Miniport", StringComparison.OrdinalIgnoreCase)) continue;
                return name;
            }
        }
        catch { }
        return string.Empty;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _computer?.Close(); } catch { }
        try { _cpuCounter?.Dispose(); } catch { }
        try { _diskReadCounter?.Dispose(); } catch { }
        try { _diskWriteCounter?.Dispose(); } catch { }
        try { _netSentCounter?.Dispose(); } catch { }
        try { _netRecvCounter?.Dispose(); } catch { }
    }
}
