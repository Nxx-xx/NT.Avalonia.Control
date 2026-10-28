#!/usr/bin/env python3
"""Check C7 source traceability without claiming builds or runtime qualification.

Only the Python standard library is required. The catalog is maintained explicitly:
adding a public ChartBase descendant without a catalog row fails this check. Every
reference names an exact, non-empty source line and its chart symbol. Package
references additionally have to be copied by the isolated consumer harness.
"""
from __future__ import annotations

import argparse
import copy
import hashlib
import json
import re
import sys
from pathlib import Path, PurePosixPath

SOURCE_ROOT = "src/NT.AvaloniaControl.Charts"
CONSUMER_SCRIPT = "scripts/Test-ChartsPackageConsumer.ps1"
CONSUMER_PROGRAM = "scripts/Consumers/Charts/Program.cs"
OFFICIAL_MATRIX = "docs/charts-official-alignment.md"
ROLES = ("tests", "demo", "usage")
ROLE_ROOTS = {
    "tests": "tests/Charts.Headless.Tests/",
    "demo": "samples/NT.AvaloniaControl.Demo/Pages/Charts/",
    "usage": "samples/Charts.Usage/",
}
CLASS = re.compile(
    r"^public\s+((?:(?:abstract|sealed|partial)\s+)*)class\s+"
    r"(\w+)(?:\s*<[^\n>]+>)?\s*:\s*((?:global::)?\w+(?:\.\w+)*)", re.MULTILINE
)


def mask_comments_and_literals(source: str) -> str:
    """Keep line positions while hiding class-like text in comments/literals.

    This is a deliberately small C# declaration scanner, not an API compiler.
    Raw/verbatim/ordinary string and character literals are hidden as units.
    """
    token = re.compile(
        r'//[^\n]*|/\*[\s\S]*?\*/|\$?"{3,}[\s\S]*?"{3,}'
        r'|\$?@"(?:""|[^"])*"|@\$"(?:""|[^"])*"'
        r'|\$?"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\''
    )
    return token.sub(lambda match: re.sub(r"[^\r\n]", " ", match.group()), source)


def declarations(sources: dict[str, str]) -> dict[str, dict]:
    classes: dict[str, dict] = {}
    for path, content in sorted(sources.items()):
        for match in CLASS.finditer(mask_comments_and_literals(content)):
            modifiers, name, base = match.groups()
            base = base.removeprefix("global::")
            # Only canonical Charts-qualified names resolve to this project's
            # short class names. Other.Namespace.ChartBase is not our root.
            base = base.removeprefix("NT.AvaloniaControl.Charts.")
            entry = classes.setdefault(name, {"bases": set(), "abstract": False, "paths": set()})
            entry["bases"].add(base)
            entry["abstract"] |= "abstract" in modifiers.split()
            entry["paths"].add(path)
    return classes


def ancestry(name: str, classes: dict[str, dict]) -> set[str]:
    pending, result = [name], set()
    while pending:
        current = pending.pop()
        if current in result:
            continue
        result.add(current)
        pending.extend(classes.get(current, {}).get("bases", ()))
    return result


def safe_relative(value: object) -> bool:
    if not isinstance(value, str) or not value or "\\" in value or ":" in value:
        return False
    path = PurePosixPath(value)
    return not path.is_absolute() and all(part not in (".", "..") for part in value.split("/"))


class Repository:
    def __init__(self, root: Path):
        self.root = root.resolve()
        self.contents: dict[str, str] = {}
        self.hashes: dict[str, str] = {}
        self.read_paths: set[str] = set()

    def read(self, path: str) -> str:
        if not safe_relative(path):
            raise ValueError(f"Unsafe repository-relative path: {path!r}")
        resolved = (self.root / path).resolve()
        if not resolved.is_relative_to(self.root):
            raise ValueError(f"Reference escapes repository: {path}")
        self.read_paths.add(path)
        if path not in self.contents:
            payload = resolved.read_bytes()
            self.hashes[path] = hashlib.sha256(payload).hexdigest()
            self.contents[path] = payload.decode("utf-8-sig")
        return self.contents[path]


