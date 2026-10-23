"""Independent, operation-specific audit of pinned published PDF revisions.

This reads positive artifacts only. Native PDFium observations are compared with
pypdf structures and sampled Poppler pixels; successful reading or a byte-copy
save is never promoted into a general writer-compatibility claim.
"""
from __future__ import annotations
import argparse
import copy
import datetime as dt
import hashlib
import io
import json
import logging
from pathlib import Path
import re
import subprocess
import sys
import uuid

import pypdf
from PIL import Image, ImageChops, ImageDraw
from pypdf import PdfReader, PdfWriter
from pypdf.generic import ContentStream, DecodedStreamObject, IndirectObject, NameObject
from forms_verifier_common import appearance_text, appearances_font_usage, canonical_fields, normal_appearance


OPERATIONS = {"open-edit", "snapshot-export", "save-unmodified", "freetext", "sanitize", "redact-tiny-region"}
ANNOTATION_TEXT = "中文批注 ABC 123"


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest().upper()


def digest(value):
    return hashlib.sha256(value).hexdigest().upper()


def read(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def store(path, value):
    Path(path).write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")


def check(report, identifier, passed, detail):
    report["checks"].append({"id": identifier, "passed": bool(passed), "detail": detail})


def guard(report):
    if any(not value["passed"] for value in report["checks"]):
        raise ValueError("Independent provenance, operation or PDF semantics rejected the candidate")


def local(directory, relative):
    path = (directory / relative).resolve()
    if not path.is_relative_to(directory.resolve()):
        raise ValueError("Evidence path escapes its run directory")
    return path


def plain(value, depth=0):
    if depth > 5:
        return "[depth-limit]"
    value = value.get_object() if hasattr(value, "get_object") else value
    if isinstance(value, bytes):
        return {"bytes": len(value), "sha256": digest(value)}
    if isinstance(value, (str, int, float, bool)) or value is None:
        return value
    if isinstance(value, (tuple, list)):
        return [plain(item, depth + 1) for item in value]
    if isinstance(value, dict):
        return {str(key): plain(item, depth + 1) for key, item in value.items()
                if key not in ("/Parent", "/P", "/Kids", "/AP")}
    return str(value)


def all_objects(reader):
    seen = set()
    for generation, numbers in reader.xref.items():
        if generation == 65535:
            continue
        for number in numbers:
            if number and (number, generation) not in seen:
                seen.add((number, generation))
                yield reader.get_object(IndirectObject(number, generation, reader))
    for number in reader.xref_objStm:
        if (number, 0) not in seen:
            yield reader.get_object(IndirectObject(number, 0, reader))


def orphan_streams(reader):
    reachable, visited_direct = set(), set()
    def visit(value):
        if isinstance(value, IndirectObject):
            identity = value.idnum, value.generation
            if identity in reachable:
                return
            reachable.add(identity)
            visit(value.get_object())
        elif isinstance(value, (dict, list, tuple)):
            if id(value) in visited_direct:
                return
            visited_direct.add(id(value))
            for item in (value.values() if isinstance(value, dict) else value):
                visit(item)
    visit(reader.trailer)
    orphaned = []
    for value in all_objects(reader):
        if hasattr(value, "get_data") and value.get("/Type") not in ("/XRef", "/ObjStm"):
            identity = value.indirect_reference.idnum, value.indirect_reference.generation
            if identity not in reachable:
                orphaned.append({"reference": list(identity), "decodedBytes": len(value.get_data()), "sha256": digest(value.get_data())})
    return orphaned


def normalized_text(value):
    return re.sub(r"\s+", "", value or "")


def page_body(page):
    content = page.get_contents()
    return digest(content.get_data() if content is not None else b"")


def annotations(page):
    result = []
    for raw in page.get("/Annots", []):
        annot = raw.get_object()
        entry = {key: plain(annot.get(key)) for key in ("/Subtype", "/NM", "/Contents", "/Rect", "/T", "/FT", "/V", "/AS", "/F", "/A") if key in annot}
        try:
            entry["appearanceSHA256"] = digest(normal_appearance(annot).get_data())
        except (KeyError, TypeError, ValueError):
            pass
        result.append(entry)
    return result


def appearance_snapshot(widget):
    """An unfilled real checkbox may expose /AP states without a selected /AS."""
    normal = widget.get("/AP", {}).get("/N")
    if normal is None:
        return None
    normal = normal.get_object()
    if hasattr(normal, "get_data"):
        return {"streamSHA256": digest(normal.get_data())}
    return {str(key): digest(value.get_object().get_data()) for key, value in sorted(normal.items())}


def field_snapshot(reader):
    acro = reader.trailer["/Root"].get("/AcroForm")
    if acro is None or not acro.get_object().get("/Fields"):
        return {}
    _, graph = canonical_fields(reader)
    return {name: {"field": {key: plain(item["field"].get(key)) for key in ("/FT", "/Ff", "/V", "/DV", "/Opt", "/I", "/MaxLen") if key in item["field"]},
                   "widgets": [{"page": next((index for index, page in enumerate(reader.pages)
                                  if any(raw.get_object() is widget for raw in page.get("/Annots", []))), None),
                                "properties": {key: plain(widget.get(key)) for key in ("/Rect", "/V", "/AS", "/F") if key in widget},
                                "appearance": appearance_snapshot(widget)}
                               for widget in item["widgets"]]}
            for name, item in sorted(graph.items())}


def outline_snapshot(reader):
    def convert(values):
        result = []
        for value in values:
            if isinstance(value, list):
                result.append(convert(value))
            else:
                try:
                    page = reader.get_destination_page_number(value)
                except Exception:
                    page = None
                result.append({"title": str(value.get("/Title", "")), "page": page})
        return result
    return convert(reader.outline)


def attachment_snapshot(reader):
    return {name: [{"bytes": len(data), "sha256": digest(data)} for data in contents]
            for name, contents in sorted(reader.attachments.items())}


def xfa_snapshot(reader):
    root = reader.trailer["/Root"]
    acro = root["/AcroForm"].get_object() if "/AcroForm" in root else {}
    value = acro.get("/XFA")
    if value is None:
        return None
    value = value.get_object()
    def packet(item):
        item = item.get_object()
        if hasattr(item, "get_data"):
            data = item.get_data()
            return {"bytes": len(data), "sha256": digest(data)}
        return str(item)
    return [packet(item) for item in value] if isinstance(value, list) else packet(value)


def geometry(reader):
    return [{"media": list(map(float, page.mediabox)), "crop": list(map(float, page.cropbox)),
             "rotate": int(page.get("/Rotate", 0)), "userUnit": float(page.get("/UserUnit", 1))}
            for page in reader.pages]


def structural_inventory(reader, sample_indices):
    """Existence of a feature is distinct from a particular operation hitting it."""
    fonts, images, transparency, color_spaces = [], [], [], []
    for value in all_objects(reader):
        if not isinstance(value, dict):
            continue
        if value.get("/Type") == "/Font":
            descriptor = value.get("/FontDescriptor")
            embedded = {}
            if descriptor is not None:
                descriptor = descriptor.get_object()
                for key in ("/FontFile", "/FontFile2", "/FontFile3"):
                    if key in descriptor:
                        stream = descriptor[key].get_object()
                        data = stream.get_data()
                        embedded[key] = {"bytes": len(data), "sha256": digest(data), "subtype": str(stream.get("/Subtype", ""))}
            fonts.append({"subtype": str(value.get("/Subtype", "")), "baseFont": str(value.get("/BaseFont", "")),
                          "encoding": str(value.get("/Encoding", "")) if not isinstance(value.get("/Encoding"), dict) else "dictionary",
                          "toUnicode": "/ToUnicode" in value, "embedded": embedded})
        if value.get("/Subtype") == "/Image":
            filters = value.get("/Filter", [])
            filters = [str(item) for item in filters] if isinstance(filters, list) else [str(filters)]
            images.append({"width": int(value.get("/Width", 0)), "height": int(value.get("/Height", 0)),
                           "filters": filters, "bitsPerComponent": int(value.get("/BitsPerComponent", 0)),
                           "colorSpace": plain(value.get("/ColorSpace")), "softMask": "/SMask" in value,
                           "mask": "/Mask" in value})
        if any(key in value for key in ("/ca", "/CA", "/SMask", "/BM")) or value.get("/S") == "/Transparency":
            transparency.append({key: plain(value[key]) for key in ("/ca", "/CA", "/SMask", "/BM", "/S") if key in value})
        if "/ColorSpace" in value:
            space = plain(value["/ColorSpace"])
            if space not in color_spaces:
                color_spaces.append(space)
    counts, sampled, marked_dictionaries, text_modes, resource_spaces = {}, [], [], set(), []
    for index in sample_indices:
        page = reader.pages[index]
        visited = set()
        def visit(stream, resources):
            if "/ColorSpace" in resources:
                for alias, color_space in resources["/ColorSpace"].get_object().items():
                    item = {"pageIndex": index, "alias": str(alias), "definition": plain(color_space)}
                    if item not in resource_spaces:
                        resource_spaces.append(item)
            for args, operator in ContentStream(stream, reader).operations:
                name = operator.decode("ascii", errors="replace")
                counts[name] = counts.get(name, 0) + 1
                if operator in (b"BDC", b"DP") and len(args) == 2 and isinstance(args[1], dict):
                    example = {"pageIndex": index, "operator": name, "tag": str(args[0]), "dictionary": plain(args[1]),
                               "interpretation": "Valid inline marked-content property dictionary; Dictionary in PDFsharp diagnostics is a parser token classification, not an invalid PDF painting operator."}
                    if len(marked_dictionaries) < 8:
                        marked_dictionaries.append(example)
                if operator == b"Tr":
                    text_modes.add(int(args[0]))
                if operator == b"Do":
                    child = resources.get("/XObject", {}).get(args[0])
                    if child is not None:
                        child = child.get_object()
                        identity = id(child)
                        if child.get("/Subtype") == "/Form" and identity not in visited:
                            visited.add(identity)
                            visit(child, child.get("/Resources", resources))
        if page.get_contents() is not None:
            visit(page.get_contents(), page.get("/Resources", {}))
        text = page.extract_text() or ""
        sampled.append({"index": index, "textLength": len(text), "textSHA256": digest(text.encode("utf-8")),
                        "cjkScalars": sum(0x2e80 <= ord(c) <= 0x9fff or 0x20000 <= ord(c) <= 0x323af for c in text),
                        "nativeTextCorrespondence": "Whitespace, reading order and unmapped encodings can differ across engines; no exact-text parity claim."})
    fields = field_snapshot(reader)
    return {"pageCount": len(reader.pages), "fonts": fonts, "images": images, "transparencyDictionaries": transparency,
            "sampledPages": sampled, "sampledPageAndFormOperators": counts, "markedContentDictionaryExamples": marked_dictionaries,
            "sampledTextRenderingModes": sorted(text_modes), "declaredColorSpaces": color_spaces,
            "sampledResourceColorSpaces": resource_spaces,
            "fieldCount": len(fields), "fieldTypes": sorted({str(value["field"].get("/FT")) for value in fields.values()}),
            "getFieldsNodeCountIncludingNonterminals": len(reader.get_fields() or {}),
            "widgetCount": sum(annot.get("/Subtype") == "/Widget" for page in reader.pages for annot in annotations(page)),
            "outlineTopLevelEntries": len(reader.outline), "attachmentNames": sorted(reader.attachments),
            "hasXfa": "/XFA" in (reader.trailer["/Root"]["/AcroForm"].get_object() if "/AcroForm" in reader.trailer["/Root"] else {}),
            "xfaPackets": xfa_snapshot(reader), "hasStructureTree": "/StructTreeRoot" in reader.trailer["/Root"],
            "featureScope": "Font/image/transparency inventory scans current xref dictionaries; operator and Unicode inventory covers only the named sampled pages and their invoked Forms. Presence alone never identifies a refusal cause."}


def dimensions(page):
    width, height = float(page.cropbox.width), float(page.cropbox.height)
    return (height, width) if int(page.get("/Rotate", 0)) % 180 else (width, height)


def native_pages(directory, pages, reader, report, prefix, expected_indices=None):
    expected_indices = expected_indices if expected_indices is not None else ([0] if len(reader.pages) == 1 else [0, len(reader.pages) // 2])
    check(report, prefix + ":native-sample-matrix", [item["Index"] for item in pages] == expected_indices,
          [item["Index"] for item in pages])
    for item in pages:
        index = item["Index"]
        text_path, image_path = local(directory, item["TextFile"]), local(directory, item["RenderFile"])
        text = text_path.read_bytes().decode("utf-8")
        # The native exporter writes RGB against an opaque white background.
        with Image.open(image_path) as image:
            rgb = image.convert("RGB")
            pixel_hash = digest(rgb.convert("RGBA").tobytes("raw", "BGRA"))
            bbox = ImageChops.difference(rgb, Image.new("RGB", rgb.size, "white")).getbbox()
        width, height = dimensions(reader.pages[index])
        check(report, prefix + f":page-{index}:native-evidence", sha(text_path) == item["TextSHA256"] and sha(image_path) == item["RenderSHA256"]
              and len(text.encode("utf-16-le")) // 2 == item["TextLength"] and 0 <= item["Characters"] <= item["TextLength"]
              and rgb.size == (item["RenderWidth"], item["RenderHeight"]) and pixel_hash == item["PixelSHA256"]
              and abs(width - item["Width"]) < .02 and abs(height - item["Height"]) < .02,
              {"textFile": item["TextFile"], "textSHA256": sha(text_path), "renderFile": item["RenderFile"], "renderSHA256": sha(image_path),
               "pixelSHA256": pixel_hash, "size": list(rgb.size), "nonWhiteBounds": bbox, "independentPageSize": [width, height]})


def render(path, index, directory, renderer, report, label):
    target = directory / f"{label}-page-{index + 1}-poppler.png"
    target.unlink(missing_ok=True)
    process = subprocess.run([str(renderer), "-png", "-scale-to", "800", "-f", str(index + 1), "-l", str(index + 1),
                              "-singlefile", str(path), str(target.with_suffix(""))], capture_output=True, text=True, timeout=180)
    log = target.with_suffix(".stderr.txt")
    log.write_text(process.stderr, encoding="utf-8")
    check(report, label + f":page-{index}:independent-render", process.returncode == 0 and target.is_file(),
          {"exit": process.returncode, "diagnostics": process.stderr.splitlines(), "diagnosticsAreCompatibilityObservations": True})
    if not target.is_file():
        raise ValueError("Poppler failed to produce " + label)
    with Image.open(target) as image:
        image = image.convert("RGB")
    report["rasters"].append({"pdf": str(path.relative_to(directory)), "pdfSHA256": sha(path), "pageIndex": index,
                              "png": target.name, "pngSHA256": sha(target), "stderr": log.name, "stderrSHA256": sha(log), "size": list(image.size)})
    return image


def mask_rectangle(mask, page, rectangle, padding=3):
    if int(page.get("/Rotate", 0)) % 360:
        raise ValueError("The bounded pixel exclusion oracle requires an unrotated sample page")
    left, bottom, right, top = map(float, rectangle)
    x0, y0 = float(page.cropbox.left), float(page.cropbox.bottom)
    sx, sy = mask.width / float(page.cropbox.width), mask.height / float(page.cropbox.height)
    box = (int((left - x0) * sx) - padding, int((float(page.cropbox.top) - top) * sy) - padding,
           int((right - x0) * sx) + padding + 1, int((float(page.cropbox.top) - bottom) * sy) + padding + 1)
    ImageDraw.Draw(mask).rectangle(box, fill=0)


def compare_pixels(before, after, page, rectangles, report, identifier):
    check(report, identifier + ":same-render-size", before.size == after.size, {"before": list(before.size), "after": list(after.size)})
    if before.size != after.size:
        return
    mask = Image.new("L", before.size, 255)
    for rectangle in rectangles:
        mask_rectangle(mask, page, rectangle)
    difference = ImageChops.difference(before, after)
    retained = Image.composite(difference, Image.new("RGB", before.size), mask)
    check(report, identifier + ":retained-pixels-identical", retained.getbbox() is None,
          {"changedPixelBoundsOutsideExclusions": retained.getbbox(), "excludedPdfRectangles": rectangles,
           "checkedPixels": sum(value != 0 for value in mask.tobytes()), "allPixels": mask.width * mask.height})


def audit(directory, renderer, report, mutation=None, fail_fast=False):
    config_path = Path(__file__).parent / "Fixtures/real-corpus-sources.json"
    config = read(config_path)
    sources_path = directory / "sources/manifest.json"
    sources = read(sources_path)
    native_path = directory / "result.json"
    native = read(native_path)
    if mutation:
        mutate(mutation, sources, native, directory, report)
    report.update({"nativeReportSHA256": sha(native_path), "sourceManifestSHA256": sha(sources_path),
                   "pinnedConfigurationSHA256": sha(config_path), "nativeAssemblySHA256": native["ManagedAssemblySHA256"],
                   "nativeModule": native["NativeModule"]})
    fixed = {value["Id"]: value for value in config["Sources"]}
    run_sources = {value["Id"]: value for value in sources["Sources"]}
    cases = {value["Id"]: value for value in native["Result"]["Cases"]}
    check(report, "fixed-published-revision-matrix", len(fixed) >= 6 and len(run_sources) == len(sources["Sources"]) == len(fixed)
          and len(cases) == len(native["Result"]["Cases"]) == len(fixed) and set(fixed) == set(run_sources) == set(cases),
          {"pinned": sorted(fixed), "manifest": sorted(run_sources), "native": sorted(cases)})
    native_checks = native["Result"]["Checks"]
    expected_native_checks = set()
    for source_id, source in fixed.items():
        expected_native_checks.update(source_id + ":" + suffix for suffix in ("source-hash", "read", "snapshot-copy", "save-unmodified"))
        for name, expectation in source["Expected"].items():
            expected_native_checks.update(source_id + ":" + name + ":" + suffix for suffix in ("locked-expectation", "no-unexpected-failure"))
            if expectation["Outcome"] == "refused":
                expected_native_checks.add(source_id + ":" + name + ":atomic-refusal")
            elif name not in ("open-edit", "snapshot-export", "save-unmodified"):
                expected_native_checks.add(source_id + ":" + name + ":commit-and-reopen")
    check(report, "native-public-api-checks", native["Probe"] == "real-corpus" and native["ExpectationsPassed"] is True
          and bool(native_checks) and len(native_checks) == len({value["Id"] for value in native_checks})
          and {value["Id"] for value in native_checks} == expected_native_checks
          and all(value["Passed"] for value in native_checks), {"checks": len(native_checks), "status": native["Result"]["Status"]})
    check(report, "source-manifest-native-binding", native["Result"]["ManifestSHA256"] == sha(sources_path) == sha(config_path), native["Result"]["ManifestSHA256"])
    preparation = read(directory / "corpus.json")
    check(report, "source-preparation-script-and-file-bindings", preparation["Status"] == "pinned-published-source-corpus-prepared"
          and preparation["SourceManifestSHA256"] == sha(config_path)
          and preparation["PreparationScriptSHA256"] == sha(Path(__file__).with_name("prepare_real_corpus.py"))
          and preparation["SourceCount"] == len(fixed) and preparation["Sources"] == config["Sources"]
          and all(sha(local(directory, value["File"])) == value["SHA256"] and local(directory, value["File"]).stat().st_size == value["Bytes"] for value in preparation["Files"]),
          {"corpusSHA256": sha(directory / "corpus.json"), "preparationScriptSHA256": preparation["PreparationScriptSHA256"],
           "sourceCount": preparation["SourceCount"], "totalPages": preparation["TotalPages"], "totalPdfBytes": preparation["TotalPdfBytes"]})
    check(report, "frozen-license-evidence-matrix", sources.get("EvidenceFiles") == config.get("EvidenceFiles")
          and len(config.get("EvidenceFiles", [])) >= 5, sources.get("EvidenceFiles"))
    for evidence in config.get("EvidenceFiles", []):
        evidence_path = directory / "sources" / evidence["File"]
        check(report, "license-evidence:" + evidence["File"], sha(evidence_path) == evidence["SHA256"]
              and evidence_path.stat().st_size == evidence["Bytes"], {"file": "sources/" + evidence["File"], "sha256": sha(evidence_path), "url": evidence["Url"]})
    check(report, "no-read-to-edit-or-production-overclaim", not native["Result"].get("ProductEditingReady", False)
          and not native["Result"].get("GeneralWriteCompatibility", False),
          "Results below are per concrete operation and exact revision. No-op stream save does not rewrite PDF structure.")
    guard(report)
    report["cases"] = []
    rendered_inputs = {}
    for identifier, fixed_source in fixed.items():
        source, case = run_sources[identifier], cases[identifier]
        # Published URLs, licensing qualifications and expected refusals are pinned
        # outside the mutable run; forging matching local hashes is insufficient.
        keys = ("Id", "File", "SHA256", "Bytes", "Pages", "Title", "Tags", "RealPublishedSource", "LandingUrl", "PdfUrl", "License", "Expected", "CompatibilityScope")
        check(report, identifier + ":published-provenance-and-capability-pins", all(source.get(key) == fixed_source.get(key) for key in keys),
              {"landingUrl": source.get("LandingUrl"), "pdfUrl": source.get("PdfUrl"), "license": source.get("License"), "pinnedKeys": keys})
        path = local(directory, case["Input"]["File"])
        check(report, identifier + ":input-bytes-and-source-identity", case["Input"]["File"] == "sources/" + source["File"]
              and sha(path) == source["SHA256"] == case["Input"]["SHA256"] and path.stat().st_size == source["Bytes"] == case["Input"]["Bytes"],
              {"file": case["Input"]["File"], "bytes": path.stat().st_size, "sha256": sha(path)})
        guard(report)
        reader = PdfReader(path, strict=False)
        sample_indices = [0] if len(reader.pages) == 1 else [0, len(reader.pages) // 2]
        check(report, identifier + ":independent-page-count", len(reader.pages) == source["Pages"] == case["Read"]["PageCount"]
              and case["Read"]["Outcome"] == "accepted", len(reader.pages))
        native_pages(directory, case["Read"]["Pages"], reader, report, identifier + ":read")
        inventory = structural_inventory(reader, sample_indices)
        operations = {value["Name"]: value for value in case["Operations"]}
        expected_operations = OPERATIONS | ({"redact-whole-first-page"} if identifier == "gnu-make" else set())
        check(report, identifier + ":separate-operation-matrix", len(operations) == len(case["Operations"]) == len(expected_operations)
              and set(operations) == expected_operations and set(source["Expected"]) == expected_operations, sorted(operations))
        evidence = {"id": identifier, "source": {"file": case["Input"]["File"], "sha256": sha(path)},
                    "structure": inventory, "operations": [], "metadataObservations": []}
        report["cases"].append(evidence)
        guard(report)
        input_geometry, bodies, fields = geometry(reader), [page_body(page) for page in reader.pages], field_snapshot(reader)
        outlines, attachments, xfa = outline_snapshot(reader), attachment_snapshot(reader), xfa_snapshot(reader)
        for name in sorted(expected_operations):
            operation, expected = operations[name], fixed_source["Expected"][name]
            outcome = operation["Outcome"]
            refusal = outcome == "refused"
            matches = outcome == expected["Outcome"] and operation["ExpectationMatched"] is True and outcome in ("accepted", "refused")
            if refusal:
                matches = matches and operation["ExceptionType"] == expected["ExceptionType"] == "NotSupportedException" \
                          and bool(expected["ReasonContains"]) and expected["ReasonContains"] in operation["Reason"]
            else:
                matches = matches and operation["ExceptionType"] is None and operation["Reason"] is None
            check(report, identifier + ":" + name + ":locked-outcome-and-specific-reason", matches,
                  {"outcome": outcome, "exceptionType": operation["ExceptionType"], "reason": operation["Reason"], "expected": expected})
            before, after = operation["Before"], operation["After"]
            check(report, identifier + ":" + name + ":starts-from-independent-original", before == {"SHA256": source["SHA256"], "Revision": 0, "IsDirty": False}, before)
            evidence["operations"].append({"name": name, "outcome": outcome, "exceptionType": operation["ExceptionType"], "reason": operation["Reason"],
                                           "actualRefusalCause": operation["Reason"] if refusal else None,
                                           "featurePresenceDoesNotEstablishCausation": True,
                                           "scope": "No structural rewrite" if name in ("open-edit", "snapshot-export", "save-unmodified") else "Exact operation and source revision only"})
            if refusal:
                check(report, identifier + ":" + name + ":atomic-refusal-and-no-output", after == before and operation["Output"] is None
                      and not (directory / identifier / (name + ".pdf")).exists(), {"before": before, "after": after})
                continue
            if name in ("open-edit", "snapshot-export", "save-unmodified"):
                check(report, identifier + ":" + name + ":no-op-state", before == after and operation["Output"] is None, after)
                if name != "open-edit":
                    detail = operation["Details"]
                    check(report, identifier + ":" + name + ":actual-copy-bytes", detail["ByteIdentical"] is True and detail["SHA256"] == source["SHA256"]
                          and detail["Bytes"] == source["Bytes"], detail)
                continue
            output = operation["Output"]
            output_path = local(directory, output["File"])
            check(report, identifier + ":" + name + ":output-hash-and-single-commit", sha(output_path) == output["SHA256"] == after["SHA256"]
                  and output_path.stat().st_size == output["Bytes"] and after["Revision"] == 1 and after["IsDirty"] is True,
                  {"file": output["File"], "sha256": sha(output_path), "after": after})
            out = PdfReader(output_path, strict=False)
            check(report, identifier + ":" + name + ":retained-page-geometry", len(out.pages) == len(reader.pages) == output["PageCount"]
                  and geometry(out) == input_geometry, geometry(out))
            output_samples = output.get("Pages", [])
            # Sanitize/redact intentionally only record the first page natively.
            expected_samples = sample_indices if name == "freetext" else ([0, len(out.pages) // 2, len(out.pages) - 1] if name == "redact-whole-first-page" else [0])
            check(report, identifier + ":" + name + ":native-output-samples", [item["Index"] for item in output_samples] == expected_samples,
                  [item["Index"] for item in output_samples])
            native_pages(directory, output_samples, out, report, identifier + ":" + name + ":native-output", expected_samples)
            if name == "freetext":
                check(report, identifier + ":freetext:all-page-body-streams-retained", [page_body(page) for page in out.pages] == bodies,
                      "Decoded original page painting streams are identical on every page; resources and representative pixels are checked separately.")
                check(report, identifier + ":freetext:canonical-fields-and-appearances-retained", field_snapshot(out) == fields,
                      {"fieldCount": len(fields), "outputFieldCount": len(field_snapshot(out))})
                check(report, identifier + ":freetext:hybrid-XFA-packets-retained", xfa_snapshot(out) == xfa,
                      {"inputXfaPackets": xfa, "outputXfaPackets": xfa_snapshot(out), "scope": "Retention only; no XFA filling, scripting or dynamic-layout support is established."})
                output_fonts = structural_inventory(out, [])['fonts']
                original_programs = {value["sha256"] for font in inventory["fonts"] for value in font["embedded"].values()}
                output_programs = {value["sha256"] for font in output_fonts for value in font["embedded"].values()}
                check(report, identifier + ":freetext:existing-embedded-font-programs-retained", original_programs <= output_programs,
                      {"sourcePrograms": sorted(original_programs), "outputPrograms": sorted(output_programs),
                       "sourceSubtypes": sorted({font["subtype"] for font in inventory["fonts"]}),
                       "scope": "Existing programs are retained byte-for-byte; no new CFF/OpenType authoring or text editing support is inferred."})
                check(report, identifier + ":freetext:bookmarks-and-attachments-retained", outline_snapshot(out) == outlines and attachment_snapshot(out) == attachments,
                      {"outlineTopLevelEntries": len(outlines), "attachmentNames": sorted(attachments)})
                new_annotations = []
                for index, page in enumerate(out.pages):
                    original = annotations(reader.pages[index])
                    current = annotations(page)
                    additions = [value for value in current if value not in original]
                    check(report, identifier + f":freetext:page-{index}:existing-annotations-retained", all(value in current for value in original)
                          and (len(additions) == 1 if index == 0 else not additions), {"before": len(original), "after": len(current)})
                    if index == 0:
                        new_annotations = [raw.get_object() for raw in page.get("/Annots", [])
                                           if raw.get_object().get("/Subtype") == "/FreeText" and str(raw.get_object().get("/Contents")) == ANNOTATION_TEXT]
                check(report, identifier + ":freetext:canonical-new-annotation", len(new_annotations) == 1, len(new_annotations))
                if len(new_annotations) == 1:
                    annot = new_annotations[0]
                    ap = normal_appearance(annot)
                    text, fragments = appearance_text(ap, out)
                    fonts, selections = appearances_font_usage(ap, out)
                    embedded = all("/DescendantFonts" in fonts[alias].get_object() and "/FontFile2" in fonts[alias]["/DescendantFonts"][0]["/FontDescriptor"] for alias, _ in selections)
                    check(report, identifier + ":freetext:actual-CJK-AP-and-embedded-font", normalized_text(text) == normalized_text(ANNOTATION_TEXT)
                          and embedded, {"canonical": str(annot.get("/Contents")), "appearanceText": text, "fontSelections": selections, "fragments": fragments})
                evidence["metadataObservations"].append({"operation": name, "inputInfo": plain(reader.metadata), "outputInfo": plain(out.metadata),
                                                         "noMetadataByteIdentityClaim": True})
            elif name == "sanitize":
                check(report, identifier + ":sanitize:non-page-data-removed", "/AcroForm" not in out.trailer["/Root"] and not out.outline and not out.attachments
                      and all(not page.get("/Annots") for page in out.pages), "All annotations/widgets, forms, bookmarks and embedded attachments are removed by the explicit policy.")
                check(report, identifier + ":sanitize:sampled-page-body-text-retained", all(normalized_text(reader.pages[index].extract_text()) == normalized_text(out.pages[index].extract_text())
                      for index in sample_indices), sample_indices)
            elif name == "redact-tiny-region":
                evidence["operations"][-1]["specificTarget"] = operation.get("Details")
                evidence["operations"][-1]["notGeneralPartialDeletionEvidence"] = True
            elif name == "redact-whole-first-page":
                check(report, identifier + ":whole-first-page:first-page-has-no-text-or-painting", not normalized_text(out.pages[0].extract_text())
                      and not out.pages[0].get_contents().get_data().strip(),
                      {"text": out.pages[0].extract_text(), "paintingBytes": len(out.pages[0].get_contents().get_data())})
                check(report, identifier + ":whole-first-page:all-other-page-text-retained",
                      all(normalized_text(reader.pages[index].extract_text()) == normalized_text(out.pages[index].extract_text()) for index in range(1, len(reader.pages))),
                      {"pagesChecked": len(reader.pages) - 1})
                check(report, identifier + ":whole-first-page:all-other-page-painting-operands-retained",
                      all(ContentStream(reader.pages[index].get_contents(), reader).operations == ContentStream(out.pages[index].get_contents(), out).operations
                          for index in range(1, len(reader.pages))),
                      {"pagesChecked": len(reader.pages) - 1, "comparison": "Every independently decoded PDF painting operator and operand, including TJ text/adjustments; comments and serialization whitespace are irrelevant."})
                check(report, identifier + ":whole-first-page:non-page-data-removed", "/AcroForm" not in out.trailer["/Root"]
                      and not out.outline and not out.attachments and all(not page.get("/Annots") for page in out.pages),
                      "Strict cleanup policy precedes whole-object deletion.")
            if name in ("sanitize", "redact-whole-first-page"):
                orphans = orphan_streams(out)
                check(report, identifier + ":" + name + ":no-unreachable-stream-remnants", not orphans, orphans)
            guard(report)
            if not fail_fast:
                for index in (expected_samples if name == "redact-whole-first-page" else sample_indices):
                    cache_key = identifier, index
                    if cache_key not in rendered_inputs:
                        rendered_inputs[cache_key] = render(path, index, directory, renderer, report, identifier + "-input")
                    before_image = rendered_inputs[cache_key]
                    after_image = render(output_path, index, directory, renderer, report, identifier + "-" + name)
                    if name == "redact-whole-first-page" and index == 0:
                        check(report, identifier + ":whole-first-page:independent-blank-pixels",
                              ImageChops.difference(after_image, Image.new("RGB", after_image.size, "white")).getbbox() is None,
                              {"size": list(after_image.size)})
                        continue
                    exclusions = []
                    if name == "freetext" and index == 0:
                        bounds = operation["Details"]["Bounds"]
                        exclusions.append([bounds["Left"], bounds["Bottom"], bounds["Left"] + bounds["Width"], bounds["Bottom"] + bounds["Height"]])
                    if name in ("sanitize", "redact-tiny-region"):
                        exclusions += [value["/Rect"] for value in annotations(reader.pages[index]) if "appearanceSHA256" in value]
                    if name == "redact-tiny-region" and index == 0:
                        bounds = operation["Details"]["Region"]
                        exclusions.append([bounds["Left"], bounds["Bottom"], bounds["Left"] + bounds["Width"], bounds["Bottom"] + bounds["Height"]])
                    compare_pixels(before_image, after_image, reader.pages[index], exclusions, report, identifier + ":" + name + f":page-{index}")
        guard(report)
    if not fail_fast:
        for identifier, case in cases.items():
            path = local(directory, case["Input"]["File"])
            for index in ([0] if len(PdfReader(path).pages) == 1 else [0, len(PdfReader(path).pages) // 2]):
                if (identifier, index) not in rendered_inputs:
                    rendered_inputs[identifier, index] = render(path, index, directory, renderer, report, identifier + "-input")


def mutate(identifier, sources, native, directory, report):
    source = sources["Sources"][0]
    case = native["Result"]["Cases"][0]
    if identifier == "forged-provenance":
        source["PdfUrl"] = "https://example.invalid/forged-publication.pdf"
    elif identifier == "forged-local-source-hash":
        source["SHA256"] = case["Input"]["SHA256"] = "0" * 64
    elif identifier == "omitted-source":
        native["Result"]["Cases"].pop()
    elif identifier == "unexpected-pass":
        op = next(op for c in native["Result"]["Cases"] for op in c["Operations"] if op["Outcome"] == "refused")
        op.update({"Outcome": "accepted", "ExceptionType": None, "Reason": None, "ExpectationMatched": True})
    elif identifier == "wrong-refusal-cause":
        op = next(op for c in native["Result"]["Cases"] for op in c["Operations"] if op["Outcome"] == "refused")
        op.update({"ExceptionType": "NotSupportedException", "Reason": "SOME OTHER UNRELATED REASON", "ExpectationMatched": True})
    elif identifier == "read-promoted-to-write":
        native["Result"]["GeneralWriteCompatibility"] = True
    elif identifier == "lost-atomic-refusal":
        op = next(op for c in native["Result"]["Cases"] for op in c["Operations"] if op["Outcome"] == "refused")
        op["After"]["Revision"] += 1
    elif identifier in ("missing-output", "retained-body-removed", "stale-CJK-appearance", "whole-page-deletion-is-fake", "hybrid-XFA-damaged"):
        operation_name = "redact-whole-first-page" if identifier == "whole-page-deletion-is-fake" else "freetext"
        target_case = next(c for c in native["Result"]["Cases"] if c["Id"] == "irs-w4-en") if identifier == "hybrid-XFA-damaged" else native["Result"]["Cases"][0]
        op = next(op for op in target_case["Operations"] if op["Name"] == operation_name and op["Outcome"] == "accepted")
        output = op["Output"]
        if identifier == "missing-output":
            output["File"] = "negative-output-that-does-not-exist.pdf"
            return
        writer = PdfWriter(clone_from=local(directory, output["File"]))
        if identifier in ("retained-body-removed", "whole-page-deletion-is-fake"):
            stream = DecodedStreamObject()
            stream.set_data(b"q Q\n" if identifier == "retained-body-removed" else b"0 0 10 10 re f\n")
            writer.pages[0][NameObject("/Contents")] = writer._add_object(stream)
        elif identifier == "hybrid-XFA-damaged":
            xfa = writer.root_object["/AcroForm"]["/XFA"]
            target_packet = next(xfa[index + 1].get_object() for index in range(0, len(xfa), 2) if str(xfa[index]) == "template")
            target_packet.set_data(b"<template>WRONG TEMPLATE</template>")
        else:
            annot = next(raw.get_object() for raw in writer.pages[0]["/Annots"] if raw.get_object().get("/Subtype") == "/FreeText"
                         and str(raw.get_object().get("/Contents")) == ANNOTATION_TEXT)
            ap = normal_appearance(annot)
            fonts, selections = appearances_font_usage(ap, writer)
            ap.set_data(("BT " + selections[0][0] + " 12 Tf 2 2 Td <0000> Tj ET\n").encode("ascii"))
        target = directory / ("negative-" + uuid.uuid4().hex + ".pdf")
        with target.open("wb") as stream:
            writer.write(stream)
        writer.close()
        output.update({"File": target.name, "SHA256": sha(target), "Bytes": target.stat().st_size})
        op["After"]["SHA256"] = sha(target)
        report["negativeMutatedPdf"] = {"file": target.name, "sha256": sha(target)}
    else:
        raise ValueError("Unknown negative control")


def controls(directory, renderer, report):
    results = []
    negative_root = directory / "negative-controls"
    negative_root.mkdir(exist_ok=True)
    for identifier in ("forged-provenance", "forged-local-source-hash", "omitted-source", "unexpected-pass", "wrong-refusal-cause",
                       "read-promoted-to-write", "lost-atomic-refusal", "missing-output", "retained-body-removed", "stale-CJK-appearance",
                       "whole-page-deletion-is-fake", "hybrid-XFA-damaged"):
        target = negative_root / (identifier + ".json")
        store(target, {"passed": True, "status": "stale-success-sentinel"})
        process = subprocess.run([sys.executable, str(Path(__file__).resolve()), "--run", str(directory), "--renderer", str(renderer),
                                  "--mutation", identifier, "--report", str(target), "--fail-fast"], capture_output=True, text=True, timeout=240)
        negative = read(target)
        failed = [value["id"] for value in negative.get("checks", []) if not value["passed"]]
        intended = (any(value.endswith(":published-provenance-and-capability-pins") for value in failed) if identifier.startswith("forged-") else
                    "fixed-published-revision-matrix" in failed if identifier == "omitted-source" else
                    any(value.endswith(":locked-outcome-and-specific-reason") for value in failed) if identifier in ("unexpected-pass", "wrong-refusal-cause") else
                    "no-read-to-edit-or-production-overclaim" in failed if identifier == "read-promoted-to-write" else
                    any(value.endswith(":atomic-refusal-and-no-output") for value in failed) if identifier == "lost-atomic-refusal" else
                    "FileNotFoundError" in negative.get("error", "") if identifier == "missing-output" else
                    any(value.endswith(":all-page-body-streams-retained") for value in failed) if identifier == "retained-body-removed" else
                    any(value.endswith(":first-page-has-no-text-or-painting") for value in failed) if identifier == "whole-page-deletion-is-fake" else
                    any(value.endswith(":hybrid-XFA-packets-retained") for value in failed) if identifier == "hybrid-XFA-damaged" else
                    any(value.endswith(":actual-CJK-AP-and-embedded-font") or value.endswith(":existing-annotations-retained") for value in failed))
        passed = process.returncode != 0 and negative.get("passed") is False and negative.get("status") != "stale-success-sentinel" and intended
        results.append({"id": identifier, "passed": passed, "exit": process.returncode, "expectedRejectionDetected": intended,
                        "rejectedChecks": failed, "error": negative.get("error"), "report": str(target.relative_to(directory)).replace("\\", "/"),
                        "reportSHA256": sha(target), "mutatedPdf": negative.get("negativeMutatedPdf")})
    report["negativeControls"] = results
    check(report, "semantic-negative-controls-and-stale-success-protection", all(value["passed"] for value in results), results)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run", required=True)
    parser.add_argument("--renderer", required=True)
    parser.add_argument("--self-test", action="store_true")
    parser.add_argument("--mutation", help=argparse.SUPPRESS)
    parser.add_argument("--report", help=argparse.SUPPRESS)
    parser.add_argument("--fail-fast", action="store_true", help=argparse.SUPPRESS)
    args = parser.parse_args()
    supplied = Path(args.run).resolve()
    directory = supplied / "real-corpus" if (supplied / "real-corpus").is_dir() else supplied
    target = Path(args.report).resolve() if args.report else directory / "independent-audit.json"
    report = {"schemaVersion": 1, "status": "verification-incomplete", "passed": False,
              "verifiedUtc": dt.datetime.now(dt.timezone.utc).isoformat(), "scriptSHA256": sha(__file__),
              "helperSHA256": sha(Path(__file__).with_name("forms_verifier_common.py")), "pypdfVersion": pypdf.__version__,
              "checks": [], "rasters": [], "limitations": [
                  "Six exact published PDF revisions and representative pages are a compatibility sample, not a population guarantee.",
                  "Read/render, byte-copy export/save, FreeText structural rewrite, strict cleanup and a concrete tiny-region redaction have distinct outcomes.",
                  "Existing CFF/CID resources retained by an annotation rewrite do not prove CFF font creation, shaping or new text authoring.",
                  "Pixel equivalence is between independently rendered source/output on this Poppler installation; cross-engine reading parity needs manual review.",
                  "Actual refusal exception/reason is recorded separately from other document features; feature co-presence alone is not causal evidence.",
                  "No Linux/Kylin, physical input/IME, general partial deletion, arbitrary PDF preservation or official performance-parity claim."]}
    store(target, report)
    try:
        renderer = Path(args.renderer).resolve(strict=True)
        version = subprocess.run([str(renderer), "-v"], capture_output=True, text=True, timeout=15)
        if version.returncode:
            raise ValueError("Cannot identify Poppler renderer")
        report["renderer"] = {"path": str(renderer), "sha256": sha(renderer), "version": (version.stdout + version.stderr).strip()}
        audit(directory, renderer, report, args.mutation, args.fail_fast)
        if args.self_test:
            controls(directory, renderer, report)
        report["passed"] = bool(report["checks"]) and all(value["passed"] for value in report["checks"])
        report["status"] = "published-corpus-operation-boundaries-independently-confirmed" if report["passed"] else "verification-failed"
    except Exception as error:
        report.update({"passed": False, "status": "verification-error", "error": f"{type(error).__name__}: {error}"})
    store(target, report)
    print(json.dumps({"report": str(target), "passed": report["passed"], "checks": len(report["checks"]),
                      "failed": [value["id"] for value in report["checks"] if not value["passed"]], "error": report.get("error")}, ensure_ascii=True))
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
