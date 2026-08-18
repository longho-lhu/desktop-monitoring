/**
 * ============================================================================
 *  ESP32-C3 + OLED SSD1306 0.96" (128x64 I2C) - Windows Monitor BLE
 * ============================================================================
 *  Giao diện 2 Cột (Card 2 bên) đối xứng và cân đều 100%:
 * 
 *  ┌─────────────────────────────┬─────────────────────────────┐
 *  │ CPU 45%                 58C │ RAM 62%               10.8G │
 *  │ [██████████░░░░░░░░░░░░░░░] │ [██████████████░░░░░░░░░░░] │
 *  ├─────────────────────────────┼─────────────────────────────┤
 *  │ GPU 30%                 50C │ DSK 72%                3.2M │
 *  │ [███████░░░░░░░░░░░░░░░░░░] │ [████████████████░░░░░░░░░] │
 *  ├─────────────────────────────┴─────────────────────────────┤
 *  │ ▲ 120K   ▼ 1.5M                    ⏱ 02:45:12    [BLE]   │
 *  └───────────────────────────────────────────────────────────┘
 * ============================================================================
 */

#include <Arduino.h>
#include <Wire.h>
#include <Adafruit_GFX.h>
#include <Adafruit_SSD1306.h>

#include <BLEDevice.h>
#include <BLEServer.h>
#include <BLEUtils.h>
#include <BLE2902.h>

// ── 1. CẤU HÌNH PHẦN CỨNG I2C & OLED ────────────────────────────────────────
#define SCREEN_WIDTH  128
#define SCREEN_HEIGHT 64
#define OLED_RESET    -1
#define SCREEN_ADDRESS 0x3C  // Địa chỉ I2C OLED (0x3C hoặc 0x3D)

// Chọn chân I2C cho ESP32-C3:
// Board chuẩn ESP32-C3: SDA = 8, SCL = 9
// Board SuperMini / LuatOS: SDA = 4, SCL = 5
#define I2C_SDA 8
#define I2C_SCL 9

Adafruit_SSD1306 display(SCREEN_WIDTH, SCREEN_HEIGHT, &Wire, OLED_RESET);

// ── 2. CẤU HÌNH BLE UUIDs ───────────────────────────────────────────────────
#define SERVICE_UUID           "12345678-0000-1000-8000-00805F9B34FB"
#define CHARACTERISTIC_UUID_RX "12345678-0001-1000-8000-00805F9B34FB"
#define CHARACTERISTIC_UUID_TX "12345678-0002-1000-8000-00805F9B34FB"
#define DEVICE_NAME            "ESP32Monitor"

// ── 3. CẤU TRÚC DỮ LIỆU TELEMETRY ───────────────────────────────────────────
struct SystemTelemetry {
    uint8_t  cpuUsage;        // % CPU (0 - 100)
    int16_t  cpuTemp;         // °C (-1 nếu không có sensor)
    uint32_t ramUsedMB;       // MB RAM đang dùng
    uint32_t ramTotalMB;      // MB Tổng RAM
    uint8_t  ramUsage;        // % RAM (0 - 100)
    int16_t  gpuUsage;        // % GPU (-1 nếu không có)
    int16_t  gpuTemp;         // °C GPU (-1 nếu không có)
    uint16_t diskReadKBps;    // KB/s
    uint16_t diskWriteKBps;   // KB/s
    uint8_t  diskUsage;       // % Dung lượng ổ
    uint16_t netSentKBps;     // KB/s (Upload)
    uint16_t netRecvKBps;     // KB/s (Download)
    uint32_t uptimeSeconds;   // Giây
    uint16_t cpuFreqMHz;      // MHz
};

SystemTelemetry currentStats;
volatile bool hasNewData = false;
bool isConnected = false;
unsigned long lastPacketTime = 0;

// ── 4. HÀM GIẢI MÃ GÓI TIN 32 BYTES ─────────────────────────────────────────
bool parseTelemetryPacket(const uint8_t* pData, size_t length, SystemTelemetry& out) {
    if (length < 32 || pData[0] != 0xA5) return false;

    // Checksum XOR 31 bytes đầu
    uint8_t chk = 0;
    for (int i = 0; i < 31; i++) chk ^= pData[i];
    if (chk != pData[31]) return false;

    out.cpuUsage       = pData[1];
    out.cpuTemp        = (pData[2] == 0xFF) ? -1 : (int16_t)pData[2];
    out.ramUsedMB      = ((uint32_t)(pData[3] | (pData[4] << 8))) * 10;
    out.ramTotalMB     = ((uint32_t)(pData[5] | (pData[6] << 8))) * 10;
    out.ramUsage       = pData[7];
    out.gpuUsage       = (pData[8] == 0xFF) ? -1 : (int16_t)pData[8];
    out.gpuTemp        = (pData[9] == 0xFF) ? -1 : (int16_t)pData[9];
    out.diskReadKBps   = (uint16_t)(pData[10] | (pData[11] << 8));
    out.diskWriteKBps  = (uint16_t)(pData[12] | (pData[13] << 8));
    out.diskUsage      = pData[14];
    out.netSentKBps    = (uint16_t)(pData[15] | (pData[16] << 8));
    out.netRecvKBps    = (uint16_t)(pData[17] | (pData[18] << 8));
    out.uptimeSeconds  = (uint32_t)(pData[19] | (pData[20] << 8) | (pData[21] << 16) | (pData[22] << 24));
    out.cpuFreqMHz     = (uint16_t)pData[23] * 100;

    return true;
}

