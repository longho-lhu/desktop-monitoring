"""
CliApp: Giao diện Terminal Live Dashboard cho Linux Monitor BLE.
Thích hợp chạy trên Server, SSH, Raspberry Pi hoặc headless systemd daemon.
"""

import asyncio
import os
import sys
import time
from typing import Optional

try:
    from ..models.app_settings import AppSettings
    from ..models.system_metrics import SystemMetrics
    from ..services.ble_manager import BleManager, BleState
    from ..services.metrics_collector import LinuxMetricsCollector
except (ImportError, ValueError):
    from models.app_settings import AppSettings
    from models.system_metrics import SystemMetrics
    from services.ble_manager import BleManager, BleState
    from services.metrics_collector import LinuxMetricsCollector


class CliApp:
    def __init__(self, settings: AppSettings):
        self.settings = settings
        self.collector = LinuxMetricsCollector(
            network_adapter=settings.selected_network_adapter,
            disk_device=settings.selected_disk_device,
            disk_mount=settings.selected_disk_mount,
            enable_gpu=settings.enable_gpu_monitoring,
        )
        self.ble = BleManager(target_name=settings.target_device_name)
        self.ble.auto_reconnect = settings.auto_reconnect

        # Hook logs
        self._logs = []
        self._max_logs = 6
        self.ble.on_log = self._on_log
        self.ble.on_state_changed = self._on_state_changed

        self._running = False
        self._current_metrics: Optional[SystemMetrics] = None

    def _on_log(self, message: str):
        timestamp = time.strftime("%H:%M:%S")
        self._logs.append(f"[{timestamp}] {message}")
        if len(self._logs) > self._max_logs:
            self._logs.pop(0)

    def _on_state_changed(self, state: BleState):
        self._on_log(f"Trạng thái BLE chuyển sang: {state.value}")

    def _format_speed(self, kbps: float) -> str:
        if kbps >= 10240:
            return f"{kbps / 1024:.1f} MB/s"
        elif kbps >= 1024:
            return f"{kbps / 1024:.2f} MB/s"
        else:
            return f"{kbps:.0f} KB/s"

    def _format_uptime(self, seconds: int) -> str:
        hrs = seconds // 3600
        mins = (seconds % 3600) // 60
        secs = seconds % 60
        return f"{hrs:02d}:{mins:02d}:{secs:02d}"

    def _render_dashboard(self, metrics: SystemMetrics):
        """Vẽ bảng dashboard màu sắc chuyên nghiệp trên terminal."""
        # ANSI Colors
        CYAN = "\033[96m"
        GREEN = "\033[92m"
        YELLOW = "\033[93m"
        RED = "\033[91m"
        BLUE = "\033[94m"
        MAGENTA = "\033[95m"
        BOLD = "\033[1m"
        DIM = "\033[2m"
        RESET = "\033[0m"

        state_color = GREEN if self.ble.state == BleState.CONNECTED else (YELLOW if self.ble.state == BleState.CONNECTING else RED)

        # Build lines
        lines = []
        lines.append(f"{CYAN}{BOLD}╔══════════════════════════════════════════════════════════════════════════════╗{RESET}")
        lines.append(f"{CYAN}{BOLD}║         🐧 LINUX MONITOR BLE — ESP32 TELEMETRY (UBUNTU 26 / LINUX)           ║{RESET}")
        lines.append(f"{CYAN}{BOLD}╠══════════════════════════════════════════════════════════════════════════════╣{RESET}")
        
        # BLE Status Row
        ble_info = f"Trạng thái: {state_color}{BOLD}{self.ble.state.value}{RESET} | Thiết bị: {BOLD}{self.ble.connected_name or self.settings.target_device_name}{RESET}"
        if self.ble.connected_address:
            ble_info += f" [{self.ble.connected_address}]"
        pkt_info = f"Gói tin: {GREEN}↑{self.ble.packets_sent}{RESET} / {RED}✗{self.ble.packets_failed}{RESET} (Chu kỳ: {self.settings.send_interval_ms}ms)"
        lines.append(f"║ {ble_info.ljust(76)} ║")
        lines.append(f"║ {pkt_info.ljust(76)} ║")
        lines.append(f"{CYAN}╟──────────────────────────────────────────────────────────────────────────────╢{RESET}")

        # Metrics Row 1: CPU & RAM
        cpu_temp_str = f"{metrics.cpu_temperature_celsius:.0f}°C" if metrics.cpu_temperature_celsius is not None else "N/A"
        cpu_bar = self._make_progress_bar(metrics.cpu_usage_percent)
        cpu_str = f"{BOLD}CPU:{RESET} {metrics.cpu_usage_percent:4.1f}% | {cpu_temp_str:>4} | {metrics.cpu_frequency_mhz}MHz [{cpu_bar}]"
        
        ram_bar = self._make_progress_bar(metrics.ram_usage_percent)
        ram_str = f"{BOLD}RAM:{RESET} {metrics.ram_usage_percent:4.1f}% | {metrics.ram_used_mb}/{metrics.ram_total_mb}MB [{ram_bar}]"
        lines.append(f"║ {cpu_str.ljust(76)} ║")
        lines.append(f"║ {ram_str.ljust(76)} ║")

        # Metrics Row 2: GPU & Disk
        if metrics.gpu_usage_percent is not None:
            gpu_temp_str = f"{metrics.gpu_temperature_celsius:.0f}°C" if metrics.gpu_temperature_celsius is not None else "N/A"
            gpu_bar = self._make_progress_bar(metrics.gpu_usage_percent)
            gpu_str = f"{BOLD}GPU:{RESET} {metrics.gpu_usage_percent:4.1f}% | {gpu_temp_str:>4} [{gpu_bar}]"
        else:
            gpu_str = f"{BOLD}GPU:{RESET} N/A (Không phát hiện GPU rời hoặc tắt theo dõi)"
        
        disk_bar = self._make_progress_bar(metrics.disk_usage_percent)
        disk_str = f"{BOLD}DSK:{RESET} {metrics.disk_usage_percent:4.1f}% | Đọc: ↑{self._format_speed(metrics.disk_write_kbps)} ↓{self._format_speed(metrics.disk_read_kbps)} [{disk_bar}]"
        lines.append(f"║ {gpu_str.ljust(76)} ║")
        lines.append(f"║ {disk_str.ljust(76)} ║")

        # Metrics Row 3: Network & Uptime
        net_str = f"{BOLD}NET:{RESET} ↑Up: {self._format_speed(metrics.network_sent_kbps):>9} | ↓Down: {self._format_speed(metrics.network_received_kbps):>9} ({self.settings.selected_network_adapter})"
        uptime_str = f"{BOLD}Uptime:{RESET} {self._format_uptime(metrics.uptime_seconds)} (Cores: {metrics.logical_processors})"
        lines.append(f"║ {net_str.ljust(76)} ║")
        lines.append(f"║ {uptime_str.ljust(76)} ║")

        # Log Section
        lines.append(f"{CYAN}╟──────────────────────────────────────────────────────────────────────────────╢{RESET}")
        lines.append(f"║ {BOLD}Nhật ký hoạt động (Logs):{RESET}".ljust(85) + "║")
        for log in self._logs[-4:]:
            clean_log = (log[:74] + "...") if len(log) > 74 else log
            lines.append(f"║ {DIM}{clean_log.ljust(76)}{RESET} ║")
        for _ in range(4 - min(4, len(self._logs))):
            lines.append(f"║ {''.ljust(76)} ║")

        lines.append(f"{CYAN}{BOLD}╚══════════════════════════════════════════════════════════════════════════════╝{RESET}")
        lines.append(f"{DIM}Nhấn Ctrl+C để thoát ứng dụng an toàn.{RESET}")

        # Clear and redraw
        sys.stdout.write("\033[H\033[J")
        sys.stdout.write("\n".join(lines) + "\n")
        sys.stdout.flush()

    def _make_progress_bar(self, percent: float, width: int = 12) -> str:
        clamped = max(0.0, min(100.0, percent))
        filled = int(round((clamped / 100.0) * width))
        empty = width - filled
        return "█" * filled + "░" * empty

    async def run(self):
        """Khởi động vòng lặp CLI."""
        self._running = True
        self._on_log("🚀 Khởi chạy Linux Monitor BLE (Chế độ CLI)...")

        # Bật kết nối BLE nếu được cấu hình
        if self.settings.auto_connect:
            asyncio.create_task(self.ble.connect(self.settings.last_connected_address))

        try:
            interval_sec = max(0.2, self.settings.send_interval_ms / 1000.0)
            while self._running:
                # 1. Thu thập dữ liệu
                metrics = self.collector.collect()
                self._current_metrics = metrics

                # 2. Gửi sang ESP32 qua BLE nếu đang kết nối
                if self.ble.state == BleState.CONNECTED:
                    await self.ble.send_metrics(metrics)

                # 3. Vẽ Dashboard ra màn hình terminal
                self._render_dashboard(metrics)

                await asyncio.sleep(interval_sec)

        except asyncio.CancelledError:
            pass
        finally:
            self._on_log("Đang ngắt kết nối và giải phóng tài nguyên...")
            await self.ble.disconnect()
            print("\nĐã thoát chương trình an toàn. Tạm biệt!\n")

    def stop(self):
        self._running = False
