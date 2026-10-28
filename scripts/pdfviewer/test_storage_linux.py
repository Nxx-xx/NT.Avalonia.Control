"""Run as root in a PRIVATE mount namespace. Fill only this script's 64 MiB loop image."""
import argparse, errno, hashlib, json, os, pwd, shutil, subprocess, tempfile, time
from pathlib import Path
from storage_evidence import require_space,compress_images
p=argparse.ArgumentParser();p.add_argument('--app',required=True);p.add_argument('--output',required=True);p.add_argument('--dotnet',default='/home/nxx/.dotnet/dotnet');p.add_argument('--uid',type=int,default=1000);a=p.parse_args()
identity=pwd.getpwuid(a.uid);assert a.uid!=0
assert os.geteuid()==0
root=Path(a.output).resolve();assert not root.exists();require_space(root.parent);root.mkdir(parents=True)
app=Path(a.app).resolve();mount=Path(tempfile.mkdtemp(prefix='pdf-p403-loop-'));image=root/'owned-ext4.raw';checks=[];processes=[];mounted=False
def read(p):return json.loads(Path(p).read_text())
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest().upper()
def check(name,ok,**details):checks.append(dict(name=name,passed=bool(ok),**details));assert ok,(name,details)
def drop():os.setgid(identity.pw_gid);os.setuid(a.uid)
def start(args,control):
    control.mkdir(parents=True,exist_ok=True);os.chmod(control,0o777)
    out=(control/'stdout.log').open('wb');err=(control/'stderr.log').open('wb')
    process=subprocess.Popen([a.dotnet,str(app)]+args,stdout=out,stderr=err,preexec_fn=drop,env={**os.environ,'HOME':identity.pw_dir,'DOTNET_SYSTEM_GLOBALIZATION_INVARIANT':'1'})
    record=dict(pid=process.pid,args=args,uid=a.uid);processes.append(record);return process,out,err,record
def finish(owned):
    process,out,err,record=owned
    try:record['exitCode']=process.wait(80);assert process.returncode==0,record
    finally:
        if process.poll() is None:process.kill();process.wait()
        out.close();err.close()
def wait(path,owned):
    end=time.monotonic()+45
    while time.monotonic()<end:
        if path.exists():
            try:return read(path)
            except ValueError:pass
        assert owned[0].poll() is None,'Probe exited before barrier'
        time.sleep(.02)
    raise TimeoutError(path)
def fill():
    info=os.statvfs(mount);check('bounded-owned-ext4',info.f_blocks*info.f_frsize<=64*1024*1024)
    assert subprocess.check_output(['findmnt','-n','-o','FSTYPE','--target',str(mount)],text=True).strip()=='ext4'
    count=0;code=None
    with (mount/'filler.bin').open('wb',buffering=0) as f:
        while True:
            try:count+=f.write(bytes(65536))
            except OSError as e:code=e.errno;break
        try:os.fsync(f.fileno())
        except OSError as e:code=e.errno
    witness=None
    try:
        with (mount/'witness.bin').open('wb',buffering=0) as f:f.write(bytes(131072));os.fsync(f.fileno())
    except OSError as e:witness=e.errno
    check('real-kernel-ENOSPC',code==errno.ENOSPC and witness==errno.ENOSPC,allocated=count,fillErrno=code,witnessErrno=witness)
    return dict(allocated=count,fillErrno=code,witnessErrno=witness,available=os.statvfs(mount).f_bavail*info.f_frsize)
passed=False;failure=None
try:
    with image.open('xb') as f:f.truncate(64*1024*1024)
    subprocess.run(['mkfs.ext4','-q','-F','-m','0',str(image)],check=True)
    subprocess.run(['mount','-o','loop',str(image),str(mount)],check=True);mounted=True;os.chmod(mount,0o777)
    (root/'mount.txt').write_text(subprocess.check_output(['findmnt','--target',str(mount)],text=True))
    for phase in ['none','JournalFlushed','CommitPrepared']:
        directory=mount/phase;control=root/phase
        finish(start(['fixture',str(directory),'storage-large'],control/'fixture'));original=sha(directory/'original.pdf');owned=None
        try:
            if phase=='none':space=fill()
            owned=start(['storage',str(directory),str(control),phase],control)
            if phase!='none':wait(control/'stage.json',owned);space=fill();(control/'release').write_text('go')
            finish(owned);owned=None;result=read(control/'storage-result.json')
            check(phase+'-dirty-unless-saved',result['Passed'] and result['Dirty']!=result['CommitSucceeded'])
            check(phase+'-whole-target',sha(directory/'original.pdf') in [original,result['ExpectedOutputSHA256']])
            (control/'space-witness.json').write_text(json.dumps(space,indent=2))
        finally:
            if owned is not None:
                if owned[0].poll() is None:owned[0].kill()
                owned[0].wait();owned[1].close();owned[2].close()
            for name in ['filler.bin','witness.bin']:(mount/name).unlink(missing_ok=True)
        shutil.copytree(directory,control/'before-recovery')
        finish(start(['recover',str(directory)],control/'recovery'));shutil.copytree(directory,control/'after-recovery')
    passed=True
except BaseException as e:failure=repr(e)
finally:
    if mounted:subprocess.run(['umount',str(mount)],check=True)
    mount.rmdir()
    compress_images(root)
    (root/'result.json').write_text(json.dumps(dict(passed=passed,failure=failure,functionalSHA256=sha(app.parent/'NT.AvaloniaControl.PdfViewer.dll'),hostSHA256=sha(app),checks=checks,processes=processes,unmounted=True),indent=2))
    print(json.dumps(dict(passed=passed,checks=len(checks),failure=failure)))
raise SystemExit(0 if passed else 1)
