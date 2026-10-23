"""Independent CJK font-subset oracle: pypdf + fontTools + Poppler.

Python never authors the production positive outputs. A second Python containing
fontTools is used for read-only glyph analysis; product runtime uses only C#.
"""
from __future__ import annotations
import argparse
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import uuid
import pypdf
from PIL import Image, ImageChops, ImageDraw
from pypdf import PageObject, PdfReader, PdfWriter
from pypdf.generic import ContentStream, NameObject, DecodedStreamObject, RectangleObject

EXPECTED = {
    "lxgw-sc": ("简体中文批注 ABC 123", "简体中文批注 编辑后 ABC 123"),
    "lxgw-tc": ("繁體中文註解 ABC 123", "繁體中文註解 編輯後 ABC 123"),
    "lxgw-jp": ("日本語カタカナ ABC 123", "日本語カタカナひらがな ABC 123"),
    "lxgw-kr": ("한글 주석 ABC 123", "한글 주석 가나다 ABC 123"),
    "lxgw-extb": ("扩展字 𠀃𠀋 ABC 123", "扩展字 𠀃𠀋𠀎 ABC 123"),
    "zen-jp-composite": ("日本語カタカナ Éé ABC 123", "日本語ひらがな ÉéÀ ABC 123"),
    "gowun-kr-composite": ("한글 주석 Éé ABC 123", "한글 주석 가나다 ÉéÀ ABC 123"),
    "synthetic-cmap12-only": ("扩展字 𠀃𠀋 ABC 123", "扩展字 𠀃𠀋𠀎 ABC 123"),
    "synthetic-no-subsetting": ("한글 주석 Éé ABC 123", "한글 주석 가나다 ÉéÀ ABC 123"),
    "synthetic-alias-glyph": ("한글 AB Éé 123", "한글 AB 가나다 ÉéÀ 123"),
}


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest().upper()


def store(path, value):
    Path(path).write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")


def appearance(reader):
    annotations = [item.get_object() for item in reader.pages[0].get("/Annots", []) if item.get_object().get("/Subtype") == "/FreeText"]
    if len(annotations) != 1:
        raise ValueError("Exactly one FreeText required")
    annotation = annotations[0]
    ap = annotation["/AP"]["/N"].get_object()
    page = PageObject(reader)
    page[NameObject("/Resources")] = ap["/Resources"]
    page[NameObject("/Contents")] = ap
    page[NameObject("/MediaBox")] = ap["/BBox"]
    return annotation, ap, page.extract_text().strip()


def cmap_pairs(data):
    # PDFium's simple CID ToUnicode serializer uses bfchar plus bfrange blocks.
    pairs = {}
    for block in re.findall(rb"beginbfchar(.*?)endbfchar", data, re.S):
        for left, right in re.findall(rb"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>", block):
            pairs[int(left, 16)] = bytes.fromhex(right.decode()).decode("utf-16-be")
    for block in re.findall(rb"beginbfrange(.*?)endbfrange", data, re.S):
        for line in block.splitlines():
            array = re.fullmatch(rb"\s*<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>\s*\[(.*?)\]\s*", line)
            if array:
                left, right, values = array.groups()
                targets = re.findall(rb"<([0-9A-Fa-f]+)>", values)
                if len(targets) != int(right, 16) - int(left, 16) + 1:
                    raise ValueError("Malformed ToUnicode bfrange array")
                for offset, value in enumerate(targets):
                    pairs[int(left, 16) + offset] = bytes.fromhex(value.decode()).decode("utf-16-be")
                continue
            scalar = re.fullmatch(rb"\s*<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>\s*", line)
            if scalar is None:
                if line.strip():
                    raise ValueError("Unsupported ToUnicode bfrange line")
                continue
            left, right, target = scalar.groups()
            width = len(target) // 2
            for code in range(int(left, 16), int(right, 16) + 1):
                value = int(target, 16) + code - int(left, 16)
                pairs[code] = value.to_bytes(width, "big").decode("utf-16-be")
    return pairs


def cid_widths(descendant):
    """Decode both ISO 32000 CID /W forms; /DW defaults to 1000."""
    widths = {}
    values = list(descendant.get("/W", []))
    index = 0
    while index < len(values):
        first = int(values[index])
        following = values[index + 1].get_object() if hasattr(values[index + 1], "get_object") else values[index + 1]
        if isinstance(following, list):
            for offset, value in enumerate(following):
                widths[first + offset] = float(value)
            index += 2
        else:
            last = int(following)
            width = float(values[index + 2])
            if first < 0 or last < first or last > 65535:
                raise ValueError("Invalid CID /W range")
            for cid in range(first, last + 1):
                widths[cid] = width
            index += 3
    return widths, float(descendant.get("/DW", 1000))


