import copy
import hashlib
import importlib.util
import json
import shutil
import subprocess
import sys
import tempfile
import unittest
import zipfile
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))
spec = importlib.util.spec_from_file_location("tgd_preview_export", SCRIPTS / "Export-TreeDataGridPreview.py")
exporter = importlib.util.module_from_spec(spec)
spec.loader.exec_module(exporter)


def ps_literal(value):
    return "'" + str(value).replace("'", "''") + "'"


class CloseoutTests(unittest.TestCase):
    def setUp(self):
        base = SCRIPTS.parent / "artifacts/test-tmp/treedatagrid-closeout"
        base.mkdir(parents=True, exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(dir=base)
        self.root = Path(self.temp.name)
        assert self.root.resolve().is_relative_to(base.resolve())
        self.addCleanup(self.temp.cleanup)

    def powershell(self, code):
        if not shutil.which("pwsh"):
            self.skipTest("PowerShell 7 required")
        script = "$ErrorActionPreference='Stop'; . " + ps_literal(SCRIPTS / "TreeDataGrid.Validation.ps1") + "; " + code
        result = subprocess.run(["pwsh", "-NoProfile", "-Command", script], text=True, encoding="utf-8", capture_output=True)
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        return result.stdout

    def test_space_budget_deduplicates_volume_and_records_allowance(self):
        path = ps_literal(self.root)
        result = json.loads(self.powershell("Get-TreeDataGridDiskBudget -Paths @(" + path + "," + path + ") -EstimatedBytes 1 -ReserveBytes 0 | ConvertTo-Json -Depth 8"))
        self.assertTrue(result["passed"])
        self.assertEqual(1, len(result["volumes"]))
        self.assertEqual(1, len(result["volumes"][0]["paths"]))
        self.assertEqual(1, result["volumes"][0]["requiredBytes"])

    def test_low_space_fails_with_receipt(self):
        path = ps_literal(self.root)
        self.powershell("try { Assert-TreeDataGridDiskBudget -OutputDirectory " + path + " -Paths @(" + path + ") -EstimatedBytes 1PB; throw 'unexpected success' } catch { if ($_.Exception.Message -notlike 'Disk preflight failed:*') { throw }; Write-Output 'expected rejection' }")
        self.assertFalse(exporter.read(self.root / "disk-preflight.json")["passed"])

    def test_failed_step_summary_and_file_hash_index(self):
        result = {"stage": "test", "passed": False, "error": "budget exhausted", "steps": [{"name": "disk-preflight", "passed": False, "log": "disk.log"}], "tests": []}
        exporter.write(self.root / "results.json", result)
        self.powershell("Write-TreeDataGridValidationSummary -OutputDirectory " + ps_literal(self.root))
        self.assertEqual("DiskPreflight", exporter.read(self.root / "summary.json")["failureCategory"])
        index = exporter.read(self.root / "evidence-index.json")
        for row in index["files"]:
            self.assertEqual(exporter.sha(self.root / row["path"]), row["sha256"])

    def regression(self):
        return {"passed": True, "sourcesUnchanged": True, "steps": [{"passed": True}], "tests": [
            {"project": name, "result": {"total": 2, "passed": 2, "failed": 0, "errors": 0, "skipped": 0, "notRun": 0}}
            for name in ("TreeDataGrid.Core.Tests", "TreeDataGrid.Headless.Tests", "TreeDataGrid.Themes.Tests")]}

    def test_regression_rejects_missing_project_and_skips(self):
        self.assertEqual(6, exporter.validate_tests(self.regression()))
        missing = self.regression(); missing["tests"].pop()
        skipped = self.regression(); skipped["tests"][0]["result"]["skipped"] = 1
        duplicate = self.regression(); duplicate["tests"][1] = copy.deepcopy(duplicate["tests"][0])
        for result in (missing, skipped, duplicate):
            with self.subTest(result=result), self.assertRaises(ValueError):
                exporter.validate_tests(result)

    def test_regression_rejects_failed_or_changed_inputs(self):
        for field in ("passed", "sourcesUnchanged"):
            result = self.regression(); result[field] = False
            with self.subTest(field=field), self.assertRaises(ValueError):
                exporter.validate_tests(result)

    def test_package_metadata_and_tested_binary_must_match(self):
        package = self.root / "test.nupkg"
        with zipfile.ZipFile(package, "w") as archive:
            archive.writestr("test.nuspec", '<package xmlns="urn:test"><metadata><id>test</id><version>1.0</version></metadata></package>')
            archive.writestr("lib/net10.0/test.dll", b"tested")
        digest = hashlib.sha256(b"tested").hexdigest().upper()
        exporter.validate_package(package, "test", "1.0", digest)
        for version, expected in (("2.0", digest), ("1.0", "0" * 64)):
            with self.subTest(version=version), self.assertRaises(ValueError):
                exporter.validate_package(package, "test", version, expected)

    def bundle(self):
        payload = self.root / "sample.dll"; payload.write_bytes(b"tested")
        manifest = {"schemaVersion": 1, "files": [{"path": payload.name, "bytes": payload.stat().st_size, "sha256": exporter.sha(payload)}]}
        exporter.write(self.root / "SHA256.json", manifest)
        return manifest

    def test_delivery_valid_manifest(self):
        self.bundle()
        self.assertEqual(1, exporter.verify_bundle(self.root))

    def test_delivery_rejects_modified_binary(self):
        self.bundle(); (self.root / "sample.dll").write_bytes(b"tamper")
        with self.assertRaises(ValueError): exporter.verify_bundle(self.root)

    def test_delivery_rejects_missing_file(self):
        self.bundle(); (self.root / "sample.dll").unlink()
        with self.assertRaises(ValueError): exporter.verify_bundle(self.root)

    def test_delivery_rejects_unindexed_nested_manifest(self):
        self.bundle(); (self.root / "nested").mkdir(); (self.root / "nested/SHA256.json").write_text("{}")
        with self.assertRaises(ValueError): exporter.verify_bundle(self.root)

    def test_delivery_rejects_duplicate_and_traversal(self):
        for mode in ("duplicate", "traversal"):
            manifest = self.bundle()
            if mode == "duplicate": manifest["files"].append(copy.deepcopy(manifest["files"][0]))
            else: manifest["files"][0]["path"] = "../outside.dll"
            exporter.write(self.root / "SHA256.json", manifest)
            with self.subTest(mode=mode), self.assertRaises(ValueError): exporter.verify_bundle(self.root)


if __name__ == "__main__":
    unittest.main()
