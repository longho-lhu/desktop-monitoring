#!/usr/bin/env bash
# ==============================================================================
#  Linux Monitor BLE — Setup & Installer Script for Ubuntu 26 / 24+
# ==============================================================================

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo "======================================================================"
echo "  🐧 ĐANG CÀI ĐẶT LINUX MONITOR BLE CHO UBUNTU / LINUX"
echo "======================================================================"

# 1. Cài đặt các gói hệ thống cần thiết (BlueZ, Python3-venv, Tkinter)
echo "[1/4] Kiểm tra và cài đặt gói hệ thống..."
if command -v apt-get &>/dev/null; then
    sudo apt-get update -qq
    sudo apt-get install -y -qq python3 python3-pip python3-venv python3-tk bluez bluetooth
elif command -v dnf &>/dev/null; then
    sudo dnf install -y python3 python3-pip python3-tkinter bluez
elif command -v pacman &>/dev/null; then
    sudo pacman -Sy --noconfirm python python-pip tk bluez bluez-utils
fi

# 2. Khởi tạo Virtual Environment
echo "[2/4] Thiết lập môi trường ảo Python (venv)..."
if [ ! -d ".venv" ]; then
    python3 -m venv .venv
fi

# 3. Cài đặt Python Dependencies
echo "[3/4] Cài đặt các thư viện Python..."
.venv/bin/pip install --upgrade pip -q
.venv/bin/pip install -r requirements.txt -q

# Cấp quyền thực thi cho các script
chmod +x main.py run.sh run_cli.sh install.sh 2>/dev/null || true

# 4. Tạo Desktop Entry (.desktop) cho Ubuntu Application Menu
echo "[4/4] Đăng ký biểu tượng ứng dụng vào Ubuntu Menu..."
DESKTOP_DIR="$HOME/.local/share/applications"
mkdir -p "$DESKTOP_DIR"

cat <<EOF > "$DESKTOP_DIR/linux-monitor-ble.desktop"
[Desktop Entry]
Name=Linux Monitor BLE
Comment=ESP32-C3 OLED Hardware Telemetry Monitor
Exec=$SCRIPT_DIR/run.sh
Icon=utilities-system-monitor
Terminal=false
Type=Application
Categories=System;Monitor;HardwareSettings;
Keywords=monitor;ble;esp32;telemetry;hardware;
StartupNotify=true
EOF

chmod +x "$DESKTOP_DIR/linux-monitor-ble.desktop" 2>/dev/null || true

echo "======================================================================"
echo "  ✅ CÀI ĐẶT HOÀN TẤT THÀNH CÔNG!"
echo "======================================================================"
echo "  - Chạy Desktop GUI:          ./run.sh"
echo "  - Chạy Terminal Live CLI:    ./run_cli.sh"
echo "  - Chạy Headless Daemon:      .venv/bin/python3 main.py --daemon"
echo "======================================================================"
