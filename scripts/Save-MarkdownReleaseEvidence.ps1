[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SourceRoot,
    [Parameter(Mandatory)][string[]]$PackagePaths,
    [Parameter(Mandatory)][string[]]$AssemblyPaths,
    [Parameter(Mandatory)][string]$PublicApiPath,
    [Parameter(Mandatory)][string]$ConsumerSummaryPath,
    [Parameter(Mandatory)][string[]]$ConsumerResultPaths,
    [Parameter(Mandatory)][string]$VerifyResultsPath,
    [Parameter(Mandatory)][string[]]$VerifyXmlPaths,
    [string]$WindowsSmokePath,
    [string]$WindowsSmokeSourcePath,
    [string]$LinuxSmokePath,
    [string]$LinuxSmokeSourcePath,
    [string]$WindowsPerformanceGuardPath,
    [string]$LinuxPerformanceGuardPath,
    [string]$ArchiveRoot
)
$ErrorActionPreference = 'Stop'
$evidenceRepo = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$products = @('NT.AvaloniaControl.Markdown', 'NT.AvaloniaControl.Markdown.Themes', 'NT.AvaloniaControl.Markdown.TextMate')
$assemblyNames = @($products) + 'Markdown.NativeBenchmarks'
$testNames = @('TreeDataGrid.Core.Tests', 'TreeDataGrid.Headless.Tests', 'TreeDataGrid.Themes.Tests',
    'VirtualKeyboard.Core.Tests', 'VirtualKeyboard.Headless.Tests', 'VirtualKeyboard.Themes.Tests',
    'Markdown.Core.Tests', 'Markdown.Streaming.Tests', 'Markdown.Headless.Tests', 'Markdown.Themes.Tests', 'Markdown.TextMate.Tests')
