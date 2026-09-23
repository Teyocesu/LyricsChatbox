function Get-PublicPackageDocuments {
    @(
        'README.md'
        'THIRD_PARTY_NOTICES.md'
        'docs/WINDOWS-SIGNING.md'
    )
}

function Get-ApprovedPackageLicenses {
    @(
        'CSWINRT-LICENSE.txt'
        'DOTNET-LICENSE.txt'
        'DOTNET-THIRD-PARTY-NOTICES.txt'
        'SYSTEM-REACTIVE-LICENSE.txt'
        'WINDOWS-SDK-LICENSE.rtf'
        'WPF-LICENSE.txt'
        'ZEROCONF-LICENSE.txt'
    )
}

function Resolve-RepositoryPath {
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)][string]$Path
    )

    if ([IO.Path]::IsPathRooted($Path)) {
        return [IO.Path]::GetFullPath($Path)
    }

    return [IO.Path]::GetFullPath((Join-Path $RepositoryRoot $Path))
}

function Resolve-ManagedArtifactsPath {
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $repo = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $artifacts = [IO.Path]::GetFullPath((Join-Path $repo 'artifacts')).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $candidate = Resolve-RepositoryPath -RepositoryRoot $repo -Path $Path
    $parent = [IO.Path]::GetFullPath([IO.Path]::GetDirectoryName($candidate)).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)

    if ($parent -ne $artifacts -or $candidate -eq $artifacts) {
        throw "Package output must be one direct child of the repository artifacts directory: $candidate"
    }

    return $candidate
}

function Reset-ManagedPackageDirectory {
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $managed = Resolve-ManagedArtifactsPath -RepositoryRoot $RepositoryRoot -Path $Path
    if (Test-Path -LiteralPath $managed) {
        $existing = Get-Item -LiteralPath $managed -Force -ErrorAction Stop
        if (-not $existing.PSIsContainer) {
            Remove-Item -LiteralPath $managed -Force -ErrorAction Stop
        } else {
            Remove-Item -LiteralPath $managed -Recurse -Force -ErrorAction Stop
        }
    }

    New-Item -ItemType Directory -Path $managed -Force -ErrorAction Stop | Out-Null
    return $managed
}

