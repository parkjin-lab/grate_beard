using System.Collections;
using LostBreadcrumbs.Runtime.Core.Input;
using LostBreadcrumbs.Runtime.Events;
using LostBreadcrumbs.Runtime.Managers;
using LostBreadcrumbs.Runtime.Map;
using LostBreadcrumbs.Runtime.Systems;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class PlayerDummyController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float moveSpeed = 2.42f;
        [SerializeField] private KeyCode moveLeftKey = KeyCode.A;
        [SerializeField] private KeyCode moveRightKey = KeyCode.D;
        [SerializeField] private KeyCode moveDownKey = KeyCode.S;
        [SerializeField] private KeyCode moveUpKey = KeyCode.W;

        [Header("Sprint")]
        [SerializeField] private bool enableSprint = true;
        [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;
        [SerializeField] private KeyCode sprintAltKey = KeyCode.RightShift;
        [SerializeField, Min(1f)] private float sprintMoveMultiplier = 1.34f;
        [SerializeField, Min(0.1f)] private float maxStamina = 4f;
        [SerializeField, Min(0f)] private float staminaDrainPerSecond = 1.2f;
        [SerializeField, Min(0f)] private float staminaRecoverPerSecond = 0.95f;
        [SerializeField, Min(0f)] private float staminaRecoverDelay = 0.45f;
        [SerializeField, Min(0f)] private float exhaustedLockSeconds = 0.65f;

        [Header("Noise")]
        [SerializeField, Min(0.05f)] private float footstepInterval = 0.54f;
        [SerializeField, Min(0.1f)] private float footstepLoudness = 0.45f;
        [SerializeField, Min(0.1f)] private float echoLoudness = 2.1f;
        [SerializeField, Min(0.1f)] private float echoRadius = 7.8f;
        [SerializeField, Min(0.05f)] private float sprintFootstepIntervalScale = 0.7f;
        [SerializeField, Min(1f)] private float sprintNoiseMultiplier = 1.55f;

        [Header("Temporary Noise Dampening")]
        [SerializeField, Min(1f)] private float temporaryNoiseSprintDecayMultiplier = 2.35f;

        [Header("Echo Visual")]
        [SerializeField] private bool spawnEchoVisual = true;
        [SerializeField] private Color echoVisualColor = new(0.32f, 0.72f, 1f, 0.68f);
        [SerializeField, Min(0.1f)] private float echoVisualDuration = 3.05f;
        [SerializeField, Range(1, 4)] private int echoVisualRingCount = 3;
        [SerializeField, Min(0f)] private float echoVisualRingInterval = 0.56f;
        [SerializeField, Min(0.5f)] private float echoVisualRadiusMultiplier = 1f;
        [SerializeField] private int echoVisualSortingOrder = 34;

        [Header("Echo Fog Trace")]
        [SerializeField] private bool revealFogWithEcho = true;
        [SerializeField, Min(0.1f)] private float echoFogRevealRadiusMultiplier = 0.72f;
        [SerializeField, Min(0f)] private float echoFogRevealSoftnessBoost = 0.8f;

        [Header("Echo Objective Scan")]
        [SerializeField] private bool enableEchoObjectiveScan = true;
        [SerializeField, Min(0.2f)] private float echoObjectiveScanMaxDistance = 10.5f;
        [SerializeField, Min(0.1f)] private float echoObjectiveScanDuration = 2.45f;
        [SerializeField, Min(0.01f)] private float echoObjectiveScanWidth = 0.045f;
        [SerializeField, Min(0f)] private float echoObjectiveScanWaver = 0.24f;
        [SerializeField] private Color echoObjectiveBreadcrumbColor = new(1f, 0.82f, 0.28f, 0.48f);
        [SerializeField] private Color echoObjectiveExitColor = new(0.36f, 1f, 0.58f, 0.52f);
        [SerializeField] private bool showEchoObjectiveChoiceScans = true;
        [SerializeField] private Color echoObjectiveRiskCacheColor = new(1f, 0.32f, 0.22f, 0.52f);
        [SerializeField] private Color echoObjectiveExitCacheColor = new(1f, 0.72f, 0.22f, 0.54f);
        [SerializeField, Range(0.2f, 1f)] private float echoObjectiveChoiceMaxDistanceMultiplier = 0.72f;
        [SerializeField, Range(0.2f, 1f)] private float echoObjectiveChoiceWidthScale = 0.68f;
        [SerializeField, Range(0.2f, 1f)] private float echoObjectiveChoiceDurationScale = 0.76f;
        [SerializeField, Range(0.2f, 1f)] private float echoObjectiveChoiceAlphaScale = 0.78f;
        [SerializeField, Min(0f)] private float echoObjectiveChoiceMinSeparation = 1.15f;
        [SerializeField, Min(0.2f)] private float echoObjectiveScanStatusSeconds = 3.2f;
        [SerializeField] private int echoObjectiveScanSortingOrder = 36;

        [Header("Input")]
        [SerializeField] private KeyCode echoKey = KeyCode.Space;
        [SerializeField] private KeyCode flashlightKey = KeyCode.F;
        [SerializeField] private bool freezeInputDuringRegressionChecklist = true;

        [Header("Collision")]
        [SerializeField] private bool autoConfigurePhysicsBody = true;
        [SerializeField, Min(0.05f)] private float collisionRadius = 0.32f;
        [SerializeField] private Vector2 collisionOffset = Vector2.zero;
        [SerializeField, Min(0f)] private float collisionCastPadding = 0.02f;

        [Header("Spawn Safety")]
        [SerializeField] private bool autoRecoverUnsafePosition = true;
        [SerializeField, Min(0f)] private float unsafePositionRecoveryStartDelay = 0.05f;
        [SerializeField, Min(0.05f)] private float unsafePositionRecoveryInterval = 0.18f;
        [SerializeField, Min(0.1f)] private float unsafePositionRecoveryWindow = 2f;

        private float nextFootstepTime;
        private Vector2 moveInput;
        private Vector2 facingDirection = Vector2.right;
        private float facingSignX = 1f;

        private float currentStamina;
        private float recoverDelayUntil;
        private float exhaustedUntil;
        private float currentMoveSpeed;
        private bool isSprinting;
        private float runtimeMoveSpeedMultiplier = 1f;
        private float runtimeStaminaCapacityMultiplier = 1f;
        private float runtimeStaminaRecoveryMultiplier = 1f;
        private float runtimeFootstepNoiseMultiplier = 1f;
        private float runtimeSprintNoiseMultiplier = 1f;
        private float temporaryFootstepNoiseMultiplier = 1f;
        private float temporarySprintNoiseMultiplier = 1f;
        private float temporaryNoiseDampeningUntil;

        private PlayerVisibilitySource visibilitySource;
        private PlayerConcealmentState concealmentState;
        private PlayerBehaviorTelemetry behaviorTelemetry;
        private FogOfWarSystem fogSystem;
        private MapSystem mapSystem;
        private Rigidbody2D rb;
        private ContactFilter2D movementContactFilter;
        private int cachedMovementLayerMask = int.MinValue;
        private Material echoObjectiveScanMaterial;
        private readonly RaycastHit2D[] movementCastHits = new RaycastHit2D[8];
        private float unsafePositionRecoveryUntil;
        private float nextUnsafePositionRecoveryCheck;
        private int unsafePositionRecoveryCount;

        public static PlayerDummyController ActiveInstance { get; private set; }
        public Vector2 MoveInput => moveInput;
        public Vector2 FacingDirection => facingDirection.sqrMagnitude > 0.0001f ? facingDirection.normalized : Vector2.right;
        public float FacingSignX => facingSignX;
        public bool IsSprinting => isSprinting;
        public bool IsExhausted => Time.time < exhaustedUntil;
        public float CurrentStamina => currentStamina;
        public float MaxStamina => maxStamina * runtimeStaminaCapacityMultiplier;
        public float StaminaNormalized => MaxStamina > 0f ? Mathf.Clamp01(currentStamina / MaxStamina) : 0f;
        public float CurrentMoveSpeed => currentMoveSpeed;
        public float RuntimeMoveSpeedMultiplier => runtimeMoveSpeedMultiplier;
        public float RuntimeFootstepNoiseMultiplier => runtimeFootstepNoiseMultiplier;
        public float RuntimeSprintNoiseMultiplier => runtimeSprintNoiseMultiplier;
        public float RuntimeStaminaCapacityMultiplier => runtimeStaminaCapacityMultiplier;
        public float RuntimeStaminaRecoveryMultiplier => runtimeStaminaRecoveryMultiplier;
        public float TemporaryNoiseDampeningRemaining => Mathf.Max(0f, temporaryNoiseDampeningUntil - Time.time);
        public float TemporaryNoiseSprintDecayMultiplier => temporaryNoiseSprintDecayMultiplier;
        public bool IsTemporaryNoiseDampeningStrained => TemporaryNoiseDampeningRemaining > 0.05f && isSprinting;
        public float EffectiveFootstepNoiseMultiplier => runtimeFootstepNoiseMultiplier * EvaluateTemporaryFootstepNoiseMultiplier();
        public float EffectiveSprintNoiseMultiplier => runtimeSprintNoiseMultiplier * EvaluateTemporarySprintNoiseMultiplier();
        public int LastEchoObjectiveScanCount { get; private set; }
        public int LastEchoObjectiveChoiceScanCount { get; private set; }
        public bool LastEchoObjectivePrimaryWasExit { get; private set; }
        public float LastEchoObjectiveScanTime { get; private set; } = -999f;
        public float EchoObjectiveScanStatusRemaining => Mathf.Max(0f, LastEchoObjectiveScanTime + Mathf.Max(0.2f, echoObjectiveScanStatusSeconds) - Time.time);
        public int UnsafePositionRecoveryCount => unsafePositionRecoveryCount;
        public float UnsafePositionRecoveryWindowRemaining => Mathf.Max(0f, unsafePositionRecoveryUntil - Time.time);

        private void Awake()
        {
            visibilitySource = GetComponent<PlayerVisibilitySource>();
            concealmentState = GetComponent<PlayerConcealmentState>();
            behaviorTelemetry = GetComponent<PlayerBehaviorTelemetry>();
            fogSystem = FindFirstObjectByType<FogOfWarSystem>();
            mapSystem = FindFirstObjectByType<MapSystem>();

            EnsurePlayerTag();

            EnsurePhysicsComponents();
            ActiveInstance = this;
            transform.rotation = Quaternion.identity;

            maxStamina = Mathf.Max(0.1f, maxStamina);
            currentStamina = MaxStamina;
            currentMoveSpeed = moveSpeed;
        }

        private void OnEnable()
        {
            ActiveInstance = this;
            transform.rotation = Quaternion.identity;
            if (rb != null)
            {
                rb.rotation = 0f;
                rb.angularVelocity = 0f;
            }

            ScheduleUnsafePositionRecoveryProbe();
        }

        private void OnDisable()
        {
            if (ActiveInstance == this)
            {
                ActiveInstance = null;
            }
        }

        private void Update()
        {
            TickUnsafePositionRecovery();

            if (freezeInputDuringRegressionChecklist && RegressionChecklistRunner.IsRegressionRunActive)
            {
                moveInput = Vector2.zero;
                UpdateSprintState(false);
                return;
            }

            moveInput = RuntimeInputAdapter.GetMoveVector(moveLeftKey, moveRightKey, moveDownKey, moveUpKey);
            bool hasMoveInput = moveInput.sqrMagnitude > 0.01f;

            UpdateSprintState(hasMoveInput);

            if (hasMoveInput)
            {
                facingDirection = moveInput;
                if (Mathf.Abs(moveInput.x) > 0.001f)
                {
                    facingSignX = moveInput.x < 0f ? -1f : 1f;
                }

                if (transform.eulerAngles.z != 0f)
                {
                    transform.rotation = Quaternion.identity;
                    if (rb != null)
                    {
                        rb.rotation = 0f;
                        rb.angularVelocity = 0f;
                    }
                }

                float stepInterval = footstepInterval * (isSprinting ? sprintFootstepIntervalScale : 1f);
                if (Time.time >= nextFootstepTime)
                {
                    float temporaryFootstepScale = EvaluateTemporaryFootstepNoiseMultiplier();
                    float temporarySprintScale = EvaluateTemporarySprintNoiseMultiplier();
                    float movementNoiseScale = isSprinting ? sprintNoiseMultiplier * runtimeSprintNoiseMultiplier * temporarySprintScale : 1f;
                    EmitNoise(footstepLoudness * runtimeFootstepNoiseMultiplier * temporaryFootstepScale, footstepLoudness * 3f, NoiseKind.Footstep, movementNoiseScale);
                    nextFootstepTime = Time.time + Mathf.Max(0.05f, stepInterval);
                }
            }

            if (RuntimeInputAdapter.GetKeyDown(echoKey))
            {
                EmitNoise(echoLoudness, echoRadius, NoiseKind.Echo);
                SpawnEchoVisual(echoRadius);
                ApplyEchoFogTrace(echoRadius);
                TrySpawnEchoObjectiveScan();
                behaviorTelemetry?.RegisterEcho();
            }

            if (RuntimeInputAdapter.GetKeyDown(flashlightKey))
            {
                RefreshVisibilityReference();
                if (visibilitySource != null)
                {
                    visibilitySource.ToggleFlashlight();
                }

                behaviorTelemetry?.RegisterFlashlightToggle();
                EmitNoise(0.25f, 2f, NoiseKind.FlashlightToggle);
            }
        }

        public void RefreshRuntimeReferencesForRespawn()
        {
            RefreshVisibilityReference();

            if (concealmentState == null)
            {
                concealmentState = GetComponent<PlayerConcealmentState>();
            }

            if (behaviorTelemetry == null)
            {
                behaviorTelemetry = GetComponent<PlayerBehaviorTelemetry>();
            }

            if (fogSystem == null)
            {
                fogSystem = FindFirstObjectByType<FogOfWarSystem>();
            }

            if (mapSystem == null)
            {
                mapSystem = FindFirstObjectByType<MapSystem>();
            }

            EnsurePhysicsComponents();
            ScheduleUnsafePositionRecoveryProbe();
        }

        private void RefreshVisibilityReference()
        {
            if (visibilitySource == null)
            {
                visibilitySource = GetComponent<PlayerVisibilitySource>();
            }
        }

        public void ScheduleUnsafePositionRecoveryProbe()
        {
            if (!autoRecoverUnsafePosition)
            {
                return;
            }

            float startDelay = Mathf.Max(0f, unsafePositionRecoveryStartDelay);
            float window = Mathf.Max(0.1f, unsafePositionRecoveryWindow);
            unsafePositionRecoveryUntil = Time.time + startDelay + window;
            nextUnsafePositionRecoveryCheck = Time.time + startDelay;
        }

        private void TickUnsafePositionRecovery()
        {
            if (!autoRecoverUnsafePosition || Time.time > unsafePositionRecoveryUntil)
            {
                return;
            }

            if (Time.time < nextUnsafePositionRecoveryCheck)
            {
                return;
            }

            nextUnsafePositionRecoveryCheck = Time.time + Mathf.Max(0.05f, unsafePositionRecoveryInterval);

            TryRecoverUnsafePositionNow(clearRecoveryWindowWhenStable: true);
        }

        public bool TryRecoverUnsafePositionNowForRuntime()
        {
            if (!autoRecoverUnsafePosition)
            {
                return false;
            }

            return TryRecoverUnsafePositionNow(clearRecoveryWindowWhenStable: false);
        }

        private bool TryRecoverUnsafePositionNow(bool clearRecoveryWindowWhenStable)
        {
            if (mapSystem == null)
            {
                mapSystem = FindFirstObjectByType<MapSystem>();
                if (mapSystem == null)
                {
                    return false;
                }
            }

            Vector3 current = transform.position;
            if (!mapSystem.TryResolveSafePlayerPosition(current, transform, out Vector3 safePosition))
            {
                return false;
            }

            safePosition.z = current.z;
            if (((Vector2)safePosition - (Vector2)current).sqrMagnitude <= 0.0001f)
            {
                if (clearRecoveryWindowWhenStable && !mapSystem.LastPlayerSpawnUsedBlockedFallback)
                {
                    unsafePositionRecoveryUntil = 0f;
                }

                return false;
            }

            TeleportToSafePosition(safePosition);
            unsafePositionRecoveryCount++;
            unsafePositionRecoveryUntil = 0f;
            return true;
        }

        private void TeleportToSafePosition(Vector3 safePosition)
        {
            if (rb != null)
            {
                rb.position = safePosition;
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            transform.position = safePosition;
        }

        private void EnsurePhysicsComponents()
        {
            rb = GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody2D>();
            }

            CircleCollider2D bodyCollider = GetComponent<CircleCollider2D>();
            if (bodyCollider == null)
            {
                bodyCollider = gameObject.AddComponent<CircleCollider2D>();
            }

            if (!autoConfigurePhysicsBody)
            {
                return;
            }

            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.simulated = true;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            bodyCollider.isTrigger = false;
            bodyCollider.radius = Mathf.Max(0.05f, collisionRadius);

            int defaultLayer = LayerMask.NameToLayer("Default");
            if (defaultLayer >= 0)
            {
                gameObject.layer = defaultLayer;
            }
            bodyCollider.offset = collisionOffset;
            RefreshMovementContactFilter(forceRefresh: true);
        }

        private void FixedUpdate()
        {
            if (freezeInputDuringRegressionChecklist && RegressionChecklistRunner.IsRegressionRunActive)
            {
                return;
            }

            Vector2 step = moveInput * currentMoveSpeed * Time.fixedDeltaTime;
            if (rb != null)
            {
                MoveWithCollision(step);
            }
            else
            {
                transform.position += (Vector3)step;
            }
        }

        public float RecoverStamina(float amount)
        {
            if (amount <= 0f)
            {
                return 0f;
            }

            float before = currentStamina;
            currentStamina = Mathf.Min(MaxStamina, currentStamina + amount);
            float recovered = currentStamina - before;

            if (recovered > 0f)
            {
                exhaustedUntil = Mathf.Min(exhaustedUntil, Time.time);
                recoverDelayUntil = Time.time + 0.05f;
            }

            return recovered;
        }

        public void ApplyTemporaryNoiseDampeningForRuntime(
            float footstepNoiseMultiplier,
            float sprintNoiseMultiplier,
            float durationSeconds)
        {
            if (durationSeconds <= 0f)
            {
                return;
            }

            float safeFootstep = Mathf.Clamp(footstepNoiseMultiplier, 0.2f, 1f);
            float safeSprint = Mathf.Clamp(sprintNoiseMultiplier, 0.2f, 1f);
            if (Time.time < temporaryNoiseDampeningUntil)
            {
                temporaryFootstepNoiseMultiplier = Mathf.Min(temporaryFootstepNoiseMultiplier, safeFootstep);
                temporarySprintNoiseMultiplier = Mathf.Min(temporarySprintNoiseMultiplier, safeSprint);
            }
            else
            {
                temporaryFootstepNoiseMultiplier = safeFootstep;
                temporarySprintNoiseMultiplier = safeSprint;
            }

            temporaryNoiseDampeningUntil = Mathf.Max(temporaryNoiseDampeningUntil, Time.time + durationSeconds);
        }

        public void ForceResetSprintState(bool refillStamina = true)
        {
            isSprinting = false;
            recoverDelayUntil = Time.time;
            exhaustedUntil = 0f;
            ClearTemporaryNoiseDampeningForRuntime();

            if (refillStamina)
            {
                currentStamina = MaxStamina;
            }

            currentMoveSpeed = moveSpeed * runtimeMoveSpeedMultiplier;
        }

        public void ApplySavedStaminaNormalized(float normalized)
        {
            float clamped = Mathf.Clamp01(normalized);
            currentStamina = MaxStamina * clamped;
            isSprinting = false;
            exhaustedUntil = 0f;
            recoverDelayUntil = Time.time;
            currentMoveSpeed = moveSpeed * runtimeMoveSpeedMultiplier;
            ScheduleUnsafePositionRecoveryProbe();
        }

        public void ApplyRuntimeModifiers(
            float moveSpeedMultiplier,
            float staminaCapacityMultiplier,
            float staminaRecoveryMultiplier,
            float footstepNoiseMultiplier,
            float sprintNoiseMultiplier,
            bool preserveStaminaRatio = true)
        {
            float previousMaxStamina = Mathf.Max(0.01f, MaxStamina);
            float previousRatio = preserveStaminaRatio ? Mathf.Clamp01(currentStamina / previousMaxStamina) : -1f;

            runtimeMoveSpeedMultiplier = Mathf.Clamp(moveSpeedMultiplier, 0.4f, 2.2f);
            runtimeStaminaCapacityMultiplier = Mathf.Clamp(staminaCapacityMultiplier, 0.4f, 2.4f);
            runtimeStaminaRecoveryMultiplier = Mathf.Clamp(staminaRecoveryMultiplier, 0.3f, 2.6f);
            runtimeFootstepNoiseMultiplier = Mathf.Clamp(footstepNoiseMultiplier, 0.2f, 2.5f);
            runtimeSprintNoiseMultiplier = Mathf.Clamp(sprintNoiseMultiplier, 0.2f, 2.5f);

            float newMaxStamina = Mathf.Max(0.01f, MaxStamina);
            if (preserveStaminaRatio)
            {
                currentStamina = newMaxStamina * Mathf.Clamp01(previousRatio);
            }
            else
            {
                currentStamina = Mathf.Clamp(currentStamina, 0f, newMaxStamina);
            }

            currentMoveSpeed = moveSpeed * (isSprinting ? sprintMoveMultiplier : 1f) * runtimeMoveSpeedMultiplier;
        }

        public void ResetRuntimeModifiers(bool preserveStaminaRatio = true)
        {
            ApplyRuntimeModifiers(1f, 1f, 1f, 1f, 1f, preserveStaminaRatio);
        }

        public void ClearTemporaryNoiseDampeningForRuntime()
        {
            temporaryFootstepNoiseMultiplier = 1f;
            temporarySprintNoiseMultiplier = 1f;
            temporaryNoiseDampeningUntil = 0f;
        }

        private float EvaluateTemporaryFootstepNoiseMultiplier()
        {
            if (Time.time >= temporaryNoiseDampeningUntil)
            {
                return 1f;
            }

            return Mathf.Clamp(temporaryFootstepNoiseMultiplier, 0.2f, 1f);
        }

        private float EvaluateTemporarySprintNoiseMultiplier()
        {
            if (Time.time >= temporaryNoiseDampeningUntil)
            {
                return 1f;
            }

            return Mathf.Clamp(temporarySprintNoiseMultiplier, 0.2f, 1f);
        }

        private void UpdateSprintState(bool hasMoveInput)
        {
            bool sprintHeld = RuntimeInputAdapter.GetKey(sprintKey) || RuntimeInputAdapter.GetKey(sprintAltKey);
            bool canSprint = enableSprint && hasMoveInput && sprintHeld && !IsExhausted && currentStamina > 0.01f;

            if (canSprint)
            {
                isSprinting = true;
                currentStamina = Mathf.Max(0f, currentStamina - staminaDrainPerSecond * Time.deltaTime);
                recoverDelayUntil = Time.time + staminaRecoverDelay;

                if (currentStamina <= 0.001f)
                {
                    isSprinting = false;
                    exhaustedUntil = Time.time + exhaustedLockSeconds;
                }
            }
            else
            {
                isSprinting = false;
                if (Time.time >= recoverDelayUntil)
                {
                    float recoverPerSecond = staminaRecoverPerSecond * runtimeStaminaRecoveryMultiplier;
                    currentStamina = Mathf.Min(MaxStamina, currentStamina + recoverPerSecond * Time.deltaTime);
                }
            }

            currentMoveSpeed = moveSpeed * (isSprinting ? sprintMoveMultiplier : 1f) * runtimeMoveSpeedMultiplier;
            behaviorTelemetry?.RegisterSprintTick(Time.deltaTime, isSprinting);
            TickTemporaryNoiseDampening(Time.deltaTime);
        }

        private void TickTemporaryNoiseDampening(float deltaTime)
        {
            if (temporaryNoiseDampeningUntil <= 0f || Time.time >= temporaryNoiseDampeningUntil || !isSprinting)
            {
                return;
            }

            float extraDecayMultiplier = Mathf.Max(0f, temporaryNoiseSprintDecayMultiplier - 1f);
            if (extraDecayMultiplier <= 0f)
            {
                return;
            }

            temporaryNoiseDampeningUntil = Mathf.Max(Time.time, temporaryNoiseDampeningUntil - deltaTime * extraDecayMultiplier);
            if (Time.time >= temporaryNoiseDampeningUntil)
            {
                ClearTemporaryNoiseDampeningForRuntime();
            }
        }

        private void MoveWithCollision(Vector2 step)
        {
            float distance = step.magnitude;
            if (distance <= 0.0001f)
            {
                return;
            }

            RefreshMovementContactFilter(forceRefresh: false);

            Vector2 direction = step / distance;
            int hitCount = rb.Cast(direction, movementContactFilter, movementCastHits, distance + collisionCastPadding);
            float allowedDistance = distance;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = movementCastHits[i];
                if (hit.collider == null || hit.collider.isTrigger)
                {
                    continue;
                }

                float clearance = Mathf.Max(0f, hit.distance - collisionCastPadding);
                allowedDistance = Mathf.Min(allowedDistance, clearance);
            }

            if (allowedDistance <= 0.00001f)
            {
                return;
            }

            rb.MovePosition(rb.position + direction * allowedDistance);
        }

        private void RefreshMovementContactFilter(bool forceRefresh)
        {
            int layerMask = Physics2D.GetLayerCollisionMask(gameObject.layer);
            if (!forceRefresh && cachedMovementLayerMask == layerMask)
            {
                return;
            }

            cachedMovementLayerMask = layerMask;
            movementContactFilter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = layerMask,
                useTriggers = false
            };
        }
        private void EnsurePlayerTag()
        {
            if (gameObject.tag == "Player")
            {
                return;
            }

            try
            {
                gameObject.tag = "Player";
            }
            catch (UnityException)
            {
                // Built-in tag may be unavailable in a broken project state.
            }
        }

        private void EmitNoise(float loudness, float radius, NoiseKind kind, float extraScale = 1f)
        {
            if (NoiseManager.Instance == null)
            {
                return;
            }

            float concealNoiseScale = concealmentState != null ? concealmentState.CurrentNoiseMultiplier : 1f;
            float smokeNoiseScale = SmokeScreenFieldDummy.EvaluateNoiseMultiplierAt(transform.position);
            float noiseScale = concealNoiseScale * smokeNoiseScale * Mathf.Max(0.1f, extraScale);

            float scaledLoudness = loudness * noiseScale;
            float radiusScale = noiseScale >= 1f
                ? Mathf.Lerp(1f, 1.2f, Mathf.Clamp01(noiseScale - 1f))
                : Mathf.Lerp(0.68f, 1f, noiseScale);
            float scaledRadius = radius * radiusScale;

            NoiseManager.Instance.EmitNoise(
                transform.position,
                Mathf.Max(0.05f, scaledLoudness),
                Mathf.Max(0.1f, scaledRadius),
                kind,
                gameObject);
        }

        private void SpawnEchoVisual(float radius)
        {
            if (!spawnEchoVisual)
            {
                return;
            }

            GameObject visualObject = new($"ManualEchoWave_{Time.frameCount}");
            Transform vfxRoot = EnsureScenePath("Scene_Root/GameRoot/Runtime/VFX");
            if (vfxRoot != null)
            {
                visualObject.transform.SetParent(vfxRoot, false);
            }

            visualObject.transform.position = new Vector3(transform.position.x, transform.position.y, 0f);
            EchoPulseVisualDummy visual = visualObject.AddComponent<EchoPulseVisualDummy>();
            visual.Configure(
                Mathf.Max(0.45f, radius * echoVisualRadiusMultiplier),
                echoVisualColor,
                echoVisualDuration,
                echoVisualRingCount,
                echoVisualRingInterval,
                echoVisualSortingOrder);
        }

        private void ApplyEchoFogTrace(float radius)
        {
            if (!revealFogWithEcho)
            {
                return;
            }

            if (fogSystem == null)
            {
                fogSystem = FindFirstObjectByType<FogOfWarSystem>();
            }

            if (fogSystem == null)
            {
                return;
            }

            float revealRadius = Mathf.Max(0.1f, radius * echoFogRevealRadiusMultiplier);
            fogSystem.ApplyEchoRevealPulse(transform.position, revealRadius, echoFogRevealSoftnessBoost);
        }

        public bool TryResolveEchoObjectiveScanTargetsForRuntime(out int totalScanCount, out int choiceScanCount, out bool primaryIsExit)
        {
            totalScanCount = 0;
            choiceScanCount = 0;
            primaryIsExit = false;

            if (!enableEchoObjectiveScan)
            {
                return false;
            }

            StageLoopDirector stageLoop = StageLoopDirector.Instance;
            if (stageLoop == null)
            {
                return false;
            }

            Vector2 origin = transform.position;
            if (!stageLoop.TryGetNextObjectiveTarget(origin, out Vector3 primaryTarget, out primaryIsExit)
                || !IsEchoObjectiveScanTargetViable(origin, primaryTarget))
            {
                return false;
            }

            totalScanCount = 1;
            choiceScanCount = CountEchoObjectiveChoiceTargets(stageLoop, origin, primaryTarget);
            totalScanCount += choiceScanCount;
            return true;
        }

        private bool TrySpawnEchoObjectiveScan()
        {
            LastEchoObjectiveScanCount = 0;
            LastEchoObjectiveChoiceScanCount = 0;
            LastEchoObjectivePrimaryWasExit = false;

            if (!enableEchoObjectiveScan)
            {
                return false;
            }

            StageLoopDirector stageLoop = StageLoopDirector.Instance;
            if (stageLoop == null)
            {
                return false;
            }

            Vector2 origin = transform.position;
            if (!stageLoop.TryGetNextObjectiveTarget(origin, out Vector3 target, out bool targetIsExit))
            {
                return false;
            }

            Color color = targetIsExit ? echoObjectiveExitColor : echoObjectiveBreadcrumbColor;
            bool spawnedPrimary = SpawnEchoObjectiveScanLine(
                origin,
                target,
                color,
                "ManualEchoObjectiveScan",
                targetIsExit ? "ManualEchoExitScanPoint" : "ManualEchoBreadcrumbScanPoint",
                1f,
                1f,
                1f,
                targetIsExit ? 1.12f : 0.92f,
                0);
            if (!spawnedPrimary)
            {
                return false;
            }

            int choiceCount = SpawnEchoObjectiveChoiceScans(stageLoop, origin, target);
            LastEchoObjectiveScanCount = 1 + choiceCount;
            LastEchoObjectiveChoiceScanCount = choiceCount;
            LastEchoObjectivePrimaryWasExit = targetIsExit;
            LastEchoObjectiveScanTime = Time.time;
            RaiseEchoObjectiveScanEvent(stageLoop, targetIsExit, choiceCount);
            return true;
        }

        private void RaiseEchoObjectiveScanEvent(StageLoopDirector stageLoop, bool primaryIsExit, int choiceCount)
        {
            if (choiceCount <= 0 || RegressionChecklistRunner.IsRegressionRunActive)
            {
                return;
            }

            RuntimeEventBus.Raise(
                RuntimeEventType.Ability,
                BuildEchoObjectiveScanMessage(primaryIsExit, choiceCount),
                this,
                stageLoop != null ? stageLoop.CurrentStage : 0,
                semantic: RuntimeEventSemantic.EchoChoiceScan);
        }

        private static string BuildEchoObjectiveScanMessage(bool primaryIsExit, int choiceCount)
        {
            string primary = primaryIsExit ? "출구" : "빵부스러기";
            return $"메아리 경로 스캔: {primary} + 선택지 {Mathf.Max(0, choiceCount)}개";
        }

        private int SpawnEchoObjectiveChoiceScans(StageLoopDirector stageLoop, Vector2 origin, Vector3 primaryTarget)
        {
            if (!showEchoObjectiveChoiceScans || stageLoop == null)
            {
                return 0;
            }

            int spawned = 0;
            if (stageLoop.ExitChoiceCacheActive
                && CanUseEchoObjectiveChoiceTarget(origin, primaryTarget, stageLoop.ExitChoiceCacheWorldPosition))
            {
                Color color = echoObjectiveExitCacheColor;
                color.a *= Mathf.Clamp01(echoObjectiveChoiceAlphaScale);
                if (SpawnEchoObjectiveScanLine(
                        origin,
                        stageLoop.ExitChoiceCacheWorldPosition,
                        color,
                        "ManualEchoExitCacheChoiceScan",
                        "ManualEchoExitCacheChoicePoint",
                        echoObjectiveChoiceMaxDistanceMultiplier,
                        echoObjectiveChoiceWidthScale,
                        echoObjectiveChoiceDurationScale,
                        0.76f,
                        -1))
                {
                    spawned++;
                }
            }

            if (stageLoop.TryGetNearestRiskCacheTarget(origin, out Vector3 riskTarget, out _)
                && CanUseEchoObjectiveChoiceTarget(origin, primaryTarget, riskTarget))
            {
                Color color = echoObjectiveRiskCacheColor;
                color.a *= Mathf.Clamp01(echoObjectiveChoiceAlphaScale);
                if (SpawnEchoObjectiveScanLine(
                        origin,
                        riskTarget,
                        color,
                        "ManualEchoRiskCacheChoiceScan",
                        "ManualEchoRiskCacheChoicePoint",
                        echoObjectiveChoiceMaxDistanceMultiplier,
                        echoObjectiveChoiceWidthScale,
                        echoObjectiveChoiceDurationScale,
                        0.7f,
                        -1))
                {
                    spawned++;
                }
            }

            return spawned;
        }

        private int CountEchoObjectiveChoiceTargets(StageLoopDirector stageLoop, Vector2 origin, Vector3 primaryTarget)
        {
            if (!showEchoObjectiveChoiceScans || stageLoop == null)
            {
                return 0;
            }

            int count = 0;
            if (stageLoop.ExitChoiceCacheActive
                && CanUseEchoObjectiveChoiceTarget(origin, primaryTarget, stageLoop.ExitChoiceCacheWorldPosition))
            {
                count++;
            }

            if (stageLoop.TryGetNearestRiskCacheTarget(origin, out Vector3 riskTarget, out _)
                && CanUseEchoObjectiveChoiceTarget(origin, primaryTarget, riskTarget))
            {
                count++;
            }

            return count;
        }

        private bool SpawnEchoObjectiveScanLine(
            Vector2 origin,
            Vector3 target,
            Color color,
            string scanName,
            string pulseName,
            float maxDistanceMultiplier,
            float widthScale,
            float durationScale,
            float pulseRadius,
            int sortingOffset)
        {
            Vector2 toTarget = (Vector2)target - origin;
            float distance = toTarget.magnitude;
            if (distance <= 0.35f)
            {
                return false;
            }

            float maxDistance = Mathf.Max(0.2f, echoObjectiveScanMaxDistance * Mathf.Max(0.1f, maxDistanceMultiplier));
            Vector2 scanEnd = origin + toTarget.normalized * Mathf.Min(distance, maxDistance);

            GameObject scanObject = new($"{scanName}_{Time.frameCount}");
            Transform vfxRoot = EnsureScenePath("Scene_Root/GameRoot/Runtime/VFX/ManualEcho");
            if (vfxRoot != null)
            {
                scanObject.transform.SetParent(vfxRoot, false);
            }

            LineRenderer line = scanObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = false;
            line.positionCount = 3;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.widthMultiplier = Mathf.Max(0.01f, echoObjectiveScanWidth) * Mathf.Max(0.1f, widthScale);
            line.sharedMaterial = GetEchoObjectiveScanMaterial();
            line.sortingOrder = echoObjectiveScanSortingOrder + sortingOffset;

            Vector3[] points = BuildEchoObjectiveScanPoints(origin, scanEnd);
            for (int i = 0; i < points.Length; i++)
            {
                line.SetPosition(i, points[i]);
            }

            StartCoroutine(EchoObjectiveScanRoutine(scanObject, line, points, color, widthScale, durationScale));

            GameObject pulseObject = new(pulseName);
            if (vfxRoot != null)
            {
                pulseObject.transform.SetParent(vfxRoot, false);
            }

            pulseObject.transform.position = new Vector3(scanEnd.x, scanEnd.y, 0f);
            EchoPulseVisualDummy visual = pulseObject.AddComponent<EchoPulseVisualDummy>();
            Color pulseColor = color;
            pulseColor.a *= 0.58f;
            visual.Configure(
                Mathf.Max(0.1f, pulseRadius),
                pulseColor,
                Mathf.Max(0.1f, echoObjectiveScanDuration * Mathf.Max(0.1f, durationScale) * 0.8f),
                1,
                0f,
                echoObjectiveScanSortingOrder + sortingOffset);
            return true;
        }

        private bool CanUseEchoObjectiveChoiceTarget(Vector2 origin, Vector3 primaryTarget, Vector3 choiceTarget)
        {
            return IsEchoObjectiveScanTargetViable(origin, choiceTarget)
                   && Vector2.Distance(primaryTarget, choiceTarget) >= Mathf.Max(0f, echoObjectiveChoiceMinSeparation);
        }

        private static bool IsEchoObjectiveScanTargetViable(Vector2 origin, Vector3 target)
        {
            return ((Vector2)target - origin).sqrMagnitude > 0.35f * 0.35f;
        }

        private Vector3[] BuildEchoObjectiveScanPoints(Vector2 origin, Vector2 target)
        {
            Vector2 direction = target - origin;
            Vector2 side = direction.sqrMagnitude > 0.001f
                ? new Vector2(-direction.y, direction.x).normalized
                : Vector2.up;
            float waver = Mathf.Max(0f, echoObjectiveScanWaver);
            return new[]
            {
                new Vector3(origin.x, origin.y, 0f),
                (Vector3)(Vector2.Lerp(origin, target, 0.58f) + side * waver),
                new Vector3(target.x, target.y, 0f)
            };
        }

        private IEnumerator EchoObjectiveScanRoutine(
            GameObject scanObject,
            LineRenderer line,
            Vector3[] basePoints,
            Color baseColor,
            float widthScale,
            float durationScale)
        {
            float duration = Mathf.Max(0.1f, echoObjectiveScanDuration * Mathf.Max(0.1f, durationScale));
            float safeWidthScale = Mathf.Max(0.1f, widthScale);
            float startedAt = Time.time;
            Vector3 direction = basePoints[^1] - basePoints[0];
            Vector3 side = direction.sqrMagnitude > 0.001f
                ? new Vector3(-direction.y, direction.x, 0f).normalized
                : Vector3.up;

            while (line != null && Time.time < startedAt + duration)
            {
                float elapsed = Time.time - startedAt;
                float t = Mathf.Clamp01(elapsed / duration);
                float fade = 1f - Mathf.SmoothStep(0.18f, 1f, t);
                float shimmer = 0.7f + Mathf.Sin((elapsed * 2.35f + basePoints[1].sqrMagnitude * 0.07f) * Mathf.PI * 2f) * 0.3f;

                for (int i = 0; i < basePoints.Length; i++)
                {
                    Vector3 point = basePoints[i];
                    if (i == 1)
                    {
                        point += side * Mathf.Sin(elapsed * Mathf.PI * 2f * 1.35f) * echoObjectiveScanWaver * 0.28f * fade;
                    }

                    line.SetPosition(i, point);
                }

                Color color = baseColor;
                color.a *= fade * Mathf.Clamp01(shimmer);
                line.startColor = color;
                line.endColor = color;
                line.widthMultiplier = Mathf.Max(0.01f, echoObjectiveScanWidth) * safeWidthScale * Mathf.Lerp(1.18f, 0.28f, t);
                yield return null;
            }

            if (scanObject != null)
            {
                Destroy(scanObject);
            }
        }

        private Material GetEchoObjectiveScanMaterial()
        {
            if (echoObjectiveScanMaterial != null)
            {
                return echoObjectiveScanMaterial;
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            }

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                return null;
            }

            echoObjectiveScanMaterial = new Material(shader)
            {
                name = "EchoObjectiveScanMaterial",
                hideFlags = HideFlags.HideAndDontSave
            };
            return echoObjectiveScanMaterial;
        }

        private static Transform EnsureScenePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            string[] parts = path.Split('/');
            GameObject root = GameObject.Find(parts[0]);
            if (root == null)
            {
                root = new GameObject(parts[0]);
            }

            Transform current = root.transform;
            for (int i = 1; i < parts.Length; i++)
            {
                Transform child = current.Find(parts[i]);
                if (child == null)
                {
                    GameObject childObject = new(parts[i]);
                    childObject.transform.SetParent(current, false);
                    child = childObject.transform;
                }

                current = child;
            }

            return current;
        }
    }
}


