#!/usr/bin/env bash
# ==============================================================================
#  Khởi chạy nhanh Linux Monitor BLE (Desktop GUI)
# ==============================================================================

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

if [ ! -d ".venv" ]; then
    echo "Môi trường ảo chưa được cài đặt. Đang tự động chạy install.sh..."
    bash install.sh
fi

exec .venv/bin/python3 main.py "$@"
