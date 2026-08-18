using WindowsMonitorBLE.Models;
using WindowsMonitorBLE.Services;

namespace WindowsMonitorBLE;

public class SettingsForm : Form
{
    private readonly AppSettings _settings;
    private readonly SystemMetricsService _metricsService;

    // Sidebar buttons
    private Button btnMenuSystem  = null!;
    private Button btnMenuDisk    = null!;
    private Button btnMenuNetwork = null!;
    private Button btnMenuBle     = null!;

    // Content panels
    private Panel pnlSystem  = null!;
    private Panel pnlDisk    = null!;
    private Panel pnlNetwork = null!;
    private Panel pnlBle     = null!;

    // System controls
    private CheckBox chkAutoStart   = null!;
    private CheckBox chkMinToTray   = null!;

    // Disk controls
    private ComboBox cboDisks       = null!;
    private ComboBox cboDiskUnits   = null!;
    private ComboBox cboDriveLetter = null!;

    // Network controls
    private ComboBox cboNetworkAdapters = null!;
    private ComboBox cboNetUnits        = null!;

    // BLE controls
    private ComboBox cboBleInterval   = null!;
    private CheckBox chkAutoSendBle   = null!;

    // Footer buttons
    private Button btnSave   = null!;
    private Button btnCancel = null!;

    public SettingsForm(AppSettings settings, SystemMetricsService metricsService)
    {
        _settings       = settings;
        _metricsService = metricsService;

        InitializeComponent();
        LoadCurrentSettings();
        SelectMenu(btnMenuSystem, pnlSystem);
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();

        // ── Form Properties ───────────────────────────────────────
        this.Text            = "⚙ Cài đặt — Windows Monitor BLE";
        this.Size            = new Size(680, 460);
        this.MinimumSize     = new Size(650, 430);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox     = false;
        this.MinimizeBox     = false;
        this.StartPosition   = FormStartPosition.CenterParent;
        this.BackColor       = Color.FromArgb(241, 245, 249);
        this.ForeColor       = Color.FromArgb(15, 23, 42);
        this.Font            = new Font("Segoe UI", 9.5f, FontStyle.Regular);

        // ── Header Panel ──────────────────────────────────────────
        var pnlHeader = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 55,
            BackColor = Color.White,
            Padding   = new Padding(16, 0, 16, 0)
        };

        var pnlHeaderBorder = new Panel
        {
            Dock      = DockStyle.Bottom,
            Height    = 1,
            BackColor = Color.FromArgb(226, 232, 240)
        };
        pnlHeader.Controls.Add(pnlHeaderBorder);

        var lblTitle = new Label
        {
            Text      = "⚙  CÀI ĐẶT HỆ THỐNG & THIẾT BỊ",
            Font      = new Font("Segoe UI", 12, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            AutoSize  = true,
            Location  = new Point(16, 16)
        };
        pnlHeader.Controls.Add(lblTitle);

        // ── Left Sidebar Panel (Menu) ─────────────────────────────
        var pnlSidebar = new Panel
        {
            Dock      = DockStyle.Left,
            Width     = 175,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding   = new Padding(8, 12, 8, 12)
        };

        var pnlSidebarBorder = new Panel
        {
            Dock      = DockStyle.Right,
            Width     = 1,
            BackColor = Color.FromArgb(226, 232, 240)
        };
        pnlSidebar.Controls.Add(pnlSidebarBorder);

        btnMenuSystem  = MakeMenuButton("🖥  Hệ thống",  12);
        btnMenuDisk    = MakeMenuButton("💾  Ổ đĩa",     56);
        btnMenuNetwork = MakeMenuButton("🌐  Card mạng", 100);
        btnMenuBle     = MakeMenuButton("🔵  BLE / ESP32", 144);

        btnMenuSystem.Click  += (s, e) => SelectMenu(btnMenuSystem,  pnlSystem);
        btnMenuDisk.Click    += (s, e) => SelectMenu(btnMenuDisk,    pnlDisk);
        btnMenuNetwork.Click += (s, e) => SelectMenu(btnMenuNetwork, pnlNetwork);
        btnMenuBle.Click     += (s, e) => SelectMenu(btnMenuBle,     pnlBle);

        pnlSidebar.Controls.AddRange([btnMenuSystem, btnMenuDisk, btnMenuNetwork, btnMenuBle]);

        // ── Content Container Panel ───────────────────────────────
        var pnlContentContainer = new Panel
        {
            Dock      = DockStyle.Fill,
            BackColor = Color.FromArgb(241, 245, 249),
            Padding   = new Padding(16, 12, 16, 12)
        };

        // 1. Menu Panel: Hệ thống
        pnlSystem = BuildSystemPanel();
        // 2. Menu Panel: Ổ đĩa
        pnlDisk = BuildDiskPanel();
        // 3. Menu Panel: Card mạng
        pnlNetwork = BuildNetworkPanel();
        // 4. Menu Panel: BLE / ESP32
        pnlBle = BuildBlePanel();

        pnlContentContainer.Controls.AddRange([pnlSystem, pnlDisk, pnlNetwork, pnlBle]);

        // ── Footer Panel (Buttons) ────────────────────────────────
        var pnlFooter = new Panel
        {
            Dock      = DockStyle.Bottom,
            Height    = 58,
            BackColor = Color.White,
            Padding   = new Padding(16, 11, 16, 11)
        };

        var pnlFooterBorder = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 1,
            BackColor = Color.FromArgb(226, 232, 240)
        };
        pnlFooter.Controls.Add(pnlFooterBorder);

