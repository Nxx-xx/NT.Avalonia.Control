"""Read-only, standard-library verification of a portable Charts preview bundle."""
import argparse
import datetime
import hashlib
import json
import pathlib
import re
import urllib.parse
import xml.etree.ElementTree as ET
import zipfile


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def verify(bundle, require_ready=False, archive_path=None):
    bundle=pathlib.Path(bundle).resolve()
    checks=[]
    def check(name, passed, detail=None):
        checks.append(dict(name=name,passed=bool(passed),detail=detail))
    inventory={}
    for line in (bundle/'SHA256SUMS').read_text(encoding='utf-8').splitlines():
        match=re.fullmatch(r'([0-9A-Fa-f]{64})  (.+)',line)
        if not match:
            check('checksum syntax',False,line);continue
        value,name=match.groups();target=(bundle/name).resolve()
        valid=target.is_relative_to(bundle) and name not in inventory and target.is_file()
        check('manifest entry '+name,valid)
        if valid:
            check('SHA256 '+name,digest(target)==value.upper())
            inventory[name]=value.upper()
    actual={str(p.relative_to(bundle)).replace('\\','/') for p in bundle.rglob('*') if p.is_file() and p.name!='SHA256SUMS'}
    check('complete file inventory',set(inventory)==actual,sorted(actual.symmetric_difference(inventory)))
    validation=json.loads((bundle/'validation.json').read_text(encoding='utf-8-sig'))
    alias=json.loads((bundle/'charts-preview-release-validation.json').read_text(encoding='utf-8-sig'))
    check('documentation validation alias',validation==alias)
    scope=json.loads((bundle/'charts-preview-release-scope.json').read_text(encoding='utf-8-sig'))
    check('scope version',scope['releaseVersion']==validation['version'])
    check('truthful public release status',validation['readyForPublicRelease'] is False and validation['remotePushPerformed'] is False and validation['originalC7MilestoneComplete'] is False)
    names=set(scope['packages'])
    check('three canonical packages',len(validation['packages'])==3 and len(names)==3)
    expected=set()
    for row in validation['packages']:
        filename=pathlib.PurePosixPath(str(row['path']).replace('\\','/')).name;path=bundle/'packages'/filename;expected.add(filename)
        check('canonical package '+filename,path.is_file() and digest(path)==row['sha256'])
        with zipfile.ZipFile(path) as archive:
            check('ZIP integrity '+filename,archive.testzip() is None)
            spec=ET.fromstring(archive.read(next(n for n in archive.namelist() if n.endswith('.nuspec'))))
            name=spec.findtext('.//{*}id');version=spec.findtext('.//{*}version')
            check('package identity '+filename,name in names and version==validation['version'] and filename==f'{name}.{version}.nupkg')
            check('assembly payload '+filename,hashlib.sha256(archive.read(f'lib/net10.0/{name}.dll')).hexdigest().upper()==row['dllSha256'])
            if name.endswith('.Skia'):
                deps={d.attrib['id']:d.attrib.get('version') for d in spec.findall('.//{*}dependency')}
                check('exact Skia dependencies',deps.get('Avalonia')=='[12.0.3]' and deps.get('Avalonia.Skia')=='[12.0.3]')
    check('no extra/missing packages',expected=={p.name for p in (bundle/'packages').glob('*.nupkg')})
    for path in bundle.glob('*.md'):
        for link in re.findall(r'\[[^\]]*\]\(([^)]+)\)',path.read_text(encoding='utf-8-sig')):
            url=urllib.parse.urlsplit(link.strip('<>'))
            if url.scheme or not url.path:continue
            target=(path.parent/urllib.parse.unquote(url.path)).resolve()
            check('document link '+path.name+' -> '+link,target.is_relative_to(bundle) and target.exists())
    passed_gates=all(x['passed'] for x in validation['checks']) and not validation['blockers']
    for platform in ('windows','linux'):
        phase=validation['phases'][platform]['soak-v1']
        qualified=phase['status']=='passed' and phase.get('oracle',{}).get('passed') is True and phase.get('oracle',{}).get('soakQualified') is True and phase.get('oracle',{}).get('actualSeconds',0)>=28800
        passed_gates=passed_gates and qualified
    check('private readiness matches recorded gates',validation['readyForPrivateTrial']==passed_gates)
    if require_ready:check('private release ready',passed_gates)
    if archive_path:
        archive_path=pathlib.Path(archive_path).resolve()
        with zipfile.ZipFile(archive_path) as archive:
            entries=[item.filename for item in archive.infolist() if not item.is_dir()]
            prefix=bundle.name+'/'
            expected_archive={prefix+name for name in actual}|{prefix+'SHA256SUMS'}
            check('archive inventory',len(entries)==len(set(entries)) and set(entries)==expected_archive)
            check('archive ZIP integrity',archive.testzip() is None)
            for name in sorted(expected_archive.intersection(entries)):
                check('archive bytes '+name,hashlib.sha256(archive.read(name)).hexdigest().upper()==digest(bundle/name[len(prefix):]))
    return dict(schema='charts-preview-bundle-verification/v1',updatedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),bundle=str(bundle),passed=all(x['passed'] for x in checks),readyForPrivateTrial=validation['readyForPrivateTrial'],requireReady=require_ready,remotePushPerformed=False,checks=checks)


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--bundle',required=True);parser.add_argument('--require-ready',action='store_true');parser.add_argument('--archive');parser.add_argument('--output');args=parser.parse_args()
    bundle=pathlib.Path(args.bundle).resolve()
    if args.output:
        output=pathlib.Path(args.output).resolve()
        if output.is_relative_to(bundle):parser.error('Keep the verification receipt outside the immutable bundle')
        if output.exists():parser.error('New verification output required')
    try:result=verify(bundle,args.require_ready,args.archive)
    except Exception as error:result=dict(schema='charts-preview-bundle-verification/v1',passed=False,error=f'{type(error).__name__}: {error}')
    if args.output:
        output.parent.mkdir(parents=True,exist_ok=True);output.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(dict(passed=result['passed'],checks=len(result.get('checks',[])),failed=[x for x in result.get('checks',[]) if not x['passed']],error=result.get('error')),ensure_ascii=False))
    return 0 if result['passed'] else 1


if __name__=='__main__':raise SystemExit(main())
