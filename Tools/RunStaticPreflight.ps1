param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'

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
    return Add-Result $Name $status "exists=True lastWrite=$lastWrite ageDays=$ageDays stale=$isStale"
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
$enemySpawnDirectorPath = Join-Path $ProjectRoot 'Assets/_Project/Scripts/Map/EnemySpawnDirector.cs'
$gameplayRhythmPath = Join-Path $ProjectRoot 'Assets/_Project/Scripts/Managers/GameplayRhythmDirector.cs'
$stagePressurePath = Join-Path $ProjectRoot 'Assets/_Project/Scripts/Managers/StagePressureDirector.cs'
$docsRoot = Join-Path $ProjectRoot 'Assets/_Project/Docs'
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
$conflictFiles = @()
foreach ($root in $conflictRoots) {
    $conflictFiles += Get-ChildItem -Path $root -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $conflictExtensions -contains $_.Extension.ToLowerInvariant() }
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

$requiredDocPatterns = @(
    'AUTONOMOUS_NEXT_WORK_CHECKLIST_*.md',
    'RESOURCE_REQUIREMENTS_UPDATE_*.md',
    'RHYTHM_VALIDATION_PLAYBOOK_*.md',
    'VENDOR_ASSET_REVIEW_*.md',
    'GAME_GAP_ANALYSIS_AND_POLICY_*.md'
)
$missingDocPatterns = New-Object System.Collections.Generic.List[string]
if (Test-Path $docsRoot) {
    foreach ($pattern in $requiredDocPatterns) {
        $matches = @(Get-ChildItem -Path (Join-Path $docsRoot $pattern) -File -ErrorAction SilentlyContinue)
        if ($matches.Count -le 0) {
            $missingDocPatterns.Add($pattern)
        }
    }
} else {
    $missingDocPatterns.Add('Assets/_Project/Docs missing')
}

$results.Add((Add-Result 'docs.lowTouchPlanningArtifacts' ($(if ($missingDocPatterns.Count -eq 0) { 'PASS' } else { 'FAIL' })) "missing=$($missingDocPatterns -join ', ')"))

$results.Add((Add-LogArtifactResult 'logs.unityPreflightSummary' $unityPreflightSummaryPath))
$results.Add((Add-LogArtifactResult 'logs.autoSoakTrace' $tracePath))
$results.Add((Add-LogArtifactResult 'logs.autoSoakStatus' $statusPath))

New-Item -ItemType Directory -Path $releaseSoakDir -Force | Out-Null

$failCount = @($results | Where-Object { $_.Status -eq 'FAIL' }).Count
$warnCount = @($results | Where-Object { $_.Status -eq 'WARN' }).Count
$passCount = @($results | Where-Object { $_.Status -eq 'PASS' }).Count
$generatedAt = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss 'KST'")

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('LostBreadcrumbs Local Static Preflight')
$lines.Add("GeneratedAt: $generatedAt")
$lines.Add("ProjectRoot: $ProjectRoot")
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
    generatedAt = $generatedAt
    projectRoot = $ProjectRoot
    summary = [ordered]@{
        pass = [int]$passCount
        warn = [int]$warnCount
        fail = [int]$failCount
    }
    results = $jsonResults
}
$jsonSummary | ConvertTo-Json -Depth 5 | Set-Content -Path $jsonSummaryPath -Encoding UTF8
$lines | ForEach-Object { Write-Output $_ }

if ($failCount -gt 0) {
    exit 1
}

exit 0
