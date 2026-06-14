param(
    [string]$RhythmNextActionJsonPath = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..')).Path 'Logs/RhythmValidation/rhythm_next_action_last.json'),
    [string]$StaticPreflightJsonPath = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..')).Path 'Logs/ReleaseSoak/local_static_preflight_last_summary.json'),
    [string]$OutputPath = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..')).Path 'Logs/Autonomous/autonomous_heartbeat_status_last.md')
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
$humanSteps = if ($null -ne $rhythm) { @($rhythm.humanCaptureSteps) } else { @() }

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('# Autonomous Heartbeat Status')
$lines.Add('')
$lines.Add("- GeneratedAt: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")

$lines.Add('')
$lines.Add('## Progress')
$lines.Add('- Core rhythm systems, low-touch validation snapshots, next-action JSON, capture handoff, and branch tests are in place.')
if ($null -ne $rhythm) {
    $lines.Add("- RhythmNextAction: $($rhythm.nextAction)")
    $lines.Add("- OverallEvidenceStatus: $($rhythm.overallEvidenceStatus)")
    $lines.Add("- AutomationCanProceed: $($rhythm.automationCanProceed)")
}

$lines.Add('')
$lines.Add('## Validation')
if ($null -ne $preflight) {
    $lines.Add("- StaticPreflight: pass=$($preflight.summary.pass) warn=$($preflight.summary.warn) fail=$($preflight.summary.fail)")
    $lines.Add("- DurationMilliseconds: $($preflight.durationMilliseconds)")
} else {
    $lines.Add('- StaticPreflight: missing JSON summary; run Tools\RunStaticPreflight.ps1.')
}

$lines.Add('')
$lines.Add('## Blocked State')
if ($null -ne $rhythm -and [bool]$rhythm.requiresHumanCapture) {
    $lines.Add("- BlockedReason: $($rhythm.blockedReason)")
    $lines.Add("- ResumeCondition: $($rhythm.resumeCondition)")
    $lines.Add("- MinimumCaptureCount: $($rhythm.minimumCaptureCount)")
} else {
    $lines.Add('- Rhythm automation is not capture-blocked.')
}

$lines.Add('')
$lines.Add('## Human Capture Steps')
if ($humanSteps.Count -eq 0) {
    $lines.Add('- None required by the current next-action state.')
} else {
    for ($i = 0; $i -lt $humanSteps.Count; $i++) {
        $lines.Add(("{0}. {1}" -f ($i + 1), $humanSteps[$i]))
    }
}

$lines.Add('')
$lines.Add('## Safe Alternate Automation')
if ($safeActions.Count -eq 0) {
    $lines.Add('- No safe alternate actions are listed.')
} else {
    foreach ($action in $safeActions) {
        $lines.Add("- $action")
    }
}

$directory = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($directory)) {
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
}

$lines | Set-Content -LiteralPath $OutputPath -Encoding UTF8
Write-Host "Autonomous heartbeat status written: $OutputPath"