// ── 5. BLE SERVER CALLBACKS ─────────────────────────────────────────────────
class MyServerCallbacks : public BLEServerCallbacks {
    void onConnect(BLEServer* pServer) override { isConnected = true; }
    void onDisconnect(BLEServer* pServer) override {
        isConnected = false;
        BLEDevice::startAdvertising();
    }
};

class MetricsRxCallback : public BLECharacteristicCallbacks {
    void onWrite(BLECharacteristic* pCharacteristic) override {
        size_t len = pCharacteristic->getLength();
        const uint8_t* pData = pCharacteristic->getData();
        if (parseTelemetryPacket(pData, len, currentStats)) {
            hasNewData = true;
            lastPacketTime = millis();
        }
    }
};

// ── 6. CÁC HÀM VẼ GIAO DIỆN CÂN ĐỐI 2 BÊN ───────────────────────────────────

// Vẽ thanh tiến trình (Progress Bar)
void drawBar(int16_t x, int16_t y, int16_t w, int16_t h, uint8_t percent) {
    if (percent > 100) percent = 100;
    display.drawRect(x, y, w, h, SSD1306_WHITE);
    int16_t innerW = (w - 2) * percent / 100;
    if (innerW > 0) {
        display.fillRect(x + 1, y + 1, innerW, h - 2, SSD1306_WHITE);
    }
}

// Mũi tên Upload (▲)
void drawUpArrow(int16_t x, int16_t y) {
    display.fillTriangle(x + 2, y, x, y + 4, x + 4, y + 4, SSD1306_WHITE);
    display.drawFastVLine(x + 2, y + 3, 3, SSD1306_WHITE);
}

// Mũi tên Download (▼)
void drawDownArrow(int16_t x, int16_t y) {
    display.fillTriangle(x, y, x + 4, y, x + 2, y + 4, SSD1306_WHITE);
    display.drawFastVLine(x + 2, y - 1, 3, SSD1306_WHITE);
}

// Định dạng tốc độ mạng / disk
void formatSpeed(uint16_t kbps, char* buf, size_t size) {
    if (kbps >= 10240) {
        snprintf(buf, size, "%uM", kbps / 1024);
    } else if (kbps >= 1024) {
        snprintf(buf, size, "%.1fM", kbps / 1024.0);
    } else {
        snprintf(buf, size, "%uK", kbps);
    }
}

// Màn hình chờ kết nối (Khung viền tròn bo góc cân đối)
void renderWaitingScreen() {
    display.clearDisplay();
    display.setTextColor(SSD1306_WHITE);

    display.drawRoundRect(2, 2, 124, 60, 4, SSD1306_WHITE);

    display.setTextSize(1);
    display.setCursor(17, 8);
    display.print("WINDOWS MONITOR");
    display.drawFastHLine(10, 20, 108, SSD1306_WHITE);

    display.setCursor(20, 26);
    display.print("CHO KET NOI BLE");

    int step = (millis() / 400) % 4;
    display.setCursor(46, 38);
    display.print("[ ");
    for (int i = 0; i < 3; i++) {
        display.print(i < step ? "*" : ".");
    }
    display.print(" ]");

    display.setCursor(18, 49);
    display.print("Name: " DEVICE_NAME);

    display.display();
}

