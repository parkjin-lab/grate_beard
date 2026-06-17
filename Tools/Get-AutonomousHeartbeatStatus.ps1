param(
    [string]$RhythmNextActionJsonPath = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..')).Path 'Logs/RhythmValidation/rhythm_next_action_last.json'),
    [string]$StaticPreflightJsonPath = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..')).Path 'Logs/ReleaseSoak/local_static_preflight_last_summary.json')
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
$safeActions = if ($null -ne $rhythm) { @($rhythm.safeAlternateAutomationActions) } else { @() }
$preflightWarningNames = if ($null -ne $preflight -and $null -ne $preflight.results) {
    @($preflight.results | Where-Object { "$($_.status)" -eq 'WARN' } | ForEach-Object { "$($_.name)" })
} else {
    @()
}
$preflightHasWarnings = if ($null -ne $preflight) {
    if ($null -ne $preflight.hasWarnings) {
        [bool]$preflight.hasWarnings
    } else {
        [int]$preflight.summary.warn -gt 0
    }
} else {
    $false
}
$preflightWarningSummary = if ($preflightWarningNames.Count -gt 0) {
    $preflightWarningNames -join ', '
} elseif ($preflightHasWarnings) {
    'warnings present'
} else {
    'none'
}

Write-Host 'LostBreadcrumbs Autonomous Heartbeat Status'
Write-Host "RhythmNextActionJsonPath: $RhythmNextActionJsonPath"
Write-Host "StaticPreflightJsonPath: $StaticPreflightJsonPath"
Write-Host "RhythmNextActionExists: $($null -ne $rhythm)"
Write-Host "StaticPreflightExists: $($null -ne $preflight)"

if ($null -ne $rhythm) {
    Write-Host "RhythmNextAction: $($rhythm.nextAction)"
    Write-Host "OverallEvidenceStatus: $($rhythm.overallEvidenceStatus)"
    Write-Host "RequiresHumanCapture: $($rhythm.requiresHumanCapture)"
    Write-Host "AutomationCanProceed: $($rhythm.automationCanProceed)"
    Write-Host "BlockedReason: $(if ([string]::IsNullOrWhiteSpace($rhythm.blockedReason)) { 'none' } else { $rhythm.blockedReason })"
    Write-Host "ResumeCondition: $($rhythm.resumeCondition)"
    Write-Host "MinimumCaptureCount: $($rhythm.minimumCaptureCount)"
    Write-Host "HumanCaptureStepCount: $(@($rhythm.humanCaptureSteps).Count)"
} else {
    Write-Host 'RhythmNextAction: missing; run Tools\Write-RhythmNextAction.cmd.'
}

if ($null -ne $preflight) {
    Write-Host "StaticPreflight: pass=$($preflight.summary.pass) warn=$($preflight.summary.warn) fail=$($preflight.summary.fail)"
    Write-Host "StaticPreflightHasWarnings: $preflightHasWarnings"
    Write-Host "StaticPreflightWarningNames: $($preflightWarningNames -join ', ')"
    Write-Host "StaticPreflightWarningSummary: $preflightWarningSummary"
    Write-Host "StaticPreflightDurationMilliseconds: $($preflight.durationMilliseconds)"
    Write-Host "StaticPreflightGeneratedAt: $($preflight.generatedAt)"
} else {
    Write-Host 'StaticPreflight: missing; run Tools\RunStaticPreflight.ps1.'
}

if ($safeActions.Count -gt 0) {
    Write-Host "SafeAlternateAutomationActions: $($safeActions -join ' | ')"
} else {
    Write-Host 'SafeAlternateAutomationActions: none'
}
