# Desktop Monitoring BLE — ESP32-C3 System Telemetry (Windows & Linux)

Hệ thống giám sát phần cứng thời gian thực dành cho **Windows** và **Linux (Ubuntu 26 / 24+ / Debian / Fedora / Arch / Raspberry Pi)**, truyền toàn bộ thông số (CPU, RAM, GPU, Ổ đĩa, Card mạng, Uptime) sang vi điều khiển **ESP32 / ESP32-C3** qua **Bluetooth Low Energy (BLE GATT)** để hiển thị lên màn hình **OLED SSD1306 (128x64 I2C)**.

---

## 🏗️ Kiến trúc hoạt động đa nền tảng

```
┌─────────────────────────────────┐
│ Windows PC                      │
│ (C# .NET 10 WinForms)           │
└──────────────┬──────────────────┘
               │  BLE GATT Write (32 Bytes)
               ▼
┌─────────────────────────────────┐         ┌─────────────────────────┐
│ ESP32 / ESP32-C3 Firmware       │ ──────► │ OLED SSD1306 128x64 I2C │
│ (ESP32_Firmware_Example.ino)    │         │ (Giao diện 2 cột cân đối)│
└─────────────────────────────────┘         └─────────────────────────┘
               ▲
               │  BLE GATT Write (32 Bytes)
┌──────────────┴──────────────────┐
│ Linux PC (Ubuntu 26 / 24+)      │
│ (Python 3 + Bleak + Dark GUI)   │
└─────────────────────────────────┘
```

---

## 📡 Cấu hình BLE UUIDs (Dùng chung cho Windows, Linux & ESP32)

| Loại | UUID | Chức năng |
| :--- | :--- | :--- |
| **Service UUID** | `12345678-0000-1000-8000-00805F9B34FB` | Dịch vụ chính quảng bá BLE |
| **Metrics RX (Write)** | `12345678-0001-1000-8000-00805F9B34FB` | Ghi gói tin 32 bytes thông số vào đây |
| **Status TX (Notify)** | `12345678-0002-1000-8000-00805F9B34FB` | ESP32 gửi phản hồi về PC (tùy chọn) |
| **Tên thiết bị BLE** | `ESP32Monitor` | Tên tìm kiếm mặc định khi quét BLE |

---

## 📦 Cấu trúc gói tin nhị phân (32 Bytes Binary Packet)

| Byte Index | Kiểu dữ liệu | Ý nghĩa | Ghi chú |
| :---: | :--- | :--- | :--- |
| `[0]` | `uint8` | Header cố định | Luôn là `0xA5` |
| `[1]` | `uint8` | CPU Usage | `0 – 100%` |
| `[2]` | `uint8` | CPU Temp | `0 – 254°C` (`0xFF` = N/A) |
| `[3..4]` | `uint16` (LE) | RAM Used MB | Giá trị × 10 = MB |
| `[5..6]` | `uint16` (LE) | RAM Total MB | Giá trị × 10 = MB |
| `[7]` | `uint8` | RAM Usage | `0 – 100%` |
| `[8]` | `uint8` | GPU Usage | `0 – 100%` (`0xFF` = N/A) |
| `[9]` | `uint8` | GPU Temp | `0 – 254°C` (`0xFF` = N/A) |
| `[10..11]`| `uint16` (LE) | Disk Read KB/s | Tốc độ đọc ổ đĩa |
| `[12..13]`| `uint16` (LE) | Disk Write KB/s | Tốc độ ghi ổ đĩa |
| `[14]` | `uint8` | Disk Usage | `0 – 100%` dung lượng ổ đã dùng |
| `[15..16]`| `uint16` (LE) | Net Sent KB/s | Tốc độ Upload |
| `[17..18]`| `uint16` (LE) | Net Received KB/s| Tốc độ Download |
| `[19..22]`| `uint32` (LE) | Uptime Seconds | Thời gian hoạt động (giây) |
| `[23]` | `uint8` | CPU Frequency | Giá trị × 100 = MHz |
| `[24..30]`| `uint8[7]` | Reserved | Dự phòng (`0x00`) |
| `[31]` | `uint8` | Checksum XOR | `byte[0] ^ byte[1] ^ ... ^ byte[30]` |

