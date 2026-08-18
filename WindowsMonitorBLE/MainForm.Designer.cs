namespace WindowsMonitorBLE;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
            components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();

        // ── Form properties (Light Theme) ─────────────────────────
        this.Text            = "Windows Monitor BLE — ESP32-C3";
        this.Size            = new Size(900, 720);
        this.MinimumSize     = new Size(800, 650);
        this.BackColor       = Color.FromArgb(241, 245, 249); // Slate-100
        this.ForeColor       = Color.FromArgb(15, 23, 42);    // Slate-900
        this.Font            = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        this.StartPosition   = FormStartPosition.CenterScreen;

        // ── Header ────────────────────────────────────────────────
        pnlHeader = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 60,
            BackColor = Color.White,
            Padding   = new Padding(16, 0, 16, 0)
        };

        // Subtle bottom border for header
        var pnlHeaderBorder = new Panel
        {
            Dock      = DockStyle.Bottom,
            Height    = 1,
            BackColor = Color.FromArgb(226, 232, 240) // Slate-200
        };
        pnlHeader.Controls.Add(pnlHeaderBorder);

        lblTitle = new Label
        {
            Text      = "🖥  WINDOWS MONITOR",
            Font      = new Font("Segoe UI", 13.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),   // Slate-800
            AutoSize  = true,
            Location  = new Point(16, 11)
        };

        lblSubtitle = new Label
        {
            Text      = "Real-time system stats → BLE → ESP32-C3",
            Font      = new Font("Segoe UI", 9, FontStyle.Regular),
            ForeColor = Color.FromArgb(100, 116, 139), // Slate-500
            AutoSize  = true,
            Location  = new Point(18, 37)
        };

        var pnlHeaderRight = new Panel
        {
            Dock      = DockStyle.Right,
            Width     = 130,
            BackColor = Color.Transparent,
            Padding   = new Padding(0, 13, 16, 13)
        };

        btnSettings = new Button
        {
            Text      = "⚙ Cài đặt",
            Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            BackColor = Color.FromArgb(241, 245, 249),
            FlatStyle = FlatStyle.Flat,
            Dock      = DockStyle.Fill,
            Cursor    = Cursors.Hand
        };
        btnSettings.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnSettings.FlatAppearance.BorderSize = 1;
        pnlHeaderRight.Controls.Add(btnSettings);

        pnlHeader.Controls.AddRange([pnlHeaderRight, lblTitle, lblSubtitle]);

        // ── Left column (Metrics + Chart) ─────────────────────────
        var pnlLeft = new Panel
        {
            Dock    = DockStyle.Fill,
            Padding = new Padding(12, 8, 6, 8)
        };

        // Metrics table
        tblMetrics = new TableLayoutPanel
        {
            ColumnCount = 3,
            RowCount    = 2,
            Dock        = DockStyle.Top,
            Height      = 280,
            BackColor   = Color.FromArgb(241, 245, 249),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
            Padding     = new Padding(0)
        };
        tblMetrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        tblMetrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        tblMetrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));

        // CPU Card (Blue)
        var cpuCard   = BuildGaugeCard("CPU", Color.FromArgb(37, 99, 235),
                            out lblCpuVal, out lblCpuTemp, out pnlCpuBar, out pnlCpuFill);
        // RAM Card (Emerald)
        var ramCard   = BuildRamCard(out lblRamVal, out lblRamInfo, out pnlRamBar, out pnlRamFill);
        // GPU Card (Violet)
        var gpuCard   = BuildGaugeCard("GPU", Color.FromArgb(124, 58, 237),
                            out lblGpuVal, out lblGpuTemp, out pnlGpuBar, out pnlGpuFill);
        // Disk Card (Amber)
        var diskCard  = BuildInfoCard("💾 Disk", Color.FromArgb(217, 119, 6),
                            out lblDiskRead, out lblDiskWrite, out lblDiskC);
        // Network Card (Teal)
        var netCard   = BuildInfoCard("🌐 Network", Color.FromArgb(13, 148, 136),
                            out lblNetSend, out lblNetRecv, out lblUptime);
        // Uptime Card (Indigo)
        var upCard    = BuildUptimeCard(out lblUptime);

        tblMetrics.Controls.Add(cpuCard,  0, 0);
        tblMetrics.Controls.Add(ramCard,  1, 0);
        tblMetrics.Controls.Add(gpuCard,  2, 0);
        tblMetrics.Controls.Add(diskCard, 0, 1);
        tblMetrics.Controls.Add(netCard,  1, 1);
        tblMetrics.Controls.Add(upCard,   2, 1);

        // Chart section
        var chartContainer = new Panel
        {
            Dock    = DockStyle.Fill,
            Padding = new Padding(4, 8, 4, 4)
        };

        var chartLabel = new Label
        {
            Text      = "📈 LỊCH SỬ SỬ DỤNG (60s) — CPU & RAM",
            ForeColor = Color.FromArgb(71, 85, 105),
            Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
            Dock      = DockStyle.Top,
            Height    = 24,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding   = new Padding(4, 0, 0, 0)
        };

        pnlChart = new Panel
        {
            Dock        = DockStyle.Fill,
            BackColor   = Color.White,
            MinimumSize = new Size(0, 100)
        };

        chartContainer.Controls.Add(pnlChart);
        chartContainer.Controls.Add(chartLabel);

        pnlLeft.Controls.Add(chartContainer);
        pnlLeft.Controls.Add(tblMetrics);

        // ── Right column (BLE + Log) ───────────────────────────────
        var pnlRight = new Panel
        {
            Dock    = DockStyle.Right,
            Width   = 295,
            Padding = new Padding(6, 8, 12, 8),
            BackColor = Color.FromArgb(241, 245, 249)
        };

        // BLE Panel
        pnlBle = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 275,
            BackColor = Color.White,
            Padding   = new Padding(12)
        };

        var lblBleTitle = MakeSectionLabel("🔵 Bluetooth BLE");

        // Status indicator
        picBleIndicator = new PictureBox
        {
            Size      = new Size(12, 12),
            Location  = new Point(12, 45),
            BackColor = Color.Transparent
        };
        picBleIndicator.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var b = new SolidBrush(_bleIndicatorColor);
            e.Graphics.FillEllipse(b, 1, 1, 9, 9);
        };

        lblBleStatus = new Label
        {
            Text      = "Chưa kết nối",
            ForeColor = Color.FromArgb(100, 116, 139),
            Font      = new Font("Segoe UI", 9, FontStyle.Bold),
            Location  = new Point(28, 42),
            AutoSize  = true
        };

        // Device combobox
        cboDevices = new ComboBox
        {
            Dock          = DockStyle.None,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location      = new Point(12, 66),
            Width         = 265,
            BackColor     = Color.FromArgb(248, 250, 252),
            ForeColor     = Color.FromArgb(15, 23, 42),
            FlatStyle     = FlatStyle.Flat,
            Font          = new Font("Segoe UI", 9)
        };

        // Buttons row 1
        btnScan = MakeButton("🔍 Quét", Color.FromArgb(37, 99, 235));
        btnScan.Location = new Point(12, 98);
        btnScan.Width    = 128;

        btnConnect = MakeButton("🔗 Kết nối", Color.FromArgb(16, 185, 129));
        btnConnect.Location = new Point(146, 98);
        btnConnect.Width    = 131;
        btnConnect.Enabled  = false;

        btnDisconnect = MakeButton("❌ Ngắt kết nối", Color.FromArgb(239, 68, 68));
        btnDisconnect.Location = new Point(12, 134);
        btnDisconnect.Width    = 265;
        btnDisconnect.Enabled  = false;

        // Send section
        var sepLine = new Panel
        {
            BackColor = Color.FromArgb(226, 232, 240),
            Height    = 1,
            Location  = new Point(12, 172),
            Width     = 265
        };

        var lblSendTitle = MakeSectionLabel("📤 Gửi dữ liệu");
        lblSendTitle.Location = new Point(12, 178);

        chkAutoSend = new CheckBox
        {
            Text      = "Tự động truyền dữ liệu liên tục",
            Font      = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location  = new Point(12, 204),
            AutoSize  = true,
            Enabled   = false,
            Cursor    = Cursors.Hand
        };

        btnSendNow = MakeButton("📡 Gửi ngay 1 gói tin", Color.FromArgb(99, 102, 241));
        btnSendNow.Location = new Point(12, 234);
        btnSendNow.Width    = 265;
        btnSendNow.Enabled  = false;

        lblSendStats = new Label
        {
            Text      = "✅ 0  ❌ 0",
            ForeColor = Color.FromArgb(71, 85, 105),
            Location  = new Point(12, 270),
            AutoSize  = true,
            Visible   = false
        };

        pnlBle.Controls.AddRange([
            lblBleTitle, picBleIndicator, lblBleStatus,
            cboDevices, btnScan, btnConnect, btnDisconnect,
            sepLine, lblSendTitle, chkAutoSend,
            btnSendNow, lblSendStats
        ]);

        // Log Section
        var logContainer = new Panel
        {
            Dock      = DockStyle.Fill,
            Padding   = new Padding(0, 8, 0, 0),
            BackColor = Color.FromArgb(241, 245, 249)
        };

        var pnlLogCard = new Panel
        {
            Dock      = DockStyle.Fill,
            BackColor = Color.White,
            Padding   = new Padding(8)
        };

        var lblLogTitle = new Label
        {
            Text      = "📋 NHẬT KÝ",
            Font      = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = Color.FromArgb(71, 85, 105),
            Dock      = DockStyle.Top,
            Height    = 22,
            TextAlign = ContentAlignment.MiddleLeft
        };

        lstLog = new ListBox
        {
            Dock           = DockStyle.Fill,
            BackColor      = Color.White,
            ForeColor      = Color.FromArgb(51, 65, 85),
            Font           = new Font("Consolas", 8.5f),
            BorderStyle    = BorderStyle.None,
            SelectionMode  = SelectionMode.None,
            IntegralHeight = false
        };

        pnlLogCard.Controls.Add(lstLog);
        pnlLogCard.Controls.Add(lblLogTitle);
        logContainer.Controls.Add(pnlLogCard);

        pnlRight.Controls.Add(logContainer);
        pnlRight.Controls.Add(pnlBle);

        // ── Assemble form ─────────────────────────────────────────
        this.Controls.Add(pnlLeft);
        this.Controls.Add(pnlRight);
        this.Controls.Add(pnlHeader);

        this.ResumeLayout(false);
    }

    // ─────────────────────────────────────────────────────────────
    // Widget builders (Light Theme Cards)
    // ─────────────────────────────────────────────────────────────

    private static Panel BuildGaugeCard(string title, Color accent,
        out Label valLabel, out Label subLabel, out Panel bar, out Panel fill)
    {
        var card = new Panel
        {
            Dock      = DockStyle.Fill,
            Margin    = new Padding(4),
            BackColor = Color.White,
            Padding   = new Padding(12)
        };

        var lTitle = new Label
        {
            Text      = title,
            ForeColor = accent,
            Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Location  = new Point(12, 10),
            AutoSize  = true
        };

        valLabel = new Label
        {
            Text      = "—",
            ForeColor = Color.FromArgb(15, 23, 42),
            Font      = new Font("Segoe UI", 22, FontStyle.Bold),
            Location  = new Point(10, 30),
            AutoSize  = true
        };

        subLabel = new Label
        {
            Text      = "— °C",
            ForeColor = Color.FromArgb(100, 116, 139),
            Font      = new Font("Segoe UI", 9f),
            Location  = new Point(12, 68),
            AutoSize  = true
        };

        bar = new Panel
        {
            Location  = new Point(12, 88),
            Height    = 6,
            Width     = 200,
            BackColor = Color.FromArgb(226, 232, 240) // Slate-200
        };
        fill = new Panel
        {
            Location  = new Point(0, 0),
            Height    = 6,
            Width     = 0,
            BackColor = accent
        };
        bar.Controls.Add(fill);

        var pnlBar = bar;
        card.Controls.AddRange([lTitle, valLabel, subLabel, bar]);
        card.Resize += (s, e) => { pnlBar.Width = card.Width - 24; };

        return card;
    }

    private static Panel BuildRamCard(out Label val, out Label info, out Panel bar, out Panel fill)
    {
        var card = BuildGaugeCard("RAM", Color.FromArgb(5, 150, 105), out val, out info, out bar, out fill);
        info.Text = "0 / 0 GB";
        return card;
    }

    private static Panel BuildInfoCard(string title, Color accent,
        out Label l1, out Label l2, out Label l3)
    {
        var card = new Panel
        {
            Dock      = DockStyle.Fill,
            Margin    = new Padding(4),
            BackColor = Color.White,
            Padding   = new Padding(12)
        };

        var lTitle = new Label
        {
            Text      = title,
            ForeColor = accent,
            Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Location  = new Point(12, 10),
            AutoSize  = true
        };

        l1 = new Label
        {
            Text      = "—",
            ForeColor = Color.FromArgb(15, 23, 42),
            Font      = new Font("Segoe UI", 11, FontStyle.Bold),
            Location  = new Point(12, 34),
            AutoSize  = true
        };
        l2 = new Label
        {
            Text      = "—",
            ForeColor = Color.FromArgb(15, 23, 42),
            Font      = new Font("Segoe UI", 11, FontStyle.Bold),
            Location  = new Point(12, 58),
            AutoSize  = true
        };
        l3 = new Label
        {
            Text      = "—",
            ForeColor = Color.FromArgb(100, 116, 139),
            Font      = new Font("Segoe UI", 8.5f),
            Location  = new Point(12, 82),
            AutoSize  = true
        };

        card.Controls.AddRange([lTitle, l1, l2, l3]);
        return card;
    }

    private static Panel BuildUptimeCard(out Label uptimeLabel)
    {
        var card = new Panel
        {
            Dock      = DockStyle.Fill,
            Margin    = new Padding(4),
            BackColor = Color.White,
            Padding   = new Padding(12)
        };

        var lTitle = new Label
        {
            Text      = "⏱ Uptime",
            ForeColor = Color.FromArgb(217, 119, 6),
            Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Location  = new Point(12, 10),
            AutoSize  = true
        };

        uptimeLabel = new Label
        {
            Text      = "0d 00:00:00",
            ForeColor = Color.FromArgb(15, 23, 42),
            Font      = new Font("Segoe UI", 14, FontStyle.Bold),
            Location  = new Point(12, 38),
            AutoSize  = true
        };

        card.Controls.AddRange([lTitle, uptimeLabel]);
        return card;
    }

    private static Label MakeSectionLabel(string text) => new()
    {
        Text      = text,
        Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold),
        ForeColor = Color.FromArgb(30, 41, 59),
        Location  = new Point(12, 12),
        AutoSize  = true
    };

    private static Button MakeButton(string text, Color bg) => new()
    {
        Text       = text,
        Height     = 32,
        BackColor  = bg,
        ForeColor  = Color.White,
        FlatStyle  = FlatStyle.Flat,
        Font       = new Font("Segoe UI", 9, FontStyle.Bold),
        Cursor     = Cursors.Hand,
        FlatAppearance = { BorderSize = 0 }
    };
}
