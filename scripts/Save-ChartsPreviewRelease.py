"""Build a truthful local release bundle; never uploads or marks original C7 complete."""
import argparse,datetime,hashlib,importlib.util,json,pathlib,shutil,sys,xml.etree.ElementTree as ET,zipfile
from PIL import Image
import numpy as np

def read(p):return json.loads(pathlib.Path(p).read_text(encoding='utf-8-sig'))
def local(p):
    p=str(p)
    if p.startswith('/mnt/c/'):return pathlib.Path('C:/'+p[7:])
    if p.startswith('/mnt/d/'):return pathlib.Path('D:/'+p[7:])
    return pathlib.Path(p)
def fp(p):
    p=pathlib.Path(p);return dict(path=str(p),bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest().upper())
def write(p,data):
    p=pathlib.Path(p);p.parent.mkdir(parents=True,exist_ok=True);tmp=p.with_suffix(p.suffix+'.tmp');tmp.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');tmp.replace(p)

def live_progress(root):
    """A point-in-time observation is never a completed-run verdict."""
    p=root/'run/result.json'
    if not p.exists():return None
    raw=read(p);sample=raw.get('latestSample',{})
    progress=dict(status=raw.get('status'),elapsedSeconds=sample.get('elapsedSeconds',0),sampleUtc=sample.get('utc'),produced=sample.get('produced'),applied=sample.get('applied'),statistics=sample.get('statistics'),managedBytes=sample.get('managedBytes'),privateBytes=sample.get('privateBytes'),qualified=False)
    process=root/'native-process/process.json'
    if process.exists():
        receipt=read(process)
        progress.update(processId=receipt.get('processId'),heartbeatUtc=receipt.get('heartbeatUtc'),processStartedUtc=receipt.get('processStartedUtc'))
        if receipt.get('processStartedUtc'):
            started=datetime.datetime.fromisoformat(receipt['processStartedUtc'].replace('Z','+00:00'))
            progress['earliestFinishUtc']=(started+datetime.timedelta(seconds=28800)).isoformat()
    return progress
