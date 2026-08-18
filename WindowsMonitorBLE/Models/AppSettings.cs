namespace WindowsMonitorBLE.Models;

public class AppSettings
{
    // ── 1. Hệ thống ───────────────────────────────────────────────
    /// <summary>Tự động khởi động cùng Windows khi bật máy</summary>
    public bool StartWithWindows { get; set; } = false;

    /// <summary>Tự động thu nhỏ xuống khay hệ thống (System Tray)</summary>
    public bool MinimizeToTray { get; set; } = false;

    // ── 2. Ổ đĩa ──────────────────────────────────────────────────
    /// <summary>Tên ổ đĩa được chọn ("_Total" hoặc instance cụ thể như "0 C:")</summary>
    public string SelectedDiskInstance { get; set; } = "_Total";

    /// <summary>Đơn vị đo tốc độ đọc/ghi ổ đĩa ("Auto", "KB/s", "MB/s", "GB/s")</summary>
    public string DiskSpeedUnit { get; set; } = "Auto";

    /// <summary>Ký tự ổ đĩa để theo dõi dung lượng (mặc định "C")</summary>
    public string DiskDriveLetter { get; set; } = "C";

    // ── 3. Card mạng ──────────────────────────────────────────────
    /// <summary>Tên card mạng được chọn ("Auto" hoặc tên cụ thể)</summary>
    public string SelectedNetworkAdapter { get; set; } = "Auto";

    /// <summary>Đơn vị đo tốc độ mạng ("Auto", "KB/s", "MB/s", "Kbps", "Mbps")</summary>
    public string NetworkSpeedUnit { get; set; } = "Auto";

    // ── 4. BLE & ESP32 ───────────────────────────────────────────
    /// <summary>Chu kỳ gửi dữ liệu (ms)</summary>
    public int SendIntervalMs { get; set; } = 1000;

    /// <summary>Tự động gửi BLE khi kết nối thành công</summary>
    public bool AutoSendOnConnect { get; set; } = false;

    /// <summary>Tên thiết bị BLE kết nối gần nhất để tự kết nối lại</summary>
    public string? LastConnectedBleName { get; set; }
}
