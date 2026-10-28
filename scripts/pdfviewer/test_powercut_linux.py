"""Disposable QEMU guest power removal. Never kills the WSL/host VM.

The guest has its own kernel/RAM/ext4 and writeback virtual disk with flushes enabled.
SIGKILL of the owned QEMU discards guest RAM without a guest shutdown or sync.
This is a virtual-machine power-loss model, not physical device power removal.
"""
import argparse, gzip, hashlib, json, os, re, selectors, shutil, subprocess, tempfile, time
from pathlib import Path
from storage_evidence import require_space,compress_images
p=argparse.ArgumentParser();p.add_argument('--app',required=True);p.add_argument('--tools',required=True);p.add_argument('--output',required=True);a=p.parse_args()
assert os.geteuid()==0
app=Path(a.app).resolve();tools=Path(a.tools).resolve();root=Path(a.output).resolve();assert not root.exists();require_space(root.parent);root.mkdir(parents=True)
work=Path(tempfile.mkdtemp(prefix='pdf-power-'));init=work/'initroot';init.mkdir();checks=[];vms=[]
kernel=Path('/mnt/c/Program Files/WSL/tools/kernel');version=os.uname().release
env={**os.environ,'LD_LIBRARY_PATH':str(tools/'usr/lib/x86_64-linux-gnu')}
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest().upper()
def check(name,ok,**detail):checks.append(dict(name=name,passed=bool(ok),**detail));assert ok,(name,detail)
def copy_file(source):
    source=Path(source);dest=init/str(source).lstrip('/');dest.parent.mkdir(parents=True,exist_ok=True)
    if not dest.exists():shutil.copy2(source,dest,follow_symlinks=True)
