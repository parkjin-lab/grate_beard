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
        requiresHumanCapture = $RequiresHumanCapture
        automationCanProceed = $AutomationCanProceed
        blockedReason = $BlockedReason
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
        [int]$FailCount
    )

    $path = Join-Path $tempRoot "$Name.preflight.json"
    [ordered]@{
        summary = [ordered]@{
            pass = 34
            warn = 3
            fail = $FailCount
        }
    } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $path -Encoding UTF8
    return $path
}

function Invoke-SafeTaskCase {
    param(
        [string]$Name,
        [string]$RhythmPath,
        [string]$PreflightPath,
        [string]$ExpectedTask,
        [string]$ExpectedCanTuneRhythm
    )

    $output = (& powershell -NoProfile -ExecutionPolicy Bypass -File $scriptPath -RhythmNextActionJsonPath $RhythmPath -StaticPreflightJsonPath $PreflightPath) -join "`n"
    if ($LASTEXITCODE -ne 0) {
        throw "Get-AutonomousSafeTask failed for $Name with exit code $LASTEXITCODE"
    }

    Assert-Contains $Name $output "RecommendedTask: $ExpectedTask"
    Assert-Contains $Name $output "CanTuneRhythm: $ExpectedCanTuneRhythm"
    Write-Host "[PASS] $Name -> $ExpectedTask"
}

try {
    $blockedRhythm = Write-RhythmCase 'blocked' 'CAPTURE_RHYTHM_SNAPSHOTS' $true $false 'MISSING_RHYTHM_SNAPSHOTS'
    $tuningRhythm = Write-RhythmCase 'tuning' 'TUNE_WEAK_PHASES' $false $true ''
    $cleanPreflight = Write-PreflightCase 'clean' 0
    $failedPreflight = Write-PreflightCase 'failed' 2
    $missingPath = Join-Path $tempRoot 'missing.json'

    Invoke-SafeTaskCase 'FAILED_PREFLIGHT' $blockedRhythm $failedPreflight 'FIX_STATIC_PREFLIGHT_FAILURES' 'False'
    Invoke-SafeTaskCase 'CAPTURE_BLOCKED' $blockedRhythm $cleanPreflight 'IMPROVE_GUARDRAILS_OR_DOCUMENTATION' 'False'
    Invoke-SafeTaskCase 'RHYTHM_TUNING_ALLOWED' $tuningRhythm $cleanPreflight 'TUNE_WEAK_PHASES' 'True'
    Invoke-SafeTaskCase 'MISSING_STATUS' $missingPath $missingPath 'REFRESH_STATUS_EVIDENCE' 'False'

    Write-Host 'Autonomous safe-task tests passed.'
} finally {
    if (-not $KeepTempFiles -and (Test-Path -LiteralPath $tempRoot)) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
