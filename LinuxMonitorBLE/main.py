#!/usr/bin/env python3
"""
Linux Monitor BLE — Main Entry Point
Hỗ trợ cả chế độ Desktop GUI và Terminal CLI / Headless Daemon.

Sử dụng:
  python3 main.py          # Mặc định mở GUI (nếu có môi trường đồ họa) hoặc CLI
  python3 main.py --gui    # Ép buộc mở Desktop GUI
  python3 main.py --cli    # Chạy giao diện Live Dashboard trên Terminal
  python3 main.py --daemon # Chạy ngầm không giao diện (phù hợp systemd)
"""

import argparse
import asyncio
import os
import signal
import sys

# Ensure UTF-8 output encoding across all terminals
if sys.stdout.encoding != "utf-8":
    try:
        sys.stdout.reconfigure(encoding="utf-8")
        sys.stderr.reconfigure(encoding="utf-8")
    except Exception:
        pass

# Ensure current directory is in sys.path
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import tkinter as tk

from models.app_settings import AppSettings
from ui.cli_app import CliApp
from ui.gui_app import GuiApp


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Linux Monitor BLE — ESP32 Telemetry Monitor for Ubuntu 26 / Linux"
    )
    mode_group = parser.add_mutually_exclusive_group()
    mode_group.add_argument(
        "--gui",
        action="store_true",
        help="Khởi chạy giao diện đồ họa Desktop GUI (mặc định)",
    )
    mode_group.add_argument(
        "--cli",
        action="store_true",
        help="Khởi chạy giao diện Terminal Live Dashboard",
    )
    mode_group.add_argument(
        "--daemon",
        action="store_true",
        help="Khởi chạy ở chế độ chạy ngầm (Headless Daemon cho systemd)",
    )

    parser.add_argument(
        "--interval",
        type=int,
        default=None,
        help="Chu kỳ gửi dữ liệu sang ESP32 (ms), mặc định 1000",
    )
    parser.add_argument(
        "--device",
        type=str,
        default=None,
        help="Tên thiết bị BLE ESP32 (mặc định: ESP32Monitor)",
    )
    parser.add_argument(
        "--adapter",
        type=str,
        default=None,
        help="Tên card mạng cần theo dõi (mặc định: Auto)",
    )
    parser.add_argument(
        "--mount",
        type=str,
        default=None,
        help="Điểm gắn phân vùng ổ đĩa cần theo dõi %% (mặc định: /)",
    )
    parser.add_argument(
        "--no-gpu",
        action="store_true",
        help="Tắt thu thập thông số GPU",
    )

    return parser.parse_args()


def has_display() -> bool:
    """Kiểm tra hệ thống có môi trường hiển thị đồ họa X11 / Wayland hay không."""
    return bool(os.environ.get("DISPLAY") or os.environ.get("WAYLAND_DISPLAY"))


def main():
    args = parse_arguments()

    # Tải cấu hình
    settings = AppSettings.load()

    # Ghi đè cấu hình nếu có tham số dòng lệnh
    if args.interval is not None:
        settings.send_interval_ms = args.interval
    if args.device is not None:
        settings.target_device_name = args.device
    if args.adapter is not None:
        settings.selected_network_adapter = args.adapter
    if args.mount is not None:
        settings.selected_disk_mount = args.mount
    if args.no_gpu:
        settings.enable_gpu_monitoring = False

    # Xác định chế độ chạy
    if args.daemon:
        run_daemon_mode(settings)
    elif args.cli:
        run_cli_mode(settings)
    elif args.gui:
        run_gui_mode(settings)
    else:
        # Tự động chọn GUI nếu có màn hình, ngược lại chọn CLI
        if has_display():
            try:
                run_gui_mode(settings)
            except Exception as e:
                print(f"[WARN] Không thể khởi động GUI ({e}), chuyển sang chế độ CLI...")
                run_cli_mode(settings)
        else:
            run_cli_mode(settings)


def run_gui_mode(settings: AppSettings):
    """Khởi chạy ứng dụng GUI với Tkinter."""
    root = tk.Tk()
    app = GuiApp(root, settings)
    root.mainloop()


def run_cli_mode(settings: AppSettings):
    """Khởi chạy ứng dụng Terminal Live Dashboard."""
    cli = CliApp(settings)

    def sig_handler(sig, frame):
        cli.stop()

    signal.signal(signal.SIGINT, sig_handler)
    signal.signal(signal.SIGTERM, sig_handler)

    asyncio.run(cli.run())


def run_daemon_mode(settings: AppSettings):
    """Khởi chạy ứng dụng dạng Headless Daemon (không vẽ UI)."""
    from services.ble_manager import BleManager, BleState
    from services.metrics_collector import LinuxMetricsCollector

    collector = LinuxMetricsCollector(
        network_adapter=settings.selected_network_adapter,
        disk_device=settings.selected_disk_device,
        disk_mount=settings.selected_disk_mount,
        enable_gpu=settings.enable_gpu_monitoring,
    )
    ble = BleManager(target_name=settings.target_device_name)
    ble.auto_reconnect = True

    running = True

    def stop_daemon(sig, frame):
        nonlocal running
        running = False

    signal.signal(signal.SIGINT, stop_daemon)
    signal.signal(signal.SIGTERM, stop_daemon)

    async def daemon_loop():
        print(f"🐧 Linux Monitor BLE Daemon đã khởi động (Mục tiêu: {settings.target_device_name})...")
        asyncio.create_task(ble.connect(settings.last_connected_address))

        interval = max(0.2, settings.send_interval_ms / 1000.0)
        while running:
            metrics = collector.collect()
            if ble.state == BleState.CONNECTED:
                await ble.send_metrics(metrics)
            await asyncio.sleep(interval)

        await ble.disconnect()
        print("Daemon đã dừng an toàn.")

    asyncio.run(daemon_loop())


if __name__ == "__main__":
    main()
