"""Independent PDF syntax, affine and lossless image readers for P0 partial deletion."""
from __future__ import annotations
import hashlib
import json
import math
from pathlib import Path
from pypdf.generic import ContentStream, IndirectObject, TextStringObject
from verify_font_subset import cmap_pairs, cid_widths


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest().upper()


def store(path, data):
    Path(path).write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")


def all_objects(reader):
    seen = set()
    for generation, numbers in reader.xref.items():
        if generation == 65535:
            continue
        for number in numbers:
            key = number, generation
            if number and key not in seen:
                seen.add(key)
                yield key, reader.get_object(IndirectObject(number, generation, reader))
    for number in reader.xref_objStm:
        if (number, 0) not in seen:
            yield (number, 0), reader.get_object(IndirectObject(number, 0, reader))


def unreachable_streams(reader):
    reachable, direct = set(), set()
    def visit(value):
        if isinstance(value, IndirectObject):
            key = value.idnum, value.generation
            if key in reachable:
                return
            reachable.add(key)
            visit(value.get_object())
        elif isinstance(value, (dict, list, tuple)):
            if id(value) in direct:
                return
            direct.add(id(value))
            for child in (value.values() if isinstance(value, dict) else value):
                visit(child)
    visit(reader.trailer)
    streams, orphaned = [], []
    for key, value in all_objects(reader):
        if hasattr(value, "get_data"):
            payload = value.get_data()  # Decode every stream, including unreachable ones.
            streams.append({"reference": list(key), "bytes": len(payload), "subtype": str(value.get("/Subtype")),
                            "sha256": hashlib.sha256(payload).hexdigest().upper()})
            if key not in reachable and value.get("/Type") not in ("/XRef", "/ObjStm"):
                orphaned.append(list(key))
    return streams, orphaned


def multiply(first, second):
    a,b,c,d,e,f = first
    g,h,i,j,k,l = second
    return (a*g+c*h,b*g+d*h,a*i+c*j,b*i+d*j,a*k+c*l+e,b*k+d*l+f)


def point(matrix, x, y):
    a,b,c,d,e,f = matrix
    return a*x+c*y+e,b*x+d*y+f


def close(first, second, tolerance=1e-5):
    return len(first) == len(second) and all(abs(float(a)-float(b)) <= tolerance for a,b in zip(first, second))


def font_info(raw):
    font = raw.get_object()
    if font.get("/Subtype") != "/Type0" or font.get("/Encoding") != "/Identity-H":
        raise ValueError("The independent bounded text oracle requires Identity-H Type0")
    descendant = font["/DescendantFonts"][0].get_object()
    if descendant.get("/Subtype") != "/CIDFontType2":
        raise ValueError("Only CIDFontType2 is accepted")
    program = descendant["/FontDescriptor"]["/FontFile2"].get_object().get_data()
    mapping = cmap_pairs(font["/ToUnicode"].get_object().get_data())
    widths, default = cid_widths(descendant)
    cid_map = descendant.get("/CIDToGIDMap", "/Identity")
    cid_bytes = None if cid_map == "/Identity" else cid_map.get_object().get_data()
    def gid(cid):
        return cid if cid_bytes is None else int.from_bytes(cid_bytes[cid*2:cid*2+2], "big") if cid*2+2 <= len(cid_bytes) else 0
    return {"font": font, "descendant": descendant, "program": program, "mapping": mapping,
            "widths": widths, "defaultWidth": default, "gid": gid}


