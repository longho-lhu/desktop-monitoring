using WindowsMonitorBLE.Services;
using WindowsMonitorBLE.Models;
using DeviceInformation = Windows.Devices.Enumeration.DeviceInformation;

namespace WindowsMonitorBLE;

public partial class MainForm : Form
{
    // ── Services & Settings ──────────────────────────────────────
    private readonly AppSettings          _settings;
    private readonly SystemMetricsService _metricsService;
    private readonly BleService           _bleService;

    // ── Timer thu thập & gửi dữ liệu ─────────────────────────────
    private readonly System.Windows.Forms.Timer _collectTimer;
    private SystemMetrics? _lastMetrics;

    // ── Lịch sử CPU để vẽ biểu đồ ────────────────────────────────
    private readonly Queue<float> _cpuHistory  = new();
    private readonly Queue<float> _ramHistory  = new();
    private const int HistoryPoints = 60;

    // ── BLE state color ──────────────────────────────────────────
    private Color _bleIndicatorColor = Color.FromArgb(148, 163, 184);

    // ── Cài đặt ───────────────────────────────────────────────────
    private int   _sendIntervalMs    = 1000;   // mặc định 1 giây
    private bool  _autoSendEnabled   = false;

    // ── UI Controls ───────────────────────────────────────────────
    // Được khởi tạo trong InitializeComponent()
    private Panel   pnlHeader       = null!;
    private Label   lblTitle        = null!;
    private Label   lblSubtitle     = null!;
    private Button  btnSettings     = null!;

    // BLE Panel
    private Panel   pnlBle          = null!;
    private Button  btnScan         = null!;
    private Button  btnDisconnect   = null!;
    private ComboBox cboDevices     = null!;
    private Button  btnConnect      = null!;
    private Label   lblBleStatus    = null!;
    private PictureBox picBleIndicator = null!;

    // Metrics Panel
    private TableLayoutPanel tblMetrics = null!;

    // Gauge labels
    private Label lblCpuVal   = null!;
    private Label lblCpuTemp  = null!;
    private Panel pnlCpuBar   = null!;
    private Panel pnlCpuFill  = null!;

    private Label lblRamVal   = null!;
    private Label lblRamInfo  = null!;
    private Panel pnlRamBar   = null!;
    private Panel pnlRamFill  = null!;

    private Label lblGpuVal   = null!;
    private Label lblGpuTemp  = null!;
    private Panel pnlGpuBar   = null!;
    private Panel pnlGpuFill  = null!;

    private Label lblDiskRead  = null!;
    private Label lblDiskWrite = null!;
    private Label lblDiskC     = null!;

    private Label lblNetSend   = null!;
    private Label lblNetRecv   = null!;
    private Label lblUptime    = null!;

    // Chart panel
    private Panel   pnlChart    = null!;
    private Bitmap? _chartBitmap;

    // Log
    private ListBox lstLog      = null!;

    // Send stats
    private Label lblSendStats  = null!;
    private CheckBox chkAutoSend = null!;
    private Button  btnSendNow  = null!;

    // ─────────────────────────────────────────────────────────────

    public MainForm()
    {
        _settings       = SettingsService.LoadSettings();
        _metricsService = new SystemMetricsService(_settings.SelectedNetworkAdapter, _settings.SelectedDiskInstance, _settings.DiskDriveLetter);
        _bleService     = new BleService();
        _sendIntervalMs = _settings.SendIntervalMs;
        _autoSendEnabled = _settings.AutoSendOnConnect;

        InitializeComponent();
        chkAutoSend.Checked = _settings.AutoSendOnConnect;
        WireEvents();

        if (SystemMetricsService.IsRunningAsAdministrator())
        {
            AppendLog("🛡 Đang chạy quyền Quản trị viên (Admin) - Sensor nhiệt độ đã kích hoạt.");
        }
        else
        {
            AppendLog("⚠️ Đang chạy quyền người dùng thông thường.");
            AppendLog("💡 Mẹo: Nhấp chuột phải chọn 'Run as administrator' (hoặc chạy run_admin.bat) để đọc nhiệt độ CPU/GPU.");
        }

        _collectTimer = new System.Windows.Forms.Timer { Interval = _autoSendEnabled ? _sendIntervalMs : 1000 };
        _collectTimer.Tick += CollectTimer_Tick;
        _collectTimer.Start();
    }

