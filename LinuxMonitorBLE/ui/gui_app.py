"""
GuiApp: Giao diện Desktop GUI Dark Theme hiện đại cho Linux Monitor BLE.
Xây dựng trên nền Tkinter / TTK tương thích hoàn hảo với Ubuntu Desktop (GNOME/XFCE/KDE).
"""

import asyncio
import os
import queue
import sys
import threading
import time
import tkinter as tk
from tkinter import messagebox, ttk
from typing import Dict, List, Optional

try:
    from ..models.app_settings import AppSettings
    from ..models.system_metrics import SystemMetrics
    from ..services.ble_manager import BleManager, BleState
    from ..services.metrics_collector import LinuxMetricsCollector
except (ImportError, ValueError):
    from models.app_settings import AppSettings
    from models.system_metrics import SystemMetrics
    from services.ble_manager import BleManager, BleState
    from services.metrics_collector import LinuxMetricsCollector


class ModernCard(tk.Frame):
    """Thẻ hiển thị thông số bo góc hiện đại với Dark Theme."""

    def __init__(self, parent, title: str, accent_color: str = "#38BDF8", **kwargs):
        super().__init__(
            parent,
            bg="#1E293B",
            highlightbackground="#334155",
            highlightthickness=1,
            padx=12,
            pady=10,
            **kwargs,
        )
        self.accent_color = accent_color

        # Header Frame
        header = tk.Frame(self, bg="#1E293B")
        header.pack(fill=tk.X, pady=(0, 6))

        self.title_lbl = tk.Label(
            header,
            text=title,
            font=("DejaVu Sans", 11, "bold"),
            fg=accent_color,
            bg="#1E293B",
        )
        self.title_lbl.pack(side=tk.LEFT)

        self.sub_val_lbl = tk.Label(
            header,
            text="--",
            font=("DejaVu Sans", 10),
            fg="#94A3B8",
            bg="#1E293B",
        )
        self.sub_val_lbl.pack(side=tk.RIGHT)

        # Main Big Value Frame
        main_frame = tk.Frame(self, bg="#1E293B")
        main_frame.pack(fill=tk.X, pady=(0, 4))

        self.main_val_lbl = tk.Label(
            main_frame,
            text="0%",
            font=("DejaVu Sans", 20, "bold"),
            fg="#F8FAFC",
            bg="#1E293B",
        )
        self.main_val_lbl.pack(side=tk.LEFT)

        # Progress bar canvas
        self.canvas_bar = tk.Canvas(
            self,
            height=8,
            bg="#0F172A",
            highlightthickness=0,
            bd=0,
        )
        self.canvas_bar.pack(fill=tk.X, pady=(2, 6))

        # Details label
        self.details_lbl = tk.Label(
            self,
            text="Đang chờ dữ liệu...",
            font=("DejaVu Sans", 9),
            fg="#94A3B8",
            bg="#1E293B",
            justify=tk.LEFT,
            anchor="w",
        )
        self.details_lbl.pack(fill=tk.X)

    def set_data(
        self,
        main_text: str,
        percent: Optional[float] = None,
        sub_text: str = "",
        details_text: str = "",
    ):
        self.main_val_lbl.config(text=main_text)
        if sub_text:
            self.sub_val_lbl.config(text=sub_text)
        if details_text:
            self.details_lbl.config(text=details_text)

        # Draw bar
        self.canvas_bar.delete("all")
        if percent is not None:
            w = self.canvas_bar.winfo_width()
            if w <= 1:
                w = 200
            fill_w = max(0, min(w, int((percent / 100.0) * w)))
            color = self.accent_color
            if percent > 90:
                color = "#EF4444"
            elif percent > 75:
                color = "#F59E0B"
            self.canvas_bar.create_rectangle(
                0, 0, fill_w, 8, fill=color, outline=""
            )


