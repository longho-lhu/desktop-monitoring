# Windows Monitor BLE — ESP32-C3 System Telemetry

Ứng dụng Windows thu thập thông số phần cứng thời gian thực (CPU, RAM, GPU, Ổ đĩa, Card mạng) và truyền sang vi điều khiển **ESP32 / ESP32-C3** qua Bluetooth Low Energy (BLE GATT) để hiển thị lên màn hình **OLED SSD1306 (128x64 I2C)**.

---

## 🏗️ Kiến trúc hoạt động

```
┌─────────────────────────┐          BLE GATT           ┌─────────────────────────┐
│   Windows PC            │ ─────── Write (32B) ──────► │   ESP32-C3              │
│   (C# .NET 10 WinForms) │ ◄────── Notify (ACK) ────── │   + OLED SSD1306 128x64 │
└─────────────────────────┘                             └─────────────────────────┘
```

---

## 📡 Cấu hình BLE UUIDs

| Loại | UUID | Chức năng |
| :--- | :--- | :--- |
| **Service UUID** | `12345678-0000-1000-8000-00805F9B34FB` | Dịch vụ chính quảng bá BLE |
| **Metrics RX (Write)** | `12345678-0001-1000-8000-00805F9B34FB` | PC ghi 32 bytes thông số vào đây |
| **Status TX (Notify)** | `12345678-0002-1000-8000-00805F9B34FB` | ESP32 gửi phản hồi về PC (tùy chọn) |
| **Tên thiết bị BLE** | `ESP32Monitor` | Tên tìm kiếm khi PC quét BLE |

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

## 🚀 Khởi chạy ứng dụng Windows

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

## 📂 Cấu trúc mã nguồn tinh gọn

```
desktop-monitoring/
├── WindowsMonitorBLE.sln             # Visual Studio Solution
├── run.bat                          # File khởi chạy nhanh
├── run_admin.bat                    # File khởi chạy cấp quyền Administrator
├── ESP32_Firmware_Example.ino       # Firmware C++ hoàn chỉnh cho ESP32-C3 + OLED
├── README.md                        # Tài liệu hướng dẫn
├── .gitignore                       # Chặn file build rác
└── WindowsMonitorBLE/               # Project C# .NET 10 WinForms duy nhất
    ├── WindowsMonitorBLE.csproj
    ├── app.manifest
    ├── Program.cs
    ├── MainForm.cs
    ├── MainForm.Designer.cs
    ├── SettingsForm.cs
    ├── Models/
    │   ├── AppSettings.cs
    │   └── SystemMetrics.cs
    └── Services/
        ├── BleService.cs
        ├── SettingsService.cs
        └── SystemMetricsService.cs
```
