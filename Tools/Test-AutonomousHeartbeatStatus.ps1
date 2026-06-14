param(
    [switch]$KeepTempFiles
)

$ErrorActionPreference = 'Stop'

$scriptPath = Join-Path $PSScriptRoot 'Get-AutonomousHeartbeatStatus.ps1'
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) "lb_autonomous_heartbeat_status_tests_$([System.Guid]::NewGuid().ToString('N'))"
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

try {
    $rhythmPath = Join-Path $tempRoot 'rhythm_next_action_last.json'
    $preflightPath = Join-Path $tempRoot 'local_static_preflight_last_summary.json'
    $missingPath = Join-Path $tempRoot 'missing.json'

    $rhythm = [ordered]@{
        nextAction = 'CAPTURE_RHYTHM_SNAPSHOTS'
        overallEvidenceStatus = 'NO_EVIDENCE'
        requiresHumanCapture = $true
        automationCanProceed = $false
        blockedReason = 'MISSING_RHYTHM_SNAPSHOTS'
        resumeCondition = 'Capture at least one Calm, Build, Spike, and Release rhythm snapshot, then rerun Tools\Write-RhythmNextAction.cmd.'
        minimumCaptureCount = 4
        humanCaptureSteps = @(
            'Enter Play Mode.',
            'Use Write Rhythm Snapshot or press F13 once during Calm.',
            'Use Write Rhythm Snapshot or press F13 once during Build.',
            'Use Write Rhythm Snapshot or press F13 once during Spike.',
            'Use Write Rhythm Snapshot or press F13 once during Release.',
            'Run Tools\Write-RhythmSnapshotSummary.cmd, then rerun Tools\Get-RhythmNextAction.cmd.'
        )
        safeAlternateAutomationActions = @(
            'Run Tools\RunStaticPreflight.ps1 and fix FAIL results only.',
            'Improve static guardrails or documentation that does not retune rhythm feel.',
            'Update resource/planning docs from existing code evidence without claiming rhythm feel is proven.'
        )
    }
    $preflight = [ordered]@{
        generatedAt = '2026-06-15 00:00:00 KST'
        durationMilliseconds = 1234
        summary = [ordered]@{
            pass = 33
            warn = 3
            fail = 0
        }
    }

    $rhythm | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $rhythmPath -Encoding UTF8
    $preflight | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $preflightPath -Encoding UTF8

    $output = (& powershell -NoProfile -ExecutionPolicy Bypass -File $scriptPath -RhythmNextActionJsonPath $rhythmPath -StaticPreflightJsonPath $preflightPath) -join "`n"
    if ($LASTEXITCODE -ne 0) {
        throw "Get-AutonomousHeartbeatStatus failed with exit code $LASTEXITCODE"
    }

    Assert-Contains 'sample' $output 'LostBreadcrumbs Autonomous Heartbeat Status'
    Assert-Contains 'sample' $output 'RhythmNextAction: CAPTURE_RHYTHM_SNAPSHOTS'
    Assert-Contains 'sample' $output 'OverallEvidenceStatus: NO_EVIDENCE'
    Assert-Contains 'sample' $output 'RequiresHumanCapture: True'
    Assert-Contains 'sample' $output 'AutomationCanProceed: False'
    Assert-Contains 'sample' $output 'BlockedReason: MISSING_RHYTHM_SNAPSHOTS'
    Assert-Contains 'sample' $output 'HumanCaptureStepCount: 6'
    Assert-Contains 'sample' $output 'StaticPreflight: pass=33 warn=3 fail=0'
    Assert-Contains 'sample' $output 'SafeAlternateAutomationActions: Run Tools\RunStaticPreflight.ps1'

    $missingOutput = (& powershell -NoProfile -ExecutionPolicy Bypass -File $scriptPath -RhythmNextActionJsonPath $missingPath -StaticPreflightJsonPath $missingPath) -join "`n"
    if ($LASTEXITCODE -ne 0) {
        throw "Get-AutonomousHeartbeatStatus missing-input case failed with exit code $LASTEXITCODE"
    }

    Assert-Contains 'missing' $missingOutput 'RhythmNextActionExists: False'
    Assert-Contains 'missing' $missingOutput 'StaticPreflightExists: False'
    Assert-Contains 'missing' $missingOutput 'RhythmNextAction: missing; run Tools\Write-RhythmNextAction.cmd.'
    Assert-Contains 'missing' $missingOutput 'StaticPreflight: missing; run Tools\RunStaticPreflight.ps1.'

    Write-Host 'Autonomous heartbeat status tests passed.'
} finally {
    if (-not $KeepTempFiles -and (Test-Path -LiteralPath $tempRoot)) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
