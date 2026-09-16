param(
    [string]$Compiler,
    [string]$OutputRoot
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'PackagePolicy.ps1')
$project = [xml](Get-Content -LiteralPath (Join-Path $repo 'src/LyricsChatbox/LyricsChatbox.csproj') -Raw)
$version = ($project.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
if (-not $version) { throw 'Could not read the application version.' }
if (-not $OutputRoot) { $OutputRoot = Join-Path $repo "artifacts/v$version" }
$outputPath = Resolve-ManagedArtifactsPath -RepositoryRoot $repo -Path $OutputRoot
$distribution = Join-Path $outputPath 'win-x64'
if (-not (Test-Path -LiteralPath (Join-Path $distribution 'LyricsChatbox.exe'))) { throw 'Run scripts/Publish.ps1 first.' }

$verifyArguments = @{
    StagingPath = $distribution
    ExpectedVersion = $version
    InstallerScriptPath = (Join-Path $PSScriptRoot 'Installer.iss')
}
$zip = Join-Path $outputPath "LyricsChatbox-$version-win-x64.zip"
if (Test-Path -LiteralPath $zip -PathType Leaf) { $verifyArguments.ZipPath = $zip }
& (Join-Path $PSScriptRoot 'Verify-Package.ps1') @verifyArguments

if (-not $Compiler) {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) { $Compiler = $command.Source }
}
if (-not $Compiler -or -not (Test-Path -LiteralPath $Compiler)) { throw 'Provide -Compiler with the path to Inno Setup ISCC.exe.' }
& $Compiler "/DAppVersion=$version" "/DPublishDir=$distribution" "/DOutputPath=$outputPath" (Join-Path $PSScriptRoot 'Installer.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
$installer = Join-Path $outputPath "LyricsChatbox-Setup-$version.exe"
if (-not (Test-Path -LiteralPath $installer -PathType Leaf)) { throw 'Installer compilation did not produce the expected output.' }
$hash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath "$installer.sha256" -Value ($hash + '  ' + [IO.Path]::GetFileName($installer)) -Encoding ascii -NoNewline

& (Join-Path $PSScriptRoot 'Verify-Package.ps1') -StagingPath $distribution -InstallerPath $installer -ExpectedVersion $version -InstallerScriptPath (Join-Path $PSScriptRoot 'Installer.iss')
Write-Output $installer
Write-Output "SHA256 $hash"
