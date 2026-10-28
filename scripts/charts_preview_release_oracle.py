"""Independent validation of preview package runs. Original C7 oracle is unchanged."""
import argparse,hashlib,json,pathlib,statistics

def load(path):
    return json.loads(pathlib.Path(path).read_text(encoding='utf-8-sig'))

def validate(raw, samples, execution, require_soak=False):
    errors=[]
    def need(value,message):
        if not value: errors.append(message)
    need(execution.get('passed') is True,'supervised consumer did not pass')
    need(all(x.get('passed') is True and x.get('exitCode')==0 for x in execution.get('stages',[])),'failed/unfinished process receipt')
    need(raw.get('passed') is True and raw.get('status')=='completed','raw consumer incomplete')
    need(raw.get('formalPerformanceQualified') is False and raw.get('physicalHardwareQualified') is False,'preview qualification boundary missing')
    requested=execution.get('seconds',0);options=raw.get('options',{})
    need(options.get('seconds')==requested and options.get('mode')==execution.get('mode'),'requested workload differs')
    need(raw.get('elapsedSeconds',0)>=requested,'actual monotonic duration too short')
    need(raw.get('completedCycles',0)>=execution.get('cycles',0),'lifecycle count too small')
    planned=requested*1000
    need(raw.get('producedSamples')==planned and raw.get('appliedSamples')==planned,'planned input not fully produced and applied')
    stats=raw.get('streamStatistics',{})
    for name in ('acceptedSamples','committedSamples'):need(stats.get(name)==planned,name+' mismatch')
    for name in ('canceledSamples','faultedSamples','rejectedSamples','pendingSamples','pendingBatches','waitingWriters'):need(stats.get(name)==0,name+' not zero')
    need(stats.get('maximumObservedPendingBatches',99)<=2,'unbounded pending batches')
    need(raw.get('nativeWindow',{}).get('nonzeroHandle') is True,'actual native window missing')
    need(raw.get('renderChecks',0)>0 and raw.get('hitChecks',0)>0 and raw.get('exportChecks',0)>=6,'native checks absent')
    binding=raw.get('nativeTextAfterUi',{})
    platform=execution.get('platform')
    if platform=='linux':need(binding.get('status')=='Initialized' and binding.get('resolverObserved') is True,'normal Linux early font binding not exercised')
    elif platform=='windows':need(binding.get('status')=='NotApplicable','unexpected Windows font-binding status')
    else:need(False,'unsupported actual execution platform')
    need(bool(samples),'resource samples absent')
    previous=-1
    for row in samples:
        elapsed=row.get('elapsedSeconds',-1)
        need(elapsed>=previous,'nonmonotonic resource observation')
        previous=elapsed
        buffers=row.get('buffers',[])
        need(len(buffers)==4 and all(b.get('capacity')==16384 and 0<=b.get('count',-1)<=16384 for b in buffers),'buffer bounds violated')
        if len(buffers)==4:need(sum(b['count']+b['evictedCount'] for b in buffers)==4096+row.get('applied',-1),'retained/evicted original counts mismatch')
        need(row.get('statistics',{}).get('committedSamples')==row.get('applied'),'UI committed accounting mismatch')
        cache=row.get('cache',{})
        if 'estimatedResourceCacheBytes' in cache and 'resourceCacheBudgetBytes' in cache:need(cache['estimatedResourceCacheBytes']<=cache['resourceCacheBudgetBytes'],'logical process cache over budget')
    growth=None
    if require_soak:
        need(execution.get('mode')=='soak' and requested>=28800,'full eight-hour request absent')
        need(samples[-1].get('elapsedSeconds',0)>=28800,'eight-hour resource coverage absent')
        need(len(samples)>=requested*.9,'resource observation coverage below 90 percent')
        times=[x.get('elapsedSeconds',-1) for x in samples]
        need(bool(times) and times[0]<=5 and all(b-a<=10 for a,b in zip(times,times[1:])),'resource heartbeat gap exceeds frozen limit')
        first=[x for x in samples if 120<=x.get('elapsedSeconds',0)<420]
        end=samples[-1].get('elapsedSeconds',0)
        last=[x for x in samples if end-300<=x.get('elapsedSeconds',0)<=end]
        need(len(first)>=270 and len(last)>=270,'steady resource windows incomplete')
        growth={}
        if first and last:
            for field in ('managedBytes','privateBytes'):
                need(all(x.get(field,0)>0 for x in first+last),field+' unavailable or nonpositive')
                baseline=statistics.median(x[field] for x in first);terminal=statistics.median(x[field] for x in last)
                allowance=max(33554432,baseline*.05);delta=terminal-baseline
                growth[field]=dict(baselineMedian=baseline,terminalMedian=terminal,delta=delta,allowance=allowance,passed=delta<=allowance)
                need(delta<=allowance,field+' steady growth over frozen allowance')
    return dict(schema='charts-preview-run-oracle/v1',passed=not errors,errors=errors,resourceSamples=len(samples),requestedSeconds=requested,actualSeconds=raw.get('elapsedSeconds'),soakQualified=require_soak and not errors,growth=growth,formalPerformanceQualified=False)

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--execution',required=True);parser.add_argument('--output',required=True);parser.add_argument('--require-soak',action='store_true');a=parser.parse_args()
    path=pathlib.Path(a.execution).resolve();root=path.parent;out=pathlib.Path(a.output)
    if out.exists():raise FileExistsError('New oracle output required')
    execution=load(path);raw=load(root/'run/result.json')
    samples=[json.loads(x) for x in (root/'run/samples.jsonl').read_text(encoding='utf-8-sig').splitlines() if x.strip()]
    result=validate(raw,samples,execution,a.require_soak)
    result['inputs']=[dict(path=str(p),bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest().upper()) for p in (path,root/'run/result.json',root/'run/samples.jsonl')]
    out.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({k:result[k] for k in ('passed','resourceSamples','actualSeconds','soakQualified','errors')},ensure_ascii=False))
    return 0 if result['passed'] else 1

if __name__=='__main__':raise SystemExit(main())
