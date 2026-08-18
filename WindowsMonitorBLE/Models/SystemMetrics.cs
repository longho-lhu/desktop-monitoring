namespace WindowsMonitorBLE.Models;

/// <summary>
/// Chứa toàn bộ thông số hệ thống thu thập được tại một thời điểm
/// </summary>
public class SystemMetrics
{
    // ── Thời gian ─────────────────────────────────────────────────
    public DateTime Timestamp { get; set; } = DateTime.Now;

    // ── CPU ───────────────────────────────────────────────────────
    /// <summary>Phần trăm sử dụng CPU toàn hệ thống (0–100)</summary>
    public float CpuUsagePercent { get; set; }

    /// <summary>Nhiệt độ CPU theo °C (null nếu không lấy được)</summary>
    public float? CpuTemperatureCelsius { get; set; }

    /// <summary>Tần số CPU hiện tại (MHz)</summary>
    public int CpuFrequencyMHz { get; set; }

    /// <summary>Số nhân logic</summary>
    public int LogicalProcessors { get; set; }

    // ── RAM ───────────────────────────────────────────────────────
    /// <summary>Phần trăm RAM đang dùng (0–100)</summary>
    public float RamUsagePercent { get; set; }

    /// <summary>RAM đang dùng (MB)</summary>
    public long RamUsedMB { get; set; }

    /// <summary>Tổng RAM (MB)</summary>
    public long RamTotalMB { get; set; }

    // ── GPU ───────────────────────────────────────────────────────
    /// <summary>Phần trăm tải GPU (null nếu không đọc được)</summary>
    public float? GpuUsagePercent { get; set; }

    /// <summary>Nhiệt độ GPU (°C)</summary>
    public float? GpuTemperatureCelsius { get; set; }

    /// <summary>VRAM đang dùng (MB)</summary>
    public long? GpuVramUsedMB { get; set; }

    // ── Disk ──────────────────────────────────────────────────────
    /// <summary>Tốc độ đọc disk (KB/s)</summary>
    public float DiskReadKBps { get; set; }

    /// <summary>Tốc độ ghi disk (KB/s)</summary>
    public float DiskWriteKBps { get; set; }

    /// <summary>Phần trăm dung lượng ổ C: đã dùng</summary>
    public float DiskCUsagePercent { get; set; }

    // ── Network ───────────────────────────────────────────────────
    /// <summary>Tốc độ gửi mạng (KB/s)</summary>
    public float NetworkSentKBps { get; set; }

    /// <summary>Tốc độ nhận mạng (KB/s)</summary>
    public float NetworkReceivedKBps { get; set; }

    // ── System ────────────────────────────────────────────────────
    /// <summary>Uptime hệ thống (giây)</summary>
    public long UptimeSeconds { get; set; }

