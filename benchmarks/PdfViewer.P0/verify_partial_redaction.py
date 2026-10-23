"""Independent CJK cluster and partial-image deletion feasibility oracle.

Positive PDFs are authored only by the C# probes. This verifier reads actual PDF
operators, all active xref objects, embedded glyph bounds and lossless samples.
Its accepted scope is the fixed deliberately restricted P0 corpus, not a public
production editor or a claim about arbitrary PDF shaping and image formats.
"""
from __future__ import annotations
import argparse
import datetime as dt
import hashlib
import io
import json
import os
import math
from pathlib import Path
import re
import shutil
import subprocess
import sys
import unicodedata
import uuid
import pypdf
from PIL import Image, ImageChops, ImageDraw
from pypdf import PdfReader, PdfWriter
from pypdf.generic import ContentStream, DecodedStreamObject, NameObject, NumberObject
from partial_redaction_common import (all_objects, clean_glyph, close, font_info, image_invocations,
    image_pixels, pixel_mask, point, sha, store, text_glyphs, unreachable_streams)

TEXT_CASES = {
    "simplified": ("保留中文删除再见", "删除"),
    "traditional": ("保留繁體刪除再見", "刪除"),
    "kana-combining": ("残すカ\u3099保存", "カ\u3099"),
    "hangul-jamo": ("한\u1100\u1161글", "\u1100\u1161"),
    "supplementary-extb": ("保\U00020003\U0002000B留", "\U00020003"),
    "variation-sequence": ("保漢\U000E0100留", "漢\U000E0100"),
    "single-glyph-multi-scalar": ("保秘密留", "秘密"),
}
IMAGE_CASES = {
    "axis-rgb": ([192,0,0,144,40,60],[100,102,48,36],3),
    "axis-gray": ([192,0,0,144,40,60],[100,102,48,36],1),
    "rotate90": ([0,192,-144,0,260,40],[152,100,36,48],3),
    "mirror": ([-192,0,0,144,260,60],[152,102,48,36],3),
    "affine": ([192,48,36,144,40,40],[140,110,48,36],3),
    "shared-same-page": ([192,0,0,144,20,40],[80,82,48,36],3),
    "shared-cross-page": ([192,0,0,144,20,40],[80,82,48,36],3),
}


class AuditRejected(Exception):
    pass


def rect(value):
    if isinstance(value,(tuple,list)):
        return list(map(float,value))
    return [float(value[name]) for name in ("Left","Bottom","Width","Height")]


def check(report, identifier, passed, detail):
    report["checks"].append({"id":identifier,"passed":bool(passed),"detail":detail})


def guard(report):
    if any(not item["passed"] for item in report["checks"]):
        raise AuditRejected("Independent structural/semantic checks rejected this candidate")


def native_report(run, probe, report):
    directory=run/probe
    path=directory/"result.json"
    native=json.loads(path.read_text(encoding="utf-8-sig"))
    value=native["Result"]
    report["nativeReports"].append({"probe":probe,"file":str(path.relative_to(run)).replace("\\","/"),"sha256":sha(path)})
    checks=value["Checks"]
    if probe=="cjk-cluster-redaction":
        expected_checks={identifier+":"+suffix for identifier in TEXT_CASES for suffix in
                         ("public-cleanup-commit","actual-source-decoded","actual-output-decoded","input-one-native-object","neighbor-origins-preserved",
                          "neighbor-native-bounds-preserved","cluster-expansion","unused-mappings-pruned","strict-partial-cluster-refusal")}
        expected_checks.update(identifier+":refused-before-output" for identifier in
                               ("rotated-page","unsupported-TJ","missing-ToUnicode","invalid-surrogate-ToUnicode"))
        expected_files={identifier+"-"+phase+".pdf" for identifier in TEXT_CASES for phase in ("input","redacted")}
        expected_files.update(identifier+"-refused-input.pdf" for identifier in
                              ("rotated-page","unsupported-TJ","missing-ToUnicode","invalid-surrogate-ToUnicode"))
    else:
        expected_checks={identifier+":"+suffix for identifier in IMAGE_CASES for suffix in
                         ("pixel-samples-rewritten","transform-preserved","instance-isolated","public-cleanup-committed","hidden-tail-trimmed","input-immutable")}
        expected_checks.update(identifier+":unsupported-refused-without-output" for identifier in
                               ("refuse-jpeg","refuse-soft-mask","refuse-clip","refuse-singular","refuse-unsupported-bpc","refuse-form-xobject"))
        expected_checks.update(identifier+":unsafe-control-original-pixels-recoverable" for identifier in ("unsafe-overlay","unsafe-crop"))
        expected_files={identifier+"."+phase+".pdf" for identifier in IMAGE_CASES for phase in ("input","redacted")}
        expected_files.update(identifier+".input.pdf" for identifier in
                              ("refuse-jpeg","refuse-soft-mask","refuse-clip","refuse-singular","refuse-unsupported-bpc","refuse-form-xobject"))
        expected_files.update(identifier+"."+phase+".pdf" for identifier in ("unsafe-overlay","unsafe-crop") for phase in ("input","unsafe"))
    check(report,probe+":native-checks",native["Probe"]==probe and native["ExpectationsPassed"] is True
          and bool(checks) and len({x["Id"] for x in checks})==len(checks) and {x["Id"] for x in checks}==expected_checks and all(x["Passed"] for x in checks),
          {"status":value["Status"],"checkCount":len(checks),"managedAssemblySHA256":native["ManagedAssemblySHA256"],"nativeModule":native["NativeModule"]})
    outputs={x["File"]:x["SHA256"].upper() for x in value["Outputs"]}
    check(report,probe+":unique-complete-output-manifest",len(outputs)==len(value["Outputs"]) and set(outputs)==expected_files,sorted(outputs))
    readers={}
    for name,digest in outputs.items():
        path=directory/name
        check(report,probe+":"+name+":native-output-binding",sha(path)==digest,{"sha256":sha(path),"bytes":path.stat().st_size})
        report["pdfs"].append({"file":str(path.relative_to(run)).replace("\\","/"),"sha256":sha(path),"bytes":path.stat().st_size})
        readers[name]=PdfReader(path,strict=True)
    return directory,value,readers


