param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'
$preflightStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$durationWarningThresholdMilliseconds = 60000

function Add-Result {
    param(
        [string]$Name,
        [string]$Status,
        [string]$Detail
    )

    [pscustomobject]@{
        Name = $Name
        Status = $Status
        Detail = $Detail
    }
}

function Get-RelativePath {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return ''
    }

    $root = [System.IO.Path]::GetFullPath($ProjectRoot).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    $full = [System.IO.Path]::GetFullPath((Join-Path $ProjectRoot $Path))
    return $full.Substring($root.Length).Replace('\', '/')
}

function Add-LogArtifactResult {
    param(
        [string]$Name,
        [string]$Path,
        [int]$FreshnessDays = 7
    )

    if (-not (Test-Path $Path)) {
        return Add-Result $Name 'WARN' 'exists=False'
    }

    $item = Get-Item $Path
    $age = (Get-Date) - $item.LastWriteTime
    $ageDays = [math]::Round($age.TotalDays, 1)
    $isStale = $age.TotalDays -gt $FreshnessDays
    $status = $(if ($isStale) { 'WARN' } else { 'PASS' })
    $lastWrite = $item.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
    return Add-Result $Name $status "exists=True lastWrite=$lastWrite ageDays=$ageDays stale=$isStale freshnessDays=$FreshnessDays refreshRequired=$isStale"
}

$results = New-Object System.Collections.Generic.List[object]

$scenePath = Join-Path $ProjectRoot 'Assets/Scenes/SampleScene.unity'
$sceneMetaPath = "$scenePath.meta"
$buildSettingsPath = Join-Path $ProjectRoot 'ProjectSettings/EditorBuildSettings.asset'
$gitignorePath = Join-Path $ProjectRoot '.gitignore'
$setupPath = Join-Path $ProjectRoot 'Assets/_Project/Scripts/Editor/LostBreadcrumbsProjectSetup.cs'
$regressionPath = Join-Path $ProjectRoot 'Assets/_Project/Scripts/Managers/RegressionChecklistRunner.cs'
$debugOverlayPath = Join-Path $ProjectRoot 'Assets/_Project/Scripts/UI/DebugOverlay.cs'
$audioManagerPath = Join-Path $ProjectRoot 'Assets/_Project/Scripts/Managers/AudioManager.cs'
$playerControllerPath = Join-Path $ProjectRoot 'Assets/_Project/Scripts/Player/PlayerDummyController.cs'
$mapSystemPath = Join-Path $ProjectRoot 'Assets/_Project/Scripts/Map/MapSystem.cs'
$enemySpawnDirectorPath = Join-Path $ProjectRoot 'Assets/_Project/Scripts/Map/EnemySpawnDirector.cs'
$stageLoopPath = Join-Path $ProjectRoot 'Assets/_Project/Scripts/Map/StageLoopDirector.cs'
$riskCachePickupPath = Join-Path $ProjectRoot 'Assets/_Project/Scripts/Map/RiskCachePickup.cs'
$gameplayRhythmPath = Join-Path $ProjectRoot 'Assets/_Project/Scripts/Managers/GameplayRhythmDirector.cs'
$stagePressurePath = Join-Path $ProjectRoot 'Assets/_Project/Scripts/Managers/StagePressureDirector.cs'
$threatReadabilityPath = Join-Path $ProjectRoot 'Assets/_Project/Scripts/Managers/ThreatReadabilityDirector.cs'
$docsRoot = Join-Path $ProjectRoot 'Assets/_Project/Docs'
$rhythmPlaybookPath = Join-Path $docsRoot 'RHYTHM_VALIDATION_PLAYBOOK_2026-05-26.md'
$handoffPattern = Join-Path $ProjectRoot 'HANDOFF_*.md'
$releaseSoakDir = Join-Path $ProjectRoot 'Logs/ReleaseSoak'
$summaryPath = Join-Path $releaseSoakDir 'local_static_preflight_last_summary.txt'
$jsonSummaryPath = Join-Path $releaseSoakDir 'local_static_preflight_last_summary.json'
$unityPreflightSummaryPath = Join-Path $releaseSoakDir 'auto_soak_preflight_last_summary.txt'
$tracePath = Join-Path $releaseSoakDir 'auto_soak_flow_trace.log'
$statusPath = Join-Path $releaseSoakDir 'auto_soak_flow_last_status.txt'

$coreTypes = @(
    'LostBreadcrumbs.Runtime.Systems.SpawnSystem',
    'LostBreadcrumbs.Runtime.Managers.ProximityManager',
    'LostBreadcrumbs.Runtime.Managers.GameManager',
    'LostBreadcrumbs.Runtime.Systems.LearningSystem',
    'LostBreadcrumbs.Runtime.Systems.UIFlowSystem',
    'LostBreadcrumbs.Runtime.Systems.EchoSystem'
)