        btnSave = new Button
        {
            Text       = "💾 Lưu cài đặt",
            Font       = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            BackColor  = Color.FromArgb(16, 185, 129),
            ForeColor  = Color.White,
            FlatStyle  = FlatStyle.Flat,
            Height     = 36,
            Width      = 130,
            Location   = new Point(390, 11),
            Cursor     = Cursors.Hand,
            FlatAppearance = { BorderSize = 0 }
        };
        btnSave.Click += BtnSave_Click;

        btnCancel = new Button
        {
            Text       = "Đóng",
            Font       = new Font("Segoe UI", 9.5f),
            BackColor  = Color.FromArgb(241, 245, 249),
            ForeColor  = Color.FromArgb(51, 65, 85),
            FlatStyle  = FlatStyle.Flat,
            Height     = 36,
            Width      = 100,
            Location   = new Point(530, 11),
            Cursor     = Cursors.Hand
        };
        btnCancel.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnCancel.FlatAppearance.BorderSize = 1;
        btnCancel.Click += (s, e) => this.Close();

        pnlFooter.Controls.AddRange([btnSave, btnCancel]);

        // Assemble Form
        this.Controls.Add(pnlContentContainer);
        this.Controls.Add(pnlSidebar);
        this.Controls.Add(pnlFooter);
        this.Controls.Add(pnlHeader);