def command(args,**kw):return subprocess.run(args,check=True,**kw)
passed=False;failure=None;owned=None;mount=None
try:
    for name in ['bin','proc','sys','dev','tmp','run','disk','control','root','usr/bin','lib64']: (init/name).mkdir(parents=True,exist_ok=True)
    shutil.copy2(tools/'usr/bin/busybox',init/'bin/busybox')
    for name in ['sh','mount','umount','mkdir','sleep','cat','grep','awk','sha256sum','modprobe','poweroff','dmesg']:(init/'bin'/name).symlink_to('busybox')
    runtime=Path('/home/nxx/.dotnet');(init/'dotnet').mkdir();shutil.copy2(runtime/'dotnet',init/'dotnet/dotnet')
    shutil.copytree(runtime/'host',init/'dotnet/host');shutil.copytree(runtime/'shared/Microsoft.NETCore.App',init/'dotnet/shared/Microsoft.NETCore.App')
    shutil.copytree(app.parent,init/'app')
    # Native dependencies are resolved from the real Linux runtime and PDFium.
    elf=[init/'dotnet/dotnet',*list((init/'dotnet').rglob('*.so')),*list((init/'app/runtimes/linux-x64/native').glob('*.so'))]
    for binary in elf:
        result=subprocess.run(['ldd',str(binary)],capture_output=True,text=True)
        for source in re.findall(r'(?:=>\s*)?(/[^\s()]+)',result.stdout):
            if Path(source).is_file():copy_file(source)
    # OpenSSL is loaded with dlopen by .NET, so it does not appear in ldd.
    for name in ['libssl.so.3','libcrypto.so.3','libzstd.so.1','libz.so.1']:
        copy_file(Path('/usr/lib/x86_64-linux-gnu')/name)
    modules=Path('/lib/modules')/version
    for name in ['modules.dep','modules.alias','modules.builtin','modules.builtin.modinfo']:
        if (modules/name).exists():copy_file(modules/name)
    copy_file(modules/'kernel/drivers/block/virtio_blk.ko')
    script=r'''#!/bin/sh
export PATH=/bin:/usr/bin DOTNET_ROOT=/dotnet HOME=/root DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 DOTNET_EnableDiagnostics=0 LD_LIBRARY_PATH=/usr/lib/x86_64-linux-gnu:/app/runtimes/linux-x64/native
mount -t proc proc /proc
mount -t sysfs sysfs /sys
mount -t devtmpfs devtmpfs /dev
mount -t tmpfs tmpfs /tmp
mount -t tmpfs tmpfs /run
mount -t tmpfs tmpfs /control
modprobe virtio_blk
i=0
while [ ! -e /dev/vda ]; do sleep .1; i=$((i+1)); [ "$i" -gt 100 ] && echo PDF_FAIL_DISK && poweroff -f; done
mount -t ext4 /dev/vda /disk || { echo PDF_FAIL_MOUNT; poweroff -f; }
mode=$(cat /proc/cmdline | awk '{for(i=1;i<=NF;i++)if($i~/^pdf_mode=/){split($i,a,"=");print a[2]}}')
phase=$(cat /proc/cmdline | awk '{for(i=1;i<=NF;i++)if($i~/^pdf_phase=/){split($i,a,"=");print a[2]}}')
if [ "$mode" = cut ]; then
 /dotnet/dotnet /app/PdfViewer.P403.dll storage /disk/case /control "$phase" >/control/app.log 2>&1 &
 pid=$!
 while [ ! -s /control/stage.json ]; do
   if ! kill -0 "$pid" 2>/dev/null; then cat /control/app.log;cat /control/storage-result.json;echo PDF_FAIL_APP;poweroff -f;fi
   sleep .02
 done
 echo PDF_EXPECTED $(sha256sum /control/expected-output.pdf | awk '{print $1}')
 echo PDF_BARRIER "$phase"
 # No sync, unmount, guest kill or shutdown before parent removes VM power.
 while :; do sleep 1; done
else
 /dotnet/dotnet /app/PdfViewer.P403.dll scan /disk/case || { echo PDF_FAIL_SCAN;poweroff -f; }
 /dotnet/dotnet /app/PdfViewer.P403.dll recover /disk/case || { echo PDF_FAIL_RECOVER;poweroff -f; }
 sync
 umount /disk || { echo PDF_FAIL_UNMOUNT;poweroff -f; }
 echo PDF_DONE
 poweroff -f
fi
'''
    (init/'init').write_text(script);os.chmod(init/'init',0o755)
    archive=root/'initramfs.cpio.gz'
    with archive.open('wb') as out:
        find=subprocess.Popen(['find','.','-print0'],cwd=init,stdout=subprocess.PIPE)
        cpio=subprocess.Popen([str(tools/'usr/bin/busybox'),'cpio','--null','-o','-H','newc'],cwd=init,stdin=find.stdout,stdout=subprocess.PIPE,stderr=subprocess.PIPE);find.stdout.close()
        with gzip.GzipFile(fileobj=out,mode='wb',compresslevel=1) as zipped:shutil.copyfileobj(cpio.stdout,zipped)
        cpio.stdout.close();error=cpio.stderr.read();assert cpio.wait()==0,error;assert find.wait()==0
    (root/'environment.json').write_text(json.dumps(dict(kernel=str(kernel),kernelSHA256=sha(kernel),guestKernel=version,qemuVersion=subprocess.check_output([str(tools/'usr/bin/qemu-system-x86_64'),'--version'],env=env,text=True),initramfsSHA256=sha(archive),diskCache='writeback; flush enabled; no snapshot; no unsafe cache',functionalSHA256=sha(app.parent/'NT.AvaloniaControl.PdfViewer.dll')),indent=2))
    def boot(image,mode,phase,case):
        global owned
        log=(case/(mode+'-serial.log')).open('wb')
        cmd=[str(tools/'usr/bin/qemu-system-x86_64'),'-L',str(tools/'usr/share/qemu'),'-bios',str(tools/'usr/share/seabios/bios-256k.bin'),'-accel','kvm','-m','768','-smp','2','-nodefaults','-no-reboot','-display','none','-serial','stdio','-kernel',str(kernel),'-initrd',str(archive),'-append',f'console=ttyS0 rdinit=/init panic=-1 pdf_mode={mode} pdf_phase={phase}', '-drive',f'file={image},format=raw,if=virtio,cache=writeback,aio=threads']
        owned=subprocess.Popen(cmd,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,env=env,bufsize=0)
        record=dict(pid=owned.pid,mode=mode,phase=phase,command=cmd);vms.append(record);data=b'';deadline=time.monotonic()+120;selector=selectors.DefaultSelector();selector.register(owned.stdout,selectors.EVENT_READ)
        try:
            while time.monotonic()<deadline:
                for key,_ in selector.select(.2):
                    chunk=os.read(key.fileobj.fileno(),65536)
                    if chunk:log.write(chunk);log.flush();data+=chunk
                if b'PDF_FAIL' in data:raise AssertionError(data[-5000:].decode(errors='replace'))
                expected=re.search(rb'PDF_EXPECTED ([0-9a-f]{64})',data)
                if mode=='cut' and f'PDF_BARRIER {phase}'.encode() in data:
                    owned.kill();record['supervisorPowerRemoved']=True;record['exitCode']=owned.wait(10);return expected[1].decode().upper()
                if mode=='recover' and b'PDF_DONE' in data:
                    record['exitCode']=owned.wait(15);assert record['exitCode']==0;return
                if owned.poll() is not None:raise AssertionError(data[-5000:].decode(errors='replace'))
            raise TimeoutError('Guest boot/barrier timed out')
        finally:
            if owned.poll() is None:owned.kill();owned.wait(10)
            owned.stdout.close();log.close();selector.close();owned=None
    for phase in ['FileFlushed','Replaced','CommitFlushed']:
        case=root/phase;case.mkdir();image=case/'guest.raw'
        with image.open('xb') as f:f.truncate(64*1024*1024)
        command(['mkfs.ext4','-q','-F','-m','0',str(image)])
        mount=Path(tempfile.mkdtemp(prefix='pdf-guest-disk-'));command(['mount','-o','loop',str(image),str(mount)])
        command(['/home/nxx/.dotnet/dotnet',str(app),'fixture',str(mount/'case')],stdout=subprocess.DEVNULL)
        original=sha(mount/'case/original.pdf');command(['umount',str(mount)]);mount.rmdir();mount=None
        expected=boot(image,'cut',phase,case)
        shutil.copyfile(image,case/'power-cut-before-replay.raw')
        boot(image,'recover',phase,case)
        mount=Path(tempfile.mkdtemp(prefix='pdf-guest-audit-'));command(['mount','-o','loop,ro',str(image),str(mount)])
        shutil.copytree(mount/'case',case/'after-recovery');command(['umount',str(mount)]);mount.rmdir();mount=None
        result=json.loads((case/'after-recovery/recover-result.json').read_text())
        check(phase+'-recovered-complete-output',result['Passed'] and sha(case/'after-recovery/original.pdf')==expected,originalSHA256=original,expectedSHA256=expected)
        check(phase+'-journal-reconciled',result['After']==[])
    passed=True
except BaseException as e:failure=repr(e)
finally:
    if owned is not None and owned.poll() is None:owned.kill();owned.wait(10)
    if mount is not None:subprocess.run(['umount',str(mount)])
    # Only this script's mkdtemp tree, checked before recursive cleanup.
    assert work.parent==Path(tempfile.gettempdir()) and work.name.startswith('pdf-power-');shutil.rmtree(work)
    compress_images(root)
    (root/'result.json').write_text(json.dumps(dict(passed=passed,failure=failure,checks=checks,vms=vms,functionalSHA256=sha(app.parent/'NT.AvaloniaControl.PdfViewer.dll'),boundary='Guest power loss; physical controller/device and Windows VM power removal are not validated.'),indent=2));print(json.dumps(dict(passed=passed,checks=len(checks),failure=failure)))
raise SystemExit(0 if passed else 1)
