using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using LostBreadcrumbs.Runtime.AI;
using LostBreadcrumbs.Runtime.AI.Learning;
using LostBreadcrumbs.Runtime.Authoring;
using LostBreadcrumbs.Runtime.Core;
using LostBreadcrumbs.Runtime.Managers;
using LostBreadcrumbs.Runtime.Map;
using LostBreadcrumbs.Runtime.Player;
using LostBreadcrumbs.Runtime.Systems;
using LostBreadcrumbs.Runtime.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;
using AnimatorController = UnityEditor.Animations.AnimatorController;
using AnimatorControllerLayer = UnityEditor.Animations.AnimatorControllerLayer;
using AnimatorStateMachine = UnityEditor.Animations.AnimatorStateMachine;
using ChildAnimatorStateMachine = UnityEditor.Animations.ChildAnimatorStateMachine;
using ChildAnimatorState = UnityEditor.Animations.ChildAnimatorState;
using AnimatorState = UnityEditor.Animations.AnimatorState;

namespace LostBreadcrumbs.EditorTools
{
    public static class LostBreadcrumbsProjectSetup
    {
        private const string MapConfigAssetPath = "Assets/_Project/ScriptableObjects/Map/SO_SequentialMapConfig.asset";
        private const string LearningConfigAssetPath = "Assets/_Project/ScriptableObjects/Enemy/SO_EnemyLearningPhaseConfig.asset";
        private const string LoadoutCatalogAssetPath = "Assets/_Project/ScriptableObjects/Balance/SO_RunLoadoutCatalog.asset";
        private const double AutoSoakFlowTimeoutSeconds = 180d;
        private const string AutoSoakTraceRelativePath = "Logs/ReleaseSoak/auto_soak_flow_trace.log";
        private const string AutoSoakStatusRelativePath = "Logs/ReleaseSoak/auto_soak_flow_last_status.txt";
        private const string AutoSoakPreflightSummaryRelativePath = "Logs/ReleaseSoak/auto_soak_preflight_last_summary.txt";
        private const int AutoSoakTraceRetentionMaxBytes = 512 * 1024;
        private const int AutoSoakTraceRetentionTailLineCount = 512;
        private const string AutoSoakFlowPendingRunSessionKey = "LostBreadcrumbs.Setup.AutoSoakFlow.PendingRun";
        private const string AutoSoakFlowPendingReportWriteSessionKey = "LostBreadcrumbs.Setup.AutoSoakFlow.PendingReportWrite";
        private const string AutoSoakFlowMissingRunnerLoggedSessionKey = "LostBreadcrumbs.Setup.AutoSoakFlow.MissingRunnerLogged";
        private const string AutoSoakFlowStartedAtSessionKey = "LostBreadcrumbs.Setup.AutoSoakFlow.StartedAt";
        private const string AutoSoakFlowExpectedRunCountSessionKey = "LostBreadcrumbs.Setup.AutoSoakFlow.ExpectedRunCount";
        private const string AutoFixOnEnterPlayPrefKey = "LostBreadcrumbs.Setup.AutoFixOnEnterPlay";
        private const string AutoFixOnEnterPlayMenuPath = "LostBreadcrumbs/Setup/Diagnostics/Auto Fix On Enter Play Mode";
        private const string AggressiveLoadedScanOnEnterPlayPrefKey = "LostBreadcrumbs.Setup.AggressiveLoadedScanOnEnterPlay";
        private const string AggressiveLoadedScanOnEnterPlayMenuPath = "LostBreadcrumbs/Setup/Diagnostics/Aggressive Loaded Scan On Enter Play";
        private const string RemoveMissingScriptsInProjectPrefabsMenuPath = "LostBreadcrumbs/Setup/Diagnostics/Project/Remove Missing Scripts In Prefabs (Assets)";
        private const string RemoveMissingScriptsInProjectScenesMenuPath = "LostBreadcrumbs/Setup/Diagnostics/Project/Remove Missing Scripts In All Scenes (Assets)";
        private const string RemoveMissingScriptsInBuildScenesMenuPath = "LostBreadcrumbs/Setup/Diagnostics/Project/Remove Missing Scripts In Build Scenes";
        private const string LogBuildSceneScriptReferenceHygieneMenuPath = "LostBreadcrumbs/Setup/Diagnostics/Project/Log Build Scene Script Reference Hygiene";
        private const string LogMissingScriptsInProjectAnimatorControllersMenuPath = "LostBreadcrumbs/Setup/Diagnostics/Project/Log Missing Scripts In Animator Controllers (Assets)";
        private const string RemoveMissingScriptsInProjectAnimatorControllersMenuPath = "LostBreadcrumbs/Setup/Diagnostics/Project/Remove Missing Scripts In Animator Controllers (Assets)";
        private const string LogMissingScriptsInLoadedObjectsMenuPath = "LostBreadcrumbs/Setup/Diagnostics/Log Missing Scripts In Loaded Objects";
        private const string RemoveMissingScriptsInLoadedObjectsMenuPath = "LostBreadcrumbs/Setup/Diagnostics/Remove Missing Scripts In Loaded Objects (Open Scenes + Hidden)";
        private const string LogMissingScriptsInLoadedAnimatorControllersMenuPath = "LostBreadcrumbs/Setup/Diagnostics/Log Missing Scripts In Animator Controllers (Loaded)";
        private const string RemoveMissingScriptsInLoadedAnimatorControllersMenuPath = "LostBreadcrumbs/Setup/Diagnostics/Remove Missing Scripts In Animator Controllers (Loaded)";
        private const string AssemblyCSharpName = "Assembly-CSharp";
        private const double DelayedEnteredPlayCleanupDelaySeconds = 0.35d;
        private const double DelayedEnteredPlayCleanupFollowupIntervalSeconds = 0.85d;
        private const int DelayedEnteredPlayCleanupPassCount = 5;
        private const int DelayedEnteredPlayCleanupFinalRetryBudget = 2;
        private const double SceneLoadedCleanupCooldownSeconds = 0.5d;
        private const int AutoPlayScanMaxDetailLogs = 20;
        private const double PlayScanDuplicateLogCooldownSeconds = 8d;
        private const int RuntimeBindingFallbackCandidateWarningThreshold = 2000;
        private const double RuntimeBindingFallbackCooldownSeconds = 12d;

        private static bool autoSoakFlowPendingRun;
        private static bool autoSoakFlowPendingReportWrite;
        private static bool autoSoakFlowMissingRunnerLogged;
        private static double autoSoakFlowStartedAt;
        private static int autoSoakFlowExpectedRunCount;
        private static bool autoFixDiagnosticsRunning;
        private static bool delayedEnteredPlayCleanupPending;
        private static double delayedEnteredPlayCleanupAt;
        private static int delayedEnteredPlayCleanupPassesRemaining;
        private static int delayedEnteredPlayCleanupFinalRetriesRemaining;
        private static bool postPlayRecoverySweepRequested;
        private static bool runtimeBindingFallbackEfficiencyWarningLoggedThisPlaySession;
        private static double runtimeBindingLoadedFallbackDisabledUntil;
        private static double lastSceneLoadedCleanupAt;
        private static int lastPlayScanMissingObjectCount = -1;
        private static int lastPlayScanMissingComponentCount = -1;
        private static double lastPlayScanLoggedAt = -1d;
        private static int lastAnimatorScanAffectedControllerCount = -1;
        private static int lastAnimatorScanMissingBehaviourCount = -1;
        private static double lastAnimatorScanLoggedAt = -1d;
        private static readonly List<GameObject> loadedObjectScanBuffer = new(4096);
        private static readonly List<Animator> loadedAnimatorScanBuffer = new(1024);
        private static bool projectPrefabMissingScriptSweepDoneThisSession;
        private static bool buildSceneMissingScriptSweepDoneThisSession;
        private static bool projectAnimatorControllerMissingScriptSweepDoneThisSession;
        private static readonly RuntimeBindingSpec[] CoreRuntimeBindingSpecs =
        {
            new("SpawnSystem", "LostBreadcrumbs.Runtime.Systems.SpawnSystem", parentNameHint: "Systems", aliasName: "Spawn System", hierarchyPathHint: "GameRoot/Systems/SpawnSystem"),
            new("ProximityManager", "LostBreadcrumbs.Runtime.Managers.ProximityManager", parentNameHint: "Managers", aliasName: "Proximity Manager", hierarchyPathHint: "GameRoot/Managers/ProximityManager"),
            new("GameManager", "LostBreadcrumbs.Runtime.Managers.GameManager", parentNameHint: "Managers", aliasName: "Game Manager", hierarchyPathHint: "GameRoot/Managers/GameManager"),
            new("GameplayRhythmDirector", "LostBreadcrumbs.Runtime.Managers.GameplayRhythmDirector", parentNameHint: "Managers", aliasName: "StagePressureDirector", hierarchyPathHint: "GameRoot/Managers/GameplayRhythmDirector"),
            new("LearningSystem", "LostBreadcrumbs.Runtime.Systems.LearningSystem", parentNameHint: "Systems", aliasName: "Learning System", hierarchyPathHint: "GameRoot/Systems/LearningSystem"),
            new("UIFlowSystem", "LostBreadcrumbs.Runtime.Systems.UIFlowSystem", parentNameHint: "Systems", aliasName: "UI Flow System", hierarchyPathHint: "GameRoot/Systems/UIFlowSystem"),
            new("EchoSystem", "LostBreadcrumbs.Runtime.Systems.EchoSystem", parentNameHint: "Systems", aliasName: "Echo System", hierarchyPathHint: "GameRoot/Systems/EchoSystem"),
        };

        private readonly struct MissingScriptScanResult
        {
            public MissingScriptScanResult(int objectCount, int missingComponentCount)
            {
                ObjectCount = Mathf.Max(0, objectCount);
                MissingComponentCount = Mathf.Max(0, missingComponentCount);
            }

            public int ObjectCount { get; }
            public int MissingComponentCount { get; }
        }

        private readonly struct TmpFontScanResult
        {
            public TmpFontScanResult(int tmpTextCount, int missingFontCount, bool defaultFontMissing)
            {
                TmpTextCount = Mathf.Max(0, tmpTextCount);
                MissingFontCount = Mathf.Max(0, missingFontCount);
                DefaultFontMissing = defaultFontMissing;
            }

            public int TmpTextCount { get; }
            public int MissingFontCount { get; }
            public bool DefaultFontMissing { get; }
        }

        private readonly struct MissingScriptRemovalResult
        {
            public MissingScriptRemovalResult(int objectCount, int removedComponentCount)
            {
                ObjectCount = Mathf.Max(0, objectCount);
                RemovedComponentCount = Mathf.Max(0, removedComponentCount);
            }

            public int ObjectCount { get; }
            public int RemovedComponentCount { get; }
        }

        private readonly struct MissingScriptPlayScanEntry
        {
            public MissingScriptPlayScanEntry(GameObject gameObject, string hierarchyPath, string origin, int missingCount)
            {
                GameObject = gameObject;
                HierarchyPath = hierarchyPath ?? string.Empty;
                Origin = origin ?? string.Empty;
                MissingCount = Mathf.Max(0, missingCount);
            }

            public GameObject GameObject { get; }
            public string HierarchyPath { get; }
            public string Origin { get; }
            public int MissingCount { get; }
        }

        private readonly struct MissingAnimatorScanEntry
        {
            public MissingAnimatorScanEntry(AnimatorController controller, string controllerPath, int missingBehaviourCount, int referencedByAnimators)
            {
                Controller = controller;
                ControllerPath = controllerPath ?? string.Empty;
                MissingBehaviourCount = Mathf.Max(0, missingBehaviourCount);
                ReferencedByAnimators = Mathf.Max(0, referencedByAnimators);
            }

            public AnimatorController Controller { get; }
            public string ControllerPath { get; }
            public int MissingBehaviourCount { get; }
            public int ReferencedByAnimators { get; }
        }

        private readonly struct AnimatorMissingBehaviourRemovalResult
        {
            public AnimatorMissingBehaviourRemovalResult(int controllerCount, int removedBehaviourCount)
            {
                ControllerCount = Mathf.Max(0, controllerCount);
                RemovedBehaviourCount = Mathf.Max(0, removedBehaviourCount);
            }

            public int ControllerCount { get; }
            public int RemovedBehaviourCount { get; }
        }

        private readonly struct ProjectPrefabMissingScriptCleanupResult
        {
            public ProjectPrefabMissingScriptCleanupResult(int prefabAssetCount, int affectedPrefabCount, int removedComponentCount)
            {
                PrefabAssetCount = Mathf.Max(0, prefabAssetCount);
                AffectedPrefabCount = Mathf.Max(0, affectedPrefabCount);
                RemovedComponentCount = Mathf.Max(0, removedComponentCount);
            }

            public int PrefabAssetCount { get; }
            public int AffectedPrefabCount { get; }
            public int RemovedComponentCount { get; }
        }

        private readonly struct ProjectSceneMissingScriptCleanupResult
        {
            public ProjectSceneMissingScriptCleanupResult(int sceneAssetCount, int affectedSceneCount, int removedComponentCount)
            {
                SceneAssetCount = Mathf.Max(0, sceneAssetCount);
                AffectedSceneCount = Mathf.Max(0, affectedSceneCount);
                RemovedComponentCount = Mathf.Max(0, removedComponentCount);
            }

            public int SceneAssetCount { get; }
            public int AffectedSceneCount { get; }
            public int RemovedComponentCount { get; }
        }

        private readonly struct SceneScriptReferenceHygieneScanResult
        {
            public SceneScriptReferenceHygieneScanResult(int sceneAssetCount, int guidlessScriptReferenceCount, int duplicateCoreRuntimeComponentCount)
            {
                SceneAssetCount = Mathf.Max(0, sceneAssetCount);
                GuidlessScriptReferenceCount = Mathf.Max(0, guidlessScriptReferenceCount);
                DuplicateCoreRuntimeComponentCount = Mathf.Max(0, duplicateCoreRuntimeComponentCount);
            }

            public int SceneAssetCount { get; }
            public int GuidlessScriptReferenceCount { get; }
            public int DuplicateCoreRuntimeComponentCount { get; }
            public bool Passed => GuidlessScriptReferenceCount == 0 && DuplicateCoreRuntimeComponentCount == 0;
        }

        private readonly struct ProjectAnimatorControllerMissingScriptCleanupResult
        {
            public ProjectAnimatorControllerMissingScriptCleanupResult(int animatorControllerAssetCount, int affectedAnimatorControllerCount, int removedBehaviourCount)
            {
                AnimatorControllerAssetCount = Mathf.Max(0, animatorControllerAssetCount);
                AffectedAnimatorControllerCount = Mathf.Max(0, affectedAnimatorControllerCount);
                RemovedBehaviourCount = Mathf.Max(0, removedBehaviourCount);
            }

            public int AnimatorControllerAssetCount { get; }
            public int AffectedAnimatorControllerCount { get; }
            public int RemovedBehaviourCount { get; }
        }

        private readonly struct ProjectAnimatorControllerMissingScriptScanResult
        {
            public ProjectAnimatorControllerMissingScriptScanResult(int animatorControllerAssetCount, int affectedAnimatorControllerCount, int missingBehaviourCount)
            {
                AnimatorControllerAssetCount = Mathf.Max(0, animatorControllerAssetCount);
                AffectedAnimatorControllerCount = Mathf.Max(0, affectedAnimatorControllerCount);
                MissingBehaviourCount = Mathf.Max(0, missingBehaviourCount);
            }

            public int AnimatorControllerAssetCount { get; }
            public int AffectedAnimatorControllerCount { get; }
            public int MissingBehaviourCount { get; }
        }

        private readonly struct RuntimeBindingSpec
        {
            public RuntimeBindingSpec(string objectName, string typeName, string parentNameHint = "", string aliasName = "", string hierarchyPathHint = "")
            {
                ObjectName = objectName ?? string.Empty;
                TypeName = typeName ?? string.Empty;
                ParentNameHint = parentNameHint ?? string.Empty;
                AliasName = aliasName ?? string.Empty;
                HierarchyPathHint = hierarchyPathHint ?? string.Empty;
            }

            public string ObjectName { get; }
            public string TypeName { get; }
            public string ParentNameHint { get; }
            public string AliasName { get; }
            public string HierarchyPathHint { get; }
        }

        private readonly struct RuntimeBindingRepairResult
        {
            public RuntimeBindingRepairResult(
                int targetCount,
                int foundCount,
                int missingScriptRemovedCount,
                int addedComponentCount,
                int duplicateComponentRemovedCount,
                int missingObjectCount,
                int unresolvedTypeCount,
                int loadedFallbackResolvedCount,
                int loadedFallbackCandidateCount,
                int sceneCandidateCount,
                int loadedFallbackSkippedCount)
            {
                TargetCount = Mathf.Max(0, targetCount);
                FoundCount = Mathf.Max(0, foundCount);
                MissingScriptRemovedCount = Mathf.Max(0, missingScriptRemovedCount);
                AddedComponentCount = Mathf.Max(0, addedComponentCount);
                DuplicateComponentRemovedCount = Mathf.Max(0, duplicateComponentRemovedCount);
                MissingObjectCount = Mathf.Max(0, missingObjectCount);
                UnresolvedTypeCount = Mathf.Max(0, unresolvedTypeCount);
                LoadedFallbackResolvedCount = Mathf.Max(0, loadedFallbackResolvedCount);
                LoadedFallbackCandidateCount = Mathf.Max(0, loadedFallbackCandidateCount);
                SceneCandidateCount = Mathf.Max(0, sceneCandidateCount);
                LoadedFallbackSkippedCount = Mathf.Max(0, loadedFallbackSkippedCount);
            }

            public int TargetCount { get; }
            public int FoundCount { get; }
            public int MissingScriptRemovedCount { get; }
            public int AddedComponentCount { get; }
            public int DuplicateComponentRemovedCount { get; }
            public int MissingObjectCount { get; }
            public int UnresolvedTypeCount { get; }
            public int LoadedFallbackResolvedCount { get; }
            public int LoadedFallbackCandidateCount { get; }
            public int SceneCandidateCount { get; }
            public int LoadedFallbackSkippedCount { get; }
        }

        [MenuItem("LostBreadcrumbs/Setup/Create Recommended Folder Structure")]
        public static void CreateRecommendedFolders()
        {
            string[] folderPaths =
            {
                "Assets/_Project/Art",
                "Assets/_Project/Audio",
                "Assets/_Project/Materials",
                "Assets/_Project/Prefabs/Player",
                "Assets/_Project/Prefabs/Enemy",
                "Assets/_Project/Prefabs/Props",
                "Assets/_Project/Prefabs/UI",
                "Assets/_Project/Prefabs/VFX",
                "Assets/_Project/Scenes",
                "Assets/_Project/Scripts/Core",
                "Assets/_Project/Scripts/Core/Input",
                "Assets/_Project/Scripts/Managers",
                "Assets/_Project/Scripts/Player",
                "Assets/_Project/Scripts/AI/Brain",
                "Assets/_Project/Scripts/AI/States",
                "Assets/_Project/Scripts/AI/Sensors",
                "Assets/_Project/Scripts/AI/Learning",
                "Assets/_Project/Scripts/AI/Debug",
                "Assets/_Project/Scripts/Audio",
                "Assets/_Project/Scripts/Map",
                "Assets/_Project/Scripts/Events",
                "Assets/_Project/Scripts/UI",
                "Assets/_Project/Scripts/Save",
                "Assets/_Project/ScriptableObjects/Enemy",
                "Assets/_Project/ScriptableObjects/Map",
                "Assets/_Project/ScriptableObjects/Audio",
                "Assets/_Project/ScriptableObjects/Events",
                "Assets/_Project/ScriptableObjects/Balance",
                "Assets/_Project/Tilemaps",
                "Assets/_Project/Animations",
                "Assets/_Project/Resources",
                "Assets/_Project/Addressables",
                "Assets/ThirdParty"
            };

            for (int i = 0; i < folderPaths.Length; i++)
            {
                EnsureFolderPath(folderPaths[i]);
            }

            AssetDatabase.Refresh();
            Debug.Log("LostBreadcrumbs folder structure is ready.");
        }

        [MenuItem("LostBreadcrumbs/Setup/Create Default Enemy Profiles")]
        public static void CreateDefaultEnemyProfiles()
        {
            EnsureFolderPath("Assets/_Project/ScriptableObjects/Enemy");

            CreateOrUpdateProfile(
                "Assets/_Project/ScriptableObjects/Enemy/EP_Obsessive.asset",
                p =>
                {
                    p.profileId = "obsessive";
                    p.persistence = 0.95f;
                    p.searchBreadth = 0.9f;
                    p.predictionBias = 0.85f;
                    p.aggression = 0.65f;
                    p.curiosity = 0.75f;
                    p.audioSensitivity = 1.2f;
                    p.lightSensitivity = 1f;
                    p.patrolSpeed = 1.22f;
                    p.investigateSpeed = 1.58f;
                    p.chaseSpeed = 2.32f;
                    p.returnSpeed = 1.68f;
                    p.searchDurationSeconds = 10f;
                    p.chaseForgetSeconds = 3.2f;
                    p.safeHavenDetectionFactor = 0.12f;
                    p.decoyNoiseResponse = 1.15f;
                    p.itemNoiseResponse = 1.08f;
                    p.smokeVisionPenetration = 0.22f;
                });

            CreateOrUpdateProfile(
                "Assets/_Project/ScriptableObjects/Enemy/EP_Cautious.asset",
                p =>
                {
                    p.profileId = "cautious";
                    p.persistence = 0.75f;
                    p.searchBreadth = 0.65f;
                    p.predictionBias = 0.45f;
                    p.aggression = 0.4f;
                    p.curiosity = 0.85f;
                    p.audioSensitivity = 0.9f;
                    p.lightSensitivity = 1.15f;
                    p.patrolSpeed = 1.16f;
                    p.investigateSpeed = 1.54f;
                    p.chaseSpeed = 2.2f;
                    p.returnSpeed = 1.62f;
                    p.suspicionHoldTime = 1.7f;
                    p.chaseForgetSeconds = 2f;
                    p.safeHavenDetectionFactor = 0.05f;
                    p.decoyNoiseResponse = 0.9f;
                    p.itemNoiseResponse = 0.95f;
                    p.smokeVisionPenetration = 0.14f;
                });

            CreateOrUpdateProfile(
                "Assets/_Project/ScriptableObjects/Enemy/EP_Impulsive.asset",
                p =>
                {
                    p.profileId = "impulsive";
                    p.persistence = 0.35f;
                    p.searchBreadth = 0.45f;
                    p.predictionBias = 0.2f;
                    p.aggression = 0.95f;
                    p.curiosity = 0.35f;
                    p.audioSensitivity = 1.35f;
                    p.lightSensitivity = 0.85f;
                    p.patrolSpeed = 1.28f;
                    p.investigateSpeed = 1.7f;
                    p.chaseSpeed = 2.68f;
                    p.returnSpeed = 1.75f;
                    p.chaseForgetSeconds = 1.4f;
                    p.searchDurationSeconds = 4.5f;
                    p.safeHavenDetectionFactor = 0f;
                    p.decoyNoiseResponse = 1.3f;
                    p.itemNoiseResponse = 1.32f;
                    p.smokeVisionPenetration = 0.08f;
                });

            CreateOrUpdateProfile(
                "Assets/_Project/ScriptableObjects/Enemy/EP_Flanker.asset",
                p =>
                {
                    p.profileId = "flanker";
                    p.persistence = 0.68f;
                    p.searchBreadth = 0.8f;
                    p.predictionBias = 0.9f;
                    p.aggression = 0.55f;
                    p.curiosity = 0.7f;
                    p.audioSensitivity = 1f;
                    p.lightSensitivity = 1.1f;
                    p.patrolSpeed = 1.2f;
                    p.investigateSpeed = 1.62f;
                    p.chaseSpeed = 2.3f;
                    p.returnSpeed = 1.7f;
                    p.searchDurationSeconds = 8.8f;
                    p.safeHavenDetectionFactor = 0.35f;
                    p.decoyNoiseResponse = 1.05f;
                    p.itemNoiseResponse = 1.04f;
                    p.smokeVisionPenetration = 0.35f;
                });

            CreateOrUpdateProfile(
                "Assets/_Project/ScriptableObjects/Enemy/EP_Seeker.asset",
                p =>
                {
                    p.profileId = "seeker";
                    p.persistence = 0.82f;
                    p.searchBreadth = 0.9f;
                    p.predictionBias = 0.72f;
                    p.aggression = 0.6f;
                    p.curiosity = 0.92f;
                    p.audioSensitivity = 1.25f;
                    p.lightSensitivity = 1.15f;
                    p.patrolSpeed = 1.18f;
                    p.investigateSpeed = 1.68f;
                    p.chaseSpeed = 2.38f;
                    p.returnSpeed = 1.72f;
                    p.searchDurationSeconds = 11.5f;
                    p.suspicionGainPerNoise = 0.22f;
                    p.safeHavenDetectionFactor = 0.72f;
                    p.decoyNoiseResponse = 0.28f;
                    p.itemNoiseResponse = 1.18f;
                    p.smokeVisionPenetration = 0.72f;
                });
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Default enemy profiles created/updated.");
        }

        [MenuItem("LostBreadcrumbs/Setup/Create Default Learning Phase Config")]
        public static void CreateDefaultLearningPhaseConfig()
        {
            EnemyLearningPhaseConfig learningConfig = EnsureLearningPhaseConfigAsset();
            if (learningConfig == null)
            {
                Debug.LogError("Failed to create learning phase config asset.");
                return;
            }

            EditorUtility.SetDirty(learningConfig);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Default enemy learning phase config is ready.");
        }

        [MenuItem("LostBreadcrumbs/Setup/Create Default Map Config")]
        public static void CreateDefaultMapConfig()
        {
            SequentialMapConfig mapConfig = EnsureMapConfigAsset();
            if (mapConfig == null)
            {
                Debug.LogError("Failed to create map config asset.");
                return;
            }

            EditorUtility.SetDirty(mapConfig);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Default sequential map config is ready.");
        }


        [MenuItem("LostBreadcrumbs/Setup/Create Default Run Loadout Catalog")]
        public static void CreateDefaultRunLoadoutCatalog()
        {
            RunLoadoutCatalog loadoutCatalog = EnsureRunLoadoutCatalogAsset();
            if (loadoutCatalog == null)
            {
                Debug.LogError("Failed to create run loadout catalog asset.");
                return;
            }

            EditorUtility.SetDirty(loadoutCatalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Default run loadout catalog is ready.");
        }
        [MenuItem("LostBreadcrumbs/Setup/Build Dummy Hierarchy In Active Scene")]
        public static void BuildDummyHierarchyInActiveScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                Debug.LogError("No active scene loaded.");
                return;
            }

            GameObject sceneRoot = EnsureRoot("Scene_Root");

            GameObject gameRoot = EnsureChild(sceneRoot, "GameRoot");
            EnsureChild(gameRoot, "Bootstrap");

            GameObject managers = EnsureChild(gameRoot, "Managers");
            EnsureManager<GameManager>(managers, "GameManager");
            EnsureManager<StageManager>(managers, "StageManager");
            EnsureManager<TurnOrTickManager>(managers, "TurnOrTickManager");
            EnsureManager<NoiseManager>(managers, "NoiseManager");
            EnsureManager<AIManager>(managers, "AIManager");
            EnsureManager<ProximityManager>(managers, "ProximityManager");
            EnsureManager<EventManager>(managers, "EventManager");
            SaveManager saveManager = AddOrGet<SaveManager>(EnsureChild(managers, "SaveManager"));
            AudioManager audioManager = AddOrGet<AudioManager>(EnsureChild(managers, "AudioManager"));
            AudioCombatDuckingDirector audioDuckingDirector = AddOrGet<AudioCombatDuckingDirector>(EnsureChild(managers, "AudioCombatDuckingDirector"));
            AudioDummyLoopRuntime audioDummyLoopRuntime = AddOrGet<AudioDummyLoopRuntime>(EnsureChild(managers, "AudioDummyLoopRuntime"));
            RunLoadoutDirector runLoadoutDirector = AddOrGet<RunLoadoutDirector>(EnsureChild(managers, "RunLoadoutDirector"));
            GameplayRhythmDirector gameplayRhythmDirector = AddOrGet<GameplayRhythmDirector>(EnsureChild(managers, "GameplayRhythmDirector"));
            StagePressureDirector stagePressureDirector = AddOrGet<StagePressureDirector>(EnsureChild(managers, "StagePressureDirector"));
            ThreatReadabilityDirector threatReadabilityDirector = AddOrGet<ThreatReadabilityDirector>(EnsureChild(managers, "ThreatReadabilityDirector"));
            StageSetPieceDirector stageSetPieceDirector = AddOrGet<StageSetPieceDirector>(EnsureChild(managers, "StageSetPieceDirector"));
            AddOrGet<RegressionChecklistRunner>(EnsureChild(managers, "RegressionChecklistRunner"));
            EnsureManager<DebugManager>(managers, "DebugManager");