    // ── Serialization ─────────────────────────────────────────────
    /// <summary>
    /// Chuyển thông số thành mảng byte compact để gửi qua BLE.
    /// 
    /// Cấu trúc gói tin (32 bytes):
    /// [0]      Header: 0xA5
    /// [1]      CPU usage % (0–100)
    /// [2]      CPU temp °C (0–255, 0xFF = không có)
    /// [3-4]    RAM used MB (uint16, chia 10 → max 655350 MB)
    /// [5-6]    RAM total MB (uint16, chia 10)
    /// [7]      RAM usage % (0–100)
    /// [8]      GPU usage % (0–100, 0xFF = không có)
    /// [9]      GPU temp °C (0xFF = không có)
    /// [10-11]  Disk read KB/s (uint16)
    /// [12-13]  Disk write KB/s (uint16)
    /// [14]     Disk C: usage % (0–100)
    /// [15-16]  Network sent KB/s (uint16)
    /// [17-18]  Network received KB/s (uint16)
    /// [19-22]  Uptime seconds (uint32, little-endian)
    /// [23]     CPU frequency (MHz / 100, để fit vào 1 byte, max 25500 MHz)
    /// [24-31]  Reserved / padding
    /// [31]     Checksum XOR của [0..30]
    /// </summary>
    public byte[] ToBlePaket()
    {
        byte[] packet = new byte[32];

        // Header
        packet[0] = 0xA5;

        // CPU
        packet[1] = (byte)Math.Clamp(CpuUsagePercent, 0, 100);
        packet[2] = CpuTemperatureCelsius.HasValue
            ? (byte)Math.Clamp(CpuTemperatureCelsius.Value, 0, 254)
            : (byte)0xFF;

        // RAM (đơn vị: 10 MB → fit uint16 = 655,350 MB tối đa)
        ushort ramUsed10 = (ushort)Math.Min(RamUsedMB / 10, 65535);
        ushort ramTotal10 = (ushort)Math.Min(RamTotalMB / 10, 65535);
        packet[3] = (byte)(ramUsed10 & 0xFF);
        packet[4] = (byte)(ramUsed10 >> 8);
        packet[5] = (byte)(ramTotal10 & 0xFF);
        packet[6] = (byte)(ramTotal10 >> 8);
        packet[7] = (byte)Math.Clamp(RamUsagePercent, 0, 100);

        // GPU
        packet[8] = GpuUsagePercent.HasValue
            ? (byte)Math.Clamp(GpuUsagePercent.Value, 0, 254)
            : (byte)0xFF;
        packet[9] = GpuTemperatureCelsius.HasValue
            ? (byte)Math.Clamp(GpuTemperatureCelsius.Value, 0, 254)
            : (byte)0xFF;

        // Disk R/W (KB/s, uint16)
        ushort diskR = (ushort)Math.Min(DiskReadKBps, 65535);
        ushort diskW = (ushort)Math.Min(DiskWriteKBps, 65535);
        packet[10] = (byte)(diskR & 0xFF);
        packet[11] = (byte)(diskR >> 8);
        packet[12] = (byte)(diskW & 0xFF);
        packet[13] = (byte)(diskW >> 8);
        packet[14] = (byte)Math.Clamp(DiskCUsagePercent, 0, 100);

        // Network (KB/s, uint16)
        ushort netSend = (ushort)Math.Min(NetworkSentKBps, 65535);
        ushort netRecv = (ushort)Math.Min(NetworkReceivedKBps, 65535);
        packet[15] = (byte)(netSend & 0xFF);
        packet[16] = (byte)(netSend >> 8);
        packet[17] = (byte)(netRecv & 0xFF);
        packet[18] = (byte)(netRecv >> 8);

        // Uptime (uint32, little-endian)
        uint uptime = (uint)Math.Min(UptimeSeconds, uint.MaxValue);
        packet[19] = (byte)(uptime & 0xFF);
        packet[20] = (byte)((uptime >> 8) & 0xFF);
        packet[21] = (byte)((uptime >> 16) & 0xFF);
        packet[22] = (byte)((uptime >> 24) & 0xFF);

        // CPU Frequency (MHz / 100)
        packet[23] = (byte)Math.Clamp(CpuFrequencyMHz / 100, 0, 254);

        // Bytes 24-30: reserved, zero
        // Checksum XOR
        byte chk = 0;
        for (int i = 0; i < 31; i++) chk ^= packet[i];
        packet[31] = chk;

        return packet;
    }

    public override string ToString()
    {
        return $"CPU: {CpuUsagePercent:F1}% {(CpuTemperatureCelsius.HasValue ? $"({CpuTemperatureCelsius:F0}°C)" : "")} | " +
               $"RAM: {RamUsedMB}/{RamTotalMB} MB ({RamUsagePercent:F1}%) | " +
               $"GPU: {(GpuUsagePercent.HasValue ? $"{GpuUsagePercent:F1}%" : "N/A")} | " +
               $"Disk: ↑{DiskWriteKBps:F0} ↓{DiskReadKBps:F0} KB/s | " +
               $"Net: ↑{NetworkSentKBps:F0} ↓{NetworkReceivedKBps:F0} KB/s";
    }
}
