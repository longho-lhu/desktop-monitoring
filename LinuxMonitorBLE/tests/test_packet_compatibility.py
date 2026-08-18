"""
Test Suite: Xác thực tính tương thích tuyệt đối giữa gói tin 32 bytes của Linux Monitor BLE
và bộ giải mã C++ trên Firmware ESP32-C3 SSD1306.
"""

import unittest
from models.system_metrics import SystemMetrics


def parse_telemetry_packet_cpp_emulation(packet: bytes):
    """Mô phỏng chính xác hàm parseTelemetryPacket() trong ESP32_Firmware_Example.ino"""
    if len(packet) < 32 or packet[0] != 0xA5:
        return None, "Invalid Header or Length"

    # Checksum XOR 31 bytes đầu
    chk = 0
    for i in range(31):
        chk ^= packet[i]
    if chk != packet[31]:
        return None, f"Checksum Error: expected {packet[31]}, got {chk}"

    cpu_usage = packet[1]
    cpu_temp = -1 if packet[2] == 0xFF else packet[2]
    ram_used_mb = (packet[3] | (packet[4] << 8)) * 10
    ram_total_mb = (packet[5] | (packet[6] << 8)) * 10
    ram_usage = packet[7]
    gpu_usage = -1 if packet[8] == 0xFF else packet[8]
    gpu_temp = -1 if packet[9] == 0xFF else packet[9]
    disk_read_kbps = packet[10] | (packet[11] << 8)
    disk_write_kbps = packet[12] | (packet[13] << 8)
    disk_usage = packet[14]
    net_sent_kbps = packet[15] | (packet[16] << 8)
    net_recv_kbps = packet[17] | (packet[18] << 8)
    uptime_seconds = packet[19] | (packet[20] << 8) | (packet[21] << 16) | (packet[22] << 24)
    cpu_freq_mhz = packet[23] * 100

    return {
        "cpu_usage": cpu_usage,
        "cpu_temp": cpu_temp,
        "ram_used_mb": ram_used_mb,
        "ram_total_mb": ram_total_mb,
        "ram_usage": ram_usage,
        "gpu_usage": gpu_usage,
        "gpu_temp": gpu_temp,
        "disk_read_kbps": disk_read_kbps,
        "disk_write_kbps": disk_write_kbps,
        "disk_usage": disk_usage,
        "net_sent_kbps": net_sent_kbps,
        "net_recv_kbps": net_recv_kbps,
        "uptime_seconds": uptime_seconds,
        "cpu_freq_mhz": cpu_freq_mhz,
    }, "OK"


class TestPacketCompatibility(unittest.TestCase):
    def test_packet_with_full_values(self):
        """Test gói tin đầy đủ sensor (CPU, RAM, GPU, Disk, Net, Uptime)."""
        metrics = SystemMetrics(
            cpu_usage_percent=45.2,
            cpu_temperature_celsius=58.0,
            cpu_frequency_mhz=3800,
            logical_processors=16,
            ram_usage_percent=62.4,
            ram_used_mb=10800,
            ram_total_mb=32000,
            gpu_usage_percent=30.0,
            gpu_temperature_celsius=50.0,
            gpu_vram_used_mb=4096,
            disk_read_kbps=3200.0,
            disk_write_kbps=1500.0,
            disk_usage_percent=72.0,
            network_sent_kbps=120.0,
            network_received_kbps=1500.0,
            uptime_seconds=9912,  # 02:45:12
        )

        packet = metrics.to_ble_packet()
        self.assertEqual(len(packet), 32, "Độ dài gói tin phải chính xác là 32 bytes")
        self.assertEqual(packet[0], 0xA5, "Header cố định phải là 0xA5")

        parsed, status = parse_telemetry_packet_cpp_emulation(packet)
        self.assertEqual(status, "OK")
        self.assertIsNotNone(parsed)

        self.assertEqual(parsed["cpu_usage"], 45)
        self.assertEqual(parsed["cpu_temp"], 58)
        self.assertEqual(parsed["cpu_freq_mhz"], 3800)
        self.assertEqual(parsed["ram_usage"], 62)
        self.assertEqual(parsed["ram_used_mb"], 10800)
        self.assertEqual(parsed["ram_total_mb"], 32000)
        self.assertEqual(parsed["gpu_usage"], 30)
        self.assertEqual(parsed["gpu_temp"], 50)
        self.assertEqual(parsed["disk_read_kbps"], 3200)
        self.assertEqual(parsed["disk_write_kbps"], 1500)
        self.assertEqual(parsed["disk_usage"], 72)
        self.assertEqual(parsed["net_sent_kbps"], 120)
        self.assertEqual(parsed["net_recv_kbps"], 1500)
        self.assertEqual(parsed["uptime_seconds"], 9912)

    def test_packet_with_missing_gpu_and_temp(self):
        """Test gói tin khi máy không có GPU rời hoặc không có sensor nhiệt độ (trường hợp iGPU/Server)."""
        metrics = SystemMetrics(
            cpu_usage_percent=85.0,
            cpu_temperature_celsius=None,  # N/A
            cpu_frequency_mhz=2400,
            logical_processors=4,
            ram_usage_percent=40.0,
            ram_used_mb=3200,
            ram_total_mb=8000,
            gpu_usage_percent=None,  # N/A
            gpu_temperature_celsius=None,  # N/A
            disk_read_kbps=0.0,
            disk_write_kbps=450.0,
            disk_usage_percent=55.0,
            network_sent_kbps=10.0,
            network_received_kbps=25.0,
            uptime_seconds=3600,
        )

        packet = metrics.to_ble_packet()
        self.assertEqual(len(packet), 32)
        self.assertEqual(packet[2], 0xFF, "CPU Temp N/A phải là 0xFF")
        self.assertEqual(packet[8], 0xFF, "GPU Usage N/A phải là 0xFF")
        self.assertEqual(packet[9], 0xFF, "GPU Temp N/A phải là 0xFF")

        parsed, status = parse_telemetry_packet_cpp_emulation(packet)
        self.assertEqual(status, "OK")
        self.assertEqual(parsed["cpu_temp"], -1)
        self.assertEqual(parsed["gpu_usage"], -1)
        self.assertEqual(parsed["gpu_temp"], -1)
        self.assertEqual(parsed["cpu_usage"], 85)


if __name__ == "__main__":
    unittest.main()