        this.ResumeLayout(false);
    }

    // ─────────────────────────────────────────────────────────────
    // Sub-Panel Builders
    // ─────────────────────────────────────────────────────────────

    private Panel BuildSystemPanel()
    {
        var pnl = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(241, 245, 249) };
        int top = 8;

        var lblSec1 = MakeSectionHeader("🚀 Khởi động cùng Windows");
        lblSec1.Location = new Point(8, top);
        pnl.Controls.Add(lblSec1);
        top += 26;

        chkAutoStart = new CheckBox
        {
            Text      = "Tự động khởi động ứng dụng khi bật máy tính",
            Font      = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location  = new Point(12, top),
            Width     = 420,
            AutoSize  = true,
            Cursor    = Cursors.Hand
        };
        pnl.Controls.Add(chkAutoStart);
        top += 46;

        var lblSec2 = MakeSectionHeader("🪟 Thu nhỏ xuống khay hệ thống (System Tray)");
        lblSec2.Location = new Point(8, top);
        pnl.Controls.Add(lblSec2);
        top += 26;

        chkMinToTray = new CheckBox
        {
            Text      = "Chạy ngầm ở góc màn hình khi bấm thu nhỏ",
            Font      = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location  = new Point(12, top),
            Width     = 420,
            AutoSize  = true,
            Cursor    = Cursors.Hand
        };
        pnl.Controls.Add(chkMinToTray);

        return pnl;
    }

    private Panel BuildDiskPanel()
    {
        var pnl = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(241, 245, 249) };
        int top = 8;

        // 1. Chọn ổ đĩa
        var lblSec1 = MakeSectionHeader("💾 Chọn ổ đĩa theo dõi tốc độ đọc / ghi");
        lblSec1.Location = new Point(8, top);
        pnl.Controls.Add(lblSec1);
        top += 24;

        cboDisks = new ComboBox
        {
            Location      = new Point(12, top),
            Width         = 440,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor     = Color.White,
            ForeColor     = Color.FromArgb(15, 23, 42),
            FlatStyle     = FlatStyle.Flat,
            Font          = new Font("Segoe UI", 9.5f)
        };
        pnl.Controls.Add(cboDisks);
        top += 42;

        // 2. Đơn vị đo tốc độ ổ đĩa
        var lblSec2 = MakeSectionHeader("📏 Đơn vị đo tốc độ đọc / ghi");
        lblSec2.Location = new Point(8, top);
        pnl.Controls.Add(lblSec2);
        top += 24;

        cboDiskUnits = new ComboBox
        {
            Location      = new Point(12, top),
            Width         = 440,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor     = Color.White,
            ForeColor     = Color.FromArgb(15, 23, 42),
            FlatStyle     = FlatStyle.Flat,
            Font          = new Font("Segoe UI", 9.5f)
        };
        cboDiskUnits.Items.AddRange([
            "Tự động (Auto: B/s, KB/s, MB/s, GB/s)",
            "KB/s (Kilobytes/giây)",
            "MB/s (Megabytes/giây)",
            "GB/s (Gigabytes/giây)"
        ]);
        pnl.Controls.Add(cboDiskUnits);
        top += 42;

        // 3. Phân vùng dung lượng
        var lblSec3 = MakeSectionHeader("🗂 Phân vùng ổ đĩa theo dõi dung lượng (%)");
        lblSec3.Location = new Point(8, top);
        pnl.Controls.Add(lblSec3);
        top += 24;

        cboDriveLetter = new ComboBox
        {
            Location      = new Point(12, top),
            Width         = 440,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor     = Color.White,
            ForeColor     = Color.FromArgb(15, 23, 42),
            FlatStyle     = FlatStyle.Flat,
            Font          = new Font("Segoe UI", 9.5f)
        };
        pnl.Controls.Add(cboDriveLetter);

        return pnl;
    }

    private Panel BuildNetworkPanel()
    {
        var pnl = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(241, 245, 249) };
        int top = 8;

        // 1. Chọn card mạng
        var lblSec1 = MakeSectionHeader("🌐 Chọn Card mạng theo dõi tốc độ truyền / nhận");
        lblSec1.Location = new Point(8, top);
        pnl.Controls.Add(lblSec1);
        top += 24;

        cboNetworkAdapters = new ComboBox
        {
            Location      = new Point(12, top),
            Width         = 440,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor     = Color.White,
            ForeColor     = Color.FromArgb(15, 23, 42),
            FlatStyle     = FlatStyle.Flat,
            Font          = new Font("Segoe UI", 9.5f)
        };
        pnl.Controls.Add(cboNetworkAdapters);
        top += 48;

        // 2. Đơn vị đo tốc độ mạng
        var lblSec2 = MakeSectionHeader("📏 Đơn vị đo tốc độ mạng");
        lblSec2.Location = new Point(8, top);
        pnl.Controls.Add(lblSec2);
        top += 24;

        cboNetUnits = new ComboBox
        {
            Location      = new Point(12, top),
            Width         = 440,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor     = Color.White,
            ForeColor     = Color.FromArgb(15, 23, 42),
            FlatStyle     = FlatStyle.Flat,
            Font          = new Font("Segoe UI", 9.5f)
        };
        cboNetUnits.Items.AddRange([
            "Tự động (Auto: KB/s, MB/s, GB/s)",
            "KB/s (Kilobytes/giây - Byte)",
            "MB/s (Megabytes/giây - Byte)",
            "Kbps (Kilobits/giây - Bit)",
            "Mbps (Megabits/giây - Bit)"
        ]);
        pnl.Controls.Add(cboNetUnits);

        return pnl;
    }

    private Panel BuildBlePanel()
    {
        var pnl = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(241, 245, 249) };
        int top = 8;

        var lblSec1 = MakeSectionHeader("⏱ Tần suất gửi gói tin sang ESP32-C3");
        lblSec1.Location = new Point(8, top);
        pnl.Controls.Add(lblSec1);
        top += 24;

        cboBleInterval = new ComboBox
        {
            Location      = new Point(12, top),
            Width         = 440,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor     = Color.White,
            ForeColor     = Color.FromArgb(15, 23, 42),
            FlatStyle     = FlatStyle.Flat,
            Font          = new Font("Segoe UI", 9.5f)
        };
        cboBleInterval.Items.AddRange([
            "0.5 giây (Cực nhanh)",
            "1.0 giây (Khuyên dùng - Tiêu chuẩn)",
            "2.0 giây (Tiết kiệm năng lượng)",
            "3.0 giây",
            "5.0 giây"
        ]);
        pnl.Controls.Add(cboBleInterval);
        top += 48;

        var lblSec2 = MakeSectionHeader("📡 Tự động truyền dữ liệu");
        lblSec2.Location = new Point(8, top);
        pnl.Controls.Add(lblSec2);
        top += 26;

        chkAutoSendBle = new CheckBox
        {
            Text      = "Tự động bắt đầu gửi số liệu ngay khi kết nối BLE",
            Font      = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location  = new Point(12, top),
            Width     = 420,
            AutoSize  = true,
            Cursor    = Cursors.Hand
        };
        pnl.Controls.Add(chkAutoSendBle);

        return pnl;
    }

    // ─────────────────────────────────────────────────────────────
    // UI Helpers & Menu Switcher
    // ─────────────────────────────────────────────────────────────

    private Button MakeMenuButton(string text, int top) => new()
    {
        Text       = text,
        Font       = new Font("Segoe UI", 9.5f, FontStyle.Regular),
        ForeColor  = Color.FromArgb(71, 85, 105),
        BackColor  = Color.Transparent,
        FlatStyle  = FlatStyle.Flat,
        Height     = 38,
        Width      = 157,
        Location   = new Point(8, top),
        TextAlign  = ContentAlignment.MiddleLeft,
        Padding    = new Padding(10, 0, 0, 0),
        Cursor     = Cursors.Hand,
        FlatAppearance = { BorderSize = 0, MouseOverBackColor = Color.FromArgb(241, 245, 249) }
    };

    private void SelectMenu(Button activeBtn, Panel activePanel)
    {
        // Reset all menu buttons
        foreach (var b in new[] { btnMenuSystem, btnMenuDisk, btnMenuNetwork, btnMenuBle })
        {
            b.BackColor = Color.Transparent;
            b.ForeColor = Color.FromArgb(71, 85, 105);
            b.Font      = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        }

        // Highlight active menu button
        activeBtn.BackColor = Color.FromArgb(239, 246, 255);
        activeBtn.ForeColor = Color.FromArgb(37, 99, 235);
        activeBtn.Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold);

        // Switch panel
        activePanel.BringToFront();
    }

    private static Label MakeSectionHeader(string text) => new()
    {
        Text      = text,
        Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold),
        ForeColor = Color.FromArgb(30, 41, 59),
        AutoSize  = true
    };

    // ─────────────────────────────────────────────────────────────
    // Load & Save
    // ─────────────────────────────────────────────────────────────

    private void LoadCurrentSettings()
    {
        // 1. Hệ thống
        chkAutoStart.Checked = _settings.StartWithWindows;
        chkMinToTray.Checked = _settings.MinimizeToTray;

        // 2. Ổ đĩa
        cboDisks.Items.Clear();
        var disks = SystemMetricsService.GetAvailableDisks();
        foreach (var d in disks)
        {
            string display = d == "_Total" ? "_Total (Tổng hợp tất cả các ổ đĩa)" : $"Ổ đĩa: {d}";
            cboDisks.Items.Add(new DiskItem(d, display));
        }

        int diskIndex = 0;
        for (int i = 0; i < cboDisks.Items.Count; i++)
        {
            if (cboDisks.Items[i] is DiskItem item && item.RawName == _settings.SelectedDiskInstance)
            {
                diskIndex = i;
                break;
            }
        }
        if (cboDisks.Items.Count > 0) cboDisks.SelectedIndex = diskIndex;

        // Đơn vị đo Disk
        cboDiskUnits.SelectedIndex = _settings.DiskSpeedUnit switch
        {
            "KB/s" => 1,
            "MB/s" => 2,
            "GB/s" => 3,
            _      => 0
        };

        // Phân vùng ổ đĩa
        cboDriveLetter.Items.Clear();
        var drives = SystemMetricsService.GetAvailableLogicalDrives();
        foreach (var dr in drives)
        {
            cboDriveLetter.Items.Add($"Ổ đĩa {dr}:");
        }
        int driveIdx = 0;
        for (int i = 0; i < drives.Count; i++)
        {
            if (drives[i].Equals(_settings.DiskDriveLetter, StringComparison.OrdinalIgnoreCase))
            {
                driveIdx = i;
                break;
            }
        }
        if (cboDriveLetter.Items.Count > 0) cboDriveLetter.SelectedIndex = driveIdx;

        // 3. Card mạng
        cboNetworkAdapters.Items.Clear();
        cboNetworkAdapters.Items.Add("Auto (Tự động chọn card mạng chính)");
        var adapters = SystemMetricsService.GetAvailableNetworkAdapters();
        foreach (var ad in adapters)
        {
            cboNetworkAdapters.Items.Add(ad);
        }

        int netIndex = 0;
        if (!string.IsNullOrEmpty(_settings.SelectedNetworkAdapter) &&
            !_settings.SelectedNetworkAdapter.Equals("Auto", StringComparison.OrdinalIgnoreCase))
        {
            for (int i = 1; i < cboNetworkAdapters.Items.Count; i++)
            {
                if (cboNetworkAdapters.Items[i]?.ToString() == _settings.SelectedNetworkAdapter)
                {
                    netIndex = i;
                    break;
                }
            }
        }
        cboNetworkAdapters.SelectedIndex = netIndex;

        // Đơn vị đo Network
        cboNetUnits.SelectedIndex = _settings.NetworkSpeedUnit switch
        {
            "KB/s" => 1,
            "MB/s" => 2,
            "Kbps" => 3,
            "Mbps" => 4,
            _      => 0
        };

        // 4. BLE
        cboBleInterval.SelectedIndex = _settings.SendIntervalMs switch
        {
            500  => 0,
            1000 => 1,
            2000 => 2,
            3000 => 3,
            5000 => 4,
            _    => 1
        };
        chkAutoSendBle.Checked = _settings.AutoSendOnConnect;
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        // 1. Hệ thống
        _settings.StartWithWindows = chkAutoStart.Checked;
        _settings.MinimizeToTray   = chkMinToTray.Checked;

        // 2. Ổ đĩa
        if (cboDisks.SelectedItem is DiskItem diskItem)
            _settings.SelectedDiskInstance = diskItem.RawName;
        else
            _settings.SelectedDiskInstance = "_Total";

        _settings.DiskSpeedUnit = cboDiskUnits.SelectedIndex switch
        {
            1 => "KB/s",
            2 => "MB/s",
            3 => "GB/s",
            _ => "Auto"
        };

        if (cboDriveLetter.SelectedIndex >= 0)
        {
            string selectedDriveText = cboDriveLetter.SelectedItem?.ToString() ?? "C:";
            // Tách ký tự ổ (vd "Ổ đĩa C:" -> "C")
            var drives = SystemMetricsService.GetAvailableLogicalDrives();
            if (cboDriveLetter.SelectedIndex < drives.Count)
                _settings.DiskDriveLetter = drives[cboDriveLetter.SelectedIndex];
        }

        // 3. Card mạng
        if (cboNetworkAdapters.SelectedIndex <= 0)
            _settings.SelectedNetworkAdapter = "Auto";
        else
            _settings.SelectedNetworkAdapter = cboNetworkAdapters.SelectedItem?.ToString() ?? "Auto";

        _settings.NetworkSpeedUnit = cboNetUnits.SelectedIndex switch
        {
            1 => "KB/s",
            2 => "MB/s",
            3 => "Kbps",
            4 => "Mbps",
            _ => "Auto"
        };

        // 4. BLE
        _settings.SendIntervalMs = cboBleInterval.SelectedIndex switch
        {
            0 => 500,
            1 => 1000,
            2 => 2000,
            3 => 3000,
            4 => 5000,
            _ => 1000
        };
        _settings.AutoSendOnConnect = chkAutoSendBle.Checked;

        // Áp dụng ngay vào Services
        _metricsService.SetNetworkAdapter(_settings.SelectedNetworkAdapter);
        _metricsService.SetDiskInstance(_settings.SelectedDiskInstance);
        _metricsService.SetDriveLetter(_settings.DiskDriveLetter);

        // Lưu vào file JSON và Registry
        SettingsService.SaveSettings(_settings);

        MessageBox.Show("✅ Đã lưu cấu hình thành công!", "Cài đặt", MessageBoxButtons.OK, MessageBoxIcon.Information);
        this.DialogResult = DialogResult.OK;
        this.Close();
    }

    private sealed class DiskItem(string rawName, string displayText)
    {
        public string RawName { get; } = rawName;
        public string DisplayText { get; } = displayText;
        public override string ToString() => DisplayText;
    }
}
