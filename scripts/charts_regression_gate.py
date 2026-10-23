"""Independent C4-08 gate. Raw values and pixels are authoritative; summaries are derived here."""
import argparse
import hashlib
import json
import math
from pathlib import Path
import re
import sys
from charts_mechanism_oracle import validate_mechanism
from charts_png_oracle import decode_png, sha_bgra, blue_counts, compare_png


def reject_constant(value):
    raise ValueError('Non-finite JSON literal: ' + value)


def finite_float(value):
    result = float(value)
    if not math.isfinite(result):
        raise ValueError('Non-finite JSON number: ' + value)
    return result


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError('Duplicate JSON key: ' + key)
        result[key] = value
    return result


def load(path):
    path = Path(path)
    if path.stat().st_size > 128 * 1024 * 1024:
        raise ValueError('JSON exceeds evidence size limit: ' + str(path))
    return json.loads(path.read_text(encoding='utf-8-sig'), object_pairs_hook=unique_object, parse_constant=reject_constant, parse_float=finite_float)


def digest(path):
    value = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            value.update(block)
    return value.hexdigest().upper()


def number(value, integer=False):
    if type(value) not in (int, float) or not math.isfinite(value) or value < 0 or integer and int(value) != value:
        raise ValueError('Expected a finite nonnegative ' + ('integer' if integer else 'number'))
    return value


def percentile(values, fraction):
    if not values:
        raise ValueError('No samples; an empty percentile cannot pass.')
    values = sorted(number(value) for value in values)
    return values[math.ceil(len(values) * fraction) - 1]


def inside(root, relative):
    if not isinstance(relative, str) or not relative or Path(relative).is_absolute():
        raise ValueError('Expected a relative evidence path.')
    path = (Path(root) / relative).resolve()
    if not path.is_relative_to(Path(root).resolve()):
        raise ValueError('Evidence path escapes its run: ' + relative)
    return path


def stats(values):
    return {'count': len(values), 'p50': percentile(values, .5), 'p95': percentile(values, .95), 'maximum': max(values)}