---

## 🐧 Hướng dẫn cài đặt & Khởi chạy trên Linux / Ubuntu 26

### 1. Cài đặt tự động (1 lệnh duy nhất)
Mở terminal tại thư mục `LinuxMonitorBLE/` và chạy:
```bash
cd LinuxMonitorBLE
bash install.sh
```
Script sẽ tự động cài đặt `python3-venv`, `python3-tk`, `bluez`, tạo môi trường ảo `.venv`, cài đặt thư viện `bleak`, `psutil`, `pynvml` và tạo phím tắt Desktop Entry trong Ubuntu Menu.

### 2. Các cách khởi chạy trên Linux:
* **Giao diện Desktop GUI (Khuyên dùng):**
  ```bash
  ./run.sh
  ```
  *(Hoặc nhấp mở biểu tượng **Linux Monitor BLE** trong menu ứng dụng của Ubuntu)*
* **Giao diện Terminal Live Dashboard (Dành cho CLI / SSH / Server):**
  ```bash
  ./run_cli.sh
  ```
* **Chạy ngầm dạng Daemon (Tự khởi động cùng Linux qua Systemd):**
  ```bash
  mkdir -p ~/.config/systemd/user/
  cp linux_monitor_ble.service ~/.config/systemd/user/
  systemctl --user daemon-reload
  systemctl --user enable --now linux_monitor_ble.service
  ```

---

## 🪟 Hướng dẫn khởi chạy trên Windows

* **Chạy với quyền Administrator (Đọc đầy đủ Nhiệt độ CPU/GPU):** Nhấp đúp chuột vào file **`run_admin.bat`**.
* **Chạy thông thường:** Nhấp đúp chuột vào file **`run.bat`**.

---

## 🔌 Nối dây ESP32-C3 với OLED SSD1306 0.96" I2C

| OLED SSD1306 | ESP32-C3 (Chuẩn) | ESP32-C3 SuperMini / LuatOS |
| :---: | :---: | :---: |
| **VCC** | **3.3V** | **3.3V** |
| **GND** | **GND** | **GND** |
| **SCL** | **GPIO 9** | **GPIO 5** |
| **SDA** | **GPIO 8** | **GPIO 4** |

> Mở file **`ESP32_Firmware_Example.ino`** trong Arduino IDE (cài thư viện `Adafruit SSD1306` + `Adafruit GFX`) và nạp vào ESP32.

---

## 📂 Cấu trúc mã nguồn dự án

```
desktop-monitoring/
├── ESP32_Firmware_Example.ino       # Firmware C++ hoàn chỉnh cho ESP32-C3 + OLED
├── README.md                        # Tài liệu hướng dẫn đa nền tảng
├── .gitignore                       # Chặn file rác
│
├── LinuxMonitorBLE/                 # 🐧 Ứng dụng Linux (Ubuntu 26+)
│   ├── main.py                      # Điểm vào chính (--gui, --cli, --daemon)
│   ├── requirements.txt             # Dependencies (bleak, psutil, pynvml)
│   ├── install.sh                   # Script cài đặt tự động 1-click
│   ├── run.sh                       # Khởi chạy Desktop GUI
│   ├── run_cli.sh                   # Khởi chạy Terminal Live CLI
│   ├── linux_monitor_ble.service    # Systemd service chạy ngầm
│   ├── models/                      # Data models & 32-byte BLE packeting
│   ├── services/                    # Metrics collector (/sys, /proc) & BLE client (Bleak)
│   ├── ui/                          # Tkinter Dark GUI & Rich CLI Dashboard
│   └── tests/                       # Unit tests kiểm tra tương thích gói tin
│
└── WindowsMonitorBLE/               # 🪟 Ứng dụng Windows (C# .NET 10 WinForms)
    ├── WindowsMonitorBLE.sln
    ├── run.bat
    ├── run_admin.bat
    └── WindowsMonitorBLE/
```