def verify(repo: Repository, matrix: dict) -> dict:
    errors: list[str] = []
    gaps: list[dict] = []
    if matrix.get("schemaVersion") != 1:
        errors.append("schemaVersion must be 1")
    if matrix.get("validationLevel") != "static-traceability":
        errors.append("This verifier only accepts validationLevel=static-traceability")
    if matrix.get("runtimeQualification") != {"windows": "not-asserted", "linux": "not-asserted"}:
        errors.append("Static traceability cannot assert Windows/Linux runtime qualification")

    sources = {}
    for path in sorted((repo.root / SOURCE_ROOT).rglob("*.cs")):
        if any(part in ("bin", "obj") for part in path.relative_to(repo.root / SOURCE_ROOT).parts):
            continue
        relative = path.relative_to(repo.root).as_posix()
        sources[relative] = repo.read(relative)
    classes = declarations(sources)
    concrete = {
        name for name, entry in classes.items()
        if not entry["abstract"] and "ChartBase" in ancestry(name, classes) and name != "ChartBase"
    }
    rows = matrix.get("controls", [])
    names = [row.get("name") for row in rows]
    if len(names) != len(set(names)):
        errors.append("Duplicate control catalog rows")
    if set(names) != concrete:
        errors.append(f"Concrete ChartBase inventory differs: missing={sorted(concrete - set(names))}, unexpected={sorted(set(names) - concrete)}")

    official_text = repo.read(OFFICIAL_MATRIX)
    official_ids = set(re.findall(r"^\| ([A-Z]{2}\d{2}) \|", official_text, re.MULTILINE))
    consumer_script = repo.read(CONSUMER_SCRIPT)
    consumer_program = repo.read(CONSUMER_PROGRAM)
    # The driver copies MainWindow.axaml.cs and named partials from Charts.Usage.
    # Match quoted filenames rather than incidental prose/variable references.
    copy_loop = re.search(
        r"foreach\s*\(\$source\s+in\s+@\(([\s\S]*?)\)\)\s*\{\s*Copy-Item[^\n]+samples/Charts\.Usage/\$source",
        consumer_script,
    )
    copied_usage_names = set(re.findall(r"'([^'/]+\.(?:cs|axaml))'", copy_loop.group(1))) if copy_loop else set()
    if not copied_usage_names:
        errors.append("Cannot identify the actual Charts.Usage source-copy loop in isolated consumer script")
    consumers_seen = set()

    def reference(ref: dict, prefix: str, expected_symbol: str | None = None) -> bool:
        path = ref.get("path")
        if not safe_relative(path):
            errors.append(f"{prefix}: unsafe or missing path")
            return False
        try:
            content = repo.read(path)
        except (OSError, ValueError) as error:
            errors.append(f"{prefix}: {error}")
            return False
        anchor = ref.get("anchor")
        if not isinstance(anchor, str) or not anchor.strip() or anchor not in {line.strip() for line in content.splitlines()}:
            errors.append(f"{prefix}: exact source-line anchor missing in {path}")
            return False
        if expected_symbol and not re.search(r"\b" + re.escape(expected_symbol) + r"\b", anchor):
            errors.append(f"{prefix}: anchor does not reference symbol {expected_symbol}")
            return False
        return True

    role_totals = {role: {"direct": 0, "inherited": 0, "pending": 0} for role in ROLES}
    for row in rows:
        name = row.get("name")
        if name not in concrete:
            continue
        chain = ancestry(name, classes)
        if row.get("baseType") not in classes[name]["bases"]:
            errors.append(f"{name}: baseType differs from declaration")
        src = row.get("source", {})
        if src.get("path") not in classes[name]["paths"]:
            errors.append(f"{name}: source does not declare this control")
        reference(src, f"{name}/source", name)
        for official_id in row.get("officialMatrixIds", []):
            if official_id not in official_ids:
                errors.append(f"{name}: unknown official matrix ID {official_id}")
        for role in ROLES:
            coverage = row.get(role, {})
            status = coverage.get("status")
            if status not in role_totals[role]:
                errors.append(f"{name}/{role}: invalid coverage status")
                continue
            role_totals[role][status] += 1
            symbol = coverage.get("symbol")
            if status == "direct" and symbol != name:
                errors.append(f"{name}/{role}: direct coverage must name the concrete control")
            if status == "inherited" and (symbol == name or symbol not in chain):
                errors.append(f"{name}/{role}: inherited reference must name an actual ancestor")
            refs = coverage.get("references", [])
            if status in ("direct", "inherited") and not refs:
                errors.append(f"{name}/{role}: coverage requires references")
            if status != "direct":
                reason = coverage.get("gap")
                if not isinstance(reason, str) or not reason.strip():
                    errors.append(f"{name}/{role}: non-direct coverage requires an explicit gap")
                gaps.append({"control": name, "role": role, "status": status, "reason": reason})
            for ref in refs:
                if not str(ref.get("path", "")).startswith(ROLE_ROOTS[role]):
                    errors.append(f"{name}/{role}: reference is outside the role's source root")
                reference(ref, f"{name}/{role}", symbol)
        package = row.get("packageConsumer", {})
        status = package.get("status")
        if status not in ("shared-usage-source", "inherited-example-only", "pending"):
            errors.append(f"{name}/packageConsumer: invalid status")
        refs = package.get("usageSources", [])
        if status != "pending" and not refs:
            errors.append(f"{name}/packageConsumer: shared source list is empty")
        for path in refs:
            if not safe_relative(path) or not path.startswith(ROLE_ROOTS["usage"]):
                errors.append(f"{name}/packageConsumer: invalid usage source path")
                continue
            if PurePosixPath(path).name not in copied_usage_names:
                errors.append(f"{name}/packageConsumer: {path} is not copied by isolated consumer script")
            if path not in {ref.get("path") for ref in row.get("usage", {}).get("references", [])}:
                errors.append(f"{name}/packageConsumer: file has no matching Usage reference")
        driver = package.get("driverAnchor")
        if not isinstance(driver, str) or driver not in {line.strip() for line in consumer_program.splitlines()}:
            errors.append(f"{name}/packageConsumer: independent driver invocation missing")
        else:
            consumers_seen.add(driver)
        if status != "shared-usage-source":
            if not package.get("gap"):
                errors.append(f"{name}/packageConsumer: non-direct coverage requires an explicit gap")
            gaps.append({"control": name, "role": "packageConsumer", "status": status, "reason": package.get("gap")})
        call_chain = package.get("usageVerificationCall", {})
        if call_chain.get("path") != "samples/Charts.Usage/MainWindow.axaml.cs":
            errors.append(f"{name}/packageConsumer: Usage verification call must be in the copied MainWindow driver")
        reference(call_chain, f"{name}/packageConsumer/usageVerificationCall")

    for adjunct in matrix.get("adjunctsOutsideChartBaseInventory", []):
        name = adjunct.get("name")
        if name not in classes or name in concrete or adjunct.get("source") not in classes[name]["paths"]:
            errors.append(f"{name}: adjunct inventory or source is stale")
    ci = matrix.get("ciTraceability", {})
    for key in ("job", "execution", "failureEvidence"):
        ref = ci.get(key, {})
        if ref.get("path") != ".github/workflows/charts.yml":
            errors.append(f"ciTraceability/{key}: expected Charts workflow")
        reference(ref, f"ciTraceability/{key}")
    if "scripts/verify_charts_integration_matrix.py --self-test" not in ci.get("execution", {}).get("anchor", ""):
        errors.append("ciTraceability: CI must invoke this verifier and its mutation self-tests")

    return {
        "passed": not errors,
        "validationLevel": "static-traceability",
        "runtimeQualification": {"windows": "not-asserted", "linux": "not-asserted"},
        "concreteChartBaseControls": len(concrete),
        "catalogRows": len(rows),
        "coverageReferences": role_totals,
        "independentDriverInvocationsReferenced": len(consumers_seen),
        "openTraceabilityGaps": gaps,
        "errors": errors,
        "limits": [
            "Exact source references establish traceability only; no build, test or sample was executed.",
            "A copied Usage source establishes a consumer path, not successful nupkg consumption.",
            "The catalog scopes concrete ChartBase descendants; series, axes, layers, FinancialChart and ChartRangeSelector are listed as adjuncts and need their own behavior matrices.",
            "Official row IDs identify intended families, not API compatibility or official runtime parity.",
        ],
    }


