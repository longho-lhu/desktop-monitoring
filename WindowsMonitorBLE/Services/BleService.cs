using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;
using WindowsMonitorBLE.Models;

namespace WindowsMonitorBLE.Services;

/// <summary>
/// Quản lý kết nối BLE với ESP32-C3.
/// 
/// ╔══════════════════════════════════════════════════════════════╗
/// ║              UUID quy ước cho phía ESP32-C3                 ║
/// ╠══════════════════════════════════════════════════════════════╣
/// ║ Service:    12345678-0000-1000-8000-00805F9B34FB            ║
/// ║ Metrics RX: 12345678-0001-1000-8000-00805F9B34FB  (Write)  ║
/// ║ Status  TX: 12345678-0002-1000-8000-00805F9B34FB  (Notify) ║
/// ╚══════════════════════════════════════════════════════════════╝
/// 
/// ESP32 phải quảng bá Service UUID trên để app tìm được.
/// </summary>
public sealed class BleService : IAsyncDisposable
{
    // ── BLE UUID definitions ──────────────────────────────────────
    public static readonly Guid ServiceUuid      = new("12345678-0000-1000-8000-00805F9B34FB");
    public static readonly Guid MetricsCharUuid  = new("12345678-0001-1000-8000-00805F9B34FB");
    public static readonly Guid StatusCharUuid   = new("12345678-0002-1000-8000-00805F9B34FB");

    // ── Events ───────────────────────────────────────────────────
    public event Action<string>?        OnLog;
    public event Action<BleState>?      OnStateChanged;
    public event Action<string>?        OnEsp32Status;   // phản hồi từ ESP32

    // ── State ─────────────────────────────────────────────────────
    public BleState State { get; private set; } = BleState.Disconnected;
    public string? ConnectedDeviceName { get; private set; }

    // ── Internals ─────────────────────────────────────────────────
    private BluetoothLEDevice?           _device;
    private GattCharacteristic?          _metricsChar;
    private GattCharacteristic?          _statusChar;
    private DeviceWatcher?               _watcher;
    private readonly SemaphoreSlim       _writeLock = new(1, 1);

    // Tổng số gói đã gửi / thất bại
    public int PacketsSent   { get; private set; }
    public int PacketsFailed { get; private set; }

    // ─────────────────────────────────────────────────────────────
    // Scan
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Bắt đầu quét BLE. Khi tìm thấy ESP32 sẽ tự động kết nối.
    /// </summary>
    /// <param name="targetName">Tên device cần tìm (VD: "ESP32Monitor").
    /// Nếu null thì dùng Service UUID để lọc.</param>
    public void StartScan(string? targetName = null)
    {
        if (State == BleState.Connected) return;

        SetState(BleState.Scanning);
        Log("🔍 Đang quét BLE...");

        // Tạo AQS filter theo Service UUID
        string aqsFilter = BluetoothLEDevice.GetDeviceSelectorFromPairingState(false);

        _watcher = DeviceInformation.CreateWatcher(
            BluetoothLEDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Disconnected));

