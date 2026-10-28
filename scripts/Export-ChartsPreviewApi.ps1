[CmdletBinding()]
param([Parameter(Mandatory)][string]$AssemblyDirectory,[Parameter(Mandatory)][string]$OutputPath)
$ErrorActionPreference='Stop'
Set-StrictMode -Version 3
$bin=[IO.Path]::GetFullPath($AssemblyDirectory);$output=[IO.Path]::GetFullPath($OutputPath)
if(Test-Path -LiteralPath $output){throw 'Fresh API snapshot required'}
$sdk=Join-Path (Split-Path (Get-Command dotnet).Source -Parent) 'sdk/10.0.402/System.Reflection.MetadataLoadContext.dll'
if(!(Test-Path -LiteralPath $sdk)){throw 'Pinned SDK metadata loader missing'}
[void][Reflection.Assembly]::LoadFrom($sdk)
$paths=@(([string][AppContext]::GetData('TRUSTED_PLATFORM_ASSEMBLIES')).Split([IO.Path]::PathSeparator))+@(Get-ChildItem -LiteralPath $bin -File -Filter '*.dll'|ForEach-Object FullName)
$resolver=[System.Reflection.PathAssemblyResolver]::new([string[]]$paths)
$context=[System.Reflection.MetadataLoadContext]::new($resolver,'System.Private.CoreLib')
$text=[Text.StringBuilder]::new();[void]$text.AppendLine('Charts preview public/protected API metadata snapshot. No constructors, property getters or static initializers executed.')
$flags=[Reflection.BindingFlags]'Public,NonPublic,Static,Instance,DeclaredOnly'
function Accessible($m){$m.IsPublic -or $m.IsFamily -or $m.IsFamilyOrAssembly}
try{
 foreach($name in @('NT.AvaloniaControl.Charts','NT.AvaloniaControl.Charts.Themes','NT.AvaloniaControl.Charts.Skia')){
  $path=Join-Path $bin "$name.dll";$a=$context.LoadFromAssemblyPath($path)
  [void]$text.AppendLine("`nAssembly: $($a.FullName)`nSHA256: $((Get-FileHash -LiteralPath $path).Hash)")
  foreach($type in $a.GetExportedTypes()|Sort-Object FullName){
   $baseName=if($null -ne $type.BaseType){$type.BaseType.FullName}else{''}
   [void]$text.AppendLine("Type: $($type.FullName) : $baseName")
   foreach($m in $type.GetConstructors($flags)|Where-Object {Accessible $_}|Sort-Object {$_.ToString()}){[void]$text.AppendLine('  Constructor: '+$m.ToString())}
   foreach($m in $type.GetMethods($flags)|Where-Object {!$_.IsSpecialName -and (Accessible $_)}|Sort-Object {$_.ToString()}){[void]$text.AppendLine('  Method: '+$m.ToString())}
   foreach($p in $type.GetProperties($flags)|Where-Object {($_.GetMethod -and (Accessible $_.GetMethod)) -or ($_.SetMethod -and (Accessible $_.SetMethod))}|Sort-Object Name){[void]$text.AppendLine('  Property: '+$p.ToString()+" [get=$($null -ne $p.GetMethod), set=$($null -ne $p.SetMethod)]")}
   foreach($f in $type.GetFields($flags)|Where-Object {Accessible $_}|Sort-Object Name){[void]$text.AppendLine('  Field: '+$f.ToString()+" [readonly=$($f.IsInitOnly)]")}
   foreach($e in $type.GetEvents($flags)|Where-Object {$_.AddMethod -and (Accessible $_.AddMethod)}|Sort-Object Name){[void]$text.AppendLine('  Event: '+$e.ToString())}
  }
 }
 [IO.Directory]::CreateDirectory((Split-Path $output -Parent))|Out-Null
 [IO.File]::WriteAllText($output,$text.ToString())
}finally{$context.Dispose()}
[pscustomobject]@{path=$output;bytes=(Get-Item -LiteralPath $output).Length;sha256=(Get-FileHash -LiteralPath $output).Hash}
