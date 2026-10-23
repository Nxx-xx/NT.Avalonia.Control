"""Fail-closed binding of a frozen P1 source, native runs and fresh-cache package evidence.

This exports evidence already produced by the runners. It does not run tests, infer
physical acceptance from a window capture, or inherit a passing P0 executable.
"""
import argparse
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET
import zipfile


REPO = Path(__file__).resolve().parents[2]
PRODUCT = "NT.AvaloniaControl.PdfViewer"
THEMES = PRODUCT + ".Themes"
SUITES = {"Core": 92, "Headless": 33}
BACKEND_CHECKS = {
    "fixture-pages-and-outline", "fixture-text", "fixture-search", "fixture-render",
    "published-pdf-provided", "published-source-sha256", "published-page-count",
    "published-first-page-text", "published-search", "published-two-page-renders",
    "actual-pdfium-module-loaded",
}
WINDOW_CHECKS = {
    "native-window-handle", "native-cjk-font-4E2D", "native-cjk-font-6587", "native-cjk-font-9875",
    "native-control-loaded", "native-control-search", "native-control-navigation", "native-control-layout",
    "native-control-page-raster-cached", "native-composition-rendered", "native-window-image",
}
SOURCE_ROOTS = (
    "src/" + PRODUCT + "/", "src/" + THEMES + "/", "benchmarks/PdfViewer.P1/",
    "tests/PdfViewer.Core.Tests/", "tests/PdfViewer.Headless.Tests/",
    "benchmarks/PdfViewer.P0/Fixtures/", "scripts/Consumers/PdfViewer/",
)
SOURCE_FILES = {
    "Directory.Build.props", "tests/Directory.Build.props", "Directory.Packages.props", "global.json", "NuGet.Config",
    "scripts/Freeze-PdfViewerBaseline.ps1", "scripts/Test-PdfViewerNative.ps1",
    "scripts/Verify-PdfViewer-Linux.sh", "scripts/Test-PdfViewerPackageConsumer.ps1",
    ".github/workflows/pdfviewer-native.yml",
}


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def sha(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def utc(value):
    return dt.datetime.fromisoformat(value.replace("Z", "+00:00"))


def mapped_path(value):
    """Read actual bytes across the Windows/WSL drive mount, without executing shell text."""
    value = str(value).replace("\\", "/")
    match = re.fullmatch(r"/mnt/([a-zA-Z])/(.*)", value)
    if os.name == "nt" and match:
        value = match[1].upper() + ":/" + match[2]
    return Path(value).resolve()


def under(directory, relative):
    path = (directory / relative).resolve()
    require(path.is_relative_to(directory), "Evidence path escapes its root: " + str(relative))
    return path


def source_map(value):
    values = value if isinstance(value, list) else value["Files"]
    require(bool(values), "Empty source identity")
    result = {}
    for item in values:
        path = item["Path"].replace("\\", "/")
        require(path not in result, "Duplicate source identity: " + path)
        result[path] = item["SHA256"].upper()
    return result


def critical(path):
    return (path in SOURCE_FILES or path.startswith(SOURCE_ROOTS)) and not (
        {"bin", "obj", "__pycache__"}.intersection(Path(path).parts)
        or path.endswith((".md", ".pyc", ".pyo")))


class Export:
    def __init__(self, root):
        self.root = root
        self.evidence = {}
        self.native_reports = []
        self.window_images = []

    def bind(self, path, expected=None, size=None):
        path = path.resolve()
        require(path.is_file(), "Missing evidence file: " + str(path))
        digest = sha(path)
        require(expected is None or digest == expected.upper(), "Changed evidence file: " + str(path))
        require(size is None or path.stat().st_size == size, "Changed evidence length: " + str(path))
        if path.is_relative_to(self.root):
            scope, relative = "run", path.relative_to(self.root).as_posix()
        elif path.is_relative_to(REPO):
            scope, relative = "repository", path.relative_to(REPO).as_posix()
        else:
            scope, relative = "external", path.as_posix()
        entry = {"scope": scope, "path": relative, "sha256": digest, "bytes": path.stat().st_size}
        self.evidence[(scope, relative)] = entry
        return entry

    def freeze(self):
        directory = self.root / "baseline"
        baseline = read(directory / "baseline.json")
        self.bind(directory / "source-manifest.json", baseline["SourceManifestSHA256"])
        self.bind(directory / "source-snapshot.zip", baseline["SnapshotSHA256"])
        frozen_entries = read(directory / "source-manifest.json")
        frozen = source_map(frozen_entries)
        with zipfile.ZipFile(directory / "source-snapshot.zip") as archive:
            require(len(archive.namelist()) == len(set(archive.namelist())) == len(frozen), "Duplicate/incomplete frozen archive")
            require(set(archive.namelist()) == set(frozen), "Frozen archive file set differs")
            for entry in frozen_entries:
                content = archive.read(entry["Path"])
                require(len(content) == entry["Bytes"] and hashlib.sha256(content).hexdigest().upper() == entry["SHA256"].upper(),
                        "Frozen archive bytes differ: " + entry["Path"])
        current = set(SOURCE_FILES)
        for prefix in SOURCE_ROOTS:
            current.update(path.relative_to(REPO).as_posix() for path in (REPO / prefix).rglob("*") if path.is_file())
        current = {path for path in current if critical(path)}
        frozen_critical = {path for path in frozen if critical(path)}
        require(current == frozen_critical, "Frozen executable source set changed/missing: " + repr(sorted(current ^ frozen_critical)))
        for path in sorted(current):
            self.bind(REPO / path, frozen[path])
        self.frozen = frozen
        self.frozen_utc = utc(baseline["CapturedUtc"])
        self.native_assets = read(directory / "native-assets.json")
        require(self.native_assets["Pdfium"] == "155.0.8044", "Wrong native asset baseline")
        for item in self.native_assets["Assets"]:
            require(item["Matches"] is True and item["ActualSHA256"].upper() == item["ExpectedSHA256"].upper(), "Invalid frozen native asset")
        return {**baseline, "SourceFileCount": len(frozen), "CurrentExecutableSourceFileCount": len(current),
                "CurrentExecutableSourceMatchesFreeze": True, "DocumentationMayBeUpdatedAfterFreeze": True}

    def sources(self, directory):
        before, after = source_map(read(directory / "source-before.json")), source_map(read(directory / "source-after.json"))
        require(before == after, "Source changed during native run: " + str(directory))
        for path, digest in before.items():
            require(self.frozen.get(path) == digest, "Native run source differs from frozen baseline: " + path)
            self.bind(under(REPO, path), digest)
        require("benchmarks/PdfViewer.P1/Program.cs" in before and "src/" + PRODUCT + "/PdfDocument.cs" in before,
                "Native run source identity omits implementation/host")
        return len(before)

    def tests(self, directory, functional):
        result = []
        for suite, count in SUITES.items():
            path = directory / (suite + ".xml")
            document = ET.parse(path).getroot()
            assemblies = document.findall("assembly")
            require(len(assemblies) == 1, "Unexpected test assembly set: " + str(path))
            assembly = assemblies[0]
            require(assembly.get("total") == assembly.get("passed") == str(count)
                    and all(assembly.get(key) == "0" for key in ("failed", "errors", "skipped", "not-run")), "Incomplete/failing tests: " + str(path))
            tests = assembly.findall(".//test")
            require(len(tests) == count and all(test.get("result") == "Pass" for test in tests), "Test rows differ from summary: " + str(path))
            require(len({test.get("name") for test in tests}) == count, "Duplicate test names: " + str(path))
            require(utc(assembly.get("start-rtf")) >= self.frozen_utc, "Stale test XML predates source freeze")
            binary = mapped_path(assembly.get("name"))
            require(binary.name == "PdfViewer." + suite + ".Tests.dll", "Unexpected test binary")
            self.bind(binary)
            self.bind(binary.with_name(PRODUCT + ".dll"), functional)
            result.append({"suite": suite, "passed": count, "total": count, "xml": self.bind(path),
                           "assembly": self.bind(binary), "environment": assembly.get("environment")})
        return result

    def report(self, directory, name, linux):
        path = directory / name / "result.json"
        report = read(path)
        require(report.get("Passed") is True and not report.get("Failure"), "Failing native report: " + str(path))
        require(utc(report["StartedUtc"]) >= self.frozen_utc, "Native report predates frozen source")
        checks = report.get("Checks", {})
        expected = BACKEND_CHECKS | (WINDOW_CHECKS if name == "native-window" else set())
        require(expected <= set(checks) and all(value is True for value in checks.values()), "Incomplete native checks: " + str(path))
        require(report["Headless"] is False and report["NativeWindowRequested"] is (name == "native-window"), "Native/headless/window mode mislabeled")
        for flag in ("PhysicalInputVerified", "ScanOutVerified", "OfficialPerformanceComparison", "KylinVerified"):
            require(report[flag] is False, "Unexpected unsupported acceptance claim: " + flag)
        require(report["RuntimeIdentifier"] == ("linux-x64" if linux else "win-x64"), "Unexpected actual native RID")
        require(report["ProcessArchitecture"].upper() == "X64", "Unexpected actual native process architecture")
        loaded = report["Observations"]["LoadedPdfium"]
        require(len(loaded) == 1, "Native module identity is missing/ambiguous")
        asset = next(item for item in self.native_assets["Assets"] if item["RID"] == report["RuntimeIdentifier"])
        self.bind(mapped_path(loaded[0]["Path"]), asset["ActualSHA256"], loaded[0]["Bytes"])
        require(loaded[0]["SHA256"].upper() == asset["ActualSHA256"].upper(), "Loaded native DLL differs from frozen asset")
        self.bind(REPO / "benchmarks/PdfViewer.P1/bin/Release/net10.0/PdfViewer.P1.dll", report["HostAssemblySHA256"])
        self.bind(REPO / ("src/" + PRODUCT + "/bin/Release/net10.0/" + PRODUCT + ".dll"), report["FunctionalAssemblySHA256"])
        published = report["Observations"]["PublishedSource"]
        pinned = next(item for item in read(REPO / "benchmarks/PdfViewer.P0/Fixtures/real-corpus-sources.json")["Sources"] if item["Id"] == "gnu-make")
        require(published["SHA256"].upper() == pinned["SHA256"].upper() and published["Bytes"] == pinned["Bytes"]
                and published["Pages"] == 229 and published["SearchMatches"] > 0, "Published source/reading evidence differs")
        self.bind(directory / "corpus/gnu-make.pdf", pinned["SHA256"], pinned["Bytes"])
        fixture = directory / name / "owned-fixture.pdf"
        self.bind(fixture, report["Observations"]["SyntheticFixtureSHA256"])
        if name == "native-window":
            native_window = report["Observations"]["NativeWindow"]
            require(native_window["CompositionRendered"] is True and native_window["CachedPageCount"] > 0
                    and native_window["CachedBytes"] > 0, "Native window did not render/cache pages")
            image = directory / name / "native-window.png"
            self.bind(image, native_window["ImageSHA256"])
            self.window_images.append(image)
            self.native_reports.append(path)
        return report

    def platform(self, linux=False):
        directory = self.root / ("linux" if linux else "windows")
        if linux and not directory.exists():
            return {"executed": False, "status": "not executed; CI configuration is not execution evidence"}
        run = read(directory / "run.json")
        require(run.get("Passed") is True and not run.get("Failure") and run["WindowRequested"] is True
                and run["CoreAndHeadlessRequested"] is True and run["CoreAndHeadlessExecuted"] is True,
                "Incomplete native run: " + str(directory))
        require(run["PhysicalDesktopAcceptance"] is False, "Window smoke incorrectly claims physical acceptance")
        require(run["BuildExecuted"] is run["BuildRequested"], "Build request/execution mismatch")
        if linux:
            require(run["ExitCode"] == 0 and run["LastStage"] == "complete", "Linux wrapper did not finish successfully")
        else:
            steps = run["Steps"]
            names = [step["Name"] for step in steps]
            require(len(set(names)) == len(names) and {"dotnet-info", "backend", "native-window", "test-Core", "test-Headless"} <= set(names), "Missing/duplicate Windows runner steps")
            for step in steps:
                require(step["ExitCode"] == 0 and step["TimedOut"] is False, "Failed/timed out child: " + step["Name"])
                self.bind(mapped_path(step["Stdout"]))
                self.bind(mapped_path(step["Stderr"]))
        for entry in run["Files"]:
            self.bind(under(directory, entry["Path"]), entry["SHA256"], entry["Bytes"])
        source_count = self.sources(directory)
        backend = self.report(directory, "backend", linux)
        window = self.report(directory, "native-window", linux)
        require(backend["FunctionalAssemblySHA256"] == window["FunctionalAssemblySHA256"]
                and backend["HostAssemblySHA256"] == window["HostAssemblySHA256"], "Mixed native executable builds")
        environment = read(directory / "environment.json")
        if linux:
            asset = read(directory / "native-asset.json")
            require(asset["DynamicDependenciesResolved"] is True, "Unresolved Linux dynamic dependencies")
            actual = backend["Observations"]["LoadedPdfium"][0]
            require(mapped_path(actual["Path"]) == mapped_path(asset["Path"])
                    and actual["SHA256"].upper() == asset["SHA256"].upper(), "Linux ELF audit and loaded asset differ")
            self.bind(mapped_path(asset["Path"]), asset["SHA256"], asset["Bytes"])
            require(bool(environment["Wsl"]) == bool(backend["Wsl"]), "Linux WSL classification differs")
        return {"executed": True, "passed": True, "sourceFileCount": source_count,
                "functionalAssemblySHA256": backend["FunctionalAssemblySHA256"], "hostAssemblySHA256": backend["HostAssemblySHA256"],
                "actualOS": backend["OS"], "runtime": backend["Runtime"], "runtimeIdentifier": backend["RuntimeIdentifier"],
                "wsl": backend["Wsl"], "environment": environment, "buildRequested": run["BuildRequested"],
                "buildExecuted": run["BuildExecuted"], "nativeBackendChecks": len(backend["Checks"]),
                "nativeWindowChecks": len(window["Checks"]), "regressions": self.tests(directory, backend["FunctionalAssemblySHA256"]),
                "nativeModule": backend["Observations"]["LoadedPdfium"][0],
                "captureKind": window["ImageCaptureKind"], "physicalDesktopAcceptance": False,
                "githubRunId": backend["GitHubRunId"], "run": self.bind(directory / "run.json")}

    def packages(self, functional):
        path = self.root / "packages/consumer-results.json"
        report = read(path)
        require(report["Passed"] is True and len(report["Consumers"]) == 2, "Incomplete fresh-cache package consumers")
        require({item["Mode"] for item in report["Consumers"]} == {"FunctionOnly", "DefaultTheme"}, "Package consumer modes differ")
        result = []
        for item in report["Consumers"]:
            require(item["Passed"] is True, "Failed package consumer")
            consumer = mapped_path(item["ConsumerDirectory"])
            runtime = mapped_path(item["Result"])
            actual = read(runtime)
            checks = actual["Checks"]
            require(actual["Passed"] is True and not actual["Failure"] and actual["Mode"] == item["Mode"]
                    and len(checks) == item["CheckCount"] and all(value is True for value in checks.values()), "Failed/mixed consumer runtime")
            require({"nativeMetadataAndPermissionContract", "typedSourceFromRestoredPackage", "asyncDisposeRejectsSubsequentOperations",
                     "bindableDocumentStateFromPackage", "functionalAssemblyIndependentOfThemes"} <= set(checks), "Stale pre-P1 package consumer")
            supervisor = read(consumer / "supervisor.json")
            require(supervisor["ExitCode"] == 0 and supervisor["TimedOut"] is False and not supervisor["Error"]
                    and supervisor["ConsumerResultPresent"] is True, "Failed/timed out package process")
            require(utc(supervisor["StartedUtc"]) >= self.frozen_utc, "Stale package consumer predates source freeze")
            self.bind(mapped_path(supervisor["ConsumerAssembly"]), supervisor["ConsumerAssemblySHA256"])
            for name in ("Stdout", "Stderr"):
                self.bind(mapped_path(supervisor[name]))
            assets_path = consumer / "obj/project.assets.json"
            assets = read(assets_path)
            libraries = assets["libraries"]
            require(not any(value["type"] == "project" for value in libraries.values()), "Source project leaked into package restore graph")
            require("PDFsharp/6.2.4" in libraries, "Pinned production writer absent from restored graph")
            project = consumer / "Consumer.csproj"
            require(not ET.parse(project).getroot().findall(".//ProjectReference"), "Consumer contains source project references")
            output = runtime.parent
            self.bind(output / (PRODUCT + ".dll"), functional)
            if item["Mode"] == "FunctionOnly":
                require(not any(name.lower().startswith(THEMES.lower() + "/") for name in libraries)
                        and not (output / (THEMES + ".dll")).exists(), "Optional Themes leaked into function-only consumer")
            archives = []
            for package in item["Products"]:
                restored = mapped_path(package["RestoredArchive"])
                self.bind(restored, package["SHA256"])
                copied = self.root / "packages" / (package["Package"] + "." + package["Version"] + ".nupkg")
                self.bind(copied, package["SHA256"])
                with zipfile.ZipFile(restored) as archive:
                    binary_name = "lib/net10.0/" + package["Package"] + ".dll"
                    binary_sha = hashlib.sha256(archive.read(binary_name)).hexdigest().upper()
                    if package["Package"] == PRODUCT:
                        require(binary_sha == functional, "Packaged functional DLL differs from current native runs")
                    self.bind(restored.parent / binary_name, binary_sha)
                    self.bind(output / (package["Package"] + ".dll"), binary_sha)
                archives.append({"package": package["Package"], "version": package["Version"], "archive": self.bind(copied), "dllSHA256": binary_sha})
            expected = {PRODUCT} if item["Mode"] == "FunctionOnly" else {PRODUCT, THEMES}
            require({item["package"] for item in archives} == expected, "Consumer restored products differ")
            for fixture in item["WriterFixtures"]:
                self.bind(mapped_path(fixture["Output"]), fixture["SHA256"])
            for artifact in (runtime, consumer / "supervisor.json", assets_path, project):
                self.bind(artifact)
            result.append({"mode": item["Mode"], "passed": True, "checks": len(checks), "sourceProjectReferences": False,
                           "functionOnlyThemeDependency": False if item["Mode"] == "FunctionOnly" else None,
                           "functionalAssemblySHA256": functional, "freshPackageCache": item["FreshPackageCache"], "packages": archives})
        return result

    def visual(self):
        candidates = [path for path in (self.root / "final/manual-visual-review.json", self.root / "manual-visual-review.json") if path.exists()]
        require(len(candidates) <= 1, "Ambiguous manual visual review records")
        if not candidates:
            return {"performed": False, "status": "No manual review evidence supplied; native automated captures are recorded separately."}
        path = candidates[0]
        report = read(path)
        require(report["passed"] is True and report.get("reviewer") and report.get("reviewedUtc"), "Incomplete manual visual review")
        reports = {under(self.root, item["file"]): item["sha256"] for item in report["nativeReports"]}
        images = {under(self.root, item["file"]): item["sha256"] for item in report["images"]}
        require(set(reports) == set(self.native_reports) and set(images) == set(self.window_images), "Manual review does not bind every actual native report and window image")
        for file, digest in {**reports, **images}.items():
            self.bind(file, digest)
        return {"performed": True, "passed": True, "reviewer": report["reviewer"], "reviewedUtc": report["reviewedUtc"], "report": self.bind(path)}

    def builds(self):
        result = []
        for name in ("buildUsage", "buildDemo"):
            path = self.root / "logs" / (name + ".log")
            log = path.read_text(encoding="utf-8-sig")
            require(any(summary in log for summary in ("Build succeeded.", "生成成功", "已成功生成"))
                    and re.search(r"\b0 Error\(s\)|0\s*个错误", log), "No successful build summary: " + str(path))
            result.append({"name": name, "passed": True, "scope": "compile only", "log": self.bind(path)})
        return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    root, output = Path(args.run).resolve(), Path(args.output).resolve()
    require(root.is_dir(), "Run directory is missing")
    require(not output.is_relative_to(root), "The record must be outside its recursively hashed evidence root")
    export = Export(root)
    baseline = export.freeze()
    windows, linux = export.platform(), export.platform(linux=True)
    functional = windows["functionalAssemblySHA256"]
    if linux["executed"]:
        require(linux["functionalAssemblySHA256"] == functional, "Windows/Linux functional DLLs differ; classify binary equivalence explicitly before export")
    packages = export.packages(functional)
    visual = export.visual()
    builds = export.builds()
    parent_path = REPO / "docs/validation/pdfviewer-p0-real-corpus-20261015.json"
    parent = read(parent_path)
    parent_evidence = export.bind(parent_path, export.frozen[parent_path.relative_to(REPO).as_posix()])
    workflow = REPO / ".github/workflows/pdfviewer-native.yml"
    require("ubuntu-24.04" in workflow.read_text(encoding="utf-8") and "windows-latest" in workflow.read_text(encoding="utf-8"), "Required CI targets are absent")
    for path in root.rglob("*"):
        if path.is_file():
            export.bind(path)
    run_ids = [item["githubRunId"] for item in (windows, linux) if item.get("githubRunId")]
    exported_utc = dt.datetime.now(dt.timezone.utc)
    record = {
        "schemaVersion": 1, "phase": "P1 public reader contracts, capabilities, scheduling and asynchronous native lifetime",
        "date": exported_utc.astimezone(dt.timezone(dt.timedelta(hours=8))).date().isoformat(),
        "timeZone": "Asia/Hong_Kong", "runId": root.name, "artifactRoot": root.as_posix(),
        "exportedUtc": exported_utc.isoformat(), "exporterSHA256": sha(Path(__file__)),
        "status": "current-platform evidence verified; target Ubuntu CI and physical-platform acceptance remain open",
        "G1Complete": False, "baseline": baseline, "functionalAssemblySHA256": functional,
        "parentRecord": {**parent_evidence, "runId": parent["runId"],
                         "scope": "Historical P0 provenance only; P0 binaries, test counts and passes are not inherited as P1 coverage."},
        "windows": windows, "linux": linux, "sameFunctionalILAcrossExecutedPlatforms": True if linux["executed"] else None,
        "packageConsumers": packages, "builds": builds, "manualVisualReview": visual,
        "ci": {"configured": True, "workflow": export.bind(workflow), "targets": ["windows-latest", "ubuntu-24.04"],
               "configuredOnly": not bool(run_ids), "observedGitHubRunIds": run_ids,
               "ubuntu2404Executed": False, "scope": "Configuration is not proof that hosted CI ran."},
        "audit": {"logsHashBound": [path.relative_to(root).as_posix() for path in (root / "logs").rglob("*") if path.is_file() and "audit" in path.name.lower()],
                  "passedInferredFromLogPresence": False},
        "scope": {"newContracts": "Owned/reopenable sources, metadata/permissions/errors, viewer state/session and optional host service interfaces.",
                  "implementedViewerOperations": ["read", "search", "permission/host-gated text copying"],
                  "futureViewerOperations": ["annotations", "form input", "save", "printing", "sharing"],
                  "scheduling": "One process-wide native worker, weighted foreground/search/thumbnail queues; pagewise search; running native calls are not preempted.",
                  "release": "Dispose stops admission and initiates cancellation; DisposeAsync awaits actual native close and pinned-input release.",
                  "physicalInputVerified": False, "scanOutVerified": False, "officialPerformanceCompared": False,
                  "kylinX64Arm64Verified": False, "performanceAcceptance": False},
        "remainingG1Items": ["Execute the configured Ubuntu 24.04 x64 hosted CI job and retain its run evidence.",
                             "Validate exact deployment images and native desktop dependencies on target devices; WSL/native visual capture is not physical input/IME or scan-out acceptance."],
        "evidence": sorted(export.evidence.values(), key=lambda item: (item["scope"], item["path"])),
    }
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(record, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"output": str(output), "evidence": len(export.evidence), "functionalAssemblySHA256": functional,
                      "windows": True, "linux": linux["executed"], "G1Complete": False}))


if __name__ == "__main__":
    try:
        main()
    except (RuntimeError, OSError, KeyError, ValueError, StopIteration, ET.ParseError, zipfile.BadZipFile) as error:
        print("P1 record export refused: " + str(error), file=sys.stderr)
        raise SystemExit(1)