if (-not (Test-Path $scenePath)) {
    $results.Add((Add-Result 'scene.exists' 'FAIL' 'Assets/Scenes/SampleScene.unity is missing.'))
} else {
    $sceneText = Get-Content $scenePath -Raw
    $sceneLines = $sceneText -split "`n"
    $guidless = ($sceneLines | Where-Object {
        $_.IndexOf('m_Script: {fileID:', [System.StringComparison]::Ordinal) -ge 0 -and
        $_.IndexOf('guid:', [System.StringComparison]::Ordinal) -lt 0
    }).Count

    $duplicateCore = 0
    foreach ($type in $coreTypes) {
        $token = "m_EditorClassIdentifier: Assembly-CSharp::$type"
        $count = 0
        $index = 0
        while (($found = $sceneText.IndexOf($token, $index, [System.StringComparison]::Ordinal)) -ge 0) {
            $count++
            $index = $found + $token.Length
        }

        if ($count -gt 1) {
            $duplicateCore += ($count - 1)
        }
    }

    $results.Add((Add-Result 'scene.guidlessScripts' ($(if ($guidless -eq 0) { 'PASS' } else { 'FAIL' })) "guidless=$guidless"))
    $results.Add((Add-Result 'scene.duplicateCoreComponents' ($(if ($duplicateCore -eq 0) { 'PASS' } else { 'FAIL' })) "duplicateCore=$duplicateCore"))
}

if (Test-Path $buildSettingsPath) {
    $buildSettingsText = Get-Content $buildSettingsPath -Raw
    $sampleSceneMatch = [regex]::Match(
        $buildSettingsText,
        '(?m)^\s*-\s+enabled:\s*(?<enabled>\d+)\s*\r?\n\s*path:\s*Assets/Scenes/SampleScene\.unity\s*\r?\n\s*guid:\s*(?<guid>[0-9a-fA-F]+)\s*$'
    )
    $hasSampleScene = $sampleSceneMatch.Success
    $sampleSceneEnabled = $hasSampleScene -and $sampleSceneMatch.Groups['enabled'].Value -eq '1'
    $buildSettingsGuid = $(if ($hasSampleScene) { $sampleSceneMatch.Groups['guid'].Value } else { '' })
    $sampleSceneMetaGuid = ''
    $sampleSceneMetaExists = Test-Path $sceneMetaPath
    if ($sampleSceneMetaExists) {
        $sampleSceneMetaText = Get-Content $sceneMetaPath -Raw
        $sampleSceneMetaMatch = [regex]::Match($sampleSceneMetaText, '(?m)^guid:\s*(?<guid>[0-9a-fA-F]+)\s*$')
        if ($sampleSceneMetaMatch.Success) {
            $sampleSceneMetaGuid = $sampleSceneMetaMatch.Groups['guid'].Value
        }
    }
    $sampleSceneGuidMatches = $hasSampleScene -and $sampleSceneMetaExists -and $buildSettingsGuid -eq $sampleSceneMetaGuid
    $buildSceneStatus = $(if ($hasSampleScene -and $sampleSceneEnabled -and $sampleSceneGuidMatches) { 'PASS' } else { 'FAIL' })
    $results.Add((Add-Result 'buildSettings.sampleSceneBinding' $buildSceneStatus "registered=$hasSampleScene enabled=$sampleSceneEnabled metaExists=$sampleSceneMetaExists guidMatches=$sampleSceneGuidMatches buildGuid=$buildSettingsGuid metaGuid=$sampleSceneMetaGuid"))

    $buildSceneMatches = [regex]::Matches(
        $buildSettingsText,
        '(?m)^\s*-\s+enabled:\s*(?<enabled>\d+)\s*\r?\n\s*path:\s*(?<path>.+?)\s*\r?\n\s*guid:\s*(?<guid>[0-9a-fA-F]+)\s*$'
    )
    $enabledBuildScenes = @($buildSceneMatches | Where-Object { $_.Groups['enabled'].Value -eq '1' })
    $buildSceneGuidless = 0
    $buildSceneDuplicateCore = 0
    $missingBuildScenes = 0

    foreach ($buildScene in $enabledBuildScenes) {
        $relativeScenePath = $buildScene.Groups['path'].Value.Trim()
        $absoluteScenePath = Join-Path $ProjectRoot $relativeScenePath
        if (-not (Test-Path $absoluteScenePath)) {
            $missingBuildScenes++
            continue
        }

        $buildSceneText = Get-Content $absoluteScenePath -Raw
        $buildSceneLines = $buildSceneText -split "`n"
        $buildSceneGuidless += ($buildSceneLines | Where-Object {
            $_.IndexOf('m_Script: {fileID:', [System.StringComparison]::Ordinal) -ge 0 -and
            $_.IndexOf('guid:', [System.StringComparison]::Ordinal) -lt 0
        }).Count

        foreach ($type in $coreTypes) {
            $token = "m_EditorClassIdentifier: Assembly-CSharp::$type"
            $count = 0
            $index = 0
            while (($found = $buildSceneText.IndexOf($token, $index, [System.StringComparison]::Ordinal)) -ge 0) {
                $count++
                $index = $found + $token.Length
            }

            if ($count -gt 1) {
                $buildSceneDuplicateCore += ($count - 1)
            }
        }
    }

    $buildSceneHygieneStatus = $(if ($enabledBuildScenes.Count -gt 0 -and $missingBuildScenes -eq 0 -and $buildSceneGuidless -eq 0 -and $buildSceneDuplicateCore -eq 0) { 'PASS' } else { 'FAIL' })
    $results.Add((Add-Result 'buildSettings.enabledSceneHygiene' $buildSceneHygieneStatus "enabledScenes=$($enabledBuildScenes.Count) missing=$missingBuildScenes guidless=$buildSceneGuidless duplicateCore=$buildSceneDuplicateCore"))
} else {
    $results.Add((Add-Result 'buildSettings.exists' 'FAIL' 'ProjectSettings/EditorBuildSettings.asset is missing.'))
}

