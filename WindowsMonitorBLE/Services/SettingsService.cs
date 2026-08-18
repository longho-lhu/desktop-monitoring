using System.Text.Json;
using Microsoft.Win32;
using WindowsMonitorBLE.Models;

namespace WindowsMonitorBLE.Services;

/// <summary>
/// Quản lý cấu hình ứng dụng và thiết lập Khởi động cùng Windows (Registry).
/// </summary>
public static class SettingsService
{
    private const string AppName = "WindowsMonitorBLE";
    private const string RegistryRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        AppName
    );
    
    private static readonly string SettingsFilePath = Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Đọc cấu hình từ file settings.json (nếu chưa có sẽ tạo mặc định).
    /// </summary>
    public static AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                string json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (settings != null)
                {
                    // Đồng bộ trạng thái thực tế từ Registry
                    settings.StartWithWindows = IsAutoStartEnabled();
                    return settings;
                }
            }
        }
        catch { }

        var defaultSettings = new AppSettings
        {
            StartWithWindows = IsAutoStartEnabled()
        };
        SaveSettings(defaultSettings);
        return defaultSettings;
    }

    /// <summary>
    /// Lưu cấu hình vào file settings.json và cập nhật Registry.
    /// </summary>
    public static void SaveSettings(AppSettings settings)
    {
        try
        {
            if (!Directory.Exists(SettingsDir))
                Directory.CreateDirectory(SettingsDir);

            string json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsFilePath, json);

            // Cập nhật Registry tự khởi động
            SetAutoStart(settings.StartWithWindows);
        }
        catch { }
    }

    /// <summary>
    /// Kiểm tra ứng dụng có đang được đăng ký khởi động cùng Windows hay không.
    /// </summary>
    public static bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, false);
            var val = key?.GetValue(AppName);
            return val != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Bật/tắt tự động khởi động cùng Windows qua Registry HKCU\Run.
    /// </summary>
    public static void SetAutoStart(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, true);
            if (key == null) return;

            if (enable)
            {
                // Lấy đường dẫn exe hiện tại
                string exePath = Environment.ProcessPath ?? Application.ExecutablePath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(AppName, $"\"{exePath}\"");
                }
            }
            else
            {
                key.DeleteValue(AppName, false);
            }
        }
        catch { }
    }
}