    // ─────────────────────────────────────────────────────────────
    // Events wiring
    // ─────────────────────────────────────────────────────────────

    private void WireEvents()
    {
        _bleService.OnLog           += msg => SafeInvoke(() => AppendLog(msg));
        _bleService.OnStateChanged  += s   => SafeInvoke(() => UpdateBleState(s));
        _bleService.OnEsp32Status   += msg => SafeInvoke(() => AppendLog($"[ESP32] {msg}"));

        btnScan.Click       += BtnScan_Click;
        btnConnect.Click    += BtnConnect_Click;
        btnDisconnect.Click += BtnDisconnect_Click;
        btnSendNow.Click    += BtnSendNow_Click;

        btnSettings.Click += OpenSettingsDialog;

        chkAutoSend.CheckedChanged += (s, e) =>
        {
            _autoSendEnabled = chkAutoSend.Checked;
            _settings.AutoSendOnConnect = chkAutoSend.Checked;
            SettingsService.SaveSettings(_settings);
            _collectTimer.Interval = _autoSendEnabled ? _sendIntervalMs : 1000;
        };

        pnlChart.Paint += PnlChart_Paint;
        FormClosing    += MainForm_FormClosing;
    }

    // ─────────────────────────────────────────────────────────────
    // Timer
    // ─────────────────────────────────────────────────────────────

    private async void CollectTimer_Tick(object? sender, EventArgs e)
    {
        _lastMetrics = _metricsService.Collect();

        // Cập nhật lịch sử biểu đồ
        EnqueueHistory(_cpuHistory, _lastMetrics.CpuUsagePercent);
        EnqueueHistory(_ramHistory, _lastMetrics.RamUsagePercent);

        // Cập nhật UI
        UpdateMetricsDisplay(_lastMetrics);

        // Gửi BLE nếu auto send
        if (_autoSendEnabled)
        {
            bool ok = await _bleService.SendMetricsAsync(_lastMetrics);
            lblSendStats.Text = $"✅ {_bleService.PacketsSent}  ❌ {_bleService.PacketsFailed}";
        }
    }

    // ─────────────────────────────────────────────────────────────
    // BLE button handlers
    // ─────────────────────────────────────────────────────────────

    private async void BtnScan_Click(object? sender, EventArgs e)
    {
        btnScan.Enabled   = false;
        cboDevices.Items.Clear();
        AppendLog("🔍 Đang quét thiết bị BLE...");

        var devices = await BleService.ScanDevicesAsync(5000);

        cboDevices.Items.Clear();
        foreach (var d in devices)
            cboDevices.Items.Add(new DeviceItem(d));

        AppendLog($"✅ Tìm thấy {devices.Count} thiết bị.");
        btnScan.Enabled = true;

        if (cboDevices.Items.Count > 0)
            cboDevices.SelectedIndex = 0;
    }

    private async void BtnConnect_Click(object? sender, EventArgs e)
    {
        if (cboDevices.SelectedItem is not DeviceItem item) return;
        await _bleService.ConnectToDeviceAsync(item.Info.Id, item.Info.Name);
    }

    private async void BtnDisconnect_Click(object? sender, EventArgs e)
    {
        await _bleService.DisconnectAsync();
    }

    private async void BtnSendNow_Click(object? sender, EventArgs e)
    {
        if (_lastMetrics == null) return;
        bool ok = await _bleService.SendMetricsAsync(_lastMetrics);
        AppendLog(ok ? "📤 Đã gửi thủ công." : "❌ Gửi thất bại.");
        lblSendStats.Text = $"✅ {_bleService.PacketsSent}  ❌ {_bleService.PacketsFailed}";
    }

