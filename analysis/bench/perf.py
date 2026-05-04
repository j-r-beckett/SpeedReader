"""
Hardware performance counters via libpfm4 + perf_event_open.

Per-CPU event groups give per-inference granularity with zero multiplexing.
Uncore arb counters give system-wide memory bandwidth.
"""

import ctypes
import ctypes.util
import fcntl
import os
import struct
from pathlib import Path

# ── perf_event_open constants ────────────────────────────────────────────────

_SYS_PERF_EVENT_OPEN = 298

_PERF_FORMAT_TOTAL_TIME_ENABLED = 1 << 0
_PERF_FORMAT_TOTAL_TIME_RUNNING = 1 << 1
_PERF_FORMAT_GROUP = 1 << 3
_READ_FORMAT = _PERF_FORMAT_TOTAL_TIME_ENABLED | _PERF_FORMAT_TOTAL_TIME_RUNNING | _PERF_FORMAT_GROUP

_PERF_EVENT_IOC_ENABLE = 0x2400
_PERF_EVENT_IOC_RESET = 0x2403
_PERF_IOC_FLAG_GROUP = 1

_FLAG_DISABLED = 1 << 0


# ── perf_event_attr (136 bytes, kernel 6.x) ─────────────────────────────────

class _PerfEventAttr(ctypes.Structure):
    _fields_ = [
        ("type", ctypes.c_uint32),
        ("size", ctypes.c_uint32),
        ("config", ctypes.c_uint64),
        ("sample_period", ctypes.c_uint64),
        ("sample_type", ctypes.c_uint64),
        ("read_format", ctypes.c_uint64),
        ("flags", ctypes.c_uint64),
        ("wakeup_events", ctypes.c_uint32),
        ("bp_type", ctypes.c_uint32),
        ("config1", ctypes.c_uint64),
        ("config2", ctypes.c_uint64),
        ("branch_sample_type", ctypes.c_uint64),
        ("sample_regs_user", ctypes.c_uint64),
        ("sample_stack_user", ctypes.c_uint32),
        ("clockid", ctypes.c_int32),
        ("sample_regs_intr", ctypes.c_uint64),
        ("aux_watermark", ctypes.c_uint32),
        ("sample_max_stack", ctypes.c_uint16),
        ("__reserved_2", ctypes.c_uint16),
        ("aux_sample_size", ctypes.c_uint32),
        ("__reserved_3", ctypes.c_uint32),
        ("sig_data", ctypes.c_uint64),
        ("config3", ctypes.c_uint64),
    ]

assert ctypes.sizeof(_PerfEventAttr) == 136


# ── pfm_perf_encode_arg_t (40 bytes on 64-bit) ──────────────────────────────

class _PfmPerfEncodeArg(ctypes.Structure):
    _fields_ = [
        ("attr", ctypes.c_void_p),
        ("fstr", ctypes.c_void_p),
        ("size", ctypes.c_size_t),
        ("idx", ctypes.c_int),
        ("cpu", ctypes.c_int),
        ("flags", ctypes.c_int),
        ("pad0", ctypes.c_int),
    ]

_PFM_PLM3 = 0x08
_PFM_OS_PERF_EVENT_EXT = 2


# ── Event definitions ────────────────────────────────────────────────────────
# Fixed-counter events use the kernel's sysfs configs (pfm4 encodes different
# umasks that have wrong semantics for our use case).  GP events use pfm4.

_FIXED_INST = 0xc0     # instructions (INST_RETIRED.ANY)
_FIXED_CYCLES = 0x3c   # cpu-cycles (CPU_CLK_UNHALTED, fixed counter 1)
_FIXED_REF = 0x13c     # ref-cycles (CPU_CLK_UNHALTED.REF_TSC, fixed counter 2)

_P_GP_EVENTS = [
    b"adl_glc::MEMORY_ACTIVITY:STALLS_L3_MISS",
    b"adl_glc::MEMORY_ACTIVITY:STALLS_L2_MISS",
    b"adl_glc::MEMORY_ACTIVITY:STALLS_L1D_MISS",
    b"adl_glc::EXE_ACTIVITY:BOUND_ON_LOADS",
]