def audit(directory, renderer, font_python, report, fail_fast=False):
    def check(identifier, passed, detail):
        report["checks"].append({"id": identifier, "passed": bool(passed), "detail": detail})
    native_path = directory / "result.json"
    native = json.loads(native_path.read_text(encoding="utf-8-sig"))
    result = native["Result"]
    report["nativeReportSHA256"] = sha(native_path)
    expected_native_checks = {identifier + ":frozen-source" for identifier in EXPECTED}
    expected_native_checks.update(identifier + ":" + mode + ":" + suffix
                                  for identifier in EXPECTED for mode in ("auto", "full")
                                  for suffix in ("persisted-id-text", "native-render"))
    actual_native_checks = [item["Id"] for item in result["Checks"]]
    check("production-public-api", native["Probe"] == "font-subset" and native["ExpectationsPassed"] is True
          and result["Status"] == "production-managed-font-subsetting" and len(result["Cases"]) == len(EXPECTED)
          and len(actual_native_checks) == len(set(actual_native_checks)) == 50 and set(actual_native_checks) == expected_native_checks
          and all(item["Passed"] for item in result["Checks"]),
          {"functionalAssemblySHA256": result["FunctionalAssemblySHA256"], "nativeModule": native["NativeModule"]})
    corpus = json.loads((directory / "corpus.json").read_text(encoding="utf-8"))
    check("frozen-corpus-binding", sha(directory / "corpus.json") == result["CorpusSHA256"] and len(corpus["Cases"]) == len(EXPECTED)
          and corpus["PreparationScriptSHA256"] == sha(Path(__file__).parent / "prepare_font_subset_corpus.py")
          and corpus["SourceManifestSHA256"] == sha(directory / "sources" / "source-manifest.json"),
          {"corpusSHA256": sha(directory / "corpus.json")})
    check("fixed-positive-case-matrix", {item["Id"] for item in corpus["Cases"]} == set(EXPECTED)
          and {item["Id"] for item in result["Cases"]} == set(EXPECTED)
          and all((item["Text"], item["UpdatedText"]) == EXPECTED[item["Id"]] for item in corpus["Cases"]),
          {"expectedCases": sorted(EXPECTED)})
    original_manifest = Path(__file__).parent / "Fixtures" / "font-subset-sources.json"
    check("tracked-source-manifest-binding", sha(directory / "sources" / "source-manifest.json") == sha(original_manifest), sha(original_manifest))
    sources = json.loads(original_manifest.read_text(encoding="utf-8-sig"))
    for source in sources["Files"]:
        path = directory / "sources" / source["File"]
        check("source:" + source["File"], sha(path) == source["SHA256"] and path.stat().st_size == source["Bytes"], source)
    expected_hashes = {item["File"]: item["SHA256"] for item in result["Outputs"]}
    expected_output_count = len(EXPECTED) * 4 + 1
    expected_output_names = {identifier + "-" + mode + "-" + stage + ".pdf"
                             for identifier in EXPECTED for mode in ("auto", "full") for stage in ("created", "updated")}
    expected_output_names.add("input.pdf")
    check("output-set-complete", len(result["Outputs"]) == len(expected_hashes) == expected_output_count and set(expected_hashes) == expected_output_names,
          {"expected": expected_output_count, "actual": len(expected_hashes)})
    readers = {}
    for name, digest in expected_hashes.items():
        path = directory / name
        check(name + ":native-output-binding", sha(path) == digest, {"sha256": sha(path), "bytes": path.stat().st_size})
        readers[name] = PdfReader(path, strict=True)
    baseline = readers["input.pdf"]
    baseline_text = baseline.pages[0].extract_text()
    check("input-fixture-text", baseline_text.strip() == "KEEP FONT SUBSET ORIGINAL", baseline_text)
    extract = directory / "extracted-fonts"
    extract.mkdir(exist_ok=True)
    jobs, entries = [], []
    corpus_by_id = {item["Id"]: item for item in corpus["Cases"]}
    for case in result["Cases"]:
        identifier = case["Id"]
        info = corpus_by_id[identifier]
        source_path = directory / case["FontFile"]
        check(identifier + ":source-and-scope", sha(source_path) == case["SourceSHA256"] == info["SHA256"]
              and case["RealFullFont"] == info["RealFullFont"] and len(case["Stages"]) == 4
              and {(stage["Mode"], stage["Stage"]) for stage in case["Stages"]} == {(mode, phase) for mode in ("auto", "full") for phase in ("created", "updated")},
              {"family": info["Family"], "purpose": case["Purpose"], "bytes": source_path.stat().st_size})
        points = sorted(set(map(ord, info["Text"] + info["UpdatedText"])))
        jobs.append({"id": identifier + ":source", "path": str(source_path), "codepoints": points})
        annotation_ids = {}
        for stage in case["Stages"]:
            name = stage["File"]
            reader = readers[name]
            annotation, ap, decoded = appearance(reader)
            expected = info["Text"] if stage["Stage"] == "created" else info["UpdatedText"]
            check(name + ":canonical-and-ToUnicode-text", expected == stage["Text"] == str(annotation.get("/Contents")) == decoded,
                  {"expected": expected, "decoded": decoded})
            check(name + ":geometry-and-page-preservation", len(reader.pages) == 1 and reader.pages[0].extract_text() == baseline_text
                  and tuple(map(float, reader.pages[0].mediabox)) == tuple(map(float, baseline.pages[0].mediabox))
                  and tuple(map(float, annotation["/Rect"])) == (25, 130, 575, 200)
                  and tuple(map(float, ap["/BBox"])) == (0, 0, 550, 70),
                  {"rect": list(annotation["/Rect"]), "bbox": list(ap["/BBox"]), "pageText": reader.pages[0].extract_text()})
            mode = stage["Mode"]
            nm = str(annotation.get("/NM"))
            if stage["Stage"] == "created":
                annotation_ids[mode] = nm
            check(name + ":reopened-stable-annotation-id", nm == "NT.FreeText." + stage["AnnotationId"] and nm == annotation_ids[mode], nm)
            fonts = ap["/Resources"]["/Font"]
            selections = [str(args[0]) for args, op in ContentStream(ap, reader).operations if op == b"Tf"]
            if len(set(selections)) != 1:
                raise ValueError("Exactly one AP font selection required")
            font = fonts[selections[0]].get_object()
            descendant = font["/DescendantFonts"][0].get_object()
            data = descendant["/FontDescriptor"]["/FontFile2"].get_object().get_data()
            font_path = extract / (Path(name).stem + ".ttf")
            font_path.write_bytes(data)
            # Updates must not accumulate superseded FreeText font streams in
            # the reachable page/default resource dictionaries or in the xref.
            embedded_streams = []
            for generation, offsets in reader.xref.items():
                for object_id in offsets:
                    if object_id == 0:
                        continue
                    obj = reader.get_object(pypdf.generic.IndirectObject(object_id, generation, reader))
                    if isinstance(obj, dict) and "/FontFile2" in obj:
                        embedded_streams.append(obj["/FontFile2"].indirect_reference.idnum)
            check(name + ":no-superseded-embedded-font-streams", len(set(embedded_streams)) == 1,
                  {"embeddedStreamReferences": embedded_streams})
            jobs.append({"id": name, "path": str(font_path), "codepoints": sorted(set(map(ord, expected)))})
            to_unicode = cmap_pairs(font["/ToUnicode"].get_object().get_data())
            mapping = descendant.get("/CIDToGIDMap", "/Identity")
            cid_map = None if mapping == "/Identity" else mapping.get_object().get_data()
            advances, default_advance = cid_widths(descendant)
            entries.append({"file": name, "case": identifier, "mode": mode, "stage": stage["Stage"], "text": expected,
                            "fontPath": font_path, "fontBytes": len(data), "fontSHA256": hashlib.sha256(data).hexdigest().upper(),
                            "toUnicode": to_unicode, "cidMap": cid_map, "noSubsetting": case["NoSubsetting"],
                            "advances": advances, "defaultAdvance": default_advance})
    if fail_fast and any(not item["passed"] for item in report["checks"]):
        raise ValueError("Negative control rejected by canonical/structural checks")
    jobs.sort(key=lambda item: Path(item["path"]).stat().st_size)
    job_path = extract / "jobs.json"
    store(job_path, jobs)
    inspected = subprocess.run([str(font_python), str(Path(__file__).parent / "font_subset_inspector.py"), str(job_path)],
                               capture_output=True, text=True, encoding="utf-8", timeout=240)
    if inspected.returncode != 0:
        raise RuntimeError("Independent fontTools oracle failed: " + inspected.stderr[-3000:])
    oracle = json.loads(inspected.stdout)
    store(extract / "fonttools-analysis.json", oracle)
    report["fontTools"] = {"version": oracle["fontToolsVersion"], "python": str(font_python),
                           "inspectorSHA256": sha(Path(__file__).parent / "font_subset_inspector.py"), "analysisSHA256": sha(extract / "fonttools-analysis.json")}
    fonts = {item["id"]: item for item in oracle["fonts"]}
    report["fonts"] = []
    for entry in entries:
        name = entry["file"]
        inspected = fonts[name]
        source = fonts[entry["case"] + ":source"]
        points = inspected["codepoints"]
        preserve = all(value is not None and source["codepoints"][code] is not None
                       and value["outlineSHA256"] == source["codepoints"][code]["outlineSHA256"]
                       and value["metrics"] == source["codepoints"][code]["metrics"] for code, value in points.items())
        check(name + ":all-glyph-outlines-metrics-and-composite-closure", preserve and inspected["sfntChecksumCorrect"] and inspected["allTableChecksumsCorrect"],
              {"codepoints": points, "sfntChecksumCorrect": inspected["sfntChecksumCorrect"], "allTableChecksumsCorrect": inspected["allTableChecksumsCorrect"]})
        if entry["mode"] == "auto" and not entry["noSubsetting"]:
            real_full = corpus_by_id[entry["case"]]["RealFullFont"]
            check(name + ":automatic-size-reduction", (inspected["bytes"] < source["bytes"] and inspected["glyphCount"] < source["glyphCount"])
                  if real_full else inspected["glyphCount"] <= source["glyphCount"],
                  {"sourceBytes": source["bytes"], "embeddedBytes": inspected["bytes"], "sourceGlyphs": source["glyphCount"], "embeddedGlyphs": inspected["glyphCount"]})
        else:
            check(name + ":full-or-permission-preserving-embedding", inspected["sha256"] == source["sha256"],
                  {"sourceSHA256": source["sha256"], "embeddedSHA256": inspected["sha256"], "fsType": inspected["fsType"]})
        mappings = []
        for code, value in entry["toUnicode"].items():
            if len(value) != 1 or ord(value) not in set(map(ord, entry["text"])):
                continue
            cid = code
            mapping = entry["cidMap"]
            gid = cid if mapping is None else int.from_bytes(mapping[cid * 2:cid * 2 + 2], "big")
            expected_gid = points[str(ord(value))]["gid"]
            mappings.append({"unicode": ord(value), "cid": cid, "gid": gid, "expectedGid": expected_gid})
        check(name + ":CIDToGID-ToUnicode-consistency", {item["unicode"] for item in mappings} == set(map(ord, entry["text"]))
              and all(item["gid"] == item["expectedGid"] and item["gid"] != 0 for item in mappings), mappings)
        advances = []
        for item in mappings:
            actual = entry["advances"].get(item["cid"], entry["defaultAdvance"])
            expected = points[str(item["unicode"])]["metrics"][0] * 1000.0 / inspected["unitsPerEm"]
            advances.append({"unicode": item["unicode"], "cid": item["cid"], "pdfWidth": actual, "expectedWidth": expected,
                             "absoluteError": abs(actual - expected)})
        check(name + ":CID-widths-match-source-horizontal-advances", bool(advances)
              and all(item["absoluteError"] <= 1.0 for item in advances),
              {"unitsPerEm": inspected["unitsPerEm"], "toleranceIn1000EmUnits": 1.0, "widths": advances})
        report["fonts"].append({"pdf": name, "sourceBytes": source["bytes"], "embeddedBytes": inspected["bytes"],
                                "sourceGlyphs": source["glyphCount"], "embeddedGlyphs": inspected["glyphCount"],
                                "fontSHA256": inspected["sha256"], "mode": entry["mode"], "noSubsetting": entry["noSubsetting"]})
    if fail_fast and any(not item["passed"] for item in report["checks"]):
        raise ValueError("Negative control rejected by glyph/font checks")
    report["renderer"] = {"path": str(renderer), "sha256": sha(renderer)}
    images = {}
    report["rasters"] = []
    for name in expected_hashes:
        prefix = directory / (Path(name).stem + "-poppler")
        png = prefix.with_suffix(".png")
        png.unlink(missing_ok=True)
        process = subprocess.run([str(renderer), "-png", "-r", "96", "-f", "1", "-singlefile", str(directory / name), str(prefix)],
                                 capture_output=True, text=True, timeout=60)
        diagnostics = [line for line in process.stderr.splitlines() if line.strip()]
        allowed = {"Syntax Error: No display font for 'Symbol'", "Syntax Error: No display font for 'ArialUnicode'"}
        check(name + ":independent-render", process.returncode == 0 and png.is_file() and not [line for line in diagnostics if line not in allowed],
              {"exit": process.returncode, "diagnostics": diagnostics})
        with Image.open(png) as img:
            images[name] = img.convert("RGB")
        report["rasters"].append({"pdf": name, "png": png.name, "sha256": sha(png)})
    for case in result["Cases"]:
        for stage in ("created", "updated"):
            auto_name = case["Id"] + "-auto-" + stage + ".pdf"
            full_name = case["Id"] + "-full-" + stage + ".pdf"
            auto = images[auto_name]
            full = images[full_name]
            check(auto_name + ":auto-equals-full-pixels", auto.size == full.size and ImageChops.difference(auto, full).getbbox() is None,
                  {"auto": auto_name, "full": full_name, "size": auto.size})
            difference = ImageChops.difference(images["input.pdf"], auto)
            outside = difference.copy()
            outside.paste((0, 0, 0), (31, 130, 770, 230))
            check(auto_name + ":retained-pixels-exact-and-CJK-visible", outside.getbbox() is None and difference.getbbox() is not None,
                  {"differenceBounds": difference.getbbox(), "excludedAnnotationPixels": [31, 130, 770, 230]})
    sheet = Image.new("RGB", (1600, len(EXPECTED) * 430), "#dddddd")
    draw = ImageDraw.Draw(sheet)
    for index, case in enumerate(result["Cases"]):
        for column, mode in enumerate(("auto", "full")):
            name = case["Id"] + "-" + mode + "-updated.pdf"
            x, y = column * 800, index * 430
            draw.text((x + 8, y + 8), name, fill="black")
            sheet.paste(images[name], (x, y + 30))
    sheet.save(directory / "font-subset-contact-sheet.png")
    report["contactSheet"] = {"file": "font-subset-contact-sheet.png", "sha256": sha(directory / "font-subset-contact-sheet.png")}


