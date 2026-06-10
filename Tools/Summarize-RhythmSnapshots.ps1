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

function Read-FlagBool {
    param(
        [string]$Flags,
        [string]$Name
    )

    if ([string]::IsNullOrWhiteSpace($Flags)) {
        return $null
    }

    $match = [regex]::Match($Flags, "(^|,\s*)$([regex]::Escape($Name))=(True|False|true|false|YES|NO|yes|no|1|0)")
    if (-not $match.Success) {
        return $null
    }

    switch ($match.Groups[2].Value.ToLowerInvariant()) {
        'true' { return $true }
        'yes' { return $true }
        '1' { return $true }
        default { return $false }
    }
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
$spikeTotal = 0
$spikePass = 0
$spikeUnfair = 0
$spikeMissingFlags = 0
$phaseCounts = @{}
$lastReleaseSummary = ''
$lastReleaseFile = ''
$lastSpikeSummary = ''
$lastSpikeFile = ''
$weakFiles = New-Object System.Collections.Generic.List[string]
$unfairSpikeFiles = New-Object System.Collections.Generic.List[string]

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

    if ($phase -eq 'Spike') {
        $spikeTotal++
        $spikeFlags = Read-SnapshotValue -Lines $lines -Key 'SpikeFairnessFlags'
        $spikeSummary = Read-SnapshotValue -Lines $lines -Key 'SpikeFairness'
        $entryWarn = Read-FlagBool -Flags $spikeFlags -Name 'entryWarn'
        $chaseWarn = Read-FlagBool -Flags $spikeFlags -Name 'chaseWarn'
        $lastSpikeSummary = if ([string]::IsNullOrWhiteSpace($spikeSummary)) { $spikeFlags } else { $spikeSummary }
        $lastSpikeFile = $file.Name

        if ($null -eq $entryWarn -and $null -eq $chaseWarn) {
            $spikeMissingFlags++
            $unfairSpikeFiles.Add($file.Name)
        } elseif ($entryWarn -or $chaseWarn) {
            $spikePass++
        } else {
            $spikeUnfair++
            $unfairSpikeFiles.Add("$($file.Name): entryWarn=$entryWarn chaseWarn=$chaseWarn")
        }
    }

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
$hasSpikeEvidence = $spikeTotal -gt 0
$allSpikeSnapshotsPass = -not $hasSpikeEvidence -or ($spikeUnfair -eq 0 -and $spikeMissingFlags -eq 0)

Write-Host 'LostBreadcrumbs Rhythm Snapshot Summary'
Write-Host "InputDirectory: $InputDirectory"
Write-Host "DirectoryExists: $exists"
Write-Host "SnapshotCount: $total"
Write-Host "PhaseCounts: $phaseSummary"
Write-Host "ReleaseSnapshots: total=$releaseTotal pass=$releasePass weak=$releaseWeak missingFlags=$releaseMissingFlags minimumChannels=$minimum"
Write-Host "ReleaseEvidenceStatus: $(if (-not $hasReleaseEvidence) { 'NO_RELEASE_SNAPSHOTS' } elseif ($allReleaseSnapshotsPass) { 'PASS' } else { 'WEAK' })"
Write-Host "LastReleaseSnapshot: $(if ([string]::IsNullOrWhiteSpace($lastReleaseFile)) { 'none' } else { $lastReleaseFile })"
Write-Host "LastReleaseRelief: $(if ([string]::IsNullOrWhiteSpace($lastReleaseSummary)) { 'none' } else { $lastReleaseSummary })"
Write-Host "SpikeSnapshots: total=$spikeTotal pass=$spikePass unfair=$spikeUnfair missingFlags=$spikeMissingFlags"
Write-Host "SpikeEvidenceStatus: $(if (-not $hasSpikeEvidence) { 'NO_SPIKE_SNAPSHOTS' } elseif ($allSpikeSnapshotsPass) { 'PASS' } else { 'UNFAIR' })"
Write-Host "LastSpikeSnapshot: $(if ([string]::IsNullOrWhiteSpace($lastSpikeFile)) { 'none' } else { $lastSpikeFile })"
Write-Host "LastSpikeFairness: $(if ([string]::IsNullOrWhiteSpace($lastSpikeSummary)) { 'none' } else { $lastSpikeSummary })"

if ($weakFiles.Count -gt 0) {
    Write-Host "WeakReleaseFiles: $($weakFiles -join '; ')"
}

if ($unfairSpikeFiles.Count -gt 0) {
    Write-Host "UnfairSpikeFiles: $($unfairSpikeFiles -join '; ')"
}

if (-not $hasReleaseEvidence -and -not $hasSpikeEvidence) {
    exit 0
}

if ($allReleaseSnapshotsPass -and $allSpikeSnapshotsPass) {
    exit 0
}

exit 2
