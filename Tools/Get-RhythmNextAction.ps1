param(
    [string]$SummaryJsonPath = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..')).Path 'Logs/RhythmValidation/rhythm_snapshot_summary_last.json'),
    [string]$OutputJsonPath = ''
)

$ErrorActionPreference = 'Stop'

$summaryExists = Test-Path -LiteralPath $SummaryJsonPath
$status = 'SUMMARY_MISSING'
$phaseEvidenceComplete = $false
$sourceExitCode = 0
$targetPhases = @()
$nextAction = 'REFRESH_SUMMARY'
$requiresHumanCapture = $false
$automationCanProceed = $true
$captureHotkey = 'F13'
$minimumCaptureCount = 0
$humanCaptureSteps = @()
$blockedReason = ''
$resumeCondition = 'Run Tools\Write-RhythmNextAction.cmd again after refreshing rhythm evidence.'
$safeAlternateAutomationActions = @()
$rationale = 'Rhythm snapshot summary JSON is missing, so automation should refresh it before choosing tuning work.'
$suggestedCommand = 'Tools\Write-RhythmSnapshotSummary.cmd'

if ($summaryExists) {
    $summary = Get-Content -LiteralPath $SummaryJsonPath -Raw | ConvertFrom-Json
    $status = "$($summary.overallEvidenceStatus)"
    $phaseEvidenceComplete = [bool]$summary.phaseEvidenceComplete
    $sourceExitCode = [int]$summary.exitCode

    $phaseStatus = [ordered]@{
        Calm = "$($summary.calm.status)"
        Build = "$($summary.build.status)"
        Spike = "$($summary.spike.status)"
        Release = "$($summary.release.status)"
    }

    $missingPhases = @($phaseStatus.GetEnumerator() | Where-Object { $_.Value.StartsWith('NO_', [System.StringComparison]::Ordinal) } | ForEach-Object { $_.Key })
    $weakPhases = @($phaseStatus.GetEnumerator() | Where-Object {
        -not $_.Value.StartsWith('NO_', [System.StringComparison]::Ordinal) -and $_.Value -ne 'PASS'
    } | ForEach-Object { "$($_.Key):$($_.Value)" })

    switch ($status) {
        'NO_EVIDENCE' {
            $nextAction = 'CAPTURE_RHYTHM_SNAPSHOTS'
            $targetPhases = @('Calm', 'Build', 'Spike', 'Release')
            $requiresHumanCapture = $true
            $automationCanProceed = $false
            $minimumCaptureCount = 4
            $blockedReason = 'MISSING_RHYTHM_SNAPSHOTS'
            $resumeCondition = 'Capture at least one Calm, Build, Spike, and Release rhythm snapshot, then rerun Tools\Write-RhythmNextAction.cmd.'
            $safeAlternateAutomationActions = @(
                'Run Tools\RunStaticPreflight.ps1 and fix FAIL results only.',
                'Improve static guardrails or documentation that does not retune rhythm feel.',
                'Update resource/planning docs from existing code evidence without claiming rhythm feel is proven.'
            )
            $humanCaptureSteps = @(
                'Enter Play Mode.',
                'Use Write Rhythm Snapshot or press F13 once during Calm.',
                'Use Write Rhythm Snapshot or press F13 once during Build.',
                'Use Write Rhythm Snapshot or press F13 once during Spike.',
                'Use Write Rhythm Snapshot or press F13 once during Release.',
                'Run Tools\Write-RhythmSnapshotSummary.cmd, then rerun Tools\Get-RhythmNextAction.cmd.'
            )
            $rationale = 'No rhythm snapshots exist yet. Capture one lightweight snapshot per phase before retuning feel.'
            $suggestedCommand = 'Enter Play Mode, use Write Rhythm Snapshot or press F13 once during each rhythm phase, then run Tools\Write-RhythmSnapshotSummary.cmd.'
        }
        'PARTIAL_EVIDENCE' {
            $nextAction = 'CAPTURE_MISSING_PHASES'
            $targetPhases = $missingPhases
            $requiresHumanCapture = $true
            $automationCanProceed = $false
            $minimumCaptureCount = $missingPhases.Count
            $blockedReason = 'MISSING_RHYTHM_PHASE_SNAPSHOTS'
            $resumeCondition = "Capture snapshots for missing phases: $($missingPhases -join ', '), then rerun Tools\Write-RhythmNextAction.cmd."
            $safeAlternateAutomationActions = @(
                'Run Tools\RunStaticPreflight.ps1 and fix FAIL results only.',
                'Improve static guardrails or documentation that does not retune rhythm feel.',
                'Update capture handoff wording from existing evidence without changing gameplay tuning.'
            )
            $humanCaptureSteps = @(
                'Enter Play Mode.',
                "Use Write Rhythm Snapshot or press F13 once during each missing phase: $($missingPhases -join ', ').",
                'Run Tools\Write-RhythmSnapshotSummary.cmd, then rerun Tools\Get-RhythmNextAction.cmd.'
            )
            $rationale = 'Some phases have clean evidence, but the rhythm cycle is not fully covered yet.'
            $suggestedCommand = "Capture missing phase snapshots: $($missingPhases -join ', ')."
        }
        'NEEDS_TUNING' {
            $nextAction = 'TUNE_WEAK_PHASES'
            $targetPhases = $weakPhases
            $rationale = 'Existing rhythm snapshots include at least one failed phase-specific evidence check.'
            $suggestedCommand = "Tune the flagged phase evidence first: $($weakPhases -join ', ')."
        }
        'PASS' {
            $nextAction = 'CONTINUE_NEXT_RHYTHM_VARIATION'
            $targetPhases = @('Build', 'Spike', 'Release')
            $rationale = 'Current snapshot evidence is complete and clean, so avoid retuning old evidence and add the next controlled variation.'
            $suggestedCommand = 'Pick one small rhythm-variation task, add static hooks, then refresh snapshots when a human pass is available.'
        }
        default {
            throw "Unsupported rhythm evidence status '$status' in $SummaryJsonPath"
        }
    }
}

