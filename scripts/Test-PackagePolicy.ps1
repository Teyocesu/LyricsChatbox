$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'PackagePolicy.ps1')

$testRoot = Join-Path (Join-Path $repositoryRoot 'artifacts') ("phase4-package-policy-test-" + [Guid]::NewGuid().ToString('N'))
$managedTestRoot = Resolve-ManagedArtifactsPath -RepositoryRoot $repositoryRoot -Path $testRoot

function Assert-ExpectedFailure {
    param([Parameter(Mandatory = $true)][scriptblock]$Action,[Parameter(Mandatory = $true)][string]$Name)
    $failed = $false
    try { & $Action } catch { $failed = $true }
    if (-not $failed) { throw "Expected failure did not occur: $Name" }
}

function New-TestZip {
    param([Parameter(Mandatory = $true)][string]$Path,[Parameter(Mandatory = $true)][string[]]$EntryNames)
    Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction SilentlyContinue
    $archive = [IO.Compression.ZipFile]::Open($Path, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($name in $EntryNames) {
            $entry = $archive.CreateEntry($name)
            $stream = $entry.Open()
            try { $stream.WriteByte(0x78) } finally { $stream.Dispose() }
        }
    } finally { $archive.Dispose() }
}

try {
    $null = Reset-ManagedPackageDirectory -RepositoryRoot $repositoryRoot -Path $testRoot
    $staging = Join-Path $managedTestRoot 'win-x64'
    New-Item -ItemType Directory -Path $staging -Force | Out-Null

    # A stale internal document must be removed by the managed clean before a new publish.
    Set-Content -LiteralPath (Join-Path $managedTestRoot 'PLAN.md') -Value 'stale internal evidence' -Encoding utf8
    New-Item -ItemType Directory -Path (Join-Path $managedTestRoot 'tests') -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $managedTestRoot 'tests/fixture.txt') -Value 'stale test' -Encoding utf8
    $null = Reset-ManagedPackageDirectory -RepositoryRoot $repositoryRoot -Path $testRoot
    if ((Test-Path -LiteralPath (Join-Path $managedTestRoot 'PLAN.md')) -or (Test-Path -LiteralPath (Join-Path $managedTestRoot 'tests'))) {
        throw 'Managed staging cleanup did not remove stale files.'
    }

    New-Item -ItemType Directory -Path (Join-Path $staging 'docs') -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $staging 'licenses') -Force | Out-Null
    [IO.File]::WriteAllBytes((Join-Path $staging 'LyricsChatbox.exe'), [byte[]](0x4d, 0x5a))
    Set-Content -LiteralPath (Join-Path $staging 'LyricsChatbox.deps.json') -Value '{}' -Encoding utf8
    Set-Content -LiteralPath (Join-Path $staging 'LyricsChatbox.runtimeconfig.json') -Value '{}' -Encoding utf8
    Set-Content -LiteralPath (Join-Path $staging 'README.md') -Value '# LyricsChatbox' -Encoding utf8
    Set-Content -LiteralPath (Join-Path $staging 'THIRD_PARTY_NOTICES.md') -Value '# Third-party components' -Encoding utf8
    Set-Content -LiteralPath (Join-Path $staging 'docs/WINDOWS-SIGNING.md') -Value '# Windows signing' -Encoding utf8
    foreach ($license in Get-ApprovedPackageLicenses) {
        Copy-Item -LiteralPath (Join-Path $repositoryRoot (Join-Path 'licenses' $license)) -Destination (Join-Path $staging (Join-Path 'licenses' $license))
    }

    $summary = Assert-PackageStaging -RepositoryRoot $repositoryRoot -StagingPath $staging
    if ($summary.FileCount -lt 10 -or $summary.LicenseCount -ne (Get-ApprovedPackageLicenses).Count) {
        throw 'Valid package manifest did not report the required files.'
    }

    $forbiddenCases = @(
        'PLAN.md'
        'HANDOFF.md'
        'AGENTS.md'
        'SPEC.md'
        'spikes/SpotifyPlaybackSpike.exe'
        'tests/LyricsChatbox.Tests.dll'
        'runtime-state.json'
        'settings.json'
        'profiles/selected.json'
        'quick messages/list.json'
        'cache/record.json'
        'lyrics/record.lrc'
        'corrections/record.json'
        'matches/record.json'
        'ignore/record.json'
        'physical-report.json'
        'diagnostic-export.json'
        'secret-token.txt'
    )
    foreach ($relative in $forbiddenCases) {
        if (-not (Get-PackagePathViolation -RelativePath $relative)) {
            throw "Package policy did not reject the forbidden path case: $relative"
        }
    }

    Set-Content -LiteralPath (Join-Path $staging 'PLAN.md') -Value 'forbidden' -Encoding utf8
    Assert-ExpectedFailure -Name 'PLAN.md staging exclusion' -Action { Assert-PackageStaging -RepositoryRoot $repositoryRoot -StagingPath $staging }
    Remove-Item -LiteralPath (Join-Path $staging 'PLAN.md') -Force

    $zip = Join-Path $managedTestRoot 'LyricsChatbox-test-win-x64.zip'
    Compress-Archive -Path $staging -DestinationPath $zip -Force
    $zipSummary = Assert-ZipMatchesStaging -StagingPath $staging -ZipPath $zip
    if ($zipSummary.FileCount -ne $summary.FileCount) { throw 'ZIP/staging test manifest counts differ.' }

    $installerInput = Assert-InstallerInputTree -RepositoryRoot $repositoryRoot -StagingPath $staging -InstallerScriptPath (Join-Path $PSScriptRoot 'Installer.iss')
    if ($installerInput.SourceDirective -ne '{#PublishDir}\*') { throw 'Installer source regression was not detected.' }

    $localDataRoot = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)) 'LyricsChatbox'
    if ($testRoot.StartsWith($localDataRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Package-policy validation path unexpectedly overlaps real LocalData.'
    }

    $traversalZip = Join-Path $managedTestRoot 'traversal.zip'
    New-TestZip -Path $traversalZip -EntryNames @('win-x64/../PLAN.md')
    Assert-ExpectedFailure -Name 'ZIP traversal rejection' -Action { Assert-ZipMatchesStaging -StagingPath $staging -ZipPath $traversalZip }

    $absoluteZip = Join-Path $managedTestRoot 'absolute.zip'
    New-TestZip -Path $absoluteZip -EntryNames @('C:/Users/example/secret.txt')
    Assert-ExpectedFailure -Name 'ZIP absolute-path rejection' -Action { Assert-ZipMatchesStaging -StagingPath $staging -ZipPath $absoluteZip }

    Assert-ExpectedFailure -Name 'managed cleanup path guard' -Action { Resolve-ManagedArtifactsPath -RepositoryRoot $repositoryRoot -Path (Join-Path $repositoryRoot 'outside-package-root') }
    Write-Output 'PACKAGE-POLICY TEST PASS: allowlist, stale cleanup, manifest, ZIP parity, traversal and path-safety checks'
} finally {
    if (Test-Path -LiteralPath $managedTestRoot) {
        Remove-Item -LiteralPath $managedTestRoot -Recurse -Force -ErrorAction Stop
    }
}