def audit_full_rewrite(reader,path,report,label):
    streams,orphaned=unreachable_streams(reader)
    check(report,label+":all-active-xref-decodable-and-no-orphan-stream",not orphaned,{"streams":streams,"unreachable":orphaned})
    data=path.read_bytes()
    check(report,label+":one-complete-rewrite",b"/Prev" not in data and len(re.findall(rb"startxref\s+\d+\s+%%EOF",data))==1,
          {"startxrefCount":len(re.findall(rb"startxref\s+\d+\s+%%EOF",data)),"hasPrev":b"/Prev" in data})


def cluster_units(glyphs):
    """Independent restricted corpus policy: CID atoms plus Unicode extend marks.

    This deliberately does not purport to implement complete UAX 29. Hangul Jamo,
    variation sequences and synthetic multi-scalar mappings here are single CID
    atoms. Japanese combining dakuten occupies two CIDs and is joined here.
    """
    groups=[]
    for glyph in glyphs:
        scalar=ord(glyph["unicode"][0])
        extend=unicodedata.combining(glyph["unicode"][0])!=0 or 0xFE00<=scalar<=0xFE0F or 0xE0100<=scalar<=0xE01EF
        if extend and groups:
            groups[-1].append(glyph)
        else:
            groups.append([glyph])
    return groups


def expected_units(identifier):
    if identifier=="hangul-jamo":
        return ["한","\u1100\u1161","글"]
    if identifier=="variation-sequence":
        return ["保","漢\U000E0100","留"]
    if identifier=="single-glyph-multi-scalar":
        return ["保","秘密","留"]
    return list(TEXT_CASES[identifier][0])


def glyph_unicode(unit):
    return {"\u1100\u1161":"가","漢\U000E0100":"漢","秘密":"密"}.get(unit,unit)


