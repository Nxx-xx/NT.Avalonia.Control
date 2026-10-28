"""Real non-cooperating child writers at production publication barriers."""
import argparse, hashlib, json, os, shutil, subprocess, sys, time
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--app',required=True);p.add_argument('--output',required=True);p.add_argument('--dotnet',default='dotnet');a=p.parse_args()
root=Path(a.output).resolve();assert not root.exists();root.mkdir(parents=True)
app=Path(a.app).resolve();checks=[];processes=[]
def read(p):return json.loads(Path(p).read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest().upper()
def check(name,ok):checks.append(dict(name=name,passed=bool(ok)));assert ok,name
def start(args,control):
    control.mkdir(parents=True,exist_ok=True);out=(control/'stdout.log').open('wb');err=(control/'stderr.log').open('wb')
    process=subprocess.Popen(args,stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW if os.name=='nt' else 0)
    record=dict(pid=process.pid,command=args);processes.append(record);return process,out,err,record
def finish(owned):
    process,out,err,record=owned
    try:record['exitCode']=process.wait(70);assert process.returncode==0,record
    finally:
        if process.poll() is None:process.kill();process.wait()
        out.close();err.close()
def run(mode,directory,control):finish(start([a.dotnet,str(app),mode,str(directory)],control))
def wait(path,owned):
    end=time.monotonic()+45
    while time.monotonic()<end:
        if path.exists():
            try:return read(path)
            except (ValueError,PermissionError):pass
        assert owned[0].poll() is None,'Probe exited before barrier'
        time.sleep(.02)
    raise TimeoutError(path)
passed=False;failure=None
try:
    for case in ['rewrite','replace','create','post-publication']:
        directory=root/case;control=root/(case+'-control');directory.mkdir()
        run('fixture',directory,control/'fixture');original=sha(directory/'original.pdf');external=sha(directory/'external.pdf')
        target=directory/('new.pdf' if case=='create' else 'original.pdf');stage='Replaced' if case=='post-publication' else 'CommitPrepared'
        owned=start([a.dotnet,str(app),'storage',str(directory),str(control),stage,target.name],control)
        try:
            wait(control/'stage.json',owned)
            # A separately spawned process never acquires the library's lock.
            code="import os,sys;from pathlib import Path;s=Path(sys.argv[1]);t=Path(sys.argv[2]);b=s.read_bytes();"+('q=t.with_suffix(".external");q.write_bytes(b);os.replace(q,t)' if case=='replace' else 't.write_bytes(b)')
            finish(start([sys.executable,'-c',code,str(directory/'external.pdf'),str(target)],control/'external-process'))
            (control/'release').write_text('go');finish(owned);owned=None
            result=read(control/'storage-result.json');check(case+'-typed-dirty-conflict',not result['CommitSucceeded'] and result['Dirty'] and result['ErrorType'].endswith('PdfSaveConflictException'))
            check(case+'-publication-status',result['WasPublished']==(case!='create'))
            check(case+'-complete-target',sha(target)==(external if case in ['create','post-publication'] else result['ExpectedOutputSHA256']))
            if case!='create':
                txn=Path(result['RecoveryDirectory']);check(case+'-output-retained',sha(txn/'output.pdf')==result['ExpectedOutputSHA256'])
                check(case+'-external-retained',sha(Path(result['PreservedExternalPath']))==(original if case=='post-publication' else external))
                check(case+'-scan-published-conflict',result['Recoveries'][0]['Status']==6)
                run('attempt-recover',directory,control/'recovery');check(case+'-auto-recovery-refused',not read(directory/'attempt-recover-result.json')['Recovered'])
        finally:
            if owned is not None:
                if owned[0].poll() is None:owned[0].kill()
                owned[0].wait();owned[1].close();owned[2].close()
    passed=True
except BaseException as e:failure=repr(e)
finally:
    record=dict(passed=passed,failure=failure,functionalSHA256=sha(app.parent/'NT.AvaloniaControl.PdfViewer.dll'),hostSHA256=sha(app),checks=checks,processes=processes)
    (root/'result.json').write_text(json.dumps(record,indent=2));print(json.dumps(dict(passed=passed,checks=len(checks),failure=failure)))
sys.exit(0 if passed else 1)
