param([Parameter(Mandatory)][string]$ProbePath)
$ErrorActionPreference='Stop'
$linuxUpdateRepo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$probe=[IO.Path]::GetFullPath($ProbePath)
$allowed=[IO.Path]::GetFullPath((Join-Path $linuxUpdateRepo 'artifacts/linux-build'))+[IO.Path]::DirectorySeparatorChar
if(-not $probe.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase) -or -not(Test-Path -LiteralPath (Join-Path $probe 'build-passed.txt'))){throw 'Reuse only a previously compiled workspace Linux probe.'}
$source=Join-Path $probe 'source';$revision=Join-Path $probe ('revision-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $revision -Force|Out-Null
$files=@(Get-ChildItem -LiteralPath $linuxUpdateRepo -File|ForEach-Object Name)
Push-Location $linuxUpdateRepo
try{$files+=@(rg --files --hidden src samples tests benchmarks docs scripts .github -g '!**/bin/**' -g '!**/obj/**')}
finally{Pop-Location}
# Preserve complete prior binaries and runtime assets before the cache-backed rebuild.
foreach($project in @($files|Where-Object {$_ -like '*.csproj'})){
    $projectDirectory=Split-Path $project -Parent;$previousBin=Join-Path $source "$projectDirectory/bin/Release/net10.0"
    if(Test-Path -LiteralPath $previousBin){
        $backup=Join-Path $revision "previous-output/$projectDirectory"
        New-Item -ItemType Directory -Path $backup -Force|Out-Null
        Copy-Item -LiteralPath $previousBin -Destination $backup -Recurse
    }
}
$manifest=@();$changes=@()
foreach($file in $files){
    $current=Join-Path $linuxUpdateRepo $file;$target=Join-Path $source $file;$sha=(Get-FileHash -LiteralPath $current).Hash
    $old=if(Test-Path -LiteralPath $target){(Get-FileHash -LiteralPath $target).Hash}else{$null}
    if($old -ne $sha){
        if($old){$backup=Join-Path $revision "previous-source/$file";New-Item -ItemType Directory -Path (Split-Path $backup -Parent) -Force|Out-Null;Copy-Item -LiteralPath $target -Destination $backup}
        New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force|Out-Null
        Copy-Item -LiteralPath $current -Destination $target -Force
        $changes+=[pscustomobject]@{路径=$file;原SHA256=$old;最终SHA256=$sha}
    }
    $manifest+=[pscustomobject]@{路径=$file;SHA256=$sha}
}
$manifest|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $revision 'source-files.json') -Encoding utf8
$changes|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $revision 'source-changes.json') -Encoding utf8
function LinuxUpdatePath([string]$Path){$full=[IO.Path]::GetFullPath($Path);return '/mnt/'+$full.Substring(0,1).ToLowerInvariant()+$full.Substring(2).Replace('\','/')}
$original=[IO.File]::ReadAllText((Join-Path $probe 'run-build.sh'))
$bodyIndex=$original.IndexOf('cd "$probe/source"',[StringComparison]::Ordinal)
if($bodyIndex -lt 0){throw 'Prior process environment prelude is missing.'}
$prelude=$original.Substring(0,$bodyIndex)
$body=@'
revision='__REVISION__'
cd "$probe/source"
"$sdk/dotnet" --info > "$revision/sdk-info.txt"
[[ "$("$sdk/dotnet" --version)" = 10.0.401 ]] || exit 2
"$sdk/dotnet" restore NT.AvaloniaControl.slnx --locked-mode --packages "$NUGET_PACKAGES" 2>&1 | tee "$revision/restore.log"
"$sdk/dotnet" build NT.AvaloniaControl.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false 2>&1 | tee "$revision/build.log"
printf '%s\n' 'Linux SDK revision compile completed.' > "$revision/build-passed.txt"
failed=0
for name in TreeDataGrid.Core.Tests TreeDataGrid.Headless.Tests TreeDataGrid.Themes.Tests VirtualKeyboard.Core.Tests VirtualKeyboard.Headless.Tests VirtualKeyboard.Themes.Tests Markdown.Core.Tests Markdown.Streaming.Tests Markdown.Headless.Tests Markdown.Themes.Tests Markdown.TextMate.Tests; do
    cd "$probe/source/tests/$name/bin/Release/net10.0"
    if "$sdk/dotnet" "$name.dll" -noLogo 2>&1 | tee "$revision/$name.log"; then
        printf '%s\t0\n' "$name" >> "$revision/status.tsv"
    else
        printf '%s\t1\n' "$name" >> "$revision/status.tsv"
        failed=1
    fi
done
exit "$failed"
'@.Replace('__REVISION__',(LinuxUpdatePath $revision))
$scriptPath=Join-Path $revision 'run-build.sh';[IO.File]::WriteAllText($scriptPath,($prelude+$body).Replace("`r`n","`n"),[Text.UTF8Encoding]::new($false))
Write-Output "Linux revised build evidence: $revision"
wsl -d Ubuntu -- bash ((LinuxUpdatePath $revision)+'/run-build.sh')
$exitCode=$LASTEXITCODE;$compiled=Test-Path -LiteralPath (Join-Path $revision 'build-passed.txt')
$statuses=@();if(Test-Path -LiteralPath (Join-Path $revision 'status.tsv')){$statuses=@(Import-Csv -LiteralPath (Join-Path $revision 'status.tsv') -Delimiter "`t" -Header 项目,ExitCode|ForEach-Object{[pscustomobject]@{项目=$_.项目;ExitCode=[int]$_.ExitCode;是否通过=([int]$_.ExitCode -eq 0)}})}
$hashes=[ordered]@{};if($compiled){foreach($name in @('NT.AvaloniaControl.Markdown','NT.AvaloniaControl.Markdown.Themes','NT.AvaloniaControl.Markdown.TextMate','Markdown.NativeBenchmarks')){$hashes[$name]=(Get-FileHash -LiteralPath (Join-Path $source "benchmarks/Markdown.Benchmarks/NativeHost/bin/Release/net10.0/$name.dll")).Hash}}
$passed=$compiled -and $exitCode -eq 0 -and $statuses.Count -eq 11 -and @($statuses|Where-Object {-not $_.是否通过}).Count -eq 0
[pscustomobject]@{创建时间UTC=[DateTimeOffset]::UtcNow;LinuxSDK编译=$compiled;全部通过=$passed;SDK='10.0.401';ProbePath=$probe;RevisionPath=$revision;源文件清单SHA256=(Get-FileHash -LiteralPath (Join-Path $revision 'source-files.json')).Hash;程序集SHA256=[pscustomobject]$hashes;测试=$statuses;说明='复用原隔离SDK/cache/obj；修改前源码与全部完整bin/运行资源另存，历史失败与原报告不覆盖。'}|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $revision 'result.json') -Encoding utf8
if(-not $passed){throw "Linux revised build/test failed; preserve $revision"}