def audit_text(run,font_python,report,font_cache=None):
    directory,native,readers=native_report(run,"cjk-cluster-redaction",report)
    cases=native["Cases"]
    check(report,"text:fixed-case-matrix",len(cases)==len(TEXT_CASES) and {x["Id"] for x in cases}==set(TEXT_CASES),sorted(x["Id"] for x in cases))
    source_manifest=Path(__file__).parent/"Fixtures"/"font-subset-sources.json"
    font_sources={x["File"]:x["SHA256"] for x in json.loads(source_manifest.read_text(encoding="utf-8-sig"))["Files"] if x["File"].endswith(".ttf")}
    report["sourceManifestSHA256"]=sha(source_manifest)
    jobs,fonts,glyphsets={}, {}, {}
    extracted=run/"partial-redaction"/"extracted-fonts"
    extracted.mkdir(exist_ok=True)
    for case in cases:
        identifier=case["Id"]
        for phase in ("input","redacted"):
            name=identifier+"-"+phase+".pdf"
            page=readers[name].pages[0]
            glyphs=glyphsets[(identifier,phase)]=text_glyphs(page,readers[name])
            for glyph in glyphs:
                program=glyph["font"]["program"]
                digest=hashlib.sha256(program).hexdigest().upper()
                path=extracted/(digest+".ttf")
                if font_cache and (font_cache/path.name).is_file() and sha(font_cache/path.name)==digest:
                    path=font_cache/path.name
                elif not path.exists():
                    path.write_bytes(program)
                job=jobs.setdefault(digest,{"id":digest,"path":str(path),"gids":set(),"codepoints":set()})
                job["gids"].add(glyph["gid"])
                job["codepoints"].update(ord(value) for value in glyph_unicode(glyph["unicode"]))
    jobpath=run/"partial-redaction"/"font-jobs.json"
    store(jobpath,[{**x,"gids":sorted(x["gids"]),"codepoints":sorted(x["codepoints"])} for x in jobs.values()])
    helper=Path(__file__).with_name("partial_redaction_font_inspector.py")
    command=subprocess.run([str(font_python),str(helper),str(jobpath)],capture_output=True,text=True,encoding="utf-8",timeout=120)
    if command.returncode:
        raise RuntimeError("fontTools bounds oracle failed: "+command.stderr[-2000:])
    analysis=json.loads(command.stdout)
    store(run/"partial-redaction"/"font-analysis.json",analysis)
    fonts={x["id"]:x for x in analysis["fonts"]}
    report["fontTools"]={"pythonPath":str(font_python),"pythonSHA256":sha(font_python),"helperSHA256":sha(helper),"version":analysis["fontToolsVersion"],
                         "jobsSHA256":sha(jobpath),"analysisSHA256":sha(run/"partial-redaction"/"font-analysis.json")}
    raster_pairs=[]
    for case in cases:
        identifier=case["Id"]
        expected,removed=TEXT_CASES[identifier]
        input_name,output_name=identifier+"-input.pdf",identifier+"-redacted.pdf"
        original,output=readers[input_name],readers[output_name]
        before,after=glyphsets[(identifier,"input")],glyphsets[(identifier,"redacted")]
        expected_font="GowunDodum-Regular.ttf" if identifier=="hangul-jamo" else "LXGWWenKai-Regular.ttf"
        check(report,identifier+":frozen-full-font-source",case["FontFile"]==expected_font and case["FontSourceSHA256"]==font_sources[expected_font]
              and all(hashlib.sha256(glyph["font"]["program"]).hexdigest().upper()==font_sources[expected_font] for glyph in before),
              {"sourceFont":expected_font,"sha256":font_sources[expected_font]})
        actual_before="".join(x["unicode"] for x in before)
        actual_after="".join(x["unicode"] for x in after)
        units=expected_units(identifier)
        check(report,identifier+":fixed-CID-atoms-and-real-font-cmap-glyphs",[x["unicode"] for x in before]==units
              and all(len(glyph_unicode(unit))==1 and glyph["gid"]>0 and glyph["gid"]==fonts[hashlib.sha256(glyph["font"]["program"]).hexdigest().upper()]["cmapGids"].get(str(ord(glyph_unicode(unit))))
                      for glyph,unit in zip(before,units)),
              {"expectedAtoms":units,"actualAtoms":[x["unicode"] for x in before],"gids":[x["gid"] for x in before]})
        check(report,identifier+":fixed-input-and-removed-cluster-text",actual_before==expected and actual_after==expected.replace(removed,"",1),
              {"before":actual_before,"after":actual_after,"removed":removed})
        expected_x, origins=40.,[]
        for glyph in before:
            origins.append((expected_x,160,26))
            expected_x+=glyph["width"]/1000*26
        text_operations=ContentStream(original.pages[0].get_contents(),original).operations
        shown=[operands[0].original_bytes if hasattr(operands[0],"original_bytes") else bytes(operands[0])
               for operands,operator in text_operations if operator==b"Tj"]
        check(report,identifier+":packed-single-source-Tj-with-interior-deletion",len(shown)==1 and len(shown[0])==len(before)*2 and len(before)>2
              and actual_before.startswith(before[0]["unicode"]) and removed not in before[0]["unicode"] and removed not in before[-1]["unicode"],
              {"sourceTjCount":len(shown),"CIDCount":len(before),"sourceShowByteLengths":[len(x) for x in shown]})
        check(report,identifier+":fixed-page-and-baseline",len(original.pages)==len(output.pages)==1
              and list(original.pages[0].mediabox)==list(output.pages[0].mediabox)
              and all(close((x["x"],x["y"],x["size"]),origin) for x,origin in zip(before,origins)),
              {"mediaBox":list(output.pages[0].mediabox),"glyphs":[clean_glyph(x) for x in before]})
        check(report,identifier+":retained-CID-geometry-and-font-identical",all(any(x["cid"]==y["cid"] and x["unicode"]==y["unicode"] and x["gid"]==y["gid"]
              and close(x["matrix"],y["matrix"]) and x["size"]==y["size"] and x["width"]==y["width"]
              and x["font"]["program"]==y["font"]["program"] for x in before) for y in after),[clean_glyph(x) for x in after])
        groups=cluster_units(before)
        selected=[]
        genuine_partial=False
        regions=[rect(x) for x in case.get("Regions",[case.get("Region")])]
        boxes=[]
        for group in groups:
            group_boxes=[]
            for glyph in group:
                info=fonts[hashlib.sha256(glyph["font"]["program"]).hexdigest().upper()]
                bounds=info["glyphs"][str(glyph["gid"])]["bounds"]
                if bounds is None:
                    raise ValueError("Positive glyph has no outline")
                factor=glyph["size"]/info["unitsPerEm"]
                box=[glyph["x"]+bounds[0]*factor,glyph["y"]+bounds[1]*factor,glyph["x"]+bounds[2]*factor,glyph["y"]+bounds[3]*factor]
                group_boxes.append(box)
                width=info["glyphs"][str(glyph["gid"])]["advance"]*1000/info["unitsPerEm"]
                check(report,identifier+":cid"+str(glyph["cid"])+":width-from-embedded-hmtx",abs(width-glyph["width"])<0.1,{"pdfWidth":glyph["width"],"fontWidth":width})
            selected_by_region=any(min(box[2],r[0]+r[2])>max(box[0],r[0]) and min(box[3],r[1]+r[3])>max(box[1],r[1]) for box in group_boxes for r in regions)
            if selected_by_region:
                selected.extend(group)
                boxes.extend(group_boxes)
                union=(min(x[0] for x in group_boxes),min(x[1] for x in group_boxes),max(x[2] for x in group_boxes),max(x[3] for x in group_boxes))
                genuine_partial=genuine_partial or not any(r[0]<=union[0] and r[1]<=union[1] and r[0]+r[2]>=union[2] and r[1]+r[3]>=union[3] for r in regions)
        selected_text="".join(x["unicode"] for x in selected)
        selected_ids={x["cid"] for x in selected}
        retained_ids={x["cid"] for x in before}-selected_ids
        check(report,identifier+":independent-outline-cluster-hit-and-expansion",selected_text==removed and selected_ids and genuine_partial
              and {x["cid"] for x in after}==retained_ids and len(after)==len(before)-len(selected),
              {"regions":regions,"genuinePartialClusterHit":genuine_partial,"selectedText":selected_text,"selectedCIDs":sorted(selected_ids),"clusters":["".join(x["unicode"] for x in group) for group in groups]})
        for name,font in output.pages[0]["/Resources"]["/Font"].items():
            info=font_info(font)
            check(report,identifier+":"+str(name)+":only-used-ToUnicode-and-erased-deleted-CIDToGID",set(info["mapping"])==retained_ids
                  and all(info["gid"](cid)==0 for cid in selected_ids) and all(info["gid"](cid)>0 for cid in retained_ids),
                  {"mappedCIDs":sorted(info["mapping"]),"deletedGIDs":{str(cid):info["gid"](cid) for cid in selected_ids}})
        audit_full_rewrite(output,directory/output_name,report,identifier)
        # Traverse every active object, not merely text reachable from the page.
        text_streams,hidden=[],[]
        for reference,obj in all_objects(output):
            payload=obj.get_data() if hasattr(obj,"get_data") else str(obj).encode("utf-8")
            if b"CJK source hidden copy" in payload:
                hidden.append(list(reference))
            if hasattr(obj,"get_data") and obj.get("/Subtype")=="/Form":
                text_streams.append(list(reference))
        check(report,identifier+":no-retained-or-orphan-content-Form",not text_streams,text_streams)
        check(report,identifier+":source-hidden-title-and-nonpage-surfaces-removed",not hidden and all("/Annots" not in page for page in output.pages)
              and set(map(str,output.trailer["/Root"])).issubset({"/Type","/Pages","/Version","/Metadata"}),
              {"hiddenSourceCopies":hidden,"catalogKeys":sorted(map(str,output.trailer["/Root"]))})
        raster_pairs.append({"id":identifier,"kind":"text","input":directory/input_name,"output":directory/output_name,"boxes":boxes,"pages":1})
    refusals=native["Refusals"]
    refusal_ids={"rotated-page","unsupported-TJ","missing-ToUnicode","invalid-surrogate-ToUnicode"}
    check(report,"text:fixed-refusal-input-matrix",len(refusals)==4 and {x["Id"] for x in refusals}==refusal_ids
          and all(x["Refused"] is True and x.get("OutputFile") is None for x in refusals),[x["Id"] for x in refusals])
    for case in refusals:
        identifier=case["Id"]
        reader=readers[case["InputFile"]]
        page=reader.pages[0]
        if identifier=="rotated-page":
            invalid=int(page.get("/Rotate",0))%360==90
        elif identifier=="unsupported-TJ":
            invalid=any(op==b"TJ" for _,op in ContentStream(page.get_contents(),reader).operations)
        elif identifier=="missing-ToUnicode":
            invalid=any("/ToUnicode" not in raw.get_object() for raw in page["/Resources"]["/Font"].values())
        else:
            try:
                text_glyphs(page,reader)
                invalid=False
            except UnicodeDecodeError:
                invalid=True
        check(report,identifier+":independent-invalid-text-input",invalid,{"input":case["InputFile"],"output":case.get("OutputFile")})
    return raster_pairs


