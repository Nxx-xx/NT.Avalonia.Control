"""External, identity-bound process-memory sampling for Charts.MemoryProbe.

Observed maxima are samples, not instantaneous peaks or exclusive native bytes.
This program never terminates a process and never releases the host itself.
"""
from __future__ import annotations

import argparse
import ctypes
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
import platform
import re
import sys
import time

SCHEMA = "charts-memory-sampling/v1"
IDENTITY_FIELDS = ("runToken", "processId", "processStartedUtc", "mode", "windowsCreationFileTimeUtc", "linuxProcStartTicks", "linuxBootId")
METRIC_DEFINITIONS = {
    "privateCommitBytes": "Windows PROCESS_MEMORY_COUNTERS_EX.PrivateUsage: current process private commit charge, not private resident bytes.",
    "workingSetBytes": "Windows current WorkingSetSize, including shared resident pages.",
    "osLifetimePeakCommitChargeBytes": "Windows PeakPagefileUsage: OS process-lifetime peak commit charge, not paging-file occupancy; independent host lifetime only.",
    "osLifetimePeakWorkingSetBytes": "Windows PeakWorkingSetSize: OS process-lifetime high-water working set; independent host lifetime only.",
    "rssBytes": "Linux status VmRSS or smaps_rollup Rss, kept in separate process/smaps streams; status kernel accounting is approximate.",
    "osLifetimePeakRssBytes": "Linux status VmHWM: OS lifetime high-water RSS accounting, approximate and not an exact physical peak.",
    "pssBytes": "Linux smaps_rollup Pss sampled at 100ms; proportional resident accounting.",
    "privateResidentBytes": "Linux sampled Private_Clean+Private_Dirty+Private_Hugetlb; components remain separately recorded.",
    "swapBytes": "Linux smaps_rollup Swap sampled at 100ms; separate from status VmSwap and optional SwapPss.",
    "references": ["https://learn.microsoft.com/en-us/windows/desktop/api/psapi/ns-psapi-process_memory_counters_ex", "https://docs.kernel.org/filesystems/proc.html"],
}


def require(value, message):
    if not value:
        raise ValueError(message)


def load(path):
    def pairs(items):
        result = {}
        for key, value in items:
            require(key not in result, "Duplicate JSON key: " + key)
            result[key] = value
        return result
    return json.loads(Path(path).read_text(encoding="utf-8-sig"), object_pairs_hook=pairs,
                      parse_constant=lambda value: (_ for _ in ()).throw(ValueError("Nonfinite JSON: " + value)))


def write_new(path, value):
    path = Path(path)
    require(not path.exists(), "Fresh evidence file required: " + str(path))
    temporary = path.with_name(path.name + ".tmp")
    with temporary.open("x", encoding="utf-8", newline="\n") as stream:
        json.dump(value, stream, allow_nan=False, ensure_ascii=False, indent=2)
        stream.write("\n"); stream.flush(); os.fsync(stream.fileno())
    require(not path.exists(), "Evidence appeared during atomic write: " + str(path))
    os.rename(temporary, path)


def reference(path):
    raw = Path(path).read_bytes()
    return {"path": str(Path(path).resolve()), "bytes": len(raw), "sha256": hashlib.sha256(raw).hexdigest().upper()}


def utc_filetime(value):
    """Parse the complete .NET UTC O timestamp without losing its seventh digit."""
    match = re.fullmatch(r"(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})\.(\d{7})(?:Z|\+00:00)", value)
    require(match is not None, "Complete UTC O timestamp with seven fractional digits required")
    year, month, day, hour, minute, second = map(int, match.groups()[:6])
    require(hour < 24 and minute < 60 and second < 60, "Invalid UTC time")
    days = dt.date(year, month, day).toordinal() - dt.date(1601, 1, 1).toordinal()
    return ((days * 24 + hour) * 3600 + minute * 60 + second) * 10_000_000 + int(match[7])


