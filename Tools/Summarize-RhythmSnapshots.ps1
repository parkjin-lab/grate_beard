param(
    [string]$InputDirectory = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..')).Path 'Logs/RhythmValidation'),
    [int]$MinimumReleaseChannels = 2
)

$ErrorActionPreference = 'Stop'

function Read-SnapshotValue {
    param(
        [string[]]$Lines,
        [string]$Key
    )

    $prefix = "${Key}:"
    $line = @($Lines) | Where-Object { $_ -ne $null -and $_.StartsWith($prefix, [System.StringComparison]::Ordinal) } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($line)) {
        return ''
    }

    return $line.Substring($prefix.Length).Trim()
}

function Read-FlagNumber {
    param(
        [string]$Flags,
        [string]$Name
    )

    if ([string]::IsNullOrWhiteSpace($Flags)) {
        return $null
    }

    $match = [regex]::Match($Flags, "(^|,\s*)$([regex]::Escape($Name))=([-+]?\d+(?:\.\d+)?)")
    if (-not $match.Success) {
        return $null
    }

    return [double]::Parse($match.Groups[2].Value, [Globalization.CultureInfo]::InvariantCulture)
}

$minimum = [Math]::Max(0, $MinimumReleaseChannels)
$exists = Test-Path -LiteralPath $InputDirectory
$files = @()
if ($exists) {
    $files = Get-ChildItem -LiteralPath $InputDirectory -Filter 'rhythm_snapshot_*.txt' -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime
}

$total = 0
$releaseTotal = 0
$releasePass = 0
$releaseWeak = 0
$releaseMissingFlags = 0
$phaseCounts = @{}
$lastReleaseSummary = ''
$lastReleaseFile = ''
$weakFiles = New-Object System.Collections.Generic.List[string]

foreach ($file in $files) {
    $total++
    $lines = Get-Content -LiteralPath $file.FullName
    $phase = Read-SnapshotValue -Lines $lines -Key 'RhythmPhase'
    if ([string]::IsNullOrWhiteSpace($phase)) {
        $phase = 'Unknown'
    }

    if (-not $phaseCounts.ContainsKey($phase)) {
        $phaseCounts[$phase] = 0
    }
    $phaseCounts[$phase]++

    if ($phase -ne 'Release') {
        continue
    }

    $releaseTotal++
    $flags = Read-SnapshotValue -Lines $lines -Key 'ReleaseReliefFlags'
    $summary = Read-SnapshotValue -Lines $lines -Key 'ReleaseRelief'
    $channels = Read-FlagNumber -Flags $flags -Name 'channels'
    $lastReleaseSummary = if ([string]::IsNullOrWhiteSpace($summary)) { $flags } else { $summary }
    $lastReleaseFile = $file.Name

    if ($null -eq $channels) {
        $releaseMissingFlags++
        $weakFiles.Add($file.Name)
        continue
    }

    if ($channels -ge $minimum) {
        $releasePass++
    } else {
        $releaseWeak++
        $weakFiles.Add("$($file.Name): channels=$channels")
    }
}

$phaseSummary = if ($phaseCounts.Count -eq 0) {
    'none'
} else {
    ($phaseCounts.GetEnumerator() | Sort-Object Name | ForEach-Object { "$($_.Name)=$($_.Value)" }) -join ', '
}

$hasReleaseEvidence = $releaseTotal -gt 0
$allReleaseSnapshotsPass = -not $hasReleaseEvidence -or ($releaseWeak -eq 0 -and $releaseMissingFlags -eq 0)

Write-Host 'LostBreadcrumbs Rhythm Snapshot Summary'
Write-Host "InputDirectory: $InputDirectory"
Write-Host "DirectoryExists: $exists"
Write-Host "SnapshotCount: $total"
Write-Host "PhaseCounts: $phaseSummary"
Write-Host "ReleaseSnapshots: total=$releaseTotal pass=$releasePass weak=$releaseWeak missingFlags=$releaseMissingFlags minimumChannels=$minimum"
Write-Host "ReleaseEvidenceStatus: $(if (-not $hasReleaseEvidence) { 'NO_RELEASE_SNAPSHOTS' } elseif ($allReleaseSnapshotsPass) { 'PASS' } else { 'WEAK' })"
Write-Host "LastReleaseSnapshot: $(if ([string]::IsNullOrWhiteSpace($lastReleaseFile)) { 'none' } else { $lastReleaseFile })"
Write-Host "LastReleaseRelief: $(if ([string]::IsNullOrWhiteSpace($lastReleaseSummary)) { 'none' } else { $lastReleaseSummary })"

if ($weakFiles.Count -gt 0) {
    Write-Host "WeakReleaseFiles: $($weakFiles -join '; ')"
}

if (-not $hasReleaseEvidence) {
    exit 0
}

if ($allReleaseSnapshotsPass) {
    exit 0
}

exit 2
