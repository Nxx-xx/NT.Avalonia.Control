"""Assemble a local preview from verified packages, sample and existing A6 evidence."""
import argparse
import datetime
import hashlib
import json
import shutil
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

from verify_treedatagrid_acceptance import check
from verify_treedatagrid_native import verify


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest().upper()


def write(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def require(condition, message):
    if not condition:
        raise ValueError(message)


def validate_tests(result):
    require(result.get("passed") is True and result.get("sourcesUnchanged") is True, "Regression failed or scoped inputs changed")
    steps = result.get("steps", [])
    require(steps and all(x.get("passed") is True for x in steps), "Incomplete regression steps")
    tests = result.get("tests", [])
    require({x["project"] for x in tests} == {"TreeDataGrid.Core.Tests", "TreeDataGrid.Headless.Tests", "TreeDataGrid.Themes.Tests"}
            and len(tests) == 3, "Missing or duplicate control test project")
    total = 0
    for row in tests:
        item = row["result"]
        require(type(item.get("total")) is int and item["total"] > 0 and item["total"] == item.get("passed")
                and all(item.get(k) == 0 for k in ("failed", "errors", "skipped", "notRun")), "Incomplete control tests")
        total += item["passed"]
    return total


def validate_package(path, name, version, expected):
    with zipfile.ZipFile(path) as archive:
        spec = [p for p in archive.namelist() if p.endswith(".nuspec")]
        require(len(spec) == 1, "Invalid package metadata")
        metadata = ET.fromstring(archive.read(spec[0])).find("{*}metadata")
        require(metadata.findtext("{*}id") == name and metadata.findtext("{*}version") == version, "Wrong package identity/version")
        actual = hashlib.sha256(archive.read("lib/net10.0/" + name + ".dll")).hexdigest().upper()
        require(actual == expected, "Package DLL does not match tested binary")


def verify_bundle(directory):
    manifest = read(directory / "SHA256.json")
    entries = manifest["files"]
    require(entries, "Empty delivery manifest")
    names = [x["path"] for x in entries]
    require(len(names) == len(set(names)), "Duplicate delivery file")
    for item in entries:
        path = Path(item["path"])
        require(not path.is_absolute() and ".." not in path.parts and path.as_posix() != "SHA256.json", "Unsafe delivery path")
        file = directory / path
        require(file.resolve().is_relative_to(directory.resolve()), "Delivery path escapes its directory")
        require(file.is_file() and file.stat().st_size == item["bytes"] and sha(file) == item["sha256"], "Delivery file changed: " + str(path))
    actual = {p.relative_to(directory).as_posix() for p in directory.rglob("*") if p.is_file() and p != directory / "SHA256.json"}
    require(actual == set(names), "Delivery manifest has missing or additional files")
    return len(entries)


def export(repo, regression, native, sample, output):
    for input_path in (regression, native, sample):
        require(not output.is_relative_to(input_path) and not input_path.is_relative_to(output), "Output overlaps an input")
    require(not output.exists() and not Path(str(output) + ".zip").exists(), "Use a new delivery directory; previous deliveries remain intact")
    count = validate_tests(read(regression / "results.json"))
    for item in read(regression / "source-manifest-final.json"):
        require(sha(repo / item["path"]) == item["sha256"], "Regression source changed: " + item["path"])
    matrix = read(regression / "platform-matrix.json")
    gates = {scope: check(matrix, scope, repo) for scope in ("local", "windows", "windows-kylin")}
    require(gates["local"]["passed"] and all(not g["errors"] for g in gates.values()), "Acceptance evidence is invalid")
    hashes = read(regression / "binary-hashes.json")
    expected = {}
    for kind, name in (("function", "NT.AvaloniaControl.TreeDataGrid"), ("theme", "NT.AvaloniaControl.TreeDataGrid.Themes")):
        group = hashes[kind]
        require(group["allMatch"] and group["files"] and len({x["sha256"] for x in group["files"]}) == 1, "Tested binary hashes disagree")
        expected[name] = group["files"][0]["sha256"]
        require(sha(sample / (name + ".dll")) == expected[name], "Sample contains a different tested DLL")
    for name in ("TreeDataGrid.Usage.dll", "TreeDataGrid.Usage.deps.json", "TreeDataGrid.Usage.runtimeconfig.json"):
        require((sample / name).is_file(), "Incomplete standalone sample")
    version = ET.parse(repo / "Directory.Build.props").getroot().findtext("PropertyGroup/Version")
    packages = []
    for name, digest in expected.items():
        path = repo / "artifacts/packages" / (name + "." + version + ".nupkg")
        validate_package(path, name, version, digest)
        packages.append(path)
    native_results = {}
    for name in ("explicit-performance", "explicit-soak-30m"):
        proof = verify(native / name)
        require(proof["passed"], "Reused native raw evidence is invalid: " + name)
        report = read(native / name / "report.json")
        require(report["environment"]["functionDll"]["sha256"] == expected["NT.AvaloniaControl.TreeDataGrid"]
                and report["environment"]["themeDll"]["sha256"] == expected["NT.AvaloniaControl.TreeDataGrid.Themes"], "Native evidence uses another product/theme")
        if name == "explicit-soak-30m":
            require(report["measuredSoakSeconds"] >= 1800, "Reused native soak is shorter than 30 minutes")
        native_results[name] = proof
    # Product/client/host code remains frozen; driver bookkeeping changes are disclosed separately.
    code_changes = []
    allowed = {"scripts/Test-TreeDataGridAlignment.ps1", "scripts/Test-TreeDataGridNativePerformance.ps1", "benchmarks/TreeDataGrid.NativeBenchmarks/README.md"}
    for item in read(native / "current-input-manifest.json"):
        current = sha(repo / item["path"])
        if current != item["sha256"]:
            require(Path(item["path"]).as_posix() in allowed, "Measured executable input changed: " + item["path"])
            code_changes.append({"path": item["path"], "measuredSha256": item["sha256"], "currentSha256": current})
    output.mkdir(parents=True)
    (output / "packages").mkdir()
    for path in packages:
        shutil.copy2(path, output / "packages" / path.name)
    shutil.copytree(sample, output / "sample")
    (output / "docs").mkdir()
    for name in ("treedatagrid-usage.md", "treedatagrid-binding-support.md", "treedatagrid-row-move-support.md", "treedatagrid-element-factory.md", "treedatagrid-accessibility.md", "treedatagrid-local-preview-closeout.md"):
        shutil.copy2(repo / "docs" / name, output / "docs" / name)
    evidence = output / "evidence"
    evidence.mkdir()
    for path in regression.rglob("*"):
        relative = path.relative_to(regression)
        if path.is_file() and relative.parts[0] != "publish":
            target = evidence / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, target)
    write(evidence / "native-revalidation.json", native_results)
    for name in native_results:
        target = evidence / name
        target.mkdir()
        for file in ("report.json", "initial.json", "process.json", "validated.json"):
            shutil.copy2(native / name / file, target / file)
    write(evidence / "reused-native-input-amendments.json", code_changes)
    native_index = [{"path": str(path.relative_to(repo)), "bytes": path.stat().st_size, "sha256": sha(path)}
                    for name in native_results for path in sorted((native / name).iterdir()) if path.is_file()]
    write(evidence / "external-native-evidence-index.json", {"scope": "Raw traces/images remain at these repository-relative paths; summarized reports are included in this delivery.", "files": native_index})
    soak = read(native / "explicit-soak-30m/report.json")
    release = {
        "schemaVersion": 1, "version": version, "kind": "LocalWindowsPreview", "localCloseoutPassed": True,
        "completedUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(), "controlsPassed": count,
        "productHashes": expected, "productCodeChanged": False, "defaultHostReusePreserved": True,
        "regression": str(regression.relative_to(repo)), "reusedNativeEvidence": str(native.relative_to(repo)),
        "nativeSoakSeconds": soak["measuredSoakSeconds"], "privateBytesMedianDelta": soak["drift"]["privateBytesMedianDelta"],
        "acceptance": gates, "overallPlatformA6Complete": False,
        "runtime": ".NET 10 framework-dependent; Windows x64 native assets included; uses the locally verified runtime, not a clean deployment certificate.",
        "limitations": ["Real screen reader, IME, physical drag/focus/clipboard, DPI and clean deployment remain pending.",
                        "Actual Windows CI and target Kylin remain pending.", "Opaque native CompiledBinding safe writeback (A3.1) remains open.",
                        "Native Grid/Table patterns are not implemented.", "Fluent builder API is outside scope.",
                        "Controlled UIA results do not certify arbitrary client reference lifetimes; pre-provider-shutdown row peers/hosts remain disclosed in native reports."],
    }
    write(output / "release.json", release)
    (output / "README.md").write_text(
        "# TreeDataGrid " + version + " 本地预览交付\n\n"
        "Windows 本地自动验收完成；整体平台 A6 保持待验。完整状态与限制见 release.json。\n\n"
        "- packages/：功能包与主题包；两者使用相同版本。\n"
        "- sample/：独立 TreeDataGrid.Usage 发布目录，需要 .NET 10；在 Windows x64 执行 `dotnet sample/TreeDataGrid.Usage.dll`。\n"
        "- docs/：用法、绑定、行移动、工厂、无障碍与收尾说明。\n"
        "- evidence/：本轮回归报告和复用的原生报告；完整原始轨迹由 external-native-evidence-index.json 索引。\n"
        "- SHA256.json：全部交付文件的长度与哈希。验包：`python scripts/Export-TreeDataGridPreview.py --verify <解压目录>`（仓库脚本）。\n\n"
        "包和示例均为本地候选产物；没有公共包发布或真实平台认证。\n", encoding="utf-8")
    entries = [{"path": p.relative_to(output).as_posix(), "bytes": p.stat().st_size, "sha256": sha(p)}
               for p in sorted(output.rglob("*")) if p.is_file()]
    write(output / "SHA256.json", {"schemaVersion": 1, "files": entries})
    files = verify_bundle(output)
    archive_path = Path(str(output) + ".zip")
    with zipfile.ZipFile(archive_path, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for path in sorted(output.rglob("*")):
            if path.is_file():
                archive.write(path, path.relative_to(output).as_posix())
    with zipfile.ZipFile(archive_path) as archive:
        require(archive.testzip() is None, "Delivery ZIP failed CRC verification")
        for item in read(output / "SHA256.json")["files"]:
            with archive.open(item["path"]) as stream:
                require(hashlib.file_digest(stream, "sha256").hexdigest().upper() == item["sha256"], "ZIP differs from delivery manifest")
    receipt = {"passed": True, "files": files, "archive": archive_path.name, "bytes": archive_path.stat().st_size, "sha256": sha(archive_path)}
    write(output.parent / (output.name + ".receipt.json"), receipt)
    return receipt


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--verify", type=Path)
    parser.add_argument("--repo", type=Path, default=Path(__file__).resolve().parent.parent)
    for name in ("regression", "native-evidence", "sample", "output"):
        parser.add_argument("--" + name, type=Path)
    args = parser.parse_args()
    if args.verify:
        print(json.dumps({"passed": True, "files": verify_bundle(args.verify.resolve())}))
        return
    require(all((args.regression, args.native_evidence, args.sample, args.output)), "All export paths are required")
    print(json.dumps(export(args.repo.resolve(), args.regression.resolve(), args.native_evidence.resolve(), args.sample.resolve(), args.output.resolve()), indent=2))


if __name__ == "__main__":
    main()
