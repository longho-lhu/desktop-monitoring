# Windows Monitor BLE — Tài liệu tích hợp ESP32-C3

## Tổng quan kiến trúc

```
┌─────────────────────┐        BLE GATT         ┌──────────────────┐
│   Windows PC        │ ───── Write ──────────► │   ESP32-C3       │
│   (C# App)          │ ◄──── Notify ─────────── │   (Peripheral)   │
└─────────────────────┘                          └──────────────────┘
  Thu thập: CPU, RAM,                              Nhận gói 32 byte
  GPU, Disk, Network                               Hiển thị / xử lý
```

---

## UUID BLE (cần cấu hình giống nhau ở cả 2 phía)

| Loại               | UUID                                   | Property              |
|--------------------|----------------------------------------|-----------------------|
| **Service**        | `12345678-0000-1000-8000-00805F9B34FB` | —                     |
| **Metrics (Write)**| `12345678-0001-1000-8000-00805F9B34FB` | Write Without Response|
| **Status (Notify)**| `12345678-0002-1000-8000-00805F9B34FB` | Notify (tuỳ chọn)     |

---

## Cấu trúc gói tin BLE (32 bytes)

| Byte  | Nội dung                  | Đơn vị / Ghi chú                     |
|-------|---------------------------|---------------------------------------|
| 0     | Header `0xA5`             | Để nhận diện gói hợp lệ              |
| 1     | CPU usage %               | 0–100                                 |
| 2     | CPU temperature           | °C, `0xFF` = không có dữ liệu        |
| 3–4   | RAM used (little-endian)  | uint16 × 10 MB (max 655,350 MB)      |
| 5–6   | RAM total (little-endian) | uint16 × 10 MB                       |
| 7     | RAM usage %               | 0–100                                 |
| 8     | GPU usage %               | 0–100, `0xFF` = N/A                   |
| 9     | GPU temperature           | °C, `0xFF` = N/A                      |
| 10–11 | Disk read (little-endian) | uint16 KB/s                           |
| 12–13 | Disk write (little-endian)| uint16 KB/s                           |
| 14    | Disk C: usage %           | 0–100                                 |
| 15–16 | Net sent (little-endian)  | uint16 KB/s                           |
| 17–18 | Net recv (little-endian)  | uint16 KB/s                           |
| 19–22 | Uptime (little-endian)    | uint32 giây                           |
| 23    | CPU freq                  | MHz / 100 (VD: 35 = 3500 MHz)        |
| 24–30 | Reserved                  | Để mở rộng sau                       |
| 31    | Checksum XOR              | XOR của byte 0–30                    |

---

## Ví dụ code ESP32-C3 (Arduino)