_E_GP_EVENTS = [
    b"adl_grt::LD_HEAD:L1_MISS_AT_RET",
    b"adl_grt::LD_HEAD:L1_BOUND_AT_RET",
    b"adl_grt::MEM_BOUND_STALLS:LOAD",
    b"adl_grt::MEM_BOUND_STALLS:LOAD_DRAM_HIT",
    b"adl_grt::MEM_BOUND_STALLS:LOAD_LLC_HIT",
    b"adl_grt::MEM_BOUND_STALLS:LOAD_L2_HIT",
]

# Indices into the values tuple (3 fixed + N GP)
# P-core: 3 fixed + 4 GP = 7
_P_INST = 0
_P_CLK = 1
_P_REF = 2
_P_L3 = 3
_P_L2 = 4
_P_L1D = 5
_P_LOADS = 6
# E-core: 3 fixed + 6 GP = 9
_E_INST = 0
_E_CLK = 1
_E_REF = 2
_E_L1_MISS = 3
_E_L1_BOUND = 4
_E_LOAD = 5
_E_DRAM = 6
_E_LLC = 7
_E_L2 = 8

# Uncore arb events (same config on both arb units, pfm4 doesn't handle uncore)
_UNCORE_TRK_ALL = 0x181
_UNCORE_COH_ALL = 0x184


# ── Helpers ──────────────────────────────────────────────────────────────────

_libc = ctypes.CDLL("libc.so.6", use_errno=True)


def _read_sysfs(path: str) -> str | None:
    try:
        return Path(path).read_text().strip()
    except (FileNotFoundError, PermissionError):
        return None


def _parse_cpu_range(s: str) -> set[int]:
    cpus: set[int] = set()
    for part in s.split(","):
        if "-" in part:
            lo, hi = part.split("-", 1)
            cpus.update(range(int(lo), int(hi) + 1))
        else:
            cpus.add(int(part))
    return cpus


def _detect_topology() -> tuple[set[int], set[int], int, int]:
    """Returns (p_cpus, e_cpus, p_type, e_type).

    On non-hybrid systems, e_cpus is empty and e_type is 0.
    """
    p_str = _read_sysfs("/sys/devices/cpu_core/cpus")
    e_str = _read_sysfs("/sys/devices/cpu_atom/cpus")
    p_type = _read_sysfs("/sys/devices/cpu_core/type")
    e_type = _read_sysfs("/sys/devices/cpu_atom/type")
    if p_str and e_str and p_type and e_type:
        return _parse_cpu_range(p_str), _parse_cpu_range(e_str), int(p_type), int(e_type)
    # Non-hybrid: all cores are P-cores
    n = os.cpu_count() or 1
    return set(range(n)), set(), int(p_type) if p_type else 4, 0


def _detect_uncore_arb() -> list[int]:
    """Return list of uncore arb PMU types, e.g. [26, 27]."""
    types = []
    for i in range(4):
        t = _read_sysfs(f"/sys/devices/uncore_arb_{i}/type")
        if t is not None:
            types.append(int(t))
    return types


# ── libpfm4 ─────────────────────────────────────────────────────────────────

_pfm = ctypes.CDLL("libpfm.so")
_pfm.pfm_initialize.restype = ctypes.c_int
_pfm.pfm_get_os_event_encoding.restype = ctypes.c_int
_pfm.pfm_get_os_event_encoding.argtypes = [
    ctypes.c_char_p, ctypes.c_int, ctypes.c_int, ctypes.c_void_p,
]
_pfm.pfm_terminate.restype = None

_pfm_initialized = False


def _pfm_init():
    global _pfm_initialized
    if _pfm_initialized:
        return
    rc = _pfm.pfm_initialize()
    if rc != 0:
        raise RuntimeError(f"pfm_initialize failed: {rc}")
    _pfm_initialized = True


def _pfm_resolve(event_name: bytes) -> tuple[int, int]:
    """Resolve event name → (type, config) via libpfm4."""
    _pfm_init()
    attr = _PerfEventAttr()
    attr.size = ctypes.sizeof(_PerfEventAttr)
    arg = _PfmPerfEncodeArg()
    arg.attr = ctypes.addressof(attr)
    arg.fstr = 0
    arg.size = ctypes.sizeof(_PfmPerfEncodeArg)
    rc = _pfm.pfm_get_os_event_encoding(event_name, _PFM_PLM3, _PFM_OS_PERF_EVENT_EXT, ctypes.byref(arg))
    if rc != 0:
        raise RuntimeError(f"pfm_get_os_event_encoding({event_name!r}) failed: {rc}")
    return attr.type, attr.config


