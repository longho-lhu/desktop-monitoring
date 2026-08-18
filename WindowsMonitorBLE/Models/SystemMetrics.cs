namespace WindowsMonitorBLE.Models;

/// <summary>
/// Chứa toàn bộ thông số hệ thống thu thập được tại một thời điểm.
/// </summary>
public class SystemMetrics
{
    public DateTime Timestamp { get; set; } = DateTime.Now;

    // CPU
    public float CpuUsagePercent { get; set; }
    public float? CpuTemperatureCelsius { get; set; }
    public int CpuFrequencyMHz { get; set; }
    public int LogicalProcessors { get; set; }

    // RAM
    public float RamUsagePercent { get; set; }
    public long RamUsedMB { get; set; }
    public long RamTotalMB { get; set; }

    // GPU
    public float? GpuUsagePercent { get; set; }
    public float? GpuTemperatureCelsius { get; set; }
    public long? GpuVramUsedMB { get; set; }

    // Disk
    public float DiskReadKBps { get; set; }
    public float DiskWriteKBps { get; set; }
    public float DiskCUsagePercent { get; set; }

    // Network
    public float NetworkSentKBps { get; set; }
    public float NetworkReceivedKBps { get; set; }

    // System
    public long UptimeSeconds { get; set; }

    /// <summary>
    /// Chuyển thông số thành mảng 32-byte nhị phân gửi qua BLE GATT.
    /// </summary>
    public byte[] ToBlePacket()
    {
        byte[] packet = new byte[32];

        // [0] Header
        packet[0] = 0xA5;

        // [1-2] CPU (% & Temp)
        packet[1] = (byte)Math.Clamp(CpuUsagePercent, 0, 100);
        packet[2] = CpuTemperatureCelsius.HasValue
            ? (byte)Math.Clamp(CpuTemperatureCelsius.Value, 0, 254)
            : (byte)0xFF;

        // [3-7] RAM (Used/10, Total/10, %)
        ushort ramUsed10 = (ushort)Math.Min(RamUsedMB / 10, 65535);
        ushort ramTotal10 = (ushort)Math.Min(RamTotalMB / 10, 65535);
        packet[3] = (byte)(ramUsed10 & 0xFF);
        packet[4] = (byte)(ramUsed10 >> 8);
        packet[5] = (byte)(ramTotal10 & 0xFF);
        packet[6] = (byte)(ramTotal10 >> 8);
        packet[7] = (byte)Math.Clamp(RamUsagePercent, 0, 100);

        // [8-9] GPU (% & Temp)
        packet[8] = GpuUsagePercent.HasValue ? (byte)Math.Clamp(GpuUsagePercent.Value, 0, 254) : (byte)0xFF;
        packet[9] = GpuTemperatureCelsius.HasValue ? (byte)Math.Clamp(GpuTemperatureCelsius.Value, 0, 254) : (byte)0xFF;

        // [10-14] Disk (Read, Write, % C:)
        ushort diskR = (ushort)Math.Min(DiskReadKBps, 65535);
        ushort diskW = (ushort)Math.Min(DiskWriteKBps, 65535);
        packet[10] = (byte)(diskR & 0xFF);
        packet[11] = (byte)(diskR >> 8);
        packet[12] = (byte)(diskW & 0xFF);
        packet[13] = (byte)(diskW >> 8);
        packet[14] = (byte)Math.Clamp(DiskCUsagePercent, 0, 100);

        // [15-18] Network (Sent, Recv KB/s)
        ushort netSend = (ushort)Math.Min(NetworkSentKBps, 65535);
        ushort netRecv = (ushort)Math.Min(NetworkReceivedKBps, 65535);
        packet[15] = (byte)(netSend & 0xFF);
        packet[16] = (byte)(netSend >> 8);
        packet[17] = (byte)(netRecv & 0xFF);
        packet[18] = (byte)(netRecv >> 8);

        // [19-22] Uptime (uint32 seconds)
        uint uptime = (uint)Math.Min(UptimeSeconds, uint.MaxValue);
        packet[19] = (byte)(uptime & 0xFF);
        packet[20] = (byte)((uptime >> 8) & 0xFF);
        packet[21] = (byte)((uptime >> 16) & 0xFF);
        packet[22] = (byte)((uptime >> 24) & 0xFF);

        // [23] CPU Frequency (MHz / 100)
        packet[23] = (byte)Math.Clamp(CpuFrequencyMHz / 100, 0, 254);

        // [24-30] Reserved (0x00)
        // [31] Checksum XOR
        byte chk = 0;
        for (int i = 0; i < 31; i++) chk ^= packet[i];
        packet[31] = chk;

        return packet;
    }

    [Obsolete("Sử dụng ToBlePacket() thay thế")]
    public byte[] ToBlePaket() => ToBlePacket();

    public override string ToString()
    {
        return $"CPU: {CpuUsagePercent:F1}% | RAM: {RamUsedMB}/{RamTotalMB} MB ({RamUsagePercent:F1}%) | " +
               $"Disk: ↑{DiskWriteKBps:F0} ↓{DiskReadKBps:F0} KB/s | Net: ↑{NetworkSentKBps:F0} ↓{NetworkReceivedKBps:F0} KB/s";
    }
}
