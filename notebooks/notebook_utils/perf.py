import os
import signal
import subprocess
import tempfile
import time
from datetime import datetime, timedelta, timezone

import pandas as pd


class PerfBandwidthMeasurement:
    """
    Measures memory bandwidth using perf stat.

    Usage:
        perf = start_perf_bandwidth()
        # ... run workload ...
        df = perf.stop()  # Returns DataFrame with timestamp, bandwidth_pct
    """

    def __init__(self):
        self._output_file = tempfile.mktemp(suffix=".csv")
        self._start_time = datetime.now(timezone.utc)
        self._proc = subprocess.Popen(
            [
                "perf", "stat", "-a",
                "-M", "tma_info_system_dram_bw_use",
                "-I", "250",
                "-x", ",",
                "-o", self._output_file,
            ],
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        time.sleep(0.1)  # Let perf initialize

    def stop(self) -> pd.DataFrame:
        """Stop measurement and return DataFrame with timestamp + bandwidth_gbps."""
        self._proc.send_signal(signal.SIGINT)
        self._proc.wait(timeout=5)
        time.sleep(0.2)  # Let perf flush output

        df = self._parse_output()
        os.unlink(self._output_file)
        return df

    def _parse_output(self) -> pd.DataFrame:
        rows = []
        with open(self._output_file) as f:
            for line in f:
                if "tma_info_system_dram_bw_use" in line:
                    parts = line.strip().split(",")
                    relative_secs = float(parts[0])
                    bandwidth_gbps = float(parts[6])
                    absolute_time = self._start_time + timedelta(seconds=relative_secs)
                    rows.append({
                        "timestamp": absolute_time,
                        "bandwidth_gbps": bandwidth_gbps,
                    })
        return pd.DataFrame(rows)


def start_perf_bandwidth() -> PerfBandwidthMeasurement:
    """Start measuring memory bandwidth. Call .stop() on the returned object to get results."""
    return PerfBandwidthMeasurement()