            GameObject runtime = EnsureChild(gameRoot, "Runtime");
            GameObject playerRoot = EnsureChild(runtime, "Player");
            GameObject playerDummy = EnsureChild(playerRoot, "Player_Dummy");
            PlayerDummyController playerController = AddOrGet<PlayerDummyController>(playerDummy);
            Rigidbody2D playerBody = AddOrGet<Rigidbody2D>(playerDummy);
            playerBody.gravityScale = 0f;
            playerBody.freezeRotation = true;
            playerBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            playerBody.interpolation = RigidbodyInterpolation2D.Interpolate;
            CircleCollider2D playerCollider = AddOrGet<CircleCollider2D>(playerDummy);
            playerCollider.isTrigger = false;
            playerCollider.radius = 0.32f;
            playerCollider.offset = Vector2.zero;
            PlayerEchoPulseAbility playerPulse = AddOrGet<PlayerEchoPulseAbility>(playerDummy);
            PlayerDecoyAbility playerDecoy = AddOrGet<PlayerDecoyAbility>(playerDummy);
            PlayerSmokeAbility playerSmoke = AddOrGet<PlayerSmokeAbility>(playerDummy);
            AddOrGet<PlayerConcealmentState>(playerDummy);
            PlayerBehaviorTelemetry playerTelemetry = AddOrGet<PlayerBehaviorTelemetry>(playerDummy);
            PlayerVisibilitySource playerVisibility = AddOrGet<PlayerVisibilitySource>(playerDummy);
            PlayerVitalSystem playerVitals = AddOrGet<PlayerVitalSystem>(playerDummy);
            AddOrGet<PlayerDummyVisual>(playerDummy);
            if (TagExists("Player"))
            {
                playerDummy.tag = "Player";
            }

            GameObject systems = EnsureChild(gameRoot, "Systems");
            GameObject mapSystemObject = EnsureChild(systems, "MapSystem");
            MapSystem mapSystem = AddOrGet<MapSystem>(mapSystemObject);
            EnsureSystem<VisionSystem>(systems, "VisionSystem");
            EnsureSystem<EchoSystem>(systems, "EchoSystem");
            EnsureSystem<LearningSystem>(systems, "LearningSystem");
            EnsureSystem<SpawnSystem>(systems, "SpawnSystem");
            EnsureSystem<UIFlowSystem>(systems, "UIFlowSystem");
            GameObject stageLoopObject = EnsureChild(systems, "StageLoopDirector");
            StageLoopDirector stageLoop = AddOrGet<StageLoopDirector>(stageLoopObject);
            stageLoop.SetMapSystemForEditor(mapSystem);

            GameObject mapTuningObject = EnsureChild(systems, "MapTuningDebugController");
            MapTuningDebugController mapTuningDebug = AddOrGet<MapTuningDebugController>(mapTuningObject);
            mapTuningDebug.SetMapSystemForEditor(mapSystem);

            SequentialMapConfig mapConfig = EnsureMapConfigAsset();
            if (mapConfig != null)
            {
                mapSystem.SetConfigForEditor(mapConfig);
                mapTuningDebug.SetBaseConfigForEditor(mapConfig);
                EditorUtility.SetDirty(mapSystem);
                EditorUtility.SetDirty(mapTuningDebug);
            }

            EnemyLearningPhaseConfig learningConfig = EnsureLearningPhaseConfigAsset();
            if (learningConfig != null)
            {
                playerTelemetry.SetLearningConfigForEditor(learningConfig);
            }

            playerTelemetry.SetMapSystemForEditor(mapSystem);
            playerVitals.SetMapSystemForEditor(mapSystem);
            saveManager.SetMapSystemForEditor(mapSystem);
            RunLoadoutCatalog loadoutCatalog = EnsureRunLoadoutCatalogAsset();
            if (loadoutCatalog != null)
            {
                runLoadoutDirector.SetCatalogForEditor(loadoutCatalog);
                EditorUtility.SetDirty(runLoadoutDirector);
            }
            runLoadoutDirector.SetReferencesForEditor(playerController, playerVisibility, playerPulse, playerDecoy, playerSmoke);

            GameObject enemiesRoot = EnsureChild(runtime, "Enemies");
            GameObject setPiecesRoot = EnsureChild(runtime, "SetPieces");
            EnemyProfile[] defaultProfiles = LoadDefaultEnemyProfiles();
            GameObject enemySpawnObject = EnsureChild(systems, "EnemySpawnDirector");
            EnemySpawnDirector enemySpawnDirector = AddOrGet<EnemySpawnDirector>(enemySpawnObject);
            enemySpawnDirector.SetMapSystemForEditor(mapSystem);
            enemySpawnDirector.SetRuntimeRootsForEditor(enemiesRoot.transform, playerDummy.transform);
            enemySpawnDirector.SetProfilePoolForEditor(defaultProfiles);
            stagePressureDirector.SetReferencesForEditor(mapSystem, enemySpawnDirector, runLoadoutDirector, playerTelemetry, gameplayRhythmDirector);
            stageSetPieceDirector.SetReferencesForEditor(mapSystem, enemySpawnDirector, setPiecesRoot.transform, stagePressureDirector, mapTuningDebug);

            EnsureChild(runtime, "Interactables");
            EnsureChild(runtime, "Pickups");
            EnsureChild(runtime, "Traps");
            GameObject vfxRoot = EnsureChild(runtime, "VFX");
            EnsureChild(vfxRoot, "ThreatPulseProbe");
            EnsureChild(runtime, "ReadabilityArtProbe");
            GameObject loadoutRoot = EnsureChild(runtime, "Loadout");
            EnsureChild(loadoutRoot, "Loadout_Balanced");
            EnsureChild(loadoutRoot, "Loadout_Pathfinder");
            EnsureChild(loadoutRoot, "Loadout_EchoSpecialist");
            EnsureChild(loadoutRoot, "Loadout_ShadowRunner");
            GameObject audioEmittersRoot = EnsureChild(runtime, "AudioEmitters");
            GameObject bgmDummyObject = EnsureChild(audioEmittersRoot, "BGM_Dummy");
            GameObject ambienceDummyObject = EnsureChild(audioEmittersRoot, "Ambience_Dummy");
            AudioSource bgmDummySource = AddOrGet<AudioSource>(bgmDummyObject);
            AudioSource ambienceDummySource = AddOrGet<AudioSource>(ambienceDummyObject);
            bgmDummySource.loop = true;
            bgmDummySource.playOnAwake = false;
            bgmDummySource.spatialBlend = 0f;
            bgmDummySource.volume = 0.9f;
            ambienceDummySource.loop = true;
            ambienceDummySource.playOnAwake = false;
            ambienceDummySource.spatialBlend = 0f;
            ambienceDummySource.volume = 0.85f;

            GameObject cameras = EnsureChild(gameRoot, "Cameras");
            GameObject mainCamera = EnsureChild(cameras, "MainCamera");
            Camera cameraComponent = AddOrGet<Camera>(mainCamera);
            cameraComponent.orthographic = true;
            cameraComponent.orthographicSize = 4.35f;
            cameraComponent.clearFlags = CameraClearFlags.SolidColor;
            cameraComponent.backgroundColor = new Color(0.018f, 0.024f, 0.034f, 1f);
            AudioListener mainAudioListener = AddOrGet<AudioListener>(mainCamera);
            mainAudioListener.enabled = true;
            CameraFollow2D cameraFollow = AddOrGet<CameraFollow2D>(mainCamera);
            mapSystem.SetCameraForEditor(cameraComponent);
            mapSystem.ConfigureCameraFitForEditor(1.25f, 4.25f, 5.75f);
            cameraFollow.SetTargetForEditor(playerDummy.transform);
            EnsureChild(cameras, "VirtualCamera");

            audioManager.SetDuckingSourcesForEditor(bgmDummySource, ambienceDummySource);
            audioDuckingDirector.SetAudioManagerForEditor(audioManager);
            audioDuckingDirector.SetPlayerForEditor(playerDummy.transform);
            audioDummyLoopRuntime.SetSourcesForEditor(audioManager, bgmDummySource, ambienceDummySource, gameplayRhythmDirector);

            GameObject tilemapRoot = EnsureChild(sceneRoot, "TilemapRoot");
            EnsureChild(tilemapRoot, "Ground");
            EnsureChild(tilemapRoot, "Walls");
            EnsureChild(tilemapRoot, "Occluders");
            EnsureChild(tilemapRoot, "Archetypes");
            EnsureChild(tilemapRoot, "Decoration");
            EnsureChild(tilemapRoot, "Collision");
            GameObject fogMask = EnsureChild(tilemapRoot, "FogMask");
            FogOfWarSystem fogSystem = AddOrGet<FogOfWarSystem>(fogMask);
            fogSystem.SetTargetForEditor(playerDummy.transform, playerVisibility);
            mapSystem.SetFogSystemForEditor(fogSystem);
            threatReadabilityDirector.SetReferencesForEditor(playerDummy.transform, playerVisibility, cameraComponent, cameraFollow, fogSystem, stagePressureDirector, mapTuningDebug, mapSystem);
            gameplayRhythmDirector.SetReferencesForEditor(mapSystem, stagePressureDirector, threatReadabilityDirector, cameraFollow);

            GameObject lightingRoot = EnsureChild(sceneRoot, "LightingRoot");
            EnsureChild(lightingRoot, "GlobalLight2D");
            EnsureChild(lightingRoot, "PlayerAmbientLight");
            EnsureChild(lightingRoot, "FlashlightLight");
            EnsureChild(lightingRoot, "EchoRevealLights");
            EnsureChild(lightingRoot, "SafeHavenLights");

            GameObject uiRoot = EnsureChild(sceneRoot, "UIRoot");
            GameplayHudRuntime gameplayHud = AddOrGet<GameplayHudRuntime>(EnsureChild(uiRoot, "HUD"));
            AddOrGet<GameplayFlowGuideRuntime>(EnsureChild(uiRoot, "FlowGuide"));
            DreadScreenOverlayRuntime dreadOverlay = AddOrGet<DreadScreenOverlayRuntime>(EnsureChild(uiRoot, "DreadScreenOverlay"));
            dreadOverlay.SetThreatSourceForEditor(threatReadabilityDirector);
            GameObject alertsObject = EnsureChild(uiRoot, "Alerts");
            EventFeedbackRuntime eventFeedback = AddOrGet<EventFeedbackRuntime>(alertsObject);
            eventFeedback.SetCameraForEditor(cameraFollow);
            eventFeedback.SetRuntimeSourcesForEditor(playerVitals, stageLoop);
            Canvas hudCanvas = gameplayHud != null ? gameplayHud.GetComponentInChildren<Canvas>(true) : null;
            if (hudCanvas != null)
            {
                eventFeedback.SetTargetCanvasForEditor(hudCanvas);
            }
            EnsureChild(uiRoot, "TouchInput");
            EnsureChild(uiRoot, "PauseMenu");
            AddOrGet<DebugOverlay>(EnsureChild(uiRoot, "DebugOverlay"));

            GameObject authoringRoot = EnsureChild(sceneRoot, "AuthoringRoot");
            GameObject spawnPoints = EnsureChild(authoringRoot, "SpawnPoints");
            GameObject patrolRoutes = EnsureChild(authoringRoot, "PatrolRoutes");
            GameObject specialRooms = EnsureChild(authoringRoot, "SpecialRooms");
            GameObject testMarkers = EnsureChild(authoringRoot, "TestMarkers");

            CreateMarker(spawnPoints, "Spawn_Obsessive", AuthoringMarkerType.EnemySpawn, new Vector3(3f, 0f, 0f));
            CreateMarker(spawnPoints, "Spawn_Cautious", AuthoringMarkerType.EnemySpawn, new Vector3(4f, -1.4f, 0f));
            CreateMarker(spawnPoints, "Spawn_Impulsive", AuthoringMarkerType.EnemySpawn, new Vector3(4f, 1.4f, 0f));

            CreateMarker(patrolRoutes, "Patrol_A_01", AuthoringMarkerType.PatrolPoint, new Vector3(-2f, -1f, 0f));
            CreateMarker(patrolRoutes, "Patrol_A_02", AuthoringMarkerType.PatrolPoint, new Vector3(-1f, 0f, 0f));
            CreateMarker(patrolRoutes, "Patrol_A_03", AuthoringMarkerType.PatrolPoint, new Vector3(-2f, 1f, 0f));

            CreateMarker(specialRooms, "Hideout_Dummy", AuthoringMarkerType.Hideout, new Vector3(-4f, 0f, 0f));

            CreateMarker(testMarkers, "StraightCorridor", AuthoringMarkerType.Corridor, new Vector3(0f, -3f, 0f));
            CreateMarker(testMarkers, "ForkPoint", AuthoringMarkerType.Fork, new Vector3(0f, 3f, 0f));
            CreateMarker(testMarkers, "FlashlightTestZone", AuthoringMarkerType.FlashlightZone, new Vector3(2f, 3f, 0f));

            GameObject noiseButton = CreateMarker(testMarkers, "NoiseButton", AuthoringMarkerType.NoiseTest, new Vector3(-2f, 3f, 0f));
            AddOrGet<DebugNoiseButtonDummy>(noiseButton);

            GameObject personalitySpawns = EnsureChild(testMarkers, "PersonalityCompareSpawns");
            CreateMarker(personalitySpawns, "PersonalitySpawn_A", AuthoringMarkerType.EnemySpawn, new Vector3(6f, -1f, 0f));
            CreateMarker(personalitySpawns, "PersonalitySpawn_B", AuthoringMarkerType.EnemySpawn, new Vector3(6f, 0f, 0f));
            CreateMarker(personalitySpawns, "PersonalitySpawn_C", AuthoringMarkerType.EnemySpawn, new Vector3(6f, 1f, 0f));

            int listenerAdjustedCount = EnsureSingleActiveAudioListenerInOpenScenes();
            if (listenerAdjustedCount > 0)
            {
                Debug.Log($"Adjusted {listenerAdjustedCount} extra AudioListener components to keep a single active listener.");
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            Selection.activeGameObject = sceneRoot;
            Debug.Log("Dummy hierarchy generated in active scene.");
        }

        [MenuItem("LostBreadcrumbs/Setup/Build Full Playground")]
        public static void BuildFullPlayground()
        {
            CreateRecommendedFolders();
            CreateDefaultEnemyProfiles();
            CreateDefaultMapConfig();
            CreateDefaultLearningPhaseConfig();
            CreateDefaultRunLoadoutCatalog();
            BuildDummyHierarchyInActiveScene();
        }

        [InitializeOnLoadMethod]
        private static void RegisterEnterPlayAutoFixHook()
        {
            EditorApplication.playModeStateChanged -= OnEnterPlayAutoFixStateChanged;
            EditorApplication.playModeStateChanged += OnEnterPlayAutoFixStateChanged;
            EditorApplication.update -= PollDelayedEnteredPlayCleanup;
            EditorApplication.update += PollDelayedEnteredPlayCleanup;
            SceneManager.sceneLoaded -= OnSceneLoadedEnterPlayAutoFix;
            SceneManager.sceneLoaded += OnSceneLoadedEnterPlayAutoFix;
            Menu.SetChecked(AutoFixOnEnterPlayMenuPath, IsAutoFixOnEnterPlayEnabled());
            Menu.SetChecked(AggressiveLoadedScanOnEnterPlayMenuPath, IsAggressiveLoadedScanOnEnterPlayEnabled());
        }

        [MenuItem(AutoFixOnEnterPlayMenuPath)]
        private static void ToggleAutoFixOnEnterPlayMenu()
        {
            bool nextState = !IsAutoFixOnEnterPlayEnabled();
            EditorPrefs.SetBool(AutoFixOnEnterPlayPrefKey, nextState);
            Menu.SetChecked(AutoFixOnEnterPlayMenuPath, nextState);
            Debug.Log($"Auto fix on enter play {(nextState ? "enabled" : "disabled")}.");
        }

        [MenuItem(AutoFixOnEnterPlayMenuPath, true)]
        private static bool ToggleAutoFixOnEnterPlayMenuValidate()
        {
            Menu.SetChecked(AutoFixOnEnterPlayMenuPath, IsAutoFixOnEnterPlayEnabled());
            return true;
        }

        [MenuItem(AggressiveLoadedScanOnEnterPlayMenuPath)]
        private static void ToggleAggressiveLoadedScanOnEnterPlayMenu()
        {
            bool nextState = !IsAggressiveLoadedScanOnEnterPlayEnabled();
            EditorPrefs.SetBool(AggressiveLoadedScanOnEnterPlayPrefKey, nextState);
            Menu.SetChecked(AggressiveLoadedScanOnEnterPlayMenuPath, nextState);
            Debug.Log($"Aggressive loaded scan on enter play {(nextState ? "enabled" : "disabled")}.");
        }

        [MenuItem(AggressiveLoadedScanOnEnterPlayMenuPath, true)]
        private static bool ToggleAggressiveLoadedScanOnEnterPlayMenuValidate()
        {
            Menu.SetChecked(AggressiveLoadedScanOnEnterPlayMenuPath, IsAggressiveLoadedScanOnEnterPlayEnabled());
            return true;
        }

        [MenuItem("LostBreadcrumbs/Setup/Diagnostics/Fix Common Scene Issues")]
        public static void FixCommonSceneIssuesMenu()
        {
            int mapConfigFixedCount = FixMissingMapConfigInOpenScenes();
            int listenerAdjustedCount = EnsureSingleActiveAudioListenerInOpenScenes();
            RuntimeBindingRepairResult runtimeBindingRepair = RepairCoreRuntimeBindingsInOpenScenes(logDetails: true, maxDetailLogs: 120);
            AnimatorMissingBehaviourRemovalResult animatorMissingBehavioursRemoved =
                RemoveMissingScriptsInLoadedAnimatorControllersPass(logDetails: true, maxDetailLogs: 120);
            ProjectAnimatorControllerMissingScriptCleanupResult animatorProjectMissingBehavioursRemoved =
                RemoveMissingScriptsInProjectAnimatorControllersInternal(logDetails: true, maxDetailLogs: 200);
            projectAnimatorControllerMissingScriptSweepDoneThisSession = true;
            ProjectAnimatorControllerMissingScriptScanResult animatorProjectMissingBehavioursAfter =
                ScanMissingScriptsInProjectAnimatorControllersInternal(logDetails: false, maxDetailLogs: 0);
            MissingScriptScanResult missingScriptsBefore = ScanMissingScriptsInOpenScenes(logDetails: true);
            MissingScriptRemovalResult missingScriptsRemoved = RemoveMissingScriptsInOpenScenesInternal(logDetails: true);
            MissingScriptScanResult missingScriptsAfter = ScanMissingScriptsInOpenScenes(logDetails: false);
            ProjectPrefabMissingScriptCleanupResult prefabMissingScriptsRemoved = RemoveMissingScriptsInProjectPrefabsInternal(logDetails: true, maxDetailLogs: 200);
            projectPrefabMissingScriptSweepDoneThisSession = true;
            SceneScriptReferenceHygieneScanResult buildSceneScriptHygiene =
                ScanBuildSceneScriptReferenceHygiene(logDetails: true, maxDetailLogs: 200);

            TmpFontScanResult tmpFontsBefore = ScanMissingTmpFontAssignmentsInOpenScenes(logDetails: true);
            bool tmpFontFixAttempted = AssignMissingTmpFontsInOpenScenes(logDetails: true, out int tmpFontsAssignedCount, out string tmpDefaultFontPath);
            TmpFontScanResult tmpFontsAfter = ScanMissingTmpFontAssignmentsInOpenScenes(logDetails: false);

            bool hasWarning =
                missingScriptsAfter.MissingComponentCount > 0 ||
                tmpFontsAfter.MissingFontCount > 0 ||
                tmpFontsAfter.DefaultFontMissing ||
                runtimeBindingRepair.MissingObjectCount > 0 ||
                runtimeBindingRepair.UnresolvedTypeCount > 0 ||
                animatorProjectMissingBehavioursAfter.MissingBehaviourCount > 0 ||
                !buildSceneScriptHygiene.Passed;
            string summary =
                $"Scene diagnostics complete. mapConfigFixed={mapConfigFixedCount}, audioListenersAdjusted={listenerAdjustedCount}, " +
                $"{FormatRuntimeBindingRepairSummary(runtimeBindingRepair)}, " +
                $"animatorMissingBehavioursRemoved={animatorMissingBehavioursRemoved.RemovedBehaviourCount}/{animatorMissingBehavioursRemoved.ControllerCount} controllers, " +
                $"animatorProjectMissingBehavioursRemoved={animatorProjectMissingBehavioursRemoved.RemovedBehaviourCount}/{animatorProjectMissingBehavioursRemoved.AffectedAnimatorControllerCount} controllers (scanned={animatorProjectMissingBehavioursRemoved.AnimatorControllerAssetCount}), " +
                $"animatorProjectMissingBehavioursRemaining={animatorProjectMissingBehavioursAfter.MissingBehaviourCount}/{animatorProjectMissingBehavioursAfter.AffectedAnimatorControllerCount} controllers, " +
                $"missingScripts(beforeObjects={missingScriptsBefore.ObjectCount}, beforeComponents={missingScriptsBefore.MissingComponentCount}, removedObjects={missingScriptsRemoved.ObjectCount}, removedComponents={missingScriptsRemoved.RemovedComponentCount}, remainingComponents={missingScriptsAfter.MissingComponentCount}), " +
                $"prefabMissingScripts(removedPrefabs={prefabMissingScriptsRemoved.AffectedPrefabCount}, removedComponents={prefabMissingScriptsRemoved.RemovedComponentCount}, scanned={prefabMissingScriptsRemoved.PrefabAssetCount}), " +
                $"buildSceneScriptHygiene(guidlessScriptRefs={buildSceneScriptHygiene.GuidlessScriptReferenceCount}, duplicateCoreRuntimeComponents={buildSceneScriptHygiene.DuplicateCoreRuntimeComponentCount}, scanned={buildSceneScriptHygiene.SceneAssetCount}), " +
                $"tmpFonts(beforeMissing={tmpFontsBefore.MissingFontCount}, assigned={tmpFontsAssignedCount}, remainingMissing={tmpFontsAfter.MissingFontCount}, defaultMissing={tmpFontsAfter.DefaultFontMissing}, fixAttempted={tmpFontFixAttempted}, defaultFont='{tmpDefaultFontPath}').";

            if (hasWarning)
            {
                Debug.LogWarning(summary);
            }
            else
            {
                Debug.Log(summary);
            }
        }

        [MenuItem("LostBreadcrumbs/Setup/Diagnostics/Remove Missing Scripts In Open Scenes")]
        public static void RemoveMissingScriptsInOpenScenesMenu()
        {
            MissingScriptRemovalResult result = RemoveMissingScriptsInOpenScenesInternal(logDetails: true);
            if (result.RemovedComponentCount > 0)
            {
                Debug.Log($"Removed {result.RemovedComponentCount} missing script components from {result.ObjectCount} objects.");
            }
            else
            {
                Debug.Log("No missing scripts were removed.");
            }
        }

        [MenuItem(RemoveMissingScriptsInProjectPrefabsMenuPath)]
        public static void RemoveMissingScriptsInProjectPrefabsMenu()
        {
            ProjectPrefabMissingScriptCleanupResult result = RemoveMissingScriptsInProjectPrefabsInternal(logDetails: true, maxDetailLogs: 200);
            projectPrefabMissingScriptSweepDoneThisSession = true;
            if (result.RemovedComponentCount > 0)
            {
                Debug.Log(
                    $"Removed {result.RemovedComponentCount} missing script components from {result.AffectedPrefabCount} prefab assets " +
                    $"(scanned={result.PrefabAssetCount}).");
            }
            else
            {
                Debug.Log($"No missing scripts found in prefab assets (scanned={result.PrefabAssetCount}).");
            }
        }

        [MenuItem(RemoveMissingScriptsInProjectScenesMenuPath)]
        public static void RemoveMissingScriptsInProjectScenesMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("Canceled project scene missing-script cleanup (save dialog canceled).");
                return;
            }

