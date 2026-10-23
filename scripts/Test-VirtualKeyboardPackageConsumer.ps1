param()
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$feed = Join-Path $repoRoot 'artifacts/packages'
$fixtureRoot = Join-Path $PSScriptRoot 'Consumers/VirtualKeyboard'
$buildProperties = [xml](Get-Content -LiteralPath (Join-Path $repoRoot 'Directory.Build.props') -Raw)
$packageVersion = [string]$buildProperties.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($packageVersion)) { throw 'VirtualKeyboard package version is missing.' }

foreach ($mode in 'FunctionOnly', 'DefaultTheme') {
    $consumer = Join-Path $repoRoot ('artifacts/consumer/' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $consumer -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $consumer 'Directory.Build.props') -Value '<Project />' -Encoding utf8
    Set-Content -LiteralPath (Join-Path $consumer 'Directory.Packages.props') -Value '<Project />' -Encoding utf8
    $packageId = if ($mode -eq 'FunctionOnly') { 'NT.AvaloniaControl.VirtualKeyboard' } else { 'NT.AvaloniaControl.VirtualKeyboard.Themes' }
    $project = Get-Content -LiteralPath (Join-Path $fixtureRoot 'Consumer.csproj.template') -Raw
    $project.Replace('__PACKAGE_ID__', $packageId).Replace('__PRODUCT_VERSION__', $packageVersion) |
        Set-Content -LiteralPath (Join-Path $consumer 'Consumer.csproj') -Encoding utf8
    Copy-Item -LiteralPath (Join-Path $fixtureRoot 'Program.cs.template') -Destination (Join-Path $consumer 'Program.cs')
    Copy-Item -LiteralPath (Join-Path $fixtureRoot 'Window.axaml.template') -Destination (Join-Path $consumer 'Window.axaml')
    $styles = if ($mode -eq 'FunctionOnly') {
        Get-Content -LiteralPath (Join-Path $fixtureRoot 'CustomStyles.axaml.template') -Raw
    } else {
        '<StyleInclude Source="avares://NT.AvaloniaControl.VirtualKeyboard.Themes/Themes/Generic.axaml" />'
    }
    $app = Get-Content -LiteralPath (Join-Path $fixtureRoot 'App.axaml.template') -Raw
    $app.Replace('__KEYBOARD_STYLES__', $styles) | Set-Content -LiteralPath (Join-Path $consumer 'App.axaml') -Encoding utf8
    $escapedFeed = [System.Security.SecurityElement]::Escape($feed)
    $config = Join-Path $consumer 'NuGet.Config'
    "<configuration><packageSources><clear /><add key=`"local-preview`" value=`"$escapedFeed`" /><add key=`"nuget.org`" value=`"https://api.nuget.org/v3/index.json`" /></packageSources></configuration>" |
        Set-Content -LiteralPath $config -Encoding utf8
    dotnet restore (Join-Path $consumer 'Consumer.csproj') --packages (Join-Path $consumer '.packages') --configfile $config --disable-parallel
    if ($LASTEXITCODE -ne 0) { throw "VirtualKeyboard $mode independent package restore failed." }
    dotnet run --project (Join-Path $consumer 'Consumer.csproj') -c Release --no-restore -- $mode
    if ($LASTEXITCODE -ne 0) { throw "VirtualKeyboard $mode independent package consumer failed." }
}
