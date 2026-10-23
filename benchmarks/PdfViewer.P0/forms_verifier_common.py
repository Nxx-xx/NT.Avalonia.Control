"""Read-only pypdf/Poppler helpers for the P0 AcroForm independent oracles."""
from __future__ import annotations
import hashlib
import json
from pathlib import Path
import re
import subprocess
from PIL import Image, ImageChops
from pypdf import PageObject
from pypdf.generic import ContentStream, NameObject


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest().upper()


def store(path, value):
    Path(path).write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")


def reference(value):
    item = value if hasattr(value, "idnum") else getattr(value, "indirect_reference", None)
    return (item.idnum, item.generation) if item is not None else None


def canonical_fields(reader):
    """Return real terminal fields and verify both directions of the widget graph.

    get_fields() alone can return plausible /V despite orphan page widgets, an
    invalid /Parent or a detached /Kids branch. Do not use it as the graph oracle.
    """
    root = reader.trailer["/Root"] if hasattr(reader, "trailer") else reader._root_object
    acro = root["/AcroForm"].get_object()
    roots = acro["/Fields"]
    fields, widgets, visited = {}, set(), set()

    def visit(raw, parent=None, prefix="", inherited_type=None):
        field = raw.get_object()
        identity = reference(raw)
        if identity is None or identity in visited:
            raise ValueError("Field graph contains a direct, duplicate or cyclic node")
        visited.add(identity)
        actual_parent = reference(field.raw_get("/Parent")) if "/Parent" in field else None
        if actual_parent != parent:
            raise ValueError("Field /Parent does not match canonical /Kids owner")
        local_name = str(field.get("/T", ""))
        name = ".".join(value for value in (prefix, local_name) if value)
        kind = field.get("/FT", inherited_type)
        kids = list(field.get("/Kids", []))
        if kids and any("/T" in value.get_object() for value in kids):
            if any("/T" not in value.get_object() for value in kids):
                raise ValueError("Mixed child fields and widget children")
            for child in kids:
                visit(child, identity, name, kind)
            return
        if not name or name in fields or kind is None:
            raise ValueError("Terminal field name/type is absent or duplicated")
        own_widgets = []
        if kids:
            for child in kids:
                widget = child.get_object()
                child_identity = reference(child)
                if child_identity is None or child_identity in visited:
                    raise ValueError("Duplicate or direct widget in /Kids")
                visited.add(child_identity)
                if widget.get("/Subtype") != "/Widget" or "/T" in widget or reference(widget.raw_get("/Parent")) != identity:
                    raise ValueError("Widget /Parent does not match its terminal field")
                own_widgets.append(widget)
                widgets.add(child_identity)
        elif field.get("/Subtype") == "/Widget":
            own_widgets.append(field)
            widgets.add(identity)
        else:
            raise ValueError("Terminal field has no page widget")
        fields[name] = {"field": field, "widgets": own_widgets, "type": str(kind), "reference": identity}

    for raw in roots:
        visit(raw)
    page_widgets = []
    for page in reader.pages:
        for raw in page.get("/Annots", []):
            annot = raw.get_object()
            if annot.get("/Subtype") == "/Widget":
                page_widgets.append(reference(raw))
                if "/P" in annot and reference(annot.raw_get("/P")) != reference(page):
                    raise ValueError("Widget /P points to a different page")
    if len(page_widgets) != len(set(page_widgets)) or set(page_widgets) != widgets:
        raise ValueError("Canonical /Fields widget set differs from page /Annots")
    return acro, fields


def normal_appearance(widget):
    normal = widget["/AP"]["/N"].get_object()
    if not hasattr(normal, "get_data"):
        state = widget.get("/AS")
        if state is None or state not in normal:
            raise ValueError("Widget appearance state has no normal appearance")
        normal = normal[state].get_object()
    if normal.get("/Subtype") != "/Form" or not normal.get_data():
        raise ValueError("Normal appearance is not a non-empty Form stream")
    return normal


def appearance_text(ap, reader):
    page = PageObject(reader)
    page[NameObject("/Resources")] = ap["/Resources"]
    page[NameObject("/Contents")] = ap
    page[NameObject("/MediaBox")] = ap["/BBox"]
    fragments = []
    text = page.extract_text(visitor_text=lambda value, cm, tm, font, size: fragments.append(
        {"text": value, "x": round(float(tm[4]), 5), "y": round(float(tm[5]), 5), "size": float(size)}))
    return text or "", [value for value in fragments if value["text"].strip()]