def audit_images(run,report):
    directory,native,readers=native_report(run,"partial-image-redaction",report)
    positives=[x for x in native["Cases"] if x["Kind"]=="positive"]
    check(report,"image:fixed-case-matrix",len(positives)==len(IMAGE_CASES) and {x["Id"] for x in positives}==set(IMAGE_CASES),[x["Id"] for x in positives])
    raster_pairs=[]
    for case in positives:
        identifier=case["Id"]
        expected_matrix,region,components=IMAGE_CASES[identifier]
        input_name,output_name=case["InputFile"],case["OutputFile"]
        original,output=readers[input_name],readers[output_name]
        invocations_before=[image_invocations(page,original) for page in original.pages]
        invocations_after=[image_invocations(page,output) for page in output.pages]
        check(report,identifier+":page-scope-and-invocation-count",len(output.pages)==len(original.pages)==(2 if identifier=="shared-cross-page" else 1)
              and [len(x) for x in invocations_before]==[len(x) for x in invocations_after],
              {"before":[len(x) for x in invocations_before],"after":[len(x) for x in invocations_after]})
        check(report,identifier+":fixed-source-transform-and-target",close(invocations_before[0][0]["matrix"],expected_matrix)
              and close(rect(case["Region"]),region) and close(case["Matrix"],expected_matrix),
              {"actual":invocations_before[0][0]["matrix"],"region":rect(case["Region"])})
        width,height,c,before,tail=image_pixels(invocations_before[0][0]["image"],True)
        mask=pixel_mask(width,height,expected_matrix,region)
        fixed_pattern=bytes(17+((x*19+y*37+channel*61)%211) for y in range(24) for x in range(32) for channel in range(components))
        check(report,identifier+":nontrivial-source-and-independent-pixel-mask",(width,height,c)==(32,24,components) and 0<sum(mask)<width*height
              and before==fixed_pattern and tail.startswith(b"P0_IMAGE_TRAILING_SECRET_"),{"samples":len(before),"maskedPixels":sum(mask),"tail":tail.decode("ascii","replace")})
        expected=bytearray(before)
        for index,selected in enumerate(mask):
            if selected:
                expected[index*c:(index+1)*c]=bytes([255])*c
        actual=image_pixels(invocations_after[0][0]["image"])
        check(report,identifier+":exact-independent-pixels-not-overlay-or-clip",actual[:3]==(width,height,c) and actual[3]==expected,
              {"expectedSHA256":hashlib.sha256(expected).hexdigest().upper(),"actualSHA256":hashlib.sha256(actual[3]).hexdigest().upper(),"removedPixels":sum(mask)})
        for pi,(before_inv,after_inv) in enumerate(zip(invocations_before,invocations_after)):
            used={x["name"] for x in after_inv}
            defined=set(map(str,output.pages[pi]["/Resources"]["/XObject"]))
            check(report,identifier+":page"+str(pi)+":no-unused-image-resource",used==defined,{"used":sorted(used),"defined":sorted(defined)})
            for ii,(a,b) in enumerate(zip(before_inv,after_inv)):
                check(report,identifier+":page"+str(pi)+":instance"+str(ii)+":same-transform",close(a["matrix"],b["matrix"]),b["matrix"])
                if pi or ii:
                    check(report,identifier+":page"+str(pi)+":instance"+str(ii)+":unselected-exact-original-samples",image_pixels(a["image"],True)[:4]==image_pixels(b["image"])[:4],"Original selected-instance mask must not leak to shared uses.")
        images=[]
        hidden=[]
        for reference,obj in all_objects(output):
            payload=obj.get_data() if hasattr(obj,"get_data") else str(obj).encode("utf-8")
            if b"P0_IMAGE_TRAILING_SECRET_" in payload or b"P0_IMAGE_HIDDEN_INFO_" in payload:
                hidden.append(list(reference))
            if hasattr(obj,"get_data") and obj.get("/Subtype")=="/Image":
                pixels=image_pixels(obj)
                images.append({"reference":list(reference),"sha256":hashlib.sha256(pixels[3]).hexdigest().upper()})
        original_hash=hashlib.sha256(before).hexdigest().upper()
        shared=identifier.startswith("shared-")
        check(report,identifier+":all-xref-image-closure-and-shared-exception",len(images)==(2 if shared else 1)
              and (any(x["sha256"]==original_hash for x in images) if shared else all(x["sha256"]!=original_hash for x in images)),images)
        check(report,identifier+":all-xref-hidden-info-and-image-tail-removed",not hidden,hidden)
        operators=[operator for page in output.pages for _,operator in ContentStream(page.get_contents(),output).operations]
        check(report,identifier+":positive-painting-is-only-invocation-no-cover-or-clip",set(operators).issubset({b"q",b"Q",b"cm",b"Do"})
              and all("/CropBox" not in page and "/Annots" not in page for page in output.pages),[operator.decode() for operator in operators])
        audit_full_rewrite(output,directory/output_name,report,identifier)
        points=[]
        for index,selected in enumerate(mask):
            if selected:
                x,y=index%width,index//width
                points.extend(point(expected_matrix,u,v) for u,v in ((x/width,1-y/height),((x+1)/width,1-y/height),
                              ((x+1)/width,1-(y+1)/height),(x/width,1-(y+1)/height)))
        selected_bounds=[min(p[0] for p in points),min(p[1] for p in points),max(p[0] for p in points),max(p[1] for p in points)]
        raster_pairs.append({"id":identifier,"kind":"image","input":directory/input_name,"output":directory/output_name,
                             "region":region,"boxes":[selected_bounds],"pages":len(output.pages)})
    refusals=[x for x in native["Cases"] if x["Kind"]=="refusal"]
    refusal_ids={"refuse-jpeg","refuse-soft-mask","refuse-clip","refuse-singular","refuse-unsupported-bpc","refuse-form-xobject"}
    check(report,"image:unsupported-input-refusal-matrix",len(refusals)==len(refusal_ids) and {x["Id"] for x in refusals}==refusal_ids
          and all(x.get("OutputFile") is None for x in refusals),[x["Id"] for x in refusals])
    for case in refusals:
        reader=readers[case["InputFile"]]
        page=reader.pages[0]
        objects=[obj for _,obj in all_objects(reader) if isinstance(obj,dict)]
        images=[obj for obj in objects if obj.get("/Subtype")=="/Image"]
        identifier=case["Id"]
        if identifier=="refuse-jpeg":
            jpeg=next(obj for obj in images if obj.get("/Filter")=="/DCTDecode")
            with Image.open(io.BytesIO(jpeg.get_data())) as decoded:
                reason=decoded.format=="JPEG" and decoded.size==(32,24)
                decoded.load()  # A mislabeled non-JPEG stream cannot satisfy this check.
        elif identifier=="refuse-soft-mask":
            reason=any("/SMask" in obj for obj in images)
        elif identifier=="refuse-clip":
            reason=any(operator in (b"W",b"W*") for _,operator in ContentStream(page.get_contents(),reader).operations)
        elif identifier=="refuse-singular":
            matrices=[tuple(map(float,operands)) for operands,operator in ContentStream(page.get_contents(),reader).operations if operator==b"cm"]
            reason=any(abs(a*d-b*c)<1e-9 for a,b,c,d,e,f in matrices)
        elif identifier=="refuse-unsupported-bpc":
            reason=any(int(obj["/BitsPerComponent"])!=8 for obj in images)
        else:
            reason=any(obj.get("/Subtype")=="/Form" for obj in objects)
        check(report,identifier+":independent-unsupported-input-counterexample",reason,{"input":case["InputFile"],"output":case.get("OutputFile")})
    unsafe=[x for x in native["Cases"] if x["Kind"]=="unsafe-control"]
    recoverable=[]
    for case in unsafe:
        reader=readers[case["OutputFile"]]
        images=[obj for _,obj in all_objects(reader) if isinstance(obj,dict) and obj.get("/Subtype")=="/Image"]
        pattern=bytes(17+((x*19+y*37+channel*61)%211) for y in range(24) for x in range(32) for channel in range(3))
        recoverable.append({"id":case["Id"],"originalSamplesRecoverable":any(image_pixels(obj,True)[:4]==(32,24,3,pattern)
                            and image_pixels(obj,True)[4].startswith(b"P0_IMAGE_TRAILING_SECRET_") for obj in images)})
    check(report,"image:unsafe-clip-and-cover-retain-recoverable-original",len(recoverable)==2 and {x["id"] for x in recoverable}=={"unsafe-overlay","unsafe-crop"}
          and all(x["originalSamplesRecoverable"] for x in recoverable),recoverable)
    return raster_pairs


