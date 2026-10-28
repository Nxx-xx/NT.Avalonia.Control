param(
    [Parameter(Mandatory)][string]$RegressionDirectory,
    [Parameter(Mandatory)][string]$NativeEvidenceDirectory,
    [Parameter(Mandatory)][string]$SampleDirectory,
    [Parameter(Mandatory)][string]$OutputDirectory
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'TreeDataGrid.Validation.ps1')
$previewRepo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
function Resolve-PreviewPath([string]$Path) { if([IO.Path]::IsPathRooted($Path)){[IO.Path]::GetFullPath($Path)}else{[IO.Path]::GetFullPath((Join-Path $previewRepo $Path))} }
$previewRegression=Resolve-PreviewPath $RegressionDirectory
$previewNative=Resolve-PreviewPath $NativeEvidenceDirectory
$previewSample=Resolve-PreviewPath $SampleDirectory
$previewOutput=Resolve-PreviewPath $OutputDirectory
$previewAudit=$previewOutput+'.export'
foreach($existing in @($previewOutput,($previewOutput+'.zip'),$previewAudit)) {
    if(Test-Path -LiteralPath $existing){throw 'Use a new delivery directory; previous output/audit must remain intact.'}
}
if(-not(Test-Path -LiteralPath $previewSample -PathType Container)){throw 'SampleDirectory must be an existing standalone publish directory.'}
New-Item -ItemType Directory -Path $previewAudit -Force | Out-Null
$previewStarted=[DateTimeOffset]::UtcNow; $previewPassed=$false; $previewError=$null
$previewSteps=[Collections.Generic.List[object]]::new()
function Invoke-PreviewStep([string]$Name,[scriptblock]$Action) {
    $okay=$false; $log=Join-Path $previewAudit ($Name+'.log')
    try { & $Action *>&1 | Tee-Object -FilePath $log; $okay=$true }
    finally { $previewSteps.Add([ordered]@{name=$Name;passed=$okay;log=$log}) }
}
Push-Location $previewRepo
try {
    Invoke-PreviewStep 'disk-preflight' {
        $sampleBytes=(Get-ChildItem -LiteralPath $previewSample -Recurse -File | Measure-Object Length -Sum).Sum
        $budget=[long][Math]::Max(2GB,$sampleBytes*3)
        Assert-TreeDataGridDiskBudget -OutputDirectory $previewAudit -Paths @($previewOutput,[IO.Path]::GetTempPath()) -EstimatedBytes $budget
    }
    Invoke-PreviewStep 'verify-and-export' {
        & python (Join-Path $PSScriptRoot 'Export-TreeDataGridPreview.py') --repo $previewRepo --regression $previewRegression --native-evidence $previewNative --sample $previewSample --output $previewOutput
        if($LASTEXITCODE -ne 0){throw "Preview export failed with exit $LASTEXITCODE"}
    }
    $previewPassed=$true
}
catch { $previewError=$_.Exception.Message; throw }
finally {
    [ordered]@{stage='TreeDataGrid-local-preview-export';passed=$previewPassed;startedUtc=$previewStarted;completedUtc=[DateTimeOffset]::UtcNow;error=$previewError;steps=$previewSteps.ToArray();output=$previewOutput} |
        ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $previewAudit 'results.json') -Encoding utf8
    try { Write-TreeDataGridValidationSummary -OutputDirectory $previewAudit } finally { Pop-Location }
}
