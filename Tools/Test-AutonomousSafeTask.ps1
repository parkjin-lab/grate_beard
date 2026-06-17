param(
    [switch]$KeepTempFiles
)

$ErrorActionPreference = 'Stop'

$scriptPath = Join-Path $PSScriptRoot 'Get-AutonomousSafeTask.ps1'
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) "lb_autonomous_safe_task_tests_$([System.Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null

function Assert-Contains {
    param(
        [string]$Name,
        [string]$Text,
        [string]$Expected
    )

    if (-not $Text.Contains($Expected)) {
        throw "$Name missing expected output: $Expected"
    }
}

function Write-RhythmCase {
    param(
        [string]$Name,
        [string]$NextAction,
        [bool]$RequiresHumanCapture,
        [bool]$AutomationCanProceed,
        [string]$BlockedReason = ''
    )

    $path = Join-Path $tempRoot "$Name.rhythm.json"
    [ordered]@{
        nextAction = $NextAction
        targetPhases = @('Calm', 'Build', 'Spike', 'Release')
        captureHotkey = 'F13'
        minimumCaptureCount = $(if ($RequiresHumanCapture) { 4 } else { 0 })
        requiresHumanCapture = $RequiresHumanCapture
        automationCanProceed = $AutomationCanProceed
        blockedReason = $BlockedReason
        resumeCondition = 'Capture at least one Calm, Build, Spike, and Release rhythm snapshot, then rerun Tools\Write-RhythmNextAction.cmd.'
        safeAlternateAutomationActions = @(
            'Run Tools\RunStaticPreflight.ps1 and fix FAIL results only.',
            'Improve static guardrails or documentation that does not retune rhythm feel.',
            'Update resource/planning docs from existing code evidence without claiming rhythm feel is proven.'
        )
    } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $path -Encoding UTF8
    return $path
}

function Write-PreflightCase {
    param(
        [string]$Name,
        [int]$FailCount,
        [int]$WarnCount = 3,
        [string[]]$WarnNames = @('logs.unityPreflightSummary', 'logs.autoSoakTrace', 'logs.autoSoakStatus')
    )

    $path = Join-Path $tempRoot "$Name.preflight.json"
    $results = foreach ($warnName in $WarnNames) {
        [ordered]@{
            name = $warnName
            status = 'WARN'
            detail = 'exists=True stale=True refreshRequired=True'
        }
    }

    [ordered]@{
        hasWarnings = $WarnCount -gt 0
        summary = [ordered]@{
            pass = 34
            warn = $WarnCount
            fail = $FailCount
        }
        results = @($results)
    } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $path -Encoding UTF8
    return $path
}