```cpp
#include <BLEDevice.h>
#include <BLEServer.h>
#include <BLEUtils.h>
#include <BLE2902.h>

#define SERVICE_UUID      "12345678-0000-1000-8000-00805F9B34FB"
#define METRICS_CHAR_UUID "12345678-0001-1000-8000-00805F9B34FB"
#define STATUS_CHAR_UUID  "12345678-0002-1000-8000-00805F9B34FB"

BLEServer*         pServer         = nullptr;
BLECharacteristic* pMetricsChar    = nullptr;
BLECharacteristic* pStatusChar     = nullptr;
bool               deviceConnected = false;

class ServerCallbacks : public BLEServerCallbacks {
  void onConnect(BLEServer* s)    { deviceConnected = true; }
  void onDisconnect(BLEServer* s) { deviceConnected = false; s->startAdvertising(); }
};

class MetricsCallbacks : public BLECharacteristicCallbacks {
  void onWrite(BLECharacteristic* pChar) {
    uint8_t* data = pChar->getData();
    size_t   len  = pChar->getLength();
    if (len != 32 || data[0] != 0xA5) return;

    // Kiểm tra checksum XOR
    uint8_t chk = 0;
    for (int i = 0; i < 31; i++) chk ^= data[i];
    if (chk != data[31]) return;

    // Giải mã
    uint8_t  cpu_pct   = data[1];
    uint8_t  cpu_temp  = data[2];   // 0xFF = N/A
    uint16_t ram_used  = data[3] | (data[4] << 8);   // x10 MB
    uint16_t ram_total = data[5] | (data[6] << 8);   // x10 MB
    uint8_t  ram_pct   = data[7];
    uint8_t  gpu_pct   = data[8];   // 0xFF = N/A
    uint8_t  gpu_temp  = data[9];   // 0xFF = N/A
    uint16_t disk_r    = data[10] | (data[11] << 8); // KB/s
    uint16_t disk_w    = data[12] | (data[13] << 8); // KB/s
    uint8_t  disk_pct  = data[14];
    uint16_t net_tx    = data[15] | (data[16] << 8); // KB/s
    uint16_t net_rx    = data[17] | (data[18] << 8); // KB/s
    uint32_t uptime    = (uint32_t)data[19] | ((uint32_t)data[20]<<8)
                       | ((uint32_t)data[21]<<16) | ((uint32_t)data[22]<<24);
    uint16_t cpu_mhz   = data[23] * 100;

    Serial.printf("CPU:%d%% %dC | RAM:%d%% | GPU:%d%% %dC | Net TX:%d RX:%d KB/s\n",
      cpu_pct, cpu_temp, ram_pct, gpu_pct, gpu_temp, net_tx, net_rx);

    // Gửi ACK về PC
    if (pStatusChar && deviceConnected) {
      char resp[16];
      snprintf(resp, sizeof(resp), "OK");
      pStatusChar->setValue((uint8_t*)resp, strlen(resp));
      pStatusChar->notify();
    }
  }
};

void setup() {
  Serial.begin(115200);
  BLEDevice::init("ESP32Monitor");  // ← tên hiện trong app C#

  pServer = BLEDevice::createServer();
  pServer->setCallbacks(new ServerCallbacks());

  BLEService* svc = pServer->createService(SERVICE_UUID);

  pMetricsChar = svc->createCharacteristic(METRICS_CHAR_UUID,
    BLECharacteristic::PROPERTY_WRITE_NR);
  pMetricsChar->setCallbacks(new MetricsCallbacks());

  pStatusChar = svc->createCharacteristic(STATUS_CHAR_UUID,
    BLECharacteristic::PROPERTY_NOTIFY);
  pStatusChar->addDescriptor(new BLE2902());

  svc->start();
  BLEDevice::getAdvertising()->addServiceUUID(SERVICE_UUID);
  BLEDevice::startAdvertising();
}

void loop() { delay(10); }
```

---

## Build & Chạy app C#

```powershell
# Yêu cầu: .NET 10 SDK + Windows 10/11 Build 19041+
cd WindowsMonitorBLE
dotnet restore
dotnet run
# Hoặc build release:
dotnet publish -c Release -r win-x64 --self-contained
```

> **Lưu ý:** Cần chạy với quyền **Administrator** để đọc nhiệt độ CPU/GPU.

---

## Cấu trúc project

```
desktop-monitoring/
├── WindowsMonitorBLE.sln
├── run.bat                         (1-Click chạy nhanh)
├── bin/
│   └── WindowsMonitorBLE.exe       (Bản publish chạy ngay)
└── WindowsMonitorBLE/
    ├── WindowsMonitorBLE.csproj    (.NET 10, WinForms)
    ├── app.manifest                (asInvoker)
    ├── Program.cs                  (Entry point [STAThread] + error handling)
    ├── MainForm.cs                 (UI logic + BLE/Timer events)
    ├── MainForm.Designer.cs        (UI layout dark theme)
    ├── SettingsForm.cs             (Cửa sổ Cài đặt: Autostart, Card mạng, Ổ đĩa)
    ├── Models/
    │   ├── AppSettings.cs          (Cấu hình lưu trữ)
    │   └── SystemMetrics.cs        (Data model + 32-byte BLE serializer)
    └── Services/
        ├── SettingsService.cs      (Đọc/ghi JSON + Windows Registry Run key)
        ├── SystemMetricsService.cs (PerformanceCounter + LHM + P/Invoke RAM)
        └── BleService.cs           (Windows BLE GATT Central)
```
