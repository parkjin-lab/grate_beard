param(
    [string]$RhythmNextActionJsonPath = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..')).Path 'Logs/RhythmValidation/rhythm_next_action_last.json'),
    [string]$StaticPreflightJsonPath = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..')).Path 'Logs/ReleaseSoak/local_static_preflight_last_summary.json'),
    [string]$OutputJsonPath = ''
)

$ErrorActionPreference = 'Stop'

function Read-OptionalJson {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path)) {
        return $null
    }

    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

$rhythm = Read-OptionalJson $RhythmNextActionJsonPath
$preflight = Read-OptionalJson $StaticPreflightJsonPath
$preflightFailCount = if ($null -ne $preflight) { [int]$preflight.summary.fail } else { -1 }
$preflightWarnCount = if ($null -ne $preflight) { [int]$preflight.summary.warn } else { -1 }
$preflightHasWarnings = if ($null -ne $preflight) {
    if ($null -ne $preflight.hasWarnings) {
        [bool]$preflight.hasWarnings
    } else {
        $preflightWarnCount -gt 0
    }
} else {
    $false
}
$preflightWarningNames = if ($null -ne $preflight -and $null -ne $preflight.results) {
    @($preflight.results | Where-Object { "$($_.status)" -eq 'WARN' } | ForEach-Object { "$($_.name)" })
} else {
    @()
}
$preflightWarningSummary = if ($preflightWarningNames.Count -gt 0) {
    $preflightWarningNames -join ', '
} elseif ($preflightHasWarnings) {
    'warnings present'
} else {
    'none'
}
$safeActions = if ($null -ne $rhythm) { @($rhythm.safeAlternateAutomationActions) } else { @() }
[string[]]$targetPhases = if ($null -ne $rhythm) { @($rhythm.targetPhases) } else { @() }
$requiresHumanCapture = $null -ne $rhythm -and [bool]$rhythm.requiresHumanCapture
$automationCanProceed = $null -ne $rhythm -and [bool]$rhythm.automationCanProceed
$blockedReason = if ($null -ne $rhythm) { "$($rhythm.blockedReason)" } else { '' }
$resumeCondition = if ($null -ne $rhythm) { "$($rhythm.resumeCondition)" } else { 'Refresh rhythm next-action evidence.' }
$captureHotkey = if ($null -ne $rhythm) { "$($rhythm.captureHotkey)" } else { '' }
$minimumCaptureCount = if ($null -ne $rhythm) { [int]$rhythm.minimumCaptureCount } else { 0 }

$recommendedTask = 'REFRESH_STATUS_EVIDENCE'
$recommendedCommand = 'Tools\RunStaticPreflight.ps1'
$reason = 'Status evidence is missing or stale enough that the next step should refresh static summaries first.'
$canTuneRhythm = $false
$humanRequired = $false
$forbiddenAutomationActions = @()
$automationMode = 'REFRESH_STATUS'
$humanActionSummary = 'Refresh status evidence before choosing gameplay or tuning work.'

if ($preflightFailCount -gt 0) {
    $recommendedTask = 'FIX_STATIC_PREFLIGHT_FAILURES'
    $recommendedCommand = 'Tools\RunStaticPreflight.ps1'
    $reason = 'Static preflight has FAIL results; fix those before feature or tuning work.'
    $automationMode = 'FIX_FAILURES_ONLY'
    $humanActionSummary = 'No human play capture is needed; fix static preflight FAIL results first.'
    $forbiddenAutomationActions = @(
        'Do not tune rhythm feel while static preflight has FAIL results.',
        'Do not claim release readiness until FAIL results are fixed.'
    )
} elseif ($requiresHumanCapture -and -not $automationCanProceed) {
    $recommendedTask = 'IMPROVE_GUARDRAILS_OR_DOCUMENTATION'
    $recommendedCommand = 'Tools\Get-AutonomousHeartbeatStatus.cmd'
    $reason = "Rhythm tuning is blocked by $($rhythm.blockedReason); choose only non-tuning work until snapshots exist."
    $humanRequired = $true
    $automationMode = 'SAFE_ALTERNATE_ONLY'
    $humanActionSummary = "Capture $minimumCaptureCount rhythm snapshots with ${captureHotkey}: $($targetPhases -join ', ')."
    $forbiddenAutomationActions = @(
        'Do not retune rhythm feel without Calm, Build, Spike, and Release snapshots.',
        'Do not claim Spike fairness or Release relief is proven from missing evidence.',
        'Do not run broad manual Play Mode validation as an automation substitute.'
    )
} elseif ($automationCanProceed -and $null -ne $rhythm) {
    $recommendedTask = $rhythm.nextAction
    $recommendedCommand = 'Tools\Write-RhythmNextAction.cmd'
    $reason = 'Rhythm evidence allows automation to proceed with the reported next action.'
    $canTuneRhythm = $rhythm.nextAction -eq 'TUNE_WEAK_PHASES'
    $automationMode = $(if ($canTuneRhythm) { 'RHYTHM_TUNING_ALLOWED' } else { 'RHYTHM_AUTOMATION_ALLOWED' })
    $humanActionSummary = 'No human capture is required by the current safe-task state.'
}