class SettingsDialog(tk.Toplevel):
    """Hộp thoại Cài đặt nâng cao."""

    def __init__(self, parent, settings: AppSettings, on_save_callback):
        super().__init__(parent)
        self.title("⚙️ Cài đặt - Linux Monitor BLE")
        self.geometry("480x490")
        self.configure(bg="#0F172A")
        self.resizable(False, False)
        self.transient(parent)
        self.grab_set()

        self.settings = settings
        self.on_save = on_save_callback

        # Container
        container = tk.Frame(self, bg="#0F172A", padx=20, pady=16)
        container.pack(fill=tk.BOTH, expand=True)

        # Title
        tk.Label(
            container,
            text="TÙY CHỌN HỆ THỐNG & BLE",
            font=("DejaVu Sans", 12, "bold"),
            fg="#38BDF8",
            bg="#0F172A",
        ).pack(anchor="w", pady=(0, 16))

        # 1. Send Interval
        f1 = tk.Frame(container, bg="#0F172A")
        f1.pack(fill=tk.X, pady=6)
        tk.Label(
            f1,
            text="Chu kỳ gửi (ms):",
            fg="#F8FAFC",
            bg="#0F172A",
            font=("DejaVu Sans", 10),
            width=18,
            anchor="w",
        ).pack(side=tk.LEFT)
        self.interval_var = tk.StringVar(value=str(settings.send_interval_ms))
        interval_cb = ttk.Combobox(
            f1,
            textvariable=self.interval_var,
            values=["250", "500", "1000", "2000", "3000"],
            state="readonly",
            width=15,
        )
        interval_cb.pack(side=tk.LEFT)

        # 2. Network Adapter
        f2 = tk.Frame(container, bg="#0F172A")
        f2.pack(fill=tk.X, pady=6)
        tk.Label(
            f2,
            text="Card mạng theo dõi:",
            fg="#F8FAFC",
            bg="#0F172A",
            font=("DejaVu Sans", 10),
            width=18,
            anchor="w",
        ).pack(side=tk.LEFT)
        adapters = LinuxMetricsCollector.get_available_network_adapters()
        self.adapter_var = tk.StringVar(value=settings.selected_network_adapter)
        if settings.selected_network_adapter not in adapters:
            self.adapter_var.set("Auto")
        adapter_cb = ttk.Combobox(
            f2,
            textvariable=self.adapter_var,
            values=adapters,
            state="readonly",
            width=15,
        )
        adapter_cb.pack(side=tk.LEFT)

        # 3. Disk Mount Point
        f3 = tk.Frame(container, bg="#0F172A")
        f3.pack(fill=tk.X, pady=6)
        tk.Label(
            f3,
            text="Phân vùng ổ đĩa:",
            fg="#F8FAFC",
            bg="#0F172A",
            font=("DejaVu Sans", 10),
            width=18,
            anchor="w",
        ).pack(side=tk.LEFT)
        mounts = LinuxMetricsCollector.get_available_mounts()
        self.mount_var = tk.StringVar(value=settings.selected_disk_mount)
        if settings.selected_disk_mount not in mounts:
            self.mount_var.set("/")
        mount_cb = ttk.Combobox(
            f3,
            textvariable=self.mount_var,
            values=mounts,
            state="readonly",
            width=15,
        )
        mount_cb.pack(side=tk.LEFT)

        # 4. Checkboxes
        f4 = tk.Frame(container, bg="#0F172A")
        f4.pack(fill=tk.X, pady=10)

        self.autostart_var = tk.BooleanVar(value=settings.start_with_linux)
        tk.Checkbutton(
            f4,
            text="Tự động khởi động cùng Linux khi bật máy (Autostart)",
            variable=self.autostart_var,
            fg="#38BDF8",
            bg="#0F172A",
            selectcolor="#1E293B",
            activebackground="#0F172A",
            activeforeground="#38BDF8",
            font=("DejaVu Sans", 9, "bold"),
        ).pack(anchor="w", pady=2)

        self.daemon_autostart_var = tk.BooleanVar(value=settings.minimize_to_tray)
        tk.Checkbutton(
            f4,
            text="Khởi động ngầm (Headless Daemon, không mở cửa sổ)",
            variable=self.daemon_autostart_var,
            fg="#F8FAFC",
            bg="#0F172A",
            selectcolor="#1E293B",
            activebackground="#0F172A",
            activeforeground="#F8FAFC",
        ).pack(anchor="w", pady=2)

        self.auto_conn_var = tk.BooleanVar(value=settings.auto_connect)
        tk.Checkbutton(
            f4,
            text="Tự động tìm & kết nối ESP32 khi mở app",
            variable=self.auto_conn_var,
            fg="#F8FAFC",
            bg="#0F172A",
            selectcolor="#1E293B",
            activebackground="#0F172A",
            activeforeground="#F8FAFC",
        ).pack(anchor="w", pady=2)

        self.auto_reconn_var = tk.BooleanVar(value=settings.auto_reconnect)
        tk.Checkbutton(
            f4,
            text="Tự động kết nối lại khi mất sóng BLE",
            variable=self.auto_reconn_var,
            fg="#F8FAFC",
            bg="#0F172A",
            selectcolor="#1E293B",
            activebackground="#0F172A",
            activeforeground="#F8FAFC",
        ).pack(anchor="w", pady=2)

        self.gpu_var = tk.BooleanVar(value=settings.enable_gpu_monitoring)
        tk.Checkbutton(
            f4,
            text="Bật giám sát GPU (NVIDIA / AMD / Intel)",
            variable=self.gpu_var,
            fg="#F8FAFC",
            bg="#0F172A",
            selectcolor="#1E293B",
            activebackground="#0F172A",
            activeforeground="#F8FAFC",
        ).pack(anchor="w", pady=2)

        # Buttons
        btn_frame = tk.Frame(container, bg="#0F172A")
        btn_frame.pack(fill=tk.X, side=tk.BOTTOM, pady=(16, 0))

        tk.Button(
            btn_frame,
            text="Hủy bỏ",
            font=("DejaVu Sans", 10),
            bg="#334155",
            fg="#F8FAFC",
            bd=0,
            padx=16,
            pady=6,
            command=self.destroy,
        ).pack(side=tk.RIGHT, padx=4)

        tk.Button(
            btn_frame,
            text="Lưu cấu hình",
            font=("DejaVu Sans", 10, "bold"),
            bg="#10B981",
            fg="#FFFFFF",
            bd=0,
            padx=16,
            pady=6,
            command=self._save_and_close,
        ).pack(side=tk.RIGHT, padx=4)

    def _save_and_close(self):
        try:
            self.settings.send_interval_ms = int(self.interval_var.get())
        except ValueError:
            self.settings.send_interval_ms = 1000

        self.settings.selected_network_adapter = self.adapter_var.get()
        self.settings.selected_disk_mount = self.mount_var.get()
        self.settings.start_with_linux = self.autostart_var.get()
        self.settings.minimize_to_tray = self.daemon_autostart_var.get()
        self.settings.auto_connect = self.auto_conn_var.get()
        self.settings.auto_reconnect = self.auto_reconn_var.get()
        self.settings.enable_gpu_monitoring = self.gpu_var.get()

        self.settings.save()
        self.on_save(self.settings)
        self.destroy()


