"""
AutostartService: Quản lý tự động khởi động cùng Linux (XDG Autostart & Systemd User Service).
Hoạt động tương đương với Windows Registry HKCU\\Run trên Windows.
"""

import os
import subprocess
import sys
from pathlib import Path


class AutostartService:
    APP_NAME = "linux-monitor-ble"
    AUTOSTART_DIR = os.path.expanduser("~/.config/autostart")
    AUTOSTART_FILE = os.path.join(AUTOSTART_DIR, f"{APP_NAME}.desktop")

    @classmethod
    def get_run_script_path(cls) -> str:
        """Lấy đường dẫn tuyệt đối đến file run.sh."""
        base_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
        run_sh = os.path.join(base_dir, "run.sh")
        if os.path.exists(run_sh):
            return run_sh
        return os.path.join(base_dir, "main.py")

    @classmethod
    def is_autostart_enabled(cls) -> bool:
        """Kiểm tra ứng dụng có đang được kích hoạt tự khởi động cùng Linux hay không."""
        return os.path.exists(cls.AUTOSTART_FILE)

    @classmethod
    def set_autostart(cls, enable: bool, run_in_background: bool = False) -> bool:
        """
        Bật hoặc tắt tự động khởi động cùng Linux.
        Tự động tạo hoặc xóa file ~/.config/autostart/linux-monitor-ble.desktop.
        """
        try:
            if enable:
                os.makedirs(cls.AUTOSTART_DIR, exist_ok=True)
                run_script = cls.get_run_script_path()
                exec_cmd = f"bash \"{run_script}\""
                if run_in_background:
                    exec_cmd += " --daemon"

                content = f"""[Desktop Entry]
Type=Application
Version=1.0
Name=Linux Monitor BLE (AutoStart)
Comment=Tự động khởi động giám sát phần cứng truyền ESP32 BLE
Exec={exec_cmd}
Icon=utilities-system-monitor
Terminal=false
StartupNotify=false
X-GNOME-Autostart-enabled=true
"""
                with open(cls.AUTOSTART_FILE, "w", encoding="utf-8") as f:
                    f.write(content)

                os.chmod(cls.AUTOSTART_FILE, 0o755)
                return True
            else:
                if os.path.exists(cls.AUTOSTART_FILE):
                    os.remove(cls.AUTOSTART_FILE)
                return True
        except Exception as e:
            print(f"[ERROR] Không thể cập nhật autostart: {e}")
            return False