def render_pairs(run,pairs,renderer,report):
    command=subprocess.run([str(renderer),"-v"],capture_output=True,text=True,timeout=15)
    if command.returncode:
        raise RuntimeError("Cannot identify Poppler renderer")
    report["renderer"]={"path":str(renderer),"sha256":sha(renderer),"version":(command.stdout+command.stderr).strip()}
    sheet=Image.new("RGB",(1200,len(pairs)*320),"#dddddd")
    draw=ImageDraw.Draw(sheet)
    for index,pair in enumerate(pairs):
        images={}
        for stage in ("input","output"):
            for page in range(pair["pages"]):
                pdf=pair[stage]
                prefix=run/"partial-redaction"/(pair["id"]+"-"+stage+"-p"+str(page))
                png=prefix.with_suffix(".png")
                png.unlink(missing_ok=True)
                completed=subprocess.run([str(renderer),"-png","-r","144","-f",str(page+1),"-singlefile",str(pdf),str(prefix)],capture_output=True,text=True,timeout=60)
                prefix.with_suffix(".stderr.txt").write_text(completed.stderr,encoding="utf-8")
                diagnostics=[x.strip() for x in completed.stderr.splitlines() if x.strip()]
                check(report,pair["id"]+":"+stage+":page"+str(page)+":Poppler-render",completed.returncode==0 and png.is_file() and not diagnostics,{"exit":completed.returncode,"diagnostics":diagnostics})
                with Image.open(png) as image:
                    images[(stage,page)]=image.convert("RGB")
                report["rasters"].append({"pdf":str(pdf.relative_to(run)).replace("\\","/"),"page":page,"pdfSHA256":sha(pdf),"png":str(png.relative_to(run)).replace("\\","/"),"pngSHA256":sha(png)})
        original,output=images[("input",0)],images[("output",0)]
        difference=ImageChops.difference(original,output)
        page_reader=PdfReader(pair["input"],strict=True)
        page_width,page_height=map(float,(page_reader.pages[0].mediabox.width,page_reader.pages[0].mediabox.height))
        scale=2
        boxes=pair["boxes"]
        excluded=[]
        for left,bottom,right,top in boxes:
            # Pixel-border tolerance is limited to two pixels of antialiasing.
            box=(int(left*scale)-2,int((page_height-top)*scale)-2,int(right*scale)+3,int((page_height-bottom)*scale)+3)
            excluded.append(box)
            difference.paste((0,0,0),box)
            if pair["kind"]=="text":
                # The expanded exclusion mask tolerates renderer antialiasing,
                # but it may touch a retained neighbor. Test whiteness only in
                # the actual deleted glyph's independently derived outline box.
                tight=(math.floor(left*scale),math.floor((page_height-top)*scale),math.ceil(right*scale),math.ceil((page_height-bottom)*scale))
                crop=output.crop(tight)
                check(report,pair["id"]+":deleted-outline-pixels-blank:"+str(len(excluded)),crop.getextrema()==((255,255),(255,255),(255,255))
                      and original.crop(tight).getextrema()!=((255,255),(255,255),(255,255)),{"outlineBox":tight,"antialiasExclusionBox":box})
        check(report,pair["id"]+":all-retained-page0-pixels-exact",difference.getbbox() is None,{"excludedPixelBoxes":excluded,"differenceBounds":difference.getbbox()})
        for page in range(1,pair["pages"]):
            check(report,pair["id"]+":unselected-page"+str(page)+":all-pixels-exact",ImageChops.difference(images[("input",page)],images[("output",page)]).getbbox() is None,"Shared image on unselected page must be unchanged.")
        if pair["kind"]=="image":
            left,bottom,width,height=pair["region"]
            box=(int(left*2)+2,int((page_height-bottom-height)*2)+2,int((left+width)*2)-2,int((page_height-bottom)*2)-2)
            check(report,pair["id"]+":target-interior-pixels-blank",output.crop(box).getextrema()==((255,255),(255,255),(255,255)),box)
        for col,stage in enumerate(("input","output")):
            image=images[(stage,0)].copy()
            image.thumbnail((580,290))
            draw.text((col*600+5,index*320+5),pair["id"]+" / "+stage,fill="black")
            sheet.paste(image,(col*600,index*320+25))
    contact=run/"partial-redaction"/"partial-redaction-contact-sheet.png"
    sheet.save(contact)
    report["contactSheet"]={"file":str(contact.relative_to(run)).replace("\\","/"),"sha256":sha(contact),"visualReview":"separate inspection required"}


