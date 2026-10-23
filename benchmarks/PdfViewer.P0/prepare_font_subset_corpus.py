"""Freeze OFL CJK compatibility sources and derive explicit edge-case fonts.

Run with a Python containing fontTools. Downloads are pinned and SHA256 checked;
fontTools is test tooling only and never part of either product package.
"""
from __future__ import annotations
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import urllib.request
from fontTools import subset, version as fonttools_version
from fontTools.ttLib import TTFont


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest().upper()


def derive(source, target, text, family, *, no_subset=False, cmap12_only=False):
    font = TTFont(source, recalcTimestamp=False)
    options = subset.Options()
    options.layout_features = []
    sub = subset.Subsetter(options=options)
    sub.populate(text=text)
    sub.subset(font)
    if cmap12_only:
        font["cmap"].tables = [table for table in font["cmap"].tables if table.format == 12]
        assert font["cmap"].tables
    font["OS/2"].fsType = 0x0100 if no_subset else 0
    for name in font["name"].names:
        if name.nameID in (1, 2, 3, 4, 6):
            value = "Regular" if name.nameID == 2 else family.replace(" ", "") if name.nameID == 6 else family
            name.string = value.encode(name.getEncoding(), errors="replace")
    font["head"].created = font["head"].modified = 3_600_000_000
    font.save(target)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", required=True)
    parser.add_argument("--source-cache")
    args = parser.parse_args()
    directory = Path(args.output).resolve() / "font-subset"
    sources = directory / "sources"
    sources.mkdir(parents=True, exist_ok=True)
    manifest_path = Path(__file__).parent / "Fixtures" / "font-subset-sources.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    cache = Path(args.source_cache) if args.source_cache else None
    for item in manifest["Files"]:
        destination = sources / item["File"]
        if not destination.exists() or sha(destination) != item["SHA256"]:
            if cache is not None and (cache / item["File"]).is_file():
                shutil.copyfile(cache / item["File"], destination)
            else:
                with urllib.request.urlopen(item["Url"], timeout=90) as response:
                    destination.write_bytes(response.read())
        if sha(destination) != item["SHA256"] or destination.stat().st_size != item["Bytes"]:
            raise ValueError(f"Frozen source hash/size differs: {destination}")
    shutil.copyfile(manifest_path, sources / "source-manifest.json")
    cases = [
        ("lxgw-sc", "LXGWWenKai-Regular.ttf", "简体中文批注 ABC 123", "简体中文批注 编辑后 ABC 123", "real-full-font: simplified Chinese", True),
        ("lxgw-tc", "LXGWWenKai-Regular.ttf", "繁體中文註解 ABC 123", "繁體中文註解 編輯後 ABC 123", "real-full-font: traditional Chinese", True),
        ("lxgw-jp", "LXGWWenKai-Regular.ttf", "日本語カタカナ ABC 123", "日本語カタカナひらがな ABC 123", "real-full-font: Japanese", True),
        ("lxgw-kr", "LXGWWenKai-Regular.ttf", "한글 주석 ABC 123", "한글 주석 가나다 ABC 123", "real-full-font: precomposed Korean", True),
        ("lxgw-extb", "LXGWWenKai-Regular.ttf", "扩展字 𠀃𠀋 ABC 123", "扩展字 𠀃𠀋𠀎 ABC 123", "real-full-font: CJK Extension B / supplementary Unicode cmap12", True),
        ("zen-jp-composite", "ZenKakuGothicNew-Regular.ttf", "日本語カタカナ Éé ABC 123", "日本語ひらがな ÉéÀ ABC 123", "real-full-font: cmap4 only and composite Latin glyphs", True),
        ("gowun-kr-composite", "GowunDodum-Regular.ttf", "한글 주석 Éé ABC 123", "한글 주석 가나다 ÉéÀ ABC 123", "real-full-font: cmap4/12 and composite Latin glyphs", True),
        ("synthetic-cmap12-only", "P0Cmap12Only.ttf", "扩展字 𠀃𠀋 ABC 123", "扩展字 𠀃𠀋𠀎 ABC 123", "synthetic derivative: cmap12-only input; source LXGW", False),
        ("synthetic-no-subsetting", "P0NoSubsetting.ttf", "한글 주석 Éé ABC 123", "한글 주석 가나다 ÉéÀ ABC 123", "synthetic derivative: OS/2 fsType no-subsetting bit; source Gowun", False),
        ("synthetic-alias-glyph", "P0AliasGlyph.ttf", "한글 AB Éé 123", "한글 AB 가나다 ÉéÀ 123", "synthetic derivative: different Unicode values A/B share one glyph; source Gowun", False),
    ]
    derive(sources / "LXGWWenKai-Regular.ttf", sources / "P0Cmap12Only.ttf", cases[7][3], "P0 Cmap12 Only", cmap12_only=True)
    derive(sources / "GowunDodum-Regular.ttf", sources / "P0NoSubsetting.ttf", cases[8][3], "P0 No Subsetting", no_subset=True)
    derive(sources / "GowunDodum-Regular.ttf", sources / "P0AliasGlyph.ttf", cases[9][3], "P0 Alias Glyph")
    alias = TTFont(sources / "P0AliasGlyph.ttf", recalcTimestamp=False)
    for table in alias["cmap"].tables:
        if table.isUnicode() and 65 in table.cmap:
            table.cmap[66] = table.cmap[65]
    alias.save(sources / "P0AliasGlyph.ttf")
    entries = []
    for identifier, filename, text, updated, purpose, full in cases:
        path = sources / filename
        font = TTFont(path, recalcTimestamp=False)
        cmap = font.getBestCmap()
        assert all(ord(char) in cmap for char in updated), (identifier, "missing source glyph")
        entries.append({"Id": identifier, "File": "sources/" + filename, "SHA256": sha(path), "Bytes": path.stat().st_size,
                        "Text": text, "UpdatedText": updated, "Purpose": purpose, "RealFullFont": full,
                        "Family": font["name"].getDebugName(1), "GlyphCount": font["maxp"].numGlyphs,
                        "CmapFormats": sorted(set(table.format for table in font["cmap"].tables if table.isUnicode())),
                        "FsType": font["OS/2"].fsType})
    result = {"SchemaVersion": 1, "FontToolsVersion": fonttools_version, "SourceManifestSHA256": sha(sources / "source-manifest.json"),
              "PreparationScriptSHA256": sha(__file__), "Cases": entries,
              "License": "SIL OFL 1.1; sources retain the original OFL files; derivative names replace upstream names.",
              "Scope": "Three real full static TrueType CJK families; synthetic edge fixtures are explicitly identified. No font ships in the product packages."}
    (directory / "corpus.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"corpus": str(directory / "corpus.json"), "cases": len(entries), "realFamilies": 3}))


if __name__ == "__main__":
    main()
