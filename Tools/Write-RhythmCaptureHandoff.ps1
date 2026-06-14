param(
    [string]$NextActionJsonPath = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..')).Path 'Logs/RhythmValidation/rhythm_next_action_last.json'),
    [string]$OutputPath = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..')).Path 'Logs/RhythmValidation/rhythm_capture_handoff_last.md')
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $NextActionJsonPath)) {
    throw "Rhythm next-action JSON is missing: $NextActionJsonPath. Run Tools\Write-RhythmNextAction.cmd first."
}

$nextAction = Get-Content -LiteralPath $NextActionJsonPath -Raw | ConvertFrom-Json
$steps = @($nextAction.humanCaptureSteps)
$targetPhases = @($nextAction.targetPhases)

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('# Rhythm Capture Handoff')
$lines.Add('')
$lines.Add("- GeneratedAt: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$lines.Add("- NextAction: $($nextAction.nextAction)")
$lines.Add("- OverallEvidenceStatus: $($nextAction.overallEvidenceStatus)")
$lines.Add("- RequiresHumanCapture: $($nextAction.requiresHumanCapture)")
$lines.Add("- AutomationCanProceed: $($nextAction.automationCanProceed)")
$lines.Add("- CaptureHotkey: $($nextAction.captureHotkey)")
$lines.Add("- MinimumCaptureCount: $($nextAction.minimumCaptureCount)")
$lines.Add("- TargetPhases: $($targetPhases -join ', ')")
$lines.Add("- BlockedReason: $($nextAction.blockedReason)")
$lines.Add("- ResumeCondition: $($nextAction.resumeCondition)")
$lines.Add('')
$lines.Add('## Tiny Capture Pass')
if ($steps.Count -eq 0) {
    $lines.Add('- No human capture steps are required for the current next-action state.')
} else {
    for ($i = 0; $i -lt $steps.Count; $i++) {
        $lines.Add(("{0}. {1}" -f ($i + 1), $steps[$i]))
    }
}
$lines.Add('')
$lines.Add('## Rationale')
$lines.Add($nextAction.rationale)
$lines.Add('')
$lines.Add('## Suggested Command')
$lines.Add('```text')
$lines.Add($nextAction.suggestedCommand)
$lines.Add('```')

$directory = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($directory)) {
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
}

$lines | Set-Content -LiteralPath $OutputPath -Encoding UTF8
Write-Host "Rhythm capture handoff written: $OutputPath"
