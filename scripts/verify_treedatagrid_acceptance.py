"""Validate recorded A6 evidence; exit 2 for incomplete acceptance, 1 for invalid evidence."""
import argparse
import hashlib
import json
from pathlib import Path

LOCAL = {"controls", "performance", "retention", "packages", "native-published"}
WINDOWS = {"windows-ime", "windows-drag", "windows-clipboard-focus", "windows-font-dpi", "windows-screenreader", "windows-clean-deployment", "windows-native-performance", "windows-ci"}
REQUIRED = {"local": LOCAL, "windows": LOCAL | WINDOWS, "windows-kylin": LOCAL | WINDOWS | {"kylin-desktop"}}


def check(matrix, scope, root):
    errors, pending, seen = [], [], set()
    if matrix.get("schemaVersion") != 1:
        errors.append("Unsupported schemaVersion")
    rows = matrix.get("requirements", [])
    for row in rows:
        key = row.get("id")
        if key in seen:
            errors.append(f"Duplicate requirement: {key}")
        seen.add(key)
        if key not in REQUIRED[scope]:
            continue
        expected_kind = "automated" if key in LOCAL else "ci" if key == "windows-ci" else "physical"
        if row.get("kind") != expected_kind or scope not in row.get("requiredFor", []):
            errors.append(f"Invalid kind/scope: {key}")
        if row.get("status") not in {"Passed", "Failed", "NotRun", "EnvironmentUnavailable"}:
            errors.append(f"Invalid status: {key}")
        if row.get("status") != "Passed":
            pending.append(key)
            continue
        proof = row.get("evidence") or {}
        try:
            path = (root / proof["path"]).resolve()
            if hashlib.sha256(path.read_bytes()).hexdigest().lower() != proof["sha256"].lower():
                raise ValueError("SHA256 mismatch")
            report = json.loads(path.read_text(encoding="utf-8-sig"))
            if report.get("passed") is not True:
                raise ValueError("Report did not pass")
            if expected_kind == "automated":
                if report.get("evidenceKind") != "LocalAutomated" or key not in report.get("completedRequirements", []):
                    raise ValueError("Automated requirement was not executed")
            elif expected_kind == "physical":
                if report.get("evidenceKind") != "PhysicalInteraction" or report.get("requirement") != key:
                    raise ValueError("Physical evidence cannot be replaced by Headless/native programmatic rendering")
                for field in ("recordedBy", "executedUtc", "environment", "expected", "observed"):
                    if not report.get(field):
                        raise ValueError(f"Missing physical record: {field}")
                if report.get("captureKind") != "Desktop" or not report.get("attachments"):
                    raise ValueError("Physical acceptance needs desktop capture attachments")
                for attachment in report["attachments"]:
                    if hashlib.sha256((root / attachment["path"]).read_bytes()).hexdigest().lower() != attachment["sha256"].lower():
                        raise ValueError("Attachment SHA256 mismatch")
            else:
                if report.get("evidenceKind") != "CiRun" or not report.get("jobUrl", "").startswith("https://github.com/") or "/actions/runs/" not in report["jobUrl"]:
                    raise ValueError("Actual CI run URL required")
                if not report.get("runner") or report.get("nativeWindowCreated") is not True:
                    raise ValueError("CI desktop/window evidence required")
        except (KeyError, OSError, ValueError, TypeError) as exc:
            errors.append(f"{key}: {exc}")
    errors.extend(f"Missing requirement: {key}" for key in sorted(REQUIRED[scope] - seen))
    return {"schemaVersion": 1, "scope": scope, "passed": not errors and not pending, "errors": errors, "pending": sorted(pending)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("matrix", type=Path)
    parser.add_argument("--scope", choices=REQUIRED, default="local")
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parent.parent)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    result = check(json.loads(args.matrix.read_text(encoding="utf-8-sig")), args.scope, args.root)
    text = json.dumps(result, ensure_ascii=False, indent=2)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(text + "\n", encoding="utf-8")
    print(text)
    return 1 if result["errors"] else 0 if result["passed"] else 2


if __name__ == "__main__":
    raise SystemExit(main())
