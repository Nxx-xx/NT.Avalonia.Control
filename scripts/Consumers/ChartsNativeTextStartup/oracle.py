"""Independent package-consumer startup/glyph evidence. Never starts .NET."""
import argparse
import hashlib
import json
from pathlib import Path
import runpy
import sys

sys.dont_write_bytecode = True


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest().upper()


def inspect(directory, scripts, expected_platform, expected_pid):
    sys.path.insert(0, str(scripts))
    decoder = runpy.run_path(str(scripts / "charts_png_oracle.py"))["decode_png"]
    glyph = runpy.run_path(str(scripts / "charts_platform_acceptance_oracle.py"))["glyph_orientation_evidence"]
    raw = json.loads((directory / "raw.json").read_text(encoding="utf-8-sig"))
    checks = {}
    def check(name, value):
        checks[name] = value is True
    check("schema", raw.get("schema") == "charts-native-text-package-consumer/v1")
    check("actual-terminal-report", raw.get("passed") is True and raw.get("status") == "completed" and raw.get("error") is None)
    check("runtime-PID-and-platform-bound", raw.get("processId") == expected_pid and expected_pid > 0 and raw.get("operatingSystem") == expected_platform and (("windows" in str(raw.get("osDescription", "")).lower()) == (expected_platform == "windows")))
    check("output-directory-bound", Path(raw["options"]["output"]).resolve() == directory.resolve())
    check("nonempty-producer-checks", bool(raw.get("checks")) and all(value is True for value in raw["checks"].values()))
    check("no-ambient-preload", raw["environment"].get("ldPreload") in (None, ""))
    system = raw["options"].get("systemLibrary")
    if system is not None:
        control = raw.get("systemPreload") or {}
        check("controlled-system-global-preload", expected_platform == "linux" and Path(system).is_absolute() and control.get("path") == str(Path(system).resolve()) and type(control.get("flags")) is int and control["flags"] == 258 and control.get("purpose") == "system-global-control" and bool(control.get("handle")) and control.get("actualVersion") not in (None, "8.3.1") and control.get("symbol") == "hb_version_string" and bool(control.get("symbolAddress")) and control.get("symbolProviderPath") == control.get("path") and control.get("symbolProviderSha256") == control.get("sha256") and any(row["path"] == control.get("symbolProviderPath") and row["sha256"] == control.get("sha256") for row in raw["nativeModules"]))
    mode = raw["options"]["mode"]
    fault = mode in ("missing", "late", "native-loaded", "resolver-conflict")
    result = None
    if fault:
        expected = "MissingNativeAsset" if mode == "missing" else "ResolverConflict" if mode == "resolver-conflict" else "TooLate"
        state = raw["currentBinding"]
        check("expected-failure-policy", state["status"] == "Failed" and state["errorCode"] == expected)
        check("policy-is-not-window-startup", raw.get("nativeWindow") is None and raw.get("artifacts") == [])
    else:
        window = raw["nativeWindow"]
        check("actual-visible-window", window["visible"] is True and type(window["handle"]) is int and window["handle"] > 0 and window["descriptor"] in ("HWND", "XID"))
        check("actual-backend-bound-to-parent-platform", window["descriptor"] == ("HWND" if expected_platform == "windows" else "XID") and str(window["backendType"]).startswith("Avalonia.Win32." if expected_platform == "windows" else "Avalonia.X11."))
        check("exact-glyph-artifact-set", len(raw["artifacts"]) == 3 and {row["path"] for row in raw["artifacts"]} == {"glyph-baseline.png", "glyph-hidden.png", "glyph-restored.png"})
        frames = {}
        for row in raw["artifacts"]:
            path = (directory / row["path"]).resolve()
            if not path.is_relative_to(directory.resolve()):
                raise ValueError("Consumer image escapes its evidence directory")
            frame = decoder(path)
            check("image-" + row["path"], digest(path) == row["sha256"] and path.stat().st_size == row["bytes"] and frame[:2] == (row["pixelWidth"], row["pixelHeight"]))
            frames[row["path"]] = frame
        regions = json.loads((directory / "glyph-regions.json").read_text(encoding="utf-8-sig"))
        region = lambda key: dict(zip(("x", "y", "width", "height"), regions[key]))
        result = glyph(frames["glyph-baseline.png"], frames["glyph-hidden.png"], frames["glyph-restored.png"], region("chartGlyph"), region("referenceGlyph"))
        check("actual-chart-and-native-reference-F", result["passed"] is True)
        if mode != "baseline":
            state = raw["afterWindow"]
            linux = expected_platform == "linux"
            check("production-API-integration", state["status"] == ("Initialized" if linux else "NotApplicable"))
            if linux:
                check("actual-private-provider-and-resolver", state["deepBindingEnabled"] is True and state["resolverRegistered"] is True and state["resolverObserved"] is True and state["resolverInvocationCount"] > 0 and state["resolvedLibraryName"] == "libHarfBuzzSharp" and state["assetPath"] == state["symbolProviderPath"] and state["nativeVersion"] == "8.3.1")
                check("loaded-private-native-module", any(row["path"] == state["assetPath"] and row["sha256"] == state["assetSha256"] for row in raw["nativeModules"]))
    check("no-host-workaround-flag", raw["qualification"]["hostIsolationFlagUsed"] is False)
    return {"schema": "charts-native-text-package-consumer-oracle/v1", "passed": all(checks.values()), "checks": checks,
        "failedChecks": [name for name, value in checks.items() if not value], "mode": mode, "glyph": result,
        "rawSha256": digest(directory / "raw.json"), "oracleSha256": digest(Path(__file__)),
        "pixelDecoderSha256": digest(scripts / "charts_png_oracle.py"), "glyphValidatorSha256": digest(scripts / "charts_platform_acceptance_oracle.py"),
        "qualification": "Package consumer native startup or separately identified failure-policy evidence. Offscreen glyph pixels are not compositor/device/performance certification."}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--directory", type=Path, required=True)
    parser.add_argument("--scripts", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--expected-platform", choices=("windows", "linux"), required=True)
    parser.add_argument("--expected-process-id", type=int, required=True)
    args = parser.parse_args()
    if args.output.exists():
        parser.error("Use a fresh oracle output")
    try:
        report = inspect(args.directory, args.scripts, args.expected_platform, args.expected_process_id)
    except (OSError, KeyError, TypeError, ValueError) as error:
        report = {"schema": "charts-native-text-package-consumer-oracle/v1", "passed": False, "error": type(error).__name__ + ": " + str(error)}
    with args.output.open("x", encoding="utf-8") as stream:
        json.dump(report, stream, ensure_ascii=False, indent=2)
    print(json.dumps({"passed": report["passed"], "output": str(args.output), "failedChecks": report.get("failedChecks"), "error": report.get("error")}))
    raise SystemExit(0 if report["passed"] else 1)