Write-Host 'LostBreadcrumbs Autonomous Safe Task'
Write-Host "RhythmNextActionExists: $($null -ne $rhythm)"
Write-Host "StaticPreflightExists: $($null -ne $preflight)"
Write-Host "StaticPreflightFailCount: $preflightFailCount"
Write-Host "StaticPreflightWarnCount: $preflightWarnCount"
Write-Host "StaticPreflightHasWarnings: $preflightHasWarnings"
Write-Host "StaticPreflightWarningNames: $($preflightWarningNames -join ', ')"
Write-Host "StaticPreflightWarningSummary: $preflightWarningSummary"
Write-Host "RequiresHumanCapture: $requiresHumanCapture"
Write-Host "AutomationCanProceed: $automationCanProceed"
Write-Host "CanTuneRhythm: $canTuneRhythm"
Write-Host "HumanRequired: $humanRequired"
Write-Host "AutomationMode: $automationMode"
Write-Host "BlockedReason: $(if ([string]::IsNullOrWhiteSpace($blockedReason)) { 'none' } else { $blockedReason })"
Write-Host "ResumeCondition: $resumeCondition"
Write-Host "TargetPhases: $($targetPhases -join ', ')"
Write-Host "CaptureHotkey: $captureHotkey"
Write-Host "MinimumCaptureCount: $minimumCaptureCount"
Write-Host "HumanActionSummary: $humanActionSummary"
Write-Host "RecommendedTask: $recommendedTask"
Write-Host "RecommendedCommand: $recommendedCommand"
Write-Host "Reason: $reason"
Write-Host "ForbiddenAutomationActions: $(if ($forbiddenAutomationActions.Count -gt 0) { $forbiddenAutomationActions -join ' | ' } else { 'none' })"

if ($safeActions.Count -gt 0) {
    Write-Host "SafeAlternateAutomationActions: $($safeActions -join ' | ')"
} else {
    Write-Host 'SafeAlternateAutomationActions: none'
}

if (-not [string]::IsNullOrWhiteSpace($OutputJsonPath)) {
    $directory = Split-Path -Parent $OutputJsonPath
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    [ordered]@{
        schemaVersion = 1
        rhythmNextActionJsonPath = $RhythmNextActionJsonPath
        staticPreflightJsonPath = $StaticPreflightJsonPath
        rhythmNextActionExists = $null -ne $rhythm
        staticPreflightExists = $null -ne $preflight
        staticPreflightFailCount = $preflightFailCount
        staticPreflightWarnCount = $preflightWarnCount
        staticPreflightHasWarnings = $preflightHasWarnings
        staticPreflightWarningNames = $preflightWarningNames
        staticPreflightWarningSummary = $preflightWarningSummary
        requiresHumanCapture = $requiresHumanCapture
        automationCanProceed = $automationCanProceed
        canTuneRhythm = $canTuneRhythm
        humanRequired = $humanRequired
        automationMode = $automationMode
        blockedReason = $blockedReason
        resumeCondition = $resumeCondition
        targetPhases = $targetPhases
        captureHotkey = $captureHotkey
        minimumCaptureCount = $minimumCaptureCount
        humanActionSummary = $humanActionSummary
        recommendedTask = $recommendedTask
        recommendedCommand = $recommendedCommand
        reason = $reason
        safeAlternateAutomationActions = $safeActions
        forbiddenAutomationActions = $forbiddenAutomationActions
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $OutputJsonPath -Encoding UTF8
    Write-Host "OutputJsonPath: $OutputJsonPath"
}
