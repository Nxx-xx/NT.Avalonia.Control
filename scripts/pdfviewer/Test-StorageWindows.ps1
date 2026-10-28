param([Parameter(Mandatory)][string]$OutputDirectory, [string]$App='benchmarks/PdfViewer.P403/bin/Release/net10.0/PdfViewer.P403.dll')
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$output=[IO.Path]::GetFullPath($OutputDirectory,$repo)
if (-not $output.StartsWith($repo+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $output)) { throw 'A fresh project artifact directory is required.' }
$appPath=[IO.Path]::GetFullPath($App,$repo)
if([IO.DriveInfo]::new([IO.Path]::GetPathRoot($output)).AvailableFreeSpace-lt512MB){throw 'Reserve at least 512 MiB for isolated storage evidence.'}
$null=New-Item -ItemType Directory -Path $output
Add-Type -TypeDefinition @'
using System; using System.Runtime.InteropServices;
public static class PdfTestSpace {
 [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
 public static extern bool GetDiskFreeSpaceEx(string path, out ulong available, out ulong total, out ulong free);
}
'@
$checks=[Collections.Generic.List[object]]::new()
$processes=[Collections.Generic.List[object]]::new()
$image=Join-Path $output 'owned-test.vhdx'
$diskNumber=$null; $letter=$null; $share=$null; $attached=$false
$started=[DateTimeOffset]::UtcNow
function Save-Json($value,$path) { [IO.File]::WriteAllText($path,($value|ConvertTo-Json -Depth 18),[Text.UTF8Encoding]::new($false)) }
function Check($name,$condition,$detail=$null) {
 $checks.Add([ordered]@{Name=$name;Passed=[bool]$condition;Detail=$detail})
 if(-not $condition){throw "Failed: $name"}
}
function Hash($path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
function Start-Probe([string[]]$arguments,[string]$control){
 $null=New-Item -ItemType Directory -Force -Path $control
 $info=[Diagnostics.ProcessStartInfo]::new((Get-Command dotnet -CommandType Application).Source)
 $info.UseShellExecute=$false; $info.CreateNoWindow=$true; $info.RedirectStandardOutput=$true; $info.RedirectStandardError=$true
 $info.ArgumentList.Add($appPath); foreach($argument in $arguments){$info.ArgumentList.Add($argument)}
 $process=[Diagnostics.Process]::new(); $process.StartInfo=$info
 if(-not $process.Start()){throw 'Could not start owned probe.'}
 $record=[ordered]@{Pid=$process.Id;Arguments=$arguments;Control=$control;StartedUtc=[DateTimeOffset]::UtcNow.ToString('O')}
 $processes.Add($record)
 return @{Process=$process;Stdout=$process.StandardOutput.ReadToEndAsync();Stderr=$process.StandardError.ReadToEndAsync();Record=$record}
}
function Finish-Probe($owned,[int]$seconds=90){
 $process=$owned.Process
 try {
  if(-not $process.WaitForExit($seconds*1000)){ $process.Kill($true); $process.WaitForExit(); throw 'Owned storage probe timed out.' }
  $owned.Record.ExitCode=$process.ExitCode; $owned.Record.CompletedUtc=[DateTimeOffset]::UtcNow.ToString('O')
  [IO.File]::WriteAllText((Join-Path $owned.Record.Control 'stdout.log'),$owned.Stdout.GetAwaiter().GetResult())
  [IO.File]::WriteAllText((Join-Path $owned.Record.Control 'stderr.log'),$owned.Stderr.GetAwaiter().GetResult())
  if($process.ExitCode-ne0){throw "Owned probe failed: $($owned.Record.Control)"}
 } finally { $process.Dispose() }
}
function Wait-Json($path,[int]$seconds=50){
 $watch=[Diagnostics.Stopwatch]::StartNew()
 while($watch.Elapsed.TotalSeconds-lt$seconds){
  if(Test-Path -LiteralPath $path){try{$value=Get-Content -LiteralPath $path -Raw|ConvertFrom-Json;if($null-ne$value){return $value}}catch{}}
  Start-Sleep -Milliseconds 25
 }
 throw "Timed out waiting for $path"
}
function Fixture($directory,$control){ $owned=Start-Probe @('fixture',$directory,'storage-large') $control; Finish-Probe $owned }
function Assert-OwnedVolume {
 $current=Get-DiskImage -ImagePath $image | Get-Disk
 if($current.Number-ne$diskNumber -or $current.IsBoot -or $current.IsSystem -or $current.Size-gt140MB){throw 'Owned disk identity changed.'}
 $partition=Get-Partition -DriveLetter $letter
 if($partition.DiskNumber-ne$diskNumber){throw 'The test letter is no longer bound to the owned virtual disk.'}
}
function Fill-OwnedVolume($filler){
 Assert-OwnedVolume
 [ulong]$available=0;[ulong]$total=0;[ulong]$free=0
 if(-not[PdfTestSpace]::GetDiskFreeSpaceEx("${letter}:\",[ref]$available,[ref]$total,[ref]$free) -or $total-gt140MB){throw 'Refusing to fill an unverified volume.'}
 $file=[IO.FileStream]::new($filler,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::Read,1,[IO.FileOptions]::WriteThrough)
 $buffer=[byte[]]::new(65536); $nativeFailure=$null
 try {
  while($true){
   $null=[PdfTestSpace]::GetDiskFreeSpaceEx("${letter}:\",[ref]$available,[ref]$total,[ref]$free)
   if($available-lt4096){break}
   try{$file.Write($buffer,0,[int][Math]::Min(65536,$available))}catch [IO.IOException]{$nativeFailure=$_.Exception.HResult;break}
  }
  try{$file.Flush($true)}catch [IO.IOException]{$nativeFailure=$_.Exception.HResult}
 } finally {try{$file.Dispose()}catch{$nativeFailure=$_.Exception.GetBaseException().HResult}}
 $witness=Join-Path (Split-Path $filler -Parent) 'full-witness.bin'
 $witnessCode=$null
 try{[IO.File]::WriteAllBytes($witness,[byte[]]::new(131072))}catch [IO.IOException]{$witnessCode=$_.Exception.HResult}
 $null=[PdfTestSpace]::GetDiskFreeSpaceEx("${letter}:\",[ref]$available,[ref]$total,[ref]$free)
 Check 'real-NTFS-disk-full-witness' (($witnessCode-band0xffff)-eq112 -or ($nativeFailure-band0xffff)-eq112) @{Available=$available;Total=$total;WriteHResult=$witnessCode;FillHResult=$nativeFailure}
 return [ordered]@{Available=$available;Total=$total;WriteHResult=$witnessCode;FillHResult=$nativeFailure}
}
try {
 # Diskpart creates only a new named image. No disk is selected or formatted here.
 $script=Join-Path $output 'create-vhd.txt'
 [IO.File]::WriteAllText($script,"create vdisk file=`"$image`" maximum=128 type=expandable`r`nexit`r`n")
 & diskpart /s $script > (Join-Path $output 'diskpart.log') 2>&1
 if($LASTEXITCODE-ne0 -or -not(Test-Path -LiteralPath $image)){throw 'Owned VHD creation failed.'}
 $null=Mount-DiskImage -ImagePath $image -Access ReadWrite -NoDriveLetter; $attached=$true
 $disk=Get-DiskImage -ImagePath $image | Get-Disk; $diskNumber=$disk.Number
 if($disk.IsBoot -or $disk.IsSystem -or $disk.Size-ne128MB){throw 'Refusing to initialize an unexpected disk.'}
 Save-Json ([ordered]@{Image=$image;Disk=$disk|Select-Object Number,Size,IsBoot,IsSystem,UniqueId,Location}) (Join-Path $output 'owned-disk.json')
 $used=@(Get-Volume|Where-Object DriveLetter|ForEach-Object{[string]$_.DriveLetter})+@(Get-PSDrive -PSProvider FileSystem|ForEach-Object Name)
 $letter=@('Z','Y','X','W','V','U','T','S','R','Q','P')|Where-Object{$_-notin$used}|Select-Object -First 1
 if(-not$letter){throw 'No unused test drive letter.'}
 $null=Initialize-Disk -Number $diskNumber -PartitionStyle GPT
 $partition=New-Partition -DiskNumber $diskNumber -UseMaximumSize -DriveLetter $letter
 $null=Format-Volume -Partition $partition -FileSystem NTFS -NewFileSystemLabel 'PdfP403Test' -Confirm:$false
 Assert-OwnedVolume
 foreach($phase in @('none','JournalFlushed','CommitPrepared')){
  $name='full-'+$phase; $directory="${letter}:\$name"; $control=Join-Path $output $name
  Fixture $directory (Join-Path $control 'fixture-process')
  $original=Hash (Join-Path $directory 'original.pdf'); $filler="${letter}:\filler-$phase.bin"
  $owned=$null
  try{
   if($phase-eq'none'){$space=Fill-OwnedVolume $filler}
   $owned=Start-Probe @('storage',$directory,$control,$phase) $control
   if($phase-ne'none'){$null=Wait-Json (Join-Path $control 'stage.json');$space=Fill-OwnedVolume $filler;[IO.File]::WriteAllText((Join-Path $control 'release'),'go')}
   Finish-Probe $owned; $owned=$null
   $result=Wait-Json (Join-Path $control 'storage-result.json')
   $expected=if($result.CommitSucceeded){$result.ExpectedOutputSHA256}else{$original}
   Check "$name-commit-state" ($result.Passed -and ($result.Dirty-ne$result.CommitSucceeded))
   # A durability exception may follow publication. Its target must still be one complete version.
   Check "$name-complete-target" ($result.TargetSnapshot.Sha256-in@($original,$result.ExpectedOutputSHA256))
   Save-Json $space (Join-Path $control 'space-witness.json')
  }finally{
   if($owned){try{$owned.Process.Kill($true);$owned.Process.WaitForExit()}catch{}; $owned.Process.Dispose()}
   Assert-OwnedVolume
   if(Test-Path -LiteralPath $filler){Remove-Item -LiteralPath $filler}
   $witness="${letter}:\full-witness.bin";if(Test-Path -LiteralPath $witness){Remove-Item -LiteralPath $witness}
  }
  $null=New-Item -ItemType Directory -Force -Path (Join-Path $control 'before-recovery')
  Copy-Item -LiteralPath $directory -Destination (Join-Path $control 'before-recovery/volume') -Recurse
  $owned=Start-Probe @('recover',$directory) (Join-Path $control 'recover-process');Finish-Probe $owned
  Copy-Item -LiteralPath $directory -Destination (Join-Path $control 'after-recovery') -Recurse
 }

 # A private authenticated share. Only this share is removed to break its SMB tree;
 # the host adapter, LanmanServer service and unrelated sessions are untouched.
 $shareRoot=Join-Path $output 'share-data';$null=New-Item -ItemType Directory -Path $shareRoot
 $share='PdfP403_'+[guid]::NewGuid().ToString('N')
 $account=[Security.Principal.WindowsIdentity]::GetCurrent().Name
 $null=New-SmbShare -Name $share -Path $shareRoot -FullAccess $account -Temporary
 $unc="\\localhost\$share"
 foreach($phase in @('BeforeFlush','CommitPrepared','Replaced')){
  $name='smb-'+$phase; $directory="$unc\$name"; $physical=Join-Path $shareRoot $name; $control=Join-Path $output $name
  Fixture $directory (Join-Path $control 'fixture-process')
  $original=Hash (Join-Path $physical 'original.pdf')
  $null=New-Item -ItemType Directory -Force -Path $control;[IO.File]::WriteAllText((Join-Path $control 'wait-audit'),'yes')
  $owned=Start-Probe @('storage',$directory,$control,$phase) $control
  try{
   $null=Wait-Json (Join-Path $control 'stage.json')
   Remove-SmbShare -Name $share -Force -Confirm:$false
   Check "$name-share-offline" (-not(Get-SmbShare -Name $share -ErrorAction SilentlyContinue))
   [IO.File]::WriteAllText((Join-Path $control 'release'),'go')
   $operation=Wait-Json (Join-Path $control 'operation.json') 70
   Check "$name-reports-failure-dirty" (-not$operation.CommitSucceeded -and $operation.Dirty)
   $null=New-SmbShare -Name $share -Path $shareRoot -FullAccess $account -Temporary
   [IO.File]::WriteAllText((Join-Path $control 'audit-release'),'go')
   Finish-Probe $owned; $owned=$null
   $result=Wait-Json (Join-Path $control 'storage-result.json')
   Check "$name-complete-target" ($result.TargetSnapshot.Sha256-in@($original,$result.ExpectedOutputSHA256))
   Copy-Item -LiteralPath $physical -Destination (Join-Path $control 'before-recovery') -Recurse
   $recover=Start-Probe @('recover',$directory) (Join-Path $control 'recover-process');Finish-Probe $recover
   Copy-Item -LiteralPath $physical -Destination (Join-Path $control 'after-recovery') -Recurse
  }finally{
   if(-not(Get-SmbShare -Name $share -ErrorAction SilentlyContinue)){$null=New-SmbShare -Name $share -Path $shareRoot -FullAccess $account -Temporary}
   if($owned){try{$owned.Process.Kill($true);$owned.Process.WaitForExit()}catch{}; $owned.Process.Dispose()}
  }
 }
 $passed=$true
}catch{$passed=$false;$failure=$_.Exception.ToString()}
finally{
 if($share){Get-SmbShare -Name $share -ErrorAction SilentlyContinue|Remove-SmbShare -Force -Confirm:$false}
 if($attached){$null=Dismount-DiskImage -ImagePath $image;$attached=(Get-DiskImage -ImagePath $image).Attached}
 if(-not $attached -and (Test-Path -LiteralPath $image)){
  $rawHash=Hash $image;$rawBytes=(Get-Item -LiteralPath $image).Length
  $inputFile=[IO.File]::OpenRead($image);$archiveFile=[IO.File]::Create($image+'.gz');$gzip=[IO.Compression.GZipStream]::new($archiveFile,[IO.Compression.CompressionLevel]::Fastest)
  try{$inputFile.CopyTo($gzip)}finally{$gzip.Dispose();$archiveFile.Dispose();$inputFile.Dispose()}
  $archiveFile=[IO.File]::OpenRead($image+'.gz');$gzip=[IO.Compression.GZipStream]::new($archiveFile,[IO.Compression.CompressionMode]::Decompress)
  try{$verified=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($gzip));if($verified-ne$rawHash){throw 'Compressed image verification failed'}}finally{$gzip.Dispose();$archiveFile.Dispose()}
  Remove-Item -LiteralPath $image
  Save-Json @{RawSHA256=$rawHash;RawBytes=$rawBytes;Archive=$image+'.gz';ArchiveBytes=(Get-Item -LiteralPath ($image+'.gz')).Length} (Join-Path $output 'images.json')
 }
 Save-Json ([ordered]@{Passed=[bool]$passed;Failure=$failure;StartedUtc=$started.ToString('O');CompletedUtc=[DateTimeOffset]::UtcNow.ToString('O');Checks=$checks;Processes=$processes;WindowsPowerCut='User explicitly deferred: no disposable Windows VM image.';Image=$image;DiskNumber=$diskNumber;DriveLetter=$letter;Detached=(-not $attached)}) (Join-Path $output 'result.json')
}
if(-not$passed){throw "Storage validation failed: $failure"}
