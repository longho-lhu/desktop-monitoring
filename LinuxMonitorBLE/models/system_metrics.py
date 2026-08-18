"""
SystemMetrics: Model dữ liệu thông số phần cứng và đóng gói 32-byte BLE binary packet.
Tương thích 100% với giao thức gói tin của firmware ESP32-C3 SSD1306.
"""

from dataclasses import dataclass, field
from datetime import datetime
from typing import Optional


@dataclass
class SystemMetrics:
    timestamp: datetime = field(default_factory=datetime.now)

    # CPU
    cpu_usage_percent: float = 0.0
    cpu_temperature_celsius: Optional[float] = None
    cpu_frequency_mhz: int = 0
    logical_processors: int = 1

    # RAM
    ram_usage_percent: float = 0.0
    ram_used_mb: int = 0
    ram_total_mb: int = 0

    # GPU
    gpu_usage_percent: Optional[float] = None
    gpu_temperature_celsius: Optional[float] = None
    gpu_vram_used_mb: Optional[int] = None

    # Disk
    disk_read_kbps: float = 0.0
    disk_write_kbps: float = 0.0
    disk_usage_percent: float = 0.0

    # Network
    network_sent_kbps: float = 0.0
    network_received_kbps: float = 0.0

    # System
    uptime_seconds: int = 0

    def to_ble_packet(self) -> bytes:
        """
        Chuyển thông số thành mảng 32-byte nhị phân gửi qua BLE GATT.
        
        Cấu trúc gói tin 32 bytes:
        [0]     Header cố định: 0xA5
        [1]     CPU Usage (0-100%)
        [2]     CPU Temp (0-254°C, 0xFF nếu N/A)
        [3..4]  RAM Used MB / 10 (uint16 LE)
        [5..6]  RAM Total MB / 10 (uint16 LE)
        [7]     RAM Usage (0-100%)
        [8]     GPU Usage (0-100%, 0xFF nếu N/A)
        [9]     GPU Temp (0-254°C, 0xFF nếu N/A)
        [10..11] Disk Read KB/s (uint16 LE)
        [12..13] Disk Write KB/s (uint16 LE)
        [14]    Disk Usage (0-100%)
        [15..16] Network Sent KB/s (uint16 LE)
        [17..18] Network Received KB/s (uint16 LE)
        [19..22] Uptime Seconds (uint32 LE)
        [23]    CPU Frequency MHz / 100 (uint8)
        [24..30] Reserved (0x00)
        [31]    Checksum XOR byte 0..30
        """
        packet = bytearray(32)

        # [0] Header cố định
        packet[0] = 0xA5

        # [1..2] CPU
        packet[1] = max(0, min(100, int(round(self.cpu_usage_percent))))
        packet[2] = (
            max(0, min(254, int(round(self.cpu_temperature_celsius))))
            if self.cpu_temperature_celsius is not None
            else 0xFF
        )

        # [3..7] RAM (Used / 10, Total / 10, %)
        ram_used_10 = min(max(0, self.ram_used_mb // 10), 65535)
        ram_total_10 = min(max(0, self.ram_total_mb // 10), 65535)
        packet[3] = ram_used_10 & 0xFF
        packet[4] = (ram_used_10 >> 8) & 0xFF
        packet[5] = ram_total_10 & 0xFF
        packet[6] = (ram_total_10 >> 8) & 0xFF
        packet[7] = max(0, min(100, int(round(self.ram_usage_percent))))

        # [8..9] GPU
        packet[8] = (
            max(0, min(100, int(round(self.gpu_usage_percent))))
            if self.gpu_usage_percent is not None
            else 0xFF
        )
        packet[9] = (
            max(0, min(254, int(round(self.gpu_temperature_celsius))))
            if self.gpu_temperature_celsius is not None
            else 0xFF
        )

        # [10..14] Disk (Read KB/s, Write KB/s, % Usage)
        disk_r = min(max(0, int(round(self.disk_read_kbps))), 65535)
        disk_w = min(max(0, int(round(self.disk_write_kbps))), 65535)
        packet[10] = disk_r & 0xFF
        packet[11] = (disk_r >> 8) & 0xFF
        packet[12] = disk_w & 0xFF
        packet[13] = (disk_w >> 8) & 0xFF
        packet[14] = max(0, min(100, int(round(self.disk_usage_percent))))

        # [15..18] Network (Sent KB/s, Recv KB/s)
        net_send = min(max(0, int(round(self.network_sent_kbps))), 65535)
        net_recv = min(max(0, int(round(self.network_received_kbps))), 65535)
        packet[15] = net_send & 0xFF
        packet[16] = (net_send >> 8) & 0xFF
        packet[17] = net_recv & 0xFF
        packet[18] = (net_recv >> 8) & 0xFF

        # [19..22] Uptime (uint32 LE)
        uptime = min(max(0, int(self.uptime_seconds)), 0xFFFFFFFF)
        packet[19] = uptime & 0xFF
        packet[20] = (uptime >> 8) & 0xFF
        packet[21] = (uptime >> 16) & 0xFF
        packet[22] = (uptime >> 24) & 0xFF

        # [23] CPU Frequency (MHz / 100)
        packet[23] = max(0, min(254, self.cpu_frequency_mhz // 100))

        # [24..30] Reserved (0x00) -> Mặc định đã là 0

        # [31] Checksum XOR
        checksum = 0
        for i in range(31):
            checksum ^= packet[i]
        packet[31] = checksum

        return bytes(packet)

    def __str__(self) -> str:
        gpu_str = (
            f"GPU: {self.gpu_usage_percent:.1f}% ({self.gpu_temperature_celsius or 0:.0f}°C)"
            if self.gpu_usage_percent is not None
            else "GPU: N/A"
        )
        temp_str = (
            f"{self.cpu_temperature_celsius:.1f}°C"
            if self.cpu_temperature_celsius is not None
            else "N/A"
        )
        return (
            f"CPU: {self.cpu_usage_percent:.1f}% ({temp_str}, {self.cpu_frequency_mhz}MHz) | "
            f"RAM: {self.ram_used_mb}/{self.ram_total_mb} MB ({self.ram_usage_percent:.1f}%) | "
            f"{gpu_str} | "
            f"Disk: ↑{self.disk_write_kbps:.0f} ↓{self.disk_read_kbps:.0f} KB/s ({self.disk_usage_percent:.0f}%) | "
            f"Net: ↑{self.network_sent_kbps:.0f} ↓{self.network_received_kbps:.0f} KB/s | "
            f"Uptime: {self.uptime_seconds}s"
        )
