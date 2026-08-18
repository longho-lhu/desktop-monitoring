"""
LinuxMetricsCollector: Thu thập thông số phần cứng tối ưu hiệu năng trên Linux / Ubuntu.
Đọc trực tiếp từ /proc, /sys, psutil và GPU drivers (NVIDIA / AMD / Intel).
"""

import os
import subprocess
import time
from typing import Dict, List, Optional, Tuple

try:
    import psutil
except ImportError:
    psutil = None

try:
    from ..models.system_metrics import SystemMetrics
except (ImportError, ValueError):
    from models.system_metrics import SystemMetrics


class LinuxMetricsCollector:
    def __init__(
        self,
        network_adapter: str = "Auto",
        disk_device: str = "All",
        disk_mount: str = "/",
        enable_gpu: bool = True,
    ):
        self.network_adapter = network_adapter
        self.disk_device = disk_device
        self.disk_mount = disk_mount
        self.enable_gpu = enable_gpu

        # Disk I/O tracking
        self._last_disk_time = time.monotonic()
        self._last_disk_read_bytes = 0
        self._last_disk_write_bytes = 0

        # Network I/O tracking
        self._last_net_time = time.monotonic()
        self._last_net_sent_bytes = 0
        self._last_net_recv_bytes = 0

        # Cached sensor values (Throttled update để tiết kiệm CPU)
        self._cached_cpu_temp: Optional[float] = None
        self._cached_gpu_usage: Optional[float] = None
        self._cached_gpu_temp: Optional[float] = None
        self._cached_gpu_vram_mb: Optional[int] = None
        self._last_sensor_update = 0.0
        self._sensor_update_interval = 2.5  # cập nhật cảm biến mỗi 2.5s

        # Cached disk usage
        self._cached_disk_usage = 0.0
        self._last_disk_usage_update = 0.0
        self._disk_usage_interval = 10.0  # cập nhật % dung lượng ổ đĩa mỗi 10s

        # Total RAM
        self._total_ram_mb = 0
        try:
            self._total_ram_mb = int(psutil.virtual_memory().total // (1024 * 1024))
        except Exception:
            self._total_ram_mb = self._read_ram_total_from_proc()

        # NVIDIA NVML state
        self._nvml_initialized = False
        self._nvml_handle = None
        if self.enable_gpu:
            self._init_nvml()

        # Khởi tạo giá trị ban đầu cho I/O deltas
        self._init_io_counters()

        # Prime CPU percent reader
        try:
            psutil.cpu_percent(interval=None)
        except Exception:
            pass

    def _init_nvml(self):
        """Khởi tạo NVIDIA Management Library nếu có."""
        try:
            import pynvml
            pynvml.nvmlInit()
            self._nvml_handle = pynvml.nvmlDeviceGetHandleByIndex(0)
            self._nvml_initialized = True
        except Exception:
            self._nvml_initialized = False
            self._nvml_handle = None

    def _init_io_counters(self):
        """Lấy mẫu ban đầu của Disk & Network counters."""
        now = time.monotonic()
        
        # Disk
        self._last_disk_time = now
        disk_counters = self._get_disk_io_raw()
        if disk_counters:
            self._last_disk_read_bytes = disk_counters.read_bytes
            self._last_disk_write_bytes = disk_counters.write_bytes

        # Net
        self._last_net_time = now
        net_counters = self._get_net_io_raw()
        if net_counters:
            self._last_net_sent_bytes = net_counters.bytes_sent
            self._last_net_recv_bytes = net_counters.bytes_recv

    # ─────────────────────────────────────────────────────────────
    # Public: Collect
    # ─────────────────────────────────────────────────────────────

    def collect(self) -> SystemMetrics:
        """Thu thập một snapshot đầy đủ thông số hệ thống Linux."""
        now = time.monotonic()

        # Cập nhật sensor định kỳ (CPU Temp, GPU)
        if now - self._last_sensor_update >= self._sensor_update_interval:
            self._refresh_hardware_sensors()
            self._last_sensor_update = now

        # Cập nhật dung lượng ổ đĩa định kỳ
        if now - self._last_disk_usage_update >= self._disk_usage_interval:
            self._cached_disk_usage = self._read_disk_usage_percent(self.disk_mount)
            self._last_disk_usage_update = now

        # 1. CPU Usage & Freq
        cpu_usage = 0.0
        try:
            cpu_usage = float(psutil.cpu_percent(interval=None))
        except Exception:
            pass

        cpu_freq = self._read_cpu_frequency()
        logical_procs = os.cpu_count() or 1

        # 2. RAM
        ram_used_mb, ram_total_mb, ram_percent = self._read_ram_info()

        # 3. Disk I/O Speeds
        disk_read_kbps, disk_write_kbps = self._calculate_disk_speed(now)

        # 4. Network I/O Speeds
        net_sent_kbps, net_recv_kbps = self._calculate_net_speed(now)

        # 5. Uptime
        uptime_sec = self._read_uptime_seconds()

        return SystemMetrics(
            cpu_usage_percent=cpu_usage,
            cpu_temperature_celsius=self._cached_cpu_temp,
            cpu_frequency_mhz=cpu_freq,
            logical_processors=logical_procs,
            ram_usage_percent=ram_percent,
            ram_used_mb=ram_used_mb,
            ram_total_mb=ram_total_mb or self._total_ram_mb,
            gpu_usage_percent=self._cached_gpu_usage,
            gpu_temperature_celsius=self._cached_gpu_temp,
            gpu_vram_used_mb=self._cached_gpu_vram_mb,
            disk_read_kbps=disk_read_kbps,
            disk_write_kbps=disk_write_kbps,
            disk_usage_percent=self._cached_disk_usage,
            network_sent_kbps=net_sent_kbps,
            network_received_kbps=net_recv_kbps,
            uptime_seconds=uptime_sec,
        )

    # ─────────────────────────────────────────────────────────────
    # CPU Metrics
    # ─────────────────────────────────────────────────────────────

    def _read_cpu_frequency(self) -> int:
        """Đọc xung nhịp CPU hiện tại (MHz)."""
        try:
            freq = psutil.cpu_freq()
            if freq and freq.current > 0:
                return int(freq.current)
        except Exception:
            pass

        # Fallback đọc từ /sys/devices/system/cpu/cpu0/cpufreq/scaling_cur_freq (kHz)
        sys_freq_path = "/sys/devices/system/cpu/cpu0/cpufreq/scaling_cur_freq"
        if os.path.exists(sys_freq_path):
            try:
                with open(sys_freq_path, "r") as f:
                    khz = int(f.read().strip())
                    return khz // 1000
            except Exception:
                pass

        # Fallback đọc từ /proc/cpuinfo
        if os.path.exists("/proc/cpuinfo"):
            try:
                with open("/proc/cpuinfo", "r") as f:
                    for line in f:
                        if "cpu MHz" in line:
                            return int(float(line.split(":")[1].strip()))
            except Exception:
                pass

        return 0

    def _read_cpu_temperature(self) -> Optional[float]:
        """Đọc nhiệt độ CPU qua psutil hoặc sysfs hwmon / thermal zones."""
        # 1. psutil sensors_temperatures
        try:
            temps = psutil.sensors_temperatures()
            if temps:
                # Ưu tiên các tên sensor CPU phổ biến
                priority_names = [
                    "coretemp",
                    "k10temp",
                    "zenpower",
                    "cpu_thermal",
                    "soc_thermal",
                    "acpitz",
                    "cpu-thermal",
                ]
                for name in priority_names:
                    if name in temps and temps[name]:
                        for entry in temps[name]:
                            # Ưu tiên nhãn Package / Tctl / CPU
                            if any(
                                lbl in (entry.label or "").lower()
                                for lbl in ["package", "tctl", "die", "cpu"]
                            ):
                                if entry.current and entry.current > 0:
                                    return float(entry.current)
                        # Lấy entry đầu tiên nếu không có label trùng khớp
                        if temps[name][0].current and temps[name][0].current > 0:
                            return float(temps[name][0].current)

                # Fallback bất kỳ sensor nào có 'cpu' hoặc 'temp'
                for chip_name, entries in temps.items():
                    for entry in entries:
                        if entry.current and 10 <= entry.current <= 120:
                            return float(entry.current)
        except Exception:
            pass

        # 2. Sysfs: /sys/class/thermal/thermal_zone*/temp
        thermal_base = "/sys/class/thermal"
        if os.path.exists(thermal_base):
            try:
                for zone in sorted(os.listdir(thermal_base)):
                    if zone.startswith("thermal_zone"):
                        temp_path = os.path.join(thermal_base, zone, "temp")
                        type_path = os.path.join(thermal_base, zone, "type")
                        if os.path.exists(temp_path):
                            with open(temp_path, "r") as f:
                                val = float(f.read().strip())
                                # Millidegrees -> Celsius
                                if val > 1000:
                                    val /= 1000.0
                                if 15.0 <= val <= 125.0:
                                    return val
            except Exception:
                pass

        # 3. Sysfs: /sys/class/hwmon/hwmon*/temp*_input
        hwmon_base = "/sys/class/hwmon"
        if os.path.exists(hwmon_base):
            try:
                for hw in sorted(os.listdir(hwmon_base)):
                    hw_dir = os.path.join(hwmon_base, hw)
                    for file in os.listdir(hw_dir):
                        if file.startswith("temp") and file.endswith("_input"):
                            with open(os.path.join(hw_dir, file), "r") as f:
                                val = float(f.read().strip())
                                if val > 1000:
                                    val /= 1000.0
                                if 15.0 <= val <= 125.0:
                                    return val
            except Exception:
                pass

        return None

    # ─────────────────────────────────────────────────────────────
    # RAM Metrics
    # ─────────────────────────────────────────────────────────────

    def _read_ram_info(self) -> Tuple[int, int, float]:
        """Trả về (ram_used_mb, ram_total_mb, ram_percent)."""
        try:
            mem = psutil.virtual_memory()
            total_mb = int(mem.total // (1024 * 1024))
            used_mb = int((mem.total - mem.available) // (1024 * 1024))
            percent = float(mem.percent)
            return used_mb, total_mb, percent
        except Exception:
            pass

        # Fallback đọc /proc/meminfo
        try:
            mem_info: Dict[str, int] = {}
            with open("/proc/meminfo", "r") as f:
                for line in f:
                    parts = line.split(":")
                    if len(parts) == 2:
                        key = parts[0].strip()
                        val = int(parts[1].strip().split()[0])  # in kB
                        mem_info[key] = val

            total_kb = mem_info.get("MemTotal", 0)
            avail_kb = mem_info.get("MemAvailable", mem_info.get("MemFree", 0))
            used_kb = max(0, total_kb - avail_kb)

            total_mb = total_kb // 1024
            used_mb = used_kb // 1024
            percent = (used_kb / total_kb * 100.0) if total_kb > 0 else 0.0
            return used_mb, total_mb, percent
        except Exception:
            return 0, 0, 0.0

    def _read_ram_total_from_proc(self) -> int:
        """Đọc tổng RAM (MB) từ /proc/meminfo."""
        try:
            with open("/proc/meminfo", "r") as f:
                for line in f:
                    if line.startswith("MemTotal:"):
                        kb = int(line.split()[1])
                        return kb // 1024
        except Exception:
            pass
        return 0

    # ─────────────────────────────────────────────────────────────
    # GPU Metrics (NVIDIA, AMD, Intel)
    # ─────────────────────────────────────────────────────────────

    def _refresh_hardware_sensors(self):
        """Cập nhật các cảm biến nhiệt độ & GPU định kỳ."""
        self._cached_cpu_temp = self._read_cpu_temperature()

        if not self.enable_gpu:
            self._cached_gpu_usage = None
            self._cached_gpu_temp = None
            self._cached_gpu_vram_mb = None
            return

        # 1. Thử NVIDIA NVML
        if self._nvml_initialized and self._nvml_handle:
            try:
                import pynvml
                util = pynvml.nvmlDeviceGetUtilizationRates(self._nvml_handle)
                temp = pynvml.nvmlDeviceGetTemperature(
                    self._nvml_handle, pynvml.NVML_TEMPERATURE_GPU
                )
                mem = pynvml.nvmlDeviceGetMemoryInfo(self._nvml_handle)

                self._cached_gpu_usage = float(util.gpu)
                self._cached_gpu_temp = float(temp)
                self._cached_gpu_vram_mb = int(mem.used // (1024 * 1024))
                return
            except Exception:
                pass

        # 2. Thử gọi lệnh nvidia-smi
        try:
            res = subprocess.run(
                [
                    "nvidia-smi",
                    "--query-gpu=utilization.gpu,temperature.gpu,memory.used",
                    "--format=csv,noheader,nounits",
                ],
                capture_output=True,
                text=True,
                timeout=0.6,
            )
            if res.returncode == 0 and res.stdout.strip():
                parts = [p.strip() for p in res.stdout.strip().split(",")]
                if len(parts) >= 3:
                    self._cached_gpu_usage = float(parts[0])
                    self._cached_gpu_temp = float(parts[1])
                    self._cached_gpu_vram_mb = int(float(parts[2]))
                    return
        except Exception:
            pass

        # 3. Thử AMD GPU qua sysfs (/sys/class/drm/card*/device/gpu_busy_percent)
        amd_res = self._read_amd_gpu()
        if amd_res is not None:
            usage, temp = amd_res
            self._cached_gpu_usage = usage
            self._cached_gpu_temp = temp
            self._cached_gpu_vram_mb = None
            return

        # Không tìm thấy GPU rời
        self._cached_gpu_usage = None
        self._cached_gpu_temp = None
        self._cached_gpu_vram_mb = None

    def _read_amd_gpu(self) -> Optional[Tuple[float, Optional[float]]]:
        """Đọc thông số AMD GPU từ sysfs DRM."""
        drm_path = "/sys/class/drm"
        if not os.path.exists(drm_path):
            return None

        try:
            for card in sorted(os.listdir(drm_path)):
                if card.startswith("card") and "-" not in card:
                    dev_path = os.path.join(drm_path, card, "device")
                    busy_path = os.path.join(dev_path, "gpu_busy_percent")
                    if os.path.exists(busy_path):
                        with open(busy_path, "r") as f:
                            usage = float(f.read().strip())

                        # Đọc nhiệt độ AMD hwmon
                        temp = None
                        hwmon_dir = os.path.join(dev_path, "hwmon")
                        if os.path.exists(hwmon_dir):
                            for sub in os.listdir(hwmon_dir):
                                t1 = os.path.join(hwmon_dir, sub, "temp1_input")
                                if os.path.exists(t1):
                                    with open(t1, "r") as tf:
                                        t_val = float(tf.read().strip())
                                        temp = t_val / 1000.0 if t_val > 1000 else t_val
                                    break
                        return usage, temp
        except Exception:
            pass
        return None

    # ─────────────────────────────────────────────────────────────
    # Disk I/O & Usage
    # ─────────────────────────────────────────────────────────────

    def _get_disk_io_raw(self):
        """Lấy raw disk io counters."""
        try:
            if self.disk_device and self.disk_device != "All":
                perdisk = psutil.disk_io_counters(perdisk=True)
                if perdisk and self.disk_device in perdisk:
                    return perdisk[self.disk_device]
            return psutil.disk_io_counters(perdisk=False)
        except Exception:
            return None

    def _calculate_disk_speed(self, now: float) -> Tuple[float, float]:
        """Tính tốc độ Đọc / Ghi ổ đĩa (KB/s)."""
        disk_counters = self._get_disk_io_raw()
        if not disk_counters:
            return 0.0, 0.0

        elapsed = now - self._last_disk_time
        if elapsed <= 0:
            elapsed = 0.001

        delta_read = disk_counters.read_bytes - self._last_disk_read_bytes
        delta_write = disk_counters.write_bytes - self._last_disk_write_bytes

        # Cập nhật cho lần sau
        self._last_disk_time = now
        self._last_disk_read_bytes = disk_counters.read_bytes
        self._last_disk_write_bytes = disk_counters.write_bytes

        if delta_read < 0:
            delta_read = 0
        if delta_write < 0:
            delta_write = 0

        read_kbps = (delta_read / 1024.0) / elapsed
        write_kbps = (delta_write / 1024.0) / elapsed

        return read_kbps, write_kbps

    def _read_disk_usage_percent(self, mount_point: str) -> float:
        """Đọc % dung lượng ổ đĩa tại phân vùng mount_point."""
        try:
            target = mount_point if os.path.exists(mount_point) else "/"
            usage = psutil.disk_usage(target)
            return float(usage.percent)
        except Exception:
            return 0.0

    # ─────────────────────────────────────────────────────────────
    # Network I/O
    # ─────────────────────────────────────────────────────────────

    def _get_active_network_adapter(self) -> Optional[str]:
        """Tự động tìm card mạng đang có kết nối internet / dữ liệu thực tế."""
        try:
            stats = psutil.net_if_stats()
            io = psutil.net_io_counters(pernic=True)

            ignored_prefixes = ("lo", "docker", "veth", "virbr", "br-", "tun", "tap", "tailscale")

            # Tìm adapter đang UP và có traffic
            best_nic = None
            max_bytes = -1

            for nic, stat in stats.items():
                if any(nic.startswith(prefix) for prefix in ignored_prefixes):
                    continue
                if stat.isup and nic in io:
                    total_traffic = io[nic].bytes_sent + io[nic].bytes_recv
                    if total_traffic > max_bytes:
                        max_bytes = total_traffic
                        best_nic = nic

            return best_nic
        except Exception:
            return None

    def _get_net_io_raw(self):
        """Lấy raw network counters theo adapter chỉ định hoặc tự động."""
        try:
            pernic = psutil.net_io_counters(pernic=True)
            if not pernic:
                return psutil.net_io_counters(pernic=False)

            target = self.network_adapter
            if not target or target.lower() == "auto":
                active_nic = self._get_active_network_adapter()
                if active_nic and active_nic in pernic:
                    return pernic[active_nic]
                # Nếu không xác định được, lấy adapter đầu tiên không phải lo
                for nic, counters in pernic.items():
                    if not nic.startswith("lo"):
                        return counters
                return psutil.net_io_counters(pernic=False)

            if target in pernic:
                return pernic[target]

            return psutil.net_io_counters(pernic=False)
        except Exception:
            return None

    def _calculate_net_speed(self, now: float) -> Tuple[float, float]:
        """Tính tốc độ Upload (Sent) / Download (Recv) (KB/s)."""
        net_counters = self._get_net_io_raw()
        if not net_counters:
            return 0.0, 0.0

        elapsed = now - self._last_net_time
        if elapsed <= 0:
            elapsed = 0.001

        delta_sent = net_counters.bytes_sent - self._last_net_sent_bytes
        delta_recv = net_counters.bytes_recv - self._last_net_recv_bytes

        # Cập nhật cho lần sau
        self._last_net_time = now
        self._last_net_sent_bytes = net_counters.bytes_sent
        self._last_net_recv_bytes = net_counters.bytes_recv

        if delta_sent < 0:
            delta_sent = 0
        if delta_recv < 0:
            delta_recv = 0

        sent_kbps = (delta_sent / 1024.0) / elapsed
        recv_kbps = (delta_recv / 1024.0) / elapsed

        return sent_kbps, recv_kbps

    # ─────────────────────────────────────────────────────────────
    # System Uptime
    # ─────────────────────────────────────────────────────────────

    def _read_uptime_seconds(self) -> int:
        """Đọc số giây hệ thống hoạt động từ /proc/uptime hoặc psutil.boot_time()."""
        if os.path.exists("/proc/uptime"):
            try:
                with open("/proc/uptime", "r") as f:
                    return int(float(f.read().split()[0]))
            except Exception:
                pass

        try:
            return int(time.time() - psutil.boot_time())
        except Exception:
            return 0

    # ─────────────────────────────────────────────────────────────
    # Discovery Helpers (cho Settings UI & CLI)
    # ─────────────────────────────────────────────────────────────

    @staticmethod
    def get_available_network_adapters() -> List[str]:
        """Lấy danh sách tất cả các card mạng hợp lệ."""
        adapters = ["Auto"]
        try:
            for nic in psutil.net_if_addrs().keys():
                if not nic.startswith("lo"):
                    adapters.append(nic)
        except Exception:
            pass
        return adapters

    @staticmethod
    def get_available_disks() -> List[str]:
        """Lấy danh sách tất cả các ổ đĩa vật lý có thể đo I/O."""
        disks = ["All"]
        try:
            perdisk = psutil.disk_io_counters(perdisk=True)
            if perdisk:
                for d in sorted(perdisk.keys()):
                    disks.append(d)
        except Exception:
            pass
        return disks

    @staticmethod
    def get_available_mounts() -> List[str]:
        """Lấy danh sách các phân vùng mount point (/, /home, etc.)."""
        mounts = []
        try:
            for part in psutil.disk_partitions(all=False):
                if part.mountpoint and part.mountpoint not in mounts:
                    mounts.append(part.mountpoint)
        except Exception:
            pass
        if not mounts or "/" not in mounts:
            mounts.insert(0, "/")
        return mounts
