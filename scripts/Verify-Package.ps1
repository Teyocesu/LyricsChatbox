param(
    [Parameter(Mandatory = $true)][string]$StagingPath,
    [string]$ZipPath,
    [string]$InstallerScriptPath,
    [string]$InstallerPath,
    [string]$ExpectedVersion,
    [switch]$RequireChecksum
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'PackagePolicy.ps1')

$stagingSummary = Assert-PackageStaging -RepositoryRoot $repositoryRoot -StagingPath $StagingPath
Write-Output ("STAGING files={0} licenses={1} required-docs={2}" -f $stagingSummary.FileCount, $stagingSummary.LicenseCount, $stagingSummary.RequiredDocuments)

if ($ZipPath) {
    if ($ExpectedVersion) {
        $expectedZipName = "LyricsChatbox-$ExpectedVersion-win-x64.zip"
        if ([IO.Path]::GetFileName($ZipPath) -ne $expectedZipName) {
            throw "Unexpected ZIP name. Expected $expectedZipName"
        }
    }
    $zipSummary = Assert-ZipMatchesStaging -StagingPath $StagingPath -ZipPath $ZipPath -RequireChecksum:$RequireChecksum
    Write-Output ("ZIP entries={0} files={1} sha256={2}" -f $zipSummary.EntryCount, $zipSummary.FileCount, $zipSummary.Sha256)
}

if ($InstallerScriptPath) {
    $installerInputSummary = Assert-InstallerInputTree -RepositoryRoot $repositoryRoot -StagingPath $StagingPath -InstallerScriptPath $InstallerScriptPath
    Write-Output ("INSTALLER-SOURCE files={0} source={1} inspection={2}" -f $installerInputSummary.FileCount, $installerInputSummary.SourceDirective, $installerInputSummary.InstallerPayloadInspection)
}

if ($InstallerPath) {
    if (-not $ExpectedVersion) { throw 'ExpectedVersion is required when InstallerPath is supplied.' }
    $installerSummary = Assert-InstallerArtifact -InstallerPath $InstallerPath -ExpectedVersion $ExpectedVersion
    Write-Output ("INSTALLER files-by-source={0} bytes={1} sha256={2}" -f $stagingSummary.FileCount, $installerSummary.Length, $installerSummary.Sha256)
}
