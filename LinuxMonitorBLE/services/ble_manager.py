"""
BleManager: Quản lý quét, kết nối và truyền dữ liệu BLE GATT với ESP32 trên Linux.
Sử dụng thư viện bleak tương thích chuẩn với BlueZ DBus stack trên Ubuntu.
"""

import asyncio
from enum import Enum
import logging
from typing import Callable, List, Optional

try:
    from bleak import BleakClient, BleakScanner
    from bleak.backends.device import BLEDevice
    from bleak.backends.scanner import AdvertisementData
except ImportError:
    BleakClient = None
    BleakScanner = None
    BLEDevice = None
    AdvertisementData = None

try:
    from ..models.system_metrics import SystemMetrics
except (ImportError, ValueError):
    from models.system_metrics import SystemMetrics

logger = logging.getLogger("LinuxMonitorBLE.BleManager")


class BleState(Enum):
    DISCONNECTED = "Disconnected"
    SCANNING = "Scanning"
    CONNECTING = "Connecting"
    CONNECTED = "Connected"


class BleManager:
    # ── BLE UUID Definitions (Khớp 100% với ESP32 Firmware) ──────────
    SERVICE_UUID = "12345678-0000-1000-8000-00805f9b34fb"
    METRICS_CHAR_UUID = "12345678-0001-1000-8000-00805f9b34fb"  # RX (Write)
    STATUS_CHAR_UUID = "12345678-0002-1000-8000-00805f9b34fb"   # TX (Notify)
    DEFAULT_DEVICE_NAME = "ESP32Monitor"

    def __init__(self, target_name: str = DEFAULT_DEVICE_NAME):
        self.target_name = target_name
        self.state: BleState = BleState.DISCONNECTED
        self.connected_device: Optional[BLEDevice] = None
        self.connected_name: Optional[str] = None
        self.connected_address: Optional[str] = None

        self._client: Optional[BleakClient] = None
        self._write_lock = asyncio.Lock()

        # Thống kê gói tin
        self.packets_sent: int = 0
        self.packets_failed: int = 0

        # Callbacks
        self.on_state_changed: Optional[Callable[[BleState], None]] = None
        self.on_log: Optional[Callable[[str], None]] = None
        self.on_esp32_status: Optional[Callable[[str], None]] = None
        self.on_device_found: Optional[Callable[[BLEDevice, AdvertisementData], None]] = None

        # Reconnect flags
        self.auto_reconnect: bool = True
        self._should_reconnect: bool = False
        self._reconnect_task: Optional[asyncio.Task] = None

    def _set_state(self, new_state: BleState):
        """Cập nhật trạng thái kết nối."""
        if self.state != new_state:
            self.state = new_state
            if self.on_state_changed:
                try:
                    self.on_state_changed(new_state)
                except Exception as e:
                    logger.error(f"Lỗi trong on_state_changed: {e}")

    def _log(self, message: str):
        """Ghi log kèm timestamp."""
        logger.info(message)
        if self.on_log:
            try:
                self.on_log(message)
            except Exception:
                pass

    # ─────────────────────────────────────────────────────────────
    # Scan
    # ─────────────────────────────────────────────────────────────

    async def scan_for_devices(self, timeout: float = 5.0) -> List[BLEDevice]:
        """Quét và trả về danh sách các thiết bị BLE tìm thấy."""
        self._set_state(BleState.SCANNING)
        self._log(f"🔍 Đang quét thiết bị BLE ({timeout:.1f}s)...")
        found_devices: List[BLEDevice] = []

        def detection_callback(device: BLEDevice, adv: AdvertisementData):
            name = device.name or adv.local_name or "Unknown"
            if device not in found_devices:
                found_devices.append(device)
            if self.on_device_found:
                try:
                    self.on_device_found(device, adv)
                except Exception:
                    pass

        try:
            scanner = BleakScanner(detection_callback=detection_callback)
            await scanner.start()
            await asyncio.sleep(timeout)
            await scanner.stop()
            self._log(f"📋 Tìm thấy {len(found_devices)} thiết bị BLE.")
            return found_devices
        except Exception as e:
            self._log(f"❌ Lỗi khi quét BLE: {e}")
            return []
        finally:
            if self.state == BleState.SCANNING:
                self._set_state(BleState.DISCONNECTED)

    async def find_target_device(self, timeout: float = 6.0) -> Optional[BLEDevice]:
        """Tìm kiếm thiết bị ESP32 theo tên hoặc Service UUID."""
        self._log(f"🔍 Đang tìm kiếm '{self.target_name}' hoặc Service UUID...")
        self._set_state(BleState.SCANNING)

        target_dev: Optional[BLEDevice] = None

        def match_filter(dev: BLEDevice, adv: AdvertisementData) -> bool:
            nonlocal target_dev
            dev_name = dev.name or adv.local_name or ""
            # Khớp tên
            if self.target_name.lower() in dev_name.lower():
                target_dev = dev
                return True
            # Khớp Service UUID
            if adv.service_uuids:
                for u in adv.service_uuids:
                    if u.lower() == self.SERVICE_UUID.lower():
                        target_dev = dev
                        return True
            return False

        try:
            dev = await BleakScanner.find_device_by_filter(match_filter, timeout=timeout)
            return dev or target_dev
        except Exception as e:
            self._log(f"❌ Lỗi tìm thiết bị: {e}")
            return None
        finally:
            if self.state == BleState.SCANNING:
                self._set_state(BleState.DISCONNECTED)

    # ─────────────────────────────────────────────────────────────
    # Connect & Disconnect
    # ─────────────────────────────────────────────────────────────

    async def connect(self, device_or_address: Optional[str] = None) -> bool:
        """Kết nối đến thiết bị BLE."""
        if self.state == BleState.CONNECTED:
            return True

        self._set_state(BleState.CONNECTING)
        self._should_reconnect = self.auto_reconnect

        target = device_or_address
        if not target:
            target_device = await self.find_target_device(timeout=5.0)
            if not target_device:
                self._log(f"⚠ Không tìm thấy '{self.target_name}'. Vui lòng bật ESP32.")
                self._set_state(BleState.DISCONNECTED)
                return False
            target = target_device.address
            self.connected_name = target_device.name or self.target_name
        else:
            self.connected_name = self.target_name

        self._log(f"🔗 Đang kết nối đến {target} ({self.connected_name})...")

        try:
            self._client = BleakClient(
                target,
                disconnected_callback=self._on_disconnected,
                timeout=12.0,
            )
            await self._client.connect()

            if not self._client.is_connected:
                raise Exception("Không thể thiết lập kết nối BLE.")

            # Kiểm tra GATT Services & Characteristics
            services = self._client.services
            metrics_char_found = False

            for s in services:
                for c in s.characteristics:
                    if c.uuid.lower() == self.METRICS_CHAR_UUID.lower():
                        metrics_char_found = True
                    elif c.uuid.lower() == self.STATUS_CHAR_UUID.lower():
                        # Đăng ký nhận notify từ ESP32 nếu có
                        try:
                            await self._client.start_notify(
                                c.uuid, self._on_status_notification
                            )
                            self._log("🔔 Đã đăng ký nhận Notify phản hồi từ ESP32.")
                        except Exception as ne:
                            self._log(f"⚠ Không thể bật Notify: {ne}")

            if not metrics_char_found:
                raise Exception(
                    f"Không tìm thấy Metrics Characteristic ({self.METRICS_CHAR_UUID}) trên thiết bị."
                )

            self.connected_address = target
            self._set_state(BleState.CONNECTED)
            self._log(f"✅ Đã kết nối BLE thành công với {self.connected_name} [{target}]")
            return True

        except Exception as e:
            self._log(f"❌ Lỗi kết nối BLE: {e}")
            await self.disconnect()
            return False

    async def disconnect(self):
        """Ngắt kết nối an toàn."""
        self._should_reconnect = False
        if self._client:
            try:
                if self._client.is_connected:
                    await self._client.disconnect()
            except Exception:
                pass
            self._client = None

        self.connected_device = None
        self.connected_address = None
        self.connected_name = None
        self._set_state(BleState.DISCONNECTED)
        self._log("🔌 Đã ngắt kết nối BLE.")

    def _on_disconnected(self, client: BleakClient):
        """Callback khi bị ngắt kết nối bất ngờ từ phía ESP32."""
        self._log("⚠ Kết nối BLE bị ngắt!")
        self.connected_device = None
        self._set_state(BleState.DISCONNECTED)

        if self._should_reconnect and self.auto_reconnect:
            if not self._reconnect_task or self._reconnect_task.done():
                self._reconnect_task = asyncio.create_task(self._reconnect_loop())

    async def _reconnect_loop(self):
        """Vòng lặp tự động kết nối lại khi mất sóng."""
        self._log("🔄 Bắt đầu tiến trình tự động kết nối lại...")
        retry_delay = 3.0
        while self._should_reconnect and self.auto_reconnect and self.state != BleState.CONNECTED:
            await asyncio.sleep(retry_delay)
            if self.state == BleState.CONNECTED or not self._should_reconnect:
                break
            self._log(f"🔄 Thử kết nối lại với {self.connected_address or self.target_name}...")
            success = await self.connect(self.connected_address)
            if success:
                self._log("🎉 Kết nối lại thành công!")
                break
            retry_delay = min(retry_delay + 2.0, 10.0)

    # ─────────────────────────────────────────────────────────────
    # Send Metrics
    # ─────────────────────────────────────────────────────────────

    async def send_metrics(self, metrics: SystemMetrics) -> bool:
        """
        Gửi gói 32-byte SystemMetrics sang ESP32 qua BLE GATT Write.
        Sử dụng response=False (Write Without Response) để đạt hiệu năng tối đa.
        """
        if self.state != BleState.CONNECTED or not self._client or not self._client.is_connected:
            return False

        async with self._write_lock:
            try:
                packet = metrics.to_ble_packet()
                await self._client.write_gatt_char(
                    self.METRICS_CHAR_UUID, packet, response=False
                )
                self.packets_sent += 1
                return True
            except Exception as e:
                self.packets_failed += 1
                self._log(f"⚠ Lỗi gửi gói BLE: {e}")
                return False

    # ─────────────────────────────────────────────────────────────
    # Notification Callback
    # ─────────────────────────────────────────────────────────────

    def _on_status_notification(self, sender, data: bytearray):
        """Nhận tin nhắn phản hồi từ ESP32."""
        try:
            msg = data.decode("utf-8", errors="ignore").strip()
            self._log(f"📩 Phản hồi từ ESP32: {msg}")
            if self.on_esp32_status:
                self.on_esp32_status(msg)
        except Exception:
            pass