class GuiApp:
    def __init__(self, root: tk.Tk, settings: AppSettings):
        self.root = root
        self.settings = settings

        self.root.title("Linux Monitor BLE — ESP32 Telemetry (Ubuntu 26)")
        self.root.geometry("820x680")
        self.root.minsize(740, 600)
        self.root.configure(bg="#0F172A")

        # Services
        self.collector = LinuxMetricsCollector(
            network_adapter=settings.selected_network_adapter,
            disk_device=settings.selected_disk_device,
            disk_mount=settings.selected_disk_mount,
            enable_gpu=settings.enable_gpu_monitoring,
        )
        self.ble = BleManager(target_name=settings.target_device_name)
        self.ble.auto_reconnect = settings.auto_reconnect

        # Async Loop Thread for BLE
        self.ble_loop: Optional[asyncio.AbstractEventLoop] = None
        self.ble_thread: Optional[threading.Thread] = None
        self._start_ble_background_thread()

        # Thread-safe UI event queue
        self.ui_queue: queue.Queue = queue.Queue()

        # Wire up BLE Callbacks
        self.ble.on_state_changed = self._on_ble_state_changed
        self.ble.on_log = self._on_ble_log
        self.ble.on_esp32_status = self._on_esp32_status

        # Build UI layout
        self._setup_styles()
        self._create_layout()

        # Polling loops
        self._running = True
        self.root.after(100, self._process_ui_queue)
        self.root.after(200, self._metrics_tick)

        # Auto connect on launch if configured
        if self.settings.auto_connect:
            self.root.after(800, self._connect_ble)

        self.root.protocol("WM_DELETE_WINDOW", self._on_close)

    def _setup_styles(self):
        style = ttk.Style()
        style.theme_use("clam")
        style.configure(
            "TCombobox",
            fieldbackground="#1E293B",
            background="#334155",
            foreground="#F8FAFC",
            arrowcolor="#38BDF8",
            bordercolor="#334155",
        )

    def _create_layout(self):
        # Main container with padding
        main_pad = tk.Frame(self.root, bg="#0F172A", padx=16, pady=14)
        main_pad.pack(fill=tk.BOTH, expand=True)

        # ── 1. HEADER & STATUS BAR ───────────────────────────────────────
        header_frame = tk.Frame(main_pad, bg="#0F172A")
        header_frame.pack(fill=tk.X, pady=(0, 12))

        # Title
        title_box = tk.Frame(header_frame, bg="#0F172A")
        title_box.pack(side=tk.LEFT)

        tk.Label(
            title_box,
            text="🐧 LINUX MONITOR BLE",
            font=("DejaVu Sans", 14, "bold"),
            fg="#38BDF8",
            bg="#0F172A",
        ).pack(anchor="w")

        tk.Label(
            title_box,
            text="Ubuntu 26 / Linux → ESP32 OLED Telemetry",
            font=("DejaVu Sans", 9),
            fg="#94A3B8",
            bg="#0F172A",
        ).pack(anchor="w")

        # BLE Status Pill Badge
        status_box = tk.Frame(header_frame, bg="#0F172A")
        status_box.pack(side=tk.RIGHT)

        self.status_pill = tk.Label(
            status_box,
            text="● DISCONNECTED",
            font=("DejaVu Sans", 9, "bold"),
            fg="#EF4444",
            bg="#1E293B",
            padx=12,
            pady=6,
            highlightbackground="#334155",
            highlightthickness=1,
        )
        self.status_pill.pack(side=tk.RIGHT, padx=4)

        self.settings_btn = tk.Button(
            status_box,
            text="⚙️ Cài đặt",
            font=("DejaVu Sans", 9),
            bg="#334155",
            fg="#F8FAFC",
            activebackground="#475569",
            activeforeground="#FFFFFF",
            bd=0,
            padx=10,
            pady=5,
            command=self._open_settings,
        )
        self.settings_btn.pack(side=tk.RIGHT, padx=4)

        # ── 2. BLE CONTROLS BAR ──────────────────────────────────────────
        ctrl_frame = tk.Frame(
            main_pad,
            bg="#1E293B",
            highlightbackground="#334155",
            highlightthickness=1,
            padx=12,
            pady=8,
        )
        ctrl_frame.pack(fill=tk.X, pady=(0, 14))

        tk.Label(
            ctrl_frame,
            text="Thiết bị BLE:",
            font=("DejaVu Sans", 9, "bold"),
            fg="#F8FAFC",
            bg="#1E293B",
        ).pack(side=tk.LEFT, padx=(0, 6))

        self.device_entry = tk.Entry(
            ctrl_frame,
            font=("DejaVu Sans", 9),
            bg="#0F172A",
            fg="#F8FAFC",
            insertbackground="#F8FAFC",
            bd=1,
            relief=tk.SOLID,
            width=18,
        )
        self.device_entry.insert(0, self.settings.target_device_name)
        self.device_entry.pack(side=tk.LEFT, padx=(0, 8))

        self.scan_btn = tk.Button(
            ctrl_frame,
            text="🔍 Quét BLE",
            font=("DejaVu Sans", 9),
            bg="#475569",
            fg="#F8FAFC",
            bd=0,
            padx=10,
            pady=4,
            command=self._scan_ble,
        )
        self.scan_btn.pack(side=tk.LEFT, padx=(0, 6))

        self.conn_btn = tk.Button(
            ctrl_frame,
            text="🔗 Kết nối",
            font=("DejaVu Sans", 9, "bold"),
            bg="#10B981",
            fg="#FFFFFF",
            bd=0,
            padx=14,
            pady=4,
            command=self._toggle_connection,
        )
        self.conn_btn.pack(side=tk.LEFT, padx=(0, 12))

        # Packet stats
        self.pkt_stats_lbl = tk.Label(
            ctrl_frame,
            text="Gói gửi: ↑0 | Lỗi: 0",
            font=("DejaVu Sans", 9),
            fg="#94A3B8",
            bg="#1E293B",
        )
        self.pkt_stats_lbl.pack(side=tk.RIGHT)

        # ── 3. 2x2 METRICS CARDS GRID ────────────────────────────────────
        cards_grid = tk.Frame(main_pad, bg="#0F172A")
        cards_grid.pack(fill=tk.BOTH, expand=True, pady=(0, 12))
        cards_grid.columnconfigure(0, weight=1, uniform="col")
        cards_grid.columnconfigure(1, weight=1, uniform="col")
        cards_grid.rowconfigure(0, weight=1, uniform="row")
        cards_grid.rowconfigure(1, weight=1, uniform="row")

        # Card 1: CPU (Cyan)
        self.cpu_card = ModernCard(cards_grid, title="⚡ CPU USAGE", accent_color="#38BDF8")
        self.cpu_card.grid(row=0, column=0, padx=(0, 6), pady=(0, 6), sticky="nsew")

        # Card 2: RAM (Emerald)
        self.ram_card = ModernCard(cards_grid, title="🧠 RAM MEMORY", accent_color="#10B981")
        self.ram_card.grid(row=0, column=1, padx=(6, 0), pady=(0, 6), sticky="nsew")

        # Card 3: GPU & Disk (Amber)
        self.gpu_disk_card = ModernCard(cards_grid, title="🎮 GPU & DISK SPEED", accent_color="#F59E0B")
        self.gpu_disk_card.grid(row=1, column=0, padx=(0, 6), pady=(6, 0), sticky="nsew")

        # Card 4: Network & System (Indigo)
        self.net_sys_card = ModernCard(cards_grid, title="🌐 NETWORK & SYSTEM", accent_color="#818CF8")
        self.net_sys_card.grid(row=1, column=1, padx=(6, 0), pady=(6, 0), sticky="nsew")

        # ── 4. LOG CONSOLE ───────────────────────────────────────────────
        log_frame = tk.Frame(
            main_pad,
            bg="#1E293B",
            highlightbackground="#334155",
            highlightthickness=1,
            padx=8,
            pady=6,
        )
        log_frame.pack(fill=tk.X)

        log_head = tk.Frame(log_frame, bg="#1E293B")
        log_head.pack(fill=tk.X, pady=(0, 4))
        tk.Label(
            log_head,
            text="📋 Nhật ký hoạt động (Live Logs):",
            font=("DejaVu Sans", 8, "bold"),
            fg="#94A3B8",
            bg="#1E293B",
        ).pack(side=tk.LEFT)

        tk.Button(
            log_head,
            text="Xóa log",
            font=("DejaVu Sans", 8),
            bg="#334155",
            fg="#94A3B8",
            bd=0,
            padx=6,
            pady=1,
            command=self._clear_logs,
        ).pack(side=tk.RIGHT)

        self.log_text = tk.Text(
            log_frame,
            height=4,
            bg="#0F172A",
            fg="#94A3B8",
            font=("DejaVu Sans Mono", 8),
            bd=0,
            state=tk.DISABLED,
        )
        self.log_text.pack(fill=tk.X)

    # ─────────────────────────────────────────────────────────────
    # Async Event Loop Thread for BLE
    # ─────────────────────────────────────────────────────────────

    def _start_ble_background_thread(self):
        def loop_runner():
            self.ble_loop = asyncio.new_event_loop()
            asyncio.set_event_loop(self.ble_loop)
            self.ble_loop.run_forever()

        self.ble_thread = threading.Thread(target=loop_runner, daemon=True)
        self.ble_thread.start()

    def _run_async(self, coro):
        """Đưa coroutine vào Event Loop của BLE chạy nền."""
        if self.ble_loop and self.ble_loop.is_running():
            asyncio.run_coroutine_threadsafe(coro, self.ble_loop)

    # ─────────────────────────────────────────────────────────────
    # BLE Callbacks & Events
    # ─────────────────────────────────────────────────────────────

    def _on_ble_state_changed(self, state: BleState):
        self.ui_queue.put(("state", state))

    def _on_ble_log(self, message: str):
        self.ui_queue.put(("log", message))

    def _on_esp32_status(self, msg: str):
        self.ui_queue.put(("esp32_msg", msg))

    def _process_ui_queue(self):
        """Xử lý các event từ background BLE thread gửi sang UI main thread."""
        try:
            while not self.ui_queue.empty():
                evt, data = self.ui_queue.get_nowait()
                if evt == "state":
                    self._update_state_ui(data)
                elif evt == "log":
                    self._append_log(data)
                elif evt == "esp32_msg":
                    self._append_log(f"ESP32 Phản hồi: {data}")
        except Exception:
            pass

        if self._running:
            self.root.after(50, self._process_ui_queue)

    def _update_state_ui(self, state: BleState):
        if state == BleState.CONNECTED:
            self.status_pill.config(
                text=f"● CONNECTED ({self.ble.connected_name or 'ESP32'})",
                fg="#10B981",
            )
            self.conn_btn.config(text="🔌 Ngắt kết nối", bg="#EF4444")
            self.scan_btn.config(state=tk.DISABLED)
        elif state == BleState.CONNECTING:
            self.status_pill.config(text="● CONNECTING...", fg="#F59E0B")
            self.conn_btn.config(text="Đang kết nối...", bg="#F59E0B")
            self.scan_btn.config(state=tk.DISABLED)
        elif state == BleState.SCANNING:
            self.status_pill.config(text="● SCANNING...", fg="#38BDF8")
            self.conn_btn.config(text="Đang quét...", bg="#38BDF8")
            self.scan_btn.config(state=tk.DISABLED)
        else:
            self.status_pill.config(text="● DISCONNECTED", fg="#EF4444")
            self.conn_btn.config(text="🔗 Kết nối", bg="#10B981")
            self.scan_btn.config(state=tk.NORMAL)

    def _append_log(self, text: str):
        self.log_text.config(state=tk.NORMAL)
        ts = time.strftime("%H:%M:%S")
        self.log_text.insert(tk.END, f"[{ts}] {text}\n")
        self.log_text.see(tk.END)
        self.log_text.config(state=tk.DISABLED)

    def _clear_logs(self):
        self.log_text.config(state=tk.NORMAL)
        self.log_text.delete("1.0", tk.END)
        self.log_text.config(state=tk.DISABLED)

    # ─────────────────────────────────────────────────────────────
    # Actions
    # ─────────────────────────────────────────────────────────────

    def _toggle_connection(self):
        if self.ble.state == BleState.CONNECTED:
            self._disconnect_ble()
        elif self.ble.state == BleState.DISCONNECTED:
            self._connect_ble()

    def _connect_ble(self):
        target_name = self.device_entry.get().strip() or "ESP32Monitor"
        self.ble.target_name = target_name
        self.settings.target_device_name = target_name
        self._run_async(self.ble.connect())

    def _disconnect_ble(self):
        self._run_async(self.ble.disconnect())

    def _scan_ble(self):
        self._run_async(self.ble.scan_for_devices(timeout=5.0))

    def _open_settings(self):
        SettingsDialog(self.root, self.settings, self._on_settings_saved)

    def _on_settings_saved(self, updated: AppSettings):
        self.collector.network_adapter = updated.selected_network_adapter
        self.collector.disk_mount = updated.selected_disk_mount
        self.collector.enable_gpu = updated.enable_gpu_monitoring
        self.ble.auto_reconnect = updated.auto_reconnect
        self._append_log("Đã cập nhật cấu hình mới thành công.")

    # ─────────────────────────────────────────────────────────────
    # Main Metrics Loop (Tick)
    # ─────────────────────────────────────────────────────────────

    def _format_speed(self, kbps: float) -> str:
        if kbps >= 10240:
            return f"{kbps / 1024:.1f} MB/s"
        elif kbps >= 1024:
            return f"{kbps / 1024:.2f} MB/s"
        else:
            return f"{kbps:.0f} KB/s"

    def _format_uptime(self, sec: int) -> str:
        h = sec // 3600
        m = (sec % 3600) // 60
        s = sec % 60
        return f"{h:02d}:{m:02d}:{s:02d}"

    def _metrics_tick(self):
        """Thu thập chỉ số phần cứng và gửi qua BLE nếu kết nối."""
        if not self._running:
            return

        metrics = self.collector.collect()

        # Update Card 1: CPU
        cpu_temp = f"{metrics.cpu_temperature_celsius:.0f}°C" if metrics.cpu_temperature_celsius is not None else "N/A"
        self.cpu_card.set_data(
            main_text=f"{metrics.cpu_usage_percent:.0f}%",
            percent=metrics.cpu_usage_percent,
            sub_text=f"Nhiệt độ: {cpu_temp}",
            details_text=f"Xung nhịp: {metrics.cpu_frequency_mhz} MHz | Số luồng: {metrics.logical_processors}",
        )

        # Update Card 2: RAM
        used_gb = metrics.ram_used_mb / 1024.0
        total_gb = metrics.ram_total_mb / 1024.0
        self.ram_card.set_data(
            main_text=f"{metrics.ram_usage_percent:.0f}%",
            percent=metrics.ram_usage_percent,
            sub_text=f"{used_gb:.1f} / {total_gb:.1f} GB",
            details_text=f"Đã dùng: {metrics.ram_used_mb} MB | Còn trống: {metrics.ram_total_mb - metrics.ram_used_mb} MB",
        )

        # Update Card 3: GPU & Disk Speed
        if metrics.gpu_usage_percent is not None:
            gpu_t = f"{metrics.gpu_temperature_celsius:.0f}°C" if metrics.gpu_temperature_celsius is not None else "N/A"
            gpu_str = f"GPU: {metrics.gpu_usage_percent:.0f}% ({gpu_t})"
            gpu_pct = metrics.gpu_usage_percent
        else:
            gpu_str = "GPU: N/A (Tích hợp)"
            gpu_pct = 0

        self.gpu_disk_card.set_data(
            main_text=f"{metrics.disk_usage_percent:.0f}% DSK",
            percent=metrics.disk_usage_percent,
            sub_text=gpu_str,
            details_text=f"Ổ đĩa: Đọc ↑{self._format_speed(metrics.disk_write_kbps)} | Ghi ↓{self._format_speed(metrics.disk_read_kbps)}",
        )

        # Update Card 4: Network & System
        up_s = self._format_speed(metrics.network_sent_kbps)
        down_s = self._format_speed(metrics.network_received_kbps)
        self.net_sys_card.set_data(
            main_text=f"↑{up_s}",
            percent=min(100.0, (metrics.network_sent_kbps + metrics.network_received_kbps) / 200.0),
            sub_text=f"↓ {down_s}",
            details_text=f"Card: {self.settings.selected_network_adapter} | Uptime: {self._format_uptime(metrics.uptime_seconds)}",
        )

        # Gửi BLE nếu đang kết nối
        if self.ble.state == BleState.CONNECTED:
            self._run_async(self.ble.send_metrics(metrics))

        # Cập nhật thống kê gói tin
        self.pkt_stats_lbl.config(
            text=f"Gói gửi: ↑{self.ble.packets_sent} | Lỗi: {self.ble.packets_failed}"
        )

        # Lặp lại theo interval
        interval = max(200, self.settings.send_interval_ms)
        self.root.after(interval, self._metrics_tick)

    def _on_close(self):
        self._running = False
        if self.ble_loop and self.ble_loop.is_running():
            asyncio.run_coroutine_threadsafe(self.ble.disconnect(), self.ble_loop)
            self.ble_loop.call_soon_threadsafe(self.ble_loop.stop)
        self.root.destroy()