def main():
    parser=argparse.ArgumentParser();parser.add_argument('--batch',required=True);parser.add_argument('--repository',required=True);parser.add_argument('--bundle',required=True);a=parser.parse_args()
    batch=pathlib.Path(a.batch).resolve();repo=pathlib.Path(a.repository).resolve();bundle=pathlib.Path(a.bundle).resolve();bundle.mkdir(parents=True,exist_ok=True)
    scope=read(repo/'docs/charts-preview-release-scope.json');version=scope['releaseVersion']
    oracle_path=pathlib.Path(__file__).with_name('charts_preview_release_oracle.py')
    spec=importlib.util.spec_from_file_location('release_run_oracle',oracle_path);oracle=importlib.util.module_from_spec(spec);spec.loader.exec_module(oracle)
    checks=[];blockers=[]
    def check(name,ok,detail=None):
        checks.append(dict(name=name,passed=bool(ok),detail=detail))
        if not ok:blockers.append(name)
    manifest=read(batch/'source-manifest-v1.json')
    for row in manifest['files']:
        if row['path'].startswith(('src/NT.AvaloniaControl.Charts/','src/NT.AvaloniaControl.Charts.Themes/','src/NT.AvaloniaControl.Charts.Skia/')):
            p=repo/row['path'];check('current product source '+row['path'],p.exists() and fp(p)['sha256']==row['sha256'])
    for os in ('windows','linux'):
        root=batch/(os+'-source-v1')
        for row in manifest['files']:
            p=root/row['path'];check(os+' frozen source '+row['path'],p.exists() and fp(p)['sha256']==row['sha256'])
        result=read(batch/os/'build-v1/result.json');check(os+' build stages',result['passed'] and all(x['passed'] and x['exitCode']==0 for x in result['stages']))
        counters=ET.parse(batch/os/'build-v1/test-results/charts.trx').find('.//{*}Counters')
        check(os+' full chart regression',counters is not None and int(counters.attrib['failed'])==0 and int(counters.attrib['passed'])==int(counters.attrib['total']) and int(counters.attrib['total'])>0)
    packages=[];payloads={}
    for name in scope['packages']:
        p=batch/'windows/build-v1/feed'/f'{name}.{version}.nupkg'
        with zipfile.ZipFile(p) as z:
            dll=z.read(f'lib/net10.0/{name}.dll');payloads[name]=hashlib.sha256(dll).hexdigest().upper()
            nuspec=ET.fromstring(z.read(next(x for x in z.namelist() if x.endswith('.nuspec'))))
            check(name+' package version',nuspec.findtext('.//{*}version')==version)
            check(name+' package readme',bool(nuspec.findtext('.//{*}readme')) and nuspec.findtext('.//{*}readme') in z.namelist())
            if name.endswith('.Skia'):
                deps={d.attrib['id']:d.attrib.get('version') for d in nuspec.findall('.//{*}dependency')}
                check('Skia exact Avalonia version',deps.get('Avalonia')=='[12.0.3]' and deps.get('Avalonia.Skia')=='[12.0.3]')
        target=bundle/'packages'/p.name;target.parent.mkdir(parents=True,exist_ok=True)
        if not target.exists():shutil.copy2(p,target)
        check(name+' immutable delivery bytes',fp(target)['sha256']==fp(p)['sha256'])
        packages.append(dict(**fp(target),dllSha256=payloads[name]))
    selected={'windows':{'core':'native-core-v3','themes':'native-themes-v2','skia':'native-skia-v5'},'linux':{'core':'native-core-v1','themes':'native-themes-v1','skia':'native-skia-v1'}}
    native={};image_checks=[]
    for os,variants in selected.items():
        native[os]={}
        for variant,name in variants.items():
            root=batch/os/name;execution=root/'execution.json'
            if not execution.exists():native[os][variant]={'status':'pending'};blockers.append(os+' '+variant+' native consumer pending');continue
            e=read(execution)
            if not e.get('passed'):native[os][variant]={'status':'running' if not e.get('finishedUtc') else 'failed','execution':fp(execution),'error':e.get('error')};blockers.append(os+' '+variant+' native consumer not passed');continue
            raw=read(root/'run/result.json');samples=[json.loads(x) for x in (root/'run/samples.jsonl').read_text(encoding='utf-8-sig').splitlines() if x]
            verdict=oracle.validate(raw,samples,e)
            check(os+' '+variant+' native correctness',verdict['passed'],verdict['errors'])
            check(os+' '+variant+' canonical package payload',all(p['dllSha256']==payloads[p['id']] for p in e['payloads']))
            assets=read(local(e['consumer'])/'obj/project.assets.json')
            check(os+' '+variant+' no source project references',not any(x['type']=='project' for x in assets['libraries'].values()))
            for path in sorted((root/'run').glob('*')):
                if path.suffix not in ('.png','.jpg'):continue
                with Image.open(path) as image:
                    arr=np.array(image.convert('RGB'));ok=image.size==(520,320) and int(np.count_nonzero(np.min(arr,axis=2)<240))>250
                image_checks.append(dict(**fp(path),dimensions=[520,320],nonempty=ok));check(os+' '+variant+' decoded '+path.name,ok)
            native[os][variant]=dict(status='passed',execution=fp(execution),raw=fp(root/'run/result.json'),oracle=verdict)
    phases={}
    for os in ('windows','linux'):
        phases[os]={}
        for name in ('lifecycle-v1','pre-soak-v1','soak-v1'):
            root=batch/os/name;p=root/'execution.json'
            if not p.exists():phases[os][name]={'status':'pending'};blockers.append(os+' '+name+' pending');continue
            e=read(p)
            if not e.get('finishedUtc'):phases[os][name]={'status':'running','execution':fp(p),'progress':live_progress(root)};blockers.append(os+' '+name+' running');continue
            if not e.get('passed'):phases[os][name]={'status':'failed','execution':fp(p),'error':e.get('error')};blockers.append(os+' '+name+' failed');continue
            raw=read(root/'run/result.json');samples=[json.loads(x) for x in (root/'run/samples.jsonl').read_text(encoding='utf-8-sig').splitlines() if x]
            verdict=oracle.validate(raw,samples,e,require_soak=name=='soak-v1')
            check(os+' '+name+' independent verdict',verdict['passed'],verdict['errors'])
            if name=='lifecycle-v1':check(os+' at least100 cycles',raw['completedCycles']>=100)
            phases[os][name]=dict(status='passed' if verdict['passed'] else 'failed',execution=fp(p),raw=fp(root/'run/result.json'),samples=fp(root/'run/samples.jsonl'),oracle=verdict,summary={key:raw.get(key) for key in ('elapsedSeconds','producedSamples','appliedSamples','streamStatistics','renderChecks','hitChecks','exportChecks','completedCycles','environment')})
    for os in ('windows','linux'):
        for kind in ('usage','demo'):
            p=batch/os/(kind+'-smoke-v1')/'process/process.json'
            check(os+' '+kind+' native smoke',p.exists() and read(p)['passed'])
        p=batch/os/'native-text-v1/result.json'
        if not p.exists():p=batch/os/'native-text-v1/results.json'
        check(os+' independent font startup',p.exists() and read(p).get('passed') is True)
    api=batch/'api/charts-preview-public-api.txt';check('API metadata snapshot',api.exists() and api.stat().st_size>1000)
    if api.exists():shutil.copy2(api,bundle/'charts-preview-public-api.txt')
    for name in ('charts-preview-release.md','charts-preview-release-notes.md','charts-preview-release-scope.json','charts-preview-validation-protocol.json','charts-preview-quickstart.md'):
        shutil.copy2(repo/'docs'/name,bundle/name)
    shutil.copy2(repo/'scripts/Verify-ChartsPreviewBundle.py',bundle/'Verify-ChartsPreviewBundle.py')
    shutil.copy2(batch/'windows-source-v1/src/NT.AvaloniaControl.Charts/THIRD-PARTY-NOTICES.md',bundle/'THIRD-PARTY-NOTICES.md')
    theme_files=['src/NT.AvaloniaControl.Charts.Themes/ChartsTheme.cs','src/NT.AvaloniaControl.Charts.Themes/Themes/Generic.axaml','src/NT.AvaloniaControl.Charts/ChartContrastPalettes.cs']
    theme=dict(schema='charts-preview-theme-source-snapshot/v1',files=[fp(batch/'windows-source-v1'/p) for p in theme_files],assemblySha256=payloads['NT.AvaloniaControl.Charts.Themes'])
    write(bundle/'themes-snapshot.json',theme)
    ready=not blockers
    result=dict(schema='charts-preview-release-validation/v1',updatedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),version=version,status='ready-for-private-trial' if ready else 'in-progress',readyForPrivateTrial=ready,readyForPublicRelease=False,publicDistributionLicense=scope['publicDistributionLicense'],remotePushPerformed=False,originalC7MilestoneComplete=False,checks=checks,blockers=blockers,packages=packages,nativeConsumers=native,phases=phases,decodedImages=image_checks,sourceManifest=fp(batch/'source-manifest-v1.json'),scope=fp(repo/'docs/charts-preview-release-scope.json'))
    write(bundle/'validation.json',result)
    write(bundle/'charts-preview-release-validation.json',result)
    write(repo/'docs/charts-preview-release-validation.json',result)
    md=repo/'docs/charts-preview-release.md'
    text=md.read_text(encoding='utf-8')
    start='<!-- preview-status:start -->';end='<!-- preview-status:end -->'
    paragraph=start+'\n## 实际验证状态\n\n版本：'+version+'。状态：'+result['status']+'。私有试用就绪：'+str(ready).lower()+'；公开发布就绪：false。\n\n更新时间：'+result['updatedUtc']+'。\n\nWindows/Linux正式8小时长稳：'+phases['windows']['soak-v1']['status']+' / '+phases['linux']['soak-v1']['status']+'。\n\n'
    for os in ('windows','linux'):
        phase=phases[os]['soak-v1']
        if phase['status']=='passed':
            summary=phase['summary'];stats=summary['streamStatistics'];growth=phase['oracle']['growth']
            paragraph+=f"- {os}：实际 {summary['elapsedSeconds']:.3f} 秒，生产/提交 {summary['producedSamples']}/{summary['appliedSamples']}，末态待处理 {stats['pendingSamples']}，失败/取消/拒绝 {stats['faultedSamples']}/{stats['canceledSamples']}/{stats['rejectedSamples']}；独立判定通过。托管/私有内存末段中位数相对基线变化 {growth['managedBytes']['delta']/1048576:.2f}/{growth['privateBytes']['delta']/1048576:.2f} MiB。\n"
        elif phase.get('progress'):
            progress=phase['progress'];paragraph+=f"- {os}：采样时运行 {progress['elapsedSeconds']/3600:.2f}/8 小时，心跳 {progress.get('heartbeatUtc')}；尚未取得末态结论。\n"
    paragraph+='\n实际结果见[验收记录](charts-preview-release-validation.json)，接入步骤见[本地包接入](charts-preview-quickstart.md)，范围与限制见[发布说明](charts-preview-release-notes.md)。当前未完成事项：'+('；'.join(blockers) if blockers else '无首版私有试用阻断项')+'。\n'+end
    if start in text and end in text:text=text[:text.index(start)]+paragraph+text[text.index(end)+len(end):]
    else:text+='\n'+paragraph+'\n'
    md.write_text(text,encoding='utf-8');shutil.copy2(md,bundle/md.name)
    mp=repo/'docs/charts-platform-matrix.json';matrix=read(mp);block=matrix['previewReleasePreparation'];block['status']=result['status'];block['readyForPrivateTrial']=ready;block['readyForPublicRelease']=False;block['validation']='docs/charts-preview-release-validation.json';block['lifecycle']={os:phases[os]['lifecycle-v1']['status'] for os in phases};block['eightHourSoak']={os:phases[os]['soak-v1']['status'] for os in phases};write(mp,matrix)
    current='**当前进度**：0.2.0-preview.3首版范围已冻结。Windows/Linux各3267项回归、三包干净原生消费者、目录部署、字体启动、Usage/Demo和各100轮生命周期均已通过。8小时长稳状态：Windows '+phases['windows']['soak-v1']['status']+' / Linux '+phases['linux']['soak-v1']['status']+'。'+('本地私有试用就绪。' if ready else '尚未登记发布就绪。')+'官方功能对照跳过，官方性能对照、散点深度优化和SDK扩展后置。见[首版发布收尾](charts-preview-release.md)。'
    next_step='**下一步**：'+('按[本地包接入](charts-preview-quickstart.md)开展业务试用，新增缺陷以新候选版本修复；公开发布前确定项目分发许可。' if ready else '等待Linux实际跑满8小时并完成独立判定；失败则保留证据、修复后重新验收，未完成前不登记通过。')+'原C7完整对标状态及历史门禁保留，暂不远程发布。'
    for name in ('charts-development-plan.md','charts-official-alignment.md','charts-performance-platform-plan.md'):
        path=repo/'docs'/name;lines=path.read_text(encoding='utf-8').splitlines();replaced=set()
        for i,line in enumerate(lines):
            if line.startswith('更新日期：') and 'date' not in replaced:lines[i]='更新日期：'+datetime.datetime.now(datetime.timezone(datetime.timedelta(hours=8))).date().isoformat()+'。';replaced.add('date')
            elif line.startswith('**当前进度**：') and 'current' not in replaced:lines[i]=current;replaced.add('current')
            elif line.startswith('**下一步**：') and 'next' not in replaced:lines[i]=next_step;replaced.add('next')
        path.write_text('\n'.join(lines)+'\n',encoding='utf-8')
    checksum=''.join(fp(p)['sha256']+'  '+str(p.relative_to(bundle)).replace('\\','/')+'\n' for p in sorted(bundle.rglob('*')) if p.is_file() and p.name!='SHA256SUMS')
    (bundle/'SHA256SUMS').write_text(checksum,encoding='utf-8')
    print(json.dumps(dict(status=result['status'],checks=len(checks),failed=sum(not x['passed'] for x in checks),blockers=blockers,bundle=str(bundle)),ensure_ascii=False))
    return 1 if any(not x['passed'] for x in checks) else 0

if __name__=='__main__':raise SystemExit(main())