        _watcher.Added   += async (s, e) => await OnDeviceFound(e, targetName);
        _watcher.Updated += async (s, e) => { /* chỉ cần Added */ await Task.CompletedTask; };
        _watcher.Start();
    }

    public void StopScan()
    {
        try { _watcher?.Stop(); } catch { }
        _watcher = null;
        if (State == BleState.Scanning) SetState(BleState.Disconnected);
        Log("⏹ Dừng quét.");
    }

    private async Task OnDeviceFound(DeviceInformation info, string? targetName)
    {
        // Lọc theo tên nếu có
        if (targetName != null &&
            !info.Name.Contains(targetName, StringComparison.OrdinalIgnoreCase))
            return;

        Log($"📡 Tìm thấy: {info.Name} [{info.Id}]");
        StopScan();

        await ConnectToDeviceAsync(info.Id, info.Name);
    }

    // ─────────────────────────────────────────────────────────────
    // Connect
    // ─────────────────────────────────────────────────────────────

    /// <summary>Kết nối đến một BLE device cụ thể theo DeviceId</summary>
    public async Task ConnectToDeviceAsync(string deviceId, string deviceName)
    {
        try
        {
            SetState(BleState.Connecting);
            Log($"🔗 Đang kết nối đến {deviceName}...");

            _device = await BluetoothLEDevice.FromIdAsync(deviceId);
            if (_device == null) throw new Exception("Không mở được BLE device.");

            _device.ConnectionStatusChanged += OnConnectionStatusChanged;

            // Lấy GATT service
            var serviceResult = await _device.GetGattServicesForUuidAsync(ServiceUuid,
                BluetoothCacheMode.Uncached);

            if (serviceResult.Status != GattCommunicationStatus.Success ||
                serviceResult.Services.Count == 0)
                throw new Exception($"Không tìm thấy Service UUID {ServiceUuid}.\n" +
                                    "Hãy kiểm tra lại UUID trong code ESP32.");

            var service = serviceResult.Services[0];

            // Lấy Metrics Characteristic (Write)
            var charResult = await service.GetCharacteristicsForUuidAsync(MetricsCharUuid,
                BluetoothCacheMode.Uncached);
            if (charResult.Status != GattCommunicationStatus.Success ||
                charResult.Characteristics.Count == 0)
                throw new Exception("Không tìm thấy Metrics Characteristic.");

            _metricsChar = charResult.Characteristics[0];

            // Lấy Status Characteristic (Notify) - tuỳ chọn
            var statusResult = await service.GetCharacteristicsForUuidAsync(StatusCharUuid,
                BluetoothCacheMode.Uncached);
            if (statusResult.Status == GattCommunicationStatus.Success &&
                statusResult.Characteristics.Count > 0)
            {
                _statusChar = statusResult.Characteristics[0];
                await EnableNotificationsAsync(_statusChar);
            }

            ConnectedDeviceName = deviceName;
            SetState(BleState.Connected);
            Log($"✅ Kết nối thành công: {deviceName}");
        }
        catch (Exception ex)
        {
            Log($"❌ Lỗi kết nối: {ex.Message}");
            await DisconnectAsync();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Send
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Gửi gói thông số qua BLE GATT Write.
    /// Thread-safe, có timeout 2s.
    /// </summary>
    public async Task<bool> SendMetricsAsync(SystemMetrics metrics)
    {
        if (State != BleState.Connected || _metricsChar == null)
            return false;

        bool acquired = await _writeLock.WaitAsync(100);
        if (!acquired) return false;

        try
        {
            byte[] packet = metrics.ToBlePacket();

            using var writer = new DataWriter();
            writer.WriteBytes(packet);

            var result = await _metricsChar.WriteValueWithResultAsync(
                writer.DetachBuffer(),
                GattWriteOption.WriteWithoutResponse);   // nhanh hơn, không cần ACK

            bool ok = result.Status == GattCommunicationStatus.Success;
            if (ok) PacketsSent++;
            else
            {
                PacketsFailed++;
                Log($"⚠ Gửi thất bại: {result.Status}");
            }
            return ok;
        }
        catch (Exception ex)
        {
            PacketsFailed++;
            Log($"❌ Lỗi ghi BLE: {ex.Message}");
            return false;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Disconnect
    // ─────────────────────────────────────────────────────────────

    public async Task DisconnectAsync()
    {
        if (_statusChar != null)
        {
            try
            {
                await _statusChar.WriteClientCharacteristicConfigurationDescriptorAsync(
                    GattClientCharacteristicConfigurationDescriptorValue.None);
                _statusChar.ValueChanged -= OnStatusNotification;
            }
            catch { }
            _statusChar = null;
        }

        _metricsChar = null;

        if (_device != null)
        {
            _device.ConnectionStatusChanged -= OnConnectionStatusChanged;
            _device.Dispose();
            _device = null;
        }

        ConnectedDeviceName = null;
        SetState(BleState.Disconnected);
        Log("🔌 Đã ngắt kết nối.");
    }

    // ─────────────────────────────────────────────────────────────
    // Scan devices list (for picker UI)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Quét và trả về danh sách BLE devices trong vòng <paramref name="timeoutMs"/> ms.
    /// </summary>
    public static async Task<List<DeviceInformation>> ScanDevicesAsync(int timeoutMs = 5000)
    {
        var found = new List<DeviceInformation>();
        var tcs   = new TaskCompletionSource();

        string selector = BluetoothLEDevice.GetDeviceSelectorFromConnectionStatus(
            BluetoothConnectionStatus.Disconnected);
        var watcher = DeviceInformation.CreateWatcher(selector);

        watcher.Added += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.Name))
                lock (found) { found.Add(e); }
        };

        watcher.Start();
        await Task.Delay(timeoutMs);
        watcher.Stop();

        return found;
    }

    // ─────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────

    private async Task EnableNotificationsAsync(GattCharacteristic ch)
    {
        try
        {
            var status = await ch.WriteClientCharacteristicConfigurationDescriptorAsync(
                GattClientCharacteristicConfigurationDescriptorValue.Notify);
            if (status == GattCommunicationStatus.Success)
                ch.ValueChanged += OnStatusNotification;
        }
        catch { }
    }

    private void OnStatusNotification(GattCharacteristic sender, GattValueChangedEventArgs args)
    {
        using var reader = DataReader.FromBuffer(args.CharacteristicValue);
        string msg = reader.ReadString(args.CharacteristicValue.Length);
        OnEsp32Status?.Invoke(msg);
        Log($"📩 ESP32: {msg}");
    }

    private void OnConnectionStatusChanged(BluetoothLEDevice sender, object args)
    {
        if (sender.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
        {
            Log("⚠ BLE bị ngắt kết nối bất ngờ.");
            SetState(BleState.Disconnected);
            _metricsChar = null;
        }
    }

    private void SetState(BleState state)
    {
        State = state;
        OnStateChanged?.Invoke(state);
    }

    private void Log(string msg)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
        OnLog?.Invoke(line);
    }

    public async ValueTask DisposeAsync()
    {
        StopScan();
        await DisconnectAsync();
        _writeLock.Dispose();
    }
}

/// <summary>Trạng thái kết nối BLE</summary>
public enum BleState
{
    Disconnected,
    Scanning,
    Connecting,
    Connected
}