$result = [ordered]@{
    schemaVersion = 1
    summaryJsonPath = $SummaryJsonPath
    summaryExists = $summaryExists
    overallEvidenceStatus = $status
    phaseEvidenceComplete = $phaseEvidenceComplete
    sourceExitCode = $sourceExitCode
    nextAction = $nextAction
    targetPhases = @($targetPhases)
    requiresHumanCapture = $requiresHumanCapture
    automationCanProceed = $automationCanProceed
    captureHotkey = $captureHotkey
    minimumCaptureCount = $minimumCaptureCount
    humanCaptureSteps = @($humanCaptureSteps)
    blockedReason = $blockedReason
    resumeCondition = $resumeCondition
    safeAlternateAutomationActions = @($safeAlternateAutomationActions)
    rationale = $rationale
    suggestedCommand = $suggestedCommand
}

Write-Host 'LostBreadcrumbs Rhythm Next Action'
Write-Host "SummaryJsonPath: $SummaryJsonPath"
Write-Host "SummaryExists: $summaryExists"
Write-Host "OverallEvidenceStatus: $status"
Write-Host "PhaseEvidenceComplete: $phaseEvidenceComplete"
Write-Host "NextAction: $nextAction"
Write-Host "TargetPhases: $(@($targetPhases) -join ', ')"
Write-Host "RequiresHumanCapture: $requiresHumanCapture"
Write-Host "AutomationCanProceed: $automationCanProceed"
Write-Host "CaptureHotkey: $captureHotkey"
Write-Host "MinimumCaptureCount: $minimumCaptureCount"
Write-Host "BlockedReason: $(if ([string]::IsNullOrWhiteSpace($blockedReason)) { 'none' } else { $blockedReason })"
Write-Host "ResumeCondition: $resumeCondition"
if ($humanCaptureSteps.Count -gt 0) {
    Write-Host "HumanCaptureSteps: $($humanCaptureSteps -join ' | ')"
}
if ($safeAlternateAutomationActions.Count -gt 0) {
    Write-Host "SafeAlternateAutomationActions: $($safeAlternateAutomationActions -join ' | ')"
}
Write-Host "Rationale: $rationale"
Write-Host "SuggestedCommand: $suggestedCommand"

if (-not [string]::IsNullOrWhiteSpace($OutputJsonPath)) {
    $jsonDirectory = Split-Path -Parent $OutputJsonPath
    if (-not [string]::IsNullOrWhiteSpace($jsonDirectory)) {
        New-Item -ItemType Directory -Force -Path $jsonDirectory | Out-Null
    }

    $result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $OutputJsonPath -Encoding UTF8
}