# ── perf_event_open ──────────────────────────────────────────────────────────

def _perf_event_open(pmu_type: int, config: int, cpu: int, group_fd: int, disabled: bool) -> int:
    attr = _PerfEventAttr()
    attr.size = ctypes.sizeof(_PerfEventAttr)
    attr.type = pmu_type
    attr.config = config
    attr.read_format = _READ_FORMAT
    if disabled:
        attr.flags = _FLAG_DISABLED
    fd = _libc.syscall(_SYS_PERF_EVENT_OPEN, ctypes.byref(attr), -1, cpu, group_fd, 0)
    if fd < 0:
        e = ctypes.get_errno()
        raise OSError(e, os.strerror(e))
    return fd


def _open_group(events: list[tuple[int, int]], cpu: int) -> tuple[int, list[int]]:
    """Open a perf event group on a CPU. Returns (leader_fd, all_fds)."""
    fds: list[int] = []
    leader_fd = -1
    for i, (pmu_type, config) in enumerate(events):
        fd = _perf_event_open(pmu_type, config, cpu, leader_fd, disabled=(i == 0))
        if i == 0:
            leader_fd = fd
        fds.append(fd)
    return leader_fd, fds


def _enable_group(leader_fd: int) -> None:
    fcntl.ioctl(leader_fd, _PERF_EVENT_IOC_RESET, _PERF_IOC_FLAG_GROUP)
    fcntl.ioctl(leader_fd, _PERF_EVENT_IOC_ENABLE, _PERF_IOC_FLAG_GROUP)


def _read_group(leader_fd: int, n: int) -> tuple[int, ...]:
    """Read a group. Returns raw counter values. Asserts no multiplexing."""
    buf = os.read(leader_fd, 8 * (3 + n))
    vals = struct.unpack(f"={3 + n}Q", buf)
    nr, time_enabled, time_running = vals[0], vals[1], vals[2]
    assert nr == n, f"expected {n} events, got {nr}"
    assert time_enabled == time_running, (
        f"multiplexing detected: enabled={time_enabled} running={time_running}"
    )
    return vals[3:]


def _close_fds(fds: list[int]) -> None:
    for fd in fds:
        try:
            os.close(fd)
        except OSError:
            pass


# ── PerfCounters ─────────────────────────────────────────────────────────────