def self_test(repo: Repository, matrix: dict) -> dict:
    checks = {}
    checks["baseline-valid"] = verify(repo, matrix)["passed"]
    fixtures = {
        "a.cs": "public abstract class Family : ChartBase {}\npublic sealed partial class Child : Family {}\n",
        "b.cs": '// public class Fake : ChartBase {}\n/*\npublic class FakeTwo : ChartBase {}\n*/\npublic class Other : object {}\npublic partial class Child : IDisposable {}\nvar s = """\npublic class FakeThree : ChartBase {}\n""";',
    }
    parsed = declarations(fixtures)
    actual = {name for name, entry in parsed.items() if not entry["abstract"] and "ChartBase" in ancestry(name, parsed)}
    checks["scanner-follows-partial-inheritance-and-ignores-comment-and-raw-literal-fakes"] = actual == {"Child"}
    qualified = declarations({"qualified.cs": (
        "public sealed class QualifiedChart : NT.AvaloniaControl.Charts.ChartBase {}\n"
        "public sealed class GlobalChart : global::NT.AvaloniaControl.Charts.ChartBase {}\n"
        "public class QualifiedChild : global::NT.AvaloniaControl.Charts.QualifiedChart, IDisposable {}\n"
        "public class OtherChart : Other.Namespace.ChartBase {}\n"
    )})
    qualified_controls = {name for name in qualified if "ChartBase" in ancestry(name, qualified)}
    checks["scanner-includes-qualified-and-global-base-transitive-controls"] = qualified_controls == {"QualifiedChart", "GlobalChart", "QualifiedChild"}
    checks["scanner-does-not-confuse-other-qualified-ChartBase-with-local-root"] = "OtherChart" not in qualified_controls

    def rejected(label, mutate):
        candidate = copy.deepcopy(matrix)
        mutate(candidate)
        checks[label] = not verify(repo, candidate)["passed"]

    rejected("rejects-omitted-control", lambda value: value["controls"].pop())
    rejected("rejects-duplicate-control", lambda value: value["controls"].append(copy.deepcopy(value["controls"][0])))
    rejected("rejects-invented-control", lambda value: value["controls"][0].update(name="InventedChart"))
    rejected("rejects-stale-base-type", lambda value: value["controls"][0].update(baseType="WrongBase"))
    rejected("rejects-source-path-traversal", lambda value: value["controls"][0]["source"].update(path="../outside.cs"))
    rejected("rejects-absolute-source-path", lambda value: value["controls"][0]["source"].update(path="C:/outside.cs"))
    rejected("rejects-stale-source-anchor", lambda value: value["controls"][0]["source"].update(anchor="public class MissingChart : ChartBase {}"))
    rejected("rejects-stale-test-anchor", lambda value: value["controls"][0]["tests"]["references"][0].update(anchor="Missing_test_reference"))
    rejected("rejects-role-reference-relabeling", lambda value: value["controls"][0]["tests"]["references"][0].update(path=value["controls"][0]["source"]["path"]))
    rejected("rejects-unknown-official-id", lambda value: value["controls"][0]["officialMatrixIds"].append("XX99"))
    rejected("rejects-uncopied-package-source", lambda value: value["controls"][0]["packageConsumer"]["usageSources"].append("samples/Charts.Usage/NotCopied.cs"))
    rejected("rejects-stale-independent-driver", lambda value: value["controls"][0]["packageConsumer"].update(driverAnchor="NeverRuns();"))
    rejected("rejects-stale-usage-verification-call", lambda value: value["controls"][0]["packageConsumer"]["usageVerificationCall"].update(anchor="NeverRuns();"))
    rejected("rejects-empty-direct-evidence", lambda value: value["controls"][0]["demo"].update(references=[]))
    rejected("rejects-unrelated-inherited-reference", lambda value: value["controls"][0]["demo"].update(status="inherited", symbol="ShapeMap", gap="not an ancestor"))
    rejected("rejects-static-check-runtime-inflation", lambda value: value.update(validationLevel="fully-qualified-runtime"))
    rejected("rejects-unrun-windows-qualification", lambda value: value["runtimeQualification"].update(windows="passed"))
    rejected("rejects-missing-ci-invocation", lambda value: value["ciTraceability"]["execution"].update(anchor="run: echo no-validation"))
    return {"passed": all(checks.values()), "checks": checks, "count": len(checks)}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--matrix", default="docs/charts-c7-integration-matrix.json")
    parser.add_argument("--output", type=Path)
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    repo = Repository(args.repo)
    try:
        repo.read("scripts/verify_charts_integration_matrix.py")
        matrix = json.loads(repo.read(args.matrix))
        result = verify(repo, matrix)
        if args.self_test:
            result["selfTest"] = self_test(repo, matrix)
            result["passed"] &= result["selfTest"]["passed"]
        changed = [path for path in sorted(repo.read_paths) if hashlib.sha256((repo.root / path).read_bytes()).hexdigest() != repo.hashes[path]]
        result["sourceInputsUnchanged"] = not changed
        if changed:
            result["passed"] = False
            result["errors"].append(f"Inputs changed during traceability verification: {changed}")
        result["inputFiles"] = [{"path": path, "sha256": repo.hashes[path]} for path in sorted(repo.read_paths)]
    except (OSError, ValueError, KeyError, TypeError) as error:
        result = {"passed": False, "validationLevel": "static-traceability", "errors": [str(error)]}
    output = json.dumps(result, ensure_ascii=False, indent=2) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(output, encoding="utf-8")
    print(json.dumps({key: value for key, value in result.items() if key != "inputFiles"}, ensure_ascii=False, indent=2))
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    sys.exit(main())