def text_glyphs(page, reader):
    """Interpret actual operators, rather than trusting pypdf extraction or the C# report."""
    resources, result = page["/Resources"], []
    matrix, ctm, stack = (1,0,0,1,0,0), (1,0,0,1,0,0), []
    font, size, spacing, scale, rise = None, 0., 0., 1., 0.
    for operands, op in ContentStream(page.get_contents(), reader).operations:
        if op == b"q":
            stack.append(ctm)
        elif op == b"Q":
            ctm = stack.pop()
        elif op == b"cm":
            ctm = multiply(ctm, tuple(map(float, operands)))
        elif op == b"Tf":
            font = font_info(resources["/Font"][operands[0]])
            size = float(operands[1])
        elif op == b"Tm":
            matrix = tuple(map(float, operands))
        elif op in (b"Td", b"TD"):
            matrix = multiply(matrix, (1,0,0,1,float(operands[0]),float(operands[1])))
        elif op == b"Tc":
            spacing = float(operands[0])
        elif op == b"Tz":
            scale = float(operands[0])/100
        elif op == b"Ts":
            rise = float(operands[0])
        elif op in (b"Tj", b"TJ"):
            if font is None:
                raise ValueError("Text was shown without a font")
            values = [operands[0]] if op == b"Tj" else operands[0]
            for value in values:
                if isinstance(value, (str, bytes)):
                    data = value.original_bytes if hasattr(value, "original_bytes") else bytes(value)
                    if len(data) % 2:
                        raise ValueError("Odd Identity-H string length")
                    for offset in range(0,len(data),2):
                        cid = int.from_bytes(data[offset:offset+2], "big")
                        unicode = font["mapping"].get(cid)
                        if unicode is None:
                            raise ValueError("A shown CID has no ToUnicode mapping")
                        # Strict UTF-16 decode in cmap_pairs rejects lone surrogates.
                        combined = multiply(ctm, matrix)
                        x,y = point(combined, 0, rise)
                        width = font["widths"].get(cid,font["defaultWidth"])
                        result.append({"cid": cid, "unicode": unicode, "gid": font["gid"](cid), "x": x,"y": y,
                                       "size": size, "matrix": list(combined), "width": width, "font": font})
                        matrix = multiply(matrix, (1,0,0,1,(width/1000*size+spacing)*scale,0))
                else:
                    matrix = multiply(matrix,(1,0,0,1,-float(value)/1000*size*scale,0))
        elif op not in (b"BT",b"ET",b"g",b"rg",b"G",b"RG",b"w",b"re",b"f",b"n"):
            raise ValueError("Unexpected text corpus operator: " + repr(op))
    return result


def image_invocations(page, reader):
    resources, ctm, stack, result = page["/Resources"], (1,0,0,1,0,0), [], []
    for operands, op in ContentStream(page.get_contents(), reader).operations:
        if op == b"q":
            stack.append(ctm)
        elif op == b"Q":
            ctm = stack.pop()
        elif op == b"cm":
            ctm = multiply(ctm, tuple(map(float,operands)))
        elif op == b"Do":
            image = resources["/XObject"][operands[0]].get_object()
            if image.get("/Subtype") != "/Image":
                raise ValueError("Positive image oracle only allows image XObjects")
            result.append({"name": str(operands[0]), "matrix": list(ctm), "image": image})
        elif op not in (b"g",b"rg",b"re",b"f",b"n",b"W",b"W*"):
            raise ValueError("Unexpected image corpus operator: " + repr(op))
    return result


def image_pixels(image, allow_tail=False):
    width,height,bpc = int(image["/Width"]),int(image["/Height"]),int(image["/BitsPerComponent"])
    color = str(image["/ColorSpace"])
    components = {"/DeviceGray":1,"/DeviceRGB":3}.get(color)
    if bpc != 8 or components is None or any(name in image for name in ("/SMask","/Mask","/Decode")):
        raise ValueError("Unsupported image format in independent positive oracle")
    filters = image.get("/Filter",[])
    filters = [filters] if isinstance(filters,str) else list(filters)
    if any(str(item) != "/FlateDecode" for item in filters) or image.get("/DecodeParms"):
        raise ValueError("Only unpredicted lossless Flate/raw samples accepted")
    data, expected = image.get_data(), width*height*components
    if len(data) < expected or (len(data) != expected and not allow_tail):
        raise ValueError("Image samples contain missing bytes or unremoved trailing bytes")
    return width,height,components,data[:expected],data[expected:]


def clipped_area(poly, rectangle):
    left,bottom,width,height = rectangle
    for axis,limit,keep_greater in ((0,left,True),(0,left+width,False),(1,bottom,True),(1,bottom+height,False)):
        output=[]
        for previous,current in zip(poly[-1:]+poly[:-1],poly):
            oldin = previous[axis] >= limit if keep_greater else previous[axis] <= limit
            newin = current[axis] >= limit if keep_greater else current[axis] <= limit
            if oldin != newin:
                ratio=(limit-previous[axis])/(current[axis]-previous[axis])
                output.append(tuple(previous[k]+ratio*(current[k]-previous[k]) for k in (0,1)))
            if newin:
                output.append(current)
        poly=output
        if not poly:
            return 0.
    return abs(sum(a[0]*b[1]-a[1]*b[0] for a,b in zip(poly,poly[1:]+poly[:1])))/2


def pixel_mask(width,height,matrix,rectangle):
    result=[]
    for y in range(height):
        for x in range(width):
            poly=[point(matrix,x/width,1-y/height),point(matrix,(x+1)/width,1-y/height),
                  point(matrix,(x+1)/width,1-(y+1)/height),point(matrix,x/width,1-(y+1)/height)]
            result.append(clipped_area(poly,rectangle)>1e-9)
    return result


def clean_glyph(glyph):
    return {key:value for key,value in glyph.items() if key != "font"}
