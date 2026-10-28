"""Freeze release inputs into two fresh OS-specific source trees; no generated files."""
import argparse,datetime,hashlib,json,pathlib,shutil,subprocess
p=argparse.ArgumentParser();p.add_argument('--output',required=True);a=p.parse_args()
repo=pathlib.Path(__file__).resolve().parents[1];out=pathlib.Path(a.output).resolve()
out.mkdir(parents=True,exist_ok=False)
paths=subprocess.run(['rg','--files','--hidden','-g','!.git/**','-g','!artifacts/**','-g','!**/bin/**','-g','!**/obj/**','-g','!**/__pycache__/**'],cwd=repo,check=True,text=True,capture_output=True).stdout.splitlines()
root_names={'.editorconfig','.gitignore','Directory.Build.props','Directory.Build.targets','Directory.Packages.props','global.json','NuGet.Config'}
doc_names={'docs/charts-preview-release.md','docs/charts-preview-release-scope.json','docs/charts-performance-budgets.json'}
selected=sorted(x.replace('\\','/') for x in paths if x.replace('\\','/').startswith(('src/','scripts/','samples/Charts.Usage/','samples/NT.AvaloniaControl.Demo/','tests/Charts.Headless.Tests/')) or x in root_names or x.replace('\\','/').startswith('tests/Directory.Build.') or x.replace('\\','/') in doc_names)
rows=[]
for name in selected:
    src=repo/name;data=src.read_bytes();sha=hashlib.sha256(data).hexdigest().upper()
    for os in ('windows','linux'):
        target=out/(os+'-source-v1')/name;target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(data)
    rows.append(dict(path=name,bytes=len(data),sha256=sha))
manifest=dict(schema='charts-preview-source-freeze/v1',createdUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),sourceRoot=str(repo),sourceFiles=len(rows),files=rows)
(out/'source-manifest-v1.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(dict(files=len(rows),output=str(out)),ensure_ascii=False))