function Invoke-SafeTaskCase {
    param(
        [string]$Name,
        [string]$RhythmPath,
        [string]$PreflightPath,
        [string]$ExpectedTask,
        [string]$ExpectedCanTuneRhythm,
        [string]$ExpectedHumanRequired,
        [string]$ExpectedAutomationMode,
        [string]$ExpectedBlockedReason,
        [int]$ExpectedTargetPhaseCount,
        [string]$ExpectedCaptureHotkey,
        [string]$ExpectedHumanActionSummary,
        [int]$ExpectedForbiddenActionCount
    )

    $outputPath = Join-Path $tempRoot "$Name.safe-task.json"
    $output = (& powershell -NoProfile -ExecutionPolicy Bypass -File $scriptPath -RhythmNextActionJsonPath $RhythmPath -StaticPreflightJsonPath $PreflightPath -OutputJsonPath $outputPath) -join "`n"
    if ($LASTEXITCODE -ne 0) {
        throw "Get-AutonomousSafeTask failed for $Name with exit code $LASTEXITCODE"
    }

    Assert-Contains $Name $output "RecommendedTask: $ExpectedTask"
    Assert-Contains $Name $output 'StaticPreflightWarnCount:'
    Assert-Contains $Name $output 'StaticPreflightHasWarnings:'
    Assert-Contains $Name $output 'StaticPreflightWarningNames:'
    Assert-Contains $Name $output 'StaticPreflightWarningSummary:'
    Assert-Contains $Name $output "CanTuneRhythm: $ExpectedCanTuneRhythm"
    Assert-Contains $Name $output "HumanRequired: $ExpectedHumanRequired"
    Assert-Contains $Name $output "AutomationMode: $ExpectedAutomationMode"
    Assert-Contains $Name $output "BlockedReason: $(if ([string]::IsNullOrWhiteSpace($ExpectedBlockedReason)) { 'none' } else { $ExpectedBlockedReason })"
    Assert-Contains $Name $output "CaptureHotkey: $ExpectedCaptureHotkey"
    Assert-Contains $Name $output "HumanActionSummary: $ExpectedHumanActionSummary"
    Assert-Contains $Name $output "OutputJsonPath: $outputPath"

    $actualJson = Get-Content -LiteralPath $outputPath -Raw | ConvertFrom-Json
    if ($actualJson.schemaVersion -ne 1) {
        throw "$Name schemaVersion expected=1 actual=$($actualJson.schemaVersion)"
    }

    if ("$($actualJson.recommendedTask)" -ne $ExpectedTask) {
        throw "$Name recommendedTask JSON expected=$ExpectedTask actual=$($actualJson.recommendedTask)"
    }

    if ($null -eq $actualJson.staticPreflightWarnCount) {
        throw "$Name staticPreflightWarnCount JSON missing"
    }

    if ($null -eq $actualJson.staticPreflightHasWarnings) {
        throw "$Name staticPreflightHasWarnings JSON missing"
    }

    if ($null -eq $actualJson.staticPreflightWarningNames) {
        throw "$Name staticPreflightWarningNames JSON missing"
    }

    if ($null -eq $actualJson.staticPreflightWarningSummary) {
        throw "$Name staticPreflightWarningSummary JSON missing"
    }

    if ([int]$actualJson.staticPreflightWarnCount -gt 0 -and @($actualJson.staticPreflightWarningNames).Count -eq 0) {
        throw "$Name staticPreflightWarningNames expected warning names when warn count is positive"
    }

    if ("$($actualJson.canTuneRhythm)" -ne $ExpectedCanTuneRhythm) {
        throw "$Name canTuneRhythm JSON expected=$ExpectedCanTuneRhythm actual=$($actualJson.canTuneRhythm)"
    }

    if ("$($actualJson.humanRequired)" -ne $ExpectedHumanRequired) {
        throw "$Name humanRequired JSON expected=$ExpectedHumanRequired actual=$($actualJson.humanRequired)"
    }

    if ("$($actualJson.automationMode)" -ne $ExpectedAutomationMode) {
        throw "$Name automationMode JSON expected=$ExpectedAutomationMode actual=$($actualJson.automationMode)"
    }

    if ("$($actualJson.blockedReason)" -ne $ExpectedBlockedReason) {
        throw "$Name blockedReason JSON expected=$ExpectedBlockedReason actual=$($actualJson.blockedReason)"
    }

    $actualTargetPhaseCount = if ($null -eq $actualJson.targetPhases -or [string]::IsNullOrWhiteSpace("$($actualJson.targetPhases)")) { 0 } else { @($actualJson.targetPhases).Count }
    if ($actualTargetPhaseCount -ne $ExpectedTargetPhaseCount) {
        throw "$Name targetPhases.Count JSON expected=$ExpectedTargetPhaseCount actual=$actualTargetPhaseCount"
    }

    if ("$($actualJson.captureHotkey)" -ne $ExpectedCaptureHotkey) {
        throw "$Name captureHotkey JSON expected=$ExpectedCaptureHotkey actual=$($actualJson.captureHotkey)"
    }

    if ("$($actualJson.humanActionSummary)" -ne $ExpectedHumanActionSummary) {
        throw "$Name humanActionSummary JSON expected=$ExpectedHumanActionSummary actual=$($actualJson.humanActionSummary)"
    }

    if (@($actualJson.forbiddenAutomationActions).Count -ne $ExpectedForbiddenActionCount) {
        throw "$Name forbiddenAutomationActions.Count expected=$ExpectedForbiddenActionCount actual=$(@($actualJson.forbiddenAutomationActions).Count)"
    }

    Write-Host "[PASS] $Name -> $ExpectedTask"
}

try {
    $blockedRhythm = Write-RhythmCase 'blocked' 'CAPTURE_RHYTHM_SNAPSHOTS' $true $false 'MISSING_RHYTHM_SNAPSHOTS'
    $tuningRhythm = Write-RhythmCase 'tuning' 'TUNE_WEAK_PHASES' $false $true ''
    $cleanPreflight = Write-PreflightCase 'clean' 0
    $failedPreflight = Write-PreflightCase 'failed' 2
    $missingPath = Join-Path $tempRoot 'missing.json'

    Invoke-SafeTaskCase 'FAILED_PREFLIGHT' $blockedRhythm $failedPreflight 'FIX_STATIC_PREFLIGHT_FAILURES' 'False' 'False' 'FIX_FAILURES_ONLY' 'MISSING_RHYTHM_SNAPSHOTS' 4 'F13' 'No human play capture is needed; fix static preflight FAIL results first.' 2
    Invoke-SafeTaskCase 'CAPTURE_BLOCKED' $blockedRhythm $cleanPreflight 'IMPROVE_GUARDRAILS_OR_DOCUMENTATION' 'False' 'True' 'SAFE_ALTERNATE_ONLY' 'MISSING_RHYTHM_SNAPSHOTS' 4 'F13' 'Capture 4 rhythm snapshots with F13: Calm, Build, Spike, Release.' 3
    Invoke-SafeTaskCase 'RHYTHM_TUNING_ALLOWED' $tuningRhythm $cleanPreflight 'TUNE_WEAK_PHASES' 'True' 'False' 'RHYTHM_TUNING_ALLOWED' '' 4 'F13' 'No human capture is required by the current safe-task state.' 0
    Invoke-SafeTaskCase 'MISSING_STATUS' $missingPath $missingPath 'REFRESH_STATUS_EVIDENCE' 'False' 'False' 'REFRESH_STATUS' '' 0 '' 'Refresh status evidence before choosing gameplay or tuning work.' 0

    Write-Host 'Autonomous safe-task tests passed.'
} finally {
    if (-not $KeepTempFiles -and (Test-Path -LiteralPath $tempRoot)) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