    private void OpenSettingsDialog(object? sender, EventArgs e)
    {
        using var form = new SettingsForm(_settings, _metricsService);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            _sendIntervalMs     = _settings.SendIntervalMs;
            chkAutoSend.Checked = _settings.AutoSendOnConnect;
            _autoSendEnabled    = _settings.AutoSendOnConnect;
            _collectTimer.Interval = _autoSendEnabled ? _sendIntervalMs : 1000;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // UI Update helpers
    // ─────────────────────────────────────────────────────────────

    private void UpdateMetricsDisplay(SystemMetrics m)
    {
        // CPU
        lblCpuVal.Text  = $"{m.CpuUsagePercent:F1}%";
        lblCpuTemp.Text = m.CpuTemperatureCelsius.HasValue ? $"{m.CpuTemperatureCelsius:F0}°C" : "— °C";
        SetBarFill(pnlCpuFill, pnlCpuBar, m.CpuUsagePercent, GetTempColor(m.CpuUsagePercent));

        // RAM
        lblRamVal.Text  = $"{m.RamUsagePercent:F1}%";
        lblRamInfo.Text = $"{m.RamUsedMB / 1024.0:F1} / {m.RamTotalMB / 1024.0:F1} GB";
        SetBarFill(pnlRamFill, pnlRamBar, m.RamUsagePercent, GetTempColor(m.RamUsagePercent));

        // GPU
        if (m.GpuUsagePercent.HasValue)
        {
            lblGpuVal.Text  = $"{m.GpuUsagePercent:F1}%";
            lblGpuTemp.Text = m.GpuTemperatureCelsius.HasValue ? $"{m.GpuTemperatureCelsius:F0}°C" : "— °C";
            SetBarFill(pnlGpuFill, pnlGpuBar, m.GpuUsagePercent.Value, GetTempColor(m.GpuUsagePercent.Value));
        }
        else
        {
            lblGpuVal.Text  = "N/A";
            lblGpuTemp.Text = "— °C";
        }

        // Disk
        lblDiskRead.Text  = $"↓ {FormatDiskSpeed(m.DiskReadKBps, _settings.DiskSpeedUnit)}";
        lblDiskWrite.Text = $"↑ {FormatDiskSpeed(m.DiskWriteKBps, _settings.DiskSpeedUnit)}";
        lblDiskC.Text     = $"{_settings.DiskDriveLetter}: {m.DiskCUsagePercent:F1}%";

        // Network
        lblNetSend.Text = $"↑ {FormatNetworkSpeed(m.NetworkSentKBps, _settings.NetworkSpeedUnit)}";
        lblNetRecv.Text = $"↓ {FormatNetworkSpeed(m.NetworkReceivedKBps, _settings.NetworkSpeedUnit)}";

        // Uptime
        var ts = TimeSpan.FromSeconds(m.UptimeSeconds);
        lblUptime.Text = $"⏱ {ts.Days}d {ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";

        // Vẽ lại biểu đồ
        DrawChart();
        pnlChart.Invalidate();
    }

    private void UpdateBleState(BleState state)
    {
        Color clr = state switch
        {
            BleState.Connected    => Color.FromArgb(5, 150, 105),
            BleState.Connecting   => Color.FromArgb(217, 119, 6),
            BleState.Scanning     => Color.FromArgb(37, 99, 235),
            _                     => Color.FromArgb(148, 163, 184)
        };
        string txt = state switch
        {
            BleState.Connected    => $"Kết nối: {_bleService.ConnectedDeviceName}",
            BleState.Connecting   => "Đang kết nối...",
            BleState.Scanning     => "Đang quét...",
            _                     => "Chưa kết nối"
        };
        _bleIndicatorColor = clr;
        picBleIndicator.Invalidate();
        lblBleStatus.Text         = txt;
        lblBleStatus.ForeColor    = clr;
        btnDisconnect.Enabled     = state == BleState.Connected;
        btnConnect.Enabled        = state == BleState.Disconnected;
        chkAutoSend.Enabled       = state == BleState.Connected;
        btnSendNow.Enabled        = state == BleState.Connected;

        if (state == BleState.Connected && _settings.AutoSendOnConnect && !chkAutoSend.Checked)
        {
            chkAutoSend.Checked = true;
        }
    }

    private void AppendLog(string msg)
    {
        lstLog.Items.Add(msg);
        if (lstLog.Items.Count > 200)
            lstLog.Items.RemoveAt(0);
        lstLog.TopIndex = lstLog.Items.Count - 1;
    }

    // ─────────────────────────────────────────────────────────────
    // Chart drawing (Light Theme)
    // ─────────────────────────────────────────────────────────────

    private void DrawChart()
    {
        int w = pnlChart.Width;
        int h = pnlChart.Height;
        if (w <= 0 || h <= 0) return;

        _chartBitmap?.Dispose();
        _chartBitmap = new Bitmap(w, h);
        using var g = Graphics.FromImage(_chartBitmap);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        // Background
        g.Clear(Color.White);

        // Border
        using var borderPen = new Pen(Color.FromArgb(226, 232, 240));
        g.DrawRectangle(borderPen, 0, 0, w - 1, h - 1);

        // Grid lines
        using var gridPen = new Pen(Color.FromArgb(241, 245, 249));
        for (int pct = 25; pct <= 75; pct += 25)
        {
            int y = (int)(h - h * pct / 100.0);
            g.DrawLine(gridPen, 0, y, w, y);
        }

        DrawHistoryLine(g, _cpuHistory, w, h, Color.FromArgb(37, 99, 235));
        DrawHistoryLine(g, _ramHistory, w, h, Color.FromArgb(5, 150, 105));

        // Legend
        using var font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        using var textBrush = new SolidBrush(Color.FromArgb(71, 85, 105));
        g.FillRectangle(new SolidBrush(Color.FromArgb(37, 99, 235)), 12, 8, 12, 10);
        g.DrawString("CPU", font, textBrush, 28, 6);
        g.FillRectangle(new SolidBrush(Color.FromArgb(5, 150, 105)), 68, 8, 12, 10);
        g.DrawString("RAM", font, textBrush, 84, 6);
    }

    private static void DrawHistoryLine(Graphics g, Queue<float> data, int w, int h, Color color)
    {
        if (data.Count < 2) return;
        var arr = data.ToArray();
        using var pen = new Pen(color, 2f);
        float xStep = (float)w / (HistoryPoints - 1);

        var pts = new PointF[arr.Length];
        for (int i = 0; i < arr.Length; i++)
        {
            float x = i * xStep;
            float y = h - h * arr[i] / 100f;
            pts[i] = new PointF(x, y);
        }
        g.DrawLines(pen, pts);
    }

    private void PnlChart_Paint(object? sender, PaintEventArgs e)
    {
        if (_chartBitmap != null)
            e.Graphics.DrawImage(_chartBitmap, 0, 0);
    }

    // ─────────────────────────────────────────────────────────────
    // Utilities
    // ─────────────────────────────────────────────────────────────

    private static void SetBarFill(Panel fill, Panel bar, float pct, Color color)
    {
        int newWidth = (int)(bar.Width * Math.Clamp(pct, 0, 100) / 100f);
        fill.Width    = newWidth;
        fill.BackColor = color;
    }

    private static Color GetTempColor(float pct) => pct switch
    {
        >= 85 => Color.FromArgb(220, 38, 38),
        >= 60 => Color.FromArgb(217, 119, 6),
        _     => Color.FromArgb(5, 150, 105)
    };

    private static string FormatDiskSpeed(float kbps, string unit) => unit switch
    {
        "KB/s" => $"{kbps:F0} KB/s",
        "MB/s" => $"{kbps / 1024f:F1} MB/s",
        "GB/s" => $"{kbps / (1024f * 1024f):F2} GB/s",
        _ => kbps switch
        {
            >= 1024 * 1024 => $"{kbps / 1024 / 1024:F1} GB/s",
            >= 1024        => $"{kbps / 1024:F1} MB/s",
            _              => $"{kbps:F0} KB/s"
        }
    };

    private static string FormatNetworkSpeed(float kbps, string unit) => unit switch
    {
        "KB/s" => $"{kbps:F0} KB/s",
        "MB/s" => $"{kbps / 1024f:F1} MB/s",
        "Kbps" => $"{kbps * 8f:F0} Kbps",
        "Mbps" => $"{kbps * 8f / 1024f:F1} Mbps",
        _ => kbps switch
        {
            >= 1024 * 1024 => $"{kbps / 1024 / 1024:F1} GB/s",
            >= 1024        => $"{kbps / 1024:F1} MB/s",
            _              => $"{kbps:F0} KB/s"
        }
    };

    private static void EnqueueHistory(Queue<float> q, float val)
    {
        if (q.Count >= HistoryPoints) q.Dequeue();
        q.Enqueue(val);
    }

    private void SafeInvoke(Action act)
    {
        if (IsHandleCreated && !IsDisposed)
            Invoke(act);
    }

    private async void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        _collectTimer.Stop();
        await _bleService.DisposeAsync();
        _metricsService.Dispose();
        _chartBitmap?.Dispose();
    }

    // ─────────────────────────────────────────────────────────────
    // Helper class
    // ─────────────────────────────────────────────────────────────
    private sealed class DeviceItem(DeviceInformation info)
    {
        public DeviceInformation Info { get; } = info;
        public override string ToString() => string.IsNullOrEmpty(Info.Name) ? $"[{Info.Id[..8]}...]" : Info.Name;
    }
}
