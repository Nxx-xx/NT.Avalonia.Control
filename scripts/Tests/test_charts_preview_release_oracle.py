import copy,importlib.util,pathlib,unittest
path=pathlib.Path(__file__).resolve().parents[1]/'charts_preview_release_oracle.py'
spec=importlib.util.spec_from_file_location('preview_oracle',path);oracle=importlib.util.module_from_spec(spec);spec.loader.exec_module(oracle)

class ReleaseOracleTests(unittest.TestCase):
    def fixture(self):
        stats=dict(acceptedSamples=1000,committedSamples=1000,canceledSamples=0,faultedSamples=0,rejectedSamples=0,pendingSamples=0,pendingBatches=0,waitingWriters=0,maximumObservedPendingBatches=2)
        raw=dict(passed=True,status='completed',formalPerformanceQualified=False,physicalHardwareQualified=False,options=dict(seconds=1,mode='smoke'),elapsedSeconds=1.1,completedCycles=1,producedSamples=1000,appliedSamples=1000,streamStatistics=stats,nativeWindow=dict(nonzeroHandle=True),renderChecks=4,hitChecks=4,exportChecks=6,nativeTextAfterUi=dict(status='NotApplicable'))
        execution=dict(passed=True,seconds=1,cycles=1,mode='smoke',platform='windows')
        samples=[dict(elapsedSeconds=1,applied=1000,statistics=stats,buffers=[dict(count=1274,capacity=16384,evictedCount=0) for _ in range(4)],cache={})]
        return raw,samples,execution
    def test_valid_short_run_is_not_soak(self):
        result=oracle.validate(*self.fixture());self.assertTrue(result['passed']);self.assertFalse(result['soakQualified'])
    def test_short_run_cannot_satisfy_eight_hours(self):self.assertFalse(oracle.validate(*self.fixture(),require_soak=True)['passed'])
    def test_missing_or_failed_supervisor_rejected(self):
        for value in (False,None):
            raw,samples,execution=self.fixture();execution['passed']=value;self.assertFalse(oracle.validate(raw,samples,execution)['passed'])
    def test_dropped_or_pending_samples_rejected(self):
        for key in ('committedSamples','pendingSamples','faultedSamples','rejectedSamples'):
            raw,samples,execution=self.fixture();raw['streamStatistics'][key]+=1;self.assertFalse(oracle.validate(raw,samples,execution)['passed'])
    def test_wrong_original_retention_rejected(self):
        raw,samples,execution=self.fixture();samples[0]['buffers'][0]['count']-=1;self.assertFalse(oracle.validate(raw,samples,execution)['passed'])
    def test_short_actual_clock_rejected(self):
        raw,samples,execution=self.fixture();raw['elapsedSeconds']=.9;self.assertFalse(oracle.validate(raw,samples,execution)['passed'])
    def test_linux_requires_observed_binding(self):
        raw,samples,execution=self.fixture();execution['platform']='linux';raw['nativeTextAfterUi']=dict(status='Initialized',resolverObserved=False);self.assertFalse(oracle.validate(raw,samples,execution)['passed'])
    def test_missing_actual_window_rejected(self):
        raw,samples,execution=self.fixture();raw['nativeWindow']={};self.assertFalse(oracle.validate(raw,samples,execution)['passed'])
    def test_formal_or_physical_claim_rejected(self):
        raw,samples,execution=self.fixture();raw['formalPerformanceQualified']=True;self.assertFalse(oracle.validate(raw,samples,execution)['passed'])
    def long_fixture(self):
        raw,_,execution=self.fixture();execution.update(seconds=28800,mode='soak');raw['options'].update(seconds=28800,mode='soak');raw.update(elapsedSeconds=28800.2,producedSamples=28800000,appliedSamples=28800000)
        raw['streamStatistics'].update(acceptedSamples=28800000,committedSamples=28800000)
        samples=[]
        for t in range(28801):
            n=t*1000;count=min(16384,1024+n//4);evicted=1024+n//4-count
            samples.append(dict(elapsedSeconds=t,applied=n,statistics=dict(committedSamples=n),buffers=[dict(count=count,capacity=16384,evictedCount=evicted)]*4,cache={},managedBytes=100000000,privateBytes=200000000))
        return raw,samples,execution
    def test_complete_eight_hours_qualifies(self):self.assertTrue(oracle.validate(*self.long_fixture(),require_soak=True)['soakQualified'])
    def test_memory_growth_fails(self):
        raw,samples,execution=self.long_fixture()
        for row in samples[-301:]:row['managedBytes']+=40000000
        self.assertFalse(oracle.validate(raw,samples,execution,True)['passed'])
    def test_missing_middle_of_eight_hours_fails(self):
        raw,samples,execution=self.long_fixture();samples=samples[:500]+samples[-500:];self.assertFalse(oracle.validate(raw,samples,execution,True)['passed'])
    def test_failed_child_cannot_be_overridden_by_success_flag(self):
        raw,samples,execution=self.fixture();execution['stages']=[dict(passed=False,exitCode=1)];self.assertFalse(oracle.validate(raw,samples,execution)['passed'])

if __name__=='__main__':unittest.main()
