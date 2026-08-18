"""
AppSettings: Quản lý cấu hình ứng dụng Linux Monitor BLE.
Hỗ trợ lưu/tải cấu hình dạng file JSON (config.json).
"""

import json
import os
from dataclasses import asdict, dataclass
from typing import Optional


@dataclass
class AppSettings:
    # BLE
    target_device_name: str = "ESP32Monitor"
    last_connected_address: Optional[str] = None
    send_interval_ms: int = 1000
    auto_connect: bool = True
    auto_send_on_connect: bool = True
    auto_reconnect: bool = True

    # Telemetry
    selected_network_adapter: str = "Auto"
    selected_disk_device: str = "All"
    selected_disk_mount: str = "/"
    enable_gpu_monitoring: bool = True

    # UI / System
    theme: str = "dark"
    log_level: str = "INFO"
    start_with_linux: bool = False
    minimize_to_tray: bool = False

    @classmethod
    def load(cls, config_path: Optional[str] = None) -> "AppSettings":
        """Tải cấu hình từ file JSON hoặc trả về mặc định nếu chưa có."""
        path = config_path or cls.get_default_config_path()
        settings = cls()
        if os.path.exists(path):
            try:
                with open(path, "r", encoding="utf-8") as f:
                    data = json.load(f)
                    valid_keys = {k: v for k, v in data.items() if hasattr(cls, k)}
                    settings = cls(**valid_keys)
            except Exception as e:
                print(f"[WARN] Không thể đọc cấu hình từ {path}: {e}")

        # Đồng bộ trạng thái autostart thực tế từ hệ thống Linux
        try:
            from ..services.autostart_service import AutostartService
            settings.start_with_linux = AutostartService.is_autostart_enabled()
        except Exception:
            try:
                from services.autostart_service import AutostartService
                settings.start_with_linux = AutostartService.is_autostart_enabled()
            except Exception:
                pass

        return settings

    def save(self, config_path: Optional[str] = None) -> bool:
        """Lưu cấu hình hiện tại vào file JSON và cập nhật Autostart."""
        path = config_path or self.get_default_config_path()
        try:
            os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
            with open(path, "w", encoding="utf-8") as f:
                json.dump(asdict(self), f, indent=4, ensure_ascii=False)

            # Cập nhật Autostart hệ thống
            try:
                from ..services.autostart_service import AutostartService
                AutostartService.set_autostart(self.start_with_linux, run_in_background=self.minimize_to_tray)
            except Exception:
                try:
                    from services.autostart_service import AutostartService
                    AutostartService.set_autostart(self.start_with_linux, run_in_background=self.minimize_to_tray)
                except Exception:
                    pass

            return True
        except Exception as e:
            print(f"[ERROR] Không thể lưu cấu hình vào {path}: {e}")
            return False

    @staticmethod
    def get_default_config_path() -> str:
        """Đường dẫn lưu config.json mặc định tại thư mục chạy hoặc ~/.config/linux-monitor-ble/."""
        # Ưu tiên thư mục hiện tại của app
        local_path = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "config.json")
        if os.access(os.path.dirname(local_path), os.W_OK):
            return local_path
        
        # Fallback thư mục ~/.config
        user_config_dir = os.path.expanduser("~/.config/linux-monitor-ble")
        return os.path.join(user_config_dir, "config.json")
