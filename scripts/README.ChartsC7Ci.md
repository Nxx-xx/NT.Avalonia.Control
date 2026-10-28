# Manual C7 self-developed CI gate

The independent workflow is `.github/workflows/charts-c7.yml`. Existing `charts.yml` push/PR/C4 jobs are unchanged. Nothing is dispatched automatically by these tools.

A manual dispatch with the default `run_formal=false` runs only `c7-contracts`: Python synthetic contracts for the CI evidence adapter, memory sampling tools and original C7 oracle. It does not build or start .NET, sample a live process, or generate a real million-point workload. Missing source/contract files fail explicitly.

Set `run_formal=true` to additionally run the original formal campaign on the existing repository runner labels `windows-2025` and `ubuntu-24.04`. The latter uses X11 under Xvfb; both request software raster. These labels and action major versions follow the existing workflow, not a claim about the newest hosted images. Actual image/runtime/font/source identities are retained with the run.

`Invoke-ChartsC7Ci.ps1` supervises the unmodified `Invoke-ChartsC7Performance.ps1 -Phase formal`. Each OS retains five independent four-track interaction processes, 100 fresh formal million-point first-use processes and two excluded calibrations. Original warmup, 30-second fixed load, drain policy, numeric thresholds, source identity and independent oracle remain unchanged. The outer 100-minute CI watchdog only bounds infrastructure use; a timeout is incomplete failed evidence, never a relaxed performance pass.

The workflow preserves both OS attempts (`fail-fast:false`) and always uploads `artifacts/charts-c7-ci/**`, including partial/failed native receipts, raw JSON, request JSONL, binary datasets, PNGs, source/assembly manifests, private cache/font configuration and logs. Upload is still subject to external runner termination or GitHub outages; artifacts do not imply that a failed/incomplete campaign passed. The original gate error remains a failed job.

`charts_c7_ci_evidence.py` runs after the original child stops. It preserves the distinction between complete 107-process evidence and a passing workload, and retains the original `formalMeasurementProtocolCompleted` flag unchanged. It checks the original five protocol/oracle/test/budget/policy pins, archives actual producer Skia/HarfBuzz file bytes and SHA-256, verifies required retained artifact paths, and fingerprints the output tree. Skia asset presence is explicitly **not** a per-process loaded-module map. Native-text resolver paths/SHA reported by the original host are retained and matched to archived bytes where available; arbitrary reported external paths are never opened.

No official Charts package/license/secret is used. Official measurements/ratios, compositor present and matched peak-memory qualification remain null; hosted Windows or Xvfb is not physical desktop/GPU/touch validation. A green manual job qualifies only the original self-developed controlled-software gate for that recorded environment, not complete C7 or official parity.

## Standalone memory CLI

The separate diagnostic entry is:

    pwsh -NoProfile -File scripts/Invoke-ChartsMemoryProbe.ps1 -OutputDirectory NEW_MEMORY_EVIDENCE_DIRECTORY

Linux needs an existing X11/XWayland session or a separately owned Xvfb display, with native dependencies/fonts provisioned as for the C7 job. Its sampler/oracle and fixed contract are `charts_memory_sampler.py`, `charts_memory_oracle.py` and `ChartsMemoryProbe.Contract.json`. This is an independent matched **sampled process-memory diagnostic**, not an original-C7 instantaneous/native/GPU peak or peak-memory SLA. Its Python mutation contracts run in `c7-contracts`; its live dual-OS memory campaign is deliberately not wired into this workflow, and no `run_memory` input suggests otherwise. Run the CLI separately and retain its own failure evidence.

No remote workflow, native campaign or live memory run is performed by the Python unit tests.