def text_lines(text):
    return [part.strip() for part in re.split(r"[\r\n]+", text) if part.strip()]


def appearances_font_usage(ap, reader):
    fonts = ap.get("/Resources", {}).get("/Font", {})
    selections = [(str(args[0]), float(args[1])) for args, op in ContentStream(ap, reader).operations if op == b"Tf"]
    if not selections or any(name not in fonts for name, _ in selections):
        raise ValueError("Appearance Tf does not resolve through its own /Resources /Font")
    return fonts, selections


def choice_options(field):
    result = []
    for item in field.get("/Opt", []):
        value = item.get_object()
        result.append((str(value[0]), str(value[1])) if isinstance(value, list) else (str(value), str(value)))
    return result


def normalized_value(field):
    value = field.get("/V", "")
    return [str(item) for item in value] if isinstance(value, list) else str(value)


def render(directory, filenames, renderer, report, check, dpi=144):
    version = subprocess.run([str(renderer), "-v"], capture_output=True, text=True, timeout=15)
    if version.returncode != 0:
        raise RuntimeError("Cannot identify independent renderer: " + version.stderr)
    report["renderer"] = {"path": str(renderer), "sha256": sha(renderer), "version": (version.stderr + version.stdout).strip()}
    report["rasters"] = []
    images = {}
    for name in filenames:
        prefix = directory / (Path(name).stem + "-poppler")
        png = prefix.with_suffix(".png")
        png.unlink(missing_ok=True)
        process = subprocess.run([str(renderer), "-png", "-r", str(dpi), "-f", "1", "-singlefile", str(directory / name), str(prefix)],
                                 capture_output=True, text=True, timeout=90)
        prefix.with_suffix(".stderr.txt").write_text(process.stderr, encoding="utf-8")
        diagnostics = [line.strip() for line in process.stderr.splitlines() if line.strip()]
        allowed = {"Syntax Error: No display font for 'Symbol'", "Syntax Error: No display font for 'ArialUnicode'"}
        check(name + ":independent-render", process.returncode == 0 and png.is_file() and not [line for line in diagnostics if line not in allowed],
              {"exit": process.returncode, "diagnostics": diagnostics})
        if process.returncode != 0 or not png.is_file():
            raise RuntimeError("Independent renderer did not produce " + name)
        with Image.open(png) as image:
            images[name] = image.convert("RGB")
        report["rasters"].append({"pdf": name, "pdfSHA256": sha(directory / name), "png": png.name,
                                  "pngSHA256": sha(png), "size": list(images[name].size)})
    return images


def pdf_roi(image, rectangle, page_width=600, page_height=700):
    left, bottom, right, top = rectangle
    sx, sy = image.width / page_width, image.height / page_height
    return (round(left * sx), round((page_height - top) * sy), round(right * sx), round((page_height - bottom) * sy))


def same_pixels(left, right, roi=None):
    if left.size != right.size:
        return False
    return ImageChops.difference(left.crop(roi) if roi else left, right.crop(roi) if roi else right).getbbox() is None


def list_selection_pixels(reader, widget, image, expected_indices, page_width=600, page_height=700):
    """Sample each label's row background, away from its glyphs and border.

    Canonical /I and a visible pixel change do not prove that the highlighted
    row matches /I. Anchor samples to independently decoded AP baselines and
    compare the selected/unselected background for every row.
    """
    ap = normal_appearance(widget)
    _, fragments = appearance_text(ap, reader)
    left, bottom, right, top = map(float, widget["/Rect"])
    bbox = list(map(float, ap["/BBox"]))
    if tuple(map(float, ap.get("/Matrix", [1, 0, 0, 1, 0, 0]))) != (1, 0, 0, 1, 0, 0):
        raise ValueError("List pixel oracle only supports identity AP matrices")
    rows = []
    for index, fragment in enumerate(fragments):
        y = bottom + (fragment["y"] - bbox[1] + fragment["size"] * .3) * (top - bottom) / (bbox[3] - bbox[1])
        roi = pdf_roi(image, (right - 12, y - 1, right - 8, y + 1), page_width, page_height)
        colors = list(image.crop(roi).getdata())
        blue = bool(colors) and all(b > r + 30 and b > g + 15 and r < 100 for r, g, b in colors)
        white = bool(colors) and all(min(color) >= 240 for color in colors)
        selected = index in expected_indices
        rows.append({"index": index, "text": fragment["text"].strip(), "selected": selected, "roi": roi,
                     "rgb": list(colors[0]) if colors else None, "passed": blue if selected else white})
    return bool(rows) and all(row["passed"] for row in rows), rows
