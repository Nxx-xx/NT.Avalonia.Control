"""Private mount AND network namespace only. Real NFS TCP link removal on private lo.
Requires root, portable nfs-ganesha-vfs/nfs-common; never alters host networking.
"""
import argparse, hashlib, json, os, shutil, subprocess, tempfile, time
from pathlib import Path
from storage_evidence import require_space,compress_images
p=argparse.ArgumentParser();p.add_argument('--app',required=True);p.add_argument('--tools',required=True);p.add_argument('--output',required=True);a=p.parse_args()
assert os.geteuid()==0
root=Path(a.output).resolve();assert not root.exists();require_space(root.parent);root.mkdir(parents=True);app=Path(a.app).resolve();tools=Path(a.tools).resolve()
work=Path(tempfile.mkdtemp(prefix='pdf-nfs-'));export=work/'export';client=work/'client';export.mkdir();client.mkdir();os.chmod(work,0o755)
checks=[];processes=[];server=None;rpc=None;disk_mounted=False;nfs_mounted=False;owned=None
env={**os.environ,'LD_LIBRARY_PATH':':'.join(str(tools/x) for x in ['usr/lib/ganesha','usr/lib/x86_64-linux-gnu']),'HOME':'/home/nxx','DOTNET_SYSTEM_GLOBALIZATION_INVARIANT':'1'}
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest().upper()
def check(name,ok,**details):checks.append(dict(name=name,passed=bool(ok),**details));assert ok,(name,details)
def cmd(args,**kw):return subprocess.run(args,check=True,**kw)
def drop():os.setgid(1000);os.setuid(1000)
def start(args,control):
    control.mkdir(parents=True,exist_ok=True);os.chmod(control,0o777)
    out=(control/'stdout.log').open('wb');err=(control/'stderr.log').open('wb');proc=subprocess.Popen(['/home/nxx/.dotnet/dotnet',str(app)]+args,stdout=out,stderr=err,env=env,preexec_fn=drop)
    record=dict(pid=proc.pid,args=args,uid=1000);processes.append(record);return proc,out,err,record
def finish(owned):
    proc,out,err,record=owned
    try:record['exitCode']=proc.wait(75);assert proc.returncode==0,record
    finally:
        if proc.poll() is None:proc.kill();proc.wait()
        out.close();err.close()
def read(path):return json.loads(Path(path).read_text())
def wait(path,owned):
    end=time.monotonic()+60
    while time.monotonic()<end:
        if path.exists():
            try:return read(path)
            except ValueError:pass
        assert owned[0].poll() is None,('Probe exited',path)
        time.sleep(.03)
    raise TimeoutError(path)
