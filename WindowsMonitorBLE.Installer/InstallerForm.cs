using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using Microsoft.Win32;

namespace WindowsMonitorBLE.Installer;

public class InstallerForm : Form
{
    private TextBox txtPath = null!;
    private Button btnBrowse = null!;
    private CheckBox chkDesktop = null!;
    private CheckBox chkAutoStart = null!;
    private ProgressBar prgInstall = null!;
    private Label lblStatus = null!;
    private Button btnInstall = null!;
    private Button btnCancel = null!;

    private bool _isCompleted = false;

    public InstallerForm()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();

        // Form settings
        this.Text            = "Cài đặt — Windows Monitor BLE";
        this.Size            = new Size(560, 390);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox     = false;
        this.MinimizeBox     = true;
        this.StartPosition   = FormStartPosition.CenterScreen;
        this.BackColor       = Color.FromArgb(241, 245, 249);
        this.ForeColor       = Color.FromArgb(15, 23, 42);
        this.Font            = new Font("Segoe UI", 9.5f, FontStyle.Regular);

        // Header Panel
        var pnlHeader = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 70,
            BackColor = Color.White,
            Padding   = new Padding(16, 12, 16, 12)
        };

        var pnlBorder = new Panel
        {
            Dock      = DockStyle.Bottom,
            Height    = 1,
            BackColor = Color.FromArgb(226, 232, 240)
        };
        pnlHeader.Controls.Add(pnlBorder);

        var lblTitle = new Label
        {
            Text      = "🖥  CÀI ĐẶT WINDOWS MONITOR BLE",
            Font      = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location  = new Point(16, 12),
            AutoSize  = true
        };

        var lblSub = new Label
        {
            Text      = "Truyền thông số phần cứng Windows sang ESP32-C3 qua BLE",
            Font      = new Font("Segoe UI", 9f, FontStyle.Regular),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location  = new Point(18, 38),
            AutoSize  = true
        };
        pnlHeader.Controls.AddRange([lblTitle, lblSub]);

        // Content Panel
        var pnlContent = new Panel
        {
            Dock      = DockStyle.Fill,
            Padding   = new Padding(24, 16, 24, 16),
            BackColor = Color.FromArgb(241, 245, 249)
        };

        var lblPathTitle = new Label
        {
            Text      = "Thư mục cài đặt ứng dụng:",
            Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(51, 65, 85),
            Location  = new Point(24, 20),
            AutoSize  = true
        };

        string defaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "WindowsMonitorBLE"
        );

        txtPath = new TextBox
        {
            Text      = defaultPath,
            Location  = new Point(24, 46),
            Width     = 390,
            Height    = 28,
            Font      = new Font("Segoe UI", 9.5f),
            BackColor = Color.White
        };

        btnBrowse = new Button
        {
            Text      = "Duyệt...",
            Location  = new Point(422, 44),
            Width     = 95,
            Height    = 30,
            Font      = new Font("Segoe UI", 9f),
            BackColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor    = Cursors.Hand
        };
        btnBrowse.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnBrowse.Click += (s, e) =>
        {
            using var fbd = new FolderBrowserDialog { SelectedPath = txtPath.Text };
            if (fbd.ShowDialog() == DialogResult.OK)
            {
                txtPath.Text = Path.Combine(fbd.SelectedPath, "WindowsMonitorBLE");
            }
        };

        chkDesktop = new CheckBox
        {
            Text      = "Tạo biểu tượng trên màn hình Desktop",
            Location  = new Point(24, 88),
            Width     = 450,
            Checked   = true,
            AutoSize  = true,
            Cursor    = Cursors.Hand
        };

        chkAutoStart = new CheckBox
        {
            Text      = "Tự động khởi động cùng Windows khi bật máy",
            Location  = new Point(24, 116),
            Width     = 450,
            Checked   = false,
            AutoSize  = true,
            Cursor    = Cursors.Hand
        };

        prgInstall = new ProgressBar
        {
            Location  = new Point(24, 152),
            Width     = 493,
            Height    = 18,
            Visible   = false
        };

        lblStatus = new Label
        {
            Text      = "Sẵn sàng cài đặt. Nhấn 'Cài đặt ngay' để tiếp tục.",
            Font      = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location  = new Point(24, 178),
            Width     = 493,
            AutoSize  = true
        };

        pnlContent.Controls.AddRange([
            lblPathTitle, txtPath, btnBrowse,
            chkDesktop, chkAutoStart,
            prgInstall, lblStatus
        ]);

        // Footer Panel
        var pnlFooter = new Panel
        {
            Dock      = DockStyle.Bottom,
            Height    = 60,
            BackColor = Color.White,
            Padding   = new Padding(16, 12, 16, 12)
        };

        var pnlFooterBorder = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 1,
            BackColor = Color.FromArgb(226, 232, 240)
        };
        pnlFooter.Controls.Add(pnlFooterBorder);

        btnInstall = new Button
        {
            Text       = "🚀 Cài đặt ngay",
            Font       = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            BackColor  = Color.FromArgb(16, 185, 129),
            ForeColor  = Color.White,
            FlatStyle  = FlatStyle.Flat,
            Width      = 150,
            Height     = 36,
            Location   = new Point(265, 12),
            Cursor     = Cursors.Hand,
            FlatAppearance = { BorderSize = 0 }
        };
        btnInstall.Click += BtnInstall_Click;

        btnCancel = new Button
        {
            Text       = "Hủy bỏ",
            Font       = new Font("Segoe UI", 9.5f),
            BackColor  = Color.FromArgb(241, 245, 249),
            ForeColor  = Color.FromArgb(71, 85, 105),
            FlatStyle  = FlatStyle.Flat,
            Width      = 100,
            Height     = 36,
            Location   = new Point(425, 12),
            Cursor     = Cursors.Hand
        };
        btnCancel.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnCancel.Click += (s, e) => this.Close();

        pnlFooter.Controls.AddRange([btnInstall, btnCancel]);

        // Assemble Form
        this.Controls.Add(pnlContent);
        this.Controls.Add(pnlFooter);
        this.Controls.Add(pnlHeader);

        this.ResumeLayout(false);
    }

    private async void BtnInstall_Click(object? sender, EventArgs e)
    {
        if (_isCompleted)
        {
            // Mở app và thoát setup
            string exe = Path.Combine(txtPath.Text, "WindowsMonitorBLE.exe");
            if (File.Exists(exe))
            {
                Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
            }
            this.Close();
            return;
        }

        string targetDir = txtPath.Text.Trim();
        if (string.IsNullOrEmpty(targetDir))
        {
            MessageBox.Show("Vui lòng chọn thư mục cài đặt hợp lệ!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        btnInstall.Enabled = false;
        btnCancel.Enabled  = false;
        btnBrowse.Enabled  = false;
        txtPath.Enabled    = false;
        chkDesktop.Enabled = false;
        chkAutoStart.Enabled = false;

        prgInstall.Visible = true;
        prgInstall.Value   = 10;

        lblStatus.Text = "Đang chuẩn bị cài đặt...";

        try
        {
            await Task.Run(() => PerformInstallation(targetDir));

            prgInstall.Value = 100;
            lblStatus.Text = "✅ Cài đặt hoàn tất thành công!";
            lblStatus.ForeColor = Color.FromArgb(16, 185, 129);

            _isCompleted = true;
            btnInstall.Text = "✨ Mở ứng dụng ngay";
            btnInstall.BackColor = Color.FromArgb(37, 99, 235);
            btnInstall.Enabled = true;
            btnCancel.Text = "Đóng";
            btnCancel.Enabled = true;
        }
        catch (Exception ex)
        {
            prgInstall.Visible = false;
            lblStatus.Text = $"❌ Lỗi khi cài đặt: {ex.Message}";
            lblStatus.ForeColor = Color.FromArgb(220, 38, 38);
            btnInstall.Enabled = true;
            btnCancel.Enabled = true;
            btnBrowse.Enabled = true;
            txtPath.Enabled = true;
        }
    }

    private void PerformInstallation(string targetDir)
    {
        // 1. Tắt tiến trình cũ nếu đang mở
        UpdateProgress(20, "Đang kiểm tra tiến trình...");
        foreach (var p in Process.GetProcessesByName("WindowsMonitorBLE"))
        {
            try { p.Kill(); p.WaitForExit(2000); } catch { }
        }

        // 2. Tạo thư mục đích
        UpdateProgress(30, "Đang tạo thư mục...");
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        // 3. Giải nén payload.zip nhúng bên trong file Setup.exe
        UpdateProgress(50, "Đang trích xuất dữ liệu ứng dụng...");
        var assembly = Assembly.GetExecutingAssembly();
        using (var stream = assembly.GetManifestResourceStream("payload.zip"))
        {
            if (stream == null)
                throw new InvalidOperationException("Không tìm thấy payload.zip bên trong bộ cài đặt!");

            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue;
                string destinationPath = Path.Combine(targetDir, entry.FullName);
                string? dir = Path.GetDirectoryName(destinationPath);
                if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                entry.ExtractToFile(destinationPath, overwrite: true);
            }
        }

        // 4. Tạo Uninstaller Script
        UpdateProgress(70, "Đang tạo công cụ gỡ cài đặt...");
        string uninstallerPath = Path.Combine(targetDir, "GoCaiDat.bat");
        string uninstallScript = $@"@echo off
title Go Cai Dat Windows Monitor BLE
echo Dang go bo Windows Monitor BLE...
taskkill /F /IM WindowsMonitorBLE.exe >nul 2>&1
timeout /t 1 /nobreak >nul
del /f /q ""%USERPROFILE%\Desktop\Windows Monitor BLE.lnk"" >nul 2>&1
del /f /q ""%APPDATA%\Microsoft\Windows\Start Menu\Programs\Windows Monitor BLE.lnk"" >nul 2>&1
reg delete ""HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\WindowsMonitorBLE"" /f >nul 2>&1
reg delete ""HKCU\Software\Microsoft\Windows\CurrentVersion\Run"" /v ""WindowsMonitorBLE"" /f >nul 2>&1
cd /d ""%TEMP%""
rd /s /q ""{targetDir}"" >nul 2>&1
echo [OK] Da go cai dat thanh cong!
pause
";
        File.WriteAllText(uninstallerPath, uninstallScript);

        // 5. Tạo Shortcuts
        UpdateProgress(85, "Đang tạo biểu tượng Desktop và Start Menu...");
        string exePath = Path.Combine(targetDir, "WindowsMonitorBLE.exe");

        if (chkDesktop.Checked)
        {
            string desktopLnk = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "Windows Monitor BLE.lnk"
            );
            CreateShortcut(desktopLnk, exePath, targetDir, "Windows Monitor BLE");
        }

        string startMenuDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Microsoft\Windows\Start Menu\Programs"
        );
        string startMenuLnk = Path.Combine(startMenuDir, "Windows Monitor BLE.lnk");
        CreateShortcut(startMenuLnk, exePath, targetDir, "Windows Monitor BLE");

        // 6. Đăng ký Uninstall trong Windows Control Panel
        UpdateProgress(95, "Đang đăng ký vào hệ thống Windows...");
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\WindowsMonitorBLE");
            key.SetValue("DisplayName", "Windows Monitor BLE");
            key.SetValue("DisplayVersion", "1.0.0");
            key.SetValue("Publisher", "Antigravity Dev");
            key.SetValue("InstallLocation", targetDir);
            key.SetValue("UninstallString", $"\"{uninstallerPath}\"");
            key.SetValue("DisplayIcon", $"\"{exePath}\",0");
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        }
        catch { }

        // 7. Khởi động cùng Windows nếu chọn
        if (chkAutoStart.Checked)
        {
            try
            {
                using var runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                runKey?.SetValue("WindowsMonitorBLE", $"\"{exePath}\"");
            }
            catch { }
        }
    }

    private void UpdateProgress(int val, string text)
    {
        if (InvokeRequired)
        {
            Invoke(() => UpdateProgress(val, text));
            return;
        }
        prgInstall.Value = val;
        lblStatus.Text   = text;
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string workingDir, string description)
    {
        try
        {
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType != null)
            {
                dynamic? shell = Activator.CreateInstance(shellType);
                if (shell != null)
                {
                    dynamic shortcut = shell.CreateShortcut(shortcutPath);
                    shortcut.TargetPath = targetPath;
                    shortcut.WorkingDirectory = workingDir;
                    shortcut.Description = description;
                    shortcut.Save();
                }
            }
        }
        catch { }
    }
}
