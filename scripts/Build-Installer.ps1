param([string]$Compiler)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$project = [xml](Get-Content -LiteralPath (Join-Path $repo 'src/LyricsChatbox/LyricsChatbox.csproj') -Raw)
$version = $project.Project.PropertyGroup.Version
$distribution = Join-Path $repo "artifacts/v$version/win-x64"
$outputPath = Join-Path $repo "artifacts/v$version"
if (-not (Test-Path -LiteralPath (Join-Path $distribution 'LyricsChatbox.exe'))) { throw 'Run scripts/Publish.ps1 first.' }
if (-not $Compiler) {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) { $Compiler = $command.Source }
}
if (-not $Compiler -or -not (Test-Path -LiteralPath $Compiler)) { throw 'Provide -Compiler with the path to Inno Setup ISCC.exe.' }
& $Compiler "/DAppVersion=$version" "/DPublishDir=$distribution" "/DOutputPath=$outputPath" (Join-Path $PSScriptRoot 'Installer.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
$installer = Join-Path $outputPath "LyricsChatbox-Setup-$version.exe"
$hash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath "$installer.sha256" -Value ($hash + '  ' + (Split-Path $installer -Leaf)) -Encoding ascii
Write-Output $installer
Write-Output "SHA256 $hash"
