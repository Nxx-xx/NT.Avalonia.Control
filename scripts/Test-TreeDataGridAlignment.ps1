param([string]$OutputDirectory = ('artifacts/treedatagrid-alignment/a6-' + [guid]::NewGuid().ToString('N')), [string]$DependencyFeed)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'TreeDataGrid.Validation.ps1')
$a6Repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$a6Output=if([IO.Path]::IsPathRooted($OutputDirectory)){[IO.Path]::GetFullPath($OutputDirectory)}else{[IO.Path]::GetFullPath((Join-Path $a6Repo $OutputDirectory))}
if (Test-Path -LiteralPath (Join-Path $a6Output 'results.json')) { throw 'Use a new evidence directory; previous results must remain intact.' }
New-Item -ItemType Directory -Path $a6Output -Force | Out-Null
$a6Started=[DateTimeOffset]::UtcNow; $a6Steps=[Collections.Generic.List[object]]::new(); $a6Tests=[Collections.Generic.List[object]]::new()
$a6Passed=$false; $a6Error=$null; $a6SourcesUnchanged=$false; $a6OldProfile=$env:TREE_DATAGRID_A6_OUTPUT_DIRECTORY
$a6RestoreArgs=@()
if($DependencyFeed) {
    $DependencyFeed=[IO.Path]::GetFullPath($DependencyFeed)
    if(-not(Test-Path -LiteralPath $DependencyFeed -PathType Container)){throw 'DependencyFeed must be a local directory.'}
    if(@(Get-ChildItem -LiteralPath $DependencyFeed -Recurse -Filter 'nt.avaloniacontrol.treedatagrid*.nupkg').Count){throw 'DependencyFeed must not contain product packages.'}
    $a6RestoreConfig=Join-Path $a6Output 'NuGet.Offline.Config'
    ('<configuration><packageSources><clear /><add key="offline-dependencies" value="'+[System.Security.SecurityElement]::Escape($DependencyFeed)+'" /></packageSources></configuration>') | Set-Content -LiteralPath $a6RestoreConfig -Encoding utf8
    $a6RestoreArgs=@('--configfile',$a6RestoreConfig)
}
function Invoke-A6Step([string]$Name, [scriptblock]$Action) {
    $a6Log=Join-Path $a6Output ($Name+'.log'); $a6StepPassed=$false
    try { & $Action *>&1 | Tee-Object -FilePath $a6Log; $a6StepPassed=$true }
    finally { $a6Steps.Add([ordered]@{name=$Name;passed=$a6StepPassed;log=$a6Log}) }
}
function Assert-A6Exit([string]$Name) { if ($LASTEXITCODE -ne 0) { throw "$Name failed with exit $LASTEXITCODE" } }
function Read-A6Test([string]$Xml) {
    $a6Result=([xml](Get-Content -LiteralPath $Xml -Raw)).assemblies.assembly
    if ([int]$a6Result.total -le 0 -or [int]$a6Result.total -ne [int]$a6Result.passed -or [int]$a6Result.failed -ne 0 -or [int]$a6Result.errors -ne 0 -or [int]$a6Result.skipped -ne 0 -or [int]$a6Result.'not-run' -ne 0) { throw "Incomplete tests: $Xml" }
    [ordered]@{total=[int]$a6Result.total;passed=[int]$a6Result.passed;failed=[int]$a6Result.failed;errors=[int]$a6Result.errors;skipped=[int]$a6Result.skipped;notRun=[int]$a6Result.'not-run';xml=$Xml}
}
function Get-A6Manifest {
    $a6Files=@(& rg --files src/NT.AvaloniaControl.TreeDataGrid src/NT.AvaloniaControl.TreeDataGrid.Themes tests/TreeDataGrid.Core.Tests tests/TreeDataGrid.Headless.Tests tests/TreeDataGrid.Themes.Tests samples/TreeDataGrid.Usage samples/NT.AvaloniaControl.Demo/Pages/TreeDataGrid benchmarks/TreeDataGrid.Benchmarks -g '!**/bin/**' -g '!**/obj/**') + @(
        'Directory.Build.props','Directory.Packages.props','NuGet.Config','global.json','docs/public-api.txt','docs/validation/p1-public-api.txt','docs/validation/p2-p3-public-api.txt','docs/validation/p4-p5-public-api.txt',
        'docs/treedatagrid-binding-support.md','docs/treedatagrid-row-move-support.md','docs/treedatagrid-element-factory.md','docs/treedatagrid-platform-matrix.template.json',
        'scripts/Test-PackageConsumer.ps1','scripts/Test-TreeDataGridFunctionConsumer.ps1','scripts/Test-TreeDataGridNative.ps1','scripts/Test-TreeDataGridAlignment.ps1','scripts/Measure-TreeDataGrid.ps1','scripts/Collect-Platform.ps1',
        'scripts/verify_treedatagrid_acceptance.py','scripts/Tests/test_treedatagrid_acceptance.py','scripts/TreeDataGrid.Validation.ps1','scripts/Export-TreeDataGridPreview.py','scripts/Export-TreeDataGridPreview.ps1','scripts/Tests/test_treedatagrid_closeout.py','.github/workflows/treedatagrid-native.yml',
        'samples/NT.AvaloniaControl.Demo/MainWindow.cs','samples/NT.AvaloniaControl.Demo/DemoPageCatalog.cs','tests/Demo.Headless.Tests/GalleryNavigationTests.cs','tests/Demo.Headless.Tests/TestApplication.cs','tests/Demo.Headless.Tests/Demo.Headless.Tests.csproj')
    if ($LASTEXITCODE -ne 0) { throw 'Source enumeration failed.' }
    if($DependencyFeed){$a6Files+=@(Get-ChildItem -LiteralPath $DependencyFeed -Recurse -Filter *.nupkg | ForEach-Object FullName)}
    @($a6Files | Sort-Object -Unique | ForEach-Object { [ordered]@{path=$_.Replace('\','/');sha256=(Get-FileHash -LiteralPath $_).Hash} })
}
function Get-A6Binary([string]$Path) {
    if ($Path.Contains('!')) {
        $a6Parts=$Path.Split('!'); $a6Zip=[IO.Compression.ZipFile]::OpenRead((Join-Path $a6Repo $a6Parts[0]))
        try { $a6Stream=$a6Zip.GetEntry($a6Parts[1]).Open(); try { $a6Hash=(Get-FileHash -InputStream $a6Stream).Hash } finally { $a6Stream.Dispose() } } finally { $a6Zip.Dispose() }
    } else { $a6Hash=(Get-FileHash -LiteralPath $Path).Hash }
    [ordered]@{path=$Path;sha256=$a6Hash}
}
Push-Location $a6Repo
try {
    $env:TREE_DATAGRID_A6_OUTPUT_DIRECTORY=$null
    Invoke-A6Step 'disk-preflight' {
        Assert-TreeDataGridDiskBudget -OutputDirectory $a6Output -Paths @($a6Output,$a6Repo,[IO.Path]::GetTempPath(),$(if($env:NUGET_PACKAGES){$env:NUGET_PACKAGES}else{Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget/packages'})) -EstimatedBytes 6GB
    }
    $a6Manifest=@(Get-A6Manifest); $a6Manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $a6Output 'source-manifest.json') -Encoding utf8
    Invoke-A6Step 'closeout-tools-tests' { & python -m unittest discover -s scripts/Tests -p test_treedatagrid_closeout.py -v; Assert-A6Exit 'Closeout tools tests' }
    foreach($a6Project in 'TreeDataGrid.Core.Tests','TreeDataGrid.Headless.Tests','TreeDataGrid.Themes.Tests') {
        Invoke-A6Step $a6Project {
            & dotnet restore "tests/$a6Project/$a6Project.csproj" --locked-mode --disable-parallel -m:1 @a6RestoreArgs; Assert-A6Exit 'Locked restore'
            & dotnet build "tests/$a6Project/$a6Project.csproj" -c Release --no-restore -m:1; Assert-A6Exit 'Release build'
            $a6Xml=Join-Path $a6Output ($a6Project+'.xml')
            & dotnet "tests/$a6Project/bin/Release/net10.0/$a6Project.dll" -noLogo -xml $a6Xml; Assert-A6Exit 'Controls tests'
            $a6Tests.Add([ordered]@{project=$a6Project;result=(Read-A6Test $a6Xml)})
        }
    }
    Invoke-A6Step 'acceptance-validator-tests' { & python -m unittest discover -s scripts/Tests -p test_treedatagrid_acceptance.py -v; Assert-A6Exit 'Acceptance validator tests' }
    Invoke-A6Step 'data-profile' {
        & dotnet restore benchmarks/TreeDataGrid.Benchmarks/TreeDataGrid.Benchmarks.csproj --locked-mode --disable-parallel -m:1 @a6RestoreArgs; Assert-A6Exit 'Benchmark restore'
        & dotnet build benchmarks/TreeDataGrid.Benchmarks/TreeDataGrid.Benchmarks.csproj -c Release --no-restore -m:1; Assert-A6Exit 'Benchmark build'
        & (Join-Path $PSScriptRoot 'Measure-TreeDataGrid.ps1') -Output (Join-Path $a6Output 'data-profile.json') -Samples 5
    }
    Invoke-A6Step 'ui-profile-and-retention' {
        $env:TREE_DATAGRID_A6_OUTPUT_DIRECTORY=Join-Path $a6Output 'profiles'
        try { & dotnet tests/TreeDataGrid.Headless.Tests/bin/Release/net10.0/TreeDataGrid.Headless.Tests.dll -noLogo -class TreeDataGrid.Headless.Tests.A6LifecycleTests -xml (Join-Path $a6Output 'expanded-lifecycle.xml'); Assert-A6Exit 'Expanded lifecycle tests' }
        finally { $env:TREE_DATAGRID_A6_OUTPUT_DIRECTORY=$null }
        $a6Profile=Get-Content -LiteralPath (Join-Path $a6Output 'profiles/ui-profile.json') -Raw | ConvertFrom-Json
        $a6Retention=Get-Content -LiteralPath (Join-Path $a6Output 'profiles/durability.json') -Raw | ConvertFrom-Json
        if ($a6Profile.passed -ne $true -or @($a6Profile.samples).Count -ne 15 -or $a6Retention.passed -ne $true -or $a6Retention.cycles -ne 40 -or @($a6Retention.retained | Where-Object retained -ne 0).Count -ne 0) { throw 'Incomplete UI/retention evidence.' }
    }
    Invoke-A6Step 'theme-package-consumer' { & (Join-Path $PSScriptRoot 'Test-PackageConsumer.ps1') -Control TreeDataGrid -DependencyFeed $DependencyFeed }
    $a6ThemePath=(Get-Content artifacts/treedatagrid-theme-consumer-path.txt -Raw).Trim()
    Invoke-A6Step 'function-package-consumer' { & (Join-Path $PSScriptRoot 'Test-TreeDataGridFunctionConsumer.ps1') -NoPack -DependencyFeed $DependencyFeed }
    $a6FunctionPath=(Get-Content artifacts/treedatagrid-function-consumer-path.txt -Raw).Trim()
    foreach($a6Consumer in $a6ThemePath,$a6FunctionPath) {
        $a6Consumption=Get-Content -LiteralPath (Join-Path $a6Consumer 'package-consumption.json') -Raw | ConvertFrom-Json
        if ($a6Consumption.passed -ne $true -or [DateTimeOffset]$a6Consumption.timestampUtc -lt $a6Started) { throw 'Stale consumer evidence.' }
    }
    foreach($a6Sample in 'TreeDataGrid.Usage','NT.AvaloniaControl.Demo') {
        Invoke-A6Step $a6Sample {
            & dotnet restore "samples/$a6Sample/$a6Sample.csproj" --locked-mode --disable-parallel -m:1 @a6RestoreArgs; Assert-A6Exit 'Sample restore'
            & dotnet build "samples/$a6Sample/$a6Sample.csproj" -c Release --no-restore -m:1; Assert-A6Exit 'Sample build'
        }
    }
    $a6Published=Join-Path $a6Output 'publish'
    Invoke-A6Step 'published-native' {
        & dotnet msbuild samples/NT.AvaloniaControl.Demo/NT.AvaloniaControl.Demo.csproj -t:Publish -p:NoBuild=true -p:Configuration=Release "-p:PublishDir=$a6Published/" -m:1 -nr:false -v:minimal; Assert-A6Exit 'Framework-dependent MSBuild Publish'
        & (Join-Path $PSScriptRoot 'Test-TreeDataGridNative.ps1') -NoBuild -ApplicationPath (Join-Path $a6Published 'NT.AvaloniaControl.Demo.dll') -OutputDirectory (Join-Path $a6Output 'native')
    }
    Invoke-A6Step 'tree-gallery-navigation' {
        & dotnet restore tests/Demo.Headless.Tests/Demo.Headless.Tests.csproj --locked-mode --disable-parallel -m:1 @a6RestoreArgs; Assert-A6Exit 'Gallery restore'
        & dotnet build tests/Demo.Headless.Tests/Demo.Headless.Tests.csproj -c Release --no-restore --no-dependencies -m:1; Assert-A6Exit 'Gallery build'
        $a6GalleryXml=Join-Path $a6Output 'tree-gallery.xml'
        & dotnet tests/Demo.Headless.Tests/bin/Release/net10.0/Demo.Headless.Tests.dll -noLogo -method Demo.Headless.Tests.GalleryNavigationTests.Tree_scenarios_and_mvvm_can_switch_without_reparenting_failures -xml $a6GalleryXml; Assert-A6Exit 'Gallery navigation'
        $a6Gallery=Read-A6Test $a6GalleryXml; if($a6Gallery.total -ne 1) { throw 'Unexpected gallery selection.' }
    }
    $a6Hashes=[ordered]@{}
    $a6FunctionDirs=@('src/NT.AvaloniaControl.TreeDataGrid','src/NT.AvaloniaControl.TreeDataGrid.Themes','tests/TreeDataGrid.Core.Tests','tests/TreeDataGrid.Headless.Tests','tests/TreeDataGrid.Themes.Tests','tests/Demo.Headless.Tests','samples/TreeDataGrid.Usage','samples/NT.AvaloniaControl.Demo',$a6ThemePath,$a6FunctionPath)
    $a6ThemeDirs=@('src/NT.AvaloniaControl.TreeDataGrid.Themes','tests/TreeDataGrid.Themes.Tests','tests/Demo.Headless.Tests','samples/TreeDataGrid.Usage','samples/NT.AvaloniaControl.Demo',$a6ThemePath)
    $a6Version=([xml](Get-Content Directory.Build.props -Raw)).Project.PropertyGroup.Version
    foreach($a6Group in 'function','theme','demo') {
        $a6Name=switch($a6Group){'function'{'NT.AvaloniaControl.TreeDataGrid'} 'theme'{'NT.AvaloniaControl.TreeDataGrid.Themes'} 'demo'{'NT.AvaloniaControl.Demo'}}
        $a6Dirs=switch($a6Group){'function'{$a6FunctionDirs} 'theme'{$a6ThemeDirs} 'demo'{@('samples/NT.AvaloniaControl.Demo','tests/Demo.Headless.Tests')}}
        $a6Files=@($a6Dirs | ForEach-Object { Get-A6Binary (Join-Path $_ "bin/Release/net10.0/$a6Name.dll") }) + @(Get-A6Binary (Join-Path $a6Published "$a6Name.dll"))
        if($a6Group -ne 'demo') { $a6Files+=Get-A6Binary "artifacts/packages/$a6Name.$a6Version.nupkg!lib/net10.0/$a6Name.dll" }
        $a6Match=@($a6Files.sha256 | Sort-Object -Unique).Count -eq 1
        $a6Hashes[$a6Group]=[ordered]@{allMatch=$a6Match;copies=$a6Files.Count;files=$a6Files}
        if(-not $a6Match) { throw "Binary mismatch: $a6Group" }
    }
    $a6Hashes | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $a6Output 'binary-hashes.json') -Encoding utf8
    $a6Final=@(Get-A6Manifest); $a6Final | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $a6Output 'source-manifest-final.json') -Encoding utf8
    $a6SourcesUnchanged=($a6Final | ConvertTo-Json -Compress) -ceq ($a6Manifest | ConvertTo-Json -Compress)
    if(-not $a6SourcesUnchanged){throw 'Scoped inputs changed during A6 validation.'}
    $a6Auto=[ordered]@{passed=$true;evidenceKind='LocalAutomated';completedRequirements=@('controls','performance','retention','packages','native-published');startedUtc=$a6Started;completedUtc=[DateTimeOffset]::UtcNow;tests=$a6Tests.ToArray();functionConsumer=$a6FunctionPath;themeConsumer=$a6ThemePath;restoreMode=$(if($DependencyFeed){'IsolatedOfflineCache'}else{'IsolatedNetworkCache'});sourceFiles=$a6Manifest.Count;sourcesUnchanged=$true;binaryHashes='binary-hashes.json';scope='Windows local automation; Headless profiles; current NuGets; framework-dependent published native app; no physical/clean-system/actual CI claims'}
    $a6AutoPath=Join-Path $a6Output 'automated-evidence.json'; $a6Auto | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath $a6AutoPath -Encoding utf8
    $a6Matrix=Get-Content docs/treedatagrid-platform-matrix.template.json -Raw | ConvertFrom-Json
    foreach($a6Requirement in $a6Matrix.requirements | Where-Object kind -eq 'automated') { $a6Requirement.status='Passed'; $a6Requirement.evidence=[ordered]@{path=[IO.Path]::GetRelativePath($a6Repo,$a6AutoPath);sha256=(Get-FileHash -LiteralPath $a6AutoPath).Hash} }
    if($env:GITHUB_ACTIONS -eq 'true') {
        $a6CiPath=Join-Path $a6Output 'ci-evidence.json'
        [ordered]@{passed=$true;evidenceKind='CiRun';jobUrl="$env:GITHUB_SERVER_URL/$env:GITHUB_REPOSITORY/actions/runs/$env:GITHUB_RUN_ID";runner=$env:RUNNER_OS;job=$env:GITHUB_JOB;nativeWindowCreated=$true} | ConvertTo-Json | Set-Content -LiteralPath $a6CiPath -Encoding utf8
        $a6CiRow=$a6Matrix.requirements | Where-Object id -eq 'windows-ci'; $a6CiRow.status='Passed'; $a6CiRow.evidence=[ordered]@{path=[IO.Path]::GetRelativePath($a6Repo,$a6CiPath);sha256=(Get-FileHash -LiteralPath $a6CiPath).Hash}
    }
    $a6MatrixPath=Join-Path $a6Output 'platform-matrix.json'; $a6Matrix | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath $a6MatrixPath -Encoding utf8
    Invoke-A6Step 'acceptance-gates' {
        foreach($a6Scope in 'local','windows','windows-kylin') {
            & python scripts/verify_treedatagrid_acceptance.py $a6MatrixPath --scope $a6Scope --root $a6Repo --output (Join-Path $a6Output ("acceptance-$a6Scope.json"))
            if ($a6Scope -eq 'local') { Assert-A6Exit 'Local acceptance' } elseif ($LASTEXITCODE -ne 2) { throw 'Release gates must remain explicitly incomplete without platform evidence.' }
        }
    }
    $a6Passed=$true
}
catch { $a6Error=$_.Exception.Message; throw }
finally {
    $env:TREE_DATAGRID_A6_OUTPUT_DIRECTORY=$a6OldProfile
    [ordered]@{stage='A6-local-automated';passed=$a6Passed;startedUtc=$a6Started;completedUtc=[DateTimeOffset]::UtcNow;error=$a6Error;sourcesUnchanged=$a6SourcesUnchanged;tests=$a6Tests.ToArray();steps=$a6Steps.ToArray();platformAcceptance='Pending physical Windows, actual CI and target Kylin evidence';fullSolutionVerify='NotRun: concurrent work in other controls'} |
        ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $a6Output 'results.json') -Encoding utf8
    try { Write-TreeDataGridValidationSummary -OutputDirectory $a6Output } finally { Pop-Location }
}