passed=False;failure=None
try:
    cmd(['ip','link','set','lo','up'])
    # Isolate RPC's pathname sockets and pid files as well as its TCP/UDP ports.
    (work/'run').mkdir();cmd(['mount','--bind',str(work/'run'),'/run'])
    (work/'run/rpcbind').mkdir();os.chown(work/'run/rpcbind',65534,65534)
    # The extracted package has not installed its service account. Supply it only
    # in this mount namespace; do not edit the host account database.
    passwd=work/'passwd';passwd.write_text(Path('/etc/passwd').read_text()+'\n_rpc:x:65534:65534:Private RPC:/run/rpcbind:/usr/sbin/nologin\n')
    cmd(['mount','--bind',str(passwd),'/etc/passwd'])
    rpclog=(root/'rpcbind.log').open('wb')
    rpc=subprocess.Popen([str(tools/'usr/sbin/rpcbind'),'-f','-d','-h','127.0.0.1'],env=env,stdout=rpclog,stderr=subprocess.STDOUT)
    time.sleep(.3);assert rpc.poll() is None,('Private rpcbind startup failed',rpc.returncode)
    image=root/'owned-export.raw'
    with image.open('xb') as f:f.truncate(64*1024*1024)
    cmd(['mkfs.ext4','-q','-F','-m','0',str(image)]);cmd(['mount','-o','loop',str(image),str(export)]);disk_mounted=True;os.chmod(export,0o777)
    config=root/'ganesha.conf'
    config.write_text(f'''NFS_CORE_PARAM {{ Protocols = 4; NFS_Port = 2049; Enable_UDP = false; Enable_NLM = false; Plugins_Dir = {tools}/usr/lib/x86_64-linux-gnu/ganesha; }}
NFSv4 {{ Grace_Period = 0; RecoveryRoot = {work}/recovery; UseGetpwnam = true; }}
DIRECTORY_SERVICES {{ DomainName = localdomain; Idmapping_Active = true; }}
EXPORT {{ Export_Id = 77; Path = {export}; Pseudo = /test; Access_Type = RW; Squash = No_Root_Squash; Protocols = 4; Transports = TCP; SecType = sys; FSAL {{ Name = VFS; }} }}
LOG {{ Default_Log_Level = EVENT; Facility {{ name = FILE; destination = {root}/ganesha.log; enable = active; }} }}
''')
    (work/'recovery').mkdir();log=(root/'server-stdout.log').open('wb')
    server=subprocess.Popen([str(tools/'usr/bin/ganesha.nfsd'),'-F','-f',str(config),'-p',str(work/'ganesha.pid')],env=env,stdout=log,stderr=subprocess.STDOUT)
    time.sleep(2);assert server.poll() is None,'Private NFS server failed; inspect ganesha.log'
    mountcmd=[str(tools/'usr/sbin/mount.nfs'),'-v','127.0.0.1:/test',str(client),'-o','vers=4.1,proto=tcp,port=2049,soft,timeo=2,retrans=1,noac']
    with (root/'mount.log').open('wb') as mountlog:cmd(mountcmd,timeout=30,stdout=mountlog,stderr=subprocess.STDOUT)
    nfs_mounted=True
    (root/'mount.txt').write_text(subprocess.check_output(['findmnt','--target',str(client)],text=True))
    # NFS cannot supply RENAME_EXCHANGE. Verify an original-path replacement is refused.
    directory=client/'unsupported-exchange';control=root/'unsupported-exchange'
    finish(start(['fixture',str(directory)],control/'fixture'));original=sha(export/'unsupported-exchange/original.pdf')
    finish(start(['storage',str(directory),str(control),'none'],control));result=read(control/'storage-result.json')
    check('NFS-existing-target-refused',not result['CommitSucceeded'] and result['Dirty'] and result['ErrorType'].endswith('NotSupportedException'))
    check('NFS-refusal-preserves-original',sha(export/'unsupported-exchange/original.pdf')==original)
    shutil.copytree(export/'unsupported-exchange',control/'evidence')
    for phase in ['BeforeFlush','CommitPrepared']:
        directory=client/phase;control=root/phase;physical=export/phase
        finish(start(['fixture',str(directory),'storage-large'],control/'fixture'));original=sha(physical/'original.pdf')
        control.mkdir(exist_ok=True);(control/'wait-audit').write_text('yes')
        owned=start(['storage',str(directory),str(control),phase,'new.pdf'],control)
        wait(control/'stage.json',owned)
        cmd(['ip','link','set','lo','down']);(root/(phase+'-link-down.txt')).write_text(subprocess.check_output(['ip','-details','link','show','lo'],text=True))
        disconnected=time.monotonic();(control/'release').write_text('go')
        # NFSv4 can wait for state recovery even with a soft data mount. This is
        # a bounded transient disconnect, not a promise of cancellable kernel I/O.
        time.sleep(3)
        check(phase+'-no-success-while-disconnected',not (control/'operation.json').exists())
        cmd(['ip','link','set','lo','up']);result=wait(control/'operation.json',owned)
        check(phase+'-completion-state-after-reconnect',result['Dirty']!=result['CommitSucceeded'],elapsedSeconds=time.monotonic()-disconnected,error=result.get('ErrorType'))
        (control/'audit-release').write_text('go');finish(owned);owned=None
        check(phase+'-source-unchanged',sha(physical/'original.pdf')==original)
        check(phase+'-whole-new-target',not (physical/'new.pdf').exists() or sha(physical/'new.pdf')==result['ExpectedOutputSHA256'])
        shutil.copytree(physical,control/'before-recovery')
        finish(start(['recover',str(directory)],control/'recovery'));shutil.copytree(physical,control/'after-recovery')
    passed=True
except BaseException as e:failure=repr(e)
finally:
    subprocess.run(['ip','link','set','lo','up'])
    if owned is not None:
        if owned[0].poll() is None:owned[0].kill()
        owned[0].wait();owned[1].close();owned[2].close()
    if nfs_mounted:subprocess.run(['umount','-f',str(client)],timeout=20)
    if server is not None:
        server.terminate()
        try:server.wait(10)
        except subprocess.TimeoutExpired:server.kill();server.wait()
        log.close()
    if disk_mounted:subprocess.run(['umount',str(export)],timeout=20)
    if rpc is not None:
        rpc.terminate();rpc.wait(10);rpclog.close()
    subprocess.run(['umount','/run'])
    subprocess.run(['umount','/etc/passwd'])
    assert work.parent==Path(tempfile.gettempdir()) and work.name.startswith('pdf-nfs-');shutil.rmtree(work)
    compress_images(root)
    (root/'result.json').write_text(json.dumps(dict(passed=passed,failure=failure,checks=checks,processes=processes,functionalSHA256=sha(app.parent/'NT.AvaloniaControl.PdfViewer.dll'),namespaceNetwork=True),indent=2));print(json.dumps(dict(passed=passed,checks=len(checks),failure=failure)))
raise SystemExit(0 if passed else 1)