            ProjectSceneMissingScriptCleanupResult result = RemoveMissingScriptsInAllProjectScenesInternal(logDetails: true, maxDetailLogs: 300);
            buildSceneMissingScriptSweepDoneThisSession = true;
            if (result.RemovedComponentCount > 0)
            {
                Debug.Log(
                    $"Removed {result.RemovedComponentCount} missing script components from {result.AffectedSceneCount} scenes " +
                    $"(scanned={result.SceneAssetCount}).");
            }
            else
            {
                Debug.Log($"No missing scripts found in scene assets (scanned={result.SceneAssetCount}).");
            }
        }

        [MenuItem(RemoveMissingScriptsInBuildScenesMenuPath)]
        public static void RemoveMissingScriptsInBuildScenesMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("Canceled build-scene missing-script cleanup (save dialog canceled).");
                return;
            }

            ProjectSceneMissingScriptCleanupResult result = RemoveMissingScriptsInBuildSettingsScenesInternal(logDetails: true, maxDetailLogs: 200);
            buildSceneMissingScriptSweepDoneThisSession = true;
            if (result.RemovedComponentCount > 0)
            {
                Debug.Log(
                    $"Removed {result.RemovedComponentCount} missing script components from {result.AffectedSceneCount} build scenes " +
                    $"(scanned={result.SceneAssetCount}).");
            }
            else
            {
                Debug.Log($"No missing scripts found in build scenes (scanned={result.SceneAssetCount}).");
            }
        }

        [MenuItem(LogBuildSceneScriptReferenceHygieneMenuPath)]
        public static void LogBuildSceneScriptReferenceHygieneMenu()
        {
            SceneScriptReferenceHygieneScanResult result = ScanBuildSceneScriptReferenceHygiene(logDetails: true, maxDetailLogs: 200);
            if (result.Passed)
            {
                Debug.Log($"Build scene script reference hygiene passed (scanned={result.SceneAssetCount}).");
                return;
            }

            Debug.LogWarning(
                "Build scene script reference hygiene failed: " +
                $"guidlessScriptRefs={result.GuidlessScriptReferenceCount}, " +
                $"duplicateCoreRuntimeComponents={result.DuplicateCoreRuntimeComponentCount}, " +
                $"scanned={result.SceneAssetCount}.");
        }

        [MenuItem(RemoveMissingScriptsInProjectAnimatorControllersMenuPath)]
        public static void RemoveMissingScriptsInProjectAnimatorControllersMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Project animator-controller missing-script cleanup is edit-mode only.");
                return;
            }

            ProjectAnimatorControllerMissingScriptCleanupResult result =
                RemoveMissingScriptsInProjectAnimatorControllersInternal(logDetails: true, maxDetailLogs: 200);
            projectAnimatorControllerMissingScriptSweepDoneThisSession = true;
            if (result.RemovedBehaviourCount > 0)
            {
                Debug.Log(
                    $"Removed {result.RemovedBehaviourCount} missing StateMachineBehaviour references from {result.AffectedAnimatorControllerCount} animator controllers " +
                    $"(scanned={result.AnimatorControllerAssetCount}).");
            }
            else
            {
                Debug.Log($"No missing StateMachineBehaviour references found in animator controllers (scanned={result.AnimatorControllerAssetCount}).");
            }
        }

        [MenuItem(LogMissingScriptsInProjectAnimatorControllersMenuPath)]
        public static void LogMissingScriptsInProjectAnimatorControllersMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Project animator-controller missing-script scan is edit-mode only.");
                return;
            }

            ProjectAnimatorControllerMissingScriptScanResult result =
                ScanMissingScriptsInProjectAnimatorControllersInternal(logDetails: true, maxDetailLogs: 200);
            if (result.MissingBehaviourCount > 0)
            {
                Debug.LogWarning(
                    $"Found {result.MissingBehaviourCount} missing StateMachineBehaviour references in {result.AffectedAnimatorControllerCount} animator controllers " +
                    $"(scanned={result.AnimatorControllerAssetCount}).");
            }
            else
            {
                Debug.Log($"No missing StateMachineBehaviour references found in animator controllers (scanned={result.AnimatorControllerAssetCount}).");
            }
        }

        [MenuItem(LogMissingScriptsInLoadedObjectsMenuPath)]
        public static void LogMissingScriptsInLoadedObjectsMenu()
        {
            LogMissingScriptsInLoadedObjectsPass(maxDetailLogs: 200);
        }

        [MenuItem(LogMissingScriptsInLoadedAnimatorControllersMenuPath)]
        public static void LogMissingScriptsInLoadedAnimatorControllersMenu()
        {
            LogMissingScriptsInLoadedAnimatorControllersPass(maxDetailLogs: 120);
        }

        [MenuItem(RemoveMissingScriptsInLoadedAnimatorControllersMenuPath)]
        public static void RemoveMissingScriptsInLoadedAnimatorControllersMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Animator controller missing-behaviour cleanup is edit-mode only.");
                return;
            }

            AnimatorMissingBehaviourRemovalResult result =
                RemoveMissingScriptsInLoadedAnimatorControllersPass(logDetails: true, maxDetailLogs: 120);
            if (result.RemovedBehaviourCount > 0)
            {
                Debug.Log(
                    $"Removed {result.RemovedBehaviourCount} missing StateMachineBehaviour references from {result.ControllerCount} animator controllers.");
            }
            else
            {
                Debug.Log("No removable missing StateMachineBehaviour references found in loaded animator controllers.");
            }
        }

        [MenuItem(RemoveMissingScriptsInLoadedObjectsMenuPath)]
        public static void RemoveMissingScriptsInLoadedObjectsMenu()
        {
            MissingScriptRemovalResult result = RemoveMissingScriptsInLoadedObjectsPass(logDetails: true, maxDetailLogs: 200);
            if (result.RemovedComponentCount > 0)
            {
                Debug.Log(
                    $"Removed {result.RemovedComponentCount} missing script components from {result.ObjectCount} loaded objects (including hidden scene objects).");
            }
            else
            {
                Debug.Log("No removable missing scripts found in loaded objects.");
            }
        }

        [MenuItem("LostBreadcrumbs/Setup/Diagnostics/TMP/Import TMP Essential Resources")]
        public static void ImportTmpEssentialResourcesMenu()
        {
            if (TryImportTmpEssentialResources())
            {
                Debug.Log("TMP essential resources import executed.");
                return;
            }

            Debug.LogWarning("TMP resource importer is unavailable. Open Window > TextMeshPro > Import TMP Essential Resources.");
        }

        [MenuItem("LostBreadcrumbs/Setup/Diagnostics/TMP/Assign TMP Default Font (First Found)")]
        public static void AssignTmpDefaultFontFromProjectMenu()
        {
            if (TryAssignTmpDefaultFontFromProject(out string fontAssetPath))
            {
                bool assigned = AssignMissingTmpFontsInOpenScenes(logDetails: true, out int assignedCount, out string resolvedFontPath);
                string activeFontPath = !string.IsNullOrWhiteSpace(resolvedFontPath) ? resolvedFontPath : fontAssetPath;
                Debug.Log($"TMP default font asset assigned: {activeFontPath}. Missing TMP font assignments fixed={assignedCount}, attempted={assigned}.");
                return;
            }

            Debug.LogWarning("Failed to assign TMP default font asset. Ensure TextMeshPro package and at least one TMP_FontAsset exist.");
        }

        [MenuItem("LostBreadcrumbs/Setup/Diagnostics/Repair EditorUserBuildSettings Access Issue")]
        public static void RepairEditorUserBuildSettingsAccessIssueMenu()
        {
            bool repaired = TryRepairEditorUserBuildSettingsAccessIssue(out string detail);
            if (repaired)
            {
                Debug.Log($"EditorUserBuildSettings repair applied. {detail}");
            }
            else
            {
                Debug.LogWarning($"EditorUserBuildSettings repair made no changes. {detail}");
            }
        }


        [MenuItem("LostBreadcrumbs/Audio/Apply Preset/Balanced (Recommended)")]
        public static void ApplyAudioPresetBalanced()
        {
            ApplyAudioPreset(AudioQuickPreset.Balanced, "Balanced");
        }

        [MenuItem("LostBreadcrumbs/Audio/Apply Preset/Intense Combat")]
        public static void ApplyAudioPresetIntenseCombat()
        {
            ApplyAudioPreset(AudioQuickPreset.IntenseCombat, "IntenseCombat");
        }

        [MenuItem("LostBreadcrumbs/Audio/Apply Preset/Chill Exploration")]
        public static void ApplyAudioPresetChillExploration()
        {
            ApplyAudioPreset(AudioQuickPreset.ChillExploration, "ChillExploration");
        }

        [MenuItem("LostBreadcrumbs/Audio/Dummy Loops/Force Disable")]
        public static void ForceDisableDummyLoops()
        {
            SetDummyLoopForceDisable(true);
        }

        [MenuItem("LostBreadcrumbs/Audio/Dummy Loops/Allow Fallback")]
        public static void AllowDummyLoopFallback()
        {
            SetDummyLoopForceDisable(false);
        }

        [MenuItem("LostBreadcrumbs/Gameplay/Loadout/Select/Balanced (Recommended)")]
        public static void SelectRunLoadoutBalanced()
        {
            SetRunLoadout(RunLoadoutId.Balanced, true, "Balanced");
        }

        [MenuItem("LostBreadcrumbs/Gameplay/Loadout/Select/Pathfinder")]
        public static void SelectRunLoadoutPathfinder()
        {
            SetRunLoadout(RunLoadoutId.Pathfinder, true, "Pathfinder");
        }

        [MenuItem("LostBreadcrumbs/Gameplay/Loadout/Select/Echo Specialist")]
        public static void SelectRunLoadoutEchoSpecialist()
        {
            SetRunLoadout(RunLoadoutId.EchoSpecialist, true, "EchoSpecialist");
        }

        [MenuItem("LostBreadcrumbs/Gameplay/Loadout/Select/Shadow Runner")]
        public static void SelectRunLoadoutShadowRunner()
        {
            SetRunLoadout(RunLoadoutId.ShadowRunner, true, "ShadowRunner");
        }

        [MenuItem("LostBreadcrumbs/Gameplay/Map Preset/Compact")]
        public static void SetMapPresetCompact()
        {
            SetMapPreset(MapTuningPreset.Compact, "Compact");
        }

        [MenuItem("LostBreadcrumbs/Gameplay/Map Preset/Standard (Recommended)")]
        public static void SetMapPresetStandard()
        {
            SetMapPreset(MapTuningPreset.Standard, "Standard");
        }

        [MenuItem("LostBreadcrumbs/Gameplay/Map Preset/Expansive")]
        public static void SetMapPresetExpansive()
        {
            SetMapPreset(MapTuningPreset.Expansive, "Expansive");
        }

        [MenuItem("LostBreadcrumbs/Gameplay/Map Preset/Regenerate Current Stage")]
        public static void RegenerateCurrentStageFromMapPresetMenu()
        {
            MapSystem mapSystem = UnityEngine.Object.FindFirstObjectByType<MapSystem>();
            if (mapSystem == null)
            {
                Debug.LogWarning("MapSystem not found in active scene.");
                return;
            }

            mapSystem.GenerateCurrentStage();
            EditorUtility.SetDirty(mapSystem);

            Scene targetScene = mapSystem.gameObject.scene;
            if (targetScene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(targetScene);
            }

            Debug.Log($"Map regenerated for stage {mapSystem.CurrentStage}.", mapSystem);
        }

        [MenuItem("LostBreadcrumbs/Gameplay/Loadout/Unlock Selection")]
        public static void UnlockRunLoadoutSelectionMenu()
        {
            RunLoadoutDirector runLoadout = UnityEngine.Object.FindFirstObjectByType<RunLoadoutDirector>();
            if (runLoadout == null)
            {
                Debug.LogWarning("RunLoadoutDirector not found in active scene.");
                return;
            }

            runLoadout.SelectLoadoutForEditor(runLoadout.SelectedLoadout, false);
            EditorUtility.SetDirty(runLoadout);

            Scene targetScene = runLoadout.gameObject.scene;
            if (targetScene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(targetScene);
            }

            Debug.Log("Run loadout selection unlocked.", runLoadout);
        }

        [MenuItem("LostBreadcrumbs/Gameplay/Regression/Run Runtime Checklist")]
        public static void RunRuntimeRegressionChecklistMenu()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("Enter Play Mode to run regression checklist.");
                return;
            }

            RegressionChecklistRunner runner = UnityEngine.Object.FindFirstObjectByType<RegressionChecklistRunner>();
            if (runner == null)
            {
                Debug.LogWarning("RegressionChecklistRunner not found in active scene.");
                return;
            }

            runner.RunChecklistNow();
            Debug.Log("Regression checklist started.", runner);
        }

        [MenuItem("LostBreadcrumbs/Gameplay/Regression/Apply Matrix Final Lock Policy")]
        public static void ApplyMatrixFinalLockPolicyMenu()
        {
            RegressionChecklistRunner runner = UnityEngine.Object.FindFirstObjectByType<RegressionChecklistRunner>();
            if (runner == null)
            {
                Debug.LogWarning("RegressionChecklistRunner not found in active scene.");
                return;
            }

            runner.ApplyMatrixFinalLockPolicyForEditor();
            EditorUtility.SetDirty(runner);

            Scene targetScene = runner.gameObject.scene;
            if (targetScene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(targetScene);
            }

            Debug.Log("Applied matrix final lock policy.", runner);
        }

        [MenuItem("LostBreadcrumbs/Gameplay/Regression/Run Release Candidate Soak Pass")]
        public static void RunReleaseCandidateSoakPassMenu()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("Enter Play Mode to run release soak pass.");
                return;
            }

            RegressionChecklistRunner runner = UnityEngine.Object.FindFirstObjectByType<RegressionChecklistRunner>();
            if (runner == null)
            {
                Debug.LogWarning("RegressionChecklistRunner not found in active scene.");
                return;
            }

            runner.RunReleaseCandidateSoakPassNow();
            Debug.Log("Release candidate soak pass started.", runner);
        }

        [MenuItem("LostBreadcrumbs/Gameplay/Regression/Run Release Soak + Write Report File (Auto)")]
        public static void RunReleaseSoakAndWriteReportAutoMenu()
        {
            EnsureAutoSoakHooks();
            autoSoakFlowPendingRun = true;
            autoSoakFlowPendingReportWrite = true;
            autoSoakFlowMissingRunnerLogged = false;
            autoSoakFlowStartedAt = EditorApplication.timeSinceStartup;
            autoSoakFlowExpectedRunCount = 0;
            SaveAutoSoakSessionState();
            BeginAutoSoakTraceSession();
            TraceAutoSoak("Menu triggered: Run Release Soak + Write Report File (Auto).");

            int mapConfigFixedCount = FixMissingMapConfigInOpenScenes();
            int listenerAdjustedCount = EnsureSingleActiveAudioListenerInOpenScenes();
            AnimatorMissingBehaviourRemovalResult animatorMissingBehavioursRemoved =
                RemoveMissingScriptsInLoadedAnimatorControllersPass(logDetails: true, maxDetailLogs: 80);
            ProjectAnimatorControllerMissingScriptCleanupResult animatorProjectMissingBehavioursRemoved = default;
            bool animatorProjectSweepExecuted = false;
            if (!EditorApplication.isPlaying && !projectAnimatorControllerMissingScriptSweepDoneThisSession)
            {
                animatorProjectMissingBehavioursRemoved = RemoveMissingScriptsInProjectAnimatorControllersInternal(logDetails: false, maxDetailLogs: 0);
                projectAnimatorControllerMissingScriptSweepDoneThisSession = true;
                animatorProjectSweepExecuted = true;
            }
            ProjectAnimatorControllerMissingScriptScanResult animatorProjectMissingBehavioursRemaining = default;
            bool animatorProjectRemainingScanExecuted = false;
            if (!EditorApplication.isPlaying)
            {
                animatorProjectMissingBehavioursRemaining =
                    ScanMissingScriptsInProjectAnimatorControllersInternal(logDetails: false, maxDetailLogs: 0);
                animatorProjectRemainingScanExecuted = true;
            }
            MissingScriptRemovalResult missingScriptsRemoved = RemoveMissingScriptsInOpenScenesInternal(logDetails: true);
            ProjectPrefabMissingScriptCleanupResult prefabMissingScriptsRemoved = default;
            bool prefabSweepExecuted = false;
            if (!projectPrefabMissingScriptSweepDoneThisSession)
            {
                prefabMissingScriptsRemoved = RemoveMissingScriptsInProjectPrefabsInternal(logDetails: false, maxDetailLogs: 0);
                projectPrefabMissingScriptSweepDoneThisSession = true;
                prefabSweepExecuted = true;
            }

            ProjectSceneMissingScriptCleanupResult buildSceneMissingScriptsRemoved = default;
            bool buildSceneSweepExecuted = false;
            if (!buildSceneMissingScriptSweepDoneThisSession)
            {
                buildSceneMissingScriptsRemoved = RemoveMissingScriptsInBuildSettingsScenesInternal(logDetails: false, maxDetailLogs: 0);
                buildSceneMissingScriptSweepDoneThisSession = true;
                buildSceneSweepExecuted = true;
            }
            SceneScriptReferenceHygieneScanResult buildSceneScriptHygiene =
                ScanBuildSceneScriptReferenceHygiene(logDetails: false, maxDetailLogs: 0);

            bool tmpFontFixAttempted = AssignMissingTmpFontsInOpenScenes(logDetails: true, out int tmpFontsAssignedCount, out string tmpDefaultFontPath);
            TmpFontScanResult tmpFontsRemaining = ScanMissingTmpFontAssignmentsInOpenScenes(logDetails: false);
            bool editorBuildSettingsRepaired = TryRepairEditorUserBuildSettingsAccessIssue(out string buildSettingsRepairDetail);
            if (mapConfigFixedCount > 0 || listenerAdjustedCount > 0 || animatorMissingBehavioursRemoved.RemovedBehaviourCount > 0 || animatorProjectMissingBehavioursRemoved.RemovedBehaviourCount > 0 || missingScriptsRemoved.RemovedComponentCount > 0 || prefabMissingScriptsRemoved.RemovedComponentCount > 0 || buildSceneMissingScriptsRemoved.RemovedComponentCount > 0 || tmpFontsAssignedCount > 0)
            {
                TraceAutoSoak(
                    $"Preflight fixes applied. mapConfigFixed={mapConfigFixedCount}, audioListenersAdjusted={listenerAdjustedCount}, " +
                    $"animatorMissingBehavioursRemoved={animatorMissingBehavioursRemoved.RemovedBehaviourCount}/{animatorMissingBehavioursRemoved.ControllerCount} controllers, " +
                    $"animatorProjectMissingBehavioursRemoved={animatorProjectMissingBehavioursRemoved.RemovedBehaviourCount}/{animatorProjectMissingBehavioursRemoved.AffectedAnimatorControllerCount} controllers (scanned={animatorProjectMissingBehavioursRemoved.AnimatorControllerAssetCount}, executed={animatorProjectSweepExecuted}), " +
                    $"animatorProjectMissingBehavioursRemaining={animatorProjectMissingBehavioursRemaining.MissingBehaviourCount}/{animatorProjectMissingBehavioursRemaining.AffectedAnimatorControllerCount} controllers (scanExecuted={animatorProjectRemainingScanExecuted}), " +
                    $"missingScriptsRemoved={missingScriptsRemoved.RemovedComponentCount}/{missingScriptsRemoved.ObjectCount} objects, " +
                    $"prefabMissingScriptsRemoved={prefabMissingScriptsRemoved.RemovedComponentCount}/{prefabMissingScriptsRemoved.AffectedPrefabCount} prefabs (scanned={prefabMissingScriptsRemoved.PrefabAssetCount}, executed={prefabSweepExecuted}), " +
                    $"buildSceneMissingScriptsRemoved={buildSceneMissingScriptsRemoved.RemovedComponentCount}/{buildSceneMissingScriptsRemoved.AffectedSceneCount} scenes (scanned={buildSceneMissingScriptsRemoved.SceneAssetCount}, executed={buildSceneSweepExecuted}), " +
                    $"buildSceneScriptHygiene(guidlessScriptRefs={buildSceneScriptHygiene.GuidlessScriptReferenceCount}, duplicateCoreRuntimeComponents={buildSceneScriptHygiene.DuplicateCoreRuntimeComponentCount}, scanned={buildSceneScriptHygiene.SceneAssetCount}), " +
                    $"tmpFontsAssigned={tmpFontsAssignedCount}, tmpDefaultFontPath='{tmpDefaultFontPath}'.");
            }
            else
            {
                TraceAutoSoak(
                    $"Preflight check complete (no modifications). " +
                    $"animatorProjectSweepExecuted={animatorProjectSweepExecuted}, " +
                    $"animatorProjectRemainingScanExecuted={animatorProjectRemainingScanExecuted}, " +
                    $"prefabSweepExecuted={prefabSweepExecuted}, buildSceneSweepExecuted={buildSceneSweepExecuted}, " +
                    $"buildSceneScriptHygiene(guidlessScriptRefs={buildSceneScriptHygiene.GuidlessScriptReferenceCount}, duplicateCoreRuntimeComponents={buildSceneScriptHygiene.DuplicateCoreRuntimeComponentCount}, scanned={buildSceneScriptHygiene.SceneAssetCount}), " +
                    $"tmpFixAttempted={tmpFontFixAttempted}.");
            }
            string preflightSummary = BuildAutoSoakPreflightSummary(
                "Preflight summary",
                mapConfigFixedCount,
                listenerAdjustedCount,
                animatorMissingBehavioursRemoved,
                animatorProjectMissingBehavioursRemoved,
                animatorProjectSweepExecuted,
                animatorProjectMissingBehavioursRemaining,
                animatorProjectRemainingScanExecuted,
                missingScriptsRemoved,
                prefabMissingScriptsRemoved,
                prefabSweepExecuted,
                buildSceneMissingScriptsRemoved,
                buildSceneSweepExecuted,
                buildSceneScriptHygiene,
                tmpFontsAssignedCount,
                tmpFontsRemaining,
                tmpFontFixAttempted,
                tmpDefaultFontPath,
                editorBuildSettingsRepaired);
            WriteAutoSoakPreflightSummary(preflightSummary);
            if (!buildSceneScriptHygiene.Passed)
            {
                TraceAutoSoak(
                    $"Build scene script reference hygiene failed. guidlessScriptRefs={buildSceneScriptHygiene.GuidlessScriptReferenceCount}, duplicateCoreRuntimeComponents={buildSceneScriptHygiene.DuplicateCoreRuntimeComponentCount}, scanned={buildSceneScriptHygiene.SceneAssetCount}.",
                    warning: true);
            }
            if (animatorProjectRemainingScanExecuted && animatorProjectMissingBehavioursRemaining.MissingBehaviourCount > 0)
            {
                TraceAutoSoak(
                    $"Animator preflight still has warnings. remainingMissingBehaviours={animatorProjectMissingBehavioursRemaining.MissingBehaviourCount}, affectedControllers={animatorProjectMissingBehavioursRemaining.AffectedAnimatorControllerCount}.",
                    warning: true);
            }
            if (tmpFontFixAttempted && (tmpFontsRemaining.MissingFontCount > 0 || tmpFontsRemaining.DefaultFontMissing))
            {
                TraceAutoSoak(
                    $"TMP preflight still has warnings. remainingMissingFonts={tmpFontsRemaining.MissingFontCount}, defaultFontMissing={tmpFontsRemaining.DefaultFontMissing}.",
                    warning: true);
            }
            if (editorBuildSettingsRepaired)
            {
                TraceAutoSoak($"Preflight EditorUserBuildSettings repair: {buildSettingsRepairDetail}");
            }

            if (!buildSceneScriptHygiene.Passed)
            {
                TraceAutoSoak("Auto soak flow aborted before Play Mode because build scene script reference hygiene failed.", warning: true);
                ClearAutoSoakState();
                ExitAutoSoakBatchMode(1);
                return;
            }

            if (EditorApplication.isPlaying)
            {
                StartAutoSoakInPlayMode();
                return;
            }

            TraceAutoSoak("Entering Play Mode for auto soak flow (Run -> Write Report File).");
            EditorApplication.isPlaying = true;
        }

        [MenuItem("LostBreadcrumbs/Gameplay/Regression/Run Auto Soak Preflight Only")]
        public static void RunAutoSoakPreflightOnlyMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Auto soak preflight-only check is edit-mode only.");
                return;
            }

            BeginAutoSoakTraceSession();
            TraceAutoSoak("Menu triggered: Run Auto Soak Preflight Only.");

            int mapConfigFixedCount = FixMissingMapConfigInOpenScenes();
            int listenerAdjustedCount = EnsureSingleActiveAudioListenerInOpenScenes();
            AnimatorMissingBehaviourRemovalResult animatorMissingBehavioursRemoved =
                RemoveMissingScriptsInLoadedAnimatorControllersPass(logDetails: true, maxDetailLogs: 80);
            ProjectAnimatorControllerMissingScriptCleanupResult animatorProjectMissingBehavioursRemoved =
                RemoveMissingScriptsInProjectAnimatorControllersInternal(logDetails: false, maxDetailLogs: 0);
            projectAnimatorControllerMissingScriptSweepDoneThisSession = true;
            ProjectAnimatorControllerMissingScriptScanResult animatorProjectMissingBehavioursRemaining =
                ScanMissingScriptsInProjectAnimatorControllersInternal(logDetails: false, maxDetailLogs: 0);
            MissingScriptRemovalResult missingScriptsRemoved = RemoveMissingScriptsInOpenScenesInternal(logDetails: true);
            ProjectPrefabMissingScriptCleanupResult prefabMissingScriptsRemoved =
                RemoveMissingScriptsInProjectPrefabsInternal(logDetails: false, maxDetailLogs: 0);
            projectPrefabMissingScriptSweepDoneThisSession = true;
            ProjectSceneMissingScriptCleanupResult buildSceneMissingScriptsRemoved =
                RemoveMissingScriptsInBuildSettingsScenesInternal(logDetails: false, maxDetailLogs: 0);
            buildSceneMissingScriptSweepDoneThisSession = true;
            SceneScriptReferenceHygieneScanResult buildSceneScriptHygiene =
                ScanBuildSceneScriptReferenceHygiene(logDetails: false, maxDetailLogs: 0);

            bool tmpFontFixAttempted = AssignMissingTmpFontsInOpenScenes(logDetails: true, out int tmpFontsAssignedCount, out string tmpDefaultFontPath);
            TmpFontScanResult tmpFontsRemaining = ScanMissingTmpFontAssignmentsInOpenScenes(logDetails: false);
            bool editorBuildSettingsRepaired = TryRepairEditorUserBuildSettingsAccessIssue(out string buildSettingsRepairDetail);
            string summary = BuildAutoSoakPreflightSummary(
                "Preflight-only summary",
                mapConfigFixedCount,
                listenerAdjustedCount,
                animatorMissingBehavioursRemoved,
                animatorProjectMissingBehavioursRemoved,
                true,
                animatorProjectMissingBehavioursRemaining,
                true,
                missingScriptsRemoved,
                prefabMissingScriptsRemoved,
                true,
                buildSceneMissingScriptsRemoved,
                true,
                buildSceneScriptHygiene,
                tmpFontsAssignedCount,
                tmpFontsRemaining,
                tmpFontFixAttempted,
                tmpDefaultFontPath,
                editorBuildSettingsRepaired);

            WriteAutoSoakPreflightSummary(summary);

            bool hasWarning = HasAutoSoakPreflightWarnings(
                buildSceneScriptHygiene,
                animatorProjectMissingBehavioursRemaining,
                tmpFontsRemaining);

            TraceAutoSoak(summary, warning: hasWarning);
            if (editorBuildSettingsRepaired)
            {
                TraceAutoSoak($"Preflight-only EditorUserBuildSettings repair: {buildSettingsRepairDetail}");
            }

            if (hasWarning)
            {
                Debug.LogWarning($"Auto soak preflight-only check completed with warnings. {summary}");
            }
            else
            {
                Debug.Log($"Auto soak preflight-only check passed. {summary}");
            }
        }

        [MenuItem("LostBreadcrumbs/Gameplay/Regression/Apply Release Checklist Freeze Defaults")]
        public static void ApplyReleaseChecklistFreezeDefaultsMenu()
        {
            RegressionChecklistRunner runner = UnityEngine.Object.FindFirstObjectByType<RegressionChecklistRunner>();
            if (runner == null)
            {
                Debug.LogWarning("RegressionChecklistRunner not found in active scene.");
                return;
            }

            runner.ApplyReleaseChecklistFreezeDefaultsForEditor();
            EditorUtility.SetDirty(runner);

            Scene targetScene = runner.gameObject.scene;
            if (targetScene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(targetScene);
            }

            Debug.Log("Applied release checklist freeze defaults.", runner);
        }

        [MenuItem("LostBreadcrumbs/Gameplay/Regression/Log Release Checklist Gate")]
        public static void LogReleaseChecklistGateMenu()
        {
            RegressionChecklistRunner runner = UnityEngine.Object.FindFirstObjectByType<RegressionChecklistRunner>();
            if (runner == null)
            {
                Debug.LogWarning("RegressionChecklistRunner not found in active scene.");
                return;
            }

            runner.LogReleaseChecklistGateForEditor();
        }

        [MenuItem("LostBreadcrumbs/Gameplay/Regression/Log Release Soak Action Plan")]
        public static void LogReleaseSoakActionPlanMenu()
        {
            RegressionChecklistRunner runner = UnityEngine.Object.FindFirstObjectByType<RegressionChecklistRunner>();
            if (runner == null)
            {
                Debug.LogWarning("RegressionChecklistRunner not found in active scene.");
                return;
            }

            runner.LogReleaseSoakActionPlanForEditor();
        }

        [MenuItem("LostBreadcrumbs/Gameplay/Regression/Log Release Soak Detailed Report")]
        public static void LogReleaseSoakDetailedReportMenu()
        {
            RegressionChecklistRunner runner = UnityEngine.Object.FindFirstObjectByType<RegressionChecklistRunner>();
            if (runner == null)
            {
                Debug.LogWarning("RegressionChecklistRunner not found in active scene.");
                return;
            }

            runner.LogReleaseSoakDetailedReportForEditor();
        }

        [MenuItem("LostBreadcrumbs/Gameplay/Regression/Write Release Soak Detailed Report File")]
        public static void WriteReleaseSoakDetailedReportFileMenu()
        {
            RegressionChecklistRunner runner = UnityEngine.Object.FindFirstObjectByType<RegressionChecklistRunner>();
            if (runner == null)
            {
                Debug.LogWarning("RegressionChecklistRunner not found in active scene.");
                return;
            }

            runner.WriteReleaseSoakDetailedReportFileForEditor();
        }

        [MenuItem("LostBreadcrumbs/Gameplay/Regression/Log Auto Soak Trace Status")]
        public static void LogAutoSoakTraceStatusMenu()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                Debug.LogWarning("Project root path is unavailable.");
                return;
            }

            string tracePath = Path.Combine(projectRoot, AutoSoakTraceRelativePath);
            string statusPath = Path.Combine(projectRoot, AutoSoakStatusRelativePath);
            string preflightSummaryPath = Path.Combine(projectRoot, AutoSoakPreflightSummaryRelativePath);
            if (!File.Exists(tracePath) && !File.Exists(statusPath) && !File.Exists(preflightSummaryPath))
            {
                Debug.LogWarning("No auto soak trace files found yet.");
                return;
            }

            string preflightSummaryText = File.Exists(preflightSummaryPath)
                ? File.ReadAllText(preflightSummaryPath)
                : "(preflight summary file missing)";

            string statusText = File.Exists(statusPath)
                ? File.ReadAllText(statusPath)
                : "(status file missing)";

            string traceTail = "(trace file missing)";
            if (File.Exists(tracePath))
            {
                string[] lines = File.ReadAllLines(tracePath);
                int tailCount = Math.Min(8, lines.Length);
                int start = Math.Max(0, lines.Length - tailCount);
                traceTail = string.Join(Environment.NewLine, lines, start, tailCount);
            }

            Debug.Log(
                $"Auto soak preflight summary file: {preflightSummaryPath}{Environment.NewLine}" +
                $"Auto soak status file: {statusPath}{Environment.NewLine}" +
                $"Auto soak trace file: {tracePath}{Environment.NewLine}" +
                $"Preflight summary:{Environment.NewLine}{preflightSummaryText}{Environment.NewLine}" +
                $"Last status:{Environment.NewLine}{statusText}{Environment.NewLine}" +
                $"Trace tail:{Environment.NewLine}{traceTail}");
        }

        private static void ApplyAudioPreset(AudioQuickPreset preset, string label)
        {
            AudioManager audioManager = UnityEngine.Object.FindFirstObjectByType<AudioManager>();
            if (audioManager == null)
            {
                Debug.LogWarning("AudioManager not found in active scene.");
                return;
            }

            audioManager.ApplyQuickPreset(preset);
            EditorUtility.SetDirty(audioManager);

            Scene targetScene = audioManager.gameObject.scene;
            if (targetScene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(targetScene);
            }

            Debug.Log($"Applied audio preset: {label}", audioManager);
        }

        private static void SetDummyLoopForceDisable(bool forceDisable)
        {
            AudioDummyLoopRuntime dummyLoop = UnityEngine.Object.FindFirstObjectByType<AudioDummyLoopRuntime>();
            if (dummyLoop == null)
            {
                Debug.LogWarning("AudioDummyLoopRuntime not found in active scene.");
                return;
            }

            dummyLoop.SetForceDisableDummyLoopsForEditor(forceDisable);
            EditorUtility.SetDirty(dummyLoop);

            Scene targetScene = dummyLoop.gameObject.scene;
            if (targetScene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(targetScene);
            }

            Debug.Log(forceDisable ? "Dummy loop fallback disabled." : "Dummy loop fallback enabled.", dummyLoop);
        }

        private static void SetMapPreset(MapTuningPreset preset, string label)
        {
            MapTuningDebugController mapTuning = UnityEngine.Object.FindFirstObjectByType<MapTuningDebugController>();
            if (mapTuning == null)
            {
                Debug.LogWarning("MapTuningDebugController not found in active scene.");
                return;
            }

            mapTuning.ApplyPresetForEditor(preset, true);
            EditorUtility.SetDirty(mapTuning);

            Scene targetScene = mapTuning.gameObject.scene;
            if (targetScene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(targetScene);
            }

            Debug.Log($"Map preset set: {label}.", mapTuning);
        }

        private static void SetRunLoadout(RunLoadoutId loadout, bool lockSelection, string label)
        {
            RunLoadoutDirector runLoadout = UnityEngine.Object.FindFirstObjectByType<RunLoadoutDirector>();
            if (runLoadout == null)
            {
                Debug.LogWarning("RunLoadoutDirector not found in active scene.");
                return;
            }

            runLoadout.SelectLoadoutForEditor(loadout, lockSelection);
            EditorUtility.SetDirty(runLoadout);

            Scene targetScene = runLoadout.gameObject.scene;
            if (targetScene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(targetScene);
            }

            Debug.Log($"Run loadout set: {label}" + (lockSelection ? " (locked)." : "."), runLoadout);
        }

        private static EnemyProfile[] LoadDefaultEnemyProfiles()
        {
            EnemyProfile[] profiles =
            {
                AssetDatabase.LoadAssetAtPath<EnemyProfile>("Assets/_Project/ScriptableObjects/Enemy/EP_Obsessive.asset"),
                AssetDatabase.LoadAssetAtPath<EnemyProfile>("Assets/_Project/ScriptableObjects/Enemy/EP_Cautious.asset"),
                AssetDatabase.LoadAssetAtPath<EnemyProfile>("Assets/_Project/ScriptableObjects/Enemy/EP_Impulsive.asset"),
                AssetDatabase.LoadAssetAtPath<EnemyProfile>("Assets/_Project/ScriptableObjects/Enemy/EP_Flanker.asset"),
                AssetDatabase.LoadAssetAtPath<EnemyProfile>("Assets/_Project/ScriptableObjects/Enemy/EP_Seeker.asset")
            };

            return profiles;
        }
        private static EnemyLearningPhaseConfig EnsureLearningPhaseConfigAsset()
        {
            EnsureFolderPath("Assets/_Project/ScriptableObjects/Enemy");

            EnemyLearningPhaseConfig config = AssetDatabase.LoadAssetAtPath<EnemyLearningPhaseConfig>(LearningConfigAssetPath);
            if (config != null)
            {
                return config;
            }

            config = ScriptableObject.CreateInstance<EnemyLearningPhaseConfig>();
            config.earlyLearningWeight = 0.25f;
            config.earlyPredictionWeight = 0.2f;
            config.midLearningWeight = 0.55f;
            config.midPredictionWeight = 0.5f;
            config.lateLearningWeight = 0.85f;
            config.latePredictionWeight = 0.8f;
            config.maxCheatCompensation = 0.9f;
            AssetDatabase.CreateAsset(config, LearningConfigAssetPath);
            return config;
        }

        private static SequentialMapConfig EnsureMapConfigAsset()
        {
            EnsureFolderPath("Assets/_Project/ScriptableObjects/Map");

            SequentialMapConfig config = AssetDatabase.LoadAssetAtPath<SequentialMapConfig>(MapConfigAssetPath);
            if (config != null)
            {
                return config;
            }

            config = ScriptableObject.CreateInstance<SequentialMapConfig>();
            AssetDatabase.CreateAsset(config, MapConfigAssetPath);
            return config;
        }


        private static RunLoadoutCatalog EnsureRunLoadoutCatalogAsset()
        {
            EnsureFolderPath("Assets/_Project/ScriptableObjects/Balance");

            RunLoadoutCatalog catalog = AssetDatabase.LoadAssetAtPath<RunLoadoutCatalog>(LoadoutCatalogAssetPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<RunLoadoutCatalog>();
                catalog.SetDefaultLoadoutsForEditor();
                AssetDatabase.CreateAsset(catalog, LoadoutCatalogAssetPath);
                return catalog;
            }

            if (catalog.LoadoutCount <= 0)
            {
                catalog.SetDefaultLoadoutsForEditor();
                EditorUtility.SetDirty(catalog);
            }

            return catalog;
        }
        private static GameObject EnsureRoot(string name)
        {
            GameObject root = GameObject.Find(name);
            if (root != null)
            {
                return root;
            }

            root = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(root, $"Create {name}");
            return root;
        }

        private static GameObject EnsureChild(GameObject parent, string childName)
        {
            Transform existing = parent.transform.Find(childName);
            if (existing != null)
            {
                return existing.gameObject;
            }

            GameObject child = new GameObject(childName);
            Undo.RegisterCreatedObjectUndo(child, $"Create {childName}");
            child.transform.SetParent(parent.transform, false);
            return child;
        }

        private static GameObject CreateMarker(GameObject parent, string name, AuthoringMarkerType type, Vector3 position)
        {
            GameObject marker = EnsureChild(parent, name);
            marker.transform.position = position;

            AuthoringMarkerGizmo gizmo = AddOrGet<AuthoringMarkerGizmo>(marker);
            gizmo.MarkerType = type;
            return marker;
        }

        private static void EnsureManager<T>(GameObject managersRoot, string objectName) where T : Component
        {
            GameObject managerObject = EnsureChild(managersRoot, objectName);
            AddOrGet<T>(managerObject);
        }

        private static void EnsureSystem<T>(GameObject systemsRoot, string objectName) where T : Component
        {
            GameObject systemObject = EnsureChild(systemsRoot, objectName);
            AddOrGet<T>(systemObject);
        }

        private static T AddOrGet<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            if (component == null)
            {
                component = Undo.AddComponent<T>(gameObject);
            }

            return component;
        }

        private static bool TagExists(string tag)
        {
            string[] tags = InternalEditorUtility.tags;
            for (int i = 0; i < tags.Length; i++)
            {
                if (string.Equals(tags[i], tag, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void CreateOrUpdateProfile(string assetPath, Action<EnemyProfile> configure)
        {
            EnemyProfile profile = AssetDatabase.LoadAssetAtPath<EnemyProfile>(assetPath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<EnemyProfile>();
                AssetDatabase.CreateAsset(profile, assetPath);
            }

            configure?.Invoke(profile);
            EditorUtility.SetDirty(profile);
        }

        private static void EnsureFolderPath(string fullPath)
        {
            if (AssetDatabase.IsValidFolder(fullPath))
            {
                return;
            }

            string[] parts = fullPath.Split('/');
            if (parts.Length < 2)
            {
                return;
            }

            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static int FixMissingMapConfigInOpenScenes()
        {
            SequentialMapConfig mapConfig = EnsureMapConfigAsset();
            if (mapConfig == null)
            {
                Debug.LogWarning("Failed to resolve default SequentialMapConfig asset.");
                return 0;
            }

            int fixedCount = 0;
            bool canPersistSceneChanges = !EditorApplication.isPlaying;
            List<GameObject> sceneObjects = new(512);
            CollectOpenSceneGameObjects(sceneObjects);
            for (int i = 0; i < sceneObjects.Count; i++)
            {
                GameObject gameObject = sceneObjects[i];
                if (gameObject == null)
                {
                    continue;
                }

                MapSystem mapSystem = gameObject.GetComponent<MapSystem>();
                if (mapSystem == null || mapSystem.Config != null)
                {
                    continue;
                }

                mapSystem.SetConfigForEditor(mapConfig);
                if (canPersistSceneChanges)
                {
                    EditorUtility.SetDirty(mapSystem);
                    MarkSceneDirtyIfValid(gameObject.scene);
                }
                fixedCount++;
                Debug.Log($"Assigned default map config to {BuildHierarchyPath(gameObject.transform)}", mapSystem);
            }

            return fixedCount;
        }

        private static int EnsureSingleActiveAudioListenerInOpenScenes()
        {
            List<AudioListener> listeners = new(16);
            if (EditorApplication.isPlaying)
            {
                AudioListener[] loadedListeners = Resources.FindObjectsOfTypeAll<AudioListener>();
                for (int i = 0; i < loadedListeners.Length; i++)
                {
                    AudioListener listener = loadedListeners[i];
                    if (listener == null)
                    {
                        continue;
                    }

                    if (EditorUtility.IsPersistent(listener))
                    {
                        continue;
                    }

                    listeners.Add(listener);
                }
            }
            else
            {
                List<GameObject> sceneObjects = new(512);
                CollectOpenSceneGameObjects(sceneObjects);

                for (int i = 0; i < sceneObjects.Count; i++)
                {
                    GameObject gameObject = sceneObjects[i];
                    if (gameObject == null)
                    {
                        continue;
                    }

                    AudioListener listener = gameObject.GetComponent<AudioListener>();
                    if (listener != null)
                    {
                        listeners.Add(listener);
                    }
                }
            }

            if (listeners.Count <= 0)
            {
                return 0;
            }

            AudioListener keepListener = null;
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                keepListener = mainCamera.GetComponent<AudioListener>();
            }

            if (keepListener == null)
            {
                for (int i = 0; i < listeners.Count; i++)
                {
                    AudioListener listener = listeners[i];
                    if (listener != null && listener.enabled)
                    {
                        keepListener = listener;
                        break;
                    }
                }
            }

            keepListener ??= listeners[0];

            int adjustedCount = 0;
            bool canPersistSceneChanges = !EditorApplication.isPlaying;
            if (keepListener != null && !keepListener.enabled)
            {
                if (canPersistSceneChanges)
                {
                    Undo.RecordObject(keepListener, "Enable primary AudioListener");
                }

                keepListener.enabled = true;
                if (canPersistSceneChanges)
                {
                    EditorUtility.SetDirty(keepListener);
                    MarkSceneDirtyIfValid(keepListener.gameObject.scene);
                }

                adjustedCount++;
            }

            for (int i = 0; i < listeners.Count; i++)
            {
                AudioListener listener = listeners[i];
                if (listener == null || listener == keepListener || !listener.enabled)
                {
                    continue;
                }

                if (canPersistSceneChanges)
                {
                    Undo.RecordObject(listener, "Disable extra AudioListener");
                }

                listener.enabled = false;
                if (canPersistSceneChanges)
                {
                    EditorUtility.SetDirty(listener);
                    MarkSceneDirtyIfValid(listener.gameObject.scene);
                }

                adjustedCount++;
                Debug.LogWarning($"Disabled extra AudioListener on {BuildHierarchyPath(listener.transform)}", listener);
            }

            return adjustedCount;
        }

        private static RuntimeBindingRepairResult RepairCoreRuntimeBindingsInOpenScenes(bool logDetails, int maxDetailLogs)
        {
            int targetCount = CoreRuntimeBindingSpecs.Length;
            int foundCount = 0;
            int missingScriptRemovedCount = 0;
            int addedComponentCount = 0;
            int duplicateComponentRemovedCount = 0;
            int missingObjectCount = 0;
            int unresolvedTypeCount = 0;
            int loadedFallbackResolvedCount = 0;
            int loadedFallbackCandidateCount = 0;
            int loadedFallbackSkippedCount = 0;
            int detailLogCount = 0;
            int safeMaxDetailLogs = Mathf.Max(0, maxDetailLogs);
            bool canPersistSceneChanges = !EditorApplication.isPlaying;

            List<GameObject> sceneObjects = new(1024);
            CollectRuntimeBindingCandidateObjects(sceneObjects, includeLoadedObjects: false);
            int sceneCandidateCount = sceneObjects.Count;
            List<GameObject> loadedFallbackObjects = null;

            for (int i = 0; i < CoreRuntimeBindingSpecs.Length; i++)
            {
                RuntimeBindingSpec spec = CoreRuntimeBindingSpecs[i];
                if (string.IsNullOrWhiteSpace(spec.ObjectName) || string.IsNullOrWhiteSpace(spec.TypeName))
                {
                    continue;
                }

                Type targetType = Type.GetType($"{spec.TypeName}, {AssemblyCSharpName}", throwOnError: false)
                                  ?? FindTypeByFullName(spec.TypeName)
                                  ?? FindTypeByLooseName(spec.TypeName, preferredAssemblyName: AssemblyCSharpName);
                if (targetType == null)
                {
                    unresolvedTypeCount++;
                    if (logDetails && detailLogCount < safeMaxDetailLogs)
                    {
                        Debug.LogWarning($"Runtime binding repair: failed to resolve type '{spec.TypeName}'.");
                        detailLogCount++;
                    }

                    continue;
                }

                if (!typeof(Component).IsAssignableFrom(targetType) || targetType.IsAbstract || targetType.IsGenericTypeDefinition)
                {
                    unresolvedTypeCount++;
                    if (logDetails && detailLogCount < safeMaxDetailLogs)
                    {
                        Debug.LogWarning($"Runtime binding repair: resolved type '{targetType.FullName}' is not a concrete Component.");
                        detailLogCount++;
                    }

                    continue;
                }

                GameObject gameObject = FindRuntimeBindingTargetObject(sceneObjects, spec, targetType);
                bool resolvedFromLoadedFallback = false;
                bool canUseLoadedFallback =
                    EditorApplication.isPlaying &&
                    !IsRuntimeBindingLoadedFallbackTemporarilyDisabled();
                if (gameObject == null && EditorApplication.isPlaying && !canUseLoadedFallback)
                {
                    loadedFallbackSkippedCount++;
                }

                if (gameObject == null && canUseLoadedFallback)
                {
                    if (loadedFallbackObjects == null)
                    {
                        loadedFallbackObjects = new List<GameObject>(sceneObjects.Count + 1024);
                        loadedFallbackObjects.AddRange(sceneObjects);
                        int openSceneCandidateCount = loadedFallbackObjects.Count;
                        AppendLoadedNonPersistentObjects(loadedFallbackObjects);
                        loadedFallbackCandidateCount = Mathf.Max(0, loadedFallbackObjects.Count - openSceneCandidateCount);
                    }

                    gameObject = FindRuntimeBindingTargetObject(loadedFallbackObjects, spec, targetType);
                    resolvedFromLoadedFallback = gameObject != null;
                }

                if (gameObject == null)
                {
                    missingObjectCount++;
                    if (logDetails && detailLogCount < safeMaxDetailLogs)
                    {
                        string aliasInfo = string.IsNullOrWhiteSpace(spec.AliasName) ? "-" : spec.AliasName;
                        string parentHintInfo = string.IsNullOrWhiteSpace(spec.ParentNameHint) ? "-" : spec.ParentNameHint;
                        string hierarchyHintInfo = string.IsNullOrWhiteSpace(spec.HierarchyPathHint) ? "-" : spec.HierarchyPathHint;
                        Debug.LogWarning(
                            $"Runtime binding repair: target object '{spec.ObjectName}' not found in runtime candidates (alias='{aliasInfo}', parentHint='{parentHintInfo}', hierarchyHint='{hierarchyHintInfo}').");
                        detailLogCount++;
                    }

                    continue;
                }

                if (resolvedFromLoadedFallback)
                {
                    loadedFallbackResolvedCount++;
                    if (logDetails && detailLogCount < safeMaxDetailLogs)
                    {
                        Debug.Log(
                            $"Runtime binding repair: resolved '{spec.ObjectName}' via loaded fallback candidates -> {BuildHierarchyPath(gameObject.transform)}",
                            gameObject);
                        detailLogCount++;
                    }
                }

                foundCount++;
                int initialMissingCount = GetMissingScriptCount(gameObject);
                int removedMissing = RemoveMissingScriptsWithRetries(gameObject, initialMissingCount, maxAttempts: 4, out int remainingMissingCount);
                if (removedMissing > 0)
                {
                    missingScriptRemovedCount += removedMissing;
                    if (logDetails && detailLogCount < safeMaxDetailLogs)
                    {
                        Debug.Log(
                            $"Runtime binding repair: removed missing scripts x{removedMissing} from {BuildHierarchyPath(gameObject.transform)}.",
                            gameObject);
                        detailLogCount++;
                    }
                }
                else if (initialMissingCount > 0 && logDetails && detailLogCount < safeMaxDetailLogs)
                {
                    Debug.LogWarning(
                        $"Runtime binding repair: detected missing scripts but failed to remove on {BuildHierarchyPath(gameObject.transform)} (missing={initialMissingCount}).",
                        gameObject);
                    detailLogCount++;
                }

                if (remainingMissingCount > 0 && logDetails && detailLogCount < safeMaxDetailLogs)
                {
                    Debug.LogWarning(
                        $"Runtime binding repair: partial missing-script cleanup on {BuildHierarchyPath(gameObject.transform)} (remaining={remainingMissingCount}).",
                        gameObject);
                    detailLogCount++;
                }

                Component[] typedComponents = gameObject.GetComponents(targetType);
                if (typedComponents == null || typedComponents.Length <= 0)
                {
                    try
                    {
                        if (canPersistSceneChanges)
                        {
                            Undo.AddComponent(gameObject, targetType);
                        }
                        else
                        {
                            gameObject.AddComponent(targetType);
                        }
                    }
                    catch (Exception ex)
                    {
                        unresolvedTypeCount++;
                        if (logDetails && detailLogCount < safeMaxDetailLogs)
                        {
                            Debug.LogWarning(
                                $"Runtime binding repair: failed to add '{targetType.FullName}' on {BuildHierarchyPath(gameObject.transform)} ({ex.Message}).",
                                gameObject);
                            detailLogCount++;
                        }
                        continue;
                    }

                    if (canPersistSceneChanges)
                    {
                        EditorUtility.SetDirty(gameObject);
                        MarkSceneDirtyIfValid(gameObject.scene);
                    }
                    addedComponentCount++;
                    if (logDetails && detailLogCount < safeMaxDetailLogs)
                    {
                        Debug.Log(
                            $"Runtime binding repair: added component '{targetType.FullName}' on {BuildHierarchyPath(gameObject.transform)}.",
                            gameObject);
                        detailLogCount++;
                    }

                    typedComponents = gameObject.GetComponents(targetType);
                }

                if (typedComponents == null || typedComponents.Length <= 1)
                {
                    continue;
                }

                int removedDuplicatesForObject = 0;
                for (int componentIndex = 1; componentIndex < typedComponents.Length; componentIndex++)
                {
                    Component duplicate = typedComponents[componentIndex];
                    if (duplicate == null)
                    {
                        continue;
                    }

                    try
                    {
                        if (canPersistSceneChanges)
                        {
                            Undo.DestroyObjectImmediate(duplicate);
                        }
                        else
                        {
                            UnityEngine.Object.DestroyImmediate(duplicate);
                        }

                        duplicateComponentRemovedCount++;
                        removedDuplicatesForObject++;
                    }
                    catch (Exception ex)
                    {
                        if (logDetails && detailLogCount < safeMaxDetailLogs)
                        {
                            Debug.LogWarning(
                                $"Runtime binding repair: failed to remove duplicate '{targetType.Name}' on {BuildHierarchyPath(gameObject.transform)} ({ex.Message}).",
                                gameObject);
                            detailLogCount++;
                        }
                    }
                }

                if (removedDuplicatesForObject > 0)
                {
                    if (canPersistSceneChanges)
                    {
                        EditorUtility.SetDirty(gameObject);
                        MarkSceneDirtyIfValid(gameObject.scene);
                    }
                    if (logDetails && detailLogCount < safeMaxDetailLogs)
                    {
                        Debug.Log(
                            $"Runtime binding repair: removed duplicate '{targetType.Name}' components on {BuildHierarchyPath(gameObject.transform)} (removed={removedDuplicatesForObject}).",
                            gameObject);
                        detailLogCount++;
                    }
                }
            }

            if (loadedFallbackResolvedCount > 0 && logDetails)
            {
                Debug.Log($"Runtime binding repair: loaded fallback resolved targets={loadedFallbackResolvedCount}.");
            }
            if (loadedFallbackCandidateCount > 0 && logDetails)
            {
                Debug.Log($"Runtime binding repair: loaded fallback candidate objects={loadedFallbackCandidateCount}.");
            }

            return new RuntimeBindingRepairResult(
                targetCount,
                foundCount,
                missingScriptRemovedCount,
                addedComponentCount,
                duplicateComponentRemovedCount,
                missingObjectCount,
                unresolvedTypeCount,
                loadedFallbackResolvedCount,
                loadedFallbackCandidateCount,
                sceneCandidateCount,
                loadedFallbackSkippedCount);
        }

        private static GameObject FindRuntimeBindingTargetObject(IReadOnlyList<GameObject> candidates, RuntimeBindingSpec spec, Type targetType)
        {
            if (candidates == null || candidates.Count <= 0)
            {
                return null;
            }

            GameObject gameObject = null;
            if (!string.IsNullOrWhiteSpace(spec.HierarchyPathHint))
            {
                gameObject = FindFirstGameObjectByHierarchyPathSuffix(candidates, spec.HierarchyPathHint);
            }

            if (gameObject == null)
            {
                gameObject = FindFirstGameObjectByName(candidates, spec.ObjectName);
            }

            if (gameObject == null && !string.IsNullOrWhiteSpace(spec.AliasName))
            {
                gameObject = FindFirstGameObjectByName(candidates, spec.AliasName);
            }

            if (gameObject == null && !string.IsNullOrWhiteSpace(spec.ParentNameHint))
            {
                gameObject = FindFirstGameObjectByNameUnderAncestor(candidates, spec.ObjectName, spec.ParentNameHint);
                if (gameObject == null && !string.IsNullOrWhiteSpace(spec.AliasName))
                {
                    gameObject = FindFirstGameObjectByNameUnderAncestor(candidates, spec.AliasName, spec.ParentNameHint);
                }
            }

            if (gameObject == null)
            {
                gameObject = FindBestGameObjectByComponentType(candidates, targetType, spec);
            }

            return gameObject;
        }

        private static GameObject FindFirstGameObjectByName(IReadOnlyList<GameObject> sceneObjects, string objectName)
        {
            if (sceneObjects == null || sceneObjects.Count <= 0 || string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            for (int i = 0; i < sceneObjects.Count; i++)
            {
                GameObject sceneObject = sceneObjects[i];
                if (sceneObject == null)
                {
                    continue;
                }

                if (string.Equals(sceneObject.name, objectName, StringComparison.Ordinal))
                {
                    return sceneObject;
                }
            }

            for (int i = 0; i < sceneObjects.Count; i++)
            {
                GameObject sceneObject = sceneObjects[i];
                if (sceneObject == null)
                {
                    continue;
                }

                if (string.Equals(sceneObject.name, objectName, StringComparison.OrdinalIgnoreCase))
                {
                    return sceneObject;
                }
            }

            for (int i = 0; i < sceneObjects.Count; i++)
            {
                GameObject sceneObject = sceneObjects[i];
                if (sceneObject == null)
                {
                    continue;
                }

                string sceneObjectName = sceneObject.name;
                if (!sceneObjectName.StartsWith(objectName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (sceneObjectName.Length <= objectName.Length)
                {
                    continue;
                }

                char suffixStart = sceneObjectName[objectName.Length];
                if (suffixStart == ' ' || suffixStart == '(' || suffixStart == '_')
                {
                    return sceneObject;
                }
            }

            string normalizedTargetName = NormalizeObjectNameForMatching(objectName);
            if (string.IsNullOrEmpty(normalizedTargetName))
            {
                return null;
            }

            GameObject bestNormalizedExactMatch = null;
            int bestNormalizedExactDepth = int.MaxValue;
            int bestNormalizedExactNameLength = int.MaxValue;
            GameObject bestNormalizedContainsMatch = null;
            int bestNormalizedContainsDepth = int.MaxValue;
            int bestNormalizedContainsNameLength = int.MaxValue;

            for (int i = 0; i < sceneObjects.Count; i++)
            {
                GameObject sceneObject = sceneObjects[i];
                if (sceneObject == null)
                {
                    continue;
                }

                string normalizedSceneObjectName = NormalizeObjectNameForMatching(sceneObject.name);
                if (string.IsNullOrEmpty(normalizedSceneObjectName))
                {
                    continue;
                }

                if (string.Equals(normalizedSceneObjectName, normalizedTargetName, StringComparison.Ordinal))
                {
                    int depth = GetTransformDepth(sceneObject.transform);
                    int nameLength = sceneObject.name?.Length ?? int.MaxValue;
                    if (bestNormalizedExactMatch == null
                        || depth < bestNormalizedExactDepth
                        || (depth == bestNormalizedExactDepth && nameLength < bestNormalizedExactNameLength))
                    {
                        bestNormalizedExactMatch = sceneObject;
                        bestNormalizedExactDepth = depth;
                        bestNormalizedExactNameLength = nameLength;
                    }

                    continue;
                }

                if (normalizedSceneObjectName.IndexOf(normalizedTargetName, StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                int containsDepth = GetTransformDepth(sceneObject.transform);
                int containsNameLength = sceneObject.name?.Length ?? int.MaxValue;
                if (bestNormalizedContainsMatch == null
                    || containsDepth < bestNormalizedContainsDepth
                    || (containsDepth == bestNormalizedContainsDepth && containsNameLength < bestNormalizedContainsNameLength))
                {
                    bestNormalizedContainsMatch = sceneObject;
                    bestNormalizedContainsDepth = containsDepth;
                    bestNormalizedContainsNameLength = containsNameLength;
                }
            }

            if (bestNormalizedExactMatch != null)
            {
                return bestNormalizedExactMatch;
            }

            if (bestNormalizedContainsMatch != null)
            {
                return bestNormalizedContainsMatch;
            }

            return null;
        }

        private static GameObject FindFirstGameObjectByHierarchyPathSuffix(IReadOnlyList<GameObject> sceneObjects, string hierarchyPathHint)
        {
            if (sceneObjects == null || sceneObjects.Count <= 0 || string.IsNullOrWhiteSpace(hierarchyPathHint))
            {
                return null;
            }

            string normalizedHint = NormalizeHierarchyPathForMatching(hierarchyPathHint);
            if (string.IsNullOrEmpty(normalizedHint))
            {
                return null;
            }

            GameObject bestSuffixMatch = null;
            int bestSuffixDepth = int.MaxValue;
            GameObject bestContainsMatch = null;
            int bestContainsDepth = int.MaxValue;

            for (int i = 0; i < sceneObjects.Count; i++)
            {
                GameObject sceneObject = sceneObjects[i];
                if (sceneObject == null)
                {
                    continue;
                }

                string hierarchyPath = BuildHierarchyPath(sceneObject.transform);
                string normalizedHierarchyPath = NormalizeHierarchyPathForMatching(hierarchyPath);
                if (string.IsNullOrEmpty(normalizedHierarchyPath))
                {
                    continue;
                }

                int depth = GetTransformDepth(sceneObject.transform);
                if (normalizedHierarchyPath.EndsWith(normalizedHint, StringComparison.Ordinal))
                {
                    if (bestSuffixMatch == null || depth < bestSuffixDepth)
                    {
                        bestSuffixMatch = sceneObject;
                        bestSuffixDepth = depth;
                    }

                    continue;
                }

                if (normalizedHierarchyPath.IndexOf(normalizedHint, StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                if (bestContainsMatch == null || depth < bestContainsDepth)
                {
                    bestContainsMatch = sceneObject;
                    bestContainsDepth = depth;
                }
            }

            return bestSuffixMatch ?? bestContainsMatch;
        }

        private static string NormalizeObjectNameForMatching(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            char[] buffer = new char[value.Length];
            int count = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (!char.IsLetterOrDigit(character))
                {
                    continue;
                }

                buffer[count] = char.ToLowerInvariant(character);
                count++;
            }

            return count > 0 ? new string(buffer, 0, count) : string.Empty;
        }

        private static string NormalizeHierarchyPathForMatching(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            char[] buffer = new char[value.Length];
            int count = 0;
            bool previousWasSeparator = false;
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                bool isSeparator = character == '/' || character == '\\';
                if (isSeparator)
                {
                    if (!previousWasSeparator && count > 0)
                    {
                        buffer[count] = '/';
                        count++;
                    }

                    previousWasSeparator = true;
                    continue;
                }

                if (!char.IsLetterOrDigit(character))
                {
                    continue;
                }

                buffer[count] = char.ToLowerInvariant(character);
                count++;
                previousWasSeparator = false;
            }

            if (count <= 0)
            {
                return string.Empty;
            }

            if (buffer[count - 1] == '/')
            {
                count--;
            }

            return count > 0 ? new string(buffer, 0, count) : string.Empty;
        }

        private static int GetTransformDepth(Transform transform)
        {
            if (transform == null)
            {
                return int.MaxValue;
            }

            int depth = 0;
            Transform current = transform;
            while (current != null)
            {
                depth++;
                current = current.parent;
            }

            return depth;
        }

        private static GameObject FindFirstGameObjectByNameUnderAncestor(IReadOnlyList<GameObject> sceneObjects, string objectName, string ancestorName)
        {
            if (sceneObjects == null || sceneObjects.Count <= 0 || string.IsNullOrWhiteSpace(objectName) || string.IsNullOrWhiteSpace(ancestorName))
            {
                return null;
            }

            string normalizedObjectName = NormalizeObjectNameForMatching(objectName);
            string normalizedAncestorName = NormalizeObjectNameForMatching(ancestorName);
            if (string.IsNullOrEmpty(normalizedObjectName) || string.IsNullOrEmpty(normalizedAncestorName))
            {
                return null;
            }

            GameObject bestMatch = null;
            int bestDepth = int.MaxValue;
            int bestNameLength = int.MaxValue;

            for (int i = 0; i < sceneObjects.Count; i++)
            {
                GameObject sceneObject = sceneObjects[i];
                if (sceneObject == null)
                {
                    continue;
                }

                if (!IsLooseObjectNameMatch(sceneObject.name, objectName, normalizedObjectName))
                {
                    continue;
                }

                if (!HasAncestorWithNormalizedName(sceneObject.transform, normalizedAncestorName))
                {
                    continue;
                }

                int depth = GetTransformDepth(sceneObject.transform);
                int nameLength = sceneObject.name?.Length ?? int.MaxValue;
                if (bestMatch == null || depth < bestDepth || (depth == bestDepth && nameLength < bestNameLength))
                {
                    bestMatch = sceneObject;
                    bestDepth = depth;
                    bestNameLength = nameLength;
                }
            }

            return bestMatch;
        }

        private static bool IsLooseObjectNameMatch(string sceneObjectName, string expectedName, string normalizedExpectedName)
        {
            if (string.IsNullOrWhiteSpace(sceneObjectName) || string.IsNullOrWhiteSpace(expectedName))
            {
                return false;
            }

            if (string.Equals(sceneObjectName, expectedName, StringComparison.Ordinal)
                || string.Equals(sceneObjectName, expectedName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (sceneObjectName.StartsWith(expectedName, StringComparison.Ordinal)
                || sceneObjectName.StartsWith(expectedName, StringComparison.OrdinalIgnoreCase))
            {
                if (sceneObjectName.Length > expectedName.Length)
                {
                    char suffixStart = sceneObjectName[expectedName.Length];
                    if (suffixStart == ' ' || suffixStart == '(' || suffixStart == '_')
                    {
                        return true;
                    }
                }
            }

            if (string.IsNullOrEmpty(normalizedExpectedName))
            {
                return false;
            }

            string normalizedSceneObjectName = NormalizeObjectNameForMatching(sceneObjectName);
            if (string.IsNullOrEmpty(normalizedSceneObjectName))
            {
                return false;
            }

            return string.Equals(normalizedSceneObjectName, normalizedExpectedName, StringComparison.Ordinal)
                || normalizedSceneObjectName.IndexOf(normalizedExpectedName, StringComparison.Ordinal) >= 0;
        }

        private static bool HasAncestorWithNormalizedName(Transform transform, string normalizedAncestorName)
        {
            if (transform == null || string.IsNullOrEmpty(normalizedAncestorName))
            {
                return false;
            }

            Transform current = transform.parent;
            while (current != null)
            {
                if (string.Equals(NormalizeObjectNameForMatching(current.name), normalizedAncestorName, StringComparison.Ordinal))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static GameObject FindBestGameObjectByComponentType(IReadOnlyList<GameObject> sceneObjects, Type componentType, RuntimeBindingSpec spec)
        {
            if (sceneObjects == null || sceneObjects.Count <= 0 || componentType == null)
            {
                return null;
            }

            string normalizedObjectName = NormalizeObjectNameForMatching(spec.ObjectName);
            string normalizedAliasName = NormalizeObjectNameForMatching(spec.AliasName);
            string normalizedParentHint = NormalizeObjectNameForMatching(spec.ParentNameHint);
            string normalizedHierarchyHint = NormalizeHierarchyPathForMatching(spec.HierarchyPathHint);

            GameObject bestMatch = null;
            int bestScore = int.MinValue;
            int bestDepth = int.MaxValue;
            int bestNameLength = int.MaxValue;
            int candidateCount = 0;
            GameObject singleCandidate = null;

            for (int i = 0; i < sceneObjects.Count; i++)
            {
                GameObject sceneObject = sceneObjects[i];
                if (sceneObject == null)
                {
                    continue;
                }

                Component[] components;
                try
                {
                    components = sceneObject.GetComponents(componentType);
                }
                catch
                {
                    continue;
                }

                if (components == null || components.Length <= 0)
                {
                    continue;
                }

                candidateCount++;
                if (candidateCount == 1)
                {
                    singleCandidate = sceneObject;
                }
                else
                {
                    singleCandidate = null;
                }

                int score = 0;
                string normalizedSceneObjectName = NormalizeObjectNameForMatching(sceneObject.name);
                string normalizedHierarchyPath = NormalizeHierarchyPathForMatching(BuildHierarchyPath(sceneObject.transform));

                if (!string.IsNullOrEmpty(normalizedHierarchyHint) && !string.IsNullOrEmpty(normalizedHierarchyPath))
                {
                    if (string.Equals(normalizedHierarchyPath, normalizedHierarchyHint, StringComparison.Ordinal))
                    {
                        score += 180;
                    }
                    else if (normalizedHierarchyPath.EndsWith(normalizedHierarchyHint, StringComparison.Ordinal))
                    {
                        score += 140;
                    }
                    else if (normalizedHierarchyPath.IndexOf(normalizedHierarchyHint, StringComparison.Ordinal) >= 0)
                    {
                        score += 90;
                    }
                }

                if (!string.IsNullOrEmpty(normalizedParentHint)
                    && HasAncestorWithNormalizedName(sceneObject.transform, normalizedParentHint))
                {
                    score += 70;
                }

                if (!string.IsNullOrEmpty(normalizedObjectName) && !string.IsNullOrEmpty(normalizedSceneObjectName))
                {
                    if (string.Equals(normalizedSceneObjectName, normalizedObjectName, StringComparison.Ordinal))
                    {
                        score += 90;
                    }
                    else if (normalizedSceneObjectName.IndexOf(normalizedObjectName, StringComparison.Ordinal) >= 0)
                    {
                        score += 45;
                    }
                }

                if (!string.IsNullOrEmpty(normalizedAliasName) && !string.IsNullOrEmpty(normalizedSceneObjectName))
                {
                    if (string.Equals(normalizedSceneObjectName, normalizedAliasName, StringComparison.Ordinal))
                    {
                        score += 70;
                    }
                    else if (normalizedSceneObjectName.IndexOf(normalizedAliasName, StringComparison.Ordinal) >= 0)
                    {
                        score += 35;
                    }
                }

                if (score <= 0)
                {
                    continue;
                }

                int depth = GetTransformDepth(sceneObject.transform);
                int nameLength = sceneObject.name?.Length ?? int.MaxValue;
                if (bestMatch == null
                    || score > bestScore
                    || (score == bestScore && depth < bestDepth)
                    || (score == bestScore && depth == bestDepth && nameLength < bestNameLength))
                {
                    bestMatch = sceneObject;
                    bestScore = score;
                    bestDepth = depth;
                    bestNameLength = nameLength;
                }
            }

            if (bestMatch != null)
            {
                return bestMatch;
            }

            // Safety net: if only one component candidate exists, prefer recovering over skipping.
            if (candidateCount == 1)
            {
                return singleCandidate;
            }

            return null;
        }

        private static MissingScriptScanResult ScanMissingScriptsInOpenScenes(bool logDetails)
        {
            int objectCount = 0;
            int missingComponentCount = 0;
            List<GameObject> sceneObjects = new(512);
            CollectOpenSceneGameObjects(sceneObjects);

            for (int i = 0; i < sceneObjects.Count; i++)
            {
                GameObject gameObject = sceneObjects[i];
                if (gameObject == null)
                {
                    continue;
                }

                int missingCount = GetMissingScriptCount(gameObject);
                if (missingCount <= 0)
                {
                    continue;
                }

                objectCount++;
                missingComponentCount += missingCount;
                if (logDetails)
                {
                    Debug.LogWarning(
                        $"Missing script x{missingCount} on {BuildHierarchyPath(gameObject.transform)} (Scene: {gameObject.scene.name})",
                        gameObject);
                }
            }

            return new MissingScriptScanResult(objectCount, missingComponentCount);
        }

        private static MissingScriptRemovalResult RemoveMissingScriptsInOpenScenesInternal(bool logDetails)
        {
            int removedComponentCount = 0;
            int affectedObjectCount = 0;
            List<GameObject> sceneObjects = new(512);
            CollectOpenSceneGameObjects(sceneObjects);

            for (int i = 0; i < sceneObjects.Count; i++)
            {
                GameObject gameObject = sceneObjects[i];
                if (gameObject == null)
                {
                    continue;
                }

                int missingCount = GetMissingScriptCount(gameObject);
                if (missingCount <= 0)
                {
                    continue;
                }

                int removed = RemoveMissingScriptsWithRetries(gameObject, missingCount, maxAttempts: 4, out int remainingMissing);
                if (removed <= 0)
                {
                    continue;
                }

                affectedObjectCount++;
                removedComponentCount += removed;
                EditorUtility.SetDirty(gameObject);
                MarkSceneDirtyIfValid(gameObject.scene);
                if (logDetails)
                {
                    if (remainingMissing > 0)
                    {
                        Debug.LogWarning(
                            $"Partially removed missing scripts on {BuildHierarchyPath(gameObject.transform)}: removed={removed}, remaining={remainingMissing}",
                            gameObject);
                    }
                    else
                    {
                        Debug.Log($"Removed missing scripts x{removed} on {BuildHierarchyPath(gameObject.transform)}", gameObject);
                    }
                }
            }

            return new MissingScriptRemovalResult(affectedObjectCount, removedComponentCount);
        }

        private static ProjectPrefabMissingScriptCleanupResult RemoveMissingScriptsInProjectPrefabsInternal(bool logDetails, int maxDetailLogs)
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            int scannedPrefabCount = 0;
            int affectedPrefabCount = 0;
            int removedComponentCount = 0;
            int detailLogCount = 0;

            List<GameObject> hierarchyObjects = new(256);
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (string.IsNullOrWhiteSpace(prefabPath))
                {
                    continue;
                }

                GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefabRoot == null)
                {
                    continue;
                }

                scannedPrefabCount++;
                bool prefabChanged = false;
                int prefabRemovedCount = 0;

                try
                {
                    hierarchyObjects.Clear();
                    CollectHierarchyGameObjects(prefabRoot.transform, hierarchyObjects);
                    for (int objectIndex = 0; objectIndex < hierarchyObjects.Count; objectIndex++)
                    {
                        GameObject gameObject = hierarchyObjects[objectIndex];
                        if (gameObject == null)
                        {
                            continue;
                        }

                        int missingCount = GetMissingScriptCount(gameObject);
                        if (missingCount <= 0)
                        {
                            continue;
                        }

                        int removed = RemoveMissingScriptsWithRetries(gameObject, missingCount, maxAttempts: 4, out int remainingMissing);
                        if (removed <= 0)
                        {
                            continue;
                        }

                        prefabChanged = true;
                        prefabRemovedCount += removed;
                        if (logDetails && detailLogCount < Mathf.Max(0, maxDetailLogs))
                        {
                            if (remainingMissing > 0)
                            {
                                Debug.LogWarning(
                                    $"Partially removed missing scripts on prefab '{prefabPath}' :: {BuildHierarchyPath(gameObject.transform)} (removed={removed}, remaining={remainingMissing})",
                                    prefabRoot);
                            }
                            else
                            {
                                Debug.Log(
                                    $"Removed missing scripts x{removed} on prefab '{prefabPath}' :: {BuildHierarchyPath(gameObject.transform)}",
                                    prefabRoot);
                            }
                            detailLogCount++;
                        }
                    }

                    if (!prefabChanged)
                    {
                        continue;
                    }

                    affectedPrefabCount++;
                    removedComponentCount += prefabRemovedCount;
                    EditorUtility.SetDirty(prefabRoot);
                    PrefabUtility.SavePrefabAsset(prefabRoot);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to clean missing scripts in prefab '{prefabPath}': {ex.Message}");
                }
            }

            return new ProjectPrefabMissingScriptCleanupResult(scannedPrefabCount, affectedPrefabCount, removedComponentCount);
        }

        private static ProjectSceneMissingScriptCleanupResult RemoveMissingScriptsInAllProjectScenesInternal(bool logDetails, int maxDetailLogs)
        {
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
            List<string> scenePaths = new(sceneGuids.Length);
            for (int i = 0; i < sceneGuids.Length; i++)
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
                if (!string.IsNullOrWhiteSpace(scenePath))
                {
                    scenePaths.Add(scenePath);
                }
            }

            return RemoveMissingScriptsInScenePathsInternal(scenePaths, logDetails, maxDetailLogs);
        }

        private static SceneScriptReferenceHygieneScanResult ScanBuildSceneScriptReferenceHygiene(bool logDetails, int maxDetailLogs)
        {
            List<string> scenePaths = new(32);
            EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
            for (int i = 0; i < buildScenes.Length; i++)
            {
                EditorBuildSettingsScene buildScene = buildScenes[i];
                if (buildScene == null || !buildScene.enabled || string.IsNullOrWhiteSpace(buildScene.path))
                {
                    continue;
                }

                if (!buildScene.path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                scenePaths.Add(buildScene.path);
            }

            return ScanSceneScriptReferenceHygiene(scenePaths, logDetails, maxDetailLogs);
        }

        private static SceneScriptReferenceHygieneScanResult ScanSceneScriptReferenceHygiene(
            IReadOnlyList<string> scenePaths,
            bool logDetails,
            int maxDetailLogs)
        {
            int scannedSceneCount = 0;
            int guidlessScriptReferenceCount = 0;
            int duplicateCoreRuntimeComponentCount = 0;
            int detailLogCount = 0;
            int safeMaxDetailLogs = Mathf.Max(0, maxDetailLogs);

            if (scenePaths == null)
            {
                return new SceneScriptReferenceHygieneScanResult(0, 0, 0);
            }

            for (int sceneIndex = 0; sceneIndex < scenePaths.Count; sceneIndex++)
            {
                string scenePath = scenePaths[sceneIndex];
                if (string.IsNullOrWhiteSpace(scenePath))
                {
                    continue;
                }

                if (!File.Exists(scenePath))
                {
                    if (logDetails && detailLogCount < safeMaxDetailLogs)
                    {
                        Debug.LogWarning($"[SceneScriptHygiene] Build scene asset is missing: '{scenePath}'.");
                        detailLogCount++;
                    }

                    continue;
                }

                string[] lines;
                try
                {
                    lines = File.ReadAllLines(scenePath);
                }
                catch (Exception ex)
                {
                    if (logDetails && detailLogCount < safeMaxDetailLogs)
                    {
                        Debug.LogWarning($"[SceneScriptHygiene] Failed to read scene '{scenePath}': {ex.Message}");
                        detailLogCount++;
                    }

                    continue;
                }

                scannedSceneCount++;
                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    string line = lines[lineIndex];
                    if (line.IndexOf("m_Script: {fileID:", StringComparison.Ordinal) < 0
                        || line.IndexOf("guid:", StringComparison.Ordinal) >= 0)
                    {
                        continue;
                    }

                    guidlessScriptReferenceCount++;
                    if (logDetails && detailLogCount < safeMaxDetailLogs)
                    {
                        Debug.LogWarning(
                            $"[SceneScriptHygiene] GUID-less script reference in '{scenePath}' at line {lineIndex + 1}: {line.Trim()}");
                        detailLogCount++;
                    }
                }

                string sceneText = string.Join("\n", lines);
                for (int specIndex = 0; specIndex < CoreRuntimeBindingSpecs.Length; specIndex++)
                {
                    RuntimeBindingSpec spec = CoreRuntimeBindingSpecs[specIndex];
                    if (string.IsNullOrWhiteSpace(spec.TypeName))
                    {
                        continue;
                    }

                    string token = $"m_EditorClassIdentifier: {AssemblyCSharpName}::{spec.TypeName}";
                    int componentCount = CountTextOccurrences(sceneText, token);
                    if (componentCount <= 1)
                    {
                        continue;
                    }

                    int duplicateCount = componentCount - 1;
                    duplicateCoreRuntimeComponentCount += duplicateCount;
                    if (logDetails && detailLogCount < safeMaxDetailLogs)
                    {
                        Debug.LogWarning(
                            $"[SceneScriptHygiene] Duplicate core runtime component in '{scenePath}': type='{spec.TypeName}', count={componentCount}, duplicates={duplicateCount}.");
                        detailLogCount++;
                    }
                }
            }

            if (logDetails)
            {
                Debug.Log(
                    "[SceneScriptHygiene] Summary: " +
                    $"scannedScenes={scannedSceneCount}, " +
                    $"guidlessScriptRefs={guidlessScriptReferenceCount}, " +
                    $"duplicateCoreRuntimeComponents={duplicateCoreRuntimeComponentCount}, " +
                    $"detailLogs={detailLogCount}/{safeMaxDetailLogs}");
            }

            return new SceneScriptReferenceHygieneScanResult(
                scannedSceneCount,
                guidlessScriptReferenceCount,
                duplicateCoreRuntimeComponentCount);
        }

        private static int CountTextOccurrences(string text, string token)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(token))
            {
                return 0;
            }

            int count = 0;
            int searchIndex = 0;
            while (searchIndex < text.Length)
            {
                int foundIndex = text.IndexOf(token, searchIndex, StringComparison.Ordinal);
                if (foundIndex < 0)
                {
                    break;
                }

                count++;
                searchIndex = foundIndex + token.Length;
            }

            return count;
        }

        private static ProjectSceneMissingScriptCleanupResult RemoveMissingScriptsInBuildSettingsScenesInternal(bool logDetails, int maxDetailLogs)
        {
            List<string> scenePaths = new(32);
            EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
            for (int i = 0; i < buildScenes.Length; i++)
            {
                EditorBuildSettingsScene buildScene = buildScenes[i];
                if (buildScene == null || !buildScene.enabled || string.IsNullOrWhiteSpace(buildScene.path))
                {
                    continue;
                }

                if (!buildScene.path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                scenePaths.Add(buildScene.path);
            }

            return RemoveMissingScriptsInScenePathsInternal(scenePaths, logDetails, maxDetailLogs);
        }

        private static ProjectAnimatorControllerMissingScriptCleanupResult RemoveMissingScriptsInProjectAnimatorControllersInternal(bool logDetails, int maxDetailLogs)
        {
            if (EditorApplication.isPlaying)
            {
                return new ProjectAnimatorControllerMissingScriptCleanupResult(0, 0, 0);
            }

            string[] controllerGuids = AssetDatabase.FindAssets("t:AnimatorController", new[] { "Assets" });
            int scannedControllerCount = 0;
            int affectedControllerCount = 0;
            int removedBehaviourCount = 0;
            int detailLogCount = 0;
            int safeMaxDetailLogs = Mathf.Max(0, maxDetailLogs);

            for (int i = 0; i < controllerGuids.Length; i++)
            {
                string controllerPath = AssetDatabase.GUIDToAssetPath(controllerGuids[i]);
                if (string.IsNullOrWhiteSpace(controllerPath))
                {
                    continue;
                }

                AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if (controller == null)
                {
                    continue;
                }

                scannedControllerCount++;
                int removed = 0;
                try
                {
                    removed = RemoveMissingStateMachineBehaviours(controller);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to clean missing StateMachineBehaviour references in animator controller '{controllerPath}': {ex.Message}");
                    continue;
                }

                if (removed <= 0)
                {
                    continue;
                }

                affectedControllerCount++;
                removedBehaviourCount += removed;
                if (logDetails && detailLogCount < safeMaxDetailLogs)
                {
                    Debug.Log(
                        $"[MissingScript/AnimatorProjectFix] controller='{controllerPath}', removedBehaviours={removed}",
                        controller);
                    detailLogCount++;
                }
            }

            if (removedBehaviourCount > 0 && logDetails)
            {
                Debug.Log(
                    $"[MissingScript/AnimatorProjectFix] Summary: affectedControllers={affectedControllerCount}, removedBehaviours={removedBehaviourCount}, detailLogs={detailLogCount}/{safeMaxDetailLogs}");
            }

            return new ProjectAnimatorControllerMissingScriptCleanupResult(scannedControllerCount, affectedControllerCount, removedBehaviourCount);
        }

        private static ProjectAnimatorControllerMissingScriptScanResult ScanMissingScriptsInProjectAnimatorControllersInternal(bool logDetails, int maxDetailLogs)
        {
            string[] controllerGuids = AssetDatabase.FindAssets("t:AnimatorController", new[] { "Assets" });
            int scannedControllerCount = 0;
            int affectedControllerCount = 0;
            int missingBehaviourCount = 0;
            int detailLogCount = 0;
            int safeMaxDetailLogs = Mathf.Max(0, maxDetailLogs);

            for (int i = 0; i < controllerGuids.Length; i++)
            {
                string controllerPath = AssetDatabase.GUIDToAssetPath(controllerGuids[i]);
                if (string.IsNullOrWhiteSpace(controllerPath))
                {
                    continue;
                }

                AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if (controller == null)
                {
                    continue;
                }

                scannedControllerCount++;
                int missingCount = 0;
                try
                {
                    missingCount = CountMissingStateMachineBehaviours(controller);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to scan animator controller '{controllerPath}' for missing StateMachineBehaviour references: {ex.Message}");
                    continue;
                }

                if (missingCount <= 0)
                {
                    continue;
                }

                affectedControllerCount++;
                missingBehaviourCount += missingCount;
                if (logDetails && detailLogCount < safeMaxDetailLogs)
                {
                    Debug.LogWarning(
                        $"[MissingScript/AnimatorProjectScan] controller='{controllerPath}', missingBehaviours={missingCount}",
                        controller);
                    detailLogCount++;
                }
            }

            if (missingBehaviourCount > 0 && logDetails)
            {
                Debug.LogWarning(
                    $"[MissingScript/AnimatorProjectScan] Summary: affectedControllers={affectedControllerCount}, missingBehaviours={missingBehaviourCount}, detailLogs={detailLogCount}/{safeMaxDetailLogs}");
            }

            return new ProjectAnimatorControllerMissingScriptScanResult(scannedControllerCount, affectedControllerCount, missingBehaviourCount);
        }

        private static ProjectSceneMissingScriptCleanupResult RemoveMissingScriptsInScenePathsInternal(IReadOnlyList<string> scenePaths, bool logDetails, int maxDetailLogs)
        {
            if (scenePaths == null || scenePaths.Count <= 0)
            {
                return new ProjectSceneMissingScriptCleanupResult(0, 0, 0);
            }

            int scannedSceneCount = 0;
            int affectedSceneCount = 0;
            int removedComponentCount = 0;
            int detailLogCount = 0;
            SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();

            try
            {
                List<GameObject> sceneObjects = new(1024);
                for (int i = 0; i < scenePaths.Count; i++)
                {
                    string scenePath = scenePaths[i];
                    if (string.IsNullOrWhiteSpace(scenePath))
                    {
                        continue;
                    }

                    Scene scene;
                    try
                    {
                        scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Failed to open scene '{scenePath}' for missing-script cleanup: {ex.Message}");
                        continue;
                    }

                    scannedSceneCount++;
                    int sceneRemovedCount = 0;
                    sceneObjects.Clear();
                    GameObject[] roots = scene.GetRootGameObjects();
                    for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                    {
                        GameObject root = roots[rootIndex];
                        if (root == null)
                        {
                            continue;
                        }

                        CollectHierarchyGameObjects(root.transform, sceneObjects);
                    }

                    for (int objectIndex = 0; objectIndex < sceneObjects.Count; objectIndex++)
                    {
                        GameObject gameObject = sceneObjects[objectIndex];
                        if (gameObject == null)
                        {
                            continue;
                        }

                        int missingCount = GetMissingScriptCount(gameObject);
                        if (missingCount <= 0)
                        {
                            continue;
                        }

                        int removed = RemoveMissingScriptsWithRetries(gameObject, missingCount, maxAttempts: 4, out int remainingMissing);
                        if (removed <= 0)
                        {
                            if (logDetails && detailLogCount < Mathf.Max(0, maxDetailLogs))
                            {
                                Debug.LogWarning(
                                    $"Detected missing scripts but failed to remove in scene '{scenePath}' :: {BuildHierarchyPath(gameObject.transform)}",
                                    gameObject);
                                detailLogCount++;
                            }

                            continue;
                        }

                        sceneRemovedCount += removed;
                        EditorUtility.SetDirty(gameObject);
                        MarkSceneDirtyIfValid(scene);
                        if (logDetails && detailLogCount < Mathf.Max(0, maxDetailLogs))
                        {
                            if (remainingMissing > 0)
                            {
                                Debug.LogWarning(
                                    $"Partially removed missing scripts in scene '{scenePath}' :: {BuildHierarchyPath(gameObject.transform)} (removed={removed}, remaining={remainingMissing})",
                                    gameObject);
                            }
                            else
                            {
                                Debug.Log(
                                    $"Removed missing scripts x{removed} in scene '{scenePath}' :: {BuildHierarchyPath(gameObject.transform)}",
                                    gameObject);
                            }
                            detailLogCount++;
                        }
                    }

                    if (sceneRemovedCount <= 0)
                    {
                        continue;
                    }

                    affectedSceneCount++;
                    removedComponentCount += sceneRemovedCount;
                    EditorSceneManager.SaveScene(scene);
                }
            }
            finally
            {
                if (originalSetup != null && originalSetup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
                }
            }

            return new ProjectSceneMissingScriptCleanupResult(scannedSceneCount, affectedSceneCount, removedComponentCount);
        }

        private static TmpFontScanResult ScanMissingTmpFontAssignmentsInOpenScenes(bool logDetails)
        {
            Type tmpTextType = Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro");
            bool defaultFontMissing = IsTmpDefaultFontMissing();
            if (tmpTextType == null)
            {
                return new TmpFontScanResult(0, 0, defaultFontMissing);
            }

            PropertyInfo fontProperty = tmpTextType.GetProperty("font", BindingFlags.Public | BindingFlags.Instance);
            if (fontProperty == null)
            {
                return new TmpFontScanResult(0, 0, defaultFontMissing);
            }

            int tmpTextCount = 0;
            int missingFontCount = 0;
            List<GameObject> sceneObjects = new(512);
            CollectOpenSceneGameObjects(sceneObjects);
            for (int i = 0; i < sceneObjects.Count; i++)
            {
                GameObject gameObject = sceneObjects[i];
                if (gameObject == null)
                {
                    continue;
                }

                Component[] tmpTexts = gameObject.GetComponents(tmpTextType);
                if (tmpTexts == null || tmpTexts.Length <= 0)
                {
                    continue;
                }

                for (int textIndex = 0; textIndex < tmpTexts.Length; textIndex++)
                {
                    Component tmpText = tmpTexts[textIndex];
                    if (tmpText == null)
                    {
                        continue;
                    }

                    tmpTextCount++;
                    object assignedFont = fontProperty.GetValue(tmpText, null);
                    if (assignedFont != null)
                    {
                        continue;
                    }

                    missingFontCount++;
                    if (logDetails)
                    {
                        Debug.LogWarning($"TMP text has no font asset: {BuildHierarchyPath(gameObject.transform)}", gameObject);
                    }
                }
            }

            if (defaultFontMissing && logDetails)
            {
                Debug.LogWarning("TMP default font asset is missing. Open Window > TextMeshPro > Import TMP Essential Resources.");
            }

            return new TmpFontScanResult(tmpTextCount, missingFontCount, defaultFontMissing);
        }

        private static bool AssignMissingTmpFontsInOpenScenes(bool logDetails, out int assignedCount, out string defaultFontPath)
        {
            assignedCount = 0;
            defaultFontPath = string.Empty;

            Type tmpTextType = Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro");
            if (tmpTextType == null)
            {
                return false;
            }

            PropertyInfo fontProperty = tmpTextType.GetProperty("font", BindingFlags.Public | BindingFlags.Instance);
            if (fontProperty == null || !fontProperty.CanWrite)
            {
                return false;
            }

            if (!GetTmpDefaultFontAsset(out UnityEngine.Object defaultFont, out defaultFontPath))
            {
                return false;
            }

            List<GameObject> sceneObjects = new(512);
            CollectOpenSceneGameObjects(sceneObjects);
            for (int i = 0; i < sceneObjects.Count; i++)
            {
                GameObject gameObject = sceneObjects[i];
                if (gameObject == null)
                {
                    continue;
                }

                Component[] tmpTexts = gameObject.GetComponents(tmpTextType);
                if (tmpTexts == null || tmpTexts.Length <= 0)
                {
                    continue;
                }

                for (int textIndex = 0; textIndex < tmpTexts.Length; textIndex++)
                {
                    Component tmpText = tmpTexts[textIndex];
                    if (tmpText == null)
                    {
                        continue;
                    }

                    object assignedFont = fontProperty.GetValue(tmpText, null);
                    if (assignedFont != null)
                    {
                        continue;
                    }

                    try
                    {
                        Undo.RecordObject(tmpText, "Assign TMP default font");
                        fontProperty.SetValue(tmpText, defaultFont, null);
                        EditorUtility.SetDirty(tmpText);
                        MarkSceneDirtyIfValid(gameObject.scene);
                        assignedCount++;
                        if (logDetails)
                        {
                            Debug.Log($"Assigned TMP default font to {BuildHierarchyPath(gameObject.transform)}", tmpText);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Failed to assign TMP default font at {BuildHierarchyPath(gameObject.transform)}: {ex.Message}", tmpText);
                    }
                }
            }

            return true;
        }

        private static bool GetTmpDefaultFontAsset(out UnityEngine.Object defaultFont, out string fontAssetPath)
        {
            defaultFont = null;
            fontAssetPath = string.Empty;

            Type tmpSettingsType = Type.GetType("TMPro.TMP_Settings, Unity.TextMeshPro");
            if (tmpSettingsType == null)
            {
                return false;
            }

            PropertyInfo instanceProperty = tmpSettingsType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
            object settingsInstance = instanceProperty?.GetValue(null, null);
            PropertyInfo defaultFontProperty = tmpSettingsType.GetProperty("defaultFontAsset", BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            if (defaultFontProperty == null)
            {
                return false;
            }

            UnityEngine.Object resolved = TryGetTmpDefaultFontFromSettingsProperty(defaultFontProperty, settingsInstance, out bool readSucceeded);

            if (resolved == null || !readSucceeded)
            {
                TryImportTmpEssentialResources();
                TryAssignTmpDefaultFontFromProject(out _);

                settingsInstance = instanceProperty?.GetValue(null, null);
                resolved = TryGetTmpDefaultFontFromSettingsProperty(defaultFontProperty, settingsInstance, out _);
            }

            if (resolved == null)
            {
                Type tmpFontType = Type.GetType("TMPro.TMP_FontAsset, Unity.TextMeshPro");
                if (tmpFontType == null)
                {
                    return false;
                }

                string[] fontGuids = AssetDatabase.FindAssets("t:TMP_FontAsset");
                for (int i = 0; i < fontGuids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(fontGuids[i]);
                    UnityEngine.Object fallbackFont = AssetDatabase.LoadAssetAtPath(path, tmpFontType);
                    if (fallbackFont == null)
                    {
                        continue;
                    }

                    defaultFont = fallbackFont;
                    fontAssetPath = path;
                    return true;
                }

                return false;
            }

            defaultFont = resolved;
            fontAssetPath = AssetDatabase.GetAssetPath(resolved);
            return true;
        }

        private static bool IsTmpDefaultFontMissing()
        {
            Type tmpSettingsType = Type.GetType("TMPro.TMP_Settings, Unity.TextMeshPro");
            if (tmpSettingsType == null)
            {
                return false;
            }

            PropertyInfo instanceProperty = tmpSettingsType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
            object settingsInstance = TryGetTmpSettingsInstance(instanceProperty);
            PropertyInfo defaultFontProperty = tmpSettingsType.GetProperty("defaultFontAsset", BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            if (defaultFontProperty == null)
            {
                return false;
            }

            UnityEngine.Object defaultFont = TryGetTmpDefaultFontFromSettingsProperty(defaultFontProperty, settingsInstance, out bool readSucceeded);
            if (!readSucceeded)
            {
                return true;
            }

            return defaultFont == null;
        }

        private static bool TryImportTmpEssentialResources()
        {
            Type importerType = FindTypeByFullName("TMPro.TMP_PackageResourceImporter")
                             ?? FindTypeByFullName("TMPro.EditorUtilities.TMP_PackageResourceImporter");
            if (importerType == null)
            {
                return false;
            }

            MethodInfo importMethod = importerType.GetMethod("ImportProjectResources", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo importResourcesMethod = importerType.GetMethod("ImportResources", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (importMethod == null && importResourcesMethod == null)
            {
                return false;
            }

            try
            {
                if (importMethod != null)
                {
                    importMethod.Invoke(null, null);
                }
                else
                {
                    importResourcesMethod.Invoke(null, new object[] { true, false, false });
                }

                WarmupTmpSettingsSingleton();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"TMP resource import failed: {ex.Message}");
                return false;
            }
        }

        private static bool TryAssignTmpDefaultFontFromProject(out string fontAssetPath)
        {
            fontAssetPath = string.Empty;
            Type tmpSettingsType = Type.GetType("TMPro.TMP_Settings, Unity.TextMeshPro");
            Type tmpFontType = Type.GetType("TMPro.TMP_FontAsset, Unity.TextMeshPro");
            if (tmpSettingsType == null || tmpFontType == null)
            {
                return false;
            }

            PropertyInfo instanceProperty = tmpSettingsType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
            object settingsInstance = TryGetTmpSettingsInstance(instanceProperty);
            PropertyInfo defaultFontProperty = tmpSettingsType.GetProperty("defaultFontAsset", BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            if (defaultFontProperty == null)
            {
                return false;
            }

            UnityEngine.Object existingFont = TryGetTmpDefaultFontFromSettingsProperty(defaultFontProperty, settingsInstance, out _);
            if (existingFont != null)
            {
                fontAssetPath = AssetDatabase.GetAssetPath(existingFont);
                return true;
            }

            string[] fontGuids = AssetDatabase.FindAssets("t:TMP_FontAsset");
            for (int i = 0; i < fontGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(fontGuids[i]);
                UnityEngine.Object fontAsset = AssetDatabase.LoadAssetAtPath(path, tmpFontType);
                if (fontAsset == null)
                {
                    continue;
                }

                settingsInstance = TryGetTmpSettingsInstance(instanceProperty);
                if (!TrySetTmpDefaultFontOnSettingsProperty(defaultFontProperty, settingsInstance, fontAsset))
                {
                    continue;
                }

                if (settingsInstance is UnityEngine.Object settingsObject)
                {
                    EditorUtility.SetDirty(settingsObject);
                }

                fontAssetPath = path;
                return true;
            }

            return false;
        }

        private static object TryGetTmpSettingsInstance(PropertyInfo instanceProperty)
        {
            if (instanceProperty == null)
            {
                return null;
            }

            try
            {
                return instanceProperty.GetValue(null, null);
            }
            catch
            {
                return null;
            }
        }

        private static void WarmupTmpSettingsSingleton()
        {
            Type tmpSettingsType = Type.GetType("TMPro.TMP_Settings, Unity.TextMeshPro");
            if (tmpSettingsType == null)
            {
                return;
            }

            try
            {
                MethodInfo loadDefaultSettingsMethod = tmpSettingsType.GetMethod("LoadDefaultSettings", BindingFlags.Public | BindingFlags.Static);
                object settingsInstance = loadDefaultSettingsMethod?.Invoke(null, null);

                if (settingsInstance == null)
                {
                    PropertyInfo instanceProperty = tmpSettingsType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
                    settingsInstance = instanceProperty?.GetValue(null, null);
                }

                MethodInfo setAssetVersionMethod = tmpSettingsType.GetMethod("SetAssetVersion", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (settingsInstance != null && setAssetVersionMethod != null)
                {
                    setAssetVersionMethod.Invoke(settingsInstance, null);
                    if (settingsInstance is UnityEngine.Object settingsObject)
                    {
                        EditorUtility.SetDirty(settingsObject);
                        AssetDatabase.SaveAssetIfDirty(settingsObject);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"TMP settings warmup failed: {ex.Message}");
            }
        }

        private static UnityEngine.Object TryGetTmpDefaultFontFromSettingsProperty(PropertyInfo defaultFontProperty, object settingsInstance, out bool readSucceeded)
        {
            readSucceeded = false;
            if (defaultFontProperty == null)
            {
                return null;
            }

            MethodInfo getter = defaultFontProperty.GetGetMethod(nonPublic: true);
            if (getter == null)
            {
                return null;
            }

            try
            {
                object value = getter.IsStatic
                    ? defaultFontProperty.GetValue(null, null)
                    : settingsInstance != null ? defaultFontProperty.GetValue(settingsInstance, null) : null;
                readSucceeded = true;
                return value as UnityEngine.Object;
            }
            catch
            {
                return null;
            }
        }

        private static bool TrySetTmpDefaultFontOnSettingsProperty(PropertyInfo defaultFontProperty, object settingsInstance, UnityEngine.Object fontAsset)
        {
            if (defaultFontProperty == null)
            {
                return false;
            }

            MethodInfo setter = defaultFontProperty.GetSetMethod(nonPublic: true);
            if (setter == null)
            {
                return false;
            }

            try
            {
                if (setter.IsStatic)
                {
                    defaultFontProperty.SetValue(null, fontAsset, null);
                    return true;
                }

                if (settingsInstance == null)
                {
                    return false;
                }

                defaultFontProperty.SetValue(settingsInstance, fontAsset, null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static Type FindTypeByFullName(string fullTypeName)
        {
            if (string.IsNullOrWhiteSpace(fullTypeName))
            {
                return null;
            }

            Type resolved = Type.GetType(fullTypeName, throwOnError: false);
            if (resolved != null)
            {
                return resolved;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly assembly = assemblies[i];
                Type match = assembly.GetType(fullTypeName, throwOnError: false);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static Type FindTypeByLooseName(string typeName, string preferredAssemblyName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            string shortName = typeName;
            int lastDotIndex = shortName.LastIndexOf('.');
            if (lastDotIndex >= 0 && lastDotIndex < shortName.Length - 1)
            {
                shortName = shortName.Substring(lastDotIndex + 1);
            }

            if (string.IsNullOrWhiteSpace(shortName))
            {
                return null;
            }

            Type preferredMatch = null;
            Type fallbackMatch = null;
            bool fallbackAmbiguous = false;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly assembly = assemblies[i];
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }
                catch
                {
                    continue;
                }

                bool isPreferredAssembly =
                    !string.IsNullOrWhiteSpace(preferredAssemblyName) &&
                    string.Equals(assembly.GetName().Name, preferredAssemblyName, StringComparison.Ordinal);

                for (int typeIndex = 0; typeIndex < types.Length; typeIndex++)
                {
                    Type candidate = types[typeIndex];
                    if (candidate == null || !string.Equals(candidate.Name, shortName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (isPreferredAssembly)
                    {
                        if (preferredMatch == null)
                        {
                            preferredMatch = candidate;
                        }
                        else if (preferredMatch != candidate)
                        {
                            return null;
                        }

                        continue;
                    }

                    if (fallbackMatch == null)
                    {
                        fallbackMatch = candidate;
                    }
                    else if (fallbackMatch != candidate)
                    {
                        fallbackAmbiguous = true;
                    }
                }
            }

            if (preferredMatch != null)
            {
                return preferredMatch;
            }

            return fallbackAmbiguous ? null : fallbackMatch;
        }

        private static void CollectOpenSceneGameObjects(List<GameObject> output)
        {
            output.Clear();
            int sceneCount = SceneManager.sceneCount;
            for (int i = 0; i < sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    continue;
                }

                GameObject[] rootObjects = scene.GetRootGameObjects();
                for (int rootIndex = 0; rootIndex < rootObjects.Length; rootIndex++)
                {
                    GameObject root = rootObjects[rootIndex];
                    if (root == null)
                    {
                        continue;
                    }

                    CollectHierarchyGameObjects(root.transform, output);
                }
            }
        }

        private static void CollectRuntimeBindingCandidateObjects(List<GameObject> output, bool includeLoadedObjects)
        {
            if (output == null)
            {
                return;
            }

            CollectOpenSceneGameObjects(output);
            if (!includeLoadedObjects || !EditorApplication.isPlaying)
            {
                return;
            }

            AppendLoadedNonPersistentObjects(output);
        }

        private static void AppendLoadedNonPersistentObjects(List<GameObject> output)
        {
            if (output == null)
            {
                return;
            }

            HashSet<int> knownIds = new(output.Count);
            for (int i = 0; i < output.Count; i++)
            {
                GameObject existing = output[i];
                if (existing == null)
                {
                    continue;
                }

                knownIds.Add(existing.GetInstanceID());
            }

            GameObject[] loadedObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < loadedObjects.Length; i++)
            {
                GameObject loaded = loadedObjects[i];
                if (loaded == null || EditorUtility.IsPersistent(loaded))
                {
                    continue;
                }

                int instanceId = loaded.GetInstanceID();
                if (knownIds.Contains(instanceId))
                {
                    continue;
                }

                knownIds.Add(instanceId);
                output.Add(loaded);
            }
        }

        private static void CollectHierarchyGameObjects(Transform root, List<GameObject> output)
        {
            if (root == null)
            {
                return;
            }

            output.Add(root.gameObject);
            for (int i = 0; i < root.childCount; i++)
            {
                CollectHierarchyGameObjects(root.GetChild(i), output);
            }
        }

        private static string BuildHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return "(null)";
            }

            string path = transform.name;
            Transform current = transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        private static void MarkSceneDirtyIfValid(Scene scene)
        {
            if (scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        private static bool TryRepairEditorUserBuildSettingsAccessIssue(out string detail)
        {
            List<string> notes = new(4);
            bool changed = false;
            detail = string.Empty;

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                detail = "project root unavailable";
                return false;
            }

            string targetPath = Path.Combine(projectRoot, "Library", "EditorUserBuildSettings.asset");
            if (!File.Exists(targetPath))
            {
                detail = "target file missing";
                return false;
            }

            try
            {
                FileAttributes attributes = File.GetAttributes(targetPath);
                if ((attributes & FileAttributes.ReadOnly) != 0)
                {
                    File.SetAttributes(targetPath, attributes & ~FileAttributes.ReadOnly);
                    changed = true;
                    notes.Add("cleared read-only attribute");
                }
            }
            catch (Exception ex)
            {
                notes.Add($"attribute update failed: {ex.Message}");
            }

            string tempDirectory = Path.Combine(projectRoot, "Temp");
            if (!Directory.Exists(tempDirectory))
            {
                detail = notes.Count > 0 ? string.Join(", ", notes) : "temp directory missing";
                return changed;
            }

            string targetHash = ComputeFileSha256(targetPath);
            long targetLength = new FileInfo(targetPath).Length;
            int staleDeletedCount = 0;
            string[] tempFiles = Directory.GetFiles(tempDirectory, "UnityTempFile-*");
            for (int i = 0; i < tempFiles.Length; i++)
            {
                string tempPath = tempFiles[i];
                if (!File.Exists(tempPath))
                {
                    continue;
                }

                FileInfo tempInfo = new(tempPath);
                if (tempInfo.Length != targetLength)
                {
                    continue;
                }

                string tempHash = ComputeFileSha256(tempPath);
                if (string.IsNullOrWhiteSpace(tempHash) || !string.Equals(tempHash, targetHash, StringComparison.Ordinal))
                {
                    continue;
                }

                try
                {
                    File.Delete(tempPath);
                    staleDeletedCount++;
                    changed = true;
                }
                catch (Exception ex)
                {
                    notes.Add($"failed to delete stale temp '{Path.GetFileName(tempPath)}': {ex.Message}");
                }
            }

            if (staleDeletedCount > 0)
            {
                notes.Add($"deleted stale temp files={staleDeletedCount}");
            }

            if (notes.Count <= 0)
            {
                notes.Add("no stale temp files matched target hash");
            }

            detail = string.Join(", ", notes);
            return changed;
        }

        private static string ComputeFileSha256(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return string.Empty;
            }

            try
            {
                using FileStream stream = File.OpenRead(filePath);
                using SHA256 sha = SHA256.Create();
                byte[] hashBytes = sha.ComputeHash(stream);
                return BitConverter.ToString(hashBytes).Replace("-", string.Empty);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool IsAutoFixOnEnterPlayEnabled()
        {
            return EditorPrefs.GetBool(AutoFixOnEnterPlayPrefKey, true);
        }

        private static bool IsAggressiveLoadedScanOnEnterPlayEnabled()
        {
            return EditorPrefs.GetBool(AggressiveLoadedScanOnEnterPlayPrefKey, false);
        }

        private static void RequestDelayedEnteredPlayCleanup(double delaySeconds, int minimumPasses)
        {
            double requestedAt = EditorApplication.timeSinceStartup + Math.Max(0d, delaySeconds);
            int safeMinimumPasses = Mathf.Max(1, minimumPasses);

            if (!delayedEnteredPlayCleanupPending)
            {
                delayedEnteredPlayCleanupPending = true;
                delayedEnteredPlayCleanupPassesRemaining = safeMinimumPasses;
                delayedEnteredPlayCleanupAt = requestedAt;
                return;
            }

            delayedEnteredPlayCleanupPassesRemaining = Mathf.Max(delayedEnteredPlayCleanupPassesRemaining, safeMinimumPasses);
            delayedEnteredPlayCleanupAt = Math.Min(delayedEnteredPlayCleanupAt, requestedAt);
        }

        private static void DeferDelayedEnteredPlayCleanup(double delaySeconds)
        {
            if (!delayedEnteredPlayCleanupPending)
            {
                RequestDelayedEnteredPlayCleanup(delaySeconds, minimumPasses: 1);
                return;
            }

            double deferredAt = EditorApplication.timeSinceStartup + Math.Max(0d, delaySeconds);
            delayedEnteredPlayCleanupAt = Math.Max(delayedEnteredPlayCleanupAt, deferredAt);
        }

        private static T ExecuteWithTemporaryAggressiveLoadedScan<T>(Func<T> action)
        {
            if (action == null)
            {
                return default;
            }

            bool previousAggressiveScan = IsAggressiveLoadedScanOnEnterPlayEnabled();
            if (previousAggressiveScan)
            {
                return action();
            }

            try
            {
                EditorPrefs.SetBool(AggressiveLoadedScanOnEnterPlayPrefKey, true);
                return action();
            }
            finally
            {
                EditorPrefs.SetBool(AggressiveLoadedScanOnEnterPlayPrefKey, previousAggressiveScan);
            }
        }

        private static void FillLoadedObjectScanBuffer(bool includePersistentAssets)
        {
            loadedObjectScanBuffer.Clear();

            if (IsAggressiveLoadedScanOnEnterPlayEnabled())
            {
                GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                for (int i = 0; i < allObjects.Length; i++)
                {
                    GameObject gameObject = allObjects[i];
                    if (gameObject == null)
                    {
                        continue;
                    }

                    if (!includePersistentAssets && EditorUtility.IsPersistent(gameObject))
                    {
                        continue;
                    }

                    loadedObjectScanBuffer.Add(gameObject);
                }

                return;
            }

            // Lightweight mode: only scan loaded scene hierarchy.
            CollectOpenSceneGameObjects(loadedObjectScanBuffer);
        }

        private static void FillLoadedAnimatorScanBuffer()
        {
            loadedAnimatorScanBuffer.Clear();

            if (IsAggressiveLoadedScanOnEnterPlayEnabled())
            {
                Animator[] animators = Resources.FindObjectsOfTypeAll<Animator>();
                for (int i = 0; i < animators.Length; i++)
                {
                    Animator animator = animators[i];
                    if (animator == null || EditorUtility.IsPersistent(animator))
                    {
                        continue;
                    }

                    loadedAnimatorScanBuffer.Add(animator);
                }

                return;
            }

            FillLoadedObjectScanBuffer(includePersistentAssets: false);
            for (int i = 0; i < loadedObjectScanBuffer.Count; i++)
            {
                GameObject gameObject = loadedObjectScanBuffer[i];
                if (gameObject == null)
                {
                    continue;
                }

                Animator animator = gameObject.GetComponent<Animator>();
                if (animator != null)
                {
                    loadedAnimatorScanBuffer.Add(animator);
                }
            }
        }

        private static void OnEnterPlayAutoFixStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                lastSceneLoadedCleanupAt = 0d;
                delayedEnteredPlayCleanupFinalRetriesRemaining = DelayedEnteredPlayCleanupFinalRetryBudget;
                postPlayRecoverySweepRequested = false;
                runtimeBindingFallbackEfficiencyWarningLoggedThisPlaySession = false;
                runtimeBindingLoadedFallbackDisabledUntil = 0d;
                ResetPlayScanMissingLogState();
                ResetAnimatorScanMissingLogState();
                if (IsAutoFixOnEnterPlayEnabled())
                {
                    RuntimeBindingRepairResult runtimeBindingRepairInPlay = RunLightweightPlayAutoFixPass(
                        out int mapConfigFixedInPlay,
                        out int playModeRemovedCount,
                        out int listenerAdjustedInPlay);
                    MaybeLogRuntimeBindingFallbackEfficiencyWarning(runtimeBindingRepairInPlay, "EnteredPlay");
                    bool hasRuntimeBindingIssuesInPlay =
                        runtimeBindingRepairInPlay.MissingObjectCount > 0 ||
                        runtimeBindingRepairInPlay.UnresolvedTypeCount > 0;
                    if (playModeRemovedCount > 0
                        || runtimeBindingRepairInPlay.AddedComponentCount > 0
                        || runtimeBindingRepairInPlay.LoadedFallbackResolvedCount > 0
                        || hasRuntimeBindingIssuesInPlay
                        || listenerAdjustedInPlay > 0
                        || mapConfigFixedInPlay > 0)
                    {
                        string message =
                            "Entered-play cleanup applied. " +
                            $"mapConfigFixed={mapConfigFixedInPlay}, " +
                            $"missingScriptsRemoved={playModeRemovedCount}, " +
                            $"audioListenersAdjusted={listenerAdjustedInPlay}, " +
                            $"{FormatRuntimeBindingRepairSummary(runtimeBindingRepairInPlay)}.";
                        if (hasRuntimeBindingIssuesInPlay)
                        {
                            Debug.LogWarning(message);
                        }
                        else
                        {
                            Debug.Log(message);
                        }
                    }

                    RequestDelayedEnteredPlayCleanup(DelayedEnteredPlayCleanupDelaySeconds, DelayedEnteredPlayCleanupPassCount);
                }
                else
                {
                    LogMissingScriptsInLoadedObjectsPass(maxDetailLogs: AutoPlayScanMaxDetailLogs);
                    LogMissingScriptsInLoadedAnimatorControllersPass(maxDetailLogs: AutoPlayScanMaxDetailLogs);
                }
                return;
            }

            if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.ExitingPlayMode)
            {
                delayedEnteredPlayCleanupPending = false;
                delayedEnteredPlayCleanupPassesRemaining = 0;
                delayedEnteredPlayCleanupFinalRetriesRemaining = 0;
                runtimeBindingFallbackEfficiencyWarningLoggedThisPlaySession = false;
                runtimeBindingLoadedFallbackDisabledUntil = 0d;
                delayedEnteredPlayCleanupAt = 0d;
                lastSceneLoadedCleanupAt = 0d;
                ResetPlayScanMissingLogState();
                ResetAnimatorScanMissingLogState();
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    TryRunPostPlayRecoverySweep();
                }
            }

            if (state != PlayModeStateChange.ExitingEditMode)
            {
                return;
            }

            // Re-run project animator-controller sweep for each play entry so newly edited assets are covered.
            projectAnimatorControllerMissingScriptSweepDoneThisSession = false;

            if (!IsAutoFixOnEnterPlayEnabled())
            {
                return;
            }

            MissingScriptRemovalResult loadedObjectsRemoved = RemoveMissingScriptsInLoadedObjectsPass(logDetails: true, maxDetailLogs: 80);
            if (loadedObjectsRemoved.RemovedComponentCount > 0)
            {
                Debug.Log(
                    $"Pre-play loaded-object cleanup removed missing scripts: {loadedObjectsRemoved.RemovedComponentCount} components on {loadedObjectsRemoved.ObjectCount} objects.");
            }

            if (!IsAggressiveLoadedScanOnEnterPlayEnabled())
            {
                try
                {
                    MissingScriptRemovalResult aggressiveLoadedObjectsRemoved = ExecuteWithTemporaryAggressiveLoadedScan(
                        () => RemoveMissingScriptsInLoadedObjectsPass(logDetails: false, maxDetailLogs: 0));
                    if (aggressiveLoadedObjectsRemoved.RemovedComponentCount > 0)
                    {
                        Debug.Log(
                            "Pre-play aggressive loaded-object cleanup applied. " +
                            $"missingScriptsRemoved={aggressiveLoadedObjectsRemoved.RemovedComponentCount}, " +
                            $"affectedObjects={aggressiveLoadedObjectsRemoved.ObjectCount}.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Pre-play aggressive loaded-object cleanup failed: {ex.Message}");
                }
            }

            if (autoSoakFlowPendingRun || autoSoakFlowPendingReportWrite)
            {
                // Auto soak path already executes dedicated preflight fixes.
                return;
            }

            RunEnterPlayAutoFixes();
        }

        private static RuntimeBindingRepairResult RunLightweightPlayAutoFixPass(
            out int mapConfigFixedCount,
            out int removedCount,
            out int listenerAdjustedCount)
        {
            mapConfigFixedCount = FixMissingMapConfigInOpenScenes();
            MissingScriptRemovalResult loadedObjectsRemoved = RemoveMissingScriptsInLoadedObjectsPass(logDetails: false, maxDetailLogs: 0);
            RuntimeBindingRepairResult runtimeBindingRepair = RepairCoreRuntimeBindingsInOpenScenes(logDetails: false, maxDetailLogs: 0);
            MissingScriptRemovalResult loadedObjectsPostBindingRemoved = RemoveMissingScriptsInLoadedObjectsPass(logDetails: false, maxDetailLogs: 0);
            listenerAdjustedCount = EnsureSingleActiveAudioListenerInOpenScenes();

            removedCount =
                loadedObjectsRemoved.RemovedComponentCount +
                loadedObjectsPostBindingRemoved.RemovedComponentCount +
                runtimeBindingRepair.MissingScriptRemovedCount +
                runtimeBindingRepair.DuplicateComponentRemovedCount;
            return runtimeBindingRepair;
        }

        private static string FormatRuntimeBindingRepairSummary(RuntimeBindingRepairResult runtimeBindingRepair)
        {
            int foundCount = Mathf.Max(0, runtimeBindingRepair.FoundCount);
            float fallbackDependencyPercent = foundCount > 0
                ? (runtimeBindingRepair.LoadedFallbackResolvedCount * 100f) / foundCount
                : 0f;
            double fallbackCooldownRemainingSeconds = GetRuntimeBindingLoadedFallbackCooldownRemainingSeconds();

            return
                $"runtimeBindingRepair(found={runtimeBindingRepair.FoundCount}/{runtimeBindingRepair.TargetCount}, " +
                $"missingScriptsRemoved={runtimeBindingRepair.MissingScriptRemovedCount}, " +
                $"added={runtimeBindingRepair.AddedComponentCount}, " +
                $"duplicatesRemoved={runtimeBindingRepair.DuplicateComponentRemovedCount}, " +
                $"missingObjects={runtimeBindingRepair.MissingObjectCount}, " +
                $"unresolvedTypes={runtimeBindingRepair.UnresolvedTypeCount}, " +
                $"sceneCandidates={runtimeBindingRepair.SceneCandidateCount}, " +
                $"loadedFallbackResolved={runtimeBindingRepair.LoadedFallbackResolvedCount}, " +
                $"loadedFallbackCandidates={runtimeBindingRepair.LoadedFallbackCandidateCount}, " +
                $"loadedFallbackSkipped={runtimeBindingRepair.LoadedFallbackSkippedCount}, " +
                $"fallbackCooldownRemaining={fallbackCooldownRemainingSeconds:0.#}s, " +
                $"fallbackDependency={fallbackDependencyPercent:0.#}%)";
        }

        private static bool IsRuntimeBindingLoadedFallbackTemporarilyDisabled()
        {
            return EditorApplication.isPlaying && runtimeBindingLoadedFallbackDisabledUntil > EditorApplication.timeSinceStartup;
        }

        private static double GetRuntimeBindingLoadedFallbackCooldownRemainingSeconds()
        {
            if (!EditorApplication.isPlaying)
            {
                return 0d;
            }

            return Math.Max(0d, runtimeBindingLoadedFallbackDisabledUntil - EditorApplication.timeSinceStartup);
        }

        private static void MaybeLogRuntimeBindingFallbackEfficiencyWarning(RuntimeBindingRepairResult runtimeBindingRepair, string context)
        {
            if (!EditorApplication.isPlaying || runtimeBindingFallbackEfficiencyWarningLoggedThisPlaySession)
            {
                return;
            }

            if (runtimeBindingRepair.LoadedFallbackCandidateCount < RuntimeBindingFallbackCandidateWarningThreshold)
            {
                return;
            }

            if (runtimeBindingRepair.LoadedFallbackResolvedCount > 0)
            {
                return;
            }

            runtimeBindingLoadedFallbackDisabledUntil = Math.Max(
                runtimeBindingLoadedFallbackDisabledUntil,
                EditorApplication.timeSinceStartup + RuntimeBindingFallbackCooldownSeconds);
            runtimeBindingFallbackEfficiencyWarningLoggedThisPlaySession = true;
            Debug.LogWarning(
                "Runtime binding fallback scan cost is high with low benefit. " +
                $"context='{context}', loadedFallbackCandidates={runtimeBindingRepair.LoadedFallbackCandidateCount}, " +
                $"loadedFallbackResolved={runtimeBindingRepair.LoadedFallbackResolvedCount}, " +
                $"cooldownSeconds={RuntimeBindingFallbackCooldownSeconds:0.#}. " +
                "Consider tightening hierarchy/object hints for core bindings.");
        }

        private static void OnSceneLoadedEnterPlayAutoFix(Scene scene, LoadSceneMode mode)
        {
            if (!EditorApplication.isPlaying || !IsAutoFixOnEnterPlayEnabled())
            {
                return;
            }

            if (autoFixDiagnosticsRunning)
            {
                DeferDelayedEnteredPlayCleanup(DelayedEnteredPlayCleanupFollowupIntervalSeconds);
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now - lastSceneLoadedCleanupAt < SceneLoadedCleanupCooldownSeconds)
            {
                RequestDelayedEnteredPlayCleanup(SceneLoadedCleanupCooldownSeconds, minimumPasses: 1);
                return;
            }

            lastSceneLoadedCleanupAt = now;
            autoFixDiagnosticsRunning = true;

            try
            {
                RuntimeBindingRepairResult runtimeBindingRepair = RunLightweightPlayAutoFixPass(
                    out int mapConfigFixed,
                    out int removedCount,
                    out int listenerAdjusted);
                MaybeLogRuntimeBindingFallbackEfficiencyWarning(runtimeBindingRepair, $"SceneLoaded:{scene.name}");
                bool hasRuntimeBindingIssues =
                    runtimeBindingRepair.MissingObjectCount > 0 ||
                    runtimeBindingRepair.UnresolvedTypeCount > 0;
                if (removedCount > 0
                    || runtimeBindingRepair.AddedComponentCount > 0
                    || runtimeBindingRepair.LoadedFallbackResolvedCount > 0
                    || hasRuntimeBindingIssues
                    || listenerAdjusted > 0
                    || mapConfigFixed > 0)
                {
                    string message =
                        "Scene-loaded cleanup applied. " +
                        $"scene='{scene.name}', mode={mode}, " +
                        $"mapConfigFixed={mapConfigFixed}, " +
                        $"missingScriptsRemoved={removedCount}, " +
                        $"audioListenersAdjusted={listenerAdjusted}, " +
                        $"{FormatRuntimeBindingRepairSummary(runtimeBindingRepair)}.";
                    if (hasRuntimeBindingIssues)
                    {
                        Debug.LogWarning(message);
                    }
                    else
                    {
                        Debug.Log(message);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Scene-loaded cleanup failed: {ex.Message}");
            }
            finally
            {
                autoFixDiagnosticsRunning = false;
                RequestDelayedEnteredPlayCleanup(DelayedEnteredPlayCleanupFollowupIntervalSeconds, minimumPasses: 1);
            }
        }

        private static void PollDelayedEnteredPlayCleanup()
        {
            if (!delayedEnteredPlayCleanupPending)
            {
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                delayedEnteredPlayCleanupPending = false;
                delayedEnteredPlayCleanupPassesRemaining = 0;
                delayedEnteredPlayCleanupFinalRetriesRemaining = 0;
                delayedEnteredPlayCleanupAt = 0d;
                return;
            }

            if (EditorApplication.timeSinceStartup < delayedEnteredPlayCleanupAt)
            {
                return;
            }

            if (!IsAutoFixOnEnterPlayEnabled())
            {
                delayedEnteredPlayCleanupPending = false;
                delayedEnteredPlayCleanupPassesRemaining = 0;
                delayedEnteredPlayCleanupFinalRetriesRemaining = 0;
                delayedEnteredPlayCleanupAt = 0d;
                return;
            }

            bool isFinalPass = delayedEnteredPlayCleanupPassesRemaining <= 1;
            bool scheduleFinalRetry = false;
            if (autoFixDiagnosticsRunning)
            {
                DeferDelayedEnteredPlayCleanup(DelayedEnteredPlayCleanupFollowupIntervalSeconds);
                return;
            }

            autoFixDiagnosticsRunning = true;
            try
            {
                bool runtimeBindingIssuesRemain = false;
                int runtimeBindingMissingObjects = 0;
                int runtimeBindingUnresolvedTypes = 0;
                bool runtimeBindingPassFailed = false;
                try
                {
                    RuntimeBindingRepairResult runtimeBindingRepair = RunLightweightPlayAutoFixPass(
                        out int mapConfigFixed,
                        out int removedCount,
                        out int listenerAdjusted);
                    MaybeLogRuntimeBindingFallbackEfficiencyWarning(runtimeBindingRepair, "DelayedPass");
                    bool hasRuntimeBindingIssues =
                        runtimeBindingRepair.MissingObjectCount > 0 ||
                        runtimeBindingRepair.UnresolvedTypeCount > 0;
                    runtimeBindingIssuesRemain = runtimeBindingIssuesRemain || hasRuntimeBindingIssues;
                    runtimeBindingMissingObjects = Mathf.Max(runtimeBindingMissingObjects, runtimeBindingRepair.MissingObjectCount);
                    runtimeBindingUnresolvedTypes = Mathf.Max(runtimeBindingUnresolvedTypes, runtimeBindingRepair.UnresolvedTypeCount);
                    if (removedCount > 0
                        || runtimeBindingRepair.AddedComponentCount > 0
                        || runtimeBindingRepair.LoadedFallbackResolvedCount > 0
                        || hasRuntimeBindingIssues
                        || listenerAdjusted > 0
                        || mapConfigFixed > 0)
                    {
                        string message =
                            "Entered-play delayed cleanup applied. " +
                            $"mapConfigFixed={mapConfigFixed}, " +
                            $"missingScriptsRemoved={removedCount}, " +
                            $"audioListenersAdjusted={listenerAdjusted}, " +
                            $"{FormatRuntimeBindingRepairSummary(runtimeBindingRepair)}.";
                        if (hasRuntimeBindingIssues)
                        {
                            Debug.LogWarning(message);
                        }
                        else
                        {
                            Debug.Log(message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    runtimeBindingIssuesRemain = true;
                    runtimeBindingPassFailed = true;
                    Debug.LogWarning($"Entered-play delayed cleanup failed: {ex.Message}");
                }

                if (isFinalPass)
                {
                    if (!IsAggressiveLoadedScanOnEnterPlayEnabled())
                    {
                        try
                        {
                            MissingScriptRemovalResult aggressiveRemoved = ExecuteWithTemporaryAggressiveLoadedScan(
                                () => RemoveMissingScriptsInLoadedObjectsPass(logDetails: false, maxDetailLogs: 0));
                            RuntimeBindingRepairResult aggressiveBindingRepair = ExecuteWithTemporaryAggressiveLoadedScan(
                                () => RepairCoreRuntimeBindingsInOpenScenes(logDetails: false, maxDetailLogs: 0));
                            MaybeLogRuntimeBindingFallbackEfficiencyWarning(aggressiveBindingRepair, "FinalAggressivePass");
                            MissingScriptRemovalResult aggressivePostBindingRemoved = ExecuteWithTemporaryAggressiveLoadedScan(
                                () => RemoveMissingScriptsInLoadedObjectsPass(logDetails: false, maxDetailLogs: 0));
                            int aggressiveRemovedCount =
                                aggressiveRemoved.RemovedComponentCount +
                                aggressivePostBindingRemoved.RemovedComponentCount +
                                aggressiveBindingRepair.MissingScriptRemovedCount +
                                aggressiveBindingRepair.DuplicateComponentRemovedCount;
                            bool hasAggressiveRuntimeBindingIssues =
                                aggressiveBindingRepair.MissingObjectCount > 0 ||
                                aggressiveBindingRepair.UnresolvedTypeCount > 0;
                            runtimeBindingIssuesRemain = runtimeBindingIssuesRemain || hasAggressiveRuntimeBindingIssues;
                            runtimeBindingMissingObjects = Mathf.Max(runtimeBindingMissingObjects, aggressiveBindingRepair.MissingObjectCount);
                            runtimeBindingUnresolvedTypes = Mathf.Max(runtimeBindingUnresolvedTypes, aggressiveBindingRepair.UnresolvedTypeCount);
                            if (aggressiveRemovedCount > 0
                                || aggressiveBindingRepair.AddedComponentCount > 0
                                || aggressiveBindingRepair.LoadedFallbackResolvedCount > 0
                                || hasAggressiveRuntimeBindingIssues)
                            {
                                string message =
                                    "Entered-play final aggressive cleanup applied. " +
                                    $"missingScriptsRemoved={aggressiveRemovedCount}, " +
                                    $"{FormatRuntimeBindingRepairSummary(aggressiveBindingRepair)}.";
                                if (hasAggressiveRuntimeBindingIssues)
                                {
                                    Debug.LogWarning(message);
                                }
                                else
                                {
                                    Debug.Log(message);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            runtimeBindingIssuesRemain = true;
                            runtimeBindingPassFailed = true;
                            Debug.LogWarning($"Entered-play final aggressive cleanup failed: {ex.Message}");
                        }
                    }

                    CountMissingScriptsInLoadedObjects(
                        out int remainingMissingObjectCount,
                        out int remainingMissingComponentCount);
                    bool hasResidualMissingScripts = remainingMissingComponentCount > 0;
                    if (hasResidualMissingScripts)
                    {
                        LogMissingScriptsInLoadedObjectsPass(maxDetailLogs: AutoPlayScanMaxDetailLogs);
                    }

                    bool shouldRequestFurtherRecovery = hasResidualMissingScripts || runtimeBindingIssuesRemain;
                    if (shouldRequestFurtherRecovery)
                    {
                        if (delayedEnteredPlayCleanupFinalRetriesRemaining > 0)
                        {
                            delayedEnteredPlayCleanupFinalRetriesRemaining--;
                            scheduleFinalRetry = true;
                            Debug.LogWarning(
                                "Entered-play delayed cleanup: residual issues detected after final pass. " +
                                $"Scheduling additional retry (remainingBudget={delayedEnteredPlayCleanupFinalRetriesRemaining}, " +
                                $"missingScriptObjects={remainingMissingObjectCount}, missingScriptComponents={remainingMissingComponentCount}, " +
                                $"runtimeBindingMissingObjects={runtimeBindingMissingObjects}, runtimeBindingUnresolvedTypes={runtimeBindingUnresolvedTypes}, runtimeBindingPassFailed={runtimeBindingPassFailed}).");
                        }
                        else
                        {
                            postPlayRecoverySweepRequested = true;
                            Debug.LogWarning(
                                "Entered-play delayed cleanup: residual issues remain after retry budget exhausted. " +
                                $"A post-play recovery sweep is queued for Edit Mode (missingScriptObjects={remainingMissingObjectCount}, missingScriptComponents={remainingMissingComponentCount}, " +
                                $"runtimeBindingMissingObjects={runtimeBindingMissingObjects}, runtimeBindingUnresolvedTypes={runtimeBindingUnresolvedTypes}, runtimeBindingPassFailed={runtimeBindingPassFailed}).");
                        }
                    }
                    else
                    {
                        delayedEnteredPlayCleanupFinalRetriesRemaining = 0;
                        postPlayRecoverySweepRequested = false;
                    }

                    LogMissingScriptsInLoadedAnimatorControllersPass(maxDetailLogs: AutoPlayScanMaxDetailLogs);
                }

                if (delayedEnteredPlayCleanupPassesRemaining > 1 || scheduleFinalRetry)
                {
                    if (delayedEnteredPlayCleanupPassesRemaining > 1)
                    {
                        delayedEnteredPlayCleanupPassesRemaining--;
                    }
                    else
                    {
                        delayedEnteredPlayCleanupPassesRemaining = 1;
                    }
                    delayedEnteredPlayCleanupAt = EditorApplication.timeSinceStartup + DelayedEnteredPlayCleanupFollowupIntervalSeconds;
                    delayedEnteredPlayCleanupPending = true;
                }
                else
                {
                    delayedEnteredPlayCleanupPassesRemaining = 0;
                    delayedEnteredPlayCleanupFinalRetriesRemaining = 0;
                    delayedEnteredPlayCleanupPending = false;
                    delayedEnteredPlayCleanupAt = 0d;
                }
            }
            finally
            {
                autoFixDiagnosticsRunning = false;
            }
        }

        private static int GetMissingScriptCount(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return 0;
            }

            int utilityCount = 0;
            try
            {
                utilityCount = Mathf.Max(0, GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject));
            }
            catch
            {
                utilityCount = 0;
            }

            int nullSlotCount = CountNullComponentSlots(gameObject);
            return Mathf.Max(utilityCount, nullSlotCount);
        }

        private static int RemoveMissingScriptsWithRetries(GameObject gameObject, int initialMissingCount, int maxAttempts, out int remainingMissingCount)
        {
            remainingMissingCount = Mathf.Max(0, initialMissingCount);
            if (gameObject == null || remainingMissingCount <= 0)
            {
                return 0;
            }

            int safeAttempts = Mathf.Clamp(maxAttempts, 1, 8);
            int removedTotal = 0;
            for (int attempt = 0; attempt < safeAttempts; attempt++)
            {
                int removed = RemoveMissingScriptsFromGameObject(gameObject);
                if (removed <= 0)
                {
                    break;
                }

                removedTotal += removed;
                remainingMissingCount = GetMissingScriptCount(gameObject);
                if (remainingMissingCount <= 0)
                {
                    break;
                }
            }

            return removedTotal;
        }

        private static int RemoveMissingScriptsFromGameObject(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return 0;
            }

            int removed = 0;
            try
            {
                removed = Mathf.Max(0, GameObjectUtility.RemoveMonoBehavioursWithMissingScript(gameObject));
            }
            catch
            {
                removed = 0;
            }

            int remainingNullSlots = CountNullComponentSlots(gameObject);
            if (remainingNullSlots <= 0)
            {
                return removed;
            }

            int removedNullSlots = 0;
            try
            {
                removedNullSlots = RemoveNullComponentSlotsViaSerializedObject(gameObject);
            }
            catch
            {
                removedNullSlots = 0;
            }

            return removed + Mathf.Max(0, removedNullSlots);
        }

        private static int CountNullComponentSlots(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return 0;
            }

            Component[] components = gameObject.GetComponents<Component>();
            if (components == null || components.Length <= 0)
            {
                return 0;
            }

            int nullCount = 0;
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                {
                    nullCount++;
                }
            }

            return nullCount;
        }

        private static int RemoveNullComponentSlotsViaSerializedObject(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return 0;
            }

            SerializedObject serializedObject;
            try
            {
                serializedObject = new SerializedObject(gameObject);
            }
            catch
            {
                return 0;
            }

            SerializedProperty componentsProperty = serializedObject.FindProperty("m_Component");
            if (componentsProperty == null || !componentsProperty.isArray)
            {
                return 0;
            }

            int removed = 0;
            bool canUseUndo = !EditorApplication.isPlaying;
            bool undoRegistered = false;
            if (canUseUndo)
            {
                try
                {
                    Undo.RegisterCompleteObjectUndo(gameObject, "Remove missing scripts");
                    undoRegistered = true;
                }
                catch
                {
                    undoRegistered = false;
                }
            }

            for (int index = componentsProperty.arraySize - 1; index >= 0; index--)
            {
                SerializedProperty componentEntry = componentsProperty.GetArrayElementAtIndex(index);
                SerializedProperty componentReference = componentEntry?.FindPropertyRelative("component");
                if (componentReference == null || componentReference.objectReferenceValue != null)
                {
                    continue;
                }

                try
                {
                    componentsProperty.DeleteArrayElementAtIndex(index);
                    removed++;
                }
                catch
                {
                    // Skip malformed entries and continue best-effort cleanup.
                }
            }

            if (removed <= 0)
            {
                return 0;
            }

            bool applied = false;
            if (!canUseUndo || !undoRegistered)
            {
                try
                {
                    serializedObject.ApplyModifiedPropertiesWithoutUndo();
                    applied = true;
                }
                catch
                {
                    applied = false;
                }
            }

            if (!applied)
            {
                try
                {
                    serializedObject.ApplyModifiedProperties();
                    applied = true;
                }
                catch
                {
                    applied = false;
                }
            }

            if (!applied)
            {
                return 0;
            }

            if (!EditorApplication.isPlaying)
            {
                EditorUtility.SetDirty(gameObject);
            }
            return removed;
        }

        private static void LogMissingScriptsInLoadedAnimatorControllersPass(int maxDetailLogs)
        {
            int safeMaxDetailLogs = Mathf.Max(0, maxDetailLogs);
            int affectedControllerCount = 0;
            int missingBehaviourCount = 0;
            List<MissingAnimatorScanEntry> detailEntries = safeMaxDetailLogs > 0
                ? new List<MissingAnimatorScanEntry>(Mathf.Min(safeMaxDetailLogs, 64))
                : null;

            FillLoadedAnimatorScanBuffer();
            Dictionary<AnimatorController, int> controllerUsage = new();
            for (int i = 0; i < loadedAnimatorScanBuffer.Count; i++)
            {
                Animator animator = loadedAnimatorScanBuffer[i];
                if (animator == null)
                {
                    continue;
                }

                AnimatorController controller = ResolveAnimatorController(animator.runtimeAnimatorController);
                if (controller == null)
                {
                    continue;
                }

                if (controllerUsage.TryGetValue(controller, out int count))
                {
                    controllerUsage[controller] = count + 1;
                }
                else
                {
                    controllerUsage.Add(controller, 1);
                }
            }

            foreach (KeyValuePair<AnimatorController, int> entry in controllerUsage)
            {
                AnimatorController controller = entry.Key;
                int missingCount = CountMissingStateMachineBehaviours(controller);
                if (missingCount <= 0)
                {
                    continue;
                }

                affectedControllerCount++;
                missingBehaviourCount += missingCount;
                if (detailEntries != null && detailEntries.Count < safeMaxDetailLogs)
                {
                    string controllerPath = AssetDatabase.GetAssetPath(controller);
                    detailEntries.Add(
                        new MissingAnimatorScanEntry(
                            controller,
                            controllerPath,
                            missingCount,
                            entry.Value));
                }
            }

            if (missingBehaviourCount <= 0)
            {
                ResetAnimatorScanMissingLogState();
                return;
            }

            bool suppressDuplicateLog = ShouldSuppressDuplicateAnimatorScanLog(affectedControllerCount, missingBehaviourCount);
            int detailLogCount = 0;
            if (!suppressDuplicateLog && detailEntries != null)
            {
                for (int i = 0; i < detailEntries.Count; i++)
                {
                    MissingAnimatorScanEntry entry = detailEntries[i];
                    Debug.LogWarning(
                        $"[MissingScript/AnimatorScan] controller='{entry.ControllerPath}', missingBehaviours={entry.MissingBehaviourCount}, referencedByAnimators={entry.ReferencedByAnimators}",
                        entry.Controller);
                    detailLogCount++;
                }
            }

            StoreAnimatorScanMissingLogState(affectedControllerCount, missingBehaviourCount);
            if (suppressDuplicateLog)
            {
                Debug.Log(
                    $"[MissingScript/AnimatorScan] Summary unchanged: affectedControllers={affectedControllerCount}, missingBehaviours={missingBehaviourCount} (duplicate detail logs suppressed).");
            }
            else
            {
                Debug.LogWarning(
                    $"[MissingScript/AnimatorScan] Summary: affectedControllers={affectedControllerCount}, missingBehaviours={missingBehaviourCount}, detailLogs={detailLogCount}/{safeMaxDetailLogs}");
            }
        }

        private static AnimatorMissingBehaviourRemovalResult RemoveMissingScriptsInLoadedAnimatorControllersPass(bool logDetails, int maxDetailLogs)
        {
            if (EditorApplication.isPlaying)
            {
                return default;
            }

            int safeMaxDetailLogs = Mathf.Max(0, maxDetailLogs);
            int detailLogCount = 0;
            int affectedControllerCount = 0;
            int removedBehaviourCount = 0;

            FillLoadedAnimatorScanBuffer();
            Dictionary<AnimatorController, int> controllerUsage = new();
            for (int i = 0; i < loadedAnimatorScanBuffer.Count; i++)
            {
                Animator animator = loadedAnimatorScanBuffer[i];
                if (animator == null)
                {
                    continue;
                }

                AnimatorController controller = ResolveAnimatorController(animator.runtimeAnimatorController);
                if (controller == null)
                {
                    continue;
                }

                if (controllerUsage.TryGetValue(controller, out int count))
                {
                    controllerUsage[controller] = count + 1;
                }
                else
                {
                    controllerUsage.Add(controller, 1);
                }
            }

            foreach (KeyValuePair<AnimatorController, int> entry in controllerUsage)
            {
                AnimatorController controller = entry.Key;
                if (controller == null)
                {
                    continue;
                }

                string controllerPath = AssetDatabase.GetAssetPath(controller);
                if (string.IsNullOrWhiteSpace(controllerPath) ||
                    !controllerPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int removed = RemoveMissingStateMachineBehaviours(controller);
                if (removed <= 0)
                {
                    continue;
                }

                affectedControllerCount++;
                removedBehaviourCount += removed;
                if (logDetails && detailLogCount < safeMaxDetailLogs)
                {
                    Debug.Log(
                        $"[MissingScript/AnimatorFix] controller='{controllerPath}', removedBehaviours={removed}, referencedByAnimators={entry.Value}",
                        controller);
                    detailLogCount++;
                }
            }

            if (removedBehaviourCount > 0 && logDetails)
            {
                Debug.Log(
                    $"[MissingScript/AnimatorFix] Summary: affectedControllers={affectedControllerCount}, removedBehaviours={removedBehaviourCount}, detailLogs={detailLogCount}/{safeMaxDetailLogs}");
            }

            return new AnimatorMissingBehaviourRemovalResult(affectedControllerCount, removedBehaviourCount);
        }

        private static AnimatorController ResolveAnimatorController(RuntimeAnimatorController runtimeController)
        {
            if (runtimeController == null)
            {
                return null;
            }

            if (runtimeController is AnimatorController animatorController)
            {
                return animatorController;
            }

            if (runtimeController is AnimatorOverrideController overrideController)
            {
                return overrideController.runtimeAnimatorController as AnimatorController;
            }

            return null;
        }

        private static int RemoveMissingStateMachineBehaviours(AnimatorController controller)
        {
            if (controller == null || EditorApplication.isPlaying)
            {
                return 0;
            }

            int removedCount = 0;
            AnimatorControllerLayer[] layers = controller.layers;
            if (layers == null || layers.Length <= 0)
            {
                return 0;
            }

            Stack<AnimatorStateMachine> stack = new();
            for (int layerIndex = 0; layerIndex < layers.Length; layerIndex++)
            {
                AnimatorStateMachine rootStateMachine = layers[layerIndex].stateMachine;
                if (rootStateMachine == null)
                {
                    continue;
                }

                stack.Clear();
                stack.Push(rootStateMachine);
                while (stack.Count > 0)
                {
                    AnimatorStateMachine stateMachine = stack.Pop();
                    if (stateMachine == null)
                    {
                        continue;
                    }

                    StateMachineBehaviour[] machineBehaviours = stateMachine.behaviours;
                    int removedFromMachine = RemoveMissingStateMachineBehavioursInArray(machineBehaviours, out StateMachineBehaviour[] compactMachineBehaviours);
                    if (removedFromMachine > 0)
                    {
                        stateMachine.behaviours = compactMachineBehaviours;
                        EditorUtility.SetDirty(stateMachine);
                        removedCount += removedFromMachine;
                    }

                    ChildAnimatorStateMachine[] childStateMachines = stateMachine.stateMachines;
                    for (int childIndex = 0; childIndex < childStateMachines.Length; childIndex++)
                    {
                        AnimatorStateMachine childStateMachine = childStateMachines[childIndex].stateMachine;
                        if (childStateMachine != null)
                        {
                            stack.Push(childStateMachine);
                        }
                    }

                    ChildAnimatorState[] childStates = stateMachine.states;
                    for (int stateIndex = 0; stateIndex < childStates.Length; stateIndex++)
                    {
                        AnimatorState state = childStates[stateIndex].state;
                        if (state == null)
                        {
                            continue;
                        }

                        StateMachineBehaviour[] stateBehaviours = state.behaviours;
                        int removedFromState = RemoveMissingStateMachineBehavioursInArray(stateBehaviours, out StateMachineBehaviour[] compactStateBehaviours);
                        if (removedFromState <= 0)
                        {
                            continue;
                        }

                        state.behaviours = compactStateBehaviours;
                        EditorUtility.SetDirty(state);
                        removedCount += removedFromState;
                    }
                }
            }

            if (removedCount > 0)
            {
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssetIfDirty(controller);
            }

            return removedCount;
        }

        private static int CountMissingStateMachineBehaviours(AnimatorController controller)
        {
            if (controller == null)
            {
                return 0;
            }

            int missingCount = 0;
            AnimatorControllerLayer[] layers = controller.layers;
            if (layers == null || layers.Length <= 0)
            {
                return 0;
            }

            Stack<AnimatorStateMachine> stack = new();
            for (int layerIndex = 0; layerIndex < layers.Length; layerIndex++)
            {
                AnimatorStateMachine rootStateMachine = layers[layerIndex].stateMachine;
                if (rootStateMachine == null)
                {
                    continue;
                }

                stack.Clear();
                stack.Push(rootStateMachine);
                while (stack.Count > 0)
                {
                    AnimatorStateMachine stateMachine = stack.Pop();
                    missingCount += CountMissingStateMachineBehavioursInArray(stateMachine.behaviours);

                    ChildAnimatorStateMachine[] childStateMachines = stateMachine.stateMachines;
                    for (int childIndex = 0; childIndex < childStateMachines.Length; childIndex++)
                    {
                        AnimatorStateMachine childStateMachine = childStateMachines[childIndex].stateMachine;
                        if (childStateMachine != null)
                        {
                            stack.Push(childStateMachine);
                        }
                    }

                    ChildAnimatorState[] childStates = stateMachine.states;
                    for (int stateIndex = 0; stateIndex < childStates.Length; stateIndex++)
                    {
                        AnimatorState state = childStates[stateIndex].state;
                        if (state == null)
                        {
                            continue;
                        }

                        missingCount += CountMissingStateMachineBehavioursInArray(state.behaviours);
                    }
                }
            }

            return missingCount;
        }

        private static int RemoveMissingStateMachineBehavioursInArray(
            StateMachineBehaviour[] behaviours,
            out StateMachineBehaviour[] compactedBehaviours)
        {
            compactedBehaviours = behaviours;
            if (behaviours == null || behaviours.Length <= 0)
            {
                return 0;
            }

            int missingCount = CountMissingStateMachineBehavioursInArray(behaviours);
            if (missingCount <= 0)
            {
                return 0;
            }

            int targetLength = Mathf.Max(0, behaviours.Length - missingCount);
            StateMachineBehaviour[] cleaned = new StateMachineBehaviour[targetLength];
            int insertIndex = 0;
            for (int i = 0; i < behaviours.Length; i++)
            {
                StateMachineBehaviour behaviour = behaviours[i];
                if (behaviour == null)
                {
                    continue;
                }

                if (insertIndex < cleaned.Length)
                {
                    cleaned[insertIndex] = behaviour;
                    insertIndex++;
                }
            }

            compactedBehaviours = cleaned;
            return missingCount;
        }

        private static int CountMissingStateMachineBehavioursInArray(StateMachineBehaviour[] behaviours)
        {
            if (behaviours == null || behaviours.Length <= 0)
            {
                return 0;
            }

            int missingCount = 0;
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] == null)
                {
                    missingCount++;
                }
            }

            return missingCount;
        }

        private static void ResetPlayScanMissingLogState()
        {
            lastPlayScanMissingObjectCount = -1;
            lastPlayScanMissingComponentCount = -1;
            lastPlayScanLoggedAt = -1d;
        }

        private static void ResetAnimatorScanMissingLogState()
        {
            lastAnimatorScanAffectedControllerCount = -1;
            lastAnimatorScanMissingBehaviourCount = -1;
            lastAnimatorScanLoggedAt = -1d;
        }

        private static bool ShouldSuppressDuplicatePlayScanLog(int affectedObjectCount, int missingComponentCount)
        {
            if (!EditorApplication.isPlaying)
            {
                return false;
            }

            if (lastPlayScanMissingObjectCount < 0 || lastPlayScanMissingComponentCount < 0 || lastPlayScanLoggedAt < 0d)
            {
                return false;
            }

            if (lastPlayScanMissingObjectCount != affectedObjectCount || lastPlayScanMissingComponentCount != missingComponentCount)
            {
                return false;
            }

            return EditorApplication.timeSinceStartup - lastPlayScanLoggedAt < PlayScanDuplicateLogCooldownSeconds;
        }

        private static void StorePlayScanMissingLogState(int affectedObjectCount, int missingComponentCount)
        {
            lastPlayScanMissingObjectCount = Mathf.Max(0, affectedObjectCount);
            lastPlayScanMissingComponentCount = Mathf.Max(0, missingComponentCount);
            lastPlayScanLoggedAt = EditorApplication.timeSinceStartup;
        }

        private static bool ShouldSuppressDuplicateAnimatorScanLog(int affectedControllerCount, int missingBehaviourCount)
        {
            if (!EditorApplication.isPlaying)
            {
                return false;
            }

            if (lastAnimatorScanAffectedControllerCount < 0 || lastAnimatorScanMissingBehaviourCount < 0 || lastAnimatorScanLoggedAt < 0d)
            {
                return false;
            }

            if (lastAnimatorScanAffectedControllerCount != affectedControllerCount
                || lastAnimatorScanMissingBehaviourCount != missingBehaviourCount)
            {
                return false;
            }

            return EditorApplication.timeSinceStartup - lastAnimatorScanLoggedAt < PlayScanDuplicateLogCooldownSeconds;
        }

        private static void StoreAnimatorScanMissingLogState(int affectedControllerCount, int missingBehaviourCount)
        {
            lastAnimatorScanAffectedControllerCount = Mathf.Max(0, affectedControllerCount);
            lastAnimatorScanMissingBehaviourCount = Mathf.Max(0, missingBehaviourCount);
            lastAnimatorScanLoggedAt = EditorApplication.timeSinceStartup;
        }

        private static void CountMissingScriptsInLoadedObjects(out int affectedObjectCount, out int missingComponentCount)
        {
            affectedObjectCount = 0;
            missingComponentCount = 0;
            FillLoadedObjectScanBuffer(includePersistentAssets: false);
            for (int i = 0; i < loadedObjectScanBuffer.Count; i++)
            {
                GameObject gameObject = loadedObjectScanBuffer[i];
                if (gameObject == null)
                {
                    continue;
                }

                int missingCount = GetMissingScriptCount(gameObject);
                if (missingCount <= 0)
                {
                    continue;
                }

                affectedObjectCount++;
                missingComponentCount += missingCount;
            }
        }

        private static void LogMissingScriptsInLoadedObjectsPass(int maxDetailLogs)
        {
            int affectedObjectCount = 0;
            int missingComponentCount = 0;
            int safeMaxDetailLogs = Mathf.Max(0, maxDetailLogs);
            List<MissingScriptPlayScanEntry> detailEntries = safeMaxDetailLogs > 0
                ? new List<MissingScriptPlayScanEntry>(Mathf.Min(safeMaxDetailLogs, 64))
                : null;

            FillLoadedObjectScanBuffer(includePersistentAssets: false);
            for (int i = 0; i < loadedObjectScanBuffer.Count; i++)
            {
                GameObject gameObject = loadedObjectScanBuffer[i];
                if (gameObject == null)
                {
                    continue;
                }

                int missingCount = GetMissingScriptCount(gameObject);
                if (missingCount <= 0)
                {
                    continue;
                }

                affectedObjectCount++;
                missingComponentCount += missingCount;
                if (detailEntries != null && detailEntries.Count < safeMaxDetailLogs)
                {
                    string sceneName = gameObject.scene.IsValid() ? gameObject.scene.name : "(NoScene)";
                    string assetPath = AssetDatabase.GetAssetPath(gameObject);
                    string origin = string.IsNullOrWhiteSpace(assetPath)
                        ? $"scene='{sceneName}'"
                        : $"asset='{assetPath}'";
                    detailEntries.Add(
                        new MissingScriptPlayScanEntry(
                            gameObject,
                            BuildHierarchyPath(gameObject.transform),
                            $"{origin}, hideFlags={gameObject.hideFlags}",
                            missingCount));
                }
            }

            if (missingComponentCount <= 0)
            {
                return;
            }

            bool suppressDuplicateLog = ShouldSuppressDuplicatePlayScanLog(affectedObjectCount, missingComponentCount);
            int detailLogCount = 0;
            if (!suppressDuplicateLog && detailEntries != null)
            {
                for (int i = 0; i < detailEntries.Count; i++)
                {
                    MissingScriptPlayScanEntry entry = detailEntries[i];
                    Debug.LogWarning(
                        $"[MissingScript/PlayScan] x{entry.MissingCount} on {entry.HierarchyPath} ({entry.Origin})",
                        entry.GameObject);
                    detailLogCount++;
                }
            }

            StorePlayScanMissingLogState(affectedObjectCount, missingComponentCount);
            if (suppressDuplicateLog)
            {
                Debug.Log(
                    $"[MissingScript/PlayScan] Summary unchanged: affectedObjects={affectedObjectCount}, missingComponents={missingComponentCount} (duplicate detail logs suppressed).");
            }
            else
            {
                Debug.LogWarning(
                    $"[MissingScript/PlayScan] Summary: affectedObjects={affectedObjectCount}, missingComponents={missingComponentCount}, detailLogs={detailLogCount}/{safeMaxDetailLogs}");
            }
        }

        private static MissingScriptRemovalResult RemoveMissingScriptsInLoadedObjectsPass(bool logDetails, int maxDetailLogs)
        {
            int affectedObjectCount = 0;
            int removedComponentCount = 0;
            int detailLogCount = 0;
            int safeMaxDetailLogs = Mathf.Max(0, maxDetailLogs);

            FillLoadedObjectScanBuffer(includePersistentAssets: false);
            for (int i = 0; i < loadedObjectScanBuffer.Count; i++)
            {
                GameObject gameObject = loadedObjectScanBuffer[i];
                if (gameObject == null)
                {
                    continue;
                }

                int missingCount = GetMissingScriptCount(gameObject);
                if (missingCount <= 0)
                {
                    continue;
                }

                int removed = 0;
                int remainingMissing = missingCount;
                const int MaxRemovalAttemptsPerObject = 4;
                for (int attempt = 0; attempt < MaxRemovalAttemptsPerObject; attempt++)
                {
                    int removedThisAttempt = RemoveMissingScriptsFromGameObject(gameObject);
                    if (removedThisAttempt <= 0)
                    {
                        break;
                    }

                    removed += removedThisAttempt;
                    remainingMissing = GetMissingScriptCount(gameObject);
                    if (remainingMissing <= 0)
                    {
                        break;
                    }
                }

                if (removed <= 0)
                {
                    if (logDetails && detailLogCount < safeMaxDetailLogs)
                    {
                        string sceneName = gameObject.scene.IsValid() ? gameObject.scene.name : "(NoScene)";
                        Debug.LogWarning(
                            $"Failed to remove missing scripts x{missingCount} on {BuildHierarchyPath(gameObject.transform)} (scene='{sceneName}', hideFlags={gameObject.hideFlags})",
                            gameObject);
                        detailLogCount++;
                    }

                    continue;
                }

                affectedObjectCount++;
                removedComponentCount += removed;
                if (gameObject.scene.IsValid() && !EditorApplication.isPlaying)
                {
                    EditorUtility.SetDirty(gameObject);
                    MarkSceneDirtyIfValid(gameObject.scene);
                }

                if (logDetails && detailLogCount < safeMaxDetailLogs)
                {
                    string sceneName = gameObject.scene.IsValid() ? gameObject.scene.name : "(NoScene)";
                    if (remainingMissing > 0)
                    {
                        Debug.LogWarning(
                            $"Partially removed missing scripts on loaded object {BuildHierarchyPath(gameObject.transform)}: removed={removed}, remaining={remainingMissing} (scene='{sceneName}', hideFlags={gameObject.hideFlags})",
                            gameObject);
                    }
                    else
                    {
                        Debug.Log(
                            $"Removed missing scripts x{removed} on loaded object {BuildHierarchyPath(gameObject.transform)} (scene='{sceneName}', hideFlags={gameObject.hideFlags})",
                            gameObject);
                    }
                    detailLogCount++;
                }
            }

            return new MissingScriptRemovalResult(affectedObjectCount, removedComponentCount);
        }

        private static void RunEnterPlayAutoFixes()
        {
            if (autoFixDiagnosticsRunning)
            {
                return;
            }

            autoFixDiagnosticsRunning = true;
            try
            {
                MissingScriptRemovalResult loadedObjectsMissingScriptsRemoved = RemoveMissingScriptsInLoadedObjectsPass(logDetails: false, maxDetailLogs: 0);
                int mapConfigFixedCount = FixMissingMapConfigInOpenScenes();
                int listenerAdjustedCount = EnsureSingleActiveAudioListenerInOpenScenes();
                RuntimeBindingRepairResult runtimeBindingRepair = RepairCoreRuntimeBindingsInOpenScenes(logDetails: true, maxDetailLogs: 80);
                MissingScriptRemovalResult loadedObjectsPostBindingMissingScriptsRemoved = RemoveMissingScriptsInLoadedObjectsPass(logDetails: false, maxDetailLogs: 0);
                AnimatorMissingBehaviourRemovalResult animatorMissingBehavioursRemoved = RemoveMissingScriptsInLoadedAnimatorControllersPass(logDetails: true, maxDetailLogs: 80);
                ProjectAnimatorControllerMissingScriptCleanupResult animatorProjectMissingBehavioursRemoved = default;
                bool animatorProjectSweepExecuted = false;
                if (!projectAnimatorControllerMissingScriptSweepDoneThisSession)
                {
                    animatorProjectMissingBehavioursRemoved = RemoveMissingScriptsInProjectAnimatorControllersInternal(logDetails: false, maxDetailLogs: 0);
                    projectAnimatorControllerMissingScriptSweepDoneThisSession = true;
                    animatorProjectSweepExecuted = true;
                }
                ProjectAnimatorControllerMissingScriptScanResult animatorProjectMissingBehavioursRemaining =
                    ScanMissingScriptsInProjectAnimatorControllersInternal(logDetails: false, maxDetailLogs: 0);
                MissingScriptRemovalResult missingScriptsRemoved = RemoveMissingScriptsInOpenScenesInternal(logDetails: true);
                ProjectPrefabMissingScriptCleanupResult prefabMissingScriptsRemoved = default;
                bool prefabSweepExecuted = false;
                if (!projectPrefabMissingScriptSweepDoneThisSession)
                {
                    prefabMissingScriptsRemoved = RemoveMissingScriptsInProjectPrefabsInternal(logDetails: false, maxDetailLogs: 0);
                    projectPrefabMissingScriptSweepDoneThisSession = true;
                    prefabSweepExecuted = true;
                }

                ProjectSceneMissingScriptCleanupResult buildSceneMissingScriptsRemoved = default;
                bool buildSceneSweepExecuted = false;
                if (!buildSceneMissingScriptSweepDoneThisSession)
                {
                    buildSceneMissingScriptsRemoved = RemoveMissingScriptsInBuildSettingsScenesInternal(logDetails: false, maxDetailLogs: 0);
                    buildSceneMissingScriptSweepDoneThisSession = true;
                    buildSceneSweepExecuted = true;
                }
                SceneScriptReferenceHygieneScanResult buildSceneScriptHygiene =
                    ScanBuildSceneScriptReferenceHygiene(logDetails: false, maxDetailLogs: 0);

                bool tmpFontFixAttempted = AssignMissingTmpFontsInOpenScenes(logDetails: true, out int tmpFontsAssignedCount, out string tmpDefaultFontPath);
                TmpFontScanResult tmpFontsRemaining = ScanMissingTmpFontAssignmentsInOpenScenes(logDetails: false);
                bool editorBuildSettingsRepaired = TryRepairEditorUserBuildSettingsAccessIssue(out string buildSettingsRepairDetail);
                bool hasSceneFixChanges =
                    mapConfigFixedCount > 0 ||
                    listenerAdjustedCount > 0 ||
                    runtimeBindingRepair.MissingScriptRemovedCount > 0 ||
                    runtimeBindingRepair.AddedComponentCount > 0 ||
                    runtimeBindingRepair.DuplicateComponentRemovedCount > 0 ||
                    loadedObjectsMissingScriptsRemoved.RemovedComponentCount > 0 ||
                    loadedObjectsPostBindingMissingScriptsRemoved.RemovedComponentCount > 0 ||
                    missingScriptsRemoved.RemovedComponentCount > 0 ||
                    tmpFontsAssignedCount > 0;

                bool openScenesSaved = false;
                if (hasSceneFixChanges)
                {
                    try
                    {
                        openScenesSaved = EditorSceneManager.SaveOpenScenes();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Failed to save open scenes after enter-play auto fixes: {ex.Message}");
                    }
                }

                CountMissingScriptsInLoadedObjects(
                    out int loadedObjectsMissingScriptsRemainingObjectCount,
                    out int loadedObjectsMissingScriptsRemainingComponentCount);
                if (loadedObjectsMissingScriptsRemainingComponentCount > 0)
                {
                    LogMissingScriptsInLoadedObjectsPass(maxDetailLogs: AutoPlayScanMaxDetailLogs);
                }

                string summary =
                    $"Enter-play auto fix complete. mapConfigFixed={mapConfigFixedCount}, audioListenersAdjusted={listenerAdjustedCount}, " +
                    $"{FormatRuntimeBindingRepairSummary(runtimeBindingRepair)}, " +
                    $"animatorMissingBehavioursRemoved={animatorMissingBehavioursRemoved.RemovedBehaviourCount}/{animatorMissingBehavioursRemoved.ControllerCount} controllers, " +
                    $"animatorProjectMissingBehavioursRemoved={animatorProjectMissingBehavioursRemoved.RemovedBehaviourCount}/{animatorProjectMissingBehavioursRemoved.AffectedAnimatorControllerCount} controllers (scanned={animatorProjectMissingBehavioursRemoved.AnimatorControllerAssetCount}, executed={animatorProjectSweepExecuted}), " +
                    $"animatorProjectMissingBehavioursRemaining={animatorProjectMissingBehavioursRemaining.MissingBehaviourCount}/{animatorProjectMissingBehavioursRemaining.AffectedAnimatorControllerCount} controllers, " +
                    $"loadedObjectsMissingScriptsRemoved={loadedObjectsMissingScriptsRemoved.RemovedComponentCount + loadedObjectsPostBindingMissingScriptsRemoved.RemovedComponentCount} (pre={loadedObjectsMissingScriptsRemoved.RemovedComponentCount}, post={loadedObjectsPostBindingMissingScriptsRemoved.RemovedComponentCount}), " +
                    $"loadedObjectsMissingScriptsRemaining={loadedObjectsMissingScriptsRemainingComponentCount}/{loadedObjectsMissingScriptsRemainingObjectCount} objects, " +
                    $"missingScriptsRemoved={missingScriptsRemoved.RemovedComponentCount}/{missingScriptsRemoved.ObjectCount} objects, " +
                    $"prefabMissingScriptsRemoved={prefabMissingScriptsRemoved.RemovedComponentCount}/{prefabMissingScriptsRemoved.AffectedPrefabCount} prefabs (scanned={prefabMissingScriptsRemoved.PrefabAssetCount}, executed={prefabSweepExecuted}), " +
                    $"buildSceneMissingScriptsRemoved={buildSceneMissingScriptsRemoved.RemovedComponentCount}/{buildSceneMissingScriptsRemoved.AffectedSceneCount} scenes (scanned={buildSceneMissingScriptsRemoved.SceneAssetCount}, executed={buildSceneSweepExecuted}), " +
                    $"buildSceneScriptHygiene(guidlessScriptRefs={buildSceneScriptHygiene.GuidlessScriptReferenceCount}, duplicateCoreRuntimeComponents={buildSceneScriptHygiene.DuplicateCoreRuntimeComponentCount}, scanned={buildSceneScriptHygiene.SceneAssetCount}), " +
                    $"openScenesSaved={openScenesSaved}, " +
                    $"tmpFontsAssigned={tmpFontsAssignedCount}, tmpDefaultFontPath='{tmpDefaultFontPath}', " +
                    $"tmpRemainingMissingFonts={tmpFontsRemaining.MissingFontCount}, tmpDefaultFontMissing={tmpFontsRemaining.DefaultFontMissing}, " +
                    $"editorUserBuildSettingsRepaired={editorBuildSettingsRepaired}.";

                if (tmpFontsRemaining.MissingFontCount > 0
                    || tmpFontsRemaining.DefaultFontMissing
                    || runtimeBindingRepair.MissingObjectCount > 0
                    || runtimeBindingRepair.UnresolvedTypeCount > 0
                    || animatorProjectMissingBehavioursRemaining.MissingBehaviourCount > 0
                    || loadedObjectsMissingScriptsRemainingComponentCount > 0
                    || !buildSceneScriptHygiene.Passed)
                {
                    Debug.LogWarning(summary);
                }
                else
                {
                    Debug.Log(summary);
                }

                if (editorBuildSettingsRepaired)
                {
                    Debug.Log($"EditorUserBuildSettings repair detail: {buildSettingsRepairDetail}");
                }
            }
            finally
            {
                autoFixDiagnosticsRunning = false;
            }
        }

        private static void TryRunPostPlayRecoverySweep()
        {
            if (!postPlayRecoverySweepRequested || !IsAutoFixOnEnterPlayEnabled())
            {
                return;
            }

            postPlayRecoverySweepRequested = false;
            projectAnimatorControllerMissingScriptSweepDoneThisSession = false;
            projectPrefabMissingScriptSweepDoneThisSession = false;
            buildSceneMissingScriptSweepDoneThisSession = false;

            Debug.Log("Post-play recovery sweep triggered: re-running deep auto-fix passes in Edit Mode.");
            RunEnterPlayAutoFixes();
        }

        private static void EnsureAutoSoakHooks()
        {
            EditorApplication.playModeStateChanged -= OnAutoSoakPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnAutoSoakPlayModeStateChanged;
            EditorApplication.update -= PollAutoSoakFlow;
            EditorApplication.update += PollAutoSoakFlow;
        }

        [InitializeOnLoadMethod]
        private static void RestoreAutoSoakHooksAfterDomainReload()
        {
            RestoreAutoSoakSessionState();
            if (autoSoakFlowPendingRun || autoSoakFlowPendingReportWrite)
            {
                EnsureAutoSoakHooks();
            }
        }

        private static void OnAutoSoakPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && autoSoakFlowPendingRun)
            {
                StartAutoSoakInPlayMode();
                return;
            }

            if (state == PlayModeStateChange.EnteredEditMode)
            {
                if (autoSoakFlowPendingRun || autoSoakFlowPendingReportWrite)
                {
                    TraceAutoSoak("Auto soak flow was interrupted before completion.", warning: true);
                }

                ClearAutoSoakState();
                ExitAutoSoakBatchMode(1);
            }
        }

        private static void StartAutoSoakInPlayMode()
        {
            RegressionChecklistRunner runner = UnityEngine.Object.FindFirstObjectByType<RegressionChecklistRunner>();
            if (runner == null)
            {
                TraceAutoSoak("RegressionChecklistRunner not found in active scene.", warning: true);
                ClearAutoSoakState();
                ExitAutoSoakBatchMode(1);
                return;
            }

            autoSoakFlowPendingRun = false;
            autoSoakFlowExpectedRunCount = runner.SoakRunCount + 1;
            SaveAutoSoakSessionState();
            runner.RunReleaseCandidateSoakPassNow();
            TraceAutoSoak($"Auto soak flow: release soak pass started (target run #{autoSoakFlowExpectedRunCount}).", runner: runner);
        }

        private static void PollAutoSoakFlow()
        {
            if (!autoSoakFlowPendingRun && !autoSoakFlowPendingReportWrite)
            {
                return;
            }

            if (EditorApplication.timeSinceStartup - autoSoakFlowStartedAt > AutoSoakFlowTimeoutSeconds)
            {
                TraceAutoSoak("Auto soak flow timed out.", warning: true);
                ClearAutoSoakState();
                ExitAutoSoakBatchMode(1);
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                return;
            }

            if (autoSoakFlowPendingRun)
            {
                StartAutoSoakInPlayMode();
                return;
            }

            RegressionChecklistRunner runner = UnityEngine.Object.FindFirstObjectByType<RegressionChecklistRunner>();
            if (runner == null)
            {
                if (!autoSoakFlowMissingRunnerLogged)
                {
                    autoSoakFlowMissingRunnerLogged = true;
                    SaveAutoSoakSessionState();
                    TraceAutoSoak("Auto soak flow waiting: RegressionChecklistRunner not found yet.", warning: true);
                }

                return;
            }

            autoSoakFlowMissingRunnerLogged = false;

            if (runner.IsSoakRunning || !runner.HasSoakRun)
            {
                return;
            }

            if (runner.SoakRunCount < autoSoakFlowExpectedRunCount)
            {
                return;
            }

            bool reportWriteSucceeded = true;
            if (autoSoakFlowPendingReportWrite)
            {
                reportWriteSucceeded = false;
                autoSoakFlowPendingReportWrite = false;
                SaveAutoSoakSessionState();
                if (runner.TryWriteReleaseSoakDetailedReportFile(4096, out string filePath))
                {
                    reportWriteSucceeded = true;
                    TraceAutoSoak($"Auto soak flow: report file written -> {filePath}", runner: runner);
                }
                else
                {
                    TraceAutoSoak("Auto soak flow: failed to write report file.", warning: true, runner: runner);
                }
            }

            TraceAutoSoak($"Auto soak flow completed. {runner.LastSoakSummary}", runner: runner);
            if (!Application.isBatchMode && !string.IsNullOrWhiteSpace(runner.LastSoakDetailedReportFilePath))
            {
                string reportPath = runner.LastSoakDetailedReportFilePath;
                EditorUtility.DisplayDialog(
                    "Auto Soak Completed",
                    $"{runner.LastSoakSummary}\nReport: {reportPath}",
                    "OK");
            }
            ClearAutoSoakState();
            ExitAutoSoakBatchMode(runner.LastSoakPassed && reportWriteSucceeded ? 0 : 1);
        }

        private static void ExitAutoSoakBatchMode(int exitCode)
        {
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
        }

        private static void SaveAutoSoakSessionState()
        {
            SessionState.SetBool(AutoSoakFlowPendingRunSessionKey, autoSoakFlowPendingRun);
            SessionState.SetBool(AutoSoakFlowPendingReportWriteSessionKey, autoSoakFlowPendingReportWrite);
            SessionState.SetBool(AutoSoakFlowMissingRunnerLoggedSessionKey, autoSoakFlowMissingRunnerLogged);
            SessionState.SetFloat(AutoSoakFlowStartedAtSessionKey, (float)autoSoakFlowStartedAt);
            SessionState.SetInt(AutoSoakFlowExpectedRunCountSessionKey, autoSoakFlowExpectedRunCount);
        }

        private static void RestoreAutoSoakSessionState()
        {
            autoSoakFlowPendingRun = SessionState.GetBool(AutoSoakFlowPendingRunSessionKey, false);
            autoSoakFlowPendingReportWrite = SessionState.GetBool(AutoSoakFlowPendingReportWriteSessionKey, false);
            autoSoakFlowMissingRunnerLogged = SessionState.GetBool(AutoSoakFlowMissingRunnerLoggedSessionKey, false);
            autoSoakFlowStartedAt = SessionState.GetFloat(AutoSoakFlowStartedAtSessionKey, 0f);
            autoSoakFlowExpectedRunCount = SessionState.GetInt(AutoSoakFlowExpectedRunCountSessionKey, 0);
        }

        private static void TraceAutoSoak(string message, bool warning = false, RegressionChecklistRunner runner = null)
        {
            string safeMessage = string.IsNullOrWhiteSpace(message) ? "Auto soak flow event." : message.Trim();
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {safeMessage}";

            if (warning)
            {
                if (runner != null)
                {
                    Debug.LogWarning(line, runner);
                }
                else
                {
                    Debug.LogWarning(line);
                }
            }
            else
            {
                if (runner != null)
                {
                    Debug.Log(line, runner);
                }
                else
                {
                    Debug.Log(line);
                }
            }

            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                if (string.IsNullOrWhiteSpace(projectRoot))
                {
                    return;
                }

                string tracePath = Path.Combine(projectRoot, AutoSoakTraceRelativePath);
                string statusPath = Path.Combine(projectRoot, AutoSoakStatusRelativePath);
                string traceDirectory = Path.GetDirectoryName(tracePath);
                if (!string.IsNullOrWhiteSpace(traceDirectory))
                {
                    Directory.CreateDirectory(traceDirectory);
                }

                File.AppendAllText(tracePath, line + Environment.NewLine);
                File.WriteAllText(statusPath, line + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Auto soak trace write failed: {ex.Message}");
            }
        }

        private static string BuildAutoSoakPreflightSummary(
            string label,
            int mapConfigFixedCount,
            int listenerAdjustedCount,
            AnimatorMissingBehaviourRemovalResult animatorMissingBehavioursRemoved,
            ProjectAnimatorControllerMissingScriptCleanupResult animatorProjectMissingBehavioursRemoved,
            bool animatorProjectSweepExecuted,
            ProjectAnimatorControllerMissingScriptScanResult animatorProjectMissingBehavioursRemaining,
            bool animatorProjectRemainingScanExecuted,
            MissingScriptRemovalResult missingScriptsRemoved,
            ProjectPrefabMissingScriptCleanupResult prefabMissingScriptsRemoved,
            bool prefabSweepExecuted,
            ProjectSceneMissingScriptCleanupResult buildSceneMissingScriptsRemoved,
            bool buildSceneSweepExecuted,
            SceneScriptReferenceHygieneScanResult buildSceneScriptHygiene,
            int tmpFontsAssignedCount,
            TmpFontScanResult tmpFontsRemaining,
            bool tmpFontFixAttempted,
            string tmpDefaultFontPath,
            bool editorBuildSettingsRepaired)
        {
            string safeLabel = string.IsNullOrWhiteSpace(label) ? "Preflight summary" : label.Trim();
            return
                $"{safeLabel}. mapConfigFixed={mapConfigFixedCount}, audioListenersAdjusted={listenerAdjustedCount}, " +
                $"animatorMissingBehavioursRemoved={animatorMissingBehavioursRemoved.RemovedBehaviourCount}/{animatorMissingBehavioursRemoved.ControllerCount}, " +
                $"animatorProjectMissingBehavioursRemoved={animatorProjectMissingBehavioursRemoved.RemovedBehaviourCount}/{animatorProjectMissingBehavioursRemoved.AffectedAnimatorControllerCount} (executed={animatorProjectSweepExecuted}), " +
                $"animatorProjectMissingBehavioursRemaining={animatorProjectMissingBehavioursRemaining.MissingBehaviourCount}/{animatorProjectMissingBehavioursRemaining.AffectedAnimatorControllerCount} (scanExecuted={animatorProjectRemainingScanExecuted}), " +
                $"missingScriptsRemoved={missingScriptsRemoved.RemovedComponentCount}/{missingScriptsRemoved.ObjectCount}, " +
                $"prefabMissingScriptsRemoved={prefabMissingScriptsRemoved.RemovedComponentCount}/{prefabMissingScriptsRemoved.AffectedPrefabCount} (executed={prefabSweepExecuted}), " +
                $"buildSceneMissingScriptsRemoved={buildSceneMissingScriptsRemoved.RemovedComponentCount}/{buildSceneMissingScriptsRemoved.AffectedSceneCount} (executed={buildSceneSweepExecuted}), " +
                $"buildSceneScriptHygiene(guidlessScriptRefs={buildSceneScriptHygiene.GuidlessScriptReferenceCount}, duplicateCoreRuntimeComponents={buildSceneScriptHygiene.DuplicateCoreRuntimeComponentCount}, scanned={buildSceneScriptHygiene.SceneAssetCount}), " +
                $"tmpFontsAssigned={tmpFontsAssignedCount}, tmpRemainingMissingFonts={tmpFontsRemaining.MissingFontCount}, tmpDefaultFontMissing={tmpFontsRemaining.DefaultFontMissing}, " +
                $"tmpFixAttempted={tmpFontFixAttempted}, tmpDefaultFontPath='{tmpDefaultFontPath}', editorUserBuildSettingsRepaired={editorBuildSettingsRepaired}.";
        }

        private static bool HasAutoSoakPreflightWarnings(
            SceneScriptReferenceHygieneScanResult buildSceneScriptHygiene,
            ProjectAnimatorControllerMissingScriptScanResult animatorProjectMissingBehavioursRemaining,
            TmpFontScanResult tmpFontsRemaining)
        {
            return
                !buildSceneScriptHygiene.Passed ||
                animatorProjectMissingBehavioursRemaining.MissingBehaviourCount > 0 ||
                tmpFontsRemaining.MissingFontCount > 0 ||
                tmpFontsRemaining.DefaultFontMissing;
        }

        private static void WriteAutoSoakPreflightSummary(string summary)
        {
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                if (string.IsNullOrWhiteSpace(projectRoot))
                {
                    return;
                }

                string summaryPath = Path.Combine(projectRoot, AutoSoakPreflightSummaryRelativePath);
                string summaryDirectory = Path.GetDirectoryName(summaryPath);
                if (!string.IsNullOrWhiteSpace(summaryDirectory))
                {
                    Directory.CreateDirectory(summaryDirectory);
                }

                string safeSummary = string.IsNullOrWhiteSpace(summary)
                    ? "Preflight summary unavailable."
                    : summary.Trim();
                File.WriteAllText(summaryPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {safeSummary}{Environment.NewLine}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Auto soak preflight summary write failed: {ex.Message}");
            }
        }

        private static void BeginAutoSoakTraceSession()
        {
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                if (string.IsNullOrWhiteSpace(projectRoot))
                {
                    return;
                }

                string tracePath = Path.Combine(projectRoot, AutoSoakTraceRelativePath);
                string statusPath = Path.Combine(projectRoot, AutoSoakStatusRelativePath);
                string traceDirectory = Path.GetDirectoryName(tracePath);
                if (!string.IsNullOrWhiteSpace(traceDirectory))
                {
                    Directory.CreateDirectory(traceDirectory);
                }

                TrimAutoSoakTraceIfNeeded(tracePath);
                string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] === Auto soak trace session started ===";
                File.AppendAllText(tracePath, Environment.NewLine + line + Environment.NewLine);
                File.WriteAllText(statusPath, line + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Auto soak trace session start failed: {ex.Message}");
            }
        }

        private static void TrimAutoSoakTraceIfNeeded(string tracePath)
        {
            if (string.IsNullOrWhiteSpace(tracePath) || !File.Exists(tracePath))
            {
                return;
            }

            FileInfo traceInfo = new(tracePath);
            if (traceInfo.Length <= AutoSoakTraceRetentionMaxBytes)
            {
                return;
            }

            string[] lines = File.ReadAllLines(tracePath);
            int tailCount = Math.Min(AutoSoakTraceRetentionTailLineCount, lines.Length);
            int startIndex = Math.Max(0, lines.Length - tailCount);
            List<string> retainedLines = new(tailCount + 1)
            {
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] === Auto soak trace trimmed; preserved last {tailCount}/{lines.Length} lines from {traceInfo.Length} bytes ==="
            };

            for (int i = startIndex; i < lines.Length; i++)
            {
                retainedLines.Add(lines[i]);
            }

            File.WriteAllLines(tracePath, retainedLines);
        }

        private static void ClearAutoSoakState()
        {
            autoSoakFlowPendingRun = false;
            autoSoakFlowPendingReportWrite = false;
            autoSoakFlowMissingRunnerLogged = false;
            autoSoakFlowStartedAt = 0d;
            autoSoakFlowExpectedRunCount = 0;
            SaveAutoSoakSessionState();
            EditorApplication.playModeStateChanged -= OnAutoSoakPlayModeStateChanged;
            EditorApplication.update -= PollAutoSoakFlow;
        }
    }
}












































