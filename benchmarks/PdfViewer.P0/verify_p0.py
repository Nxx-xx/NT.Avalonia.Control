"""Independent P0 oracle: pypdf structure plus OpenSSL CMS integrity.

Expected negative cases are assertions about capability boundaries, not editing
features that passed. Raises nonzero on unexpected results or missing evidence.
"""
import argparse
import datetime as dt
import hashlib
import json
import re
import statistics
import subprocess
import zipfile
from pathlib import Path
from pypdf import PdfReader
from PIL import Image, ImageChops


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--run", required=True)
    parser.add_argument("--repo", required=True)
    parser.add_argument("--openssl", required=True)
    parser.add_argument("--poppler", required=True)
    args = parser.parse_args()
    root, repo = Path(args.run).resolve(), Path(args.repo).resolve()
    checks, observations = [], []

    def check(identifier, passed, detail):
        checks.append({"id": identifier, "passed": bool(passed), "detail": detail})

    baseline = json.loads((root / "baseline/baseline.json").read_text(encoding="utf-8-sig"))
    manifest_path = root / "baseline/source-manifest.json"
    archive_path = root / "baseline/source-snapshot.zip"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    check("frozen-manifest-hash", sha(manifest_path) == baseline["SourceManifestSHA256"].upper(), sha(manifest_path))
    check("frozen-archive-hash", sha(archive_path) == baseline["SnapshotSHA256"].upper(), sha(archive_path))
    with zipfile.ZipFile(archive_path) as archive:
        mismatches = [f["Path"] for f in manifest if hashlib.sha256(archive.read(f["Path"])).hexdigest().upper() != f["SHA256"].upper()]
        check("archive-matches-every-source-file", not mismatches and len(archive.infolist()) == len(manifest), {"files": len(manifest), "mismatches": mismatches})
    product_prefixes = ("src/NT.AvaloniaControl.PdfViewer", "tests/PdfViewer.", "samples/PdfViewer.Usage/", "samples/NT.AvaloniaControl.Demo/Pages/PdfViewer/")
    changed = [f["Path"] for f in manifest if f["Path"].startswith(product_prefixes) and sha(repo / f["Path"]) != f["SHA256"].upper()]
    check("baseline-product-source-unchanged", not changed, changed)
    native = json.loads((root / "baseline/native-assets.json").read_text(encoding="utf-8-sig"))
    check("twelve-native-assets-frozen", len(native["Assets"]) == 12 and all(x["Matches"] for x in native["Assets"]) and len(native["Headers"]) == 8, "155.0.8044 assets match the previously verified release hashes; 8 matching public headers hashed.")
    docs_directory = root / "baseline/official-documents"
    docs = json.loads((docs_directory / "manifest.json").read_text(encoding="utf-8-sig"))
    check("seven-official-document-snapshots", len(docs) == 7 and all(sha(docs_directory / x["File"]) == x["SHA256"].upper() for x in docs), "Public document snapshots only. Commercial package/version and official runtime comparison remain unavailable.")

    for name in ["save", "forms", "fonts", "redaction", "baseline"]:
        report = json.loads((root / name / "result.json").read_text(encoding="utf-8-sig"))
        loaded = report["NativeModule"]
        check(f"{name}-actual-native-module", any(x["ActualSHA256"].upper() == loaded["SHA256"].upper() for x in native["Assets"]), loaded)
        execution = json.loads((root / "execution-manifest.json").read_text(encoding="utf-8-sig"))
        check(f"{name}-actual-managed-assembly", report["ManagedAssemblySHA256"] == execution["AssemblySHA256"], report["ManagedAssemblySHA256"])
        result = report["Result"]
        if name in ["save", "forms", "fonts"]:
            native_checks = result.get("Checks", result.get("checks", []))
            check(f"{name}-native-probe-expectations", bool(native_checks) and all(x.get("Passed", x.get("passed", False)) for x in native_checks), {"count": len(native_checks), "status": result.get("Status", result.get("status")), "note": "Includes expected API-gap negative controls; not full product feature acceptance."})

    source = PdfReader(root / "save/input.pdf", strict=True)
    for mode in ["full", "incremental", "atomic-target"]:
        reader = PdfReader(root / f"save/{mode}.pdf", strict=True)
        text = "\n".join(p.extract_text() or "" for p in reader.pages)
        check(f"{mode}-independent-content", len(reader.pages) == 3 and "Hello PDF reader" in text and "P0 saved edit" in text, text)
    encrypted = PdfReader(root / "save/encrypted-full.pdf", strict=True)
    was_encrypted = encrypted.is_encrypted
    decrypted = bool(encrypted.decrypt("reader"))
    check("encrypted-save-independent", was_encrypted and decrypted and "Encrypted saved edit" in encrypted.pages[1].extract_text(), {"encrypted": was_encrypted, "user_password_accepted": decrypted, "revision": encrypted.trailer["/Encrypt"]["/R"]})

    crypto_directory = root / "save/crypto"
    crypto_directory.mkdir(exist_ok=True)
    signature_results = []
    for file, expected_valid in [(root / "fixtures/signed-input.pdf", True), (root / "save/signed-incremental.pdf", True), (root / "save/signed-full.pdf", False)]:
        data = file.read_bytes()
        reader = PdfReader(file)
        signature = reader.get_fields()["P0Signature"]["/V"].get_object()
        ranges = list(map(int, signature["/ByteRange"]))
        contents = bytes(signature["/Contents"])
        # DER length, not rstrip(0): valid signatures may themselves end in zero.
        length_octets = contents[1] & 0x7f if contents[1] & 0x80 else 0
        der_length = (2 + length_octets + int.from_bytes(contents[2:2+length_octets], "big")) if length_octets else 2 + contents[1]
        cms_path = crypto_directory / (file.stem + ".der")
        signed_path = crypto_directory / (file.stem + ".signed-bytes")
        cms_path.write_bytes(contents[:der_length])
        signed_path.write_bytes(data[ranges[0]:ranges[0]+ranges[1]] + data[ranges[2]:ranges[2]+ranges[3]])
        process = subprocess.run([args.openssl, "cms", "-verify", "-inform", "DER", "-in", str(cms_path), "-content", str(signed_path), "-noverify", "-binary", "-out", str(crypto_directory / (file.stem + ".verified-bytes"))], capture_output=True, text=True)
        valid = process.returncode == 0
        if process.returncode not in (0, 4):
            raise RuntimeError(f"OpenSSL infrastructure failure ({process.returncode}): {process.stderr}")
        coverage = ranges[2] + ranges[3]
        item = {"file": str(file), "sha256": sha(file), "crypto_integrity": valid, "expected_integrity": expected_valid, "signed_revision_length": coverage, "file_length": len(data), "covers_latest_revision": coverage == len(data), "openssl_exit": process.returncode, "openssl_message": process.stderr.strip(), "trust_and_DocMDP": "not-validated"}
        signature_results.append(item)
        check(file.stem + "-cms-expected-result", valid == expected_valid, item)
    (crypto_directory / "result.json").write_text(json.dumps(signature_results, indent=2), encoding="utf-8")
    check("signature-original-byte-prefix", (root / "save/signed-incremental.pdf").read_bytes().startswith((root / "fixtures/signed-input.pdf").read_bytes()), "Original cryptographic signature validates the old revision; added page is outside the signed revision. This does not establish DocMDP permission or certificate trust.")

    forms = PdfReader(root / "forms/forms-saved.pdf", strict=True)
    fields = forms.get_fields()
    acro = forms.trailer["/Root"]["/AcroForm"].get_object()
    widgets = [x.get_object() for x in forms.pages[0]["/Annots"]]
    check("forms-canonical-fields", len(acro["/Fields"]) == 3 and fields["Name"]["/V"] == "P0 focused input" and fields["Agree"]["/V"] == "/Yes" and fields["Choice"]["/V"] == "Beta", {key: str(value.get("/V")) for key, value in fields.items()})
    by_name = {str(w["/T"]): w for w in widgets}
    check("forms-widget-state-and-selection", by_name["Agree"]["/AS"] == "/Yes" and list(by_name["Choice"]["/I"]) == [1], "Checkbox /AS=/Yes; combo /I=[1].")
    def has_ap(widget):
        normal = widget["/AP"]["/N"].get_object()
        return bool(normal.get_data()) if hasattr(normal, "get_data") else all(bool(x.get_object().get_data()) for x in normal.values())
    check("forms-interactive-ap-preserved", len(widgets) == 3 and all(w["/Subtype"] == "/Widget" and has_ap(w) for w in widgets), "Fields and widgets remain; no flattening; nonempty normal appearances.")

    for stem, expected, subtype in [
        ("native-stamp-cjk", "中文批注 字体验证 ABC 123", "/Stamp"),
        ("native-stamp-cjk-edited", "中文批注 编辑后 ABC 123", "/Stamp"),
        ("bridge-freetext-cjk", "中文批注 字体验证 ABC 123", "/FreeText"),
        ("bridge-freetext-cjk-edited", "中文批注 编辑后 ABC 123", "/FreeText"),
    ]:
        reader = PdfReader(root / f"fonts/{stem}.pdf", strict=True)
        page = reader.pages[0]
        annotation = page["/Annots"][0].get_object()
        appearance = annotation["/AP"]["/N"].get_object()
        used_fonts = re.findall(rb"(/[^\s]+)\s+[\d.]+\s+Tf", appearance.get_data())
        embedded = []
        for font_name in used_fonts:
            font = appearance["/Resources"]["/Font"][font_name.decode()].get_object()
            descriptor = font["/DescendantFonts"][0].get_object()["/FontDescriptor"].get_object()
            embedded.append(len(descriptor["/FontFile2"].get_object().get_data()) > 0 and bool(font["/ToUnicode"].get_object().get_data()))
        check(stem + "-annotation-ap-font", annotation["/Subtype"] == subtype and annotation["/Contents"] == expected and len(page["/Annots"]) == 1 and bool(embedded) and all(embedded) and not (page.get_contents() and page.get_contents().get_data()), {"subtype": subtype, "contents": str(annotation["/Contents"]), "embedded_fonts": len(embedded), "note": "Annotation AP, embedded FontFile2 and ToUnicode; no page-body text substitution."})
        raster = subprocess.run([args.poppler, "-png", "-scale-to", "1600", "-singlefile", str(root / f"fonts/{stem}.pdf"), str(root / f"fonts/{stem}-poppler")], capture_output=True, text=True)
        stderr = root / f"fonts/{stem}-poppler.stderr.txt"
        stderr.write_text(raster.stderr, encoding="utf-8")
        check(stem + "-poppler-clean", stderr.exists() and not stderr.read_text(encoding="utf-8-sig").strip(), "Independent Poppler raster generated without warnings; visual acceptance separately recorded.")
        check(stem + "-poppler-exit", raster.returncode == 0, {"exit": raster.returncode})

    for suffix in ["", "-edited"]:
        stamp = Image.open(root / f"fonts/native-stamp-cjk{suffix}-poppler.png").convert("RGB")
        free_text = Image.open(root / f"fonts/bridge-freetext-cjk{suffix}-poppler.png").convert("RGB")
        check("font-bridge-raster-equality" + suffix, ImageChops.difference(stamp, free_text).getbbox() is None, "Stamp and FreeText render identically under the independent renderer.")
        check("font-raster-not-blank" + suffix, ImageChops.difference(stamp, Image.new("RGB", stamp.size, "white")).getbbox() is not None, "Actual annotation AP pixels are visible.")
    negative_path = root / "fonts/native-freetext-negative-control.pdf"
    negative = subprocess.run([args.poppler, "-png", "-scale-to", "1600", "-singlefile", str(negative_path), str(root / "fonts/native-freetext-negative-control-poppler")], capture_output=True, text=True)
    (root / "fonts/native-freetext-negative-control-poppler.stderr.txt").write_text(negative.stderr, encoding="utf-8")
    negative_image = Image.open(root / "fonts/native-freetext-negative-control-poppler.png").convert("RGB")
    check("freetext-negative-control-detected", negative.returncode == 0 and "Unknown font tag" in negative.stderr and ImageChops.difference(negative_image, Image.new("RGB", negative_image.size, "white")).getbbox() is None, "Expected API-gap counterexample: unresolved AP font and blank output; not a supported FreeText path.")

    form_raster = subprocess.run([args.poppler, "-png", "-r", "120", "-singlefile", str(root / "forms/forms-saved.pdf"), str(root / "forms/forms-saved-poppler")], capture_output=True, text=True)
    (root / "forms/forms-saved-poppler.stderr.txt").write_text(form_raster.stderr, encoding="utf-8")
    check("forms-poppler-exit", form_raster.returncode == 0, {"exit": form_raster.returncode, "diagnostics": form_raster.stderr.strip(), "limitation": "ASCII form only; missing fallback display fonts remain a platform/font warning."})
    redaction_audit = json.loads((root / "redaction/independent-audit.json").read_text(encoding="utf-8-sig"))
    check("redaction-independent-expectations", redaction_audit["Status"] == "expected-restricted-results-confirmed" and redaction_audit["NativeReportSha256"] == sha(root / "redaction/result.json"), {"status": redaction_audit["Status"], "note": "Includes unsafe native candidates and unsupported-scope refusals. Never a general redaction certification."})

    baseline_result = json.loads((root / "baseline/result.json").read_text())["Result"]
    measurements = baseline_result["Measurements"]
    summaries = []
    for corpus in sorted(set(x["Corpus"] for x in measurements)):
        warm = [x for x in measurements if x["Corpus"] == corpus and x["Iteration"] > 0]
        summaries.append({"corpus": corpus, "samples": len(warm), **{metric + "Median": statistics.median(x[metric] for x in warm) for metric in ["LoadMs", "RenderMs", "SearchMs", "DisposeMs"]}, "privateBytesMax": max(x["PrivateBytes"] for x in warm)})
    (root / "baseline/timing-summary.json").write_text(json.dumps({"kind": "exploratory-backend-only", "rows": summaries, "p95_SLA_official_comparison": "not-measured"}, indent=2), encoding="utf-8")
    observations.extend(["FreeText writer bridge uses Python only in the experiment; production writer/subsetting choice remains a release blocker.", "Native redaction candidates are not approved disclosure outputs; consult redaction/independent-audit.json.", "Ubuntu and Kylin x64/ARM64 physical results, official runnable package/version, corpus breadth and UI performance remain open G0 items."])
    result = {"verifiedUtc": dt.datetime.now(dt.timezone.utc).isoformat(), "checks": checks, "passed": all(x["passed"] for x in checks), "productEditingReady": False, "G0Complete": False, "observations": observations}
    target = root / "independent-verification.json"
    target.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"report": str(target), "passed": result["passed"], "checks": len(checks), "failed": [x["id"] for x in checks if not x["passed"]]}, ensure_ascii=True))
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
