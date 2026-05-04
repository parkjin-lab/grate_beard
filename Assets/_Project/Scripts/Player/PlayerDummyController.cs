using LostBreadcrumbs.Runtime.Core.Input;
using LostBreadcrumbs.Runtime.Managers;
using LostBreadcrumbs.Runtime.Map;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class PlayerDummyController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float moveSpeed = 2.65f;
        [SerializeField] private KeyCode moveLeftKey = KeyCode.A;
        [SerializeField] private KeyCode moveRightKey = KeyCode.D;
        [SerializeField] private KeyCode moveDownKey = KeyCode.S;
        [SerializeField] private KeyCode moveUpKey = KeyCode.W;

        [Header("Sprint")]
        [SerializeField] private bool enableSprint = true;
        [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;
        [SerializeField] private KeyCode sprintAltKey = KeyCode.RightShift;
        [SerializeField, Min(1f)] private float sprintMoveMultiplier = 1.42f;
        [SerializeField, Min(0.1f)] private float maxStamina = 4f;
        [SerializeField, Min(0f)] private float staminaDrainPerSecond = 1.4f;
        [SerializeField, Min(0f)] private float staminaRecoverPerSecond = 0.95f;
        [SerializeField, Min(0f)] private float staminaRecoverDelay = 0.45f;
        [SerializeField, Min(0f)] private float exhaustedLockSeconds = 0.65f;

        [Header("Noise")]
        [SerializeField, Min(0.05f)] private float footstepInterval = 0.46f;
        [SerializeField, Min(0.1f)] private float footstepLoudness = 0.45f;
        [SerializeField, Min(0.1f)] private float echoLoudness = 2.1f;
        [SerializeField, Min(0.1f)] private float echoRadius = 7.8f;
        [SerializeField, Min(0.05f)] private float sprintFootstepIntervalScale = 0.7f;
        [SerializeField, Min(1f)] private float sprintNoiseMultiplier = 1.55f;

        [Header("Echo Visual")]
        [SerializeField] private bool spawnEchoVisual = true;
        [SerializeField] private Color echoVisualColor = new(0.32f, 0.72f, 1f, 0.68f);
        [SerializeField, Min(0.1f)] private float echoVisualDuration = 2.2f;
        [SerializeField, Range(1, 4)] private int echoVisualRingCount = 3;
        [SerializeField, Min(0f)] private float echoVisualRingInterval = 0.38f;
        [SerializeField, Min(0.5f)] private float echoVisualRadiusMultiplier = 1f;
        [SerializeField] private int echoVisualSortingOrder = 34;

        [Header("Echo Fog Trace")]
        [SerializeField] private bool revealFogWithEcho = true;
        [SerializeField, Min(0.1f)] private float echoFogRevealRadiusMultiplier = 0.72f;
        [SerializeField, Min(0f)] private float echoFogRevealSoftnessBoost = 0.8f;

        [Header("Input")]
        [SerializeField] private KeyCode echoKey = KeyCode.Space;
        [SerializeField] private KeyCode flashlightKey = KeyCode.F;
        [SerializeField] private bool freezeInputDuringRegressionChecklist = true;

        [Header("Collision")]
        [SerializeField] private bool autoConfigurePhysicsBody = true;
        [SerializeField, Min(0.05f)] private float collisionRadius = 0.32f;
        [SerializeField] private Vector2 collisionOffset = Vector2.zero;
        [SerializeField, Min(0f)] private float collisionCastPadding = 0.02f;

        private float nextFootstepTime;
        private Vector2 moveInput;

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

        private PlayerVisibilitySource visibilitySource;
        private PlayerConcealmentState concealmentState;
        private PlayerBehaviorTelemetry behaviorTelemetry;
        private FogOfWarSystem fogSystem;
        private Rigidbody2D rb;
        private ContactFilter2D movementContactFilter;
        private int cachedMovementLayerMask = int.MinValue;
        private readonly RaycastHit2D[] movementCastHits = new RaycastHit2D[8];

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

        private void Awake()
        {
            visibilitySource = GetComponent<PlayerVisibilitySource>();
            concealmentState = GetComponent<PlayerConcealmentState>();
            behaviorTelemetry = GetComponent<PlayerBehaviorTelemetry>();
            fogSystem = FindFirstObjectByType<FogOfWarSystem>();

            EnsurePlayerTag();

            EnsurePhysicsComponents();

            maxStamina = Mathf.Max(0.1f, maxStamina);
            currentStamina = MaxStamina;
            currentMoveSpeed = moveSpeed;
        }

        private void Update()
        {
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
                transform.right = moveInput;

                float stepInterval = footstepInterval * (isSprinting ? sprintFootstepIntervalScale : 1f);
                if (Time.time >= nextFootstepTime)
                {
                    float movementNoiseScale = isSprinting ? sprintNoiseMultiplier * runtimeSprintNoiseMultiplier : 1f;
                    EmitNoise(footstepLoudness * runtimeFootstepNoiseMultiplier, footstepLoudness * 3f, NoiseKind.Footstep, movementNoiseScale);
                    nextFootstepTime = Time.time + Mathf.Max(0.05f, stepInterval);
                }
            }

            if (RuntimeInputAdapter.GetKeyDown(echoKey))
            {
                EmitNoise(echoLoudness, echoRadius, NoiseKind.Echo);
                SpawnEchoVisual(echoRadius);
                ApplyEchoFogTrace(echoRadius);
                behaviorTelemetry?.RegisterEcho();
            }

            if (RuntimeInputAdapter.GetKeyDown(flashlightKey))
            {
                if (visibilitySource != null)
                {
                    visibilitySource.ToggleFlashlight();
                }

                behaviorTelemetry?.RegisterFlashlightToggle();
                EmitNoise(0.25f, 2f, NoiseKind.FlashlightToggle);
            }
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

        public void ForceResetSprintState(bool refillStamina = true)
        {
            isSprinting = false;
            recoverDelayUntil = Time.time;
            exhaustedUntil = 0f;

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