$conflictExtensions = @(
    '.asmdef',
    '.asset',
    '.controller',
    '.cs',
    '.json',
    '.mat',
    '.md',
    '.meta',
    '.prefab',
    '.ps1',
    '.shader',
    '.unity'
)
$conflictRoots = @(
    (Join-Path $ProjectRoot 'Assets'),
    (Join-Path $ProjectRoot 'ProjectSettings'),
    (Join-Path $ProjectRoot 'Packages'),
    (Join-Path $ProjectRoot 'Tools')
) | Where-Object { Test-Path $_ }
$conflictExcludeRoots = @(
    (Join-Path $ProjectRoot 'Assets/Feel'),
    (Join-Path $ProjectRoot 'Assets/Layer Lab'),
    (Join-Path $ProjectRoot 'Assets/_Recovery')
) | Where-Object { Test-Path $_ } | ForEach-Object {
    [System.IO.Path]::GetFullPath($_).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
}
$conflictFiles = @()
foreach ($root in $conflictRoots) {
    $conflictFiles += Get-ChildItem -Path $root -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object {
            $fullName = [System.IO.Path]::GetFullPath($_.FullName)
            $isExcluded = $false
            foreach ($excludeRoot in $conflictExcludeRoots) {
                if ($fullName.StartsWith($excludeRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
                    $isExcluded = $true
                    break
                }
            }

            -not $isExcluded -and $conflictExtensions -contains $_.Extension.ToLowerInvariant()
        }
}
$conflictFiles += @(Get-ChildItem -Path $handoffPattern -File -ErrorAction SilentlyContinue)
$conflictFiles = @($conflictFiles | Sort-Object FullName -Unique)
$conflictHits = New-Object System.Collections.Generic.List[string]
if ($conflictFiles.Count -gt 0) {
    foreach ($conflictFile in $conflictFiles) {
        try {
            $reader = [System.IO.File]::OpenText($conflictFile.FullName)
            try {
                $lineNumber = 0
                while (($line = $reader.ReadLine()) -ne $null) {
                    $lineNumber++
                    if ($line.StartsWith('<<<<<<<') -or $line.StartsWith('=======') -or $line.StartsWith('>>>>>>>')) {
                        $conflictHits.Add(("{0}:{1}:{2}" -f $conflictFile.FullName, $lineNumber, $line))
                    }
                }
            } finally {
                $reader.Dispose()
            }
        } catch {
            Write-Warning "Skipped conflict marker scan for '$($conflictFile.FullName)': $($_.Exception.Message)"
        }
    }
}
$conflictStatus = $(if ($conflictHits.Count -eq 0) { 'PASS' } else { 'FAIL' })
$results.Add((Add-Result 'text.conflictMarkers' $conflictStatus "files=$($conflictFiles.Count) hits=$($conflictHits.Count)"))

if (Test-Path $setupPath) {
    $setupText = Get-Content $setupPath -Raw
    $setupHooks = @(
        'Run Auto Soak Preflight Only',
        'Log Build Scene Script Reference Hygiene',
        'BuildAutoSoakPreflightSummary',
        'HasAutoSoakPreflightWarnings',
        'auto_soak_preflight_last_summary'
    )
    $missingSetupHooks = @($setupHooks | Where-Object { -not $setupText.Contains($_) })
    $results.Add((Add-Result 'code.setupPreflightHooks' ($(if ($missingSetupHooks.Count -eq 0) { 'PASS' } else { 'FAIL' })) "missing=$($missingSetupHooks -join ', ')"))
} else {
    $results.Add((Add-Result 'code.setupPreflightHooks' 'FAIL' 'LostBreadcrumbsProjectSetup.cs is missing.'))
}

if (Test-Path $regressionPath) {
    $regressionText = Get-Content $regressionPath -Raw
    $regressionHooks = @(
        'Auto Preflight Trace',
        'auto_soak_preflight_last_summary',
        'auto_soak_flow_trace',
        'auto_soak_flow_last_status'
    )
    $missingRegressionHooks = @($regressionHooks | Where-Object { -not $regressionText.Contains($_) })
    $results.Add((Add-Result 'code.regressionReportHooks' ($(if ($missingRegressionHooks.Count -eq 0) { 'PASS' } else { 'FAIL' })) "missing=$($missingRegressionHooks -join ', ')"))
} else {
    $results.Add((Add-Result 'code.regressionReportHooks' 'FAIL' 'RegressionChecklistRunner.cs is missing.'))
}

$preflightScriptText = Get-Content $PSCommandPath -Raw
$machineSummaryHooks = @(
    'local_static_preflight_last_summary.txt',
    'local_static_preflight_last_summary.json',
    'ConvertTo-Json',
    'ConvertFrom-Json',
    'jsonSummary',
    'durationMilliseconds',
    'durationWarningThresholdMilliseconds',
    'durationWarning',
    'results',
    'summary'
)
$missingMachineSummaryHooks = @($machineSummaryHooks | Where-Object { -not $preflightScriptText.Contains($_) })
$results.Add((Add-Result 'code.preflightMachineSummaryHooks' ($(if ($missingMachineSummaryHooks.Count -eq 0) { 'PASS' } else { 'FAIL' })) "missing=$($missingMachineSummaryHooks -join ', ')"))

if (Test-Path $debugOverlayPath) {
    $debugOverlayText = Get-Content $debugOverlayPath -Raw
    $debugOverlayHooks = @(
        'writeRhythmSnapshotKey',
        'WriteRhythmValidationSnapshot',
        'BuildRhythmValidationSnapshotText',
        'GetMissingRhythmPhaseLabel',
        'MissingPhases'
    )
    $missingDebugOverlayHooks = @($debugOverlayHooks | Where-Object { -not $debugOverlayText.Contains($_) })
    $results.Add((Add-Result 'code.lowTouchRhythmValidationHooks' ($(if ($missingDebugOverlayHooks.Count -eq 0) { 'PASS' } else { 'FAIL' })) "missing=$($missingDebugOverlayHooks -join ', ')"))
} else {
    $results.Add((Add-Result 'code.lowTouchRhythmValidationHooks' 'FAIL' 'DebugOverlay.cs is missing.'))
}

if (Test-Path $audioManagerPath) {
    $audioManagerText = Get-Content $audioManagerPath -Raw
    $audioManagerHooks = @(
        'AssignedStingerClipCount',
        'exitUnlockedStingerClip',
        'chaseSpikeStingerClip',
        'lockOnWarningStingerClip',
        'escapeReliefStingerClip',
        'quietBreathBrokenStingerClip',
        'echoReturnStingerClip',
        'riskRewardStingerClip',
        'rhythmShiftStingerClip',
        'setPieceShiftStingerClip',
        'pressureWaveStingerClip',
        'deathStingerClip',
        'GetAssignedStingerClip',
        'IsAssignedStingerClip',
        'CountAssignedStingerClips',
        'DebugTestLockOnWarningStinger',
        'DebugTestEscapeReliefStinger',
        'DebugTestQuietBreathBrokenStinger',
        'DebugTestEchoReturnStinger',
        'DebugTestRiskRewardStinger',
        'DebugTestRhythmShiftStinger'
    )
    $missingAudioManagerHooks = @($audioManagerHooks | Where-Object { -not $audioManagerText.Contains($_) })
    $results.Add((Add-Result 'code.semanticStingerValidationHooks' ($(if ($missingAudioManagerHooks.Count -eq 0) { 'PASS' } else { 'FAIL' })) "missing=$($missingAudioManagerHooks -join ', ')"))
} else {
    $results.Add((Add-Result 'code.semanticStingerValidationHooks' 'FAIL' 'AudioManager.cs is missing.'))
}

$spawnSafetyMissing = New-Object System.Collections.Generic.List[string]
if (Test-Path $playerControllerPath) {
    $playerControllerText = Get-Content $playerControllerPath -Raw
    $playerSafetyHooks = @(
        'autoRecoverUnsafePosition',
        'ScheduleUnsafePositionRecoveryProbe',
        'TryRecoverUnsafePositionNowForRuntime',
        'TryRecoverUnsafePositionNow',
        'TryResolveSafePlayerPosition',
        'UnsafePositionRecoveryCount'
    )
    foreach ($hook in $playerSafetyHooks) {
        if (-not $playerControllerText.Contains($hook)) {
            $spawnSafetyMissing.Add("PlayerDummyController:$hook")
        }
    }
} else {
    $spawnSafetyMissing.Add('PlayerDummyController.cs missing')
}

if (Test-Path $mapSystemPath) {
    $mapSystemText = Get-Content $mapSystemPath -Raw
    $playerSpawnSafetyHooks = @(
        'playerSpawnGeneratedBlockersOnly;',
        'TryFindSafeGeneratedCellCenter',
        'TryResolveSafePlayerSpawnPosition',
        'IsPlayerSpawnBlocked',
        'LastPlayerSpawnUsedBlockedFallback',
        'loggedPlayerSpawnBlockerScopeGuard',
        'widened player spawn blocker checks to all blocking colliders'
    )
    foreach ($hook in $playerSpawnSafetyHooks) {
        if (-not $mapSystemText.Contains($hook)) {
            $spawnSafetyMissing.Add("MapSystem:$hook")
        }
    }
} else {
    $spawnSafetyMissing.Add('MapSystem.cs missing')
}

if (Test-Path $enemySpawnDirectorPath) {
    $enemySpawnText = Get-Content $enemySpawnDirectorPath -Raw
    $enemySpawnSafetyHooks = @(
        'avoidNarrowSpawnCells',
        'PreferOpenSpawnCandidates',
        'CountSpawnCandidateSafety',
        'LastSelectedNarrowSpawnCount',
        'LastNarrowSpawnsWereFallbackOnly',
        'spawnStabilizationSeconds'
    )
    foreach ($hook in $enemySpawnSafetyHooks) {
        if (-not $enemySpawnText.Contains($hook)) {
            $spawnSafetyMissing.Add("EnemySpawnDirector:$hook")
        }
    }
} else {
    $spawnSafetyMissing.Add('EnemySpawnDirector.cs missing')
}

$results.Add((Add-Result 'code.spawnSafetyHooks' ($(if ($spawnSafetyMissing.Count -eq 0) { 'PASS' } else { 'FAIL' })) "missing=$($spawnSafetyMissing -join ', ')"))

$rhythmStateMissing = New-Object System.Collections.Generic.List[string]
if (Test-Path $gameplayRhythmPath) {
    $gameplayRhythmText = Get-Content $gameplayRhythmPath -Raw
    $rhythmHooks = @(
        'GameplayRhythmPhase.Calm => GameplayRhythmPhase.Build',
        'GameplayRhythmPhase.Build => GameplayRhythmPhase.Spike',
        'GameplayRhythmPhase.Spike => GameplayRhythmPhase.Release',
        'GameplayRhythmPhase.Release => GameplayRhythmPhase.Calm',
        'ForceSetPhaseForRuntime',
        'EnterPhase',
        'ApplyPressureRhythmForRuntime',
        'TryRaiseSpikeTell',
        'TryGrantRhythmReleaseRelief',
        'RegressionChecklistRunner.IsRegressionRunActive'
    )
    foreach ($hook in $rhythmHooks) {
        if (-not $gameplayRhythmText.Contains($hook)) {
            $rhythmStateMissing.Add("GameplayRhythmDirector:$hook")
        }
    }
} else {
    $rhythmStateMissing.Add('GameplayRhythmDirector.cs missing')
}

if (Test-Path $stagePressurePath) {
    $stagePressureText = Get-Content $stagePressurePath -Raw
    $pressureRhythmHooks = @(
        'applyRhythmPressureModulation',
        'ApplyPressureRhythmForRuntime',
        'RegressionChecklistRunner.IsRegressionRunActive'
    )
    foreach ($hook in $pressureRhythmHooks) {
        if (-not $stagePressureText.Contains($hook)) {
            $rhythmStateMissing.Add("StagePressureDirector:$hook")
        }
    }
} else {
    $rhythmStateMissing.Add('StagePressureDirector.cs missing')
}

$results.Add((Add-Result 'code.rhythmStateTransitionHooks' ($(if ($rhythmStateMissing.Count -eq 0) { 'PASS' } else { 'FAIL' })) "missing=$($rhythmStateMissing -join ', ')"))

$spikeFairnessMissing = New-Object System.Collections.Generic.List[string]
if (Test-Path $gameplayRhythmPath) {
    $gameplayRhythmText = Get-Content $gameplayRhythmPath -Raw
    $spikeTellHooks = @(
        'raiseSpikeTellEvent',
        'spikeTellLeadSeconds',
        'currentPhase != GameplayRhythmPhase.Build',
        'spikeTellRaisedThisBuild',
        'RuntimeEventSemantic.LockOnWarning',
        'Spike incoming'
    )
    foreach ($hook in $spikeTellHooks) {
        if (-not $gameplayRhythmText.Contains($hook)) {
            $spikeFairnessMissing.Add("GameplayRhythmDirector:$hook")
        }
    }
} else {
    $spikeFairnessMissing.Add('GameplayRhythmDirector.cs missing')
}

if (Test-Path $audioManagerPath) {
    $audioManagerText = Get-Content $audioManagerPath -Raw
    $spikeAudioBudgetHooks = @(
        'majorStingerBudgetCooldown',
        'IsMajorStingerBudgetBlocked',
        'IsMajorRuntimeStinger',
        'MarkMajorStingerBudget',
        'PushStingerDuckBoost',
        'RuntimeStingerKind.LockOnWarning',
        'RuntimeStingerKind.ChaseSpike',
        'nextMajorStingerTime'
    )
    foreach ($hook in $spikeAudioBudgetHooks) {
        if (-not $audioManagerText.Contains($hook)) {
            $spikeFairnessMissing.Add("AudioManager:$hook")
        }
    }
} else {
    $spikeFairnessMissing.Add('AudioManager.cs missing')
}
$results.Add((Add-Result 'code.spikeFairnessCueBudgetHooks' ($(if ($spikeFairnessMissing.Count -eq 0) { 'PASS' } else { 'FAIL' })) "missing=$($spikeFairnessMissing -join ', ')"))

$buildTemptationMissing = New-Object System.Collections.Generic.List[string]
if (Test-Path $riskCachePickupPath) {
    $riskCachePickupText = Get-Content $riskCachePickupPath -Raw
    $riskCacheTemptationHooks = @(
        'ConfigureRhythmWager',
        'buildRewardMultiplier',
        'spikeRewardMultiplier',
        'releaseRewardMultiplier',
        'buildNoiseMultiplier',
        'spikeNoiseMultiplier',
        'releaseNoiseMultiplier',
        'EvaluateRewardMultiplier',
        'EvaluateNoiseMultiplier',
        'LastRhythmPhaseLabel'
    )
    foreach ($hook in $riskCacheTemptationHooks) {
        if (-not $riskCachePickupText.Contains($hook)) {
            $buildTemptationMissing.Add("RiskCachePickup:$hook")
        }
    }
} else {
    $buildTemptationMissing.Add('RiskCachePickup.cs missing')
}

if (Test-Path $stageLoopPath) {
    $stageLoopText = Get-Content $stageLoopPath -Raw
    $stageLoopTemptationHooks = @(
        'Risk Cache Rhythm Wager',
        'riskCacheBuildRewardMultiplier',
        'riskCacheSpikeRewardMultiplier',
        'riskCacheBuildNoiseMultiplier',
        'riskCacheSpikeNoiseMultiplier',
        'ConfigureRhythmWager',
        'Breadcrumb Rhythm Momentum',
        'breadcrumbBuildRewardMultiplier',
        'breadcrumbSpikeRewardMultiplier',
        'EvaluateBreadcrumbMomentumRewardMultiplier',
        'ApplyBreadcrumbMomentumReward',
        'EmitBreadcrumbChainReaction'
    )
    foreach ($hook in $stageLoopTemptationHooks) {
        if (-not $stageLoopText.Contains($hook)) {
            $buildTemptationMissing.Add("StageLoopDirector:$hook")
        }
    }
} else {
    $buildTemptationMissing.Add('StageLoopDirector.cs missing')
}
$results.Add((Add-Result 'code.buildTemptationWagerHooks' ($(if ($buildTemptationMissing.Count -eq 0) { 'PASS' } else { 'FAIL' })) "missing=$($buildTemptationMissing -join ', ')"))

$releaseReliefMissing = New-Object System.Collections.Generic.List[string]
if (Test-Path $threatReadabilityPath) {
    $threatReadabilityText = Get-Content $threatReadabilityPath -Raw
    $releaseReliefHooks = @(
        'TryGrantRhythmReleaseRelief',
        'RecoverStamina',
        'ApplyEchoRevealPulse',
        'SpawnEscapeReliefPulse',
        'TrySpawnEscapeReliefObjectiveWhisper',
        'PlayEscapeReliefAudio',
        'StartEscapeReliefCalmWindow',
        'ApplyRhythmReleaseQuietBreath',
        'RuntimeEventSemantic.EscapeRelief'
    )
    foreach ($hook in $releaseReliefHooks) {
        if (-not $threatReadabilityText.Contains($hook)) {
            $releaseReliefMissing.Add("ThreatReadabilityDirector:$hook")
        }
    }
} else {
    $releaseReliefMissing.Add('ThreatReadabilityDirector.cs missing')
}
$results.Add((Add-Result 'code.releaseReliefContractHooks' ($(if ($releaseReliefMissing.Count -eq 0) { 'PASS' } else { 'FAIL' })) "missing=$($releaseReliefMissing -join ', ')"))

if (Test-Path $gitignorePath) {
    $gitignoreText = Get-Content $gitignorePath -Raw
    $vendorIgnoreHooks = @(
        '/[Aa]ssets/Feel/',
        '/[Aa]ssets/Feel.meta',
        '/[Aa]ssets/Layer Lab/',
        '/[Aa]ssets/Layer Lab.meta',
        '/[Aa]ssets/ThirdParty.meta'
    )
    $missingVendorIgnoreHooks = @($vendorIgnoreHooks | Where-Object { -not $gitignoreText.Contains($_) })
    $results.Add((Add-Result 'repo.vendorAssetIgnoreGuards' ($(if ($missingVendorIgnoreHooks.Count -eq 0) { 'PASS' } else { 'FAIL' })) "missing=$($missingVendorIgnoreHooks -join ', ')"))

    $validationArtifactIgnoreHooks = @(
        '/[Ll]ogs/',
        '*.log'
    )
    $missingValidationArtifactIgnoreHooks = @($validationArtifactIgnoreHooks | Where-Object { -not $gitignoreText.Contains($_) })
    $results.Add((Add-Result 'repo.validationArtifactIgnoreGuards' ($(if ($missingValidationArtifactIgnoreHooks.Count -eq 0) { 'PASS' } else { 'FAIL' })) "missing=$($missingValidationArtifactIgnoreHooks -join ', ')"))
} else {
    $results.Add((Add-Result 'repo.vendorAssetIgnoreGuards' 'FAIL' '.gitignore is missing.'))
    $results.Add((Add-Result 'repo.validationArtifactIgnoreGuards' 'FAIL' '.gitignore is missing.'))
}

$trackedVendorAssets = @()
try {
    $trackedVendorAssets = @(& git -C $ProjectRoot ls-files -- 'Assets/Feel' 'Assets/Layer Lab' 'Assets/ThirdParty.meta' 2>$null)
} catch {
    $trackedVendorAssets = @('__git_ls_files_failed__')
}
$trackedVendorAssetStatus = $(if ($trackedVendorAssets.Count -eq 0) { 'PASS' } else { 'FAIL' })
$trackedVendorAssetDetail = $(if ($trackedVendorAssets.Count -eq 0) { 'tracked=0' } else { "tracked=$($trackedVendorAssets.Count) sample=$((@($trackedVendorAssets | Select-Object -First 5)) -join ', ')" })
$results.Add((Add-Result 'repo.vendorAssetTrackedGuards' $trackedVendorAssetStatus $trackedVendorAssetDetail))

$requiredDocPatterns = @(
    'AUTONOMOUS_NEXT_WORK_CHECKLIST_*.md',
    'RESOURCE_REQUIREMENTS_UPDATE_*.md',
    'RHYTHM_VALIDATION_PLAYBOOK_*.md',
    'VENDOR_ASSET_REVIEW_*.md',
    'GAME_GAP_ANALYSIS_AND_POLICY_*.md',
    'HANDOFF_AFTER_RHYTHM_WORK_*.md',
    'PROJECT_STATUS_UPDATE_*.md'
)
$missingDocPatterns = New-Object System.Collections.Generic.List[string]
$missingDocMetaPatterns = New-Object System.Collections.Generic.List[string]
if (Test-Path $docsRoot) {
    foreach ($pattern in $requiredDocPatterns) {
        $matches = @(Get-ChildItem -Path (Join-Path $docsRoot $pattern) -File -ErrorAction SilentlyContinue)
        if ($matches.Count -le 0) {
            $missingDocPatterns.Add($pattern)
        }

        foreach ($match in $matches) {
            $metaPath = "$($match.FullName).meta"
            if (-not (Test-Path $metaPath)) {
                $missingDocMetaPatterns.Add((Get-RelativePath $metaPath))
            }
        }
    }
} else {
    $missingDocPatterns.Add('Assets/_Project/Docs missing')
    $missingDocMetaPatterns.Add('Assets/_Project/Docs missing')
}

$results.Add((Add-Result 'docs.lowTouchPlanningArtifacts' ($(if ($missingDocPatterns.Count -eq 0) { 'PASS' } else { 'FAIL' })) "missing=$($missingDocPatterns -join ', ')"))
$results.Add((Add-Result 'docs.lowTouchPlanningArtifactMetas' ($(if ($missingDocMetaPatterns.Count -eq 0) { 'PASS' } else { 'FAIL' })) "missing=$($missingDocMetaPatterns -join ', ')"))

$lowTouchValidationPolicyMissing = New-Object System.Collections.Generic.List[string]
if (Test-Path $rhythmPlaybookPath) {
    $rhythmPlaybookText = Get-Content $rhythmPlaybookPath -Raw
    $lowTouchValidationPolicyHooks = @(
        'Minimal Human Pass',
        'Plain Judgment Card',
        'Spike가 무섭지만 억울하지 않은가?',
        'Release가 실제로 안도감인가?',
        'Build가 유혹인가?',
        'scary but fair',
        'felt relief',
        'tempted',
        'Use this only when automated/static checks are not enough',
        'Write Rhythm Snapshot',
        'Logs/RhythmValidation/',
        'Snapshot file:'
    )
    foreach ($hook in $lowTouchValidationPolicyHooks) {
        if (-not $rhythmPlaybookText.Contains($hook)) {
            $lowTouchValidationPolicyMissing.Add($hook)
        }
    }
} else {
    $lowTouchValidationPolicyMissing.Add('RHYTHM_VALIDATION_PLAYBOOK_2026-05-26.md missing')
}
$results.Add((Add-Result 'docs.lowTouchValidationPolicy' ($(if ($lowTouchValidationPolicyMissing.Count -eq 0) { 'PASS' } else { 'FAIL' })) "missing=$($lowTouchValidationPolicyMissing -join ', ')"))

$results.Add((Add-LogArtifactResult 'logs.unityPreflightSummary' $unityPreflightSummaryPath))
$results.Add((Add-LogArtifactResult 'logs.autoSoakTrace' $tracePath))
$results.Add((Add-LogArtifactResult 'logs.autoSoakStatus' $statusPath))

New-Item -ItemType Directory -Path $releaseSoakDir -Force | Out-Null

$failCount = @($results | Where-Object { $_.Status -eq 'FAIL' }).Count
$warnCount = @($results | Where-Object { $_.Status -eq 'WARN' }).Count
$passCount = @($results | Where-Object { $_.Status -eq 'PASS' }).Count
$hasFailures = $failCount -gt 0
$hasWarnings = $warnCount -gt 0
$exitCode = $(if ($hasFailures) { 1 } else { 0 })
$generatedAt = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss 'KST'")
$durationMilliseconds = [int64]$preflightStopwatch.ElapsedMilliseconds
$durationWarning = $durationMilliseconds -gt $durationWarningThresholdMilliseconds
$gitContext = [ordered]@{
    available = $false
    branch = ''
    commit = ''
    shortCommit = ''
    dirty = $false
    statusShortLineCount = 0
}
try {
    $gitBranch = (& git -C $ProjectRoot rev-parse --abbrev-ref HEAD 2>$null)
    $gitCommit = (& git -C $ProjectRoot rev-parse HEAD 2>$null)
    $gitStatusShort = @(& git -C $ProjectRoot status --short 2>$null)
    $gitContext.available = $true
    $gitContext.branch = "$gitBranch"
    $gitContext.commit = "$gitCommit"
    $gitContext.shortCommit = $(if ($gitContext.commit.Length -ge 7) { $gitContext.commit.Substring(0, 7) } else { $gitContext.commit })
    $gitContext.dirty = $gitStatusShort.Count -gt 0
    $gitContext.statusShortLineCount = [int]$gitStatusShort.Count
} catch {
    $gitContext.available = $false
}

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('LostBreadcrumbs Local Static Preflight')
$lines.Add("GeneratedAt: $generatedAt")
$lines.Add("ProjectRoot: $ProjectRoot")
$lines.Add("DurationMilliseconds: $durationMilliseconds")
$lines.Add("DurationWarningThresholdMilliseconds: $durationWarningThresholdMilliseconds")
$lines.Add("DurationWarning: $durationWarning")
$lines.Add("Summary: pass=$passCount warn=$warnCount fail=$failCount")
$lines.Add('')
foreach ($result in $results) {
    $lines.Add("[$($result.Status)] $($result.Name): $($result.Detail)")
}

Set-Content -Path $summaryPath -Value $lines -Encoding UTF8
$jsonResults = @()
foreach ($result in $results) {
    $jsonResults += [ordered]@{
        name = $result.Name
        status = $result.Status
        detail = $result.Detail
    }
}
$jsonSummary = [ordered]@{
    schemaVersion = 1
    generatedAt = $generatedAt
    projectRoot = $ProjectRoot
    durationMilliseconds = [int64]$durationMilliseconds
    durationWarningThresholdMilliseconds = [int64]$durationWarningThresholdMilliseconds
    durationWarning = [bool]$durationWarning
    git = $gitContext
    exitCode = [int]$exitCode
    hasFailures = [bool]$hasFailures
    hasWarnings = [bool]$hasWarnings
    summary = [ordered]@{
        pass = [int]$passCount
        warn = [int]$warnCount
        fail = [int]$failCount
    }
    results = $jsonResults
}
$jsonSummary | ConvertTo-Json -Depth 5 | Set-Content -Path $jsonSummaryPath -Encoding UTF8
$jsonSummaryReadback = Get-Content -Path $jsonSummaryPath -Raw | ConvertFrom-Json
if ($null -eq $jsonSummaryReadback -or
    $jsonSummaryReadback.schemaVersion -ne 1 -or
    $jsonSummaryReadback.exitCode -ne $exitCode -or
    $jsonSummaryReadback.hasFailures -ne $hasFailures -or
    $jsonSummaryReadback.hasWarnings -ne $hasWarnings -or
    $jsonSummaryReadback.durationMilliseconds -ne $durationMilliseconds -or
    $jsonSummaryReadback.durationWarningThresholdMilliseconds -ne $durationWarningThresholdMilliseconds -or
    $jsonSummaryReadback.durationWarning -ne $durationWarning -or
    $jsonSummaryReadback.git.available -ne $gitContext.available -or
    "$($jsonSummaryReadback.git.branch)" -ne "$($gitContext.branch)" -or
    "$($jsonSummaryReadback.git.shortCommit)" -ne "$($gitContext.shortCommit)" -or
    $jsonSummaryReadback.git.dirty -ne $gitContext.dirty -or
    $jsonSummaryReadback.summary.pass -ne $passCount -or
    $jsonSummaryReadback.summary.warn -ne $warnCount -or
    $jsonSummaryReadback.summary.fail -ne $failCount -or
    $jsonSummaryReadback.results.Count -ne $results.Count) {
    throw "Static preflight JSON summary readback failed: $jsonSummaryPath"
}
$lines | ForEach-Object { Write-Output $_ }

exit $exitCode
