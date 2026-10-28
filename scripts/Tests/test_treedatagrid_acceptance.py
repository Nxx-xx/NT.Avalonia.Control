import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

SCRIPT = Path(__file__).resolve().parents[1] / "verify_treedatagrid_acceptance.py"
SPEC = importlib.util.spec_from_file_location("acceptance", SCRIPT)
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class AcceptanceTests(unittest.TestCase):
    def setUp(self):
        base = (SCRIPT.parent.parent / "artifacts/treedatagrid-acceptance-tests").resolve()
        base.mkdir(parents=True, exist_ok=True)
        self.directory = tempfile.TemporaryDirectory(dir=base)
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.assertTrue(self.root.resolve().is_relative_to(base))
        self.matrix = json.loads((SCRIPT.parent.parent / "docs/treedatagrid-platform-matrix.template.json").read_text(encoding="utf-8-sig"))
        report = {"passed": True, "evidenceKind": "LocalAutomated", "completedRequirements": sorted(MODULE.LOCAL)}
        self.proof = self.save("auto.json", report)
        for row in self.matrix["requirements"]:
            if row["id"] in MODULE.LOCAL:
                row.update(status="Passed", evidence=copy.deepcopy(self.proof))

    def save(self, name, report):
        data = json.dumps(report).encode()
        (self.root / name).write_bytes(data)
        return {"path": name, "sha256": hashlib.sha256(data).hexdigest()}

    def test_local_acceptance_allows_explicit_pending_physical_requirements(self):
        self.assertTrue(MODULE.check(self.matrix, "local", self.root)["passed"])

    def test_windows_release_stays_incomplete_without_physical_and_ci_evidence(self):
        result = MODULE.check(self.matrix, "windows", self.root)
        self.assertFalse(result["passed"])
        self.assertIn("windows-ime", result["pending"])
        self.assertIn("windows-ci", result["pending"])

    def test_programmatic_report_cannot_satisfy_ime(self):
        row = next(row for row in self.matrix["requirements"] if row["id"] == "windows-ime")
        row.update(status="Passed", evidence=self.proof)
        self.assertTrue(MODULE.check(self.matrix, "windows", self.root)["errors"])

    def test_tampered_artifact_is_rejected(self):
        (self.root / "auto.json").write_text("{}")
        self.assertTrue(MODULE.check(self.matrix, "local", self.root)["errors"])

    def test_missing_required_row_is_invalid(self):
        self.matrix["requirements"] = [row for row in self.matrix["requirements"] if row["id"] != "controls"]
        self.assertTrue(MODULE.check(self.matrix, "local", self.root)["errors"])

    def test_local_report_cannot_satisfy_ci(self):
        row = next(row for row in self.matrix["requirements"] if row["id"] == "windows-ci")
        row.update(status="Passed", evidence=self.proof)
        self.assertTrue(MODULE.check(self.matrix, "windows", self.root)["errors"])

    def test_missing_proof_is_invalid_for_passed_row(self):
        self.matrix["requirements"][0]["evidence"] = {"path": "absent.json", "sha256": "0" * 64}
        self.assertTrue(MODULE.check(self.matrix, "local", self.root)["errors"])

    def test_claiming_pass_without_executing_requirement_is_invalid(self):
        proof = self.save("unexecuted.json", {"passed": True, "evidenceKind": "LocalAutomated", "completedRequirements": []})
        self.matrix["requirements"][0]["evidence"] = proof
        self.assertTrue(MODULE.check(self.matrix, "local", self.root)["errors"])


if __name__ == "__main__":
    unittest.main()