def identity(envelope):
    require(envelope.get("schema") == "charts-memory-probe/v1", "Unexpected host envelope")
    result = {key: envelope[key] for key in IDENTITY_FIELDS}
    require(re.fullmatch(r"[a-fA-F0-9]{32}", result["runToken"]) is not None, "Invalid nonce token")
    require(type(result["processId"]) is int and result["processId"] > 0, "Invalid PID")
    require(result["mode"] in ("empty", "full"), "Invalid mode")
    utc_filetime(result["processStartedUtc"])
    return result


def same_identity(expected, envelope):
    require(identity(envelope) == expected, "Host identity changed")


def parse_proc_stat(text):
    end = text.rfind(")")
    require(end > text.find("("), "Malformed /proc stat command")
    fields = text[end + 2:].split()
    require(len(fields) >= 20 and fields[19].isdigit(), "Malformed /proc stat starttime")
    return int(fields[19])


def parse_kib(text, required):
    result = {}
    for line in text.splitlines():
        if ":" not in line:
            continue
        name, value = line.split(":", 1)
        if name in result:
            raise ValueError("Duplicate proc memory field " + name)
        match = re.fullmatch(r"\s*(\d+)\s+kB\s*", value)
        if match:
            result[name] = int(match[1]) * 1024
    require(all(name in result for name in required), "Missing or non-kB proc fields: " + ",".join(name for name in required if name not in result))
    return result


class Clock:
    def __init__(self):
        self.qpc = None; self.frequency = None
        if os.name == "nt":
            kernel = ctypes.WinDLL("kernel32", use_last_error=True)
            self.qpc = kernel.QueryPerformanceCounter
            self.qpc.argtypes = [ctypes.POINTER(ctypes.c_longlong)]; self.qpc.restype = ctypes.c_int
            query = kernel.QueryPerformanceFrequency
            query.argtypes = [ctypes.POINTER(ctypes.c_longlong)]; query.restype = ctypes.c_int
            value = ctypes.c_longlong()
            require(query(ctypes.byref(value)) and value.value > 0, "QueryPerformanceFrequency failed")
            self.frequency = value.value
        self.metadata = {"monotonicNs": True, "perfCounterImplementation": time.get_clock_info("perf_counter").implementation,
                         "qpcFrequency": self.frequency, "qpcSource": "kernel32 QueryPerformanceCounter/QueryPerformanceFrequency" if self.qpc else None}

    def read(self):
        ns = time.perf_counter_ns()
        ticks = None
        if self.qpc:
            value = ctypes.c_longlong()
            require(self.qpc(ctypes.byref(value)), "QueryPerformanceCounter failed")
            ticks = value.value
        return ns, ticks


class LinuxReader:
    def __init__(self, expected, proc=Path("/proc")):
        self.expected, self.proc = expected, Path(proc)
        require(expected["windowsCreationFileTimeUtc"] is None, "Unexpected Windows identity on Linux")
        self.verify()

    def verify(self):
        observed = {"linuxProcStartTicks": parse_proc_stat((self.proc / str(self.expected["processId"]) / "stat").read_text()),
                    "linuxBootId": (self.proc / "sys/kernel/random/boot_id").read_text().strip(), "windowsCreationFileTimeUtc": None}
        require(type(self.expected["linuxProcStartTicks"]) is int and observed["linuxProcStartTicks"] == self.expected["linuxProcStartTicks"] and observed["linuxBootId"] == self.expected["linuxBootId"], "PID reused, different boot or Linux process start changed")
        return observed

    def read(self, kind):
        observed = self.verify()
        root = self.proc / str(self.expected["processId"])
        if kind == "process":
            fields = parse_kib((root / "status").read_text(), ("VmRSS", "VmHWM", "VmSwap"))
            metrics = {"rssBytes": fields["VmRSS"], "osLifetimePeakRssBytes": fields["VmHWM"], "statusSwapBytes": fields["VmSwap"]}
        else:
            fields = parse_kib((root / "smaps_rollup").read_text(), ("Rss", "Pss", "Private_Clean", "Private_Dirty", "Swap"))
            metrics = {"rssBytes": fields["Rss"], "pssBytes": fields["Pss"], "privateCleanBytes": fields["Private_Clean"],
                       "privateDirtyBytes": fields["Private_Dirty"], "privateHugetlbBytes": fields.get("Private_Hugetlb", 0),
                       "privateResidentBytes": fields["Private_Clean"] + fields["Private_Dirty"] + fields.get("Private_Hugetlb", 0),
                       "swapBytes": fields["Swap"], "swapPssBytes": fields.get("SwapPss")}
        require(self.verify() == observed, "Process identity changed during memory read")
        return observed, metrics

    def close(self):
        pass