class PerfCounters:
    """Per-CPU hardware performance counters via perf_event_open.

    Usage::

        perf = PerfCounters(cpus=[0, 2, 4])
        before = perf.read_cpu(0)
        # ... workload on cpu 0 ...
        after = perf.read_cpu(0)
        metrics = perf.compute_cpu_metrics(0, before, after)
        perf.close()
    """

    def __init__(self):
        p_cpus, e_cpus, p_type, e_type = _detect_topology()
        self._is_hybrid = bool(e_cpus)

        # Fixed-counter events use sysfs configs; GP events resolved via pfm4
        p_fixed = [(p_type, _FIXED_INST), (p_type, _FIXED_CYCLES), (p_type, _FIXED_REF)]
        p_resolved = p_fixed + [_pfm_resolve(name) for name in _P_GP_EVENTS]

        if self._is_hybrid:
            e_fixed = [(e_type, _FIXED_INST), (e_type, _FIXED_CYCLES), (e_type, _FIXED_REF)]
            e_resolved = e_fixed + [_pfm_resolve(name) for name in _E_GP_EVENTS]
        else:
            e_resolved = []

        # Per-CPU: {cpu: ("P"|"E", leader_fd, all_fds, n_events)}
        self._cpu_state: dict[int, tuple[str, int, list[int], int]] = {}

        for cpu in sorted(p_cpus | e_cpus):
            if cpu in e_cpus:
                events = e_resolved
                core_type = "E"
            else:
                events = p_resolved
                core_type = "P"
            leader_fd, fds = _open_group(events, cpu)
            self._cpu_state[cpu] = (core_type, leader_fd, fds, len(events))
            _enable_group(leader_fd)

        # Uncore arb counters for bandwidth
        arb_types = _detect_uncore_arb()
        self._uncore_fds: list[int] = []
        self._uncore_leaders: list[tuple[int, int]] = []  # (leader_fd, n_events)
        try:
            for arb_type in arb_types:
                configs = [_UNCORE_TRK_ALL, _UNCORE_COH_ALL]
                events = [(arb_type, c) for c in configs]
                leader_fd, fds = _open_group(events, 0)
                self._uncore_fds.extend(fds)
                self._uncore_leaders.append((leader_fd, len(configs)))
                _enable_group(leader_fd)
        except OSError:
            # Uncore requires CAP_PERFMON or root; degrade gracefully
            _close_fds(self._uncore_fds)
            self._uncore_fds = []
            self._uncore_leaders = []

    def read_cpu(self, cpu: int) -> tuple[int, ...]:
        """Read raw counter values for a CPU. Lightweight — call from worker threads."""
        _, leader_fd, _, n = self._cpu_state[cpu]
        return _read_group(leader_fd, n)

    def read_uncore(self) -> tuple[int, ...]:
        """Read all uncore arb counters. Call from main thread."""
        if not self._uncore_leaders:
            return ()
        vals: list[int] = []
        for leader_fd, n in self._uncore_leaders:
            vals.extend(_read_group(leader_fd, n))
        return tuple(vals)

    def compute_cpu_metrics(self, cpu: int, before: tuple[int, ...], after: tuple[int, ...]) -> dict:
        """Compute metrics from a before/after snapshot pair."""
        core_type = self._cpu_state[cpu][0]
        delta = tuple(a - b for a, b in zip(after, before))
        if core_type == "P":
            return self._compute_p(delta)
        return self._compute_e(delta)

    def compute_bandwidth(self, before: tuple[int, ...], after: tuple[int, ...], elapsed_s: float) -> float:
        """Compute memory bandwidth in GB/s from uncore snapshots."""
        if not before or not after:
            return float("nan")
        total = sum(a - b for a, b in zip(after, before))
        return 64 * total / elapsed_s / 1e9

    @property
    def is_hybrid(self) -> bool:
        return self._is_hybrid

    def core_type(self, cpu: int) -> str:
        return self._cpu_state[cpu][0]

    def close(self) -> None:
        for _, _, fds, _ in self._cpu_state.values():
            _close_fds(fds)
        self._cpu_state.clear()
        _close_fds(self._uncore_fds)
        self._uncore_fds.clear()
        self._uncore_leaders.clear()

    def __del__(self):
        self.close()

    @staticmethod
    def _compute_p(d: tuple[int, ...]) -> dict:
        inst, clk, ref, l3, l2, l1d, loads = d
        if clk == 0:
            return {"ipc": 0.0, "freq_ratio": 0.0, "dram_bound_pct": 0.0,
                    "l3_bound_pct": 0.0, "l2_bound_pct": 0.0, "l1_bound_pct": 0.0}
        return {
            "ipc": inst / clk,
            "freq_ratio": clk / ref if ref else 0.0,
            "dram_bound_pct": 100 * l3 / clk,
            "l3_bound_pct": 100 * (l2 - l3) / clk,
            "l2_bound_pct": 100 * (l1d - l2) / clk,
            "l1_bound_pct": 100 * max(loads - l1d, 0) / clk,
        }

    @staticmethod
    def _compute_e(d: tuple[int, ...]) -> dict:
        inst, clk, ref, l1_miss, l1_bound, load, dram, llc, l2 = d
        if clk == 0:
            return {"ipc": 0.0, "freq_ratio": 0.0, "dram_bound_pct": 0.0,
                    "l3_bound_pct": 0.0, "l2_bound_pct": 0.0, "l1_bound_pct": 0.0}

        def correction(hit: int) -> float:
            if load == 0:
                return 0.0
            return max((load - l1_miss) / clk, 0) * hit / load

        return {
            "ipc": inst / clk,
            "freq_ratio": clk / ref if ref else 0.0,
            "dram_bound_pct": 100 * (dram / clk - correction(dram)),
            "l3_bound_pct": 100 * (llc / clk - correction(llc)),
            "l2_bound_pct": 100 * (l2 / clk - correction(l2)),
            "l1_bound_pct": 100 * l1_bound / clk,
        }
