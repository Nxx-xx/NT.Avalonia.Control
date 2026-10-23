"""Materialize pinned, published PDFs for read/write boundary observations.

Run with the benchmark Python (pypdf). Originals remain byte-identical and are
never bundled in the control packages. A changed upstream revision fails closed.
No derived PDF is authored by this preparation script.
"""
from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
import shutil
import urllib.request

from pypdf import PdfReader


def sha(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest().upper()


def valid(path, item):
    return path.is_file() and path.stat().st_size == item["Bytes"] and sha(path) == item["SHA256"]


def materialize(item, cache, destination, *, allow_download):
    if Path(item["File"]).name != item["File"]:
        raise ValueError("Source files must have flat, safe basenames")
    source = cache / item["File"]
    if not valid(source, item):
        if source.exists():
            raise ValueError(f"Cached source differs from the frozen revision: {source}")
        if not allow_download:
            raise FileNotFoundError(f"Pinned cache source missing: {source}; use --download to fetch")
        request = urllib.request.Request(item.get("PdfUrl", item.get("Url")), headers={"User-Agent": "PDF-compatibility-research/1.0"})
        with urllib.request.urlopen(request, timeout=60) as response:
            data = response.read(item["Bytes"] + 1)
        if len(data) != item["Bytes"] or hashlib.sha256(data).hexdigest().upper() != item["SHA256"]:
            raise ValueError(f"Upstream source changed: {item['File']}; no revised source was accepted")
        source.write_bytes(data)
    if destination.exists():
        if not valid(destination, item):
            raise ValueError(f"Existing run evidence differs: {destination}")
        return "existing"
    try:
        os.link(source, destination)
        return "hardlink"
    except OSError:
        shutil.copyfile(source, destination)
        return "copy"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", required=True)
    parser.add_argument("--source-cache", required=True)
    parser.add_argument("--download", action="store_true", help="Explicitly allow pinned network retrieval if a cache file is absent")
    args = parser.parse_args()
    manifest_path = Path(__file__).parent / "Fixtures" / "real-corpus-sources.json"
    raw = manifest_path.read_bytes()
    manifest = json.loads(raw.decode("utf-8-sig"))
    directory = Path(args.output).resolve() / "real-corpus"
    sources = directory / "sources"
    sources.mkdir(parents=True, exist_ok=True)
    (directory / "corpus.json").write_text(json.dumps({"Status": "preparation-incomplete", "SourceManifestSHA256": hashlib.sha256(raw).hexdigest().upper()}) + "\n", encoding="utf-8")
    cache = Path(args.source_cache).resolve()
    cache.mkdir(parents=True, exist_ok=True)
    evidence = []
    for item in [*manifest["Sources"], *manifest["EvidenceFiles"]]:
        destination = sources / item["File"]
        mode = materialize(item, cache, destination, allow_download=args.download)
        if item in manifest["Sources"]:
            reader = PdfReader(destination)
            if reader.is_encrypted or len(reader.pages) != item["Pages"]:
                raise ValueError(f"Unexpected encrypted state or page count: {destination}")
        evidence.append({"File": "sources/" + item["File"], "SHA256": sha(destination), "Bytes": destination.stat().st_size, "Materialization": mode})
    (sources / "manifest.json").write_bytes(raw)
    record = {
        "SchemaVersion": 1,
        "Status": "pinned-published-source-corpus-prepared",
        "PreparedUtc": dt.datetime.now(dt.timezone.utc).isoformat(),
        "SourceManifestFile": "sources/manifest.json",
        "SourceManifestSHA256": hashlib.sha256(raw).hexdigest().upper(),
        "PreparationScriptSHA256": sha(__file__),
        "SourceCount": len(manifest["Sources"]),
        "TotalPdfBytes": sum(item["Bytes"] for item in manifest["Sources"]),
        "TotalPages": sum(item["Pages"] for item in manifest["Sources"]),
        "Sources": manifest["Sources"],
        "Files": evidence,
        "Distribution": "Originals and derived compatibility evidence are external benchmark data; excluded from product/source packages. Retain source-specific attribution and license obligations before redistributing any evidence.",
    }
    (directory / "corpus.json").write_text(json.dumps(record, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({key: record[key] for key in ("Status", "SourceCount", "TotalPdfBytes", "TotalPages", "SourceManifestSHA256")}))


if __name__ == "__main__":
    main()
