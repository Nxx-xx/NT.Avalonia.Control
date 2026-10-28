"""Owned-process save fault / abrupt termination supervisor. Standard library only.

This tests production APIs; hooks are internal friend-assembly hooks, never PDF / environment triggers.
Independent pypdf / Poppler checks are performed separately on the preserved output PDFs.
"""
import argparse, datetime, hashlib, json, os, platform, shutil, subprocess, time
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('--app', required=True)
parser.add_argument('--output', required=True)
parser.add_argument('--dotnet', default='dotnet')
args = parser.parse_args()
app = Path(args.app).resolve()
root = Path(args.output).resolve()
assert not root.exists(), 'Use a fresh evidence directory.'
root.mkdir(parents=True)
processes, checks = [], []
started = datetime.datetime.now(datetime.timezone.utc).isoformat()

def digest(path): return hashlib.sha256(Path(path).read_bytes()).hexdigest().upper()
def read(path): return json.loads(Path(path).read_text(encoding='utf-8-sig'))
def check(name, condition, **details):
    checks.append({'name':name,'passed':bool(condition),**details})
    assert condition, (name, details)
def start(mode, directory, scenario=''):
    command = [args.dotnet,str(app),mode,str(directory),scenario]
    stdout = directory / (mode+'-'+scenario+'.stdout.log')
    stderr = directory / (mode+'-'+scenario+'.stderr.log')
    directory.mkdir(exist_ok=True)
    flags = subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0
    out, err = stdout.open('wb'), stderr.open('wb')
    process = subprocess.Popen(command,stdout=out,stderr=err,creationflags=flags)
    processes.append({'command':command,'pid':process.pid,'stdout':str(stdout.relative_to(root)),
                      'stderr':str(stderr.relative_to(root)),'startedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat()})
    return process,out,err,processes[-1]
def finish(owned, killed=False):
    process,out,err,record = owned
    try:
        if killed:
            assert process.poll() is None
            process.kill() # Only the PID returned by this supervisor's Popen.
        code = process.wait(timeout=55)
        record.update(exitCode=code,supervisorKilled=killed,completedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat())
        if not killed: assert code == 0, record
    finally:
        if process.poll() is None: process.kill(); process.wait(timeout=10)
        out.close(); err.close()
def run(mode,directory,scenario=''):
    finish(start(mode,directory,scenario))
    result = read(directory/(mode+'-result.json'))
    assert result['Passed'], result
    return result
def prepare(name,scenario=''):
    directory=root/name
    run('fixture',directory,scenario)
    return directory,digest(directory/'original.pdf')
def paused(directory,scenario):
    owned = start('save',directory,scenario)
    deadline=time.monotonic()+40
    while not (directory/'stage.json').exists():
        if owned[0].poll() is not None: finish(owned); raise AssertionError('Child exited before the save barrier.')
        if time.monotonic()>deadline: finish(owned,True); raise TimeoutError(scenario)
        time.sleep(.02)
    # Signal is published before Flush closes the file. Wait until the complete JSON is readable.
    while True:
        try: signal=read(directory/'stage.json'); break
        except (json.JSONDecodeError,PermissionError): time.sleep(.01)
    assert signal['ProcessId']==owned[0].pid
    return owned

passed=False
failure=None
try:
    scenarios=['normal','disk-full','flush-failure','mount-unavailable','cancel','corrupt-candidate',
               'late-edit','external-replace','external-delete','postcommit-failure','target-lock','readonly-target']
    if os.name != 'nt': scenarios.append('readonly-parent')
    for scenario in scenarios:
        directory,original=prepare('fault-'+scenario)
        external=digest(directory/'external.pdf')
        result=run('save',directory,scenario)
        successful=scenario in ('normal','postcommit-failure') or (scenario=='readonly-target' and os.name!='nt')
        check(scenario+'-commit-and-dirty', result['CommitSucceeded']==successful and result['Dirty']!=successful)
        expected=result['ExpectedOutputSHA256'] if successful else external if scenario in ('late-edit','external-replace') else None if scenario=='external-delete' else original
        check(scenario+'-target-bytes', result['TargetSHA256']==expected)
        if scenario in ('late-edit','external-replace','external-delete'):
            check(scenario+'-conflict-kind',result['ErrorType'].endswith('PdfSaveConflictException'))
        if scenario=='postcommit-failure':
            recovered=run('recover',directory)
            check(scenario+'-reconcile', recovered['TargetSHA256']==expected and recovered['After']==[])

    for stage in ('JournalFlushed','WriteChunk','BeforeFlush','FileFlushed','Validated','BeforeReplace','CommitPrepared','Replaced','CommitFlushed'):
        scenario='crash-'+stage
        directory,original=prepare(scenario,scenario)
        owned=paused(directory,scenario)
        expected=digest(directory/'expected-output.pdf')
        finish(owned,True)
        scanned=run('scan',directory)
        status=scanned['Entries'][0]['Status']
        expected_status=(1,) if stage in ('JournalFlushed','WriteChunk') else (1,2) if stage=='BeforeFlush' else (7,) if stage=='Replaced' else (3,) if stage=='CommitFlushed' else (2,)
        check(scenario+'-restart-status',status in expected_status,observed=status,allowed=expected_status)
        check(scenario+'-atomic-target',digest(directory/'original.pdf')==(expected if stage in ('Replaced','CommitFlushed') else original))
        if status==1:
            discarded=run('discard',directory)
            check(scenario+'-discard-incomplete',discarded['After']==[] and digest(directory/'original.pdf')==original)
        else:
            recovered=run('recover',directory)
            check(scenario+'-recovered',recovered['After']==[] and recovered['TargetSHA256']==expected)
            check(scenario+'-idempotent',run('recover',directory)['TargetSHA256']==expected)

    tampers=['target','candidate','path','foreign-file','invalid-pdf','invalid-committed-pdf']
    if os.name != 'nt': tampers.append('symlink')
    for tamper in tampers:
        directory,original=prepare('recovery-tamper-'+tamper)
        owned=paused(directory,'crash-FileFlushed'); finish(owned,True)
        transaction=next(directory.glob('.nt-pdf-txn-*'))
        if tamper=='target': shutil.copyfile(directory/'external.pdf',directory/'original.pdf')
        elif tamper=='candidate': (transaction/'candidate.pdf').write_bytes(b'broken')
        elif tamper=='path':
            journal=read(transaction/'transaction.json'); journal['TargetFileName']='../unrelated.pdf'
            (transaction/'transaction.json').write_text(json.dumps(journal),encoding='utf-8')
        elif tamper=='foreign-file': (transaction/'foreign.txt').write_text('Do not remove',encoding='utf-8')
        elif tamper=='symlink':
            (transaction/'candidate.pdf').unlink()
            (transaction/'candidate.pdf').symlink_to(directory/'external.pdf')
        else:
            invalid=directory/'original.pdf' if tamper=='invalid-committed-pdf' else transaction/'candidate.pdf'
            invalid.write_bytes(b'not a PDF')
            journal=read(transaction/'transaction.json')
            journal['Output']={'Exists':True,'Length':invalid.stat().st_size,'Sha256':digest(invalid)}
            (transaction/'transaction.json').write_text(json.dumps(journal),encoding='utf-8')
        before=digest(directory/'original.pdf')
        attempted=run('attempt-recover',directory)
        check('recovery-'+tamper+'-refused',attempted['Recovered'] is False and digest(directory/'original.pdf')==before)
        check('recovery-'+tamper+'-retained',transaction.exists() and (transaction/'transaction.json').exists())

    directory,original=prepare('cooperating-processes')
    first=paused(directory,'crash-BeforeReplace')
    shutil.copyfile(directory/'expected-output.pdf',directory/'first-expected.pdf')
    try:
        second=run('save',directory,'second-writer')
        check('second-process-blocked',second['CommitSucceeded'] is False and second['Dirty'] is True and digest(directory/'original.pdf')==original)
        check('live-save-discovered-active',run('scan',directory)['Entries'][0]['Status']==0)
    finally: finish(first,True)
    recovered=run('recover',directory)
    check('first-process-recoverable',recovered['TargetSHA256']==digest(directory/'first-expected.pdf') and recovered['After']==[])
    passed=True
except BaseException as error:
    failure=repr(error)
finally:
    result={'schemaVersion':1,'passed':passed,'failure':failure,'startedUtc':started,
            'completedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'platform':platform.platform(),
            'uid':os.getuid() if hasattr(os,'getuid') else None,'app':str(app),'hostSHA256':digest(app),
            'functionalSHA256':digest(app.parent/'NT.AvaloniaControl.PdfViewer.dll'),
            'checks':checks,'processes':processes,
            'boundaries':['Injected disk-full / flush / unavailable-mount errors are deterministic faults, not actual full disks or disconnected SMB mounts.',
                          'Crash tests kill an owned child at a production save barrier, not a power failure or kernel panic.',
                          'Atomic displaced-version retention detects non-cooperating writers in the final check/publication window; it is not a filesystem CAS or protection from later external writes.']}
    (root/'result.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({'passed':passed,'checks':len(checks),'processes':len(processes),'failure':failure}))
if not passed: raise SystemExit(1)
