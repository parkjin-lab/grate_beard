using System.Collections.Generic;
using LostBreadcrumbs.Runtime.AI.Learning;
using LostBreadcrumbs.Runtime.Events;
using LostBreadcrumbs.Runtime.Managers;
using LostBreadcrumbs.Runtime.Map;
using LostBreadcrumbs.Runtime.Player;
using LostBreadcrumbs.Runtime.Systems;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.AI
{
    public enum EnemyStateId
    {
        IdlePatrol,
        Suspicion,
        Investigate,
        Chase,
        Search,
        Return,
        Resume,
        Stunned
    }

    public sealed class EnemyController : MonoBehaviour
    {
        private static readonly List<EnemyController> activeControllers = new();

        [Header("Core")]
        [SerializeField] private EnemyProfile profile;
        [SerializeField] private Transform player;

        [Header("Perception")]
        [SerializeField, Min(0.5f)] private float visionRange = 5f;
        [SerializeField, Range(5f, 180f)] private float visionAngle = 90f;
        [SerializeField] private LayerMask lineOfSightBlockers;

        [Header("Audio Perception")]
        [SerializeField] private bool useWallOcclusionForNoise = true;
        [SerializeField] private LayerMask noiseOcclusionMask;
        [SerializeField, Range(0.1f, 1f)] private float noiseTransmissionPerWall = 0.72f;
        [SerializeField, Range(0.1f, 1f)] private float footstepTransmissionPerWall = 0.62f;
        [SerializeField, Range(0.1f, 1f)] private float echoTransmissionPerWall = 0.82f;
        [SerializeField, Range(0.1f, 1f)] private float flashlightTransmissionPerWall = 0.68f;
        [SerializeField, Range(0.1f, 1f)] private float itemUseTransmissionPerWall = 0.74f;
        [SerializeField, Range(0.1f, 1f)] private float decoyTransmissionPerWall = 0.9f;
        [SerializeField, Range(0.05f, 1f)] private float minNoiseTransmission = 0.2f;
        [SerializeField, Min(0f)] private float occludedNoiseTargetJitter = 1.1f;

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float patrolRadius = 1.8f;
        [SerializeField, Min(0.05f)] private float stopDistance = 0.2f;
        [SerializeField, Min(1)] private int maxSearchPoints = 4;
        [SerializeField] private bool constrainMovementToGeneratedMapBounds = true;
        [SerializeField, Min(0f)] private float mapBoundsPadding = 0.18f;
        [SerializeField, Min(0f)] private float mapCellInset = 0.08f;
        [SerializeField, Min(0.05f)] private float mapBoundsRefreshInterval = 0.4f;
        [SerializeField] private bool preventMovementThroughColliders = true;
        [SerializeField] private LayerMask movementBlockerMask = ~0;
        [SerializeField] private bool movementBlockGeneratedOccludersOnly = true;
        [SerializeField, Min(0.01f)] private float movementCollisionRadius = 0.18f;
        [SerializeField, Min(0f)] private float movementCollisionSkin = 0.02f;
        [SerializeField] private bool ignoreTriggerBlockers = true;
        [SerializeField] private bool enableWallSlide = true;
        [SerializeField, Range(20f, 85f)] private float wallSlideProbeAngle = 62f;
        [SerializeField, Range(1, 4)] private int wallSlideProbePairs = 2;
        [SerializeField, Min(0f)] private float wallSlideMinDistance = 0.02f;

        [Header("Movement Recovery")]
        [SerializeField] private bool enableStuckRecovery = true;
        [SerializeField, Min(0.05f)] private float stuckRecoverySeconds = 0.55f;
        [SerializeField, Min(0f)] private float stuckRecoveryMinMoveDistance = 0.018f;
        [SerializeField, Min(0.2f)] private float stuckRecoveryWaypointRadius = 2.35f;
        [SerializeField, Min(0.1f)] private float stuckRecoveryWaypointHoldSeconds = 0.75f;
        [SerializeField, Min(0.05f)] private float stuckRecoveryCooldownSeconds = 0.45f;
        [SerializeField, Min(0.01f)] private float stuckRecoveryNudgeDistance = 0.22f;
        [SerializeField] private bool recoverFromMovementOverlap = true;
        [SerializeField, Min(0.1f)] private float overlapRecoverySearchRadius = 1.4f;
        [SerializeField, Range(8, 24)] private int overlapRecoveryProbeCount = 16;

        [Header("Movement Steering")]
        [SerializeField] private bool enableMovementSteeringWaypoints = true;
        [SerializeField, Min(0.2f)] private float steeringBlockedProbeDistance = 1.15f;
        [SerializeField, Min(0.5f)] private float steeringWaypointSearchRadius = 3.4f;
        [SerializeField, Min(0.1f)] private float steeringWaypointHoldSeconds = 0.62f;
        [SerializeField, Min(0.05f)] private float steeringRepathInterval = 0.22f;
        [SerializeField, Min(0.05f)] private float steeringWaypointReachDistance = 0.24f;
        [SerializeField, Min(0.05f)] private float steeringIntentRefreshDistance = 0.55f;
        [SerializeField, Range(0f, 1f)] private float steeringForwardBias = 0.24f;

        [Header("Spawn Stabilization")]
        [SerializeField, Min(0f)] private float spawnStabilizationSeconds = 0.38f;

        [Header("Disruption")]
        [SerializeField, Min(0.1f)] private float baseStunResistance = 1f;
        [SerializeField] private Color stunnedTint = new(0.55f, 0.9f, 1f, 0.95f);

        [Header("Chase Readability")]
        [SerializeField, Min(0.5f)] private float chaseTransitionSeconds = 1f;
        [SerializeField, Min(0f)] private float chaseTransitionLostSightGrace = 0.35f;
        [SerializeField, Min(0f)] private float chaseTransitionInvestigateSpeedMultiplier = 0.78f;
        [SerializeField] private bool showChaseTransitionMarker = true;
        [SerializeField, Min(0f)] private float transitionMarkerHeight = 0.92f;
        [SerializeField, Min(1f)] private float transitionMarkerPulseSpeed = 8f;
        [SerializeField] private Color chaseAlertMarkerColor = new(1f, 0.18f, 0.18f, 0.98f);
        [SerializeField] private Color chaseBlinkColor = new(1f, 0.2f, 0.2f, 0.95f);
        [SerializeField, Min(1f)] private float chaseBlinkSpeed = 11f;
        [SerializeField, Range(0f, 1f)] private float chaseBlinkStrength = 0.72f;
        [SerializeField, Range(0f, 1f)] private float transitionBodyFlashStrength = 0.84f;
        [SerializeField, Min(0.1f)] private float transitionBodyFlashRampExponent = 1.25f;

        [Header("Chase Disengage")]
        [SerializeField, Min(0.5f)] private float chaseDisengageDistance = 11f;
        [SerializeField, Min(0.1f)] private float chaseDisengageDistanceGrace = 1.35f;
        [SerializeField, Min(0.1f)] private float chaseLostSightGraceSeconds = 2.6f;
        [SerializeField, Min(0f)] private float disengageCueSeconds = 0.9f;
        [SerializeField, Min(0.1f)] private float disengageCuePulseSpeed = 6f;
        [SerializeField] private Color disengageMarkerColor = new(1f, 0.78f, 0.2f, 0.95f);
        [SerializeField, Min(0f)] private float chaseReacquireDelaySeconds = 0.65f;

        [Header("Runtime Event Feedback")]
        [SerializeField] private bool emitAgentRuntimeEvents = true;
        [SerializeField, Min(0f)] private float runtimeEventCooldownSeconds = 1.8f;
        [SerializeField, Min(0f)] private float runtimeEventMaxDistanceFromPlayer = 14f;

        [Header("Movement Echo Visual")]
        [SerializeField] private bool showMovementEchoVisual = true;
        [SerializeField, Min(0.05f)] private float movementEchoInterval = 0.78f;
        [SerializeField, Min(0.01f)] private float movementEchoMinSpeed = 0.32f;
        [SerializeField] private Color movementEchoColor = new(0.36f, 0.9f, 1f, 0.9f);
        [SerializeField, Min(0.05f)] private float movementEchoDuration = 1.18f;
        [SerializeField, Range(1, 4)] private int movementEchoArcCount = 3;
        [SerializeField, Range(40f, 170f)] private float movementEchoArcAngle = 102f;
        [SerializeField, Min(0.05f)] private float movementEchoBaseRadius = 0.22f;
        [SerializeField, Min(0.01f)] private float movementEchoRadiusStep = 0.14f;
        [SerializeField, Min(0.005f)] private float movementEchoThickness = 0.07f;
        [SerializeField] private int movementEchoSortingOrder = 38;
        [SerializeField] private bool movementEchoOnlyWhenHiddenByFog = true;
        [SerializeField, Range(0f, 1f)] private float movementEchoFogHiddenThreshold = 0.72f;
        [SerializeField, Range(0f, 0.4f)] private float movementEchoFogHiddenHysteresis = 0.08f;
        [SerializeField, Min(0f)] private float movementEchoFogHiddenGraceSeconds = 0.12f;
        [SerializeField] private bool movementEchoClampFogThresholdToRuntimeFogRange = true;
        [SerializeField] private bool movementEchoShowWhenFogSystemUnavailable = false;
        [SerializeField, Min(0.2f)] private float movementEchoFogLookupInterval = 1f;
        [SerializeField, Min(0.01f)] private float movementEchoFogVisibilityEvaluationInterval = 0.06f;
        [SerializeField, Min(0.01f)] private float movementEchoFogVisibilityCacheDistance = 0.35f;
        [SerializeField] private bool movementEchoSampleFogFromBodyBounds = true;
        [SerializeField, Range(0.3f, 1f)] private float movementEchoBodyBoundsSampleInset = 0.6f;
        [SerializeField] private bool movementEchoRequireAllBodySamplesHidden = true;
        [SerializeField] private bool movementEchoClearActivePulsesWhenVisible = true;
        [SerializeField] private bool movementEchoPrewarmPool = true;
        [SerializeField, Range(0, 256)] private int movementEchoPrewarmPoolTargetCount = 64;

        [Header("Vision Cone Visual")]
        [SerializeField] private bool showVisionConeVisual = true;
        [SerializeField] private bool visionConeVisibleWhenIdle = true;
        [SerializeField] private bool visionConeClipToWalls = true;
        [SerializeField, Min(0.5f)] private float visionConeVisibleDistance = 13f;
        [SerializeField, Range(6, 48)] private int visionConeSegments = 28;
        [SerializeField] private Color visionConeIdleColor = new(1f, 0.72f, 0.18f, 0.12f);
        [SerializeField] private Color visionConeAlertColor = new(1f, 0.42f, 0.08f, 0.18f);
        [SerializeField] private Color visionConeChaseColor = new(1f, 0.08f, 0.1f, 0.24f);
        [SerializeField] private Color visionConeOutlineColor = new(1f, 0.86f, 0.36f, 0.48f);
        [SerializeField, Min(0.005f)] private float visionConeOutlineWidth = 0.035f;
        [SerializeField] private int visionConeSortingOrder = 34;
        [SerializeField, Min(0.01f)] private float visionConeRefreshInterval = 0.05f;

        private readonly EnemyMemory memory = new();
        private readonly List<Vector2> searchPriority = new();

        private EnemyStateId currentState = EnemyStateId.IdlePatrol;
        private string lastDetectionReason = "None";

        private Vector2 homePosition;
        private Vector2 investigateTarget;
        private Vector2 currentTargetPoint;
        private Vector2 predictedEscapeDirection;

        private bool hasCurrentTarget;
        private bool hasSearchPlan;

        private int searchIndex;
        private float stateTimer;
        private float suspicion;
        private float lastSeenTime = float.NegativeInfinity;
        private float nextSampleTime;
        private float stunnedUntil;

        private int stunCount;
        private bool stunTintApplied;
        private bool chaseTintApplied;
        private bool chaseTransitionPending;
        private float chaseTransitionTimer;
        private float chaseTransitionLostSightTimer;
        private float chaseDistanceOutOfRangeTimer;

        private Transform chaseMarker;
        private TextMesh chaseMarkerText;
        private Animator animator;
        private static readonly int HitTriggerHash = Animator.StringToHash("Hit");

        private Vector2 lastStunSource;
        private SpriteRenderer spriteRenderer;
        private PlayerConcealmentState playerConcealment;
        private Color baseColor = Color.white;
        private float lastSmokeOcclusion;
        private float lastRawSmokeOcclusion;
        private EnemyLearningPhase currentLearningPhase = EnemyLearningPhase.Early;
        private float currentLearningWeight = 0.25f;
        private float currentPredictionWeight = 0.2f;
        private float currentBehaviorScore;
        private float nextLearningRefreshTime;
        private float lastNoiseTransmission = 1f;
        private int lastNoiseWallHits;
        private NoiseKind lastNoiseKind = NoiseKind.Footstep;
        private float runtimeVisionRangeMultiplier = 1f;
        private float runtimeHearingRangeMultiplier = 1f;
        private float runtimeSuspicionGainMultiplier = 1f;
        private float nextRuntimeEventTime;
        private float runtimeTransitionDurationMultiplier = 1f;
        private float runtimeTransitionPulseSpeedMultiplier = 1f;
        private float runtimeTransitionFlashStrengthMultiplier = 1f;
        private float runtimeDisengageCueDurationMultiplier = 1f;
        private float runtimeDisengageGraceMultiplier = 1f;
        private float runtimeChaseBlinkSpeedMultiplier = 1f;
        private MapSystem mapSystem;
        private float nextMapBoundsRefreshTime;
        private bool hasGeneratedMapBounds;
        private Vector2 generatedMapMin;
        private Vector2 generatedMapMax;
        private bool hasGeneratedWalkableCellCache;
        private float generatedWalkableCellHalfExtent = 0.5f;
        private readonly List<Vector2> generatedWalkableCellCenters = new(192);
        private EnemyMovementEchoVisual movementEchoVisual;
        private Rigidbody2D movementBody2D;
        private Collider2D movementCollider2D;
        private readonly RaycastHit2D[] movementCastHits = new RaycastHit2D[8];
        private readonly Collider2D[] movementOverlapHits = new Collider2D[12];
        private float stuckElapsed;
        private float nextStuckRecoveryTime;
        private bool hasMovementRecoveryWaypoint;
        private Vector2 movementRecoveryWaypoint;
        private float movementRecoveryWaypointUntil;
        private bool hasMovementSteeringWaypoint;
        private Vector2 movementSteeringWaypoint;
        private Vector2 movementSteeringIntent;
        private float movementSteeringWaypointUntil;
        private float nextMovementSteeringEvaluateTime;
        private float spawnStabilizedUntil;
        private int movementRecoveryCount;
        private int movementOverlapRecoveryCount;
        private string lastMovementRecoveryReason = "None";
        private Vector2 lastMovementRecoveryFrom;
        private Vector2 lastMovementRecoveryTo;
        private Transform visionConeRoot;
        private MeshFilter visionConeMeshFilter;
        private MeshRenderer visionConeRenderer;
        private LineRenderer visionConeOutline;
        private Mesh visionConeMesh;
        private Material visionConeMaterial;
        private Material visionConeOutlineMaterial;
        private float nextVisionConeRefreshTime;

        private float disengageCueTimer;
        private float chaseReacquireBlockedUntil;

        private enum ChaseMarkerMode
        {
            Alert,
            Disengage
        }

        private ChaseMarkerMode chaseMarkerMode = ChaseMarkerMode.Alert;

        public EnemyStateId CurrentState => currentState;
        public float Suspicion => suspicion;
        public string LastDetectionReason => lastDetectionReason;
        public Vector2 CurrentTargetPoint => currentTargetPoint;
        public bool HasCurrentTarget => hasCurrentTarget;
        public Vector2 LastPredictedEscape => predictedEscapeDirection;
        public string DebugMemorySummary => memory.BuildDebugSummary(3, ActiveProfile.memoryCellSize);
        public IReadOnlyList<Vector2> DebugSearchPriority => searchPriority;
        public bool IsStunned => currentState == EnemyStateId.Stunned;
        public bool IsTargetConcealed => playerConcealment != null && playerConcealment.IsConcealedFromEnemies;
        public float ConcealmentPierce => Mathf.Clamp01(ActiveProfile.safeHavenDetectionFactor);
        public float DecoyResponse => Mathf.Clamp(ActiveProfile.decoyNoiseResponse, 0f, 2f);
        public float ItemNoiseResponse => Mathf.Clamp(ActiveProfile.itemNoiseResponse, 0f, 2f);
        public EnemyLearningPhase LearningPhase => currentLearningPhase;
        public float LearningWeight => currentLearningWeight;
        public float PredictionWeight => currentPredictionWeight;
        public float LearningBehaviorScore => currentBehaviorScore;
        public float SmokeOcclusion => Mathf.Clamp01(lastSmokeOcclusion);
        public float SmokeRawOcclusion => Mathf.Clamp01(lastRawSmokeOcclusion);
        public float SmokePenetration => Mathf.Clamp01(ActiveProfile.smokeVisionPenetration);
        public float StunRemaining => Mathf.Max(0f, stunnedUntil - Time.time);
        public int StunCount => stunCount;
        public float LastNoiseTransmission => Mathf.Clamp01(lastNoiseTransmission);
        public int LastNoiseWallHits => Mathf.Max(0, lastNoiseWallHits);
        public NoiseKind LastNoiseKind => lastNoiseKind;
        public float RuntimeVisionRangeMultiplier => runtimeVisionRangeMultiplier;
        public float RuntimeHearingRangeMultiplier => runtimeHearingRangeMultiplier;
        public float RuntimeSuspicionGainMultiplier => runtimeSuspicionGainMultiplier;
        public bool IsChaseTransitionPending => chaseTransitionPending;
        public float ChaseTransitionProgress => chaseTransitionPending
            ? Mathf.Clamp01(chaseTransitionTimer / Mathf.Max(0.001f, EffectiveChaseTransitionSeconds))
            : 0f;
        public float DisengageCueRemaining => Mathf.Max(0f, disengageCueTimer);
        public float ChaseReacquireBlockedRemaining => Mathf.Max(0f, chaseReacquireBlockedUntil - Time.time);
        public float EffectiveChaseTransitionSeconds => Mathf.Max(0.15f, chaseTransitionSeconds * runtimeTransitionDurationMultiplier);
        public float EffectiveTransitionPulseSpeed => Mathf.Max(0.5f, transitionMarkerPulseSpeed * runtimeTransitionPulseSpeedMultiplier);
        public float EffectiveTransitionFlashStrength => Mathf.Clamp01(transitionBodyFlashStrength * runtimeTransitionFlashStrengthMultiplier);
        public float EffectiveDisengageCueSeconds => Mathf.Max(0f, disengageCueSeconds * runtimeDisengageCueDurationMultiplier);
        public float EffectiveDisengageDistanceGraceSeconds => Mathf.Max(0.1f, chaseDisengageDistanceGrace * runtimeDisengageGraceMultiplier);
        public float EffectiveDisengageLostSightGraceSeconds => Mathf.Max(0.1f, chaseLostSightGraceSeconds * runtimeDisengageGraceMultiplier);
        public float EffectiveChaseBlinkSpeed => Mathf.Max(0.5f, chaseBlinkSpeed * runtimeChaseBlinkSpeedMultiplier);
        public bool HasMovementRecoveryWaypoint => hasMovementRecoveryWaypoint;
        public bool HasMovementSteeringWaypoint => hasMovementSteeringWaypoint;
        public float MovementStuckElapsed => Mathf.Max(0f, stuckElapsed);
        public int MovementRecoveryCount => movementRecoveryCount;
        public int MovementOverlapRecoveryCount => movementOverlapRecoveryCount;
        public string LastMovementRecoveryReason => string.IsNullOrWhiteSpace(lastMovementRecoveryReason) ? "None" : lastMovementRecoveryReason;
        public Vector2 LastMovementRecoveryFrom => lastMovementRecoveryFrom;
        public Vector2 LastMovementRecoveryTo => lastMovementRecoveryTo;
        public static int ActiveControllerCount => activeControllers.Count;

        private EnemyProfile ActiveProfile => profile != null ? profile : EnemyProfileFallback.Instance;

        public static void CopyActiveControllers(List<EnemyController> output)
        {
            if (output == null)
            {
                return;
            }

            output.Clear();
            for (int i = activeControllers.Count - 1; i >= 0; i--)
            {
                EnemyController enemy = activeControllers[i];
                if (enemy == null)
                {
                    activeControllers.RemoveAt(i);
                    continue;
                }

                if (!enemy.isActiveAndEnabled)
                {
                    continue;
                }

                output.Add(enemy);
            }
        }

        private void Awake()
        {
            homePosition = transform.position;
            spriteRenderer = GetComponent<SpriteRenderer>();
            animator = GetComponent<Animator>();
            movementBody2D = GetComponent<Rigidbody2D>();
            movementCollider2D = GetComponent<Collider2D>();
            if (spriteRenderer != null)
            {
                baseColor = spriteRenderer.color;
            }

            EnsureChaseMarker();
            SetChaseMarkerVisible(false);
            EnsureMovementEchoVisual();
            EnsureVisionConeVisual();
            RefreshGeneratedMapBounds(force: true);
            homePosition = ClampToGeneratedMapBounds(homePosition);
        }

        private void Start()
        {
            if (player == null)
            {
                try
                {
                    GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
                    if (playerObject != null)
                    {
                        player = playerObject.transform;
                    }
                }
                catch (UnityException)
                {
                    // Tag might not exist in project settings.
                }

                if (player == null)
                {
                    PlayerDummyController fallbackPlayer = FindFirstObjectByType<PlayerDummyController>();
                    if (fallbackPlayer != null)
                    {
                        player = fallbackPlayer.transform;
                    }
                }
            }

            ResolvePlayerConcealment();
            RefreshGeneratedMapBounds(force: true);
            PickNewPatrolTarget();
        }

        private void OnValidate()
        {
            if (!Application.isPlaying || !isActiveAndEnabled)
            {
                return;
            }

            EnsureMovementEchoVisual();
            EnsureVisionConeVisual();
        }

        private void OnEnable()
        {
            RegisterActiveController();
            NoiseManager.NoiseRaised += OnNoiseRaised;
        }

        private void OnDisable()
        {
            UnregisterActiveController();
            NoiseManager.NoiseRaised -= OnNoiseRaised;
            SetVisionConeVisible(false);
        }

        private void OnDestroy()
        {
            UnregisterActiveController();
            ReleaseVisionConeResources();
        }

        public void SetPlayerReference(Transform target)
        {
            player = target;
            ResolvePlayerConcealment();
        }

        public void SetProfileReference(EnemyProfile enemyProfile)
        {
            profile = enemyProfile;
        }

        public void ConfigureMapBoundsConstraintForRuntime(bool enabled)
        {
            constrainMovementToGeneratedMapBounds = enabled;
            RefreshGeneratedMapBounds(force: true);
            if (!constrainMovementToGeneratedMapBounds)
            {
                return;
            }

            Vector2 clampedPosition = ClampToGeneratedMapBounds(transform.position);
            transform.position = clampedPosition;
            homePosition = ClampToGeneratedMapBounds(homePosition);
        }

        public void PrimeSpawnStabilizationForRuntime(float seconds = -1f)
        {
            float duration = seconds >= 0f ? seconds : spawnStabilizationSeconds;
            spawnStabilizedUntil = Time.time + Mathf.Max(0f, duration);
            stuckElapsed = 0f;
            nextStuckRecoveryTime = Time.time + Mathf.Max(0f, duration);
            nextMovementSteeringEvaluateTime = Time.time + Mathf.Max(0f, duration);
            ClearMovementRecoveryWaypoint();
            ClearMovementSteeringWaypoint();
            hasCurrentTarget = false;
        }

        public bool IsMovementPositionBlockedForRuntime(Vector2 position)
        {
            RefreshGeneratedMapBounds(force: true);
            return IsMovementPositionBlocked(position);
        }

        public bool TryResolveMovementOverlapRecoveryForRuntime(Vector2 position, Vector2 intendedTarget, out Vector2 recoveredPosition)
        {
            RefreshGeneratedMapBounds(force: true);
            return TryRecoverCurrentMovementOverlap(position, intendedTarget, out recoveredPosition);
        }

        public bool TryRecoverMovementOverlapNowForRuntime(Vector2 intendedTarget, out Vector2 recoveredPosition)
        {
            Vector2 current = transform.position;
            if (!TryResolveMovementOverlapRecoveryForRuntime(current, intendedTarget, out recoveredPosition))
            {
                return false;
            }

            ApplyMovementPosition(recoveredPosition);
            RegisterMovementRecovery("RuntimeProbe", current, recoveredPosition, overlapRecovery: true);
            stuckElapsed = 0f;
            ClearMovementRecoveryWaypoint();
            ClearMovementSteeringWaypoint();
            return true;
        }

        public void ApplyRuntimePerceptionTuningForEditor(float visionRangeMultiplier, float hearingRangeMultiplier, float suspicionGainMultiplier)
        {
            runtimeVisionRangeMultiplier = Mathf.Clamp(visionRangeMultiplier, 0.35f, 2.5f);
            runtimeHearingRangeMultiplier = Mathf.Clamp(hearingRangeMultiplier, 0.35f, 2.5f);
            runtimeSuspicionGainMultiplier = Mathf.Clamp(suspicionGainMultiplier, 0.35f, 2.5f);
        }

        public void ResetRuntimePerceptionTuningForEditor()
        {
            runtimeVisionRangeMultiplier = 1f;
            runtimeHearingRangeMultiplier = 1f;
            runtimeSuspicionGainMultiplier = 1f;
        }

        public void ApplyRuntimeChaseReadabilityTuningForEditor(
            float transitionDurationMultiplier,
            float transitionPulseSpeedMultiplier,
            float transitionFlashStrengthMultiplier,
            float disengageCueDurationMultiplier,
            float disengageGraceMultiplier,
            float chaseBlinkSpeedMultiplier)
        {
            runtimeTransitionDurationMultiplier = Mathf.Clamp(transitionDurationMultiplier, 0.35f, 2.5f);
            runtimeTransitionPulseSpeedMultiplier = Mathf.Clamp(transitionPulseSpeedMultiplier, 0.35f, 2.5f);
            runtimeTransitionFlashStrengthMultiplier = Mathf.Clamp(transitionFlashStrengthMultiplier, 0.35f, 2.5f);
            runtimeDisengageCueDurationMultiplier = Mathf.Clamp(disengageCueDurationMultiplier, 0.35f, 2.5f);
            runtimeDisengageGraceMultiplier = Mathf.Clamp(disengageGraceMultiplier, 0.35f, 2.5f);
            runtimeChaseBlinkSpeedMultiplier = Mathf.Clamp(chaseBlinkSpeedMultiplier, 0.35f, 2.5f);
        }

        public void ResetRuntimeChaseReadabilityTuningForEditor()
        {
            runtimeTransitionDurationMultiplier = 1f;
            runtimeTransitionPulseSpeedMultiplier = 1f;
            runtimeTransitionFlashStrengthMultiplier = 1f;
            runtimeDisengageCueDurationMultiplier = 1f;
            runtimeDisengageGraceMultiplier = 1f;
            runtimeChaseBlinkSpeedMultiplier = 1f;
        }
        public bool ApplyStun(float durationSeconds, Vector2 sourcePosition, string reason = "Stunned")
        {
            if (durationSeconds <= 0f)
            {
                return false;
            }

            float persistenceResistance = Mathf.Lerp(0.85f, 1.4f, ActiveProfile.persistence);
            float totalResistance = Mathf.Max(0.15f, baseStunResistance * persistenceResistance);
            float adjustedDuration = Mathf.Max(0.1f, durationSeconds / totalResistance);
            float targetTime = Time.time + adjustedDuration;

            bool extended = targetTime > stunnedUntil + 0.02f;
            stunnedUntil = Mathf.Max(stunnedUntil, targetTime);
            lastStunSource = sourcePosition;

            if (extended)
            {
                stunCount++;
            }

            SetState(EnemyStateId.Stunned, string.IsNullOrWhiteSpace(reason) ? "Stunned" : reason);
            return true;
        }

        private void Update()
        {
            if (constrainMovementToGeneratedMapBounds)
            {
                Vector2 clampedPosition = ClampToGeneratedMapBounds(transform.position);
                if (((Vector2)transform.position - clampedPosition).sqrMagnitude > 0.000001f)
                {
                    transform.position = clampedPosition;
                }
            }

            stateTimer += Time.deltaTime;
            memory.Tick(Time.deltaTime, ActiveProfile.memoryDecayPerSecond);

            if (Time.time >= nextLearningRefreshTime)
            {
                RefreshLearningSnapshot();
            }

            if (player != null && Time.time >= nextSampleTime)
            {
                memory.RecordPlayerSample(player.position, ActiveProfile.maxRecentSamples, ActiveProfile.memoryCellSize);
                nextSampleTime = Time.time + 0.3f;
            }

            if (currentState == EnemyStateId.Stunned)
            {
                TickStunned();
                UpdateVisionConeVisual(force: true);
                return;
            }

            bool canSeePlayer = TryDetectPlayer();
            if (canSeePlayer)
            {
                lastSeenTime = Time.time;
                suspicion = 1f;
                memory.RecordSighting(player.position, ActiveProfile.memoryCellSize);

                if (currentState != EnemyStateId.Chase)
                {
                    investigateTarget = player.position;
                    bool canCommitChase = Time.time >= chaseReacquireBlockedUntil;
                    if (!canCommitChase)
                    {
                        CancelChaseTransition("ReacquireBlocked");
                        if (currentState != EnemyStateId.Investigate && currentState != EnemyStateId.Chase && currentState != EnemyStateId.Stunned)
                        {
                            SetState(EnemyStateId.Investigate, "ReacquireBlocked");
                        }
                    }
                    else
                    {
                        if (!chaseTransitionPending)
                        {
                            BeginChaseTransition();
                        }
                        else
                        {
                            chaseTransitionTimer += Time.deltaTime;
                            chaseTransitionLostSightTimer = 0f;
                        }

                        if (chaseTransitionPending)
                        {
                            if (currentState != EnemyStateId.Investigate && currentState != EnemyStateId.Chase && currentState != EnemyStateId.Stunned)
                            {
                                SetState(EnemyStateId.Investigate, "VisualLock");
                            }

                            if (chaseTransitionTimer >= EffectiveChaseTransitionSeconds)
                            {
                                chaseTransitionPending = false;
                                SetChaseMarkerVisible(false);
                                SetState(EnemyStateId.Chase, "DirectSight");
                            }
                        }
                    }
                }
            }
            else
            {
                suspicion = Mathf.Max(0f, suspicion - ActiveProfile.suspicionDecayPerSecond * Time.deltaTime);

                if (chaseTransitionPending)
                {
                    chaseTransitionLostSightTimer += Time.deltaTime;
                    if (chaseTransitionLostSightTimer >= chaseTransitionLostSightGrace)
                    {
                        CancelChaseTransition("VisualLostBeforeCommit");
                    }
                }
            }

            UpdateStateMachine();
            ApplyStateVisuals();
            UpdateVisionConeVisual();
        }

        private void UpdateStateMachine()
        {
            if (Time.time < spawnStabilizedUntil)
            {
                hasCurrentTarget = false;
                stuckElapsed = 0f;
                return;
            }

            switch (currentState)
            {
                case EnemyStateId.IdlePatrol:
                    TickPatrol();
                    if (suspicion >= ActiveProfile.suspicionToInvestigate)
                    {
                        SetState(EnemyStateId.Suspicion, "SuspicionThreshold");
                    }
                    break;

                case EnemyStateId.Suspicion:
                    if (stateTimer >= ActiveProfile.suspicionHoldTime)
                    {
                        SetState(EnemyStateId.Investigate, "SuspicionConfirmed");
                    }
                    break;

                case EnemyStateId.Investigate:
                    float investigateSpeed = ActiveProfile.investigateSpeed;
                    if (chaseTransitionPending)
                    {
                        investigateSpeed *= Mathf.Clamp(chaseTransitionInvestigateSpeedMultiplier, 0.25f, 1f);
                    }

                    MoveTo(investigateTarget, investigateSpeed);
                    if (Vector2.Distance(transform.position, investigateTarget) <= stopDistance)
                    {
                        SetState(EnemyStateId.Search, "ReachedInvestigatePoint");
                    }
                    break;

                case EnemyStateId.Chase:
                    TickChase();
                    break;

                case EnemyStateId.Search:
                    TickSearch();
                    break;

                case EnemyStateId.Return:
                    MoveTo(homePosition, ActiveProfile.returnSpeed);
                    if (Vector2.Distance(transform.position, homePosition) <= stopDistance)
                    {
                        SetState(EnemyStateId.Resume, "ReturnedHome");
                    }
                    break;

                case EnemyStateId.Resume:
                    if (stateTimer >= ActiveProfile.resumeDelaySeconds)
                    {
                        SetState(EnemyStateId.IdlePatrol, "ResumeCompleted");
                    }
                    break;

                case EnemyStateId.Stunned:
                    break;
            }
        }

        private void TickStunned()
        {
            ApplyStunVisual();
            hasCurrentTarget = false;
            hasSearchPlan = false;
            searchPriority.Clear();

            if (Time.time < stunnedUntil)
            {
                return;
            }

            RestoreBaseVisual();
            suspicion = Mathf.Max(suspicion, ActiveProfile.suspicionToInvestigate * 0.75f);
            investigateTarget = ClampToGeneratedMapBounds(lastStunSource);
            SetState(EnemyStateId.Investigate, "RecoverFromStun");
        }

        private void TickPatrol()
        {
            if (!hasCurrentTarget)
            {
                PickNewPatrolTarget();
            }

            MoveTo(currentTargetPoint, ActiveProfile.patrolSpeed);
            if (Vector2.Distance(transform.position, currentTargetPoint) <= stopDistance)
            {
                PickNewPatrolTarget();
            }
        }

        private void TickChase()
        {
            bool concealed = IsPlayerConcealed(out float pierceFactor);
            bool fullyConcealed = concealed && pierceFactor <= 0.001f;
            float chaseSpeedScale = concealed ? Mathf.Lerp(0.6f, 1f, pierceFactor) : 1f;

            if (player != null)
            {
                float distanceToPlayer = Vector2.Distance(transform.position, player.position);
                if (distanceToPlayer >= chaseDisengageDistance)
                {
                    chaseDistanceOutOfRangeTimer += Time.deltaTime;
                }
                else
                {
                    chaseDistanceOutOfRangeTimer = 0f;
                }
            }

            if (player != null && !fullyConcealed)
            {
                currentTargetPoint = ClampToGeneratedMapBounds(player.position);
                hasCurrentTarget = true;
                MoveTo(currentTargetPoint, ActiveProfile.chaseSpeed * chaseSpeedScale);
            }
            else if (hasCurrentTarget)
            {
                MoveTo(currentTargetPoint, ActiveProfile.chaseSpeed * 0.9f * chaseSpeedScale);
            }

            if (fullyConcealed)
            {
                float lostSightGrace = Mathf.Max(ActiveProfile.chaseForgetSeconds, EffectiveDisengageLostSightGraceSeconds);
                lastSeenTime = Mathf.Min(lastSeenTime, Time.time - lostSightGrace);
            }

            float visualForgetSeconds = Mathf.Max(ActiveProfile.chaseForgetSeconds, EffectiveDisengageLostSightGraceSeconds);
            bool lostVisualTooLong = Time.time - lastSeenTime >= visualForgetSeconds;
            bool tooFarTooLong = chaseDistanceOutOfRangeTimer >= EffectiveDisengageDistanceGraceSeconds;

            if (lostVisualTooLong || tooFarTooLong)
            {
                predictedEscapeDirection = memory.PredictEscapeDirection();

                if (memory.HasLastSeenPosition)
                {
                    float predictionDistance = 2f + ActiveProfile.predictionBias * 2f + currentPredictionWeight * 1.6f;
                    investigateTarget = ClampToGeneratedMapBounds(memory.LastSeenPosition + predictedEscapeDirection * predictionDistance);
                }
                else
                {
                    investigateTarget = ClampToGeneratedMapBounds(transform.position);
                }

                SetState(EnemyStateId.Search, tooFarTooLong ? "LostDistance" : "LostVisual");
            }
        }

        private void TickSearch()
        {
            if (!hasSearchPlan)
            {
                BuildSearchPlan();
            }

            if (searchPriority.Count == 0)
            {
                SetState(EnemyStateId.Return, "EmptySearchPlan");
                return;
            }

            Vector2 target = ClampToGeneratedMapBounds(searchPriority[searchIndex]);
            currentTargetPoint = target;
            hasCurrentTarget = true;
            MoveTo(target, ActiveProfile.investigateSpeed);

            if (Vector2.Distance(transform.position, target) <= stopDistance)
            {
                searchIndex++;
                if (searchIndex >= searchPriority.Count)
                {
                    searchIndex = searchPriority.Count - 1;
                }
            }

            if (stateTimer >= ActiveProfile.searchDurationSeconds || searchIndex >= searchPriority.Count - 1)
            {
                SetState(EnemyStateId.Return, "SearchFinished");
            }
        }

        private void BuildSearchPlan()
        {
            hasSearchPlan = true;
            searchIndex = 0;
            searchPriority.Clear();

            float learnedSearchBreadth = Mathf.Clamp01(ActiveProfile.searchBreadth + currentLearningWeight * 0.35f);
            int targetCount = Mathf.Max(2, Mathf.RoundToInt(maxSearchPoints * Mathf.Lerp(0.6f, 1.55f, learnedSearchBreadth)));
            List<Vector2> points = memory.GetPreferredSearchPoints(targetCount, ActiveProfile.memoryCellSize);

            if (points.Count == 0)
            {
                points.Add(ClampToGeneratedMapBounds(investigateTarget));
            }

            for (int i = 0; i < points.Count; i++)
            {
                searchPriority.Add(ClampToGeneratedMapBounds(points[i]));
            }
        }

        private void PickNewPatrolTarget()
        {
            Vector2 offset = Random.insideUnitCircle * patrolRadius;
            currentTargetPoint = ClampToGeneratedMapBounds(homePosition + offset);
            hasCurrentTarget = true;
        }

        private void MoveTo(Vector2 target, float speed)
        {
            Vector2 intendedTarget = ClampToGeneratedMapBounds(target);
            target = ResolveActiveMovementTarget(intendedTarget);
            Vector2 current = transform.position;
            if (TryRecoverCurrentMovementOverlap(current, intendedTarget, out Vector2 recoveredPosition))
            {
                ApplyMovementPosition(recoveredPosition);
                RegisterMovementRecovery("Overlap", current, recoveredPosition, overlapRecovery: true);
                current = recoveredPosition;
            }

            float requestedDistance = Mathf.Max(0.1f, speed) * Time.deltaTime;
            Vector2 toTarget = target - current;
            float toTargetDistance = toTarget.magnitude;
            if (toTargetDistance > 0.000001f && requestedDistance > 0f)
            {
                float desiredDistance = Mathf.Min(requestedDistance, toTargetDistance);
                Vector2 moveDirection = toTarget / Mathf.Max(0.0001f, toTargetDistance);
                float allowedDistance = ResolveAllowedMovementDistance(current, moveDirection, desiredDistance);
                Vector2 next = current + moveDirection * allowedDistance;
                bool blocked = allowedDistance < desiredDistance - 0.0001f;
                if (blocked && TryResolveWallSlide(current, moveDirection, desiredDistance, intendedTarget, out Vector2 slidePosition))
                {
                    next = slidePosition;
                    blocked = false;
                }

                next = ClampToGeneratedMapBounds(next);
                ApplyMovementPosition(next);
                UpdateMovementStuckRecovery(intendedTarget, current, next, toTargetDistance, desiredDistance, blocked);
            }
            else
            {
                ResetMovementStuckTracking();
            }

            Vector2 direction = target - current;
            if (direction.sqrMagnitude > 0.001f)
            {
                transform.right = direction.normalized;
            }
        }

        private void ApplyMovementPosition(Vector2 position)
        {
            if (movementBody2D != null && movementBody2D.simulated && movementBody2D.bodyType != RigidbodyType2D.Static)
            {
                movementBody2D.MovePosition(position);
                return;
            }

            transform.position = position;
        }

        private float ResolveAllowedMovementDistance(Vector2 current, Vector2 direction, float desiredDistance)
        {
            float safeDesiredDistance = Mathf.Max(0f, desiredDistance);
            if (!preventMovementThroughColliders || safeDesiredDistance <= 0.00001f)
            {
                return safeDesiredDistance;
            }

            float safeSkin = Mathf.Clamp(movementCollisionSkin, 0f, 0.2f);
            float castDistance = safeDesiredDistance + safeSkin;
            bool useTriggers = !ignoreTriggerBlockers;
            int hitCount = 0;
            ContactFilter2D filter = new()
            {
                useLayerMask = true,
                layerMask = movementBlockerMask,
                useTriggers = useTriggers
            };

            if (movementBody2D != null && movementBody2D.simulated && movementBody2D.bodyType != RigidbodyType2D.Static)
            {
                hitCount = movementBody2D.Cast(direction, filter, movementCastHits, castDistance);
            }

            if (hitCount <= 0)
            {
                float radius = Mathf.Max(0.01f, movementCollisionRadius);
                hitCount = Physics2D.CircleCast(current, radius, direction, filter, movementCastHits, castDistance);
            }

            if (hitCount <= 0)
            {
                return safeDesiredDistance;
            }

            float bestDistance = safeDesiredDistance;
            bool blocked = false;
            int safeHitCount = Mathf.Min(hitCount, movementCastHits.Length);
            for (int i = 0; i < safeHitCount; i++)
            {
                RaycastHit2D hit = movementCastHits[i];
                Collider2D hitCollider = hit.collider;
                if (hitCollider == null)
                {
                    continue;
                }

                if (hitCollider == movementCollider2D || hitCollider.transform == transform)
                {
                    continue;
                }

                if (!IsMovementBlockingCollider(hitCollider))
                {
                    continue;
                }

                if (hit.distance <= 0.0001f && Vector2.Dot(direction, hit.normal) >= -0.01f)
                {
                    // Ignore overlap contacts that are not opposing current move direction.
                    continue;
                }

                float allowed = Mathf.Max(0f, hit.distance - safeSkin);
                bestDistance = Mathf.Min(bestDistance, allowed);
                blocked = true;
            }

            return blocked ? Mathf.Clamp(bestDistance, 0f, safeDesiredDistance) : safeDesiredDistance;
        }

        private Vector2 ResolveActiveMovementTarget(Vector2 intendedTarget)
        {
            if (!hasMovementRecoveryWaypoint)
            {
                return ResolveSteeringMovementTarget(intendedTarget);
            }

            bool expired = Time.time >= movementRecoveryWaypointUntil;
            bool reached = Vector2.Distance(transform.position, movementRecoveryWaypoint) <= Mathf.Max(stopDistance, 0.12f);
            if (expired || reached)
            {
                hasMovementRecoveryWaypoint = false;
                return ResolveSteeringMovementTarget(intendedTarget);
            }

            return ClampToGeneratedMapBounds(movementRecoveryWaypoint);
        }

        private Vector2 ResolveSteeringMovementTarget(Vector2 intendedTarget)
        {
            if (!enableMovementSteeringWaypoints)
            {
                ClearMovementSteeringWaypoint();
                return intendedTarget;
            }

            Vector2 current = transform.position;
            if (hasMovementSteeringWaypoint)
            {
                bool expired = Time.time >= movementSteeringWaypointUntil;
                bool reached = Vector2.Distance(current, movementSteeringWaypoint) <= Mathf.Max(stopDistance, steeringWaypointReachDistance);
                bool intentChanged = Vector2.Distance(intendedTarget, movementSteeringIntent) >= Mathf.Max(0.05f, steeringIntentRefreshDistance);
                if (!expired && !reached && !intentChanged)
                {
                    return ClampToGeneratedMapBounds(movementSteeringWaypoint);
                }

                ClearMovementSteeringWaypoint();
            }

            if (Time.time < nextMovementSteeringEvaluateTime)
            {
                return intendedTarget;
            }

            nextMovementSteeringEvaluateTime = Time.time + Mathf.Max(0.05f, steeringRepathInterval);
            if (!IsDirectMovementBlockedForSteering(current, intendedTarget))
            {
                return intendedTarget;
            }

            if (!TryFindMovementSteeringWaypoint(current, intendedTarget, out Vector2 waypoint))
            {
                return intendedTarget;
            }

            movementSteeringWaypoint = waypoint;
            movementSteeringIntent = intendedTarget;
            movementSteeringWaypointUntil = Time.time + Mathf.Max(0.1f, steeringWaypointHoldSeconds);
            hasMovementSteeringWaypoint = true;
            return movementSteeringWaypoint;
        }

        private bool IsDirectMovementBlockedForSteering(Vector2 current, Vector2 intendedTarget)
        {
            if (!preventMovementThroughColliders)
            {
                return false;
            }

            Vector2 toTarget = intendedTarget - current;
            float distance = toTarget.magnitude;
            if (distance <= Mathf.Max(stopDistance * 1.6f, 0.25f))
            {
                return false;
            }

            Vector2 direction = toTarget / Mathf.Max(0.0001f, distance);
            float probeDistance = Mathf.Min(distance, Mathf.Max(0.2f, steeringBlockedProbeDistance));
            float allowed = ResolveAllowedMovementDistance(current, direction, probeDistance);
            return allowed < probeDistance - Mathf.Max(0.015f, movementCollisionSkin * 1.5f);
        }

        private bool TryFindMovementSteeringWaypoint(Vector2 current, Vector2 intendedTarget, out Vector2 waypoint)
        {
            waypoint = current;
            if (!hasGeneratedWalkableCellCache || generatedWalkableCellCenters.Count <= 0)
            {
                return false;
            }

            float searchRadius = Mathf.Max(0.5f, steeringWaypointSearchRadius);
            float currentTargetDistance = Vector2.Distance(current, intendedTarget);
            Vector2 targetDirection = intendedTarget - current;
            if (targetDirection.sqrMagnitude > 0.0001f)
            {
                targetDirection.Normalize();
            }

            float bestScore = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < generatedWalkableCellCenters.Count; i++)
            {
                Vector2 candidate = ClampToGeneratedMapBounds(generatedWalkableCellCenters[i]);
                Vector2 toCandidate = candidate - current;
                float candidateDistance = toCandidate.magnitude;
                if (candidateDistance < Mathf.Max(steeringWaypointReachDistance, 0.18f) || candidateDistance > searchRadius)
                {
                    continue;
                }

                Vector2 candidateDirection = toCandidate / Mathf.Max(0.0001f, candidateDistance);
                float allowed = ResolveAllowedMovementDistance(current, candidateDirection, candidateDistance);
                if (allowed < candidateDistance - Mathf.Max(0.04f, movementCollisionSkin * 2f))
                {
                    continue;
                }

                float targetDistance = Vector2.Distance(candidate, intendedTarget);
                if (targetDistance > currentTargetDistance + 0.65f)
                {
                    continue;
                }

                float forwardScore = targetDirection.sqrMagnitude > 0.0001f
                    ? 1f - Mathf.Clamp01((Vector2.Dot(candidateDirection, targetDirection) + 1f) * 0.5f)
                    : 0f;
                float score = targetDistance + candidateDistance * 0.22f + forwardScore * steeringForwardBias;
                if (score < bestScore)
                {
                    bestScore = score;
                    waypoint = candidate;
                    found = true;
                }
            }

            return found;
        }

        private void ClearMovementSteeringWaypoint()
        {
            hasMovementSteeringWaypoint = false;
            movementSteeringWaypoint = Vector2.zero;
            movementSteeringIntent = Vector2.zero;
            movementSteeringWaypointUntil = 0f;
        }

        private void ClearMovementRecoveryWaypoint()
        {
            hasMovementRecoveryWaypoint = false;
            movementRecoveryWaypoint = Vector2.zero;
            movementRecoveryWaypointUntil = 0f;
        }

        private bool TryResolveWallSlide(Vector2 current, Vector2 moveDirection, float desiredDistance, Vector2 intendedTarget, out Vector2 slidePosition)
        {
            slidePosition = current;
            if (!enableWallSlide || desiredDistance <= 0.0001f || moveDirection.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            int pairs = Mathf.Clamp(wallSlideProbePairs, 1, 4);
            float maxAngle = Mathf.Clamp(wallSlideProbeAngle, 20f, 85f);
            float currentTargetSqr = (intendedTarget - current).sqrMagnitude;
            float bestScore = float.PositiveInfinity;
            bool hasSlide = false;

            for (int i = 0; i < pairs; i++)
            {
                float t = (i + 1f) / pairs;
                float angle = Mathf.Lerp(28f, maxAngle, t);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector2 slideDirection = Quaternion.Euler(0f, 0f, angle * side) * moveDirection;
                    float allowed = ResolveAllowedMovementDistance(current, slideDirection, desiredDistance);
                    if (allowed <= Mathf.Max(0.001f, wallSlideMinDistance))
                    {
                        continue;
                    }

                    Vector2 candidate = ClampToGeneratedMapBounds(current + slideDirection.normalized * allowed);
                    float candidateMoveSqr = (candidate - current).sqrMagnitude;
                    if (candidateMoveSqr <= wallSlideMinDistance * wallSlideMinDistance)
                    {
                        continue;
                    }

                    float targetSqr = (intendedTarget - candidate).sqrMagnitude;
                    if (targetSqr > currentTargetSqr + 0.45f)
                    {
                        continue;
                    }

                    float score = targetSqr - candidateMoveSqr * 0.08f + i * 0.03f;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        slidePosition = candidate;
                        hasSlide = true;
                    }
                }
            }

            return hasSlide;
        }

        private void UpdateMovementStuckRecovery(
            Vector2 intendedTarget,
            Vector2 from,
            Vector2 to,
            float targetDistance,
            float desiredDistance,
            bool blocked)
        {
            if (!enableStuckRecovery || targetDistance <= Mathf.Max(stopDistance * 1.5f, 0.18f))
            {
                ResetMovementStuckTracking();
                return;
            }

            float moved = Vector2.Distance(from, to);
            float minimumMove = Mathf.Max(0.001f, Mathf.Min(stuckRecoveryMinMoveDistance, desiredDistance * 0.45f));
            if (!blocked && moved >= minimumMove)
            {
                stuckElapsed = 0f;
                return;
            }

            stuckElapsed += Time.deltaTime;
            if (stuckElapsed < Mathf.Max(0.05f, stuckRecoverySeconds) || Time.time < nextStuckRecoveryTime)
            {
                return;
            }

            if (TryFindMovementRecoveryWaypoint(to, intendedTarget, out Vector2 waypoint))
            {
                movementRecoveryWaypoint = waypoint;
                movementRecoveryWaypointUntil = Time.time + Mathf.Max(0.1f, stuckRecoveryWaypointHoldSeconds);
                hasMovementRecoveryWaypoint = true;
                ClearMovementSteeringWaypoint();
                RegisterMovementRecovery("Waypoint", to, waypoint, overlapRecovery: false);
            }
            else if (TryBuildMovementNudge(to, out Vector2 nudgeTarget))
            {
                Vector2 clampedNudge = ClampToGeneratedMapBounds(nudgeTarget);
                ApplyMovementPosition(clampedNudge);
                ClearMovementSteeringWaypoint();
                RegisterMovementRecovery("Nudge", to, clampedNudge, overlapRecovery: false);
            }

            stuckElapsed = 0f;
            nextStuckRecoveryTime = Time.time + Mathf.Max(0.05f, stuckRecoveryCooldownSeconds);
        }

        private void ResetMovementStuckTracking()
        {
            stuckElapsed = 0f;
            if (hasMovementRecoveryWaypoint && Time.time >= movementRecoveryWaypointUntil)
            {
                hasMovementRecoveryWaypoint = false;
            }
        }

        private bool TryRecoverCurrentMovementOverlap(Vector2 current, Vector2 intendedTarget, out Vector2 recoveredPosition)
        {
            recoveredPosition = current;
            if (!enableStuckRecovery || !recoverFromMovementOverlap || !preventMovementThroughColliders)
            {
                return false;
            }

            if (!IsMovementPositionBlocked(current))
            {
                return false;
            }

            if (TryBuildMovementNudge(current, out Vector2 nudgeTarget))
            {
                recoveredPosition = ClampToGeneratedMapBounds(nudgeTarget);
                return true;
            }

            return TryFindOverlapRecoveryCandidate(current, intendedTarget, out recoveredPosition);
        }

        private bool TryFindOverlapRecoveryCandidate(Vector2 current, Vector2 intendedTarget, out Vector2 recoveredPosition)
        {
            recoveredPosition = current;
            float radius = Mathf.Max(0.1f, overlapRecoverySearchRadius);
            float bestScore = float.PositiveInfinity;
            bool found = false;

            if (hasGeneratedWalkableCellCache && generatedWalkableCellCenters.Count > 0)
            {
                for (int i = 0; i < generatedWalkableCellCenters.Count; i++)
                {
                    Vector2 candidate = ClampToGeneratedMapBounds(generatedWalkableCellCenters[i]);
                    float distance = Vector2.Distance(current, candidate);
                    if (distance < 0.12f || distance > radius || IsMovementPositionBlocked(candidate))
                    {
                        continue;
                    }

                    float score = Vector2.Distance(candidate, intendedTarget) + distance * 0.24f;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        recoveredPosition = candidate;
                        found = true;
                    }
                }
            }

            int probeCount = Mathf.Clamp(overlapRecoveryProbeCount, 8, 24);
            for (int ring = 1; ring <= 3; ring++)
            {
                float distance = radius * (ring / 3f);
                for (int i = 0; i < probeCount; i++)
                {
                    float angle = i * (360f / probeCount);
                    Vector2 direction = Quaternion.Euler(0f, 0f, angle) * Vector2.right;
                    Vector2 candidate = ClampToGeneratedMapBounds(current + direction * distance);
                    if (Vector2.Distance(current, candidate) < 0.08f || IsMovementPositionBlocked(candidate))
                    {
                        continue;
                    }

                    float score = Vector2.Distance(candidate, intendedTarget) + Vector2.Distance(current, candidate) * 0.32f;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        recoveredPosition = candidate;
                        found = true;
                    }
                }

                if (found)
                {
                    return true;
                }
            }

            return found;
        }

        private bool IsMovementPositionBlocked(Vector2 position)
        {
            int hitCount = QueryMovementOverlapHits(position);
            int safeHitCount = Mathf.Min(hitCount, movementOverlapHits.Length);
            for (int i = 0; i < safeHitCount; i++)
            {
                if (IsMovementBlockingCollider(movementOverlapHits[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private int QueryMovementOverlapHits(Vector2 position)
        {
            if (!preventMovementThroughColliders)
            {
                return 0;
            }

            ContactFilter2D filter = new()
            {
                useLayerMask = true,
                layerMask = movementBlockerMask,
                useTriggers = !ignoreTriggerBlockers
            };
            float radius = Mathf.Max(0.01f, movementCollisionRadius);
            return Physics2D.OverlapCircle(position, radius, filter, movementOverlapHits);
        }

        private void RegisterMovementRecovery(string reason, Vector2 from, Vector2 to, bool overlapRecovery)
        {
            movementRecoveryCount++;
            if (overlapRecovery)
            {
                movementOverlapRecoveryCount++;
            }

            lastMovementRecoveryReason = string.IsNullOrWhiteSpace(reason) ? "Unknown" : reason;
            lastMovementRecoveryFrom = from;
            lastMovementRecoveryTo = to;
        }

        private bool TryFindMovementRecoveryWaypoint(Vector2 current, Vector2 intendedTarget, out Vector2 waypoint)
        {
            waypoint = current;
            float radius = Mathf.Max(0.2f, stuckRecoveryWaypointRadius);
            float bestScore = float.PositiveInfinity;
            bool found = false;

            if (hasGeneratedWalkableCellCache && generatedWalkableCellCenters.Count > 0)
            {
                for (int i = 0; i < generatedWalkableCellCenters.Count; i++)
                {
                    Vector2 candidate = ClampToGeneratedMapBounds(generatedWalkableCellCenters[i]);
                    Vector2 toCandidate = candidate - current;
                    float distance = toCandidate.magnitude;
                    if (distance < 0.16f || distance > radius)
                    {
                        continue;
                    }

                    Vector2 direction = toCandidate / Mathf.Max(0.0001f, distance);
                    float allowed = ResolveAllowedMovementDistance(current, direction, distance);
                    if (allowed < distance - Mathf.Max(0.04f, movementCollisionSkin * 2f))
                    {
                        continue;
                    }

                    float score = Vector2.Distance(candidate, intendedTarget) + distance * 0.18f;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        waypoint = candidate;
                        found = true;
                    }
                }
            }

            if (found)
            {
                return true;
            }

            const int probeCount = 12;
            for (int i = 0; i < probeCount; i++)
            {
                float angle = i * (360f / probeCount);
                Vector2 direction = Quaternion.Euler(0f, 0f, angle) * Vector2.right;
                float probeDistance = radius * 0.65f;
                float allowed = ResolveAllowedMovementDistance(current, direction, probeDistance);
                if (allowed < probeDistance * 0.55f)
                {
                    continue;
                }

                Vector2 candidate = ClampToGeneratedMapBounds(current + direction * allowed);
                float score = Vector2.Distance(candidate, intendedTarget);
                if (score < bestScore)
                {
                    bestScore = score;
                    waypoint = candidate;
                    found = true;
                }
            }

            return found;
        }

        private bool TryBuildMovementNudge(Vector2 current, out Vector2 nudgeTarget)
        {
            nudgeTarget = current;
            if (!preventMovementThroughColliders)
            {
                return false;
            }

            int hitCount = QueryMovementOverlapHits(current);
            if (hitCount <= 0)
            {
                return false;
            }

            Vector2 combinedAway = Vector2.zero;
            int safeHitCount = Mathf.Min(hitCount, movementOverlapHits.Length);
            for (int i = 0; i < safeHitCount; i++)
            {
                Collider2D hitCollider = movementOverlapHits[i];
                if (!IsMovementBlockingCollider(hitCollider))
                {
                    continue;
                }

                Vector2 closest = hitCollider.ClosestPoint(current);
                Vector2 away = current - closest;
                if (away.sqrMagnitude <= 0.0001f)
                {
                    away = current - (Vector2)hitCollider.bounds.center;
                }

                if (away.sqrMagnitude <= 0.0001f)
                {
                    away = -(Vector2)transform.right;
                }

                combinedAway += away.normalized;
            }

            if (combinedAway.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            nudgeTarget = current + combinedAway.normalized * Mathf.Max(0.01f, stuckRecoveryNudgeDistance);
            return !IsMovementPositionBlocked(ClampToGeneratedMapBounds(nudgeTarget));
        }

        private bool IsMovementBlockingCollider(Collider2D hitCollider)
        {
            if (hitCollider == null)
            {
                return false;
            }

            if (hitCollider == movementCollider2D || hitCollider.transform == transform)
            {
                return false;
            }

            if (hitCollider.transform != null && hitCollider.transform.IsChildOf(transform))
            {
                return false;
            }

            if (ignoreTriggerBlockers && hitCollider.isTrigger)
            {
                return false;
            }

            if (IsSelfOrPlayerCollider(hitCollider))
            {
                return false;
            }

            return !movementBlockGeneratedOccludersOnly || IsGeneratedOccluderCollider(hitCollider.transform);
        }

        private Vector2 ClampToGeneratedMapBounds(Vector2 point)
        {
            if (!constrainMovementToGeneratedMapBounds)
            {
                return point;
            }

            RefreshGeneratedMapBounds(force: false);
            if (!hasGeneratedMapBounds)
            {
                return point;
            }

            Vector2 boundedPoint = new(
                Mathf.Clamp(point.x, generatedMapMin.x, generatedMapMax.x),
                Mathf.Clamp(point.y, generatedMapMin.y, generatedMapMax.y));
            return ClampPointToGeneratedWalkableCells(boundedPoint);
        }

        private void RefreshGeneratedMapBounds(bool force)
        {
            if (!constrainMovementToGeneratedMapBounds)
            {
                hasGeneratedMapBounds = false;
                ClearGeneratedWalkableCellCache();
                return;
            }

            if (!force && Time.time < nextMapBoundsRefreshTime)
            {
                return;
            }

            nextMapBoundsRefreshTime = Time.time + Mathf.Max(0.05f, mapBoundsRefreshInterval);

            if (mapSystem == null)
            {
                mapSystem = FindFirstObjectByType<MapSystem>();
            }

            if (mapSystem == null || mapSystem.LastGeneratedCells == null || mapSystem.LastGeneratedCells.Count <= 0)
            {
                hasGeneratedMapBounds = false;
                ClearGeneratedWalkableCellCache();
                return;
            }

            Vector2 size = mapSystem.LastGeneratedWorldSize;
            if (size.x <= 0.001f || size.y <= 0.001f)
            {
                hasGeneratedMapBounds = false;
                ClearGeneratedWalkableCellCache();
                return;
            }

            Vector2 center = mapSystem.LastGeneratedWorldCenter;
            Vector2 half = (size * 0.5f) - Vector2.one * Mathf.Max(0f, mapBoundsPadding);
            half = new Vector2(Mathf.Max(0.2f, half.x), Mathf.Max(0.2f, half.y));
            generatedMapMin = center - half;
            generatedMapMax = center + half;
            hasGeneratedMapBounds = true;
            RebuildGeneratedWalkableCellCache(mapSystem.LastGeneratedCells, mapSystem.CellSize);
        }

        private Vector2 ClampPointToGeneratedWalkableCells(Vector2 point)
        {
            if (!hasGeneratedWalkableCellCache || generatedWalkableCellCenters.Count <= 0)
            {
                return point;
            }

            float halfExtent = Mathf.Max(0.05f, generatedWalkableCellHalfExtent);
            Vector2 nearestPoint = point;
            float nearestSqrDistance = float.PositiveInfinity;

            for (int i = 0; i < generatedWalkableCellCenters.Count; i++)
            {
                Vector2 center = generatedWalkableCellCenters[i];
                float clampedX = Mathf.Clamp(point.x, center.x - halfExtent, center.x + halfExtent);
                float clampedY = Mathf.Clamp(point.y, center.y - halfExtent, center.y + halfExtent);
                Vector2 candidate = new(clampedX, clampedY);
                float sqrDistance = (candidate - point).sqrMagnitude;
                if (sqrDistance <= 0.000001f)
                {
                    return point;
                }

                if (sqrDistance < nearestSqrDistance)
                {
                    nearestSqrDistance = sqrDistance;
                    nearestPoint = candidate;
                }
            }

            return nearestPoint;
        }

        private void RebuildGeneratedWalkableCellCache(IReadOnlyList<GeneratedMapCell> generatedCells, float generatedCellSize)
        {
            generatedWalkableCellCenters.Clear();
            hasGeneratedWalkableCellCache = false;
            generatedWalkableCellHalfExtent = 0.5f;

            if (generatedCells == null || generatedCells.Count <= 0)
            {
                return;
            }

            float safeCellSize = Mathf.Max(0.2f, generatedCellSize);
            float safeInset = Mathf.Clamp(mapCellInset, 0f, safeCellSize * 0.45f);
            generatedWalkableCellHalfExtent = Mathf.Max(0.05f, safeCellSize * 0.5f - safeInset);

            for (int i = 0; i < generatedCells.Count; i++)
            {
                Vector2Int cell = generatedCells[i].position;
                generatedWalkableCellCenters.Add(new Vector2(cell.x * safeCellSize, cell.y * safeCellSize));
            }

            hasGeneratedWalkableCellCache = generatedWalkableCellCenters.Count > 0;
        }

        private void ClearGeneratedWalkableCellCache()
        {
            generatedWalkableCellCenters.Clear();
            generatedWalkableCellHalfExtent = 0.5f;
            hasGeneratedWalkableCellCache = false;
        }

        private bool TryDetectPlayer()
        {
            if (player == null)
            {
                lastSmokeOcclusion = 0f;
                lastRawSmokeOcclusion = 0f;
                return false;
            }

            bool concealed = IsPlayerConcealed(out float pierceFactor);
            if (concealed && pierceFactor <= 0.001f)
            {
                lastSmokeOcclusion = 0f;
                lastRawSmokeOcclusion = 0f;
                lastDetectionReason = "SafeHavenConcealment";
                return false;
            }

            float rawSmokeOcclusion = SmokeScreenFieldDummy.EvaluateVisionBlock(transform.position, player.position);
            float smokePenetration = Mathf.Clamp01(ActiveProfile.smokeVisionPenetration);
            float smokeOcclusion = rawSmokeOcclusion * (1f - smokePenetration);
            lastRawSmokeOcclusion = rawSmokeOcclusion;
            lastSmokeOcclusion = smokeOcclusion;

            if (smokeOcclusion >= 0.98f)
            {
                lastDetectionReason = concealed ? "SafeHaven+Smoke" : "SmokeCurtain";
                return DetectByFlashlightHint(concealed, pierceFactor, smokeOcclusion);
            }

            float smokeVisionScale = Mathf.Lerp(1f, 0.14f, smokeOcclusion);
            float smokeAngleScale = Mathf.Lerp(1f, 0.5f, smokeOcclusion);
            float concealVisionScale = concealed ? Mathf.Lerp(0.2f, 1f, pierceFactor) : 1f;
            float concealAngleScale = concealed ? Mathf.Lerp(0.35f, 1f, pierceFactor) : 1f;

            Vector2 toPlayer = player.position - transform.position;
            float effectiveVisionRange = visionRange * runtimeVisionRangeMultiplier * ActiveProfile.lightSensitivity * concealVisionScale * smokeVisionScale;
            if (toPlayer.sqrMagnitude > effectiveVisionRange * effectiveVisionRange)
            {
                return DetectByFlashlightHint(concealed, pierceFactor, smokeOcclusion);
            }

            float angle = Vector2.Angle(transform.right, toPlayer);
            if (angle > visionAngle * 0.5f * concealAngleScale * smokeAngleScale)
            {
                return DetectByFlashlightHint(concealed, pierceFactor, smokeOcclusion);
            }

            if (IsLineOfSightBlocked(transform.position, player.position))
            {
                return DetectByFlashlightHint(concealed, pierceFactor, smokeOcclusion);
            }

            if (smokeOcclusion > 0.01f)
            {
                lastDetectionReason = concealed ? "SafeHavenLeakVision+Smoke" : "VisionThroughSmoke";
            }
            else
            {
                lastDetectionReason = concealed ? "SafeHavenLeakVision" : "Vision";
            }

            return true;
        }

        private bool DetectByFlashlightHint(bool concealed, float pierceFactor, float smokeOcclusion = 0f)
        {
            if (player == null)
            {
                return false;
            }

            if (concealed && pierceFactor <= 0.001f)
            {
                return false;
            }

            PlayerVisibilitySource visibility = player.GetComponent<PlayerVisibilitySource>();
            if (visibility == null)
            {
                return false;
            }

            if (!visibility.IsPointInsideFlashlight(transform.position))
            {
                return false;
            }

            float smokeScale = Mathf.Lerp(1f, 0.2f, Mathf.Clamp01(smokeOcclusion));
            if (smokeScale <= 0.001f)
            {
                return false;
            }

            float concealScale = concealed ? Mathf.Lerp(0.25f, 1f, pierceFactor) : 1f;
            suspicion += Time.deltaTime * 0.4f * ActiveProfile.lightSensitivity * concealScale * smokeScale;
            investigateTarget = player.position;

            lastDetectionReason = concealed ? "SafeHavenLeakFlashlight" : "FlashlightCone";
            if (smokeOcclusion > 0.01f)
            {
                lastDetectionReason += "+Smoke";
            }

            if (suspicion >= ActiveProfile.suspicionToInvestigate && currentState != EnemyStateId.Chase)
            {
                SetState(EnemyStateId.Investigate, "FlashlightInvestigate");
            }

            return false;
        }

        private void RefreshLearningSnapshot()
        {
            nextLearningRefreshTime = Time.time + 0.45f;

            PlayerBehaviorTelemetry telemetry = PlayerBehaviorTelemetry.Instance;
            if (telemetry == null)
            {
                currentLearningPhase = EnemyLearningPhase.Early;
                currentLearningWeight = 0.25f;
                currentPredictionWeight = 0.2f;
                currentBehaviorScore = 0f;
                return;
            }

            LearningSnapshot snapshot = telemetry.GetSnapshot();
            currentLearningPhase = snapshot.Phase;
            currentLearningWeight = snapshot.LearningWeight;
            currentPredictionWeight = snapshot.PredictionWeight;
            currentBehaviorScore = snapshot.BehaviorScore;
        }

        private void OnNoiseRaised(NoiseEvent noiseEvent)
        {
            if (currentState == EnemyStateId.Stunned)
            {
                return;
            }

            bool concealedPlayerNoise = false;
            float concealPierce = 1f;

            if (player != null && noiseEvent.Source == player.gameObject)
            {
                concealedPlayerNoise = IsPlayerConcealed(out concealPierce);
                if (concealedPlayerNoise && concealPierce <= 0.001f)
                {
                    return;
                }
            }

            float concealNoiseScale = concealedPlayerNoise ? Mathf.Lerp(0.22f, 1f, concealPierce) : 1f;
            float noiseKindWeight = EvaluateNoiseResponse(noiseEvent.Kind);
            if (noiseKindWeight <= 0.001f)
            {
                return;
            }

            float transmission = 1f;
            int wallHits = 0;
            if (useWallOcclusionForNoise)
            {
                transmission = EvaluateNoiseTransmission(noiseEvent.Position, noiseEvent.Kind, out wallHits);
            }

            lastNoiseTransmission = transmission;
            lastNoiseWallHits = wallHits;
            lastNoiseKind = noiseEvent.Kind;

            float hearingRange = noiseEvent.Radius
                                 * ActiveProfile.audioSensitivity
                                 * runtimeHearingRangeMultiplier
                                 * concealNoiseScale
                                 * Mathf.Clamp(noiseKindWeight, 0.15f, 1.6f)
                                 * transmission;

            float distance = Vector2.Distance(transform.position, noiseEvent.Position);
            if (distance > hearingRange)
            {
                return;
            }

            bool isDecoyNoise = noiseEvent.Kind == NoiseKind.Decoy;
            bool allowInvestigateRetarget = currentState != EnemyStateId.Chase || !isDecoyNoise || noiseKindWeight >= 0.85f;

            Vector2 perceivedNoisePosition = BuildPerceivedNoiseTarget(noiseEvent.Position, noiseEvent.Kind, transmission, wallHits);

            memory.RecordNoise(perceivedNoisePosition, noiseEvent.Loudness * noiseKindWeight * transmission, ActiveProfile.memoryCellSize);
            if (allowInvestigateRetarget)
            {
                investigateTarget = perceivedNoisePosition;
            }

            float distanceFactor = 1f - Mathf.Clamp01(distance / Mathf.Max(0.001f, hearingRange));
            float learningSuspicionScale = Mathf.Lerp(1f, 1.35f, currentLearningWeight);
            float occlusionSuspenseScale = wallHits > 0 ? Mathf.Lerp(1f, 1.12f, 1f - transmission) : 1f;
            float suspicionTransmissionScale = EvaluateNoiseSuspicionScale(noiseEvent.Kind, transmission, wallHits);
            suspicion += ActiveProfile.suspicionGainPerNoise
                         * runtimeSuspicionGainMultiplier
                         * noiseEvent.Loudness
                         * Mathf.Lerp(0.5f, 1f, distanceFactor)
                         * concealNoiseScale
                         * noiseKindWeight
                         * learningSuspicionScale
                         * suspicionTransmissionScale
                         * occlusionSuspenseScale;
            suspicion = Mathf.Clamp01(suspicion);

            string noiseSuffix = isDecoyNoise ? $"(x{noiseKindWeight:0.00})" : string.Empty;
            string occlusionSuffix = wallHits > 0 ? $"[W{wallHits}/T{transmission:0.00}]" : string.Empty;
            lastDetectionReason = concealedPlayerNoise
                ? $"NoiseLeak:{noiseEvent.Kind}{noiseSuffix}{occlusionSuffix}"
                : $"Noise:{noiseEvent.Kind}{noiseSuffix}{occlusionSuffix}";

            bool canEnterSuspicionFromIdle = !isDecoyNoise || noiseKindWeight >= 0.35f;
            bool canEscalateInvestigate = !isDecoyNoise || noiseKindWeight >= 0.65f;

            if ((currentState == EnemyStateId.IdlePatrol || currentState == EnemyStateId.Resume) && canEnterSuspicionFromIdle)
            {
                SetState(EnemyStateId.Suspicion, lastDetectionReason);
            }
            else if (suspicion >= ActiveProfile.suspicionToChase && currentState != EnemyStateId.Chase && canEscalateInvestigate)
            {
                SetState(EnemyStateId.Investigate, "NoiseEscalation");
            }
        }



        private bool IsLineOfSightBlocked(Vector2 from, Vector2 to)
        {
            LayerMask mask = lineOfSightBlockers;
            if (mask.value != 0)
            {
                RaycastHit2D[] maskedHits = Physics2D.LinecastAll(from, to, mask);
                for (int i = 0; i < maskedHits.Length; i++)
                {
                    Collider2D collider = maskedHits[i].collider;
                    if (!IsOccludingWallCollider(collider))
                    {
                        continue;
                    }

                    return true;
                }
            }

            RaycastHit2D[] fallbackHits = Physics2D.LinecastAll(from, to);
            for (int i = 0; i < fallbackHits.Length; i++)
            {
                Collider2D collider = fallbackHits[i].collider;
                if (!IsOccludingWallCollider(collider))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private float EvaluateNoiseTransmission(Vector2 noisePosition, NoiseKind kind, out int wallHitCount)
        {
            wallHitCount = CountOccludingWalls(transform.position, noisePosition);
            if (wallHitCount <= 0)
            {
                return 1f;
            }

            float transmission = 1f;
            float perWall = EvaluateNoiseTransmissionPerWall(kind);
            for (int i = 0; i < wallHitCount; i++)
            {
                transmission *= perWall;
            }

            return Mathf.Clamp(transmission, minNoiseTransmission, 1f);
        }

        private Vector2 BuildPerceivedNoiseTarget(Vector2 sourcePosition, NoiseKind kind, float transmission, int wallHits)
        {
            if (wallHits <= 0 || occludedNoiseTargetJitter <= 0f)
            {
                return sourcePosition;
            }

            float jitterScale = Mathf.Lerp(0.25f, 1f, 1f - Mathf.Clamp01(transmission));
            float jitterRadius = occludedNoiseTargetJitter * jitterScale * EvaluateOccludedTargetJitterScale(kind);
            return sourcePosition + Random.insideUnitCircle * jitterRadius;
        }

        private int CountOccludingWalls(Vector2 from, Vector2 to)
        {
            HashSet<int> hitIds = new();
            LayerMask mask = noiseOcclusionMask.value != 0 ? noiseOcclusionMask : lineOfSightBlockers;

            if (mask.value != 0)
            {
                RaycastHit2D[] maskedHits = Physics2D.LinecastAll(from, to, mask);
                for (int i = 0; i < maskedHits.Length; i++)
                {
                    Collider2D collider = maskedHits[i].collider;
                    if (!IsOccludingWallCollider(collider))
                    {
                        continue;
                    }

                    hitIds.Add(collider.GetInstanceID());
                }
            }

            RaycastHit2D[] fallbackHits = Physics2D.LinecastAll(from, to);
            for (int i = 0; i < fallbackHits.Length; i++)
            {
                Collider2D collider = fallbackHits[i].collider;
                if (!IsOccludingWallCollider(collider))
                {
                    continue;
                }

                hitIds.Add(collider.GetInstanceID());
            }

            return hitIds.Count;
        }

        private bool IsSelfOrPlayerCollider(Collider2D collider)
        {
            Transform colliderTransform = collider.transform;
            if (colliderTransform == transform || colliderTransform.IsChildOf(transform))
            {
                return true;
            }

            if (player != null && (colliderTransform == player || colliderTransform.IsChildOf(player)))
            {
                return true;
            }

            return false;
        }

        private bool IsOccludingWallCollider(Collider2D collider)
        {
            if (collider == null || collider.isTrigger)
            {
                return false;
            }

            if (IsSelfOrPlayerCollider(collider))
            {
                return false;
            }

            return IsGeneratedOccluderCollider(collider.transform);
        }

        private static bool IsGeneratedOccluderCollider(Transform target)
        {
            Transform current = target;
            while (current != null)
            {
                string nodeName = current.name;
                if (nodeName == "Walls"
                    || nodeName == "Occluders"
                    || nodeName.StartsWith("Wall_", System.StringComparison.Ordinal)
                    || nodeName.StartsWith("GeneratedWalls_Stage_", System.StringComparison.Ordinal)
                    || nodeName.StartsWith("Cover_", System.StringComparison.Ordinal)
                    || nodeName.StartsWith("Choke_", System.StringComparison.Ordinal)
                    || nodeName.StartsWith("GeneratedOccluders_Stage_", System.StringComparison.Ordinal))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private float EvaluateNoiseTransmissionPerWall(NoiseKind kind)
        {
            float value = kind switch
            {
                NoiseKind.Footstep => footstepTransmissionPerWall,
                NoiseKind.Echo => echoTransmissionPerWall,
                NoiseKind.FlashlightToggle => flashlightTransmissionPerWall,
                NoiseKind.ItemUse => itemUseTransmissionPerWall,
                NoiseKind.Decoy => decoyTransmissionPerWall,
                _ => noiseTransmissionPerWall
            };

            return Mathf.Clamp(value, 0.1f, 1f);
        }

        private float EvaluateNoiseSuspicionScale(NoiseKind kind, float transmission, int wallHits)
        {
            float baseScale = Mathf.Lerp(0.45f, 1f, Mathf.Clamp01(transmission));
            if (wallHits <= 0)
            {
                return baseScale;
            }

            float kindBias = kind switch
            {
                NoiseKind.Decoy => 1.1f,
                NoiseKind.Echo => 1.06f,
                NoiseKind.Footstep => 0.92f,
                _ => 1f
            };

            return Mathf.Clamp(baseScale * kindBias, 0.35f, 1f);
        }

        private float EvaluateOccludedTargetJitterScale(NoiseKind kind)
        {
            return kind switch
            {
                NoiseKind.Decoy => 1.35f,
                NoiseKind.Echo => 1.15f,
                NoiseKind.FlashlightToggle => 0.75f,
                NoiseKind.Footstep => 0.85f,
                _ => 1f
            };
        }
        private float EvaluateNoiseResponse(NoiseKind kind)
        {
            return kind switch
            {
                NoiseKind.Decoy => Mathf.Clamp(ActiveProfile.decoyNoiseResponse, 0f, 2f),
                NoiseKind.ItemUse => Mathf.Clamp(ActiveProfile.itemNoiseResponse, 0f, 2f),
                NoiseKind.Echo => 1.05f,
                _ => 1f
            };
        }
        private void SetState(EnemyStateId nextState, string reason)
        {
            EnemyStateId previousState = currentState;

            if (currentState == nextState)
            {
                if (!string.IsNullOrWhiteSpace(reason))
                {
                    lastDetectionReason = reason;
                }

                return;
            }

            currentState = nextState;
            stateTimer = 0f;
            stuckElapsed = 0f;
            ClearMovementRecoveryWaypoint();
            ClearMovementSteeringWaypoint();

            if (!string.IsNullOrWhiteSpace(reason))
            {
                lastDetectionReason = reason;
            }

            if (nextState == EnemyStateId.Investigate)
            {
                hasSearchPlan = false;
            }
            else if (nextState == EnemyStateId.Search)
            {
                hasSearchPlan = false;
            }
            else if (nextState == EnemyStateId.Return)
            {
                hasCurrentTarget = true;
                currentTargetPoint = homePosition;
            }
            else if (nextState == EnemyStateId.IdlePatrol)
            {
                hasSearchPlan = false;
                searchPriority.Clear();
                PickNewPatrolTarget();
            }
            else if (nextState == EnemyStateId.Stunned)
            {
                hasCurrentTarget = false;
                hasSearchPlan = false;
                searchPriority.Clear();
                CancelChaseTransition("Stunned");
                ClearDisengageCue();
            }

            if (nextState == EnemyStateId.Chase)
            {
                chaseDistanceOutOfRangeTimer = 0f;
                chaseTransitionPending = false;
                chaseTransitionTimer = 0f;
                chaseTransitionLostSightTimer = 0f;
                ClearDisengageCue();
                SetChaseMarkerVisible(false);
                RaiseAgentRuntimeEvent($"{name} chase started", RuntimeEventSemantic.ChaseStarted);
            }
            else if (nextState != EnemyStateId.Investigate)
            {
                chaseDistanceOutOfRangeTimer = 0f;
            }

            if (previousState == EnemyStateId.Chase && nextState != EnemyStateId.Chase)
            {
                chaseReacquireBlockedUntil = Time.time + Mathf.Max(0f, chaseReacquireDelaySeconds);
                StartDisengageCue();
                string disengageReason = string.IsNullOrWhiteSpace(lastDetectionReason) ? "unknown" : lastDetectionReason;
                RaiseAgentRuntimeEvent($"{name} chase disengaged ({disengageReason})", RuntimeEventSemantic.ChaseDisengaged);
            }
        }

        private void ResolvePlayerConcealment()
        {
            if (player == null)
            {
                playerConcealment = null;
                return;
            }

            if (playerConcealment == null || playerConcealment.gameObject != player.gameObject)
            {
                playerConcealment = player.GetComponent<PlayerConcealmentState>();
            }
        }

        private bool IsPlayerConcealed(out float pierceFactor)
        {
            ResolvePlayerConcealment();
            pierceFactor = ConcealmentPierce;
            return playerConcealment != null && playerConcealment.IsConcealedFromEnemies;
        }

        private void ApplyStunVisual()
        {
            EnsureSpriteRenderer();
            if (spriteRenderer == null)
            {
                return;
            }

            spriteRenderer.color = Color.Lerp(baseColor, stunnedTint, 0.8f);
            stunTintApplied = true;
            chaseTintApplied = false;
            SetChaseMarkerVisible(false);
        }

        private void ApplyChaseVisual()
        {
            EnsureSpriteRenderer();
            if (spriteRenderer == null)
            {
                return;
            }

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * EffectiveChaseBlinkSpeed);
            Color blinkTarget = Color.Lerp(baseColor, chaseBlinkColor, Mathf.Clamp01(chaseBlinkStrength));
            spriteRenderer.color = Color.Lerp(baseColor, blinkTarget, pulse);
            chaseTintApplied = true;
            stunTintApplied = false;
            SetChaseMarkerVisible(false);
        }

        private void ApplyStateVisuals()
        {
            if (currentState == EnemyStateId.Stunned)
            {
                ApplyStunVisual();
                return;
            }

            if (currentState == EnemyStateId.Chase)
            {
                ApplyChaseVisual();
                return;
            }

            if (chaseTransitionPending)
            {
                EnsureChaseMarker();
                ConfigureChaseMarkerVisual(ChaseMarkerMode.Alert, 1f);
                UpdateChaseMarkerPulse();
                SetChaseMarkerVisible(true);
                ApplyTransitionVisual();
                return;
            }

            if (disengageCueTimer > 0f)
            {
                TickDisengageCueVisual();
                return;
            }

            SetChaseMarkerVisible(false);
            RestoreBaseVisual();
        }

        private void ApplyTransitionVisual()
        {
            EnsureSpriteRenderer();
            if (spriteRenderer == null)
            {
                return;
            }

            float duration = Mathf.Max(0.15f, EffectiveChaseTransitionSeconds);
            float progress = Mathf.Clamp01(chaseTransitionTimer / duration);
            float ramp = Mathf.Pow(progress, Mathf.Max(0.1f, transitionBodyFlashRampExponent));
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * EffectiveTransitionPulseSpeed);
            float flashStrength = Mathf.Clamp01(EffectiveTransitionFlashStrength) * Mathf.Lerp(0.3f, 1f, ramp) * Mathf.Lerp(0.8f, 1f, pulse);
            Color transitionTarget = Color.Lerp(baseColor, chaseBlinkColor, flashStrength);
            spriteRenderer.color = Color.Lerp(baseColor, transitionTarget, Mathf.Lerp(0.5f, 1f, progress));

            chaseTintApplied = true;
            stunTintApplied = false;
        }

        private void TickDisengageCueVisual()
        {
            float effectiveDuration = Mathf.Max(0.01f, EffectiveDisengageCueSeconds);
            disengageCueTimer = Mathf.Max(0f, disengageCueTimer - Time.deltaTime);
            if (disengageCueTimer <= 0f)
            {
                SetChaseMarkerVisible(false);
                RestoreBaseVisual();
                return;
            }

            float normalized = Mathf.Clamp01(disengageCueTimer / effectiveDuration);
            float alpha = Mathf.Lerp(0.28f, 1f, normalized);

            EnsureChaseMarker();
            ConfigureChaseMarkerVisual(ChaseMarkerMode.Disengage, alpha);
            UpdateChaseMarkerPulse();
            SetChaseMarkerVisible(true);

            EnsureSpriteRenderer();
            if (spriteRenderer == null)
            {
                return;
            }

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * EvaluateCurrentMarkerPulseSpeed());
            float tintStrength = Mathf.Lerp(0.08f, 0.35f, pulse) * normalized;
            Color disengageTint = Color.Lerp(baseColor, chaseBlinkColor, tintStrength);
            spriteRenderer.color = Color.Lerp(baseColor, disengageTint, Mathf.Lerp(0.45f, 0.85f, normalized));

            chaseTintApplied = true;
            stunTintApplied = false;
        }

        private void StartDisengageCue()
        {
            float effectiveDuration = EffectiveDisengageCueSeconds;
            if (effectiveDuration <= 0.001f)
            {
                ClearDisengageCue();
                return;
            }

            disengageCueTimer = effectiveDuration;
            EnsureChaseMarker();
            ConfigureChaseMarkerVisual(ChaseMarkerMode.Disengage, 1f);
            UpdateChaseMarkerPulse();
            SetChaseMarkerVisible(true);
        }

        private void ClearDisengageCue()
        {
            disengageCueTimer = 0f;
            if (!chaseTransitionPending)
            {
                SetChaseMarkerVisible(false);
            }
        }

        private void RestoreBaseVisual()
        {
            bool needsRestore = stunTintApplied || chaseTintApplied;
            if (needsRestore && spriteRenderer != null)
            {
                spriteRenderer.color = baseColor;
            }

            stunTintApplied = false;
            chaseTintApplied = false;
        }

        private void BeginChaseTransition()
        {
            chaseTransitionPending = true;
            chaseTransitionTimer = 0f;
            chaseTransitionLostSightTimer = 0f;
            ClearDisengageCue();
            EnsureChaseMarker();
            ConfigureChaseMarkerVisual(ChaseMarkerMode.Alert, 1f);
            UpdateChaseMarkerPulse();
            SetChaseMarkerVisible(true);

            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            if (animator != null)
            {
                animator.SetTrigger(HitTriggerHash);
            }

            RaiseAgentRuntimeEvent(BuildLockOnWarningMessage(name), RuntimeEventSemantic.LockOnWarning);
        }

        private void CancelChaseTransition(string reason)
        {
            chaseTransitionPending = false;
            chaseTransitionTimer = 0f;
            chaseTransitionLostSightTimer = 0f;
            if (disengageCueTimer <= 0f)
            {
                SetChaseMarkerVisible(false);
            }

            if (!string.IsNullOrWhiteSpace(reason) && currentState != EnemyStateId.Chase)
            {
                lastDetectionReason = reason;
            }

            if (reason == "VisualLostBeforeCommit")
            {
                RaiseAgentRuntimeEvent(BuildLockOnCancelledMessage(name));
            }
        }

        private static string BuildLockOnWarningMessage(string agentName)
        {
            return $"{NormalizeAgentName(agentName)} 주시 시작";
        }

        private static string BuildLockOnCancelledMessage(string agentName)
        {
            return $"{NormalizeAgentName(agentName)} 주시 해제";
        }

        private static string NormalizeAgentName(string agentName)
        {
            return string.IsNullOrWhiteSpace(agentName) ? "위협" : agentName.Trim();
        }


        private void RaiseAgentRuntimeEvent(string message, RuntimeEventSemantic semantic = RuntimeEventSemantic.None)
        {
            if (!emitAgentRuntimeEvents || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            if (Time.time < nextRuntimeEventTime)
            {
                return;
            }

            if (player != null && runtimeEventMaxDistanceFromPlayer > 0f)
            {
                float distance = Vector2.Distance(transform.position, player.position);
                if (distance > runtimeEventMaxDistanceFromPlayer)
                {
                    return;
                }
            }

            nextRuntimeEventTime = Time.time + Mathf.Max(0f, runtimeEventCooldownSeconds);
            RuntimeEventBus.Raise(RuntimeEventType.System, message, this, semantic: semantic);
        }

        private void RegisterActiveController()
        {
            if (!activeControllers.Contains(this))
            {
                activeControllers.Add(this);
            }
        }

        private void UnregisterActiveController()
        {
            activeControllers.Remove(this);
        }

        private void EnsureSpriteRenderer()
        {
            if (spriteRenderer != null)
            {
                return;
            }

            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                baseColor = spriteRenderer.color;
            }
        }

        private void EnsureVisionConeVisual()
        {
            if (!showVisionConeVisual)
            {
                SetVisionConeVisible(false);
                return;
            }

            if (visionConeRoot == null)
            {
                Transform existing = transform.Find("VisionConeVisual");
                visionConeRoot = existing;
                if (visionConeRoot == null)
                {
                    GameObject coneObject = new("VisionConeVisual");
                    coneObject.transform.SetParent(transform, false);
                    visionConeRoot = coneObject.transform;
                }
            }

            visionConeRoot.localPosition = Vector3.zero;
            visionConeRoot.localRotation = Quaternion.identity;
            visionConeRoot.localScale = Vector3.one;

            visionConeMeshFilter = visionConeRoot.GetComponent<MeshFilter>();
            if (visionConeMeshFilter == null)
            {
                visionConeMeshFilter = visionConeRoot.gameObject.AddComponent<MeshFilter>();
            }

            visionConeRenderer = visionConeRoot.GetComponent<MeshRenderer>();
            if (visionConeRenderer == null)
            {
                visionConeRenderer = visionConeRoot.gameObject.AddComponent<MeshRenderer>();
            }

            if (visionConeMesh == null)
            {
                visionConeMesh = new Mesh
                {
                    name = "EnemyVisionConeMesh",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            visionConeMeshFilter.sharedMesh = visionConeMesh;

            if (visionConeMaterial == null)
            {
                visionConeMaterial = new Material(ResolveTransparentRuntimeShader())
                {
                    name = "EnemyVisionConeMaterial",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            visionConeRenderer.sharedMaterial = visionConeMaterial;
            visionConeRenderer.sortingOrder = visionConeSortingOrder;
            visionConeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            visionConeRenderer.receiveShadows = false;

            visionConeOutline = visionConeRoot.GetComponent<LineRenderer>();
            if (visionConeOutline == null)
            {
                visionConeOutline = visionConeRoot.gameObject.AddComponent<LineRenderer>();
            }

            if (visionConeOutlineMaterial == null)
            {
                visionConeOutlineMaterial = new Material(ResolveTransparentRuntimeShader())
                {
                    name = "EnemyVisionConeOutlineMaterial",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            visionConeOutline.sharedMaterial = visionConeOutlineMaterial;
            visionConeOutline.useWorldSpace = false;
            visionConeOutline.loop = false;
            visionConeOutline.widthMultiplier = Mathf.Max(0.005f, visionConeOutlineWidth);
            visionConeOutline.numCapVertices = 2;
            visionConeOutline.numCornerVertices = 2;
            visionConeOutline.sortingOrder = visionConeSortingOrder + 1;
        }

        private void UpdateVisionConeVisual(bool force = false)
        {
            if (!showVisionConeVisual)
            {
                SetVisionConeVisible(false);
                return;
            }

            EnsureVisionConeVisual();
            bool shouldShow = ShouldShowVisionConeVisual();
            SetVisionConeVisible(shouldShow);
            if (!shouldShow || visionConeMesh == null || visionConeOutline == null)
            {
                return;
            }

            if (!force && Time.time < nextVisionConeRefreshTime)
            {
                return;
            }

            nextVisionConeRefreshTime = Time.time + Mathf.Max(0.01f, visionConeRefreshInterval);

            int segments = Mathf.Clamp(visionConeSegments, 6, 48);
            Vector3[] vertices = new Vector3[segments + 2];
            int[] triangles = new int[segments * 3];
            Vector3[] outlinePositions = new Vector3[segments + 3];
            vertices[0] = Vector3.zero;
            outlinePositions[0] = Vector3.zero;

            float range = EvaluateEffectiveVisionRangeForVisual();
            float halfAngle = visionAngle * 0.5f;
            Vector2 origin = transform.position;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float angle = Mathf.Lerp(-halfAngle, halfAngle, t);
                Vector2 localDirection = Quaternion.Euler(0f, 0f, angle) * Vector2.right;
                Vector2 worldDirection = transform.TransformDirection(localDirection);
                float rayDistance = visionConeClipToWalls
                    ? ResolveVisionConeRayDistance(origin, worldDirection, range)
                    : range;
                Vector3 localPoint = new(localDirection.x * rayDistance, localDirection.y * rayDistance, 0f);
                vertices[i + 1] = localPoint;
                outlinePositions[i + 1] = localPoint;

                if (i < segments)
                {
                    int triangleIndex = i * 3;
                    triangles[triangleIndex] = 0;
                    triangles[triangleIndex + 1] = i + 1;
                    triangles[triangleIndex + 2] = i + 2;
                }
            }

            outlinePositions[segments + 2] = Vector3.zero;
            visionConeMesh.Clear();
            visionConeMesh.vertices = vertices;
            visionConeMesh.triangles = triangles;
            visionConeMesh.RecalculateBounds();

            Color coneColor = EvaluateVisionConeColor();
            if (visionConeMaterial != null)
            {
                visionConeMaterial.color = coneColor;
            }

            Color outlineColor = visionConeOutlineColor;
            outlineColor.a *= Mathf.Lerp(0.7f, 1.2f, Mathf.Clamp01(coneColor.a / Mathf.Max(0.001f, visionConeChaseColor.a)));
            if (visionConeOutlineMaterial != null)
            {
                visionConeOutlineMaterial.color = outlineColor;
            }

            visionConeOutline.positionCount = outlinePositions.Length;
            visionConeOutline.SetPositions(outlinePositions);
            visionConeOutline.widthMultiplier = Mathf.Max(0.005f, visionConeOutlineWidth);
        }

        private bool ShouldShowVisionConeVisual()
        {
            if (!showVisionConeVisual || currentState == EnemyStateId.Stunned)
            {
                return false;
            }

            if (visionConeVisibleWhenIdle)
            {
                return true;
            }

            if (currentState == EnemyStateId.Suspicion
                || currentState == EnemyStateId.Investigate
                || currentState == EnemyStateId.Chase
                || currentState == EnemyStateId.Search)
            {
                return true;
            }

            return player != null
                   && Vector2.Distance(transform.position, player.position) <= Mathf.Max(0.5f, visionConeVisibleDistance);
        }

        private float EvaluateEffectiveVisionRangeForVisual()
        {
            return Mathf.Max(
                0.5f,
                visionRange * runtimeVisionRangeMultiplier * Mathf.Max(0.1f, ActiveProfile.lightSensitivity));
        }

        private Color EvaluateVisionConeColor()
        {
            if (currentState == EnemyStateId.Chase)
            {
                return visionConeChaseColor;
            }

            if (currentState == EnemyStateId.Suspicion
                || currentState == EnemyStateId.Investigate
                || currentState == EnemyStateId.Search
                || chaseTransitionPending)
            {
                return Color.Lerp(visionConeAlertColor, visionConeChaseColor, ChaseTransitionProgress);
            }

            return visionConeIdleColor;
        }

        private float ResolveVisionConeRayDistance(Vector2 origin, Vector2 direction, float range)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return range;
            }

            direction.Normalize();
            float nearest = range;
            LayerMask mask = lineOfSightBlockers;
            if (mask.value != 0)
            {
                RaycastHit2D[] maskedHits = Physics2D.RaycastAll(origin, direction, range, mask);
                nearest = Mathf.Min(nearest, FindNearestOccludingRayDistance(maskedHits, range));
            }

            RaycastHit2D[] fallbackHits = Physics2D.RaycastAll(origin, direction, range);
            nearest = Mathf.Min(nearest, FindNearestOccludingRayDistance(fallbackHits, range));
            return Mathf.Clamp(nearest, 0.05f, range);
        }

        private float FindNearestOccludingRayDistance(RaycastHit2D[] hits, float fallbackDistance)
        {
            float nearest = fallbackDistance;
            if (hits == null)
            {
                return nearest;
            }

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit2D hit = hits[i];
                Collider2D hitCollider = hit.collider;
                if (!IsOccludingWallCollider(hitCollider))
                {
                    continue;
                }

                if (hit.distance <= 0.001f)
                {
                    continue;
                }

                nearest = Mathf.Min(nearest, hit.distance);
            }

            return nearest;
        }

        private void SetVisionConeVisible(bool visible)
        {
            if (visionConeRoot != null && visionConeRoot.gameObject.activeSelf != visible)
            {
                visionConeRoot.gameObject.SetActive(visible);
            }
        }

        private static Shader ResolveTransparentRuntimeShader()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                return shader;
            }

            shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null)
            {
                return shader;
            }

            shader = Shader.Find("Unlit/Transparent");
            if (shader != null)
            {
                return shader;
            }

            return Shader.Find("Unlit/Color");
        }

        private void ReleaseVisionConeResources()
        {
            ReleaseRuntimeObject(visionConeMesh);
            ReleaseRuntimeObject(visionConeMaterial);
            ReleaseRuntimeObject(visionConeOutlineMaterial);
            visionConeMesh = null;
            visionConeMaterial = null;
            visionConeOutlineMaterial = null;
        }

        private static void ReleaseRuntimeObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private void EnsureMovementEchoVisual()
        {
            movementEchoVisual = GetComponent<EnemyMovementEchoVisual>();
            if (!showMovementEchoVisual)
            {
                if (movementEchoVisual != null)
                {
                    movementEchoVisual.SetEnabledForRuntime(false);
                }

                return;
            }

            if (movementEchoVisual == null)
            {
                movementEchoVisual = gameObject.AddComponent<EnemyMovementEchoVisual>();
            }

            movementEchoVisual.Configure(
                movementEchoInterval,
                movementEchoMinSpeed,
                movementEchoColor,
                movementEchoDuration,
                movementEchoArcCount,
                movementEchoArcAngle,
                movementEchoBaseRadius,
                movementEchoRadiusStep,
                movementEchoThickness,
                movementEchoSortingOrder);
            movementEchoVisual.ConfigureFogVisibility(
                movementEchoOnlyWhenHiddenByFog,
                movementEchoFogHiddenThreshold,
                movementEchoFogHiddenHysteresis,
                movementEchoFogHiddenGraceSeconds,
                movementEchoClampFogThresholdToRuntimeFogRange,
                movementEchoShowWhenFogSystemUnavailable,
                movementEchoFogLookupInterval,
                movementEchoFogVisibilityEvaluationInterval,
                movementEchoFogVisibilityCacheDistance);
            movementEchoVisual.ConfigureFogSampling(
                movementEchoSampleFogFromBodyBounds,
                movementEchoBodyBoundsSampleInset,
                movementEchoRequireAllBodySamplesHidden);
            movementEchoVisual.ConfigureVisibilityCleanup(
                movementEchoClearActivePulsesWhenVisible);
            movementEchoVisual.EnsurePulsePoolPrewarmed(
                movementEchoPrewarmPool,
                movementEchoPrewarmPoolTargetCount);
            movementEchoVisual.SetOwnerController(this);
            movementEchoVisual.SetEnabledForRuntime(true);
        }

        private void EnsureChaseMarker()
        {
            if (chaseMarker != null)
            {
                return;
            }

            Transform existing = transform.Find("ChaseTransitionMarker");
            chaseMarker = existing;
            if (chaseMarker == null)
            {
                GameObject markerObject = new("ChaseTransitionMarker");
                markerObject.transform.SetParent(transform, false);
                chaseMarker = markerObject.transform;
            }

            chaseMarker.localPosition = new Vector3(0f, transitionMarkerHeight, 0f);
            chaseMarker.localRotation = Quaternion.identity;

            chaseMarkerText = chaseMarker.GetComponent<TextMesh>();
            if (chaseMarkerText == null)
            {
                chaseMarkerText = chaseMarker.gameObject.AddComponent<TextMesh>();
            }

            chaseMarkerText.characterSize = 0.16f;
            chaseMarkerText.fontSize = 64;
            chaseMarkerText.anchor = TextAnchor.MiddleCenter;
            chaseMarkerText.alignment = TextAlignment.Center;
            chaseMarkerMode = ChaseMarkerMode.Alert;
            ConfigureChaseMarkerVisual(chaseMarkerMode, 1f);

            MeshRenderer markerRenderer = chaseMarker.GetComponent<MeshRenderer>();
            if (markerRenderer != null)
            {
                markerRenderer.sortingOrder = 42;
            }
        }

        private void ConfigureChaseMarkerVisual(ChaseMarkerMode mode, float alpha01)
        {
            chaseMarkerMode = mode;
            if (chaseMarkerText == null)
            {
                return;
            }

            float alpha = Mathf.Clamp01(alpha01);
            if (mode == ChaseMarkerMode.Disengage)
            {
                chaseMarkerText.text = "?";
                Color color = disengageMarkerColor;
                color.a *= alpha;
                chaseMarkerText.color = color;
                return;
            }

            chaseMarkerText.text = "!";
            Color alertColor = chaseAlertMarkerColor;
            alertColor.a *= alpha;
            chaseMarkerText.color = alertColor;
        }

        private float EvaluateCurrentMarkerPulseSpeed()
        {
            if (chaseMarkerMode == ChaseMarkerMode.Disengage)
            {
                return Mathf.Max(0.5f, disengageCuePulseSpeed * Mathf.Lerp(0.9f, 1.2f, runtimeTransitionPulseSpeedMultiplier));
            }

            return EffectiveTransitionPulseSpeed;
        }

        private void SetChaseMarkerVisible(bool visible)
        {
            if (!showChaseTransitionMarker)
            {
                visible = false;
            }

            if (chaseMarker == null)
            {
                if (!visible)
                {
                    return;
                }

                EnsureChaseMarker();
            }

            chaseMarker.localPosition = new Vector3(0f, transitionMarkerHeight, 0f);
            if (chaseMarker.gameObject.activeSelf != visible)
            {
                chaseMarker.gameObject.SetActive(visible);
            }
        }

        private void UpdateChaseMarkerPulse()
        {
            if (chaseMarker == null || !chaseMarker.gameObject.activeSelf)
            {
                return;
            }

            chaseMarker.rotation = Quaternion.identity;
            float pulse = 1f + 0.15f * Mathf.Sin(Time.time * EvaluateCurrentMarkerPulseSpeed());
            chaseMarker.localScale = Vector3.one * pulse;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, visionRange);

            Vector3 right = transform.right;
            Quaternion leftRot = Quaternion.Euler(0f, 0f, visionAngle * 0.5f);
            Quaternion rightRot = Quaternion.Euler(0f, 0f, -visionAngle * 0.5f);
            Gizmos.DrawLine(transform.position, transform.position + leftRot * right * visionRange);
            Gizmos.DrawLine(transform.position, transform.position + rightRot * right * visionRange);

            if (hasCurrentTarget)
            {
                Gizmos.color = new Color(1f, 0.2f, 0.4f);
                Gizmos.DrawWireSphere(currentTargetPoint, 0.18f);
                Gizmos.DrawLine(transform.position, currentTargetPoint);
            }

            if (predictedEscapeDirection.sqrMagnitude > 0.01f)
            {
                Gizmos.color = new Color(0.2f, 0.9f, 1f);
                Gizmos.DrawLine(transform.position, transform.position + (Vector3)predictedEscapeDirection * 1.5f);
            }

            if (currentState == EnemyStateId.Stunned)
            {
                Gizmos.color = new Color(0.55f, 0.9f, 1f, 0.85f);
                Gizmos.DrawWireSphere(transform.position, 0.45f);
            }
        }
    }

    internal sealed class EnemyProfileFallback : EnemyProfile
    {
        private static EnemyProfileFallback instance;

        public static EnemyProfileFallback Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = CreateInstance<EnemyProfileFallback>();
                }

                return instance;
            }
        }
    }
}






















