using System.Collections;
using System.Collections.Generic;
using LostBreadcrumbs.Runtime.AI;
using LostBreadcrumbs.Runtime.Events;
using LostBreadcrumbs.Runtime.Core.Input;
using LostBreadcrumbs.Runtime.Managers;
using LostBreadcrumbs.Runtime.Map;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Player
{
    public sealed class PlayerEchoPulseAbility : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private KeyCode pulseKey = KeyCode.Q;

        [Header("Pulse")]
        [SerializeField, Min(0.1f)] private float stunRadius = 2.2f;
        [SerializeField, Min(0.1f)] private float stunDurationSeconds = 1.6f;
        [SerializeField, Min(0.1f)] private float cooldownSeconds = 5f;

        [Header("Risk (Noise)")]
        [SerializeField, Min(0.1f)] private float noiseLoudness = 2.2f;
        [SerializeField, Min(0.1f)] private float noiseRadius = 8f;

        [Header("Visual")]
        [SerializeField] private bool spawnPulseVisual = true;
        [SerializeField] private Color pulseVisualColor = new(0.36f, 0.78f, 1f, 0.78f);
        [SerializeField, Min(0.1f)] private float pulseVisualDuration = 2.75f;
        [SerializeField, Range(1, 4)] private int pulseVisualRingCount = 3;
        [SerializeField, Min(0f)] private float pulseVisualRingInterval = 0.52f;
        [SerializeField, Min(0.5f)] private float pulseVisualRadiusMultiplier = 1.18f;
        [SerializeField] private int pulseVisualSortingOrder = 36;

        [Header("Resonance Tail")]
        [SerializeField] private bool enableResonanceTail = true;
        [SerializeField, Range(1, 5)] private int resonanceTailPulseCount = 3;
        [SerializeField, Min(0.05f)] private float resonanceTailInterval = 0.78f;
        [SerializeField, Range(0.1f, 1f)] private float resonanceTailRevealScale = 0.58f;
        [SerializeField, Range(0f, 1f)] private float resonanceTailNoiseScale = 0.32f;
        [SerializeField, Range(0f, 1f)] private float resonanceTailVisualAlphaScale = 0.58f;
        [SerializeField, Min(0.1f)] private float resonanceTailVisualDuration = 1.65f;
        [SerializeField] private int resonanceTailSortingOrder = 35;

        [Header("Echo Return")]
        [SerializeField] private bool enableEchoReturnThreatHint = true;
        [SerializeField, Min(0.5f)] private float echoReturnThreatRadiusMultiplier = 4.1f;
        [SerializeField, Min(0.2f)] private float echoReturnHintMaxDistance = 8.5f;
        [SerializeField, Min(0.1f)] private float echoReturnHintDuration = 1.55f;
        [SerializeField, Min(0.1f)] private float echoReturnWarningSeconds = 2.9f;
        [SerializeField, Min(0.01f)] private float echoReturnHintWidth = 0.06f;
        [SerializeField, Min(0f)] private float echoReturnHintWaver = 0.58f;
        [SerializeField] private Color echoReturnThreatColor = new(1f, 0.24f, 0.16f, 0.48f);
        [SerializeField] private int echoReturnHintSortingOrder = 39;

        [Header("Fog Reveal")]
        [SerializeField] private bool revealFogWithPulse = true;
        [SerializeField, Min(0.1f)] private float fogRevealRadiusMultiplier = 1.05f;
        [SerializeField, Min(0f)] private float fogRevealSoftnessBoost = 1.05f;

        [Header("Scout Ping")]
        [SerializeField] private bool revealNearbyTargetsWithPulse = true;
        [SerializeField, Min(0.1f)] private float scoutRadiusMultiplier = 1.9f;
        [SerializeField, Min(0.1f)] private float scoutRevealDuration = 1.85f;
        [SerializeField, Range(1, 32)] private int maxScoutRevealTargets = 18;
        [SerializeField, Range(0f, 1f)] private float scoutHiddenFogThreshold = 0.42f;
        [SerializeField] private Color scoutBreadcrumbColor = new(1f, 0.86f, 0.22f, 1f);
        [SerializeField] private Color scoutExitColor = new(0.35f, 1f, 0.55f, 1f);
        [SerializeField] private Color scoutHazardColor = new(1f, 0.34f, 0.22f, 1f);
        [SerializeField] private Color scoutEnemyColor = new(0.42f, 0.92f, 1f, 1f);

        [Header("Overcharge")]
        [SerializeField] private bool enableOverchargeHold = true;
        [SerializeField, Min(0.05f)] private float tapGraceSeconds = 0.16f;
        [SerializeField, Min(0.15f)] private float chargeBuildSeconds = 0.85f;
        [SerializeField, Range(1f, 2.5f)] private float overchargeRevealRadiusMultiplier = 1.65f;
        [SerializeField, Range(1f, 2.5f)] private float overchargeNoiseMultiplier = 1.80f;
        [SerializeField, Range(0, 4)] private int overchargeExtraResonancePulses = 2;
        [SerializeField] private Color overchargeWarningColor = new(1f, 0.28f, 0.16f, 0.82f);
        [SerializeField] private bool spawnChargePreviewRings = true;
        [SerializeField, Range(0.2f, 1f)] private float smokeRevealRadiusMultiplier = 0.72f;

        [Header("Debug")]
        [SerializeField] private bool logPulseResult = false;

        private float nextReadyTime;
        private int lastStunnedCount;
        private int lastScoutRevealCount;
        private float lastNoiseScale = 1f;
        private float lastCharge01;
        private bool lastCastWasOvercharge;
        private bool lastCastWasAutoFullCharge;
        private bool lastCastWasInsideSmoke;
        private float lastRevealRadiusMultiplier = 1f;
        private bool isCharging;
        private float chargeHoldStartedAt;
        private float currentCharge01;
        private Transform chargePreviewRoot;
        private SpriteRenderer[] chargePreviewRings;
        private float runtimeCooldownMultiplier = 1f;
        private float runtimeStunRadiusMultiplier = 1f;
        private float runtimeNoiseMultiplier = 1f;
        private Coroutine resonanceTailRoutine;
        private float echoResonanceUntil;
        private int lastEchoReturnThreatCount;
        private float lastEchoReturnDistance;
        private float echoReturnWarningUntil;
        private bool echoReturnRaisedThisCast;
        private Material echoReturnLineMaterial;

        private PlayerConcealmentState concealmentState;
        private PlayerBehaviorTelemetry behaviorTelemetry;
        private FogOfWarSystem fogSystem;
        private readonly List<EnemyController> cachedEnemies = new(16);
        private readonly List<GameObject> activeEchoVisuals = new(12);
        private readonly List<BreadcrumbPickup> cachedBreadcrumbs = new(16);
        private readonly List<ExitPortalDummy> cachedExits = new(4);
        private readonly List<RoomArchetypeHookDummy> cachedHooks = new(16);

        public bool IsReady => Time.time >= nextReadyTime;
        public float CooldownRemaining => Mathf.Max(0f, nextReadyTime - Time.time);
        public int LastStunnedCount => lastStunnedCount;
        public int LastScoutRevealCount => lastScoutRevealCount;
        public float LastNoiseScale => lastNoiseScale;
        public float EffectiveCooldownSeconds => Mathf.Max(0.1f, cooldownSeconds * runtimeCooldownMultiplier);
        public float EffectiveStunRadius => Mathf.Max(0.1f, stunRadius * runtimeStunRadiusMultiplier);
        public bool IsEchoResonating => EchoResonanceRemaining > 0.05f;
        public float EchoResonanceRemaining => Mathf.Max(0f, echoResonanceUntil - Time.time);
        public bool IsEchoReturnWarningActive => EchoReturnWarningRemaining > 0.05f;
        public float EchoReturnWarningRemaining => Mathf.Max(0f, echoReturnWarningUntil - Time.time);
        public int LastEchoReturnThreatCount => lastEchoReturnThreatCount;
        public float LastEchoReturnDistance => lastEchoReturnDistance;
        public bool HasActiveEchoRuntimeEffects => resonanceTailRoutine != null
                                                   || IsEchoResonating
                                                   || IsEchoReturnWarningActive
                                                   || ActiveEchoVisualCount > 0;
        public int ActiveEchoVisualCount => CountActiveEchoVisuals();
        public float RuntimeCooldownMultiplier => runtimeCooldownMultiplier;
        public float RuntimeStunRadiusMultiplier => runtimeStunRadiusMultiplier;
        public float RuntimeNoiseMultiplier => runtimeNoiseMultiplier;
        public bool IsCharging => isCharging;
        public float Charge01 => Mathf.Clamp01(currentCharge01);
        public int ChargePercent => Mathf.RoundToInt(Charge01 * 100f);
        public float LastCharge01 => Mathf.Clamp01(lastCharge01);
        public bool LastCastWasOvercharge => lastCastWasOvercharge;
        public bool LastCastWasAutoFullCharge => lastCastWasAutoFullCharge;
        public bool LastCastWasInsideSmoke => lastCastWasInsideSmoke;
        public bool IsInsideSmoke => EvaluateInsideSmoke();
        public float LastRevealRadiusMultiplier => Mathf.Max(0.1f, lastRevealRadiusMultiplier);
        public float LastAppliedNoiseMultiplier => lastNoiseScale;
        public float SmokeRevealRadiusMultiplier => Mathf.Clamp(smokeRevealRadiusMultiplier, 0.2f, 1f);
        public float OverchargeRevealRadiusMultiplier => Mathf.Max(1f, overchargeRevealRadiusMultiplier);
        public float OverchargeNoiseMultiplier => Mathf.Max(1f, overchargeNoiseMultiplier);
        public int OverchargeExtraResonancePulses => Mathf.Max(0, overchargeExtraResonancePulses);

        private void Awake()
        {
            concealmentState = GetComponent<PlayerConcealmentState>();
            behaviorTelemetry = GetComponent<PlayerBehaviorTelemetry>();
            fogSystem = FindFirstObjectByType<FogOfWarSystem>();
        }

        public void ApplyRuntimeModifiers(float cooldownMultiplier, float stunRadiusMultiplier, float noiseMultiplier)
        {
            runtimeCooldownMultiplier = Mathf.Clamp(cooldownMultiplier, 0.2f, 2.8f);
            runtimeStunRadiusMultiplier = Mathf.Clamp(stunRadiusMultiplier, 0.4f, 2.4f);
            runtimeNoiseMultiplier = Mathf.Clamp(noiseMultiplier, 0.25f, 2.8f);
        }

        public void ResetRuntimeModifiers()
        {
            ApplyRuntimeModifiers(1f, 1f, 1f);
        }

        public void ResetCooldown()
        {
            ResetAbilityState(clearActiveVisuals: true);
        }

        public void ResetAbilityState(bool clearActiveVisuals = true)
        {
            nextReadyTime = 0f;
            lastStunnedCount = 0;
            lastScoutRevealCount = 0;
            lastNoiseScale = 1f;
            lastCharge01 = 0f;
            lastCastWasOvercharge = false;
            lastCastWasAutoFullCharge = false;
            lastCastWasInsideSmoke = false;
            lastRevealRadiusMultiplier = 1f;
            CancelCharge(clearPreview: clearActiveVisuals);
            ClearEchoReturnState();
            StopEchoResonanceTail();

            if (clearActiveVisuals)
            {
                DestroyActiveEchoVisuals();
            }
        }

        public void SetCooldownRemainingForRuntime(float remainingSeconds)
        {
            nextReadyTime = Time.time + Mathf.Max(0f, remainingSeconds);
        }

        private void Update()
        {
            if (RegressionChecklistRunner.IsRegressionRunActive)
            {
                CancelCharge(clearPreview: true);
                return;
            }

            if (Time.timeScale <= 0.0001f)
            {
                return;
            }

            if (!enableOverchargeHold || !StageManager.IsOverchargeHoldUnlocked)
            {
                if (RuntimeInputAdapter.GetKeyDown(pulseKey))
                {
                    TryCastPulse();
                }

                return;
            }

            TickOverchargeInput();
        }

        private void OnDisable()
        {
            CancelCharge(clearPreview: true);
            StopEchoResonanceTail();
            ClearEchoReturnState();
            DestroyActiveEchoVisuals();
        }

        public bool TryCastPulse()
        {
            return TryCastPulse(0f, autoFullCharge: false);
        }

        public bool TryCastPulse(float charge01, bool autoFullCharge)
        {
            if (!IsReady)
            {
                return false;
            }

            bool insideSmoke = EvaluateInsideSmoke();
            EchoOverchargePreview preview = EvaluateOverchargePreview(charge01, insideSmoke);
            nextReadyTime = Time.time + EffectiveCooldownSeconds;
            lastStunnedCount = 0;
            lastScoutRevealCount = 0;
            lastEchoReturnThreatCount = 0;
            lastEchoReturnDistance = 0f;
            echoReturnWarningUntil = 0f;
            echoReturnRaisedThisCast = false;
            lastCharge01 = preview.Charge01;
            lastCastWasOvercharge = preview.Charge01 > 0.001f;
            lastCastWasAutoFullCharge = autoFullCharge && preview.Charge01 >= 0.999f;
            lastCastWasInsideSmoke = insideSmoke;
            lastRevealRadiusMultiplier = preview.RevealRadiusMultiplier;
            CancelCharge(clearPreview: true);

            Vector2 origin = transform.position;
            float effectiveStunRadius = preview.StunRadius;

            SpawnPulseVisual(origin, effectiveStunRadius, preview);
            ApplyEchoFogReveal(origin, effectiveStunRadius, preview.RevealRadiusMultiplier);
            lastScoutRevealCount = RevealNearbyScoutTargets(origin, effectiveStunRadius, preview.RevealRadiusMultiplier);
            EmitRiskNoise(preview.NoiseMultiplier);
            StartEchoResonanceTail(origin, effectiveStunRadius, lastNoiseScale, preview.ResonancePulseCount);

            EnemyController.CopyActiveControllers(cachedEnemies);
            for (int i = 0; i < cachedEnemies.Count; i++)
            {
                EnemyController enemy = cachedEnemies[i];
                if (enemy == null)
                {
                    continue;
                }

                float distance = Vector2.Distance(origin, enemy.transform.position);
                if (distance > effectiveStunRadius)
                {
                    continue;
                }

                if (enemy.ApplyStun(stunDurationSeconds, origin, "EchoPulse"))
                {
                    lastStunnedCount++;
                }
            }

            behaviorTelemetry?.RegisterPulseCast(
                preview.Charge01,
                lastCastWasAutoFullCharge,
                insideSmoke,
                preview.RevealRadiusMultiplier,
                lastNoiseScale);
            RuntimeEventBus.Raise(RuntimeEventType.Ability, BuildEchoPulseUsedMessage(lastStunnedCount, lastScoutRevealCount, preview.Charge01, insideSmoke), this);

            if (logPulseResult)
            {
                Debug.Log($"Echo Pulse cast. Charge={preview.Charge01:0.00}, Stunned={lastStunnedCount}, Scout={lastScoutRevealCount}, NoiseScale={lastNoiseScale:0.00}", this);
            }

            return true;
        }

        private void SpawnPulseVisual(Vector2 origin, float effectiveStunRadius, EchoOverchargePreview preview)
        {
            if (!spawnPulseVisual)
            {
                return;
            }

            GameObject visualObject = new($"EchoPulseWave_{Time.frameCount}");
            RegisterEchoVisual(visualObject);
            Transform vfxRoot = EnsureScenePath("Scene_Root/GameRoot/Runtime/VFX");
            if (vfxRoot != null)
            {
                visualObject.transform.SetParent(vfxRoot, false);
            }

            visualObject.transform.position = new Vector3(origin.x, origin.y, 0f);
            EchoPulseVisualDummy visual = visualObject.AddComponent<EchoPulseVisualDummy>();
            float visualRadius = Mathf.Max(0.45f, effectiveStunRadius * pulseVisualRadiusMultiplier * preview.RevealRadiusMultiplier);
            visual.Configure(
                visualRadius,
                EvaluateOverchargeRingColor(preview.Charge01),
                pulseVisualDuration,
                pulseVisualRingCount,
                pulseVisualRingInterval,
                pulseVisualSortingOrder);
        }

        private void StartEchoResonanceTail(Vector2 origin, float effectiveStunRadius, float baseNoiseScale, int pulseCount)
        {
            if (!enableResonanceTail)
            {
                StopEchoResonanceTail();
                return;
            }

            pulseCount = Mathf.Clamp(pulseCount, 1, 5);
            float interval = Mathf.Max(0.05f, resonanceTailInterval);
            StopEchoResonanceTail();
            echoResonanceUntil = Time.time + interval * pulseCount + Mathf.Max(0.1f, resonanceTailVisualDuration);
            resonanceTailRoutine = StartCoroutine(EchoResonanceTailRoutine(origin, effectiveStunRadius, baseNoiseScale, pulseCount, interval));
        }

        private void StopEchoResonanceTail()
        {
            if (resonanceTailRoutine != null)
            {
                StopCoroutine(resonanceTailRoutine);
                resonanceTailRoutine = null;
            }

            echoResonanceUntil = 0f;
        }

        private void ClearEchoReturnState()
        {
            lastEchoReturnThreatCount = 0;
            lastEchoReturnDistance = 0f;
            echoReturnWarningUntil = 0f;
            echoReturnRaisedThisCast = false;
        }

        private void RegisterEchoVisual(GameObject visualObject)
        {
            if (visualObject == null)
            {
                return;
            }

            PruneActiveEchoVisuals();
            activeEchoVisuals.Add(visualObject);
        }

        private int CountActiveEchoVisuals()
        {
            PruneActiveEchoVisuals();
            return activeEchoVisuals.Count;
        }

        private void PruneActiveEchoVisuals()
        {
            for (int i = activeEchoVisuals.Count - 1; i >= 0; i--)
            {
                if (activeEchoVisuals[i] == null)
                {
                    activeEchoVisuals.RemoveAt(i);
                }
            }
        }

        private void DestroyActiveEchoVisuals()
        {
            for (int i = activeEchoVisuals.Count - 1; i >= 0; i--)
            {
                if (activeEchoVisuals[i] != null)
                {
                    Destroy(activeEchoVisuals[i]);
                }
            }

            activeEchoVisuals.Clear();
        }

        private IEnumerator EchoResonanceTailRoutine(Vector2 origin, float effectiveStunRadius, float baseNoiseScale, int pulseCount, float interval)
        {
            for (int i = 0; i < pulseCount; i++)
            {
                yield return new WaitForSeconds(interval);

                if (!isActiveAndEnabled)
                {
                    yield break;
                }

                float t = (i + 1f) / Mathf.Max(1f, pulseCount);
                float radius = Mathf.Max(0.25f, effectiveStunRadius * Mathf.Lerp(0.72f, 1.34f, t));
                SpawnResonanceTailVisual(origin, radius, t);
                ApplyEchoResonanceFogReveal(origin, radius, t);
                EmitResonanceTailNoise(origin, baseNoiseScale, t);
                if (i == pulseCount - 1)
                {
                    TrySpawnEchoReturnThreatHint(origin, effectiveStunRadius, t);
                }
            }

            resonanceTailRoutine = null;
        }

        private void SpawnResonanceTailVisual(Vector2 origin, float radius, float t)
        {
            if (!spawnPulseVisual)
            {
                return;
            }

            GameObject visualObject = new($"EchoPulseResonance_{Time.frameCount}");
            RegisterEchoVisual(visualObject);
            Transform vfxRoot = EnsureScenePath("Scene_Root/GameRoot/Runtime/VFX");
            if (vfxRoot != null)
            {
                visualObject.transform.SetParent(vfxRoot, false);
            }

            visualObject.transform.position = new Vector3(origin.x, origin.y, 0f);
            EchoPulseVisualDummy visual = visualObject.AddComponent<EchoPulseVisualDummy>();
            Color color = pulseVisualColor;
            color.a *= resonanceTailVisualAlphaScale * Mathf.Lerp(0.92f, 0.48f, Mathf.Clamp01(t));
            visual.Configure(
                Mathf.Max(0.3f, radius * pulseVisualRadiusMultiplier),
                color,
                Mathf.Max(0.1f, resonanceTailVisualDuration),
                1,
                0f,
                resonanceTailSortingOrder);
        }

        private void ApplyEchoResonanceFogReveal(Vector2 origin, float radius, float t)
        {
            if (!revealFogWithPulse)
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

            float revealRadius = Mathf.Max(0.1f, radius * fogRevealRadiusMultiplier * resonanceTailRevealScale);
            float softness = fogRevealSoftnessBoost * Mathf.Lerp(0.34f, 0.62f, Mathf.Clamp01(t));
            fogSystem.ApplyEchoRevealPulse(origin, revealRadius, softness);
        }

        private void EmitResonanceTailNoise(Vector2 origin, float baseNoiseScale, float t)
        {
            if (NoiseManager.Instance == null || resonanceTailNoiseScale <= 0f)
            {
                return;
            }

            float falloff = Mathf.Lerp(0.86f, 0.52f, Mathf.Clamp01(t));
            float loudness = noiseLoudness * Mathf.Max(0.1f, baseNoiseScale) * resonanceTailNoiseScale * falloff;
            float radius = noiseRadius * Mathf.Lerp(0.48f, 0.78f, Mathf.Clamp01(t)) * Mathf.Lerp(0.72f, 1f, Mathf.Clamp01(baseNoiseScale));
            NoiseManager.Instance.EmitNoise(
                origin,
                Mathf.Max(0.05f, loudness),
                Mathf.Max(0.2f, radius),
                NoiseKind.Echo,
                gameObject);
        }

        private void TrySpawnEchoReturnThreatHint(Vector2 origin, float effectiveStunRadius, float intensity)
        {
            if (!enableEchoReturnThreatHint)
            {
                return;
            }

            if (!TryFindEchoReturnThreat(origin, effectiveStunRadius, out EnemyController enemy, out float distance, out int threatCount))
            {
                return;
            }

            Vector2 threatPosition = enemy.transform.position;
            Vector2 toThreat = threatPosition - origin;
            if (toThreat.sqrMagnitude <= 0.001f)
            {
                return;
            }

            float safeIntensity = Mathf.Clamp01(intensity);
            Vector2 direction = toThreat.normalized;
            Vector2 hintEnd = origin + direction * Mathf.Min(distance, Mathf.Max(0.2f, echoReturnHintMaxDistance));
            SpawnEchoReturnHintLine(origin, hintEnd, safeIntensity);
            SpawnEchoReturnHintPulse(hintEnd, safeIntensity);

            lastEchoReturnThreatCount = Mathf.Max(1, threatCount);
            lastEchoReturnDistance = distance;
            echoReturnWarningUntil = Time.time + Mathf.Max(0.1f, echoReturnWarningSeconds);

            if (!echoReturnRaisedThisCast)
            {
                echoReturnRaisedThisCast = true;
                int stage = StageLoopDirector.Instance != null ? Mathf.Max(1, StageLoopDirector.Instance.CurrentStage) : 0;
                RuntimeEventBus.Raise(
                    RuntimeEventType.Ability,
                    BuildEchoReturnThreatMessage(distance),
                    this,
                    stage,
                    semantic: RuntimeEventSemantic.EchoReturn);
            }
        }

        private static string BuildEchoPulseUsedMessage(int stunnedCount, int scoutRevealCount, float charge01, bool insideSmoke)
        {
            int chargePercent = Mathf.RoundToInt(Mathf.Clamp01(charge01) * 100f);
            string smokeCue = insideSmoke ? "연막: 짧게 보고 조용히, " : string.Empty;
            if (chargePercent <= 0)
            {
                return $"메아리 사용 ({smokeCue}기절 {Mathf.Max(0, stunnedCount)}, 정찰 {Mathf.Max(0, scoutRevealCount)})";
            }

            return $"메아리 과충전 {chargePercent}% ({smokeCue}기절 {Mathf.Max(0, stunnedCount)}, 정찰 {Mathf.Max(0, scoutRevealCount)})";
        }

        private static string BuildEchoReturnThreatMessage(float distance)
        {
            return $"메아리 응답 - 위협 {Mathf.Max(0f, distance):0.0}m";
        }

        private bool TryFindEchoReturnThreat(Vector2 origin, float effectiveStunRadius, out EnemyController selectedEnemy, out float selectedDistance, out int threatCount)
        {
            selectedEnemy = null;
            selectedDistance = 0f;
            threatCount = 0;

            float returnRadius = Mathf.Max(0.5f, effectiveStunRadius * echoReturnThreatRadiusMultiplier);
            float bestDistanceSqr = returnRadius * returnRadius;
            float radiusSqr = bestDistanceSqr;

            EnemyController.CopyActiveControllers(cachedEnemies);
            for (int i = 0; i < cachedEnemies.Count; i++)
            {
                EnemyController enemy = cachedEnemies[i];
                if (enemy == null || !enemy.isActiveAndEnabled || enemy.IsStunned)
                {
                    continue;
                }

                Vector2 enemyPosition = enemy.transform.position;
                float distanceSqr = (enemyPosition - origin).sqrMagnitude;
                if (distanceSqr > radiusSqr)
                {
                    continue;
                }

                threatCount++;
                if (distanceSqr <= bestDistanceSqr)
                {
                    selectedEnemy = enemy;
                    bestDistanceSqr = distanceSqr;
                }
            }

            if (selectedEnemy == null)
            {
                return false;
            }

            selectedDistance = Mathf.Sqrt(bestDistanceSqr);
            return true;
        }

        private void SpawnEchoReturnHintLine(Vector2 origin, Vector2 hintEnd, float intensity)
        {
            GameObject hintObject = new($"EchoReturnHint_{Time.frameCount}");
            RegisterEchoVisual(hintObject);
            Transform vfxRoot = EnsureScenePath("Scene_Root/GameRoot/Runtime/VFX");
            if (vfxRoot != null)
            {
                hintObject.transform.SetParent(vfxRoot, false);
            }

            LineRenderer line = hintObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = false;
            line.positionCount = 3;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.widthMultiplier = Mathf.Max(0.01f, echoReturnHintWidth);
            line.sharedMaterial = GetEchoReturnLineMaterial();
            line.sortingOrder = echoReturnHintSortingOrder;

            Vector3[] points = BuildEchoReturnHintPoints(origin, hintEnd, intensity);
            for (int i = 0; i < points.Length; i++)
            {
                line.SetPosition(i, points[i]);
            }

            StartCoroutine(EchoReturnHintRoutine(hintObject, line, points, Mathf.Clamp01(intensity)));
        }

        private Vector3[] BuildEchoReturnHintPoints(Vector2 origin, Vector2 hintEnd, float intensity)
        {
            Vector2 direction = hintEnd - origin;
            Vector2 side = direction.sqrMagnitude > 0.001f
                ? new Vector2(-direction.y, direction.x).normalized
                : Vector2.up;
            float waver = Mathf.Max(0f, echoReturnHintWaver) * Mathf.Lerp(0.45f, 1f, Mathf.Clamp01(intensity));
            return new[]
            {
                new Vector3(origin.x, origin.y, 0f),
                (Vector3)(Vector2.Lerp(origin, hintEnd, 0.62f) + side * waver),
                new Vector3(hintEnd.x, hintEnd.y, 0f)
            };
        }

        private IEnumerator EchoReturnHintRoutine(GameObject hintObject, LineRenderer line, Vector3[] basePoints, float intensity)
        {
            float duration = Mathf.Max(0.1f, echoReturnHintDuration);
            float startedAt = Time.time;
            Vector3 direction = basePoints[^1] - basePoints[0];
            Vector3 side = direction.sqrMagnitude > 0.001f
                ? new Vector3(-direction.y, direction.x, 0f).normalized
                : Vector3.up;

            while (line != null && Time.time < startedAt + duration)
            {
                float elapsed = Time.time - startedAt;
                float t = Mathf.Clamp01(elapsed / duration);
                float fade = 1f - Mathf.SmoothStep(0.08f, 1f, t);
                float flicker = 0.68f + Mathf.Sin((elapsed * 10.5f + intensity * 2.1f) * Mathf.PI * 2f) * 0.32f;

                for (int i = 0; i < basePoints.Length; i++)
                {
                    Vector3 point = basePoints[i];
                    if (i == 1)
                    {
                        point += side * Mathf.Sin(elapsed * Mathf.PI * 2f * 3.4f) * echoReturnHintWaver * 0.22f * fade;
                    }

                    line.SetPosition(i, point);
                }

                Color color = echoReturnThreatColor;
                color.a *= fade * Mathf.Clamp01(flicker) * Mathf.Lerp(0.75f, 1.15f, intensity);
                line.startColor = color;
                line.endColor = color;
                line.widthMultiplier = Mathf.Max(0.01f, echoReturnHintWidth) * Mathf.Lerp(1.2f, 0.22f, t);
                yield return null;
            }

            if (hintObject != null)
            {
                Destroy(hintObject);
            }
        }

        private void SpawnEchoReturnHintPulse(Vector2 position, float intensity)
        {
            if (!spawnPulseVisual)
            {
                return;
            }

            GameObject pulseObject = new($"EchoReturnPulse_{Time.frameCount}");
            RegisterEchoVisual(pulseObject);
            Transform vfxRoot = EnsureScenePath("Scene_Root/GameRoot/Runtime/VFX");
            if (vfxRoot != null)
            {
                pulseObject.transform.SetParent(vfxRoot, false);
            }

            pulseObject.transform.position = new Vector3(position.x, position.y, 0f);
            EchoPulseVisualDummy visual = pulseObject.AddComponent<EchoPulseVisualDummy>();
            float alphaScale = Mathf.Lerp(0.74f, 1.12f, Mathf.Clamp01(intensity));
            float radius = Mathf.Lerp(0.72f, 1.18f, Mathf.Clamp01(intensity));
            float duration = Mathf.Max(0.1f, echoReturnHintDuration * 0.9f);
            Sprite threatPulseSprite = MapReadableArt.TryGetEchoReturnThreatPulseSprite();
            Color color;
            if (threatPulseSprite != null)
            {
                // Painted warning-red ember flare - white RGB so echoReturnThreatColor does not double-tint.
                color = Color.white;
                color.a = echoReturnThreatColor.a * alphaScale;
                visual.Configure(
                    radius,
                    color,
                    duration,
                    1,
                    0f,
                    echoReturnHintSortingOrder,
                    threatPulseSprite);
            }
            else
            {
                color = echoReturnThreatColor;
                color.a *= alphaScale;
                visual.Configure(
                    radius,
                    color,
                    duration,
                    1,
                    0f,
                    echoReturnHintSortingOrder);
            }
        }

        private Material GetEchoReturnLineMaterial()
        {
            if (echoReturnLineMaterial != null)
            {
                return echoReturnLineMaterial;
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

            echoReturnLineMaterial = new Material(shader)
            {
                name = "EchoReturnLineMaterial",
                hideFlags = HideFlags.HideAndDontSave
            };
            return echoReturnLineMaterial;
        }

        private void ApplyEchoFogReveal(Vector2 origin, float effectiveStunRadius, float revealRadiusMultiplier)
        {
            if (!revealFogWithPulse)
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

            float revealRadius = Mathf.Max(0.1f, effectiveStunRadius * fogRevealRadiusMultiplier * Mathf.Max(0.1f, revealRadiusMultiplier));
            fogSystem.ApplyEchoRevealPulse(origin, revealRadius, fogRevealSoftnessBoost);
        }

        private int RevealNearbyScoutTargets(Vector2 origin, float effectiveStunRadius, float revealRadiusMultiplier)
        {
            if (!revealNearbyTargetsWithPulse)
            {
                return 0;
            }

            float radius = Mathf.Max(0.1f, effectiveStunRadius * scoutRadiusMultiplier * Mathf.Max(0.1f, revealRadiusMultiplier));
            int maxTargets = Mathf.Clamp(maxScoutRevealTargets, 1, 32);
            int revealedCount = 0;

            BreadcrumbPickup.CopyActivePickups(cachedBreadcrumbs);
            revealedCount += RevealScoutTargets(cachedBreadcrumbs, origin, radius, maxTargets - revealedCount, scoutBreadcrumbColor);
            if (revealedCount >= maxTargets)
            {
                return revealedCount;
            }

            ExitPortalDummy.CopyActivePortals(cachedExits);
            revealedCount += RevealScoutTargets(cachedExits, origin, radius, maxTargets - revealedCount, scoutExitColor);
            if (revealedCount >= maxTargets)
            {
                return revealedCount;
            }

            RoomArchetypeHookDummy.CopyActiveHooks(cachedHooks);
            revealedCount += RevealScoutTargets(cachedHooks, origin, radius, maxTargets - revealedCount, scoutHazardColor);
            if (revealedCount >= maxTargets)
            {
                return revealedCount;
            }

            EnemyController.CopyActiveControllers(cachedEnemies);
            for (int i = 0; i < cachedEnemies.Count && revealedCount < maxTargets; i++)
            {
                EnemyController enemy = cachedEnemies[i];
                if (enemy == null || !enemy.isActiveAndEnabled)
                {
                    continue;
                }

                if (TryRevealScoutTarget(enemy.gameObject, origin, radius, scoutEnemyColor))
                {
                    revealedCount++;
                }
            }

            return revealedCount;
        }

        private int RevealScoutTargets<T>(List<T> targets, Vector2 origin, float radius, int remainingBudget, Color color)
            where T : Component
        {
            if (remainingBudget <= 0 || targets == null)
            {
                return 0;
            }

            int revealed = 0;
            for (int i = 0; i < targets.Count && revealed < remainingBudget; i++)
            {
                T target = targets[i];
                if (target == null)
                {
                    continue;
                }

                if (TryRevealScoutTarget(target.gameObject, origin, radius, color))
                {
                    revealed++;
                }
            }

            return revealed;
        }

        private bool TryRevealScoutTarget(GameObject target, Vector2 origin, float radius, Color scoutColor)
        {
            if (target == null || target == gameObject || !target.activeInHierarchy)
            {
                return false;
            }

            Vector2 targetPosition = target.transform.position;
            float radiusSqr = radius * radius;
            if ((targetPosition - origin).sqrMagnitude > radiusSqr)
            {
                return false;
            }

            if (!IsScoutTargetHiddenByFog(targetPosition))
            {
                return false;
            }

            SpriteRenderer[] renderers = target.GetComponentsInChildren<SpriteRenderer>(false);
            if (renderers == null || renderers.Length <= 0)
            {
                return false;
            }

            StartCoroutine(RevealScoutTargetRoutine(renderers, scoutColor, scoutRevealDuration));
            return true;
        }

        private bool IsScoutTargetHiddenByFog(Vector2 targetPosition)
        {
            if (fogSystem == null)
            {
                fogSystem = FindFirstObjectByType<FogOfWarSystem>();
            }

            if (fogSystem == null || !fogSystem.isActiveAndEnabled)
            {
                return true;
            }

            return fogSystem.SampleFogAlpha01AtWorldPosition(targetPosition) >= scoutHiddenFogThreshold;
        }

        private static IEnumerator RevealScoutTargetRoutine(SpriteRenderer[] renderers, Color scoutColor, float duration)
        {
            float safeDuration = Mathf.Max(0.1f, duration);
            Color[] originalColors = new Color[renderers.Length];
            Vector3[] originalScales = new Vector3[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                originalColors[i] = renderer.color;
                originalScales[i] = renderer.transform.localScale;
            }

            float startedAt = Time.time;
            while (Time.time < startedAt + safeDuration)
            {
                float t = Mathf.Clamp01((Time.time - startedAt) / safeDuration);
                float pulse = 0.5f + Mathf.Sin(t * Mathf.PI * 4f) * 0.5f;
                float blend = Mathf.Lerp(0.25f, 0.9f, pulse) * (1f - t * 0.2f);
                float scale = Mathf.Lerp(1f, 1.18f, pulse) * Mathf.Lerp(1f, 0.96f, t);

                for (int i = 0; i < renderers.Length; i++)
                {
                    SpriteRenderer renderer = renderers[i];
                    if (renderer == null)
                    {
                        continue;
                    }

                    Color color = Color.Lerp(originalColors[i], scoutColor, blend);
                    color.a = Mathf.Max(originalColors[i].a, scoutColor.a * Mathf.Lerp(0.8f, 1f, pulse));
                    renderer.color = color;
                    renderer.transform.localScale = originalScales[i] * scale;
                }

                yield return null;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.color = originalColors[i];
                renderer.transform.localScale = originalScales[i];
            }
        }

        private void EmitRiskNoise(float overchargeNoiseMultiplier)
        {
            if (NoiseManager.Instance == null)
            {
                return;
            }

            float concealNoiseScale = concealmentState != null ? concealmentState.CurrentNoiseMultiplier : 1f;
            float smokeNoiseScale = SmokeScreenFieldDummy.EvaluateNoiseMultiplierAt(transform.position);
            float noiseScale = concealNoiseScale * smokeNoiseScale * runtimeNoiseMultiplier * Mathf.Max(1f, overchargeNoiseMultiplier);

            lastNoiseScale = noiseScale;

            float scaledLoudness = noiseLoudness * noiseScale;
            float scaledRadius = noiseRadius * Mathf.Lerp(0.72f, 1f, Mathf.Clamp01(noiseScale));

            NoiseManager.Instance.EmitNoise(
                transform.position,
                Mathf.Max(0.1f, scaledLoudness),
                Mathf.Max(0.3f, scaledRadius),
                NoiseKind.Echo,
                gameObject);
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

        public EchoOverchargePreview EvaluateOverchargePreview(float charge01)
        {
            return EvaluateOverchargePreview(charge01, insideSmoke: false);
        }

        public EchoOverchargePreview EvaluateOverchargePreview(float charge01, bool insideSmoke)
        {
            float safeCharge = Mathf.Clamp01(charge01);
            int extraPulses = Mathf.RoundToInt(overchargeExtraResonancePulses * safeCharge);
            int pulseCount = Mathf.Clamp(resonanceTailPulseCount + extraPulses, 1, 5);
            float revealMultiplier = Mathf.Lerp(1f, OverchargeRevealRadiusMultiplier, safeCharge);
            if (insideSmoke)
            {
                revealMultiplier *= SmokeRevealRadiusMultiplier;
            }

            return new EchoOverchargePreview(
                safeCharge,
                revealMultiplier,
                Mathf.Lerp(1f, OverchargeNoiseMultiplier, safeCharge),
                pulseCount,
                EffectiveStunRadius,
                stunDurationSeconds);
        }

        public bool EvaluateInsideSmoke()
        {
            return SmokeScreenFieldDummy.EvaluateNoiseDampenAt(transform.position) > 0.001f;
        }

        private void TickOverchargeInput()
        {
            if (!isCharging)
            {
                if (!IsReady || !RuntimeInputAdapter.GetKeyDown(pulseKey))
                {
                    return;
                }

                BeginCharge();
            }

            if (!isCharging)
            {
                return;
            }

            if (!IsReady)
            {
                CancelCharge(clearPreview: true);
                return;
            }

            float heldSeconds = Mathf.Max(0f, Time.time - chargeHoldStartedAt);
            currentCharge01 = heldSeconds <= tapGraceSeconds
                ? 0f
                : Mathf.Clamp01((heldSeconds - tapGraceSeconds) / Mathf.Max(0.15f, chargeBuildSeconds));
            UpdateChargePreviewRings();
            TryDensifyHeldTrail();

            if (currentCharge01 >= 0.999f)
            {
                TryCastPulse(1f, autoFullCharge: true);
                return;
            }

            if (RuntimeInputAdapter.GetKeyUp(pulseKey) || !RuntimeInputAdapter.GetKey(pulseKey))
            {
                TryCastPulse(currentCharge01, autoFullCharge: false);
            }
        }

        private void BeginCharge()
        {
            isCharging = true;
            chargeHoldStartedAt = Time.time;
            currentCharge01 = 0f;
            UpdateChargePreviewRings();
        }

        private void CancelCharge(bool clearPreview)
        {
            isCharging = false;
            currentCharge01 = 0f;
            chargeHoldStartedAt = 0f;
            if (clearPreview)
            {
                DestroyChargePreviewRings();
            }
        }

        private void TryDensifyHeldTrail()
        {
            if (currentCharge01 <= 0.001f)
            {
                return;
            }

            EchoOverchargePreview preview = EvaluateOverchargePreview(currentCharge01, EvaluateInsideSmoke());
            float radius = Mathf.Max(0.45f, EffectiveStunRadius * scoutRadiusMultiplier * preview.RevealRadiusMultiplier);
            BreadcrumbPickup.TryDensifyNear(transform.position, radius);
        }

        private void UpdateChargePreviewRings()
        {
            if (!spawnChargePreviewRings || !isCharging)
            {
                DestroyChargePreviewRings();
                return;
            }

            EnsureChargePreviewRings();
            if (chargePreviewRoot == null || chargePreviewRings == null)
            {
                return;
            }

            chargePreviewRoot.position = new Vector3(transform.position.x, transform.position.y, 0f);
            Color color = EvaluateOverchargeRingColor(currentCharge01);
            float revealScale = EvaluateOverchargePreview(currentCharge01, EvaluateInsideSmoke()).RevealRadiusMultiplier;
            float previewRadius = Mathf.Max(0.45f, EffectiveStunRadius * pulseVisualRadiusMultiplier * revealScale);
            float pulse = 0.86f + Mathf.Sin(Time.unscaledTime * 7.4f) * 0.08f;
            for (int i = 0; i < chargePreviewRings.Length; i++)
            {
                SpriteRenderer ring = chargePreviewRings[i];
                if (ring == null)
                {
                    continue;
                }

                float ringScale = previewRadius * Mathf.Lerp(0.42f, 1f, (i + 1f) / chargePreviewRings.Length) * pulse;
                ring.transform.localScale = new Vector3(ringScale * 2f, ringScale * 2f, 1f);
                Color ringColor = color;
                ringColor.a *= Mathf.Lerp(0.42f, 0.92f, currentCharge01) * (1f - i * 0.18f);
                ring.color = ringColor;
                ring.enabled = true;
            }
        }

        private void EnsureChargePreviewRings()
        {
            if (chargePreviewRoot != null && chargePreviewRings != null && chargePreviewRings.Length >= 2)
            {
                return;
            }

            DestroyChargePreviewRings();
            GameObject previewObject = new("EchoOverchargePreview");
            Transform vfxRoot = EnsureScenePath("Scene_Root/GameRoot/Runtime/VFX");
            if (vfxRoot != null)
            {
                previewObject.transform.SetParent(vfxRoot, false);
            }

            previewObject.transform.position = new Vector3(transform.position.x, transform.position.y, 0f);
            chargePreviewRoot = previewObject.transform;
            chargePreviewRings = new SpriteRenderer[2];
            Sprite ringSprite = EchoPulseVisualDummy.SharedRingSprite;
            for (int i = 0; i < chargePreviewRings.Length; i++)
            {
                GameObject ringObject = new($"ChargeRing_{i:00}");
                ringObject.transform.SetParent(chargePreviewRoot, false);
                SpriteRenderer renderer = ringObject.AddComponent<SpriteRenderer>();
                renderer.sprite = ringSprite;
                renderer.sortingOrder = pulseVisualSortingOrder + 1 - i;
                renderer.enabled = false;
                chargePreviewRings[i] = renderer;
            }
        }

        private void DestroyChargePreviewRings()
        {
            if (chargePreviewRoot != null)
            {
                Destroy(chargePreviewRoot.gameObject);
            }

            chargePreviewRoot = null;
            chargePreviewRings = null;
        }

        private Color EvaluateOverchargeRingColor(float charge01)
        {
            return Color.Lerp(pulseVisualColor, overchargeWarningColor, Mathf.Clamp01(charge01));
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.95f, 1f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, EffectiveStunRadius);
        }
    }

    public readonly struct EchoOverchargePreview
    {
        public EchoOverchargePreview(
            float charge01,
            float revealRadiusMultiplier,
            float noiseMultiplier,
            int resonancePulseCount,
            float stunRadius,
            float stunDurationSeconds)
        {
            Charge01 = Mathf.Clamp01(charge01);
            RevealRadiusMultiplier = Mathf.Max(0.1f, revealRadiusMultiplier);
            NoiseMultiplier = Mathf.Max(1f, noiseMultiplier);
            ResonancePulseCount = Mathf.Clamp(resonancePulseCount, 1, 5);
            StunRadius = Mathf.Max(0.1f, stunRadius);
            StunDurationSeconds = Mathf.Max(0.1f, stunDurationSeconds);
        }

        public float Charge01 { get; }
        public float RevealRadiusMultiplier { get; }
        public float NoiseMultiplier { get; }
        public int ResonancePulseCount { get; }
        public float StunRadius { get; }
        public float StunDurationSeconds { get; }
    }
}