class Guard:
    def __init__(self, output):
        self.output = Path(output)
        self.checks = {}
        self.errors = []
        self.reports = []
        self.inputs = {}
        self.native = {}
        self.comparisons = []
        self.failure_rates = {}
        self._pixels = {}

    def check(self, name, passed, detail=None):
        if name in self.checks:
            raise ValueError('Duplicate guard predicate: ' + name)
        self.checks[name] = passed is True
        if passed is not True:
            self.errors.append({'check': name, 'detail': detail})

    def read(self, path):
        path = Path(path)
        self.inputs[str(path)] = digest(path)
        return load(path)

    def section(self, name, action):
        try:
            return action()
        except Exception as error:
            self.check(name + ':section-failed', False, repr(error))
            return None

    def manifest(self, root, path, name):
        manifest = self.read(path)
        entries = manifest['files']
        self.check(name + ':nonempty', isinstance(entries, list) and len(entries) > 0)
        seen = set()
        for entry in entries:
            relative = entry['path']
            if relative in seen:
                raise ValueError('Duplicate artifact entry: ' + relative)
            seen.add(relative)
            actual = inside(root, relative)
            self.check(name + ':' + relative, actual.is_file() and actual.stat().st_size == number(entry['bytes'], True) and digest(actual) == entry['sha256'])
        return seen

    def process(self, root, relative, name):
        receipt = self.read(inside(root, relative))
        self.check(name + ':success', receipt['passed'] is True and type(receipt['exitCode']) is int and receipt['exitCode'] == 0 and receipt['timedOut'] is False and receipt['error'] is None)
        self.check(name + ':real-process', type(receipt['processId']) is int and receipt['processId'] > 0)
        self.check(name + ':command-recorded', isinstance(receipt['arguments'], list) and len(receipt['arguments']) > 0 and bool(receipt['executable']))
        return receipt

    def pixels(self, path):
        path = Path(path)
        fingerprint = digest(path)
        if fingerprint not in self._pixels:
            self._pixels[fingerprint] = decode_png(path)
        return self._pixels[fingerprint]

    def native_report(self, root, policy, tag, payload=None):
        raw = self.read(Path(root) / 'raw.json')
        self.check(tag + ':native-schema', raw['schema'] == 'charts-native-window-v1' and raw['actualNativeWindowRequired'] is True)
        self.check(tag + ':completed', raw['passed'] is True and raw['status'] == 'completed' and raw['error'] is None and raw['finishedUtc'] is not None)
        self.check(tag + ':no-present-claims', raw['presentMeasured'] is False and raw['gpuMeasured'] is False and raw['physicalInputMeasured'] is False and raw['nativePresentTicks'] is None and raw['gpuTicks'] is None)
        self.check(tag + ':configuration', raw['options']['samples'] == policy['samples'] and raw['options']['warmups'] == policy['warmups'] and raw['options']['scenarios'] == policy['scenarios'])
        scenarios = raw['scenarios']
        self.check(tag + ':scenario-set', [item['id'] for item in scenarios] == policy['scenarios'])
        frequency = number(raw['environment']['stopwatchFrequency'], True)
        if frequency == 0:
            raise ValueError('Zero timestamp frequency.')
        self.check(tag + ':native-platform', raw['environment']['nativeHandleDescriptor'] is not None and 'Headless' not in raw['environment']['backend'])
        config = raw['runtimeConfiguration']
        configuration_path = Path(config['path'])
        configuration = self.read(configuration_path)
        self.check(tag + ':runtimeconfig-hash', configuration_path.name == 'Charts.NativeBenchmarks.runtimeconfig.json' and digest(configuration_path) == config['sha256'] and configuration_path.stat().st_size == number(config['bytes'], True))
        self.check(tag + ':tiered-compilation-protocol', policy['requiredTieredCompilation'] is False and configuration['runtimeOptions']['configProperties']['System.Runtime.TieredCompilation'] is False and config['declaredTieredCompilation'] is False and config['appContextSwitchPresent'] is True and config['appContextSwitchValue'] is False and config['dotnetOverride'] in (None, '0') and config['comPlusOverride'] in (None, '0') and raw['environment']['tieredCompilation'] is False and raw['environment']['tieredCompilationSwitchPresent'] is True and raw['environment']['tieredCompilationSwitchValue'] is False and raw['environment']['dotnetTieredCompilationOverride'] == config['dotnetOverride'] and raw['environment']['comPlusTieredCompilationOverride'] == config['comPlusOverride'])
        self.check(tag + ':native-assemblies', bool(raw['assemblyManifest']) and not any('Avalonia.Headless' in item['path'] for item in raw['assemblyManifest']))
        if payload is not None:
            loaded = {Path(item['path']).name: item['sha256'] for item in raw['assemblyManifest']}
            for assembly in ['Charts.NativeBenchmarks.dll', 'NT.AvaloniaControl.Charts.dll', 'NT.AvaloniaControl.Charts.Themes.dll']:
                expected = [item['sha256'] for item in payload['files'] if item['host'] == 'native' and item['path'] == assembly]
                self.check(tag + ':loaded-payload:' + assembly, len(expected) == 1 and loaded.get(assembly) == expected[0])
            runtime_configs = [item['sha256'] for item in payload['files'] if item['host'] == 'native' and item['path'] == configuration_path.name]
            self.check(tag + ':runtimeconfig-in-payload', runtime_configs == [config['sha256']])
        driver_manifest = raw['driverSourceManifest']
        self.check(tag + ':nonempty-driver-manifest', bool(driver_manifest))
        host_text = '\n'.join(f"{item['path']}|{item['bytes']}|{item['sha256']}" for item in driver_manifest)
        self.check(tag + ':driver-source-digest', hashlib.sha256(host_text.encode()).hexdigest().upper() == raw['driverSourceSha256'])
        trace_path = Path(root) / 'events.jsonl'
        self.inputs[str(trace_path)] = digest(trace_path)
        events = [json.loads(line, object_pairs_hook=unique_object, parse_constant=reject_constant) for line in trace_path.read_text(encoding='utf-8-sig').splitlines()]
        self.check(tag + ':trace-order', bool(events) and all(event['index'] == i and number(event['ticks'], True) >= 0 for i, event in enumerate(events)) and all(a['ticks'] <= b['ticks'] for a, b in zip(events, events[1:])))
        metrics = {}
        correlations = set()
        total = failed = 0
        for scenario in scenarios:
            sid = scenario['id']
            prefix = tag + ':' + sid
            chart_count = 4 if sid == 'multi-4x512' else 1
            row_count = 512 if chart_count == 4 else 10000
            self.check(prefix + ':shape', scenario['passed'] is True and scenario['chartCount'] == chart_count and scenario['pointCountPerChart'] == row_count)
            dataset_path = inside(root, scenario['datasetPath'])
            self.check(prefix + ':dataset-sha', digest(dataset_path) == scenario['datasetSha256'])
            dataset = self.read(dataset_path)
            rows = dataset['rows']
            self.check(prefix + ':source-formula', len(rows) == row_count and all(row['id'] == f'row-{i}' and row['x'] == i and row['y'] == 20 + i * 37 % 71 for i, row in enumerate(rows)))
            samples = scenario['samples']
            self.check(prefix + ':all-samples-retained', len(samples) == policy['samples'] + policy['warmups'])
            times = {key: [] for key in policy['maximumP95Milliseconds']}
            allocations = {key: [] for key in policy['maximumP95AllocatedBytes']}
            last_revision = 0
            for i, sample in enumerate(samples):
                name = prefix + f':sample-{i}'
                total += 1
                successful = sample['passed'] is True and sample['outcome'] == 'completed' and sample['error'] is None
                failed += not successful
                self.check(name + ':outcome', successful)
                correlation = sample['correlationId']
                self.check(name + ':correlation', isinstance(correlation, str) and bool(correlation) and correlation not in correlations)
                correlations.add(correlation)
                phase = 'warmup' if i < policy['warmups'] else 'measured'
                self.check(name + ':phase-index', sample['index'] == i and sample['phase'] == phase)
                self.check(name + ':action', sample['action'] == ['viewport', 'data', 'routed-key-reset'][i % 3])
                expected_revision = last_revision + int(sample['action'] == 'data')
                self.check(name + ':data-version', sample['dataRevisionBefore'] == last_revision and sample['dataRevisionAfter'] == expected_revision)
                last_revision = expected_revision
                self.check(name + ':window-visible', sample['nativeWindowVisible'] is True)
                self.check(name + ':checks-present', isinstance(sample['checks'], dict) and set(sample['checks']) == set(policy['sampleChecks']) and all(type(v) is bool and v for v in sample['checks'].values()))
                self.check(name + ':routed-key', sample['inputHandled'] is True if sample['action'] == 'routed-key-reset' else sample['inputHandled'] is None)
                versions_before, versions_after = sample['sourceVersionsBefore'], sample['sourceVersionsAfter']
                self.check(name + ':product-source-versions', len(versions_before) == len(versions_after) == chart_count and all(number(a, True) < number(b, True) if sample['action'] == 'data' else number(a, True) == number(b, True) for a, b in zip(versions_before, versions_after)))
                self.check(name + ':scene-revision', type(sample['requestedSceneRevision']) is int and sample['requestedSceneRevision'] == i + 1)
                tick_keys = ['request', 'applyComplete', 'layoutComplete', 'animationCallback', 'captureStart', 'captureComplete', 'hitStart', 'hitComplete', 'terminal']
                tick = sample['ticks']
                self.check(name + ':complete-monotonic-ticks', set(tick) == set(tick_keys) and all(number(tick[key], True) >= 0 for key in tick_keys) and all(tick[a] <= tick[b] for a, b in zip(tick_keys, tick_keys[1:])))
                intervals = {'apply': ('request', 'applyComplete'), 'layout': ('applyComplete', 'layoutComplete'), 'captureReadback': ('captureStart', 'captureComplete'), 'requestToCapture': ('request', 'captureComplete'), 'hit': ('hitStart', 'hitComplete')}
                ms = {key: (tick[end] - tick[start]) * 1000 / frequency for key, (start, end) in intervals.items()}
                alloc = {'uiApplyLayout': number(sample['uiApplyLayoutAllocatedBytes'], True), 'captureReadback': number(sample['captureReadbackAllocatedBytes'], True), 'hit': number(sample['hitAllocatedBytes'], True)}
                if phase == 'measured':
                    for key, value in ms.items():
                        times[key].append(value)
                    for key, value in alloc.items():
                        allocations[key].append(value)
                for route in ['capture', 'canonical']:
                    image_path = inside(root, sample[route + 'Path'])
                    self.check(name + ':' + route + '-file-sha', digest(image_path) == sample[route + 'Sha256'])
                    width, height, rgba = self.pixels(image_path)
                    expected_hash = sample['pixelSha256' if route == 'capture' else 'canonicalPixelSha256']
                    self.check(name + ':' + route + '-pixel-sha', sha_bgra(rgba) == expected_hash and width == sample['pixelWidth'] and height == sample['pixelHeight'])
                    self.check(name + ':' + route + '-canvas-size', width >= 800 and height >= 400)
                    if route == 'capture':
                        global_blue, plot_blue = blue_counts(width, height, rgba, sample['plotPixelRects'])
                        self.check(name + ':business-pixels', global_blue == sample['businessInkPixels'] and plot_blue == sample['plotInkPixels'] and len(plot_blue) == chart_count and all(value >= policy['minimumPlotBluePixels'] for value in plot_blue))
                self.check(name + ':canonical-pixels-identical', sample['pixelSha256'] == sample['canonicalPixelSha256'])
                hits = sample['hits']
                self.check(name + ':hit-count', len(hits) == chart_count)
                for chart_index, hit in enumerate(hits):
                    index = number(hit['expectedIndex'], True)
                    planned_index = row_count // 2 + i
                    if sid == 'bar-10k':
                        planned_index = row_count // 2
                        while planned_index * 37 % 71 != 70:
                            planned_index += 1
                        planned_index += i * 71
                    x, y = index, 20 + index * 37 % 71 + expected_revision * .25
                    self.check(name + f':hit-{chart_index}', hit['passed'] is True and index == planned_index and index < row_count and hit['chartIndex'] == chart_index and hit['actualIndex'] == index and hit['expectedX'] == hit['actualX'] == x and hit['expectedY'] == hit['actualY'] == y and hit['expectedStableId'] == hit['actualStableId'] == f'row-{index}')
                    plot, view = hit['plot'], hit['range']
                    self.check(name + f':query-projection-{chart_index}', len(plot) == len(view) == 4 and plot[2] > 0 and plot[3] > 0 and view[0] < view[1] and view[2:] == [0, 120] and abs(hit['queryX'] - (plot[0] + (x-view[0])/(view[1]-view[0])*plot[2])) < 1e-7 and abs(hit['queryY'] - (plot[1]+plot[3]-y/120*plot[3])) < 1e-7)
            metrics[sid] = {'milliseconds': {key: stats(value) for key, value in times.items()}, 'allocatedBytes': {key: stats(value) for key, value in allocations.items()}}
            for key, limit in policy['maximumP95Milliseconds'].items():
                self.check(prefix + ':absolute-p95-ms:' + key, metrics[sid]['milliseconds'][key]['p95'] <= limit, metrics[sid]['milliseconds'][key])
            for key, limit in policy['maximumP95AllocatedBytes'].items():
                self.check(prefix + ':absolute-p95-bytes:' + key, metrics[sid]['allocatedBytes'][key]['p95'] <= limit, metrics[sid]['allocatedBytes'][key])
        self.failure_rates[tag] = {'numerator': failed, 'denominator': total, 'rate': failed / total if total else None}
        self.check(tag + ':zero-failed-samples', total == 56 and failed == 0)
        starts = [event['correlationId'] for event in events if event['kind'] == 'request-started']
        completed = [event['correlationId'] for event in events if event['kind'] == 'request-completed']
        self.check(tag + ':trace-correlations', len(starts) == len(completed) == total and set(starts) == set(completed) == correlations and len(set(starts)) == total and sum(event['kind'] == 'scenario-started' for event in events) == 4 and sum(event['kind'] == 'driver-terminal' for event in events) == 1)
        self.native[tag] = metrics
        return raw, metrics

    def compare_native(self, candidate, baseline, policy, candidate_root, baseline_root):
        raw, metrics = candidate
        base_raw, base_metrics = baseline
        self.check('comparison:different-process-run-ids', raw['runId'] != base_raw['runId'])
        self.check('comparison:independent-source-directories', Path(raw['options']['sourceRoot']).resolve() != Path(base_raw['options']['sourceRoot']).resolve())
        self.check('comparison:identical-driver-source', raw['driverSourceSha256'] == base_raw['driverSourceSha256'])
        self.check('comparison:environment-comparable', all(raw['environment'][key] == base_raw['environment'][key] for key in policy['comparableEnvironmentFields']))
        for scenario, base_scenario in zip(raw['scenarios'], base_raw['scenarios'], strict=True):
            sid = scenario['id']
            self.check('comparison:' + sid + ':source-identical', scenario['datasetSha256'] == base_scenario['datasetSha256'])
            for units in ['milliseconds', 'allocatedBytes']:
                for key, current in metrics[sid][units].items():
                    before = base_metrics[sid][units][key]
                    quantiles = ['p50', 'p95'] if units == 'milliseconds' else ['p95']
                    for quantile in quantiles:
                        ratio_key, allowance_key = ('relative' + quantile.upper() + 'MaximumRatio', 'relative' + quantile.upper() + 'NoiseAllowanceMilliseconds') if units == 'milliseconds' else ('relativeP95AllocationMaximumRatio', 'relativeP95AllocationNoiseAllowanceBytes')
                        allowed = max(before[quantile] * policy[ratio_key], before[quantile] + policy[allowance_key])
                        self.check(f'comparison:{sid}:{units}:{key}:{quantile}', current[quantile] <= allowed, {'candidate': current[quantile], 'baseline': before[quantile], 'maximum': allowed})
                        self.comparisons.append({'scenario': sid, 'units': units, 'metric': key, 'quantile': quantile, 'candidate': current[quantile], 'baseline': before[quantile], 'maximum': allowed})
            for sample, base_sample in zip(scenario['samples'], base_scenario['samples'], strict=True):
                i = sample['index']
                self.check(f'comparison:{sid}:sample-{i}:same-request', sample['action'] == base_sample['action'] and sample['dataRevisionAfter'] == base_sample['dataRevisionAfter'] and sample['phase'] == base_sample['phase'])
                diff_path = self.output.parent / 'diffs' / f'{sid}-{i:03}.png'
                difference = compare_png(inside(candidate_root, sample['capturePath']), inside(baseline_root, base_sample['capturePath']), diff_path)
                self.check(f'comparison:{sid}:sample-{i}:image-difference', difference['dimensionsEqual'] is True and difference['differentPixels'] <= policy['maximumDifferentPixels'], difference)

    def run(self, root, policy_path, baseline_directory, legacy_integrity):
        root = Path(root).resolve()
        policy = self.read(policy_path)
        self.check('policy:identity', policy['schemaVersion'] == 'charts-hosted-ci-policy-v1' and policy['formalSlaEvaluated'] is False and policy['presentQualified'] is False)
        contracts = self.read(Path(__file__).with_name(policy['mechanismContracts']))
        run = self.read(root / 'orchestration.json')
        self.check('run:schema', run['schemaVersion'] == 'charts-ci-performance-run-v1' and run['planOnly'] is False and run['noRetries'] is True)
        self.check('run:unchanged-source', run['sourceUnchanged'] is True and run['error'] is None)
        self.check('run:immutable-policy', run['policySha256'] == digest(policy_path) and digest(inside(root, run['policySnapshotPath'])) == digest(policy_path))
        before = self.read(inside(root, run['sourceManifestPath']))
        after = self.read(inside(root, run['sourceAfterManifestPath']))
        self.check('run:source-manifests', before['files'] == after['files'] and len(before['files']) > 0)
        source_paths = [item['path'] for item in before['files']]
        self.check('run:guard-and-contracts-bound', all(name in source_paths for name in ['scripts/charts_regression_gate.py', 'scripts/charts_png_oracle.py', 'scripts/charts_mechanism_oracle.py', 'scripts/charts-ci-mechanism-contracts.json', 'scripts/charts-ci-performance-policy.json']))
        repo = Path(__file__).resolve().parent.parent
        self.check('run:source-files-match-current-guard-checkout', len(source_paths) == len(set(source_paths)) and all(inside(repo, entry['path']).is_file() and digest(inside(repo, entry['path'])) == entry['sha256'] for entry in before['files']))
        self.check('run:measured-builds-required', len(run['buildReceipts']) == 2 and run['sourceKind'] == 'workspace-source-before-and-after-isolated-build-processes')
        inventory = self.manifest(root, inside(root, run['preguardEvidencePath']), 'evidence')
        payload = self.read(inside(root, run['assemblyManifestPath']))
        self.check('run:payload-manifest-nonempty', payload['schemaVersion'] == 'charts-ci-payload-v1' and bool(payload['files']))
        stages = run['stages']
        self.check('run:exact-stage-order', [stage['id'] for stage in stages] == policy['requiredStages'])
        failed = 0
        for build in run['buildReceipts']:
            self.section('build:' + build['id'], lambda build=build: self.process(root, build['receiptPath'], 'build:' + build['id']))
        for stage in stages:
            name = stage['id']
            def stage_check():
                receipt = self.process(root, stage['receiptPath'], name)
                self.check(name + ':supervisor-agrees', stage['exitCode'] == receipt['exitCode'] and stage['timedOut'] == receipt['timedOut'])
                self.check(name + ':raw-in-preguard-inventory', stage['reportPath'] in inventory and stage['receiptPath'] in inventory)
                raw = self.read(inside(root, stage['reportPath']))
                if stage['kind'] == 'mechanism':
                    checks = validate_mechanism(name, raw, contracts)
                    self.check(name + ':independent-checks-nonempty', isinstance(checks, dict) and len(checks) > 0)
                    for key, value in checks.items():
                        self.check(name + ':' + key, value)
                    loaded = {Path(item['path']).name: item['sha256'] for item in raw['assemblies']}
                    self.check(name + ':loaded-payload-bound', all(any(item['host'] == 'headless' and item['path'] == assembly and item['sha256'] == value for item in payload['files']) for assembly, value in loaded.items()))
            self.section(name, stage_check)
            failed += stage['exitCode'] != 0 or stage['timedOut'] is not False or not inside(root, stage['reportPath']).exists()
        self.failure_rates['stages'] = {'numerator': failed, 'denominator': len(stages), 'rate': failed / len(stages) if stages else None}
        self.check('run:failure-rate-exact', run['failureRate']['numerator'] == failed and run['failureRate']['denominator'] == len(stages) == 9 and run['failureRate']['rate'] == failed / 9 and failed == 0)
        self.section('generic', lambda: self.generic(root, policy['generic'], legacy_integrity))
        candidate = self.section('candidate-native', lambda: self.native_report(root / 'native-window', policy['native'], 'candidate-native', payload))
        if policy['native']['baselineRequired'] and not baseline_directory:
            raise ValueError('Paired independent native baseline is required by this policy.')
        if baseline_directory:
            base_root = Path(baseline_directory).resolve()
            baseline_manifest = self.read(base_root / 'baseline.json')
            self.check('baseline:independent-source-and-build', baseline_manifest['schemaVersion'] == 'charts-ci-native-baseline-v1' and baseline_manifest['passed'] is True and baseline_manifest['productSourceUnchanged'] is True)
            self.process(base_root, baseline_manifest['buildReceiptPath'], 'baseline-build')
            self.process(base_root, baseline_manifest['nativeReceiptPath'], 'baseline-process')
            old_product = self.read(inside(base_root, baseline_manifest['productSourceManifestPath']))
            new_product = self.read(inside(base_root, baseline_manifest['productSourceAfterManifestPath']))
            self.check('baseline:product-unchanged', old_product['files'] == new_product['files'] and bool(old_product['files']))
            harness = self.read(inside(base_root, baseline_manifest['harnessSourceManifestPath']))
            self.check('baseline:copied-candidate-harness-identical', harness['files'] == harness['candidateFiles'] and bool(harness['files']))
            base_payload = self.read(inside(base_root, baseline_manifest['assemblyManifestPath']))
            baseline = self.section('baseline-native', lambda: self.native_report(base_root / 'native-window', policy['native'], 'baseline-native', base_payload))
            if candidate and baseline:
                self.section('comparison', lambda: self.compare_native(candidate, baseline, policy['native'], root / 'native-window', base_root / 'native-window'))
        for path, expected in list(self.inputs.items()):
            self.check('input-stable:' + path, digest(path) == expected)

    def generic(self, root, policy, integrity_path):
        integrity = self.read(integrity_path)
        raw_path = root / 'generic-benchmark/raw.json'
        raw = self.read(raw_path)
        self.check('generic:existing-independent-integrity-guard', integrity['passed'] is True and integrity['reportSha256'] == digest(raw_path))
        self.check('generic:fixed-config', raw['configuration']['samples'] == policy['samples'] and raw['configuration']['warmups'] == policy['warmups'] and sorted(raw['configuration']['scenarios']) == sorted(policy['scenarios']))
        frequency = number(raw['stopwatchFrequency'], True)
        for scenario in raw['scenarios']:
            sid = scenario['id']
            measured = [sample for sample in scenario['samples'] if sample['isWarmup'] is False]
            self.check('generic:' + sid + ':all-measured', len(measured) == policy['samples'])
            for metric, limit in policy['maximumP95Milliseconds'].items():
                intervals = [query['timing'] for sample in measured for query in sample['queries']] if metric == 'hitQuery' else [sample[metric] for sample in measured]
                values = [(number(item['endTicks'], True) - number(item['startTicks'], True)) * 1000 / frequency for item in intervals]
                self.check('generic:' + sid + ':p95-ms:' + metric, percentile(values, .95) <= limit, stats(values))
            for metric, limit in policy['maximumP95AllocatedBytes'].items():
                values = [number(sample[metric]['allocatedBytes'], True) for sample in measured]
                self.check('generic:' + sid + ':p95-bytes:' + metric, percentile(values, .95) <= limit, stats(values))

    def publish(self):
        result = {'schemaVersion': 'charts-ci-independent-guard-v1', 'passed': bool(self.checks) and all(self.checks.values()),
                  'checkCount': len(self.checks), 'passedChecks': sum(self.checks.values()), 'checks': self.checks, 'errors': self.errors,
                  'inputs': [{'path': key, 'sha256': value} for key, value in self.inputs.items()], 'nativeMetrics': self.native,
                  'comparisons': self.comparisons, 'failureRates': self.failure_rates, 'formalSlaEvaluated': False,
                  'presentQualified': False, 'actualProcessMemoryQualified': False, 'officialRuntimeCompared': False}
        self.output.parent.mkdir(parents=True, exist_ok=True)
        with self.output.open('x', encoding='utf-8') as stream:
            json.dump(result, stream, indent=2, ensure_ascii=False, allow_nan=False)
        summary = self.output.with_suffix('.md')
        with summary.open('x', encoding='utf-8') as stream:
            stream.write('# Charts hosted CI regression guard\n\n')
            stream.write(f"Passed: {result['passed']}; predicates: {result['passedChecks']}/{result['checkCount']}.\n\n")
            stream.write('Native timings describe controlled raster capture. Frame/present, actual process memory, long soak and official parity remain unqualified.\n\n')
            for error in self.errors:
                stream.write('- ' + error['check'] + ': ' + str(error['detail']) + '\n')
        return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--run-directory', required=True)
    parser.add_argument('--output', required=True)
    parser.add_argument('--policy', required=True)
    parser.add_argument('--baseline-directory')
    parser.add_argument('--legacy-integrity', required=True)
    args = parser.parse_args()
    if Path(args.output).exists() or Path(args.output).with_suffix('.md').exists():
        raise SystemExit('Choose new immutable guard evidence paths.')
    guard = Guard(args.output)
    guard.section('guard', lambda: guard.run(args.run_directory, args.policy, args.baseline_directory, args.legacy_integrity))
    result = guard.publish()
    print(json.dumps({key: result[key] for key in ['passed', 'checkCount', 'passedChecks', 'failureRates']}))
    return 0 if result['passed'] else 1


if __name__ == '__main__':
    sys.exit(main())
