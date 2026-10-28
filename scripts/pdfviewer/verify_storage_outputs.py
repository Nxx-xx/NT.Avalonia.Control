"""Independent pypdf/Poppler audit of real save targets and retained race versions."""
import argparse,hashlib,json,re,subprocess,sys
from pathlib import Path
import pypdf
from pypdf import PdfReader
from pypdf.generic import ContentStream
from PIL import Image
p=argparse.ArgumentParser();p.add_argument('--root',required=True);a=p.parse_args();root=Path(a.root).resolve()
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest().upper()
def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))
expected={};files=[];negatives=[];renders={};warnings=[]
for path in root.rglob('*.json'):
    try:data=read(path)
    except ValueError:continue
    if isinstance(data,dict):
        for key in ['InputSHA256','OriginalSHA256']:
            if key in data:expected[data[key]]=([],[])
        for check in data.get('checks',[]):
            if 'expectedSHA256' in check:expected[check['expectedSHA256']]=(['B'],['Durable bookmark'])
            if 'originalSHA256' in check:expected[check['originalSHA256']]=([],[])
for path in root.rglob('external.pdf'):expected[sha(path)]=([],[])
for path in list(root.rglob('expected-output.pdf'))+list(root.rglob('first-expected.pdf')):
    expected[sha(path)]=(['B'],['Recovered bookmark' if any(s.endswith('-faults') for s in path.parts) else 'Durable bookmark'])
names={'original.pdf','new.pdf','external.pdf','expected-output.pdf','output.pdf','candidate.pdf','displaced.pdf'}
for path in sorted(p for p in root.rglob('*.pdf') if p.name in names and 'independent-renders' not in p.parts):
    if 'recovery-tamper-symlink' in path.parts and path.name=='candidate.pdf':continue # WSL symlink negative is not PDF bytes.
    digest=sha(path);relative=path.relative_to(root).as_posix()
    if digest not in expected:
        # Only deliberate tamper cases and partial private staging are negative evidence.
        assert ('recovery-tamper' in relative or path.name in ['candidate.pdf','output.pdf']),relative
        rejected=False
        try:PdfReader(path,strict=True)
        except Exception:rejected=True
        negatives.append(dict(path=relative,sha256=digest,bytes=path.stat().st_size,expectedHashMismatch=True,pypdfRejected=rejected));continue
    annotations,titles=expected[digest];pdf=PdfReader(path,strict=True);assert len(pdf.pages)==1 and not pdf.is_encrypted,relative
    actual=[a.get_object() for page in pdf.pages for a in page.get('/Annots',[])]
    assert [str(a.get('/Contents','')) for a in actual]==annotations,relative
    assert [str(b['/Title']) for b in pdf.trailer['/Root'].get('/NTUserBookmarks',[])]==titles,relative
    texts=[]
    for annotation in actual:
        ap=annotation['/AP']['/N'];fonts=ap['/Resources']['/Font'];font=None;text=''
        for operands,op in ContentStream(ap,pdf).operations:
            if op==b'Tf':font=fonts[operands[0]].get_object()
            if op in [b'Tj',b'TJ']:
                mapping={bytes.fromhex(x.decode()):bytes.fromhex(y.decode()).decode('utf-16-be') for x,y in re.findall(rb'<([0-9a-fA-F]{4})>\s*<([0-9a-fA-F]{4})>',font['/ToUnicode'].get_data())}
                for part in operands[0] if op==b'TJ' else operands:
                    if isinstance(part,(float,int)):continue
                    raw=part.original_bytes if hasattr(part,'original_bytes') else bytes(part)
                    text+=''.join(mapping[raw[i:i+2]] for i in range(0,len(raw),2))
                descendant,=font['/DescendantFonts'];assert len(descendant['/FontDescriptor']['/FontFile2'].get_data())>100
        assert text==str(annotation['/Contents']),relative;texts.append(text)
    if digest not in renders:
        prefix=root/'independent-renders'/digest;prefix.parent.mkdir(exist_ok=True)
        result=subprocess.run(['pdftoppm','-f','1','-singlefile','-r','96','-png',str(path),str(prefix)],capture_output=True,text=True,timeout=30)
        assert result.returncode==0,(relative,result.stderr)
        with Image.open(prefix.with_suffix('.png')) as image:image.load();assert image.size==(534,400)
        renders[digest]=prefix.with_suffix('.png').relative_to(root).as_posix()
        if result.stderr:warnings.append(dict(path=relative,warning=result.stderr.strip()))
    files.append(dict(path=relative,sha256=digest,annotations=annotations,actualAPText=texts,bookmarks=titles,render=renders[digest]))
for case in ['fault-normal','crash-Replaced','crash-CommitFlushed']:
    w=sha(root/'windows-faults'/case/'original.pdf');l=sha(root/'linux-faults'/case/'original.pdf')
    with Image.open(root/renders[w]) as x,Image.open(root/renders[l]) as y:assert x.tobytes()==y.tobytes(),case
report=dict(passed=True,PDFs=len(files),distinctRenderedContents=len(renders),negativeControls=len(negatives),pypdfRejectedNegatives=sum(x['pypdfRejected'] for x in negatives),crossPlatformRepresentativePixelsEqual=True,pypdf=pypdf.__version__,python=sys.version,files=files,negatives=negatives,rendererWarnings=warnings)
(root/'independent.json').write_text(json.dumps(report,ensure_ascii=False,indent=2));print(json.dumps({k:v for k,v in report.items() if k not in ['files','negatives','rendererWarnings']}))
