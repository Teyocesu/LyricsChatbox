param(
    [string]$Configuration = 'Release',
    [string]$OutputRoot
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'PackagePolicy.ps1')
Push-Location $repo
try {
    $project = [xml](Get-Content src/LyricsChatbox/LyricsChatbox.csproj -Raw)
    $version = ($project.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
    if (-not $version) { throw 'Could not read the application version.' }
    if (-not $OutputRoot) { $OutputRoot = Join-Path $repo "artifacts/v$version" }
    $managedRoot = Reset-ManagedPackageDirectory -RepositoryRoot $repo -Path $OutputRoot
    $distribution = Join-Path $managedRoot 'win-x64'
    New-Item -ItemType Directory -Path $distribution -Force | Out-Null

    $publishArguments = @('publish', 'src/LyricsChatbox', '-c', $Configuration, '-r', 'win-x64', '--self-contained', 'true', '-p:RestoreLockedMode=true', '-o', $distribution)
    & dotnet @publishArguments
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }

    foreach ($document in Get-PublicPackageDocuments) {
        $source = Join-Path $repo ($document.Replace('/', [IO.Path]::DirectorySeparatorChar))
        $destination = Join-Path $distribution ($document.Replace('/', [IO.Path]::DirectorySeparatorChar))
        $destinationDirectory = [IO.Path]::GetDirectoryName($destination)
        New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
        Copy-Item -LiteralPath $source -Destination $destination -Force
    }

    $docsDestination = Join-Path $distribution 'docs'
    New-Item -ItemType Directory -Path $docsDestination -Force | Out-Null
    $licenseDestination = Join-Path $distribution 'licenses'
    New-Item -ItemType Directory -Path $licenseDestination -Force | Out-Null
    foreach ($license in Get-ApprovedPackageLicenses) {
        Copy-Item -LiteralPath (Join-Path $repo (Join-Path 'licenses' $license)) -Destination (Join-Path $licenseDestination $license) -Force
    }

    $stagingSummary = Assert-PackageStaging -RepositoryRoot $repo -StagingPath $distribution
    Write-Output ("STAGING files={0} licenses={1}" -f $stagingSummary.FileCount, $stagingSummary.LicenseCount)

    $archive = Join-Path $managedRoot "LyricsChatbox-$version-win-x64.zip"
    Compress-Archive -Path $distribution -DestinationPath $archive -Force
    $hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -LiteralPath "$archive.sha256" -Value ($hash + '  ' + [IO.Path]::GetFileName($archive)) -Encoding ascii -NoNewline

    & (Join-Path $PSScriptRoot 'Verify-Package.ps1') -StagingPath $distribution -ZipPath $archive -ExpectedVersion $version -RequireChecksum
    Write-Output $archive
    Write-Output "SHA256 $hash"
} finally { Pop-Location }
