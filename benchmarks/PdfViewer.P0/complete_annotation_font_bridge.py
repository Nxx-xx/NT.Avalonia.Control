"""P0 writer bridge: preserve a native CJK AP as an editable FreeText annotation.

Uses pypdf only as a feasibility tool. No Python/pypdf product dependency is implied.
The AP font resource is retained, and /DA names that exact resource. Rebuilding AP
after an edit requires the same shaping/font resource ownership in a real writer.
"""
import hashlib
import json
from pathlib import Path
import sys

from pypdf import PdfReader, PdfWriter
from pypdf.generic import NameObject, TextStringObject

directory = Path(sys.argv[1])
results = []
for stem in ["native-stamp-cjk", "native-stamp-cjk-edited"]:
    source = directory / f"{stem}.pdf"
    reader = PdfReader(source)
    if stem == "native-stamp-cjk-edited":
        # Perform a real update of the previously serialized FreeText, keeping
        # its annotation dictionary rather than merely retyping a second Stamp.
        writer = PdfWriter(clone_from=directory / "bridge-freetext-cjk.pdf")
        stamp = writer.pages[0]["/Annots"][0].get_object()
        source_stamp = reader.pages[0]["/Annots"][0].get_object()
        stamp[NameObject("/AP")] = source_stamp["/AP"].clone(writer)
        stamp[NameObject("/Contents")] = TextStringObject(source_stamp["/Contents"])
        assert stamp["/Subtype"] == "/FreeText"
    else:
        writer = PdfWriter(clone_from=reader)
        stamp = writer.pages[0]["/Annots"][0].get_object()
        assert stamp["/Subtype"] == "/Stamp"
    appearance = stamp["/AP"]["/N"].get_object()
    fonts = appearance["/Resources"]["/Font"]
    # UpdateObject may retain an unused resource while adding a new alias.
    # /DA must match the resource actually referenced by the latest AP stream.
    import re
    font_name = NameObject(re.findall(rb"(/[^\s]+)\s+[\d.]+\s+Tf", appearance.get_data())[-1].decode("ascii"))
    font = fonts[font_name].get_object()
    descendant = font["/DescendantFonts"][0].get_object()
    descriptor = descendant["/FontDescriptor"].get_object()
    embedded_keys = [k for k in ["/FontFile", "/FontFile2", "/FontFile3"] if k in descriptor]
    assert embedded_keys, "Native appearance font is not embedded"
    stamp[NameObject("/Subtype")] = NameObject("/FreeText")
    stamp[NameObject("/DA")] = TextStringObject(f"{font_name} 18 Tf 0 g")
    target = directory / f"{stem.replace('native-stamp', 'bridge-freetext')}.pdf"
    with target.open("wb") as handle:
        writer.write(handle)
    reread = PdfReader(target)
    annots = reread.pages[0]["/Annots"]
    assert len(annots) == 1
    annotation = annots[0].get_object()
    assert annotation["/Subtype"] == "/FreeText"
    ap = annotation["/AP"]["/N"].get_object()
    assert ap.get_data()
    assert annotation["/Contents"] == ("中文批注 字体验证 ABC 123" if stem == "native-stamp-cjk" else "中文批注 编辑后 ABC 123")
    results.append({"output": str(target.resolve()), "sha256": hashlib.sha256(target.read_bytes()).hexdigest(),
                    "subtype": str(annotation["/Subtype"]), "contents": str(annotation["/Contents"]),
                    "appearanceFont": str(font_name), "embeddedFontKeys": embedded_keys,
                    "pageContentBytes": len(reread.pages[0].get_contents().get_data()) if reread.pages[0].get_contents() else 0})
(directory / "bridge-result.json").write_text(json.dumps({"status": "partial", "checks": results,
    "limitations": ["This proves serialized FreeText/AP interoperability, not PDFium FreeText authoring.",
                    "pypdf is a prototype writer bridge only; choose and implement a managed/native product writer before P4.",
                    "Physical Linux rendering, font fallback, shaping, RTL and host editing UI remain unverified."]}, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(results, ensure_ascii=True))
