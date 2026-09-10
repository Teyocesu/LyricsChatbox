param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
Push-Location $repo
try {
    $project = [xml](Get-Content src/LyricsChatbox/LyricsChatbox.csproj -Raw)
    $version = $project.Project.PropertyGroup.Version
    $distribution = Join-Path $repo "artifacts/v$version/win-x64"
    dotnet publish src/LyricsChatbox -c $Configuration -r win-x64 --self-contained true -o $distribution
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    Copy-Item -LiteralPath README.md,THIRD_PARTY_NOTICES.md,PLAN.md -Destination $distribution
    $docsDestination = Join-Path $distribution 'docs'
    New-Item -ItemType Directory -Path $docsDestination -Force | Out-Null
    Copy-Item -LiteralPath docs/WINDOWS-SIGNING.md -Destination $docsDestination
    $licenseDestination = Join-Path $distribution 'licenses'
    New-Item -ItemType Directory -Path $licenseDestination -Force | Out-Null
    Copy-Item -Path licenses/* -Destination $licenseDestination
    $archive = Join-Path $repo "artifacts/v$version/LyricsChatbox-$version-win-x64.zip"
    Compress-Archive -Path $distribution -DestinationPath $archive -Force
    $hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -LiteralPath "$archive.sha256" -Value ($hash + '  ' + (Split-Path $archive -Leaf)) -Encoding ascii
    Write-Output $archive
    Write-Output "SHA256 $hash"
} finally { Pop-Location }