def update_declared_hash(case,probe,filename):
    path=case/probe/"result.json"
    native=json.loads(path.read_text(encoding="utf-8-sig"))
    for item in native["Result"]["Outputs"]:
        if item["File"]==filename:
            item["SHA256"]=sha(case/probe/filename)
    store(path,native)


def negative_controls(run,renderer,font_python,report):
    folder=run/"partial-redaction"/"self-tests"/uuid.uuid4().hex
    folder.mkdir(parents=True)
    definitions=[("missing-output","text"),("corrupt-pdf","text"),("byte-tamper","text"),
                 ("forged-original-text-restored","text"),("forged-unreachable-stream","text"),("forged-retained-geometry-shift","text"),
                 ("forged-deleted-CID-mapping","text"),("forged-lone-surrogate-mapping","text"),("forged-hidden-source-title","text"),("forged-original-image-restored","image"),
                 ("forged-target-pixel-not-cleared","image"),("forged-outside-pixel-modified","image"),
                 ("forged-shared-instance-modified","image"),("forged-cover-original-samples-without-tail","image"),
                 ("forged-crop-original-samples-without-tail","image")]
    controls=[]
    for identifier,kind in definitions:
        case=folder/identifier
        case.mkdir()
        for probe in ("cjk-cluster-redaction","partial-image-redaction"):
            (case/probe).mkdir()
            source=run/probe
            native=json.loads((source/"result.json").read_text(encoding="utf-8-sig"))
            shutil.copyfile(source/"result.json",case/probe/"result.json")
            for name in [x["File"] for x in native["Result"]["Outputs"]]:
                try:
                    os.link(source/name,case/probe/name)
                except OSError:
                    shutil.copyfile(source/name,case/probe/name)
        (case/"partial-redaction").mkdir()
        stale=case/"partial-redaction"/"independent-audit.json"
        store(stale,{"passed":True,"status":"stale-success-sentinel"})
        probe="cjk-cluster-redaction" if kind=="text" else "partial-image-redaction"
        filename="simplified-redacted.pdf" if kind=="text" else ("shared-same-page.redacted.pdf" if identifier=="forged-shared-instance-modified" else "axis-rgb.redacted.pdf")
        target=case/probe/filename
        if identifier=="missing-output":
            target.unlink()
        elif identifier=="corrupt-pdf":
            temporary=target.with_suffix(".mutated.pdf")
            temporary.write_bytes(b"Not a PDF; matching outer hash cannot make it valid.\n")
            temporary.replace(target)
            update_declared_hash(case,probe,filename)
        elif identifier=="byte-tamper":
            temporary=target.with_suffix(".mutated.pdf")
            shutil.copyfile(target,temporary)
            with temporary.open("ab") as stream:
                stream.write(b"\n% changed after native binding\n")
            temporary.replace(target)
        elif identifier in ("forged-original-text-restored","forged-original-image-restored"):
            original=target.with_name(filename.replace("-redacted.pdf","-input.pdf").replace(".redacted.pdf",".input.pdf"))
            temporary=target.with_suffix(".mutated.pdf")
            os.link(original,temporary)
            temporary.replace(target)
            update_declared_hash(case,probe,filename)
        else:
            source_for_writer=(case/probe/"unsafe-overlay.unsafe.pdf" if identifier=="forged-cover-original-samples-without-tail" else
                               case/probe/"unsafe-crop.unsafe.pdf" if identifier=="forged-crop-original-samples-without-tail" else target)
            writer=PdfWriter(clone_from=source_for_writer)
            if identifier=="forged-unreachable-stream":
                original=PdfReader(target.with_name("simplified-input.pdf"),strict=True)
                leak=DecodedStreamObject()
                leak[NameObject("/Type")]=NameObject("/XObject")
                leak[NameObject("/Subtype")]=NameObject("/Form")
                leak.set_data(original.pages[0].get_contents().get_data())
                leak[NameObject("/Resources")]=original.pages[0]["/Resources"].clone(writer)
                writer._add_object(leak)
            elif identifier=="forged-retained-geometry-shift":
                data=writer.pages[0].get_contents().get_data()
                stream=DecodedStreamObject()
                stream.set_data(b"q 1 0 0 1 7 0 cm\n"+data+b"\nQ")
                writer.pages[0][NameObject("/Contents")]=writer._add_object(stream)
            elif identifier=="forged-deleted-CID-mapping":
                original=PdfReader(target.with_name("simplified-input.pdf"),strict=True)
                old=font_info(next(iter(original.pages[0]["/Resources"]["/Font"].values())))
                new=font_info(next(iter(writer.pages[0]["/Resources"]["/Font"].values())))
                new["font"][NameObject("/ToUnicode")]=old["font"]["/ToUnicode"].clone(writer)
            elif identifier=="forged-lone-surrogate-mapping":
                new=font_info(next(iter(writer.pages[0]["/Resources"]["/Font"].values())))
                stream=DecodedStreamObject()
                stream.set_data(b"1 beginbfchar\n<0001> <D840>\nendbfchar\n")
                new["font"][NameObject("/ToUnicode")]=writer._add_object(stream)
            elif identifier=="forged-hidden-source-title":
                writer.add_metadata({"/Title":"CJK source hidden copy 保留中文删除再见"})
            elif identifier in ("forged-cover-original-samples-without-tail","forged-crop-original-samples-without-tail"):
                for raw in writer.pages[0]["/Resources"]["/XObject"].values():
                    obj=raw.get_object()
                    if obj.get("/Subtype")=="/Image":
                        obj.set_data(image_pixels(obj,True)[3])
            else:
                invocations=image_invocations(writer.pages[0],writer)
                image=invocations[1 if identifier=="forged-shared-instance-modified" else 0]["image"]
                width,height,c,data,_=image_pixels(image)
                data=bytearray(data)
                if identifier=="forged-target-pixel-not-cleared":
                    mask=pixel_mask(width,height,IMAGE_CASES["axis-rgb"][0],IMAGE_CASES["axis-rgb"][1])
                    pixel=mask.index(True)
                else:
                    pixel=0
                data[pixel*c:pixel*c+c]=bytes([0])*c
                image.set_data(bytes(data))
            temp=target.with_suffix(".mutated.pdf")
            with temp.open("wb") as stream:
                writer.write(stream)
            writer.close()
            temp.replace(target)
            update_declared_hash(case,probe,filename)
        completed=subprocess.run([sys.executable,str(Path(__file__).resolve()),"--run",str(case),"--renderer",str(renderer),"--font-python",str(font_python),
                                  "--font-cache",str(run/"partial-redaction"/"extracted-fonts"),"--structural-only"],capture_output=True,text=True,timeout=150)
        result=json.loads(stale.read_text(encoding="utf-8"))
        rejected=[x["id"] for x in result["checks"] if not x["passed"]]
        expected=(result.get("error","").startswith("FileNotFoundError:") if identifier=="missing-output" else
                  result.get("error","").startswith("PdfReadError:") if identifier=="corrupt-pdf" else
                  any("native-output-binding" in x for x in rejected) if identifier=="byte-tamper" else
                  any("fixed-input-and-removed-cluster-text" in x or "independent-outline-cluster-hit" in x for x in rejected) if identifier=="forged-original-text-restored" else
                  any("orphan" in x or "content-Form" in x for x in rejected) if identifier=="forged-unreachable-stream" else
                  any("retained-CID-geometry" in x for x in rejected) if identifier=="forged-retained-geometry-shift" else
                  any("only-used-ToUnicode" in x for x in rejected) if identifier=="forged-deleted-CID-mapping" else
                  result.get("error","").startswith("UnicodeDecodeError:") if identifier=="forged-lone-surrogate-mapping" else
                  any("source-hidden-title" in x for x in rejected) if identifier=="forged-hidden-source-title" else
                  any("exact-independent-pixels" in x for x in rejected) or result.get("error","").startswith("ValueError: Image samples contain")
                  if identifier in ("forged-original-image-restored","forged-target-pixel-not-cleared","forged-outside-pixel-modified",
                                    "forged-cover-original-samples-without-tail","forged-crop-original-samples-without-tail") else
                  any("unselected-exact-original-samples" in x for x in rejected))
        controls.append({"id":identifier,"passed":completed.returncode!=0 and result.get("passed") is False and result.get("status")!="stale-success-sentinel" and expected,
                         "expectedRejectionDetected":expected,"exit":completed.returncode,"rejectedChecks":rejected,"error":result.get("error"),
                         "report":str(stale.relative_to(run)).replace("\\","/"),"reportSHA256":sha(stale)})
    report["negativeControls"]=controls
    check(report,"semantic-negative-controls-and-stale-pass-protection",all(x["passed"] for x in controls),{"count":len(controls)})


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run",required=True)
    parser.add_argument("--renderer",required=True)
    parser.add_argument("--font-python",required=True)
    parser.add_argument("--self-test",action="store_true")
    parser.add_argument("--structural-only",action="store_true",help=argparse.SUPPRESS)
    parser.add_argument("--font-cache",help=argparse.SUPPRESS)
    args=parser.parse_args()
    run=Path(args.run).resolve()
    directory=run/"partial-redaction"
    directory.mkdir(parents=True,exist_ok=True)
    target=directory/"independent-audit.json"
    report={"passed":False,"status":"verification-incomplete","verifiedUtc":dt.datetime.now(dt.timezone.utc).isoformat(),
            "scope":"restricted C# CJK CID-cluster expansion and affine lossless partial-image deletion feasibility; independent read-only positive oracle",
            "scriptSHA256":sha(Path(__file__).resolve()),"helperSHA256":sha(Path(__file__).with_name("partial_redaction_common.py")),
            "fontInspectorSHA256":sha(Path(__file__).with_name("partial_redaction_font_inspector.py")),
            "fontMappingParserSHA256":sha(Path(__file__).with_name("verify_font_subset.py")),
            "pypdfVersion":pypdf.__version__,"nativeReports":[],"checks":[],"pdfs":[],"rasters":[],
            "limitations":["The text input fixtures use a single packed horizontal CID text string with explicit ToUnicode and /W; outputs preserve absolute glyph positions. General shaping, vertical text and arbitrary UAX 29 clusters remain open.",
                           "Font programs remain embedded and may retain unused glyph programs/cmap entries; deleted page CIDs, mappings and old content streams are removed.",
                           "Image validation accepts 8-bit raw/Flate DeviceGray/RGB without mask, predictors or transparency; other image formats are refusal cases.",
                           "The original image may remain only where required by an unselected shared use; selected scope gets independent edited samples.",
                           "These are benchmark-only feasibility proofs; public production partial deletion and Windows/Linux physical-machine validation remain open."]}
    store(target,report)
    try:
        renderer=Path(args.renderer).resolve(strict=True)
        font_python=Path(args.font_python).resolve(strict=True)
        pairs=audit_text(run,font_python,report,Path(args.font_cache).resolve() if args.font_cache else None)+audit_images(run,report)
        guard(report)
        if not args.structural_only:
            render_pairs(run,pairs,renderer,report)
        if args.self_test:
            negative_controls(run,renderer,font_python,report)
        report["passed"]=bool(report["checks"]) and all(x["passed"] for x in report["checks"])
        report["status"]="bounded-partial-deletion-feasibility-independently-confirmed" if report["passed"] else "verification-failed"
    except Exception as error:
        report["status"]="verification-error"
        report["passed"]=False
        report["error"]=type(error).__name__+": "+str(error)
    store(target,report)
    print(json.dumps({"report":str(target),"passed":report["passed"],"checks":len(report["checks"]),"failed":[x["id"] for x in report["checks"] if not x["passed"]],"error":report.get("error")},ensure_ascii=True))
    return 0 if report["passed"] else 1


if __name__=="__main__":
    raise SystemExit(main())