class WindowsReader:
    def __init__(self, expected):
        self.expected = expected
        self.kernel = ctypes.WinDLL("kernel32", use_last_error=True)
        self.psapi = ctypes.WinDLL("psapi", use_last_error=True)
        self.kernel.OpenProcess.argtypes = [ctypes.c_ulong, ctypes.c_int, ctypes.c_ulong]; self.kernel.OpenProcess.restype = ctypes.c_void_p
        self.kernel.CloseHandle.argtypes = [ctypes.c_void_p]; self.kernel.CloseHandle.restype = ctypes.c_int
        class FileTime(ctypes.Structure):
            _fields_ = [("low", ctypes.c_uint32), ("high", ctypes.c_uint32)]
        self.FileTime = FileTime
        self.kernel.GetProcessTimes.argtypes = [ctypes.c_void_p] + [ctypes.POINTER(FileTime)] * 4; self.kernel.GetProcessTimes.restype = ctypes.c_int
        self.kernel.GetProcessId.argtypes = [ctypes.c_void_p]; self.kernel.GetProcessId.restype = ctypes.c_uint32
        self.kernel.GetExitCodeProcess.argtypes = [ctypes.c_void_p, ctypes.POINTER(ctypes.c_uint32)]; self.kernel.GetExitCodeProcess.restype = ctypes.c_int
        class Counters(ctypes.Structure):
            _fields_ = [("cb", ctypes.c_uint32), ("pageFaultCount", ctypes.c_uint32)] + [(key, ctypes.c_size_t) for key in
                ("peakWorkingSetSize", "workingSetSize", "quotaPeakPagedPoolUsage", "quotaPagedPoolUsage", "quotaPeakNonPagedPoolUsage", "quotaNonPagedPoolUsage", "pagefileUsage", "peakPagefileUsage", "privateUsage")]
        self.Counters = Counters
        self.psapi.GetProcessMemoryInfo.argtypes = [ctypes.c_void_p, ctypes.POINTER(Counters), ctypes.c_uint32]; self.psapi.GetProcessMemoryInfo.restype = ctypes.c_int
        self.handle = self.kernel.OpenProcess(0x0400 | 0x0010, False, expected["processId"])
        if not self.handle:
            raise ctypes.WinError(ctypes.get_last_error())
        try:
            self.verify()
        except Exception:
            self.close(); raise

    def verify(self):
        exit_code = ctypes.c_uint32()
        if not self.kernel.GetExitCodeProcess(self.handle, ctypes.byref(exit_code)):
            raise ctypes.WinError(ctypes.get_last_error())
        require(self.kernel.GetProcessId(self.handle) == self.expected["processId"] and exit_code.value == 259, "Windows bound process is not the expected live held process")
        times = [self.FileTime() for _ in range(4)]
        if not self.kernel.GetProcessTimes(self.handle, *(ctypes.byref(item) for item in times)):
            raise ctypes.WinError(ctypes.get_last_error())
        creation = (times[0].high << 32) | times[0].low
        require(str(creation) == self.expected["windowsCreationFileTimeUtc"] and creation == utc_filetime(self.expected["processStartedUtc"]), "Windows complete creation FILETIME mismatch / PID reuse")
        require(self.expected["linuxProcStartTicks"] is None and self.expected["linuxBootId"] is None, "Unexpected Linux identity on Windows")
        return {"windowsCreationFileTimeUtc": str(creation), "linuxProcStartTicks": None, "linuxBootId": None}

    def read(self, kind):
        require(kind == "process", "smaps is unavailable on Windows")
        observed = self.verify(); value = self.Counters(); value.cb = ctypes.sizeof(value)
        if not self.psapi.GetProcessMemoryInfo(self.handle, ctypes.byref(value), value.cb):
            raise ctypes.WinError(ctypes.get_last_error())
        require(self.verify() == observed, "Process creation changed during read")
        return observed, {"privateCommitBytes": value.privateUsage, "workingSetBytes": value.workingSetSize,
                          "osLifetimePeakCommitChargeBytes": value.peakPagefileUsage, "osLifetimePeakWorkingSetBytes": value.peakWorkingSetSize}

    def close(self):
        if getattr(self, "handle", None):
            self.kernel.CloseHandle(self.handle); self.handle = None


