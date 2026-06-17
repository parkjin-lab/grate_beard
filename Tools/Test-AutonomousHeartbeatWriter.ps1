param(
    [switch]$KeepTempFiles
)

$ErrorActionPreference = 'Stop'

$scriptPath = Join-Path $PSScriptRoot 'Write-AutonomousHeartbeatStatus.ps1'
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) "lb_autonomous_heartbeat_writer_tests_$([System.Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null

function Assert-Contains {
    param(
        [string]$Name,
        [string]$Text,
        [string]$Expected
    )

    if (-not $Text.Contains($Expected)) {
        throw "$Name missing expected Markdown: $Expected"
    }
}

try {
    $rhythmPath = Join-Path $tempRoot 'rhythm_next_action_last.json'
    $preflightPath = Join-Path $tempRoot 'local_static_preflight_last_summary.json'
    $safeTaskPath = Join-Path $tempRoot 'autonomous_safe_task_last.json'
    $outputPath = Join-Path $tempRoot 'autonomous_heartbeat_status_last.md'

    [ordered]@{
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
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $rhythmPath -Encoding UTF8

    [ordered]@{
        durationMilliseconds = 2222
        summary = [ordered]@{
            pass = 36
            warn = 3
            fail = 0
        }
    } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $preflightPath -Encoding UTF8

    [ordered]@{
        automationMode = 'SAFE_ALTERNATE_ONLY'
        recommendedTask = 'IMPROVE_GUARDRAILS_OR_DOCUMENTATION'
        recommendedCommand = 'Tools\Get-AutonomousHeartbeatStatus.cmd'
        canTuneRhythm = $false
        humanRequired = $true
        blockedReason = 'MISSING_RHYTHM_SNAPSHOTS'
        resumeCondition = 'Capture at least one Calm, Build, Spike, and Release rhythm snapshot, then rerun Tools\Write-RhythmNextAction.cmd.'
        targetPhases = @('Calm', 'Build', 'Spike', 'Release')
        captureHotkey = 'F13'
        minimumCaptureCount = 4
        humanActionSummary = 'Capture 4 rhythm snapshots with F13: Calm, Build, Spike, Release.'
        staticPreflightWarnCount = 3
        staticPreflightHasWarnings = $true
        staticPreflightWarningNames = @('logs.unityPreflightSummary', 'logs.autoSoakTrace', 'logs.autoSoakStatus')
        staticPreflightWarningSummary = 'logs.unityPreflightSummary, logs.autoSoakTrace, logs.autoSoakStatus'
        reason = 'Rhythm tuning is blocked by MISSING_RHYTHM_SNAPSHOTS; choose only non-tuning work until snapshots exist.'
        forbiddenAutomationActions = @(
            'Do not retune rhythm feel without Calm, Build, Spike, and Release snapshots.',
            'Do not claim Spike fairness or Release relief is proven from missing evidence.',
            'Do not run broad manual Play Mode validation as an automation substitute.'
        )
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $safeTaskPath -Encoding UTF8

    & powershell -NoProfile -ExecutionPolicy Bypass -File $scriptPath -RhythmNextActionJsonPath $rhythmPath -StaticPreflightJsonPath $preflightPath -SafeTaskJsonPath $safeTaskPath -OutputPath $outputPath | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Write-AutonomousHeartbeatStatus failed with exit code $LASTEXITCODE"
    }

    $markdown = Get-Content -LiteralPath $outputPath -Raw
    Assert-Contains 'writer' $markdown '## Recommended Safe Task'
    Assert-Contains 'writer' $markdown '- AutomationMode: SAFE_ALTERNATE_ONLY'
    Assert-Contains 'writer' $markdown '- BlockedReason: MISSING_RHYTHM_SNAPSHOTS'
    Assert-Contains 'writer' $markdown '- TargetPhases: Calm, Build, Spike, Release'
    Assert-Contains 'writer' $markdown '- CaptureHotkey: F13'
    Assert-Contains 'writer' $markdown '- HumanActionSummary: Capture 4 rhythm snapshots with F13: Calm, Build, Spike, Release.'
    Assert-Contains 'writer' $markdown '- StaticPreflightWarnCount: 3'
    Assert-Contains 'writer' $markdown '- StaticPreflightHasWarnings: True'
    Assert-Contains 'writer' $markdown '- StaticPreflightWarningNames: logs.unityPreflightSummary, logs.autoSoakTrace, logs.autoSoakStatus'
    Assert-Contains 'writer' $markdown '- StaticPreflightWarningSummary: logs.unityPreflightSummary, logs.autoSoakTrace, logs.autoSoakStatus'
    Assert-Contains 'writer' $markdown '## Forbidden Automation'
    Assert-Contains 'writer' $markdown 'Do not retune rhythm feel without Calm, Build, Spike, and Release snapshots.'
    Assert-Contains 'writer' $markdown '## Human Capture Steps'
    Assert-Contains 'writer' $markdown 'Use Write Rhythm Snapshot or press F13 once during Spike.'

    Write-Host 'Autonomous heartbeat writer tests passed.'
} finally {
    if (-not $KeepTempFiles -and (Test-Path -LiteralPath $tempRoot)) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
