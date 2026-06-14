param(
    [switch]$KeepTempFiles
)

$ErrorActionPreference = 'Stop'

$scriptPath = Join-Path $PSScriptRoot 'Get-RhythmNextAction.ps1'
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) "lb_rhythm_next_action_tests_$([System.Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null

function New-PhaseSummary {
    param(
        [string]$Status,
        [int]$Total = 0
    )

    return [ordered]@{
        status = $Status
        total = $Total
        pass = $(if ($Status -eq 'PASS') { $Total } else { 0 })
        rushed = 0
        flat = 0
        unfair = 0
        weak = 0
        missingPressure = 0
        missingFlags = 0
    }
}

function Write-SummaryCase {
    param(
        [string]$Name,
        [string]$OverallStatus,
        [bool]$PhaseEvidenceComplete,
        [hashtable]$PhaseStatuses
    )

    $path = Join-Path $tempRoot "$Name.json"
    $summary = [ordered]@{
        schemaVersion = 1
        inputDirectory = $tempRoot
        directoryExists = $true
        snapshotCount = 1
        exitCode = $(if ($OverallStatus -eq 'NEEDS_TUNING') { 2 } else { 0 })
        overallEvidenceStatus = $OverallStatus
        phaseEvidenceComplete = $PhaseEvidenceComplete
        phaseCounts = [ordered]@{}
        calm = New-PhaseSummary -Status $PhaseStatuses.Calm -Total $(if ($PhaseStatuses.Calm.StartsWith('NO_')) { 0 } else { 1 })
        build = New-PhaseSummary -Status $PhaseStatuses.Build -Total $(if ($PhaseStatuses.Build.StartsWith('NO_')) { 0 } else { 1 })
        spike = New-PhaseSummary -Status $PhaseStatuses.Spike -Total $(if ($PhaseStatuses.Spike.StartsWith('NO_')) { 0 } else { 1 })
        release = New-PhaseSummary -Status $PhaseStatuses.Release -Total $(if ($PhaseStatuses.Release.StartsWith('NO_')) { 0 } else { 1 })
    }

    $summary | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $path -Encoding UTF8
    return $path
}

function Invoke-NextActionCase {
    param(
        [string]$Name,
        [string]$SummaryPath,
        [string]$ExpectedAction,
        [bool]$ExpectedRequiresHuman,
        [bool]$ExpectedCanProceed,
        [string]$ExpectedBlockedReason
    )

    $outputPath = Join-Path $tempRoot "$Name.next.json"
    & powershell -NoProfile -ExecutionPolicy Bypass -File $scriptPath -SummaryJsonPath $SummaryPath -OutputJsonPath $outputPath | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Get-RhythmNextAction failed for $Name with exit code $LASTEXITCODE"
    }

    $actual = Get-Content -LiteralPath $outputPath -Raw | ConvertFrom-Json
    if ($actual.nextAction -ne $ExpectedAction) {
        throw "$Name nextAction expected=$ExpectedAction actual=$($actual.nextAction)"
    }

    if ([bool]$actual.requiresHumanCapture -ne $ExpectedRequiresHuman) {
        throw "$Name requiresHumanCapture expected=$ExpectedRequiresHuman actual=$($actual.requiresHumanCapture)"
    }

    if ([bool]$actual.automationCanProceed -ne $ExpectedCanProceed) {
        throw "$Name automationCanProceed expected=$ExpectedCanProceed actual=$($actual.automationCanProceed)"
    }

    if ("$($actual.blockedReason)" -ne $ExpectedBlockedReason) {
        throw "$Name blockedReason expected=$ExpectedBlockedReason actual=$($actual.blockedReason)"
    }

    Write-Host "[PASS] $Name -> $($actual.nextAction)"
}

try {
    $noEvidence = Write-SummaryCase 'no_evidence' 'NO_EVIDENCE' $false @{
        Calm = 'NO_CALM_SNAPSHOTS'
        Build = 'NO_BUILD_SNAPSHOTS'
        Spike = 'NO_SPIKE_SNAPSHOTS'
        Release = 'NO_RELEASE_SNAPSHOTS'
    }
    Invoke-NextActionCase 'NO_EVIDENCE' $noEvidence 'CAPTURE_RHYTHM_SNAPSHOTS' $true $false 'MISSING_RHYTHM_SNAPSHOTS'

    $partial = Write-SummaryCase 'partial' 'PARTIAL_EVIDENCE' $false @{
        Calm = 'PASS'
        Build = 'NO_BUILD_SNAPSHOTS'
        Spike = 'NO_SPIKE_SNAPSHOTS'
        Release = 'NO_RELEASE_SNAPSHOTS'
    }
    Invoke-NextActionCase 'PARTIAL_EVIDENCE' $partial 'CAPTURE_MISSING_PHASES' $true $false 'MISSING_RHYTHM_PHASE_SNAPSHOTS'

    $needsTuning = Write-SummaryCase 'needs_tuning' 'NEEDS_TUNING' $true @{
        Calm = 'PASS'
        Build = 'PASS'
        Spike = 'UNFAIR'
        Release = 'PASS'
    }
    Invoke-NextActionCase 'NEEDS_TUNING' $needsTuning 'TUNE_WEAK_PHASES' $false $true ''

    $pass = Write-SummaryCase 'pass' 'PASS' $true @{
        Calm = 'PASS'
        Build = 'PASS'
        Spike = 'PASS'
        Release = 'PASS'
    }
    Invoke-NextActionCase 'PASS' $pass 'CONTINUE_NEXT_RHYTHM_VARIATION' $false $true ''

    Write-Host 'Rhythm next-action tests passed.'
} finally {
    if (-not $KeepTempFiles -and (Test-Path -LiteralPath $tempRoot)) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