def sampled_row(expected, kind, index, reason, scheduled, previous_start, clock, reader):
    start, qpc_start = clock.read(); metrics = None; observed = None; errors = []
    try:
        observed, metrics = reader.read(kind)
    except Exception as error:
        errors.append({"type": type(error).__name__, "message": str(error)})
    end, qpc_end = clock.read()
    return {**expected, "kind": kind, "index": index, "reason": reason, "scheduledNs": scheduled,
            "readStartNs": start, "readEndNs": end, "gapNs": None if previous_start is None else start - previous_start,
            "deadlineLatenessNs": start - scheduled, "readStartQpc": qpc_start, "readEndQpc": qpc_end,
            "observedIdentity": observed, "metrics": metrics, "errors": errors}


def observed_maxima(rows):
    result = {}
    for row in rows:
        if row["errors"] or row["metrics"] is None:
            continue
        for name, value in row["metrics"].items():
            if value is not None:
                require(type(value) is int and value >= 0, "Noninteger or negative memory read")
                result[name] = max(result.get(name, value), value)
    return result


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--ready-file", type=Path, required=True); parser.add_argument("--control-dir", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True); parser.add_argument("--interval-ms", type=int, default=20)
    parser.add_argument("--smaps-interval-ms", type=int, default=100); parser.add_argument("--timeout-seconds", type=int, default=90)
    args = parser.parse_args(argv)
    require(args.interval_ms == 20 and args.smaps_interval_ms == 100 and args.timeout_seconds == 90, "Fixed diagnostic intervals/timeout required")
    require(not args.output.exists(), "Fresh sampler directory required")
    args.output.mkdir(parents=True); require(args.control_dir.is_dir(), "Existing fresh control directory required")
    clock = Clock(); deadline = time.perf_counter_ns() + args.timeout_seconds * 1_000_000_000
    expected = None; reader = None; rows = {"process": [], "smaps": []}; result = {"schema": SCHEMA, "passed": False, "error": None,
        "intervalMs": 20, "smapsIntervalMs": 100, "clock": clock.metadata, "platform": platform.system().lower(),
        "qualification": "Sampled current process totals/maxima plus separately named OS lifetime counters. Linux RSS/HWM are approximate kernel accounting. Not exclusive native/GPU bytes, exact physical peak, original C7 or present qualification.",
        "nativeBytes": None, "gpuBytes": None, "originalC7PeakMemory": None, "metricDefinitions": METRIC_DEFINITIONS}
    try:
        while not args.ready_file.exists():
            require(time.perf_counter_ns() < deadline, "Timeout waiting for host ready")
            time.sleep(.02)
        ready = load(args.ready_file); expected = identity(ready); result["identity"] = expected
        require(ready["payload"]["readyQpc"] > 0, "Host ready clock missing")
        if os.name == "nt":
            require(ready["stopwatchFrequency"] == clock.frequency, "Host and native QPC frequencies differ")
            reader = WindowsReader(expected)
        else:
            require(sys.platform.startswith("linux") and ready["stopwatchFrequency"] == 1_000_000_000 and "CLOCK_MONOTONIC" in clock.metadata["perfCounterImplementation"], "Linux shared monotonic clock required")
            reader = LinuxReader(expected)
        result["readyReference"] = reference(args.ready_file)
        write_new(args.output / "started.json", {**result, "samplerProcessId": os.getpid(), "observedIdentity": reader.verify(), "startedNs": time.perf_counter_ns()})
        kinds = ["process", "smaps"] if sys.platform.startswith("linux") else ["process"]
        with (args.output / "samples.jsonl").open("x", encoding="utf-8") as process_stream, (args.output / "smaps.jsonl").open("x", encoding="utf-8") as smaps_stream:
            streams = {"process": process_stream, "smaps": smaps_stream}
            def sample(kind, reason, scheduled):
                previous = rows[kind][-1]["readStartNs"] if rows[kind] else None
                row = sampled_row(expected, kind, len(rows[kind]), reason, scheduled, previous, clock, reader)
                rows[kind].append(row); streams[kind].write(json.dumps(row, allow_nan=False) + "\n"); streams[kind].flush()
                require(not row["errors"], "Memory sample failed: " + json.dumps(row["errors"]))
                return row
            first = sample("process", "before-source-generation", time.perf_counter_ns())
            if "smaps" in kinds:
                sample("smaps", "before-source-generation", time.perf_counter_ns())
            write_new(args.control_dir / "sampler-ready.json", {**expected, "status": "ready", "firstProcessSampleIndex": 0,
                "preworkReadEndNs": max(rows[kind][-1]["readEndNs"] for kind in kinds),
                "preworkReadEndQpc": first["readEndQpc"], "clock": clock.metadata})
            next_due = {kind: rows[kind][0]["readStartNs"] + (20 if kind == "process" else 100) * 1_000_000 for kind in kinds}
            work_file = args.ready_file.parent / "work-complete.json"
            while not work_file.exists():
                now = time.perf_counter_ns(); require(now < deadline, "Timeout waiting for held work-complete")
                for kind in kinds:
                    if now >= next_due[kind]:
                        sample(kind, "periodic", next_due[kind])
                        interval = (20 if kind == "process" else 100) * 1_000_000
                        next_due[kind] += max(1, (time.perf_counter_ns() - next_due[kind]) // interval + 1) * interval
                wait_ns = max(0, min(next_due.values()) - time.perf_counter_ns())
                if wait_ns:
                    time.sleep(min(wait_ns, 20_000_000) / 1_000_000_000)
            complete = load(work_file); same_identity(expected, complete)
            require(complete["payload"]["status"] == "completed", "Host work failed")
            for kind in kinds:
                sample(kind, "terminal-hold", time.perf_counter_ns())
            for stream in streams.values():
                stream.flush(); os.fsync(stream.fileno())
            result["workCompleteReference"] = reference(work_file)
        result["sampleCounts"] = {kind: len(values) for kind, values in rows.items()}
        result["maxObserved"] = {kind: observed_maxima(values) for kind, values in rows.items()}
        result["passed"] = True
        write_new(args.output / "result.json", result)
        write_new(args.control_dir / "sampler-complete.json", {**expected, "status": "completed", "resultSha256": reference(args.output / "result.json")["sha256"],
            "finalProcessSampleIndex": len(rows["process"]) - 1, "finalSmapsSampleIndex": len(rows["smaps"]) - 1 if rows["smaps"] else None})
        return 0
    except Exception as error:
        result["passed"] = False; result["error"] = {"type": type(error).__name__, "message": str(error)}
        result["identity"] = expected; result["sampleCounts"] = {kind: len(values) for kind, values in rows.items()}
        result["maxObserved"] = {kind: observed_maxima(values) for kind, values in rows.items()}
        if not (args.output / "result.json").exists():
            write_new(args.output / "result.json", result)
        print(str(error), file=sys.stderr); return 1
    finally:
        if reader:
            reader.close()


if __name__ == "__main__":
    raise SystemExit(main())
