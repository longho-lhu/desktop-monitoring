namespace WindowsMonitorBLE;

static class Program
{
    [STAThread]
    static void Main()
    {
        // Global exception handling để nếu có lỗi sẽ hiển thị hộp thoại
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (s, e) =>
        {
            MessageBox.Show($"Lỗi giao diện: {e.Exception.Message}\n\n{e.Exception.StackTrace}",
                "Lỗi Windows Monitor", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                MessageBox.Show($"Lỗi hệ thống: {ex.Message}\n\n{ex.StackTrace}",
                    "Lỗi nghiêm trọng", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        try
        {
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể mở ứng dụng: {ex.Message}\n\n{ex.StackTrace}",
                "Lỗi khởi động", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