// ────────────────────────────────────────────────────────────────────────────
// GIAO DIỆN CHÍNH: 2 CỘT CÂN ĐỀU 2 BÊN (Cực kỳ cân đối và trực quan)
// ────────────────────────────────────────────────────────────────────────────
void renderTelemetryScreen(const SystemTelemetry& t) {
    display.clearDisplay();
    display.setTextColor(SSD1306_WHITE);
    display.setTextSize(1);

    // ── HÀNG 1: CPU (BÊN TRÁI: X=2..60)  &  RAM (BÊN PHẢI: X=68..126) ────────
    // [Trái] CPU
    display.setCursor(2, 1);
    display.printf("CPU%2d%%", t.cpuUsage);
    if (t.cpuTemp >= 0) {
        display.setCursor(38, 1);
        display.printf("%2dC", t.cpuTemp);
    }
    drawBar(2, 11, 58, 6, t.cpuUsage);

    // [Phải] RAM
    display.setCursor(68, 1);
    display.printf("RAM%2d%%", t.ramUsage);
    display.setCursor(98, 1);
    display.printf("%.1fG", t.ramUsedMB / 1024.0);
    drawBar(68, 11, 58, 6, t.ramUsage);

    // Đường kẻ ngang ngăn cách Hàng 1 và Hàng 2
    display.drawFastHLine(0, 21, 128, SSD1306_WHITE);

    // ── HÀNG 2: GPU (BÊN TRÁI: X=2..60)  &  DISK (BÊN PHẢI: X=68..126) ───────
    // [Trái] GPU
    display.setCursor(2, 25);
    if (t.gpuUsage >= 0) {
        display.printf("GPU%2d%%", t.gpuUsage);
        if (t.gpuTemp >= 0) {
            display.setCursor(38, 25);
            display.printf("%2dC", t.gpuTemp);
        }
        drawBar(2, 35, 58, 6, t.gpuUsage);
    } else {
        display.print("GPU N/A");
        drawBar(2, 35, 58, 6, 0);
    }

    // [Phải] DISK
    display.setCursor(68, 25);
    display.printf("DSK%2d%%", t.diskUsage);
    char dSpeed[8];
    formatSpeed(t.diskReadKBps + t.diskWriteKBps, dSpeed, sizeof(dSpeed));
    display.setCursor(98, 25);
    display.print(dSpeed);
    drawBar(68, 35, 58, 6, t.diskUsage);

    // Đường kẻ dọc phân chia 2 cột ở 2 hàng trên (X = 63, Y: 0..45)
    display.drawFastVLine(63, 0, 45, SSD1306_WHITE);

    // Đường kẻ ngang ngăn cách Hàng 2 và Hàng 3 (Footer)
    display.drawFastHLine(0, 45, 128, SSD1306_WHITE);

    // ── HÀNG 3 (FOOTER): NETWORK & UPTIME (CÂN ĐỀU TOÀN MÀN HÌNH) ───────────
    char upSpeed[8], downSpeed[8];
    formatSpeed(t.netSentKBps, upSpeed, sizeof(upSpeed));
    formatSpeed(t.netRecvKBps, downSpeed, sizeof(downSpeed));

    // Upload (▲)
    drawUpArrow(2, 51);
    display.setCursor(9, 51);
    display.print(upSpeed);

    // Download (▼)
    drawDownArrow(38, 52);
    display.setCursor(45, 51);
    display.print(downSpeed);

    // Uptime (⏱) căn đều góc phải
    uint32_t hours = t.uptimeSeconds / 3600;
    uint32_t mins  = (t.uptimeSeconds % 3600) / 60;
    uint32_t secs  = t.uptimeSeconds % 60;
    display.setCursor(80, 51);
    display.printf("%02u:%02u:%02u", hours, mins, secs);

    display.display();
}

// ── 7. SETUP & LOOP ─────────────────────────────────────────────────────────
void setup() {
    Serial.begin(115200);
    delay(500);

    Wire.begin(I2C_SDA, I2C_SCL);

    if (display.begin(SSD1306_SWITCHCAPVCC, SCREEN_ADDRESS)) {
        display.clearDisplay();
        renderWaitingScreen();
    }

    BLEDevice::init(DEVICE_NAME);
    BLEServer* pServer = BLEDevice::createServer();
    pServer->setCallbacks(new MyServerCallbacks());

    BLEService* pService = pServer->createService(SERVICE_UUID);

    // RX Characteristic
    BLECharacteristic* pRxChar = pService->createCharacteristic(
        CHARACTERISTIC_UUID_RX,
        BLECharacteristic::PROPERTY_WRITE | BLECharacteristic::PROPERTY_WRITE_NR
    );
    pRxChar->setCallbacks(new MetricsRxCallback());

    // TX Characteristic
    BLECharacteristic* pTxChar = pService->createCharacteristic(
        CHARACTERISTIC_UUID_TX,
        BLECharacteristic::PROPERTY_NOTIFY | BLECharacteristic::PROPERTY_READ
    );
    pTxChar->addDescriptor(new BLE2902());

    pService->start();

    // Bắt đầu quảng bá BLE
    BLEAdvertising* pAdvertising = BLEDevice::getAdvertising();
    pAdvertising->addServiceUUID(SERVICE_UUID);
    pAdvertising->setScanResponse(true);
    pAdvertising->setMinPreferred(0x06);
    pAdvertising->setMinPreferred(0x12);
    BLEDevice::startAdvertising();
}

void loop() {
    if (!isConnected || (millis() - lastPacketTime > 5000)) {
        static unsigned long lastWait = 0;
        if (millis() - lastWait > 300) {
            lastWait = millis();
            renderWaitingScreen();
        }
    } else if (hasNewData) {
        hasNewData = false;
        renderTelemetryScreen(currentStats);
    }
    delay(10);
}
