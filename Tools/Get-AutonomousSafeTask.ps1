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
$safeActions = if ($null -ne $rhythm) { @($rhythm.safeAlternateAutomationActions) } else { @() }
$requiresHumanCapture = $null -ne $rhythm -and [bool]$rhythm.requiresHumanCapture
$automationCanProceed = $null -ne $rhythm -and [bool]$rhythm.automationCanProceed

$recommendedTask = 'REFRESH_STATUS_EVIDENCE'
$recommendedCommand = 'Tools\RunStaticPreflight.ps1'
$reason = 'Status evidence is missing or stale enough that the next step should refresh static summaries first.'
$canTuneRhythm = $false
$humanRequired = $false
$forbiddenAutomationActions = @()

if ($preflightFailCount -gt 0) {
    $recommendedTask = 'FIX_STATIC_PREFLIGHT_FAILURES'
    $recommendedCommand = 'Tools\RunStaticPreflight.ps1'
    $reason = 'Static preflight has FAIL results; fix those before feature or tuning work.'
    $forbiddenAutomationActions = @(
        'Do not tune rhythm feel while static preflight has FAIL results.',
        'Do not claim release readiness until FAIL results are fixed.'
    )
} elseif ($requiresHumanCapture -and -not $automationCanProceed) {
    $recommendedTask = 'IMPROVE_GUARDRAILS_OR_DOCUMENTATION'
    $recommendedCommand = 'Tools\Get-AutonomousHeartbeatStatus.cmd'
    $reason = "Rhythm tuning is blocked by $($rhythm.blockedReason); choose only non-tuning work until snapshots exist."
    $humanRequired = $true
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
}

Write-Host 'LostBreadcrumbs Autonomous Safe Task'
Write-Host "RhythmNextActionExists: $($null -ne $rhythm)"
Write-Host "StaticPreflightExists: $($null -ne $preflight)"
Write-Host "StaticPreflightFailCount: $preflightFailCount"
Write-Host "RequiresHumanCapture: $requiresHumanCapture"
Write-Host "AutomationCanProceed: $automationCanProceed"
Write-Host "CanTuneRhythm: $canTuneRhythm"
Write-Host "HumanRequired: $humanRequired"
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
        requiresHumanCapture = $requiresHumanCapture
        automationCanProceed = $automationCanProceed
        canTuneRhythm = $canTuneRhythm
        humanRequired = $humanRequired
        recommendedTask = $recommendedTask
        recommendedCommand = $recommendedCommand
        reason = $reason
        safeAlternateAutomationActions = $safeActions
        forbiddenAutomationActions = $forbiddenAutomationActions
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $OutputJsonPath -Encoding UTF8
    Write-Host "OutputJsonPath: $OutputJsonPath"
}