function Get-PackageRelativePath {
    param(
        [Parameter(Mandatory = $true)][string]$BasePath,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $trim = [char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $base = [IO.Path]::GetFullPath($BasePath)
    $baseRoot = [IO.Path]::GetPathRoot($base)
    if ($base.Length -gt $baseRoot.Length) { $base = $base.TrimEnd($trim) }
    $candidate = [IO.Path]::GetFullPath($Path)
    if ($candidate.Equals($base, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Package path must be a child of the staging root: $candidate"
    }
    $basePrefix = if ($base.EndsWith([IO.Path]::DirectorySeparatorChar.ToString()) -or
        $base.EndsWith([IO.Path]::AltDirectorySeparatorChar.ToString())) {
        $base
    } else {
        $base + [IO.Path]::DirectorySeparatorChar
    }
    if (-not $candidate.StartsWith($basePrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Package path is outside the staging root: $candidate"
    }
    $relative = $candidate.Substring($basePrefix.Length)
    if ([string]::IsNullOrWhiteSpace($relative)) {
        throw "Package path must identify a child file: $candidate"
    }
    return $relative.Replace('\', '/')
}

function Get-PackagePathViolation {
    param([Parameter(Mandatory = $true)][string]$RelativePath)

    $normalized = $RelativePath.Replace('\', '/').TrimStart('/')
    $parts = @($normalized.Split('/', [StringSplitOptions]::RemoveEmptyEntries))
    if ($parts.Count -eq 0) { return 'empty path' }

    $lower = $normalized.ToLowerInvariant()
    $leaf = $parts[-1].ToLowerInvariant()
    $forbiddenDirectories = @(
        '.git', '.github', 'tests', 'test', 'spikes', 'artifacts', 'bin', 'obj',
        'localdata', 'profiles', 'profile', 'cache', 'lyrics', 'corrections',
        'matches', 'ignore', 'downloads', 'quick messages', 'quick-messages',
        'quick_messages'
    )

    if (@($parts | Where-Object { $forbiddenDirectories -contains $_.ToLowerInvariant() }).Count -gt 0) {
        return 'internal, test, artifact, or user-data directory'
    }

    if ($leaf -in @('plan.md', 'handoff.md', 'agents.md', 'spec.md')) {
        return 'internal execution/spec document'
    }

    if ($leaf -in @('.gitignore', '.gitattributes', 'app.manifest', 'packages.lock.json') -or
        $leaf -match '\.pdb$' -or
        $leaf -match '\.(cs|csproj|slnx|xaml|props|targets)$') {
        return 'source or project metadata'
    }

    if ($leaf -match '(^|[-_.])(tests?|testhost|xunit|testplatform)([-_.]|$)' -or $leaf -match 'spike') {
        return 'test or spike artifact'
    }

    if ($leaf -in @('settings.json', 'runtime-state.json') -or
        $leaf -match '^(profiles?|quick[- _]?messages?|cache|lyrics|corrections?|matches?|ignore)(\.[^.]+)?$') {
        return 'runtime state or user data'
    }

    if ($leaf -notmatch '^system\.diagnostics\.' -and
        ($leaf -match '(^|[-_.])(diagnostics?|report|validation|receipt|physical|listening|history|payload-comparison)([-_.]|$)' -or
        $leaf -match '\.(trx|jsonl|local\.json)$')) {
        return 'diagnostic or validation artifact'
    }

    if ($leaf -match '(secret|credential|password|token)' -or $leaf -match '\.(sha256|part)$') {
        return 'secret or temporary download artifact'
    }

    if ($leaf -match '\.(cs|xaml|csproj|slnx|props|targets)$') {
        return 'source file'
    }

    if ($leaf -match '\.json$' -and $leaf -notmatch '(\.deps|\.runtimeconfig)(\.dev)?\.json$') {
        return 'non-runtime JSON data'
    }

    if (($leaf -match '\.(md|txt|rtf)$') -and
        $lower -notmatch '^(readme\.md|third_party_notices\.md|docs/windows-signing\.md|licenses/[^/]+)$') {
        return 'unapproved documentation or text file'
    }

    return $null
}

function Assert-PublicText {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$RelativePath
    )

    $info = Get-Item -LiteralPath $Path -Force -ErrorAction Stop
    if ($info.Length -gt 2MB) {
        throw "Public text file is unexpectedly large: $RelativePath"
    }

    $text = [IO.File]::ReadAllText($Path)
    $internalTerms = @(
        '(?i)\bCodex\b'
        '(?i)\b(Luna|Sol|Astra)\b'
        '(?i)model routing'
        '(?i)\bprompt\b'
        '(?i)\bPhase\s+[0-9]+\b'
        '(?i)acceptance gate'
        '(?i)execution ledger'
        '(?i)\bAUD-[0-9]+\b'
        '(?i)\bcodex/'
        '(?i)\b(PLAN|HANDOFF|AGENTS|SPEC)\.md\b'
    )
    foreach ($pattern in $internalTerms) {
        if ($text -match $pattern) {
            throw "Internal workflow wording '$pattern' appears in public package text: $RelativePath"
        }
    }

    if ($text -match '(?i)([A-Z]:[\\/]+Users[\\/]+[^\\/\r\n]+|/Users/[^/\r\n]+)') {
        throw "User-specific filesystem path appears in public package text: $RelativePath"
    }
}

function Assert-PackageStaging {
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)][string]$StagingPath
    )

    $staging = [IO.Path]::GetFullPath($StagingPath)
    if (-not (Test-Path -LiteralPath $staging -PathType Container)) {
        throw "Package staging directory does not exist: $staging"
    }

    $files = @(Get-ChildItem -LiteralPath $staging -Recurse -File -Force -ErrorAction Stop)
    if ($files.Count -eq 0) {
        throw "Package staging directory is empty: $staging"
    }

    $violations = [Collections.Generic.List[string]]::new()
    foreach ($file in $files) {
        $relative = Get-PackageRelativePath -BasePath $staging -Path $file.FullName
        $reason = Get-PackagePathViolation -RelativePath $relative
        if ($null -ne $reason) {
            $violations.Add("$relative ($reason)")
        }
    }
    if ($violations.Count -gt 0) {
        throw "Forbidden package paths:`n - $($violations -join "`n - ")"
    }

    $required = @('LyricsChatbox.exe') + (Get-PublicPackageDocuments) + (Get-ApprovedPackageLicenses | ForEach-Object { "licenses/$_" })
    foreach ($relative in $required) {
        $path = Join-Path $staging ($relative.Replace('/', [IO.Path]::DirectorySeparatorChar))
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Required package file is missing: $relative"
        }
    }

    $exe = Get-Item -LiteralPath (Join-Path $staging 'LyricsChatbox.exe') -Force
    if ($exe.Length -le 0) {
        throw 'Application executable is empty.'
    }

    $sourceLicenses = Join-Path $RepositoryRoot 'licenses'
    $sourceNames = @(Get-ChildItem -LiteralPath $sourceLicenses -File -Force -ErrorAction Stop | Select-Object -ExpandProperty Name | Sort-Object)
    $approvedNames = @(Get-ApprovedPackageLicenses | Sort-Object)
    if (($sourceNames -join "`n") -ne ($approvedNames -join "`n")) {
        throw "The licenses directory differs from the explicit package license allowlist. Update PackagePolicy.ps1 deliberately."
    }

    foreach ($license in Get-ApprovedPackageLicenses) {
        $source = Join-Path $sourceLicenses $license
        $packaged = Join-Path $staging (Join-Path 'licenses' $license)
        $sourceHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
        $packagedHash = (Get-FileHash -LiteralPath $packaged -Algorithm SHA256).Hash
        if ($sourceHash -ne $packagedHash) {
            throw "Packaged license differs from source: $license"
        }
    }

    $publicText = @(Get-PublicPackageDocuments)
    foreach ($relative in $publicText) {
        $path = Join-Path $staging ($relative.Replace('/', [IO.Path]::DirectorySeparatorChar))
        Assert-PublicText -Path $path -RelativePath $relative
    }

    return [pscustomobject]@{
        StagingPath = $staging
        FileCount = $files.Count
        RequiredDocuments = (Get-PublicPackageDocuments) -join ', '
        LicenseCount = (Get-ApprovedPackageLicenses).Count
        ForbiddenPaths = 0
    }
}

function Get-ZipFileEntries {
    param([Parameter(Mandatory = $true)][string]$ZipPath)

    Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction SilentlyContinue
    $archive = [IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        $entries = @($archive.Entries)
        $result = [Collections.Generic.List[object]]::new()
        foreach ($entry in $entries) {
            $normalized = $entry.FullName.Replace('\', '/')
            $isDirectory = $normalized.EndsWith('/')
            $result.Add([pscustomobject]@{
                Entry = $entry
                Name = $normalized
                IsDirectory = $isDirectory
            })
        }
        return $result.ToArray()
    } finally {
        $archive.Dispose()
    }
}

function Get-ZipEntryHash {
    param([Parameter(Mandatory = $true)][string]$ZipPath,[Parameter(Mandatory = $true)][string]$EntryName)

    Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction SilentlyContinue
    $archive = [IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        $entry = @($archive.Entries | Where-Object { $_.FullName.Replace('\', '/') -eq $EntryName })
        if ($entry.Count -ne 1) { throw "ZIP entry is missing or duplicated: $EntryName" }
        $stream = $entry[0].Open()
        $sha = [Security.Cryptography.SHA256]::Create()
        try {
            return (($sha.ComputeHash($stream) | ForEach-Object { $_.ToString('x2') }) -join '')
        } finally {
            $sha.Dispose()
            $stream.Dispose()
        }
    } finally {
        $archive.Dispose()
    }
}

function Assert-ZipMatchesStaging {
    param(
        [Parameter(Mandatory = $true)][string]$StagingPath,
        [Parameter(Mandatory = $true)][string]$ZipPath,
        [switch]$RequireChecksum
    )

    if (-not (Test-Path -LiteralPath $ZipPath -PathType Leaf)) {
        throw "ZIP archive does not exist: $ZipPath"
    }

    $staging = [IO.Path]::GetFullPath($StagingPath)
    $zipEntries = @(Get-ZipFileEntries -ZipPath $ZipPath)
    $stagingName = [IO.Path]::GetFileName($staging)
    $prefix = ($stagingName + '/').ToLowerInvariant()
    $actual = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)

    foreach ($item in $zipEntries) {
        $name = $item.Name
        if ($name.StartsWith('/') -or $name -match '^[A-Za-z]:/' -or
            @($name.Split('/', [StringSplitOptions]::RemoveEmptyEntries) | Where-Object { $_ -eq '..' }).Count -gt 0) {
            throw "ZIP contains an absolute or traversal path: $name"
        }
        if (-not $name.ToLowerInvariant().StartsWith($prefix)) {
            throw "ZIP contains unexpected root nesting: $name"
        }

        if (-not $item.IsDirectory) {
            $relative = $name.Substring($prefix.Length)
            $reason = Get-PackagePathViolation -RelativePath $relative
            if ($null -ne $reason) {
                throw "ZIP contains forbidden path '$relative' ($reason)"
            }
            if (-not $actual.Add($name)) {
                throw "ZIP contains a duplicate file entry: $name"
            }
        }
    }

    $expected = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $stagingFiles = @(Get-ChildItem -LiteralPath $staging -Recurse -File -Force -ErrorAction Stop)
    foreach ($file in $stagingFiles) {
        $relative = Get-PackageRelativePath -BasePath $staging -Path $file.FullName
        $null = $expected.Add($stagingName + '/' + $relative)
    }

    if (-not $expected.SetEquals($actual)) {
        $missing = @($expected | Where-Object { -not $actual.Contains($_) })
        $unexpected = @($actual | Where-Object { -not $expected.Contains($_) })
        $details = @()
        if ($missing.Count -gt 0) { $details += "missing: $($missing -join ', ')" }
        if ($unexpected.Count -gt 0) { $details += "unexpected: $($unexpected -join ', ')" }
        throw "ZIP file entries do not match staging: $($details -join '; ')"
    }

    foreach ($file in $stagingFiles) {
        $relative = Get-PackageRelativePath -BasePath $staging -Path $file.FullName
        $entryName = $stagingName + '/' + $relative
        $fileHash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        $zipHash = Get-ZipEntryHash -ZipPath $ZipPath -EntryName $entryName
        if ($fileHash -ne $zipHash) {
            throw "ZIP content differs from staging: $relative"
        }
    }

    $checksumPath = "$ZipPath.sha256"
    if ($RequireChecksum -and -not (Test-Path -LiteralPath $checksumPath -PathType Leaf)) {
        throw "ZIP checksum sidecar is missing: $checksumPath"
    }
    if (Test-Path -LiteralPath $checksumPath -PathType Leaf) {
        $archiveHash = (Get-FileHash -LiteralPath $ZipPath -Algorithm SHA256).Hash.ToLowerInvariant()
        $expectedLine = "$archiveHash  $([IO.Path]::GetFileName($ZipPath))"
        $actualLine = (Get-Content -LiteralPath $checksumPath -Raw -ErrorAction Stop).Trim()
        if ($actualLine -ne $expectedLine) {
            throw "ZIP checksum sidecar does not match the archive: $checksumPath"
        }
    }

    return [pscustomobject]@{
        ZipPath = [IO.Path]::GetFullPath($ZipPath)
        EntryCount = $zipEntries.Count
        FileCount = $actual.Count
        Sha256 = (Get-FileHash -LiteralPath $ZipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

function Assert-InstallerInputTree {
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)][string]$StagingPath,
        [Parameter(Mandatory = $true)][string]$InstallerScriptPath
    )

    $summary = Assert-PackageStaging -RepositoryRoot $RepositoryRoot -StagingPath $StagingPath
    if (-not (Test-Path -LiteralPath $InstallerScriptPath -PathType Leaf)) {
        throw "Installer script does not exist: $InstallerScriptPath"
    }

    $text = [IO.File]::ReadAllText($InstallerScriptPath)
    $sources = @([regex]::Matches($text, '(?im)^\s*Source:\s*"([^"]+)"') | ForEach-Object { $_.Groups[1].Value })
    if ($sources.Count -ne 1 -or $sources[0] -ne '{#PublishDir}\*') {
        throw 'Installer must have exactly one recursive source, the verified {#PublishDir} staging tree.'
    }
    if ($text -match '(?im)^\s*Source:\s*"(?!(\{#PublishDir\}\\\*))') {
        throw 'Installer contains a source path outside the verified publish directory.'
    }
    if ($text -match '(?i)(PLAN\.md|HANDOFF\.md|AGENTS\.md|SPEC\.md|tests[/\\]|spikes[/\\]|\.git[/\\])') {
        throw 'Installer source metadata names an internal or development path.'
    }

    return [pscustomobject]@{
        StagingPath = $summary.StagingPath
        FileCount = $summary.FileCount
        SourceDirective = $sources[0]
        InstallerPayloadInspection = 'source tree and Inno Source directive'
    }
}

function Assert-InstallerArtifact {
    param(
        [Parameter(Mandatory = $true)][string]$InstallerPath,
        [Parameter(Mandatory = $true)][string]$ExpectedVersion
    )

    if (-not (Test-Path -LiteralPath $InstallerPath -PathType Leaf)) {
        throw "Installer output does not exist: $InstallerPath"
    }
    $expectedName = "LyricsChatbox-Setup-$ExpectedVersion.exe"
    if ([IO.Path]::GetFileName($InstallerPath) -ne $expectedName) {
        throw "Unexpected installer name. Expected $expectedName"
    }
    $info = Get-Item -LiteralPath $InstallerPath -Force
    if ($info.Length -le 0) { throw 'Installer output is empty.' }

    $checksumPath = "$InstallerPath.sha256"
    if (-not (Test-Path -LiteralPath $checksumPath -PathType Leaf)) {
        throw "Installer checksum sidecar is missing: $checksumPath"
    }
    $hash = (Get-FileHash -LiteralPath $InstallerPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $expectedLine = "$hash  $expectedName"
    $actualLine = (Get-Content -LiteralPath $checksumPath -Raw -ErrorAction Stop).Trim()
    if ($actualLine -ne $expectedLine) {
        throw "Installer checksum sidecar does not match the installer: $checksumPath"
    }

    return [pscustomobject]@{
        InstallerPath = [IO.Path]::GetFullPath($InstallerPath)
        Length = $info.Length
        Sha256 = $hash
    }
}
