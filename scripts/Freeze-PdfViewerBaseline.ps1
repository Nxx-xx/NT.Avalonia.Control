param(
    [Parameter(Mandatory)][string]$OutputDirectory,
    [switch]$DownloadOfficialDocuments,
    [string[]]$AdditionalSourceRoots = @()
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
foreach ($additionalRoot in $AdditionalSourceRoots) {
    if (-not (Test-Path -LiteralPath (Join-Path $repo $additionalRoot))) {
        throw "Additional source root does not exist: $additionalRoot"
    }
}
Push-Location -LiteralPath $repo
try {
$destination = [IO.Path]::GetFullPath($OutputDirectory, $repo)
[IO.Directory]::CreateDirectory($destination) | Out-Null
$baselinePath = Join-Path $destination 'baseline.json'
if (-not (Test-Path -LiteralPath $baselinePath)) {
    $files = @(& rg --files --hidden src/NT.AvaloniaControl.PdfViewer src/NT.AvaloniaControl.PdfViewer.Themes tests/PdfViewer.Core.Tests tests/PdfViewer.Headless.Tests samples/PdfViewer.Usage benchmarks/PdfViewer.P0 samples/NT.AvaloniaControl.Demo/Pages/PdfViewer)
    if ($AdditionalSourceRoots.Count) {
        $additionalFiles = @(& rg --files @AdditionalSourceRoots)
        if ($LASTEXITCODE -ne 0) { throw 'Additional source inventory failed.' }
        $files += $additionalFiles
    }
    $files += @(& rg --files docs scripts | Where-Object { $_ -match '(?i)pdfviewer' })
    $files += @('src/NT.AvaloniaControl.PdfViewer/packages.lock.json','src/NT.AvaloniaControl.PdfViewer.Themes/packages.lock.json')
    $files += @('samples/NT.AvaloniaControl.Demo/NT.AvaloniaControl.Demo.csproj','samples/NT.AvaloniaControl.Demo/packages.lock.json','samples/NT.AvaloniaControl.Demo/Program.cs','samples/NT.AvaloniaControl.Demo/MainWindow.cs')
    $files += @('Directory.Build.props', 'Directory.Packages.props', 'global.json', 'NuGet.Config', 'NT.AvaloniaControl.slnx', 'README.md')
    $files += 'tests/Directory.Build.props'
    $files = @($files | ForEach-Object { $_.Replace('\','/') } | Where-Object { $_ -notmatch '(^|/)(bin|obj|__pycache__)/|\.py[co]$' -and (Test-Path -LiteralPath (Join-Path $repo $_) -PathType Leaf) } | Sort-Object -Unique)
    $manifest = @($files | ForEach-Object { $item = Get-Item -LiteralPath (Join-Path $repo $_); [ordered]@{ Path = $_.Replace('\','/'); Bytes = $item.Length; SHA256 = (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash } })
    $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $destination 'source-manifest.json') -Encoding utf8
    $archive = [IO.Compression.ZipFile]::Open((Join-Path $destination 'source-snapshot.zip'), [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in $files) { [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, (Join-Path $repo $file), $file.Replace('\','/')) | Out-Null }
    } finally { $archive.Dispose() }
    [ordered]@{ CapturedUtc = [DateTimeOffset]::UtcNow.ToString('o'); GitCommit = (& git -C $repo rev-parse HEAD); SDK = (& dotnet --version); OS = [Runtime.InteropServices.RuntimeInformation]::OSDescription; ProcessArchitecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString(); SourceManifestSHA256 = (Get-FileHash (Join-Path $destination 'source-manifest.json')).Hash; SnapshotSHA256 = (Get-FileHash (Join-Path $destination 'source-snapshot.zip')).Hash } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $baselinePath -Encoding utf8
}
$frozen = Get-Content -LiteralPath $baselinePath -Raw | ConvertFrom-Json
$sourceManifestPath = Join-Path $destination 'source-manifest.json'
$snapshotPath = Join-Path $destination 'source-snapshot.zip'
if ((Get-FileHash -LiteralPath $sourceManifestPath).Hash -ne $frozen.SourceManifestSHA256 -or (Get-FileHash -LiteralPath $snapshotPath).Hash -ne $frozen.SnapshotSHA256) { throw 'Frozen source manifest/archive hash mismatch.' }
$snapshot = [IO.Compression.ZipFile]::OpenRead($snapshotPath)
try {
    $sourceEntries = @(Get-Content -LiteralPath $sourceManifestPath -Raw | ConvertFrom-Json)
    if (@($sourceEntries | Group-Object Path | Where-Object Count -gt 1).Count -or @($snapshot.Entries | Group-Object FullName | Where-Object Count -gt 1).Count) { throw 'Duplicate frozen manifest/archive paths.' }
    if ($snapshot.Entries.Count -ne $sourceEntries.Count) { throw 'Frozen archive entry count mismatch.' }
    foreach ($sourceEntry in $sourceEntries) {
        $entry = $snapshot.GetEntry($sourceEntry.Path)
        if ($null -eq $entry) { throw "Missing frozen archive entry: $($sourceEntry.Path)" }
        $entryStream = $entry.Open()
        try { $entryHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($entryStream)) } finally { $entryStream.Dispose() }
        if ($entryHash -ne $sourceEntry.SHA256) { throw "Frozen archive content mismatch: $($sourceEntry.Path)" }
    }
} finally { $snapshot.Dispose() }
# A previously captured snapshot is immutable. Supplemental metadata may be
# captured later, with its own timestamp, without relabelling the source snapshot.
$nugetRoot = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)) '.nuget/packages' }
$licenseManifest = Get-Content -LiteralPath (Join-Path $repo 'src/NT.AvaloniaControl.PdfViewer/Licenses/PDFium-ThirdParty/verification-manifest.json') -Raw | ConvertFrom-Json
$native = @($licenseManifest.runtimes | ForEach-Object {
    $path = Join-Path $nugetRoot $_.native_nuget_path
    $actual = if (Test-Path -LiteralPath $path) { (Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant() } else { $null }
    [ordered]@{ RID = $_.rid; PackagePath = $_.native_nuget_path; ExpectedSHA256 = $_.native_sha256; ActualSHA256 = $actual; Matches = $actual -eq $_.native_sha256 }
})
if (@($native | Where-Object { -not $_.Matches }).Count) { throw 'Native asset hash mismatch/missing asset.' }
$include = Join-Path $nugetRoot 'bblanchon.pdfium.win32/155.0.8044/build/native/include/pdfium'
$headers = @('fpdfview.h','fpdf_save.h','fpdf_edit.h','fpdf_annot.h','fpdf_formfill.h','fpdf_signature.h','fpdf_text.h','fpdf_fwlevent.h') | ForEach-Object { [ordered]@{ Name = $_; SHA256 = (Get-FileHash -LiteralPath (Join-Path $include $_)).Hash } }
$nativePath = Join-Path $destination 'native-assets.json'
if (Test-Path -LiteralPath $nativePath) {
    $frozenNative = Get-Content -LiteralPath $nativePath -Raw | ConvertFrom-Json
    if ($frozenNative.Pdfium -ne '155.0.8044' -or @($frozenNative.Assets).Count -ne 12 -or @($frozenNative.Headers).Count -ne @($headers).Count) { throw 'Frozen native metadata scope mismatch.' }
    foreach ($asset in $native) { if (@($frozenNative.Assets | Where-Object { $_.RID -eq $asset.RID -and $_.ActualSHA256 -eq $asset.ActualSHA256 -and $_.Matches }).Count -ne 1) { throw 'Frozen native asset metadata mismatch.' } }
    foreach ($header in $headers) { if (@($frozenNative.Headers | Where-Object { $_.Name -eq $header.Name -and $_.SHA256 -eq $header.SHA256 }).Count -ne 1) { throw 'Frozen header metadata mismatch.' } }
} else {
    [ordered]@{ CapturedUtc = [DateTimeOffset]::UtcNow.ToString('o'); Pdfium = '155.0.8044'; Assets = $native; Headers = @($headers) } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $nativePath -Encoding utf8
}
if (-not (Test-Path -LiteralPath (Join-Path $destination 'targets.json'))) {
[ordered]@{
    CapturedUtc = [DateTimeOffset]::UtcNow.ToString('o')
    Framework = '.NET 10 / Avalonia 12.0.3 / PDFium 155.0.8044'
    Windows = @{ Target = 'Windows 11 x64'; Actual = [Runtime.InteropServices.RuntimeInformation]::OSDescription; Status = 'native-backend-probes-on-current-machine'; UIAndIME = 'not-part-of-these-probes' }
    Ubuntu = @{ Target = 'Ubuntu 24.04 LTS x64'; Display = 'X11 and XWayland'; Status = 'not-yet-verified'; CI = $(if (Test-Path -LiteralPath (Join-Path $repo '.github/workflows/pdfviewer-native.yml')) { 'configured-awaiting-run' } else { 'planned' }) }
    KylinX64 = @{ Target = 'Kylin x64'; ExactRelease = 'TBD-on-target-device'; Status = 'device-not-available'; RequiredForRelease = $true }
    KylinArm64 = @{ Target = 'Kylin ARM64'; ExactRelease = 'TBD-on-target-device'; Status = 'device-not-available'; RequiredForRelease = $true }
    WindowsArm64 = @{ Status = 'extension-not-main-release-gate' }
    OfficialRuntime = @{ Package = 'Avalonia.Controls.PdfViewer'; Version = $null; Status = 'licensed-runnable-reference-not-available'; DocumentSnapshotIsRuntimeVersion = $false }
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $destination 'targets.json') -Encoding utf8
}
if ($DownloadOfficialDocuments) {
    $webDirectory = Join-Path $destination 'official-documents'
    [IO.Directory]::CreateDirectory($webDirectory) | Out-Null
    $existingManifest = Join-Path $webDirectory 'manifest.json'
    if (Test-Path -LiteralPath $existingManifest) {
        $frozenDocuments = @(Get-Content -LiteralPath $existingManifest -Raw | ConvertFrom-Json)
        if ($frozenDocuments.Count -ne 7 -or @($frozenDocuments | Group-Object File | Where-Object Count -gt 1).Count) { throw 'Frozen official document manifest scope mismatch.' }
        foreach ($entry in $frozenDocuments) {
            if ((Get-FileHash -LiteralPath (Join-Path $webDirectory $entry.File)).Hash -ne $entry.SHA256) { throw 'Frozen official document hash mismatch.' }
        }
        Write-Output "Frozen evidence already present and verified: $destination"
        return
    }
    $pages = @('','loading-and-saving','navigation-and-search','annotations','printing-and-sharing','theming-and-localization','platforms-and-performance')
    $documents = @($pages | ForEach-Object {
        $name = if ($_ -eq '') { 'index' } else { $_ }
        $url = 'https://docs.avaloniaui.net/controls/data-display/pdfviewer/' + $_
        $file = Join-Path $webDirectory ($name + '.html')
        $response = Invoke-WebRequest -Uri $url -TimeoutSec 30
        [IO.File]::WriteAllText($file, $response.Content, [Text.UTF8Encoding]::new($false))
        [ordered]@{ URL = $url; RetrievedUtc = [DateTimeOffset]::UtcNow.ToString('o'); File = $name + '.html'; SHA256 = (Get-FileHash -LiteralPath $file).Hash; Bytes = (Get-Item -LiteralPath $file).Length; HttpStatus = [int]$response.StatusCode; Scope = 'Public documentation snapshot; not a commercial assembly/version freeze' }
    })
    $documents | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $webDirectory 'manifest.json') -Encoding utf8
}
Write-Output "Frozen evidence: $destination"
} finally { Pop-Location }