def controls(directory, renderer, font_python, report):
    root = directory / "self-tests" / uuid.uuid4().hex
    root.mkdir(parents=True)
    results = []
    def copy_immutable(source, target):
        try:
            os.link(source, target)
        except OSError:
            # Windows workspace sandbox can prohibit hard links; ordinary
            # copies preserve identical validation semantics.
            shutil.copyfile(source, target)
    for name in ("missing-output", "corrupted-font", "wrong-cmap", "broken-composite", "wrong-geometry"):
        case = root / name
        case.mkdir()
        # Hard links keep the immutable 35 MB sources cheap; only mutate PDFs.
        for filename in ("corpus.json", "result.json"):
            shutil.copyfile(directory / filename, case / filename)
        for pdf in directory.glob("*.pdf"):
            copy_immutable(pdf, case / pdf.name)
        shutil.copytree(directory / "sources", case / "sources", copy_function=copy_immutable)
        store(case / "independent-audit.json", {"passed": True, "status": "stale-pass"})
        selected = "zen-jp-composite-auto-created.pdf"
        target = case / selected
        if name == "missing-output":
            target.unlink()
        else:
            writer = PdfWriter(clone_from=target)
            annotation, ap, _ = appearance(writer)
            if name == "wrong-geometry":
                annotation[NameObject("/Rect")] = RectangleObject((45, 130, 595, 200))
            else:
                font = next(iter(ap["/Resources"]["/Font"].values())).get_object()
                if name == "wrong-cmap":
                    stream = font["/ToUnicode"].get_object()
                    stream.set_data(stream.get_data().replace(b"<0041>", b"<0058>"))
                else:
                    descendant = font["/DescendantFonts"][0].get_object()
                    stream = descendant["/FontDescriptor"]["/FontFile2"].get_object()
                    data = bytearray(stream.get_data())
                    if name == "corrupted-font":
                        data[0:4] = b"BAD!"
                    else:
                        # A valid, checksum-correct sfnt with one real component
                        # redirected to .notdef must fail the outline oracle.
                        before = case / "composite-before.ttf"
                        after = case / "composite-after.ttf"
                        before.write_bytes(data)
                        mutation = subprocess.run([str(font_python), str(Path(__file__).parent / "font_subset_inspector.py"),
                                                   "--mutate-composite", str(before), str(after)], capture_output=True, text=True, timeout=30)
                        if mutation.returncode != 0:
                            raise RuntimeError("Could not create composite negative control: " + mutation.stderr)
                        data = bytearray(after.read_bytes())
                    stream.set_data(bytes(data))
            temporary = case / "mutated.pdf"
            with temporary.open("wb") as stream:
                writer.write(stream)
            writer.close()
            temporary.replace(target)
            native = json.loads((case / "result.json").read_text(encoding="utf-8-sig"))
            for item in native["Result"]["Outputs"]:
                if item["File"] == selected:
                    item["SHA256"] = sha(target)
            store(case / "result.json", native)
        process = subprocess.run([sys.executable, str(Path(__file__).resolve()), "--run", str(case), "--renderer", str(renderer),
                                  "--font-python", str(font_python), "--fail-fast"], capture_output=True, text=True, timeout=300)
        failed = json.loads((case / "independent-audit.json").read_text(encoding="utf-8"))
        rejected = [item["id"] for item in failed.get("checks", []) if not item["passed"]]
        expected = ("FileNotFoundError" in failed.get("error", "") if name == "missing-output" else
                    "fontTools oracle failed" in failed.get("error", "") if name == "corrupted-font" else
                    selected + ":canonical-and-ToUnicode-text" in rejected if name == "wrong-cmap" else
                    selected + ":all-glyph-outlines-metrics-and-composite-closure" in rejected or "fontTools oracle failed" in failed.get("error", "") if name == "broken-composite" else
                    selected + ":geometry-and-page-preservation" in rejected)
        results.append({"id": name, "passed": process.returncode != 0 and failed.get("passed") is False and failed.get("status") != "stale-pass" and expected,
                        "expectedRejectionDetected": expected, "exit": process.returncode, "rejectedChecks": rejected,
                        "error": failed.get("error"), "report": str((case / "independent-audit.json").relative_to(directory)),
                        "reportSHA256": sha(case / "independent-audit.json")})
    report["negativeControls"] = results
    report["checks"].append({"id": "oracle-negative-controls-and-stale-pass-protection", "passed": all(item["passed"] for item in results), "detail": results})


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run", required=True)
    parser.add_argument("--renderer", required=True)
    parser.add_argument("--font-python", required=True)
    parser.add_argument("--self-test", action="store_true")
    parser.add_argument("--fail-fast", action="store_true", help=argparse.SUPPRESS)
    args = parser.parse_args()
    supplied = Path(args.run).resolve()
    directory = supplied / "font-subset" if (supplied / "font-subset").is_dir() else supplied
    directory.mkdir(parents=True, exist_ok=True)
    target = directory / "independent-audit.json"
    report = {"status": "verification-incomplete", "passed": False, "verifiedUtc": dt.datetime.now(dt.timezone.utc).isoformat(),
              "scriptSHA256": sha(__file__), "inspectorScriptSHA256": sha(Path(__file__).parent / "font_subset_inspector.py"),
              "preparationScriptSHA256": sha(Path(__file__).parent / "prepare_font_subset_corpus.py"),
              "pypdfVersion": pypdf.__version__, "checks": [],
              "limitations": ["Three real full static TrueType families; synthetic edge fixtures do not establish additional real-font compatibility.",
                              "Single Windows x64 run; timings/allocation observations are neither peaks nor an SLA; Linux/Kylin/official/UI comparisons remain open."]}
    store(target, report)
    try:
        renderer = Path(args.renderer).resolve(strict=True)
        font_python = Path(args.font_python).resolve(strict=True)
        audit(directory, renderer, font_python, report, args.fail_fast)
        if args.self_test:
            controls(directory, renderer, font_python, report)
        report["passed"] = bool(report["checks"]) and all(item["passed"] for item in report["checks"])
        report["status"] = "font-subsetting-independently-confirmed" if report["passed"] else "verification-failed"
    except Exception as error:
        report["error"] = f"{type(error).__name__}: {error}"
        report["status"] = "verification-error"
    store(target, report)
    print(json.dumps({"report": str(target), "passed": report["passed"], "checks": len(report["checks"]),
                      "failed": [item["id"] for item in report["checks"] if not item["passed"]], "error": report.get("error")}, ensure_ascii=True))
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
