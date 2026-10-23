"""Read-only fontTools oracle. Receives a JSON jobs path, emits JSON to stdout."""
from __future__ import annotations
import hashlib
import json
from pathlib import Path
import struct
import sys
from fontTools import version
from fontTools.pens.recordingPen import DecomposingRecordingPen
from fontTools.ttLib import TTFont


def inspect(job):
    path = Path(job["path"])
    font = TTFont(path, recalcTimestamp=False, checkChecksums=2)
    # TTFont is lazy. Force every sfnt table through SFNTReader's strict
    # checksum path, including emitted hinting/name tables unused by drawing.
    for tag in font.reader.keys():
        font.reader[tag]
    glyphset = font.getGlyphSet()
    cmap = font.getBestCmap()
    points = {}
    for code in job["codepoints"]:
        name = cmap.get(code)
        if name is None:
            points[str(code)] = None
            continue
        pen = DecomposingRecordingPen(glyphset)
        glyphset[name].draw(pen)
        closure = set()
        visiting = set()
        def visit(glyph_name):
            if glyph_name in visiting:
                raise ValueError("Composite cycle")
            if glyph_name in closure:
                return
            visiting.add(glyph_name)
            glyph = font["glyf"][glyph_name]
            if glyph.isComposite():
                for component in glyph.components:
                    visit(component.glyphName)
            visiting.remove(glyph_name)
            closure.add(glyph_name)
        visit(name)
        outline = hashlib.sha256(json.dumps(pen.value, separators=(",", ":")).encode()).hexdigest().upper()
        points[str(code)] = {"gid": font.getGlyphID(name), "outlineSHA256": outline,
                             "metrics": list(font["hmtx"].metrics[name]), "composite": font["glyf"][name].isComposite(),
                             "closureCount": len(closure), "closureGids": sorted(font.getGlyphID(value) for value in closure)}
    data = path.read_bytes()
    padded = data + b"\0" * ((4 - len(data) % 4) % 4)
    checksum = sum(struct.unpack(">" + "I" * (len(padded) // 4), padded)) & 0xffffffff
    return {"id": job["id"], "sha256": hashlib.sha256(data).hexdigest().upper(), "bytes": len(data),
            "glyphCount": font["maxp"].numGlyphs, "fsType": font["OS/2"].fsType,
            "unitsPerEm": font["head"].unitsPerEm, "allTableChecksumsCorrect": True,
            "cmapFormats": sorted(set(table.format for table in font["cmap"].tables if table.isUnicode())),
            "sfntChecksumCorrect": checksum == 0xB1B0AFBA, "codepoints": points}


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    if sys.argv[1] == "--mutate-composite":
        font = TTFont(sys.argv[2], recalcTimestamp=False)
        candidates = [name for name in font.getGlyphOrder() if font["glyf"][name].isComposite()]
        if not candidates:
            raise ValueError("Negative control requires an actual composite glyph")
        glyph = font["glyf"][candidates[0]]
        glyph.components[0].glyphName = font.getGlyphOrder()[0]
        font.save(sys.argv[3])
        raise SystemExit(0)
    jobs = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
    print(json.dumps({"fontToolsVersion": version, "fonts": [inspect(job) for job in jobs]}, ensure_ascii=True))