function File-Path([string]$Path) {
    $item = Get-Item -LiteralPath $Path
    if ($item.PSIsContainer) { throw "Expected a file: $Path" }
    return $item.FullName
}
function Hash([string]$Path) { return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash }
function Json([string]$Path) { return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json }
function Json-Record([string]$Path) {
    $full = File-Path $Path
    $sha = Hash $full
    $value = Json $full
    Same-Hash (Hash $full) $sha "Evidence changed while being read: $full"
    return [pscustomobject]@{ Path=$full; SHA256=$sha; Value=$value }
}
function True-Value($Value, [string]$Label) {
    if ($Value -isnot [bool] -or -not $Value) { throw "$Label must be a true JSON boolean." }
}
function Integer-Value($Value, [string]$Label) {
    if ($null -eq $Value -or $Value -is [string] -or $Value -is [bool] -or
        [double]::IsNaN([double]$Value) -or [double]::IsInfinity([double]$Value) -or
        [double]$Value -lt 0 -or [math]::Truncate([double]$Value) -ne [double]$Value) {
        throw "$Label must be a nonnegative integer JSON number."
    }
    return [long]$Value
}
function Same-Hash([string]$Actual, [string]$Expected, [string]$Label) {
    if ($Actual -notmatch '^[0-9A-Fa-f]{64}$' -or $Expected -notmatch '^[0-9A-Fa-f]{64}$' -or $Actual -ine $Expected) {
        throw "$Label SHA256 mismatch."
    }
}
function Read-Xml([IO.Stream]$Stream) {
    $settings = [Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $reader = [Xml.XmlReader]::Create($Stream, $settings)
    try { $document = [Xml.XmlDocument]::new(); $document.XmlResolver = $null; $document.Load($reader); return ,$document }
    finally { $reader.Dispose() }
}
function Ref([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return [pscustomobject]@{ Status='Pending'; Path=$null; SHA256=$null; DeclaredPassed=$null; Reason='Not provided.' } }
    $full = [IO.Path]::GetFullPath($Path)
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { return [pscustomobject]@{ Status='Pending'; Path=$full; SHA256=$null; DeclaredPassed=$null; Reason='Specified evidence file is unavailable.' } }
    $record = Json-Record $full
    $value = $record.Value
    $declared = $null
    foreach ($name in @('全部通过', '通过', 'passed')) {
        $property = $value.PSObject.Properties[$name]
        if ($null -ne $property) {
            if ($property.Value -isnot [bool]) { throw "Native reference $name must be a JSON boolean: $full" }
            $declared = $property.Value; break
        }
    }
    return [pscustomobject]@{ Status='Recorded'; Path=$full; SHA256=$record.SHA256; DeclaredPassed=$declared; Reason='Immutable reference only; this archive does not rerun native validation or establish source/platform identity.' }
}

# Validate all required software evidence before creating the fresh archive.
$source = (Get-Item -LiteralPath $SourceRoot).FullName
if (-not (Test-Path -LiteralPath $source -PathType Container)) { throw 'SourceRoot must be a directory.' }
if ($AssemblyPaths.Count -ne 4 -or $PackagePaths.Count -ne 3 -or $ConsumerResultPaths.Count -ne 2 -or $VerifyXmlPaths.Count -ne 11) {
    throw 'Specify exactly four assemblies, three packages, two consumer results and eleven Verify XML files.'
}
$assemblies = @{}
foreach ($path in $AssemblyPaths) {
    $full = File-Path $path; $name = [IO.Path]::GetFileNameWithoutExtension($full)
    if ($name -notin $assemblyNames -or $assemblies.ContainsKey($name)) { throw "Unexpected or duplicate assembly: $name" }
    $assemblies[$name] = [pscustomobject]@{ Name=$name; Path=$full; SHA256=(Hash $full) }
}
foreach ($name in $assemblyNames) { if (-not $assemblies.ContainsKey($name)) { throw "Missing assembly: $name" } }
foreach ($name in $products) {
    $hostDependency = File-Path (Join-Path (Split-Path $assemblies['Markdown.NativeBenchmarks'].Path -Parent) "$name.dll")
    Same-Hash (Hash $hostDependency) $assemblies[$name].SHA256 "NativeHost dependency $name"
}
$apiPath = File-Path $PublicApiPath
$apiSha = Hash $apiPath
$apiText = [IO.File]::ReadAllText($apiPath)
Same-Hash (Hash $apiPath) $apiSha 'API evidence changed while being read'
$apiHashes = @{}
foreach ($match in [regex]::Matches($apiText, '(?m)^Assembly: (NT\.AvaloniaControl\.Markdown(?:\.Themes|\.TextMate)?),[^\r\n]*\r?\nSHA256: ([0-9A-Fa-f]{64})\s*$')) {
    if ($apiHashes.ContainsKey($match.Groups[1].Value)) { throw 'Duplicate assembly SHA marker in API snapshot.' }
    $apiHashes[$match.Groups[1].Value] = $match.Groups[2].Value
}
if ($apiHashes.Count -ne 3) { throw 'API snapshot must contain exactly three product assembly SHA markers.' }
foreach ($name in $products) { Same-Hash $apiHashes[$name] $assemblies[$name].SHA256 "API marker $name" }

$packages = @{}
foreach ($path in $PackagePaths) {
    $full = File-Path $path
    $packageSha = Hash $full
    $zip = [IO.Compression.ZipFile]::OpenRead($full)
    try {
        $nuspecs = @($zip.Entries | Where-Object { $_.FullName -like '*.nuspec' })
        if ($nuspecs.Count -ne 1) { throw "Package has ambiguous nuspec: $full" }
        $stream = $nuspecs[0].Open()
        try { $nuspec = Read-Xml $stream } finally { $stream.Dispose() }
        $id = $nuspec.SelectSingleNode("/*[local-name()='package']/*[local-name()='metadata']/*[local-name()='id']").InnerText
        $version = $nuspec.SelectSingleNode("/*[local-name()='package']/*[local-name()='metadata']/*[local-name()='version']").InnerText
        if ($id -notin $products -or $packages.ContainsKey($id) -or $version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$') { throw "Unexpected/duplicate package ID or invalid version: $full" }
        $dllEntryName = "lib/net10.0/$id.dll"
        $entries = @($zip.Entries | Where-Object { $_.FullName -ceq $dllEntryName })
        if ($entries.Count -ne 1) { throw "Package must have one $dllEntryName implementation DLL." }
        $stream = $entries[0].Open(); $algorithm = [Security.Cryptography.SHA256]::Create()
        try { $dllHash = [Convert]::ToHexString($algorithm.ComputeHash($stream)) }
        finally { $algorithm.Dispose(); $stream.Dispose() }
        Same-Hash $dllHash $assemblies[$id].SHA256 "Package DLL $id"
        Same-Hash (Hash $full) $packageSha "Package changed while being read: $id"
        $packages[$id] = [pscustomobject]@{ ID=$id; Version=$version; Path=$full; SHA256=$packageSha; DllEntry=$dllEntryName; DllSHA256=$dllHash }
    } finally { $zip.Dispose() }
}

$summaryRecord = Json-Record $ConsumerSummaryPath; $summaryPath=$summaryRecord.Path; $consumerSummary=$summaryRecord.Value
True-Value $consumerSummary.全部通过 'Consumer summary'
if ($consumerSummary.包 -isnot [array] -or $consumerSummary.包.Count -ne 3 -or
    $consumerSummary.消费者 -isnot [array] -or $consumerSummary.消费者.Count -ne 2) { throw 'Consumer summary must cover three packages and two consumers.' }
$seenPackages = @{}
foreach ($entry in $consumerSummary.包) {
    if ($entry.包 -notin $products -or $seenPackages.ContainsKey($entry.包)) { throw 'Consumer package IDs differ.' }
    $seenPackages[$entry.包] = $true
    Same-Hash $entry.SHA256 $packages[$entry.包].SHA256 "Consumed package $($entry.包)"
    if ($entry.版本 -cne $packages[$entry.包].Version) { throw 'Consumed package version differs.' }
}
$consumerResults = @{}
foreach ($path in $ConsumerResultPaths) {
    $record = Json-Record $path; $full=$record.Path; $value=$record.Value; $mode = [string]$value.模式
    if ($mode -notin @('FunctionOnly','DefaultTheme') -or $consumerResults.ContainsKey($mode)) { throw 'Consumer result modes must be unique FunctionOnly and DefaultTheme.' }
    True-Value $value.全部通过 "Consumer $mode"
    if ($null -ne $value.错误 -and -not [string]::IsNullOrEmpty([string]$value.错误)) { throw "Consumer $mode reports an error." }
    $checks = @($value.检查.PSObject.Properties)
    $minimumChecks = if ($mode -eq 'FunctionOnly') { 58 } else { 60 }
    if ($checks.Count -lt $minimumChecks) { throw "Consumer $mode has fewer than $minimumChecks complete consumer checks." }
    foreach ($check in $checks) { True-Value $check.Value "Consumer $mode check $($check.Name)" }
    $entry = @($consumerSummary.消费者 | Where-Object { $_.模式 -ceq $mode })
    if ($entry.Count -ne 1) { throw "Consumer summary mode mismatch: $mode" }
    True-Value $entry[0].全部通过 "Consumer summary $mode"
    if ((Integer-Value $entry[0].检查数 "Consumer $mode check count") -ne $checks.Count) { throw "Consumer $mode check count differs." }
    if ($full -ine [IO.Path]::GetFullPath((Join-Path $entry[0].消费目录 'result.json'))) { throw "Consumer $mode result is not the summary's actual run." }
    if ($value.包版本 -cne $packages[$products[0]].Version) { throw "Consumer $mode package version differs." }
    $binaryRoot = Join-Path $entry[0].消费目录 'bin/Release/net10.0'
    $consumed = if ($mode -eq 'FunctionOnly') { @($products[0]) } else { $products }
    foreach ($name in $consumed) { Same-Hash (Hash (File-Path (Join-Path $binaryRoot "$name.dll"))) $assemblies[$name].SHA256 "Consumer runtime $mode/$name" }
    if ($mode -eq 'FunctionOnly') {
        foreach ($name in $products[1..2]) { if (Test-Path -LiteralPath (Join-Path $binaryRoot "$name.dll")) { throw 'FunctionOnly has an optional product DLL.' } }
    }
    $consumerResults[$mode] = [pscustomobject]@{ Mode=$mode; Path=$full; SHA256=$record.SHA256; Checks=$checks.Count }
}

$verifyRecord = Json-Record $VerifyResultsPath; $verifyPath=$verifyRecord.Path; $verify=$verifyRecord.Value
True-Value $verify.passed 'Verify passed'
if ($verify.consumerSkipped -isnot [bool] -or $verify.consumerSkipped) { throw 'Verify skipped consumers.' }
Same-Hash $verify.markdownCoreSha256 $assemblies[$products[0]].SHA256 'Verify Core'
if ($verify.tests -isnot [array] -or $verify.tests.Count -ne 11) { throw 'Verify must contain eleven test projects.' }
$xmlPaths = @{}
foreach ($path in $VerifyXmlPaths) { $full=File-Path $path; if ($xmlPaths.ContainsKey($full)) { throw 'Duplicate Verify XML path.' }; $xmlPaths[$full]=$true }
$xmlRecords = @(); $testTotal=0L; $seenTests=@{}
foreach ($test in $verify.tests) {
    if ($test.project -notin $testNames -or $seenTests.ContainsKey($test.project)) { throw 'Verify test projects differ.' }
    $seenTests[$test.project]=$true
    foreach ($field in @('exitCode','failed','errors','skipped','notRun')) {
        if ((Integer-Value $test.$field "Verify $($test.project)/$field") -ne 0) { throw "Verify $($test.project) has $field." }
    }
    $total=Integer-Value $test.total "Verify $($test.project)/total"
    if ($total -le 0 -or (Integer-Value $test.passed "Verify $($test.project)/passed") -ne $total) { throw 'Verify project did not pass all tests.' }
    $xmlPath=File-Path $test.resultsXml
    if (-not $xmlPaths.ContainsKey($xmlPath)) { throw 'Explicit XML paths do not match Verify results.' }
    $xmlSha=Hash $xmlPath; $stream=[IO.File]::OpenRead($xmlPath)
    try { $xml=Read-Xml $stream } finally { $stream.Dispose() }
    $nodes=@($xml.SelectNodes('/assemblies/assembly'))
    if ($nodes.Count -ne 1) { throw 'Verify XML must contain one xUnit assembly.' }
    $node=$nodes[0]
    foreach ($mapping in @(@('total',$total),@('passed',$total),@('failed',0),@('errors',0),@('skipped',0),@('not-run',0))) {
        $text=$node.GetAttribute([string]$mapping[0])
        if ($text -notmatch '^\d+$' -or [long]$text -ne [long]$mapping[1]) { throw "Verify XML count differs: $($test.project)/$($mapping[0])" }
    }
    $testNodes=@($node.SelectNodes('.//test'))
    if ($testNodes.Count -ne $total -or @($testNodes|Where-Object { $_.GetAttribute('result') -cne 'Pass' }).Count -ne 0 -or
        @($xml.SelectNodes('//errors/error')).Count -ne 0) { throw "Verify XML test/error bodies differ: $($test.project)" }
    $testTotal+=$total
    Same-Hash (Hash $xmlPath) $xmlSha "Verify XML changed while being read: $($test.project)"
    $xmlRecords+=[pscustomobject]@{Project=$test.project;Path=$xmlPath;SHA256=$xmlSha;Total=$total;TestAssemblySHA256=$test.assemblySha256}
}
if ((Integer-Value $verify.testTotal 'Verify total') -ne $testTotal) { throw 'Verify aggregate total differs.' }

$nativeRefs=[ordered]@{WindowsSmoke=(Ref $WindowsSmokePath);WindowsSmokeSource=(Ref $WindowsSmokeSourcePath);
    LinuxSmoke=(Ref $LinuxSmokePath);LinuxSmokeSource=(Ref $LinuxSmokeSourcePath);
    WindowsPerformanceGuard=(Ref $WindowsPerformanceGuardPath);LinuxPerformanceGuard=(Ref $LinuxPerformanceGuardPath)}
$sourceFiles=@()
Push-Location $source
try {
    $paths=@(Get-ChildItem -LiteralPath $source -File|ForEach-Object Name)
    $dirs=@('src','samples','tests','benchmarks','docs','scripts','.github'|Where-Object {Test-Path -LiteralPath $_ -PathType Container})
    if (Get-Command rg -ErrorAction SilentlyContinue) { $paths+=@(rg --files --hidden @dirs -g '!**/bin/**' -g '!**/obj/**'); if ($LASTEXITCODE -gt 1) { throw 'Source file inventory failed.' } }
    else { foreach($dir in $dirs){$paths+=@(Get-ChildItem -LiteralPath $dir -Recurse -File|Where-Object {$_.FullName -notmatch '[\\/](bin|obj)[\\/]'}|ForEach-Object {[IO.Path]::GetRelativePath($source,$_.FullName)})} }
    foreach($path in ($paths|Sort-Object -Unique)){$sourceFiles+=[pscustomobject]@{Path=$path;SHA256=(Hash (Join-Path $source $path))}}
} finally { Pop-Location }

# New Verify evidence proves the enumerated sources stayed unchanged for that
# complete run. Older evidence is retained without retroactive source attribution.
$sourceFields=@('sourceManifest','sourceManifestSha256','sourcesUnchangedDuringVerification')
$presentFields=@($sourceFields|Where-Object {$null -ne $verify.PSObject.Properties[$_]})
$verifySource=[pscustomobject]@{Classification='HistoricalSourceUnproven';CurrentSourcesVerified=$false;
    Path=$null;SHA256=$null;FileCount=$null;Reason='Historical Verify has no source manifest fields; current source inventory cannot establish the source provenance of that historical run.'}
$verifySourceRecord=$null
if ($presentFields.Count -gt 0) {
    if ($presentFields.Count -ne $sourceFields.Count) { throw 'Verify source provenance fields are incomplete.' }
    True-Value $verify.sourcesUnchangedDuringVerification 'Verify sourcesUnchangedDuringVerification'
    if ($verify.sourceManifest -isnot [string] -or [string]::IsNullOrWhiteSpace($verify.sourceManifest)) { throw 'Verify source manifest path is missing.' }
    $verifySourceRecord=Json-Record $verify.sourceManifest
    Same-Hash $verifySourceRecord.SHA256 $verify.sourceManifestSha256 'Verify source manifest'
    $entries=@($verifySourceRecord.Value)
    if ($verifySourceRecord.Value -isnot [array] -or -not $entries.Count) { throw 'Verify source manifest must contain a nonempty JSON array.' }
    $scopeDirs=@('src','tests','samples','benchmarks','scripts','.github')
    $scopeRootFiles=@('Directory.Build.props','Directory.Packages.props','NuGet.Config','global.json','NT.AvaloniaControl.slnx','.editorconfig')
    $current=@{}
    foreach ($file in $sourceFiles) {
        $relative=$file.Path.Replace('\','/')
        $top=$relative.Split('/')[0]
        if ($top -in $scopeDirs -or $relative -in $scopeRootFiles) { $current[$relative]=$file.SHA256 }
    }
    $sourcePrefix=$source.TrimEnd('\','/')+[IO.Path]::DirectorySeparatorChar
    $seen=@{}
    foreach ($entry in $entries) {
        if ($entry.path -isnot [string] -or [string]::IsNullOrWhiteSpace($entry.path) -or [IO.Path]::IsPathRooted($entry.path)) { throw 'Verify source manifest contains a nonrelative path.' }
        $full=[IO.Path]::GetFullPath((Join-Path $source $entry.path))
        if (-not $full.StartsWith($sourcePrefix,[StringComparison]::OrdinalIgnoreCase)) { throw 'Verify source manifest escapes SourceRoot.' }
        $relative=[IO.Path]::GetRelativePath($source,$full).Replace('\','/')
        if ($seen.ContainsKey($relative) -or -not $current.ContainsKey($relative)) { throw "Verify source manifest path is duplicate or outside its current scope: $relative" }
        $seen[$relative]=$true
        Same-Hash $current[$relative] $entry.sha256 "Verify current source $relative"
    }
    if ($seen.Count -ne $current.Count) { throw 'Current Verify source scope has additional or missing files.' }
    $verifySource=[pscustomobject]@{Classification='VerifiedCurrentSource';CurrentSourcesVerified=$true;
        Path=$verifySourceRecord.Path;SHA256=$verifySourceRecord.SHA256;FileCount=$entries.Count;
        Reason='Passed Verify reports unchanged sources; its manifest hash, complete scope and every current source file hash were validated.'}
}

if (-not $ArchiveRoot) { $ArchiveRoot=Join-Path $evidenceRepo 'artifacts/markdown-release-evidence' }
$archiveBase=[IO.Path]::GetFullPath($ArchiveRoot)
$allowed=[IO.Path]::GetFullPath((Join-Path $evidenceRepo 'artifacts'))+[IO.Path]::DirectorySeparatorChar
if (-not ($archiveBase+[IO.Path]::DirectorySeparatorChar).StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)) { throw 'ArchiveRoot must stay in workspace artifacts.' }
$archive=Join-Path $archiveBase ([guid]::NewGuid().ToString('N'))
if (Test-Path -LiteralPath $archive) { throw 'Fresh immutable archive GUID already exists.' }
New-Item -ItemType Directory -Path $archive -Force|Out-Null
$copies=[Collections.Generic.List[object]]::new()
function Copy-Evidence([string]$Path,[string]$Relative,[string]$ExpectedHash) {
    $target=[IO.Path]::GetFullPath((Join-Path $archive $Relative))
    if (-not $target.StartsWith($archive+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Archive copy escapes its fresh GUID directory.' }
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force|Out-Null
    if (Test-Path -LiteralPath $target) { throw "Archive collision: $Relative" }
    Copy-Item -LiteralPath $Path -Destination $target
    Same-Hash (Hash $target) $ExpectedHash "Archived copy $Relative"
    $copies.Add([pscustomobject]@{Source=$Path;Copy=$Relative;SHA256=$ExpectedHash})
}
try {
    foreach($name in $products){Copy-Evidence $packages[$name].Path ("packages/$name.$($packages[$name].Version).nupkg") $packages[$name].SHA256}
    foreach($name in $assemblyNames){Copy-Evidence $assemblies[$name].Path "assemblies/$name.dll" $assemblies[$name].SHA256}
    Copy-Evidence $apiPath 'api/markdown-public-api.txt' $apiSha
    Copy-Evidence $summaryPath 'consumers/summary.json' $summaryRecord.SHA256
    foreach($mode in @('FunctionOnly','DefaultTheme')){Copy-Evidence $consumerResults[$mode].Path "consumers/$mode/result.json" $consumerResults[$mode].SHA256}
    Copy-Evidence $verifyPath 'verification/results.json' $verifyRecord.SHA256
    if ($null -ne $verifySourceRecord) { Copy-Evidence $verifySourceRecord.Path 'verification/source-manifest.json' $verifySourceRecord.SHA256 }
    foreach($entry in $xmlRecords){Copy-Evidence $entry.Path "verification/$($entry.Project).xml" $entry.SHA256}
    foreach($name in $nativeRefs.Keys){$reference=$nativeRefs[$name];if($reference.Status -eq 'Recorded'){Copy-Evidence $reference.Path "native-references/$name.json" $reference.SHA256}}
    $sourceFiles|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $archive 'source-current-files.json') -Encoding utf8
    $archiveStatus=if ($verifySource.CurrentSourcesVerified) { 'SoftwareEvidencePassed' } else { 'HistoricalSourceUnproven' }
    [pscustomobject]@{CreatedUtc=[DateTimeOffset]::UtcNow;Status=$archiveStatus;SoftwareEvidencePassed=$verifySource.CurrentSourcesVerified;RequiredSoftwareHashChainPassed=$true;
        Published=$false;VersionChanged=$false;ArchivePath=$archive;SourceRoot=$source;SourceFileCount=$sourceFiles.Count;
        SourceManifestSHA256=(Hash (Join-Path $archive 'source-current-files.json'));SourceScope='Current source files at archive time. This is not proof of old Verify/build/native source provenance.';
        Assemblies=@($assemblyNames|ForEach-Object {$assemblies[$_]});Packages=@($products|ForEach-Object {$packages[$_]});ApiFileSHA256=(Hash (Join-Path $archive 'api/markdown-public-api.txt'));
        ConsumerModes=@($consumerResults.Values);VerifyTestTotal=$testTotal;VerifyXml=$xmlRecords;VerifySource=$verifySource;NativeReferences=[pscustomobject]$nativeRefs;Copies=$copies;
        Notes='Required software hash chain and full consumers/XML validated. Missing native guards remain Pending; recorded native declarations are references, not new performance/platform pass claims. No build/test/native/publish/version action executed.'}|
        ConvertTo-Json -Depth 10|Set-Content -LiteralPath (Join-Path $archive 'manifest.json') -Encoding utf8
    foreach($file in (Get-ChildItem -LiteralPath $archive -Recurse -File)){$file.IsReadOnly=$true}
} catch {
    [pscustomobject]@{CreatedUtc=[DateTimeOffset]::UtcNow;Status='Rejected';SoftwareEvidencePassed=$false;Error=$_.Exception.Message}|
        ConvertTo-Json|Set-Content -LiteralPath (Join-Path $archive 'archive-rejected.json') -Encoding utf8
    throw
}
Write-Output "Markdown software release evidence: $archive"
