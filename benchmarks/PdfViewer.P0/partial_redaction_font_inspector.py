"""Read-only fontTools bounds oracle for the restricted CJK redaction corpus."""
from __future__ import annotations
import hashlib
import json
from pathlib import Path
import sys
from fontTools import version
from fontTools.pens.boundsPen import BoundsPen
from fontTools.ttLib import TTFont


def inspect(job):
    path = Path(job["path"])
    font = TTFont(path, recalcTimestamp=False, checkChecksums=2)
    for tag in font.reader.keys():
        font.reader[tag]
    glyphs, result = font.getGlyphSet(), {}
    for gid in job["gids"]:
        name = font.getGlyphOrder()[gid]
        pen = BoundsPen(glyphs)
        glyphs[name].draw(pen)
        result[str(gid)] = {"bounds": list(pen.bounds) if pen.bounds else None,
                            "advance": font["hmtx"].metrics[name][0]}
    return {"id": job["id"], "sha256": hashlib.sha256(path.read_bytes()).hexdigest().upper(),
            "unitsPerEm": font["head"].unitsPerEm, "glyphs": result,
            "cmapGids": {str(code): font.getGlyphID(font.getBestCmap()[code]) if code in font.getBestCmap() else None for code in job["codepoints"]}}


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    jobs = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
    print(json.dumps({"fontToolsVersion": version, "fonts": [inspect(job) for job in jobs]}, ensure_ascii=True))
