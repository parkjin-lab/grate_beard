using LostBreadcrumbs.Runtime.Map;
using LostBreadcrumbs.Runtime.Events;
using LostBreadcrumbs.Runtime.Managers;
using LostBreadcrumbs.Runtime.Systems;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Player
{
    public sealed class PlayerVitalSystem : MonoBehaviour
    {
        [Header("Vitals")]
        [SerializeField, Min(1)] private int maxHealth = 3;
        [SerializeField, Min(0f)] private float invulnerableSecondsAfterHit = 1f;
        [SerializeField] private bool regenerateCurrentStageOnDeath = true;

        [Header("Recovery")]
        [SerializeField] private bool healOnStageAdvance = true;
        [SerializeField] private MapSystem mapSystem;

        [Header("Safe Haven Sustain")]
        [SerializeField] private bool healWhileInsideSafeHaven = true;
        [SerializeField, Min(0.1f)] private float safeHavenHealInterval = 1.25f;
        [SerializeField, Min(1)] private int safeHavenHealPerTick = 1;

        [Header("Respawn Reset")]
        [SerializeField] private bool resetFlashlightOnDeath = true;
        [SerializeField] private bool resetFogOnDeath = true;
        [SerializeField] private bool resetPulseCooldownOnDeath = true;
        [SerializeField] private bool resetDecoyCooldownOnDeath = true;
        [SerializeField] private bool resetSmokeCooldownOnDeath = true;
        [SerializeField] private bool resetSprintStateOnDeath = true;
        [SerializeField, Min(0f)] private float respawnInvulnerableSeconds = 1.25f;

        private int currentHealth;
        private int deathCount;
        private float invulnerableUntil;

        private bool hasCheckpoint;
        private bool pendingDeathRespawnReset;
        private Vector3 checkpointWorldPosition;
        private int lastKnownStage = 1;
        private float nextSafeHavenHealTime;

        private PlayerVisibilitySource visibilitySource;
        private PlayerEchoPulseAbility pulseAbility;
        private PlayerDecoyAbility decoyAbility;
        private PlayerSmokeAbility smokeAbility;
        private PlayerConcealmentState concealmentState;
        private PlayerDummyController playerController;
        private PlayerBehaviorTelemetry behaviorTelemetry;
        private FogOfWarSystem fogSystem;
        private StagePressureDirector stagePressureDirector;

        private string lastDeathCause = "Unknown";
        private string lastDeathMissedOption = "None";
        private float lastDeathPressureSnapshot;
        private int lastDeathStage = 1;
        private float lastDeathRealtime;
        private Vector2 lastDamageSource;
        private bool hasLastDamageSource;

        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;
        public int DeathCount => deathCount;
        public bool IsInvulnerable => Time.time < invulnerableUntil;
        public bool SafeHavenHealingEnabled => healWhileInsideSafeHaven;
        public float SafeHavenHealCooldownRemaining => Mathf.Max(0f, nextSafeHavenHealTime - Time.time);
        public string LastDeathCause => string.IsNullOrWhiteSpace(lastDeathCause) ? "Unknown" : lastDeathCause;
        public string LastDeathMissedOption => string.IsNullOrWhiteSpace(lastDeathMissedOption) ? "None" : lastDeathMissedOption;
        public float LastDeathPressureSnapshot => Mathf.Clamp01(lastDeathPressureSnapshot);
        public int LastDeathStage => Mathf.Max(1, lastDeathStage);
        public float LastDeathRealtime => Mathf.Max(0f, lastDeathRealtime);

        private void Awake()
        {
            maxHealth = Mathf.Max(1, maxHealth);
            currentHealth = maxHealth;
            ResolveReferences();
            nextSafeHavenHealTime = Time.time + safeHavenHealInterval;
        }

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeMapEvents();
        }

        private void OnDisable()
        {
            UnsubscribeMapEvents();
        }

        private void Start()
        {
            ResolveReferences();

            if (mapSystem != null && mapSystem.LastGeneratedCells.Count > 0)
            {
                UpdateCheckpointFromMap(mapSystem.CurrentStage, mapSystem.LastGeneratedCells);
            }
        }

        private void Update()
        {
            TickSafeHavenRecovery();
        }

        public void SetMapSystemForEditor(MapSystem targetMapSystem)
        {
            if (mapSystem == targetMapSystem)
            {
                return;
            }

            UnsubscribeMapEvents();
            mapSystem = targetMapSystem;
            SubscribeMapEvents();
        }

        public bool TryTakeDamage(int damage, Vector2 sourcePosition)
        {
            if (damage <= 0)
            {
                return false;
            }

            if (Time.time < invulnerableUntil)
            {
                return false;
            }

            lastDamageSource = sourcePosition;
            hasLastDamageSource = true;
            currentHealth = Mathf.Max(0, currentHealth - damage);
            invulnerableUntil = Time.time + invulnerableSecondsAfterHit;

            if (currentHealth <= 0)
            {
                HandleDeath(sourcePosition);
            }

            return true;
        }

        public void RestoreFullHealth()
        {
            currentHealth = maxHealth;
        }

        public void ApplySavedVitals(int health, int deaths)
        {
            currentHealth = Mathf.Clamp(health, 1, maxHealth);
            deathCount = Mathf.Max(0, deaths);
        }

        private void HandleDeath(Vector2 damageSourcePosition)
        {
            ResolveReferences();

            deathCount++;
            behaviorTelemetry?.RegisterDeath();
            int eventStage = mapSystem != null ? mapSystem.CurrentStage : Mathf.Max(1, lastKnownStage);
            lastDeathStage = eventStage;
            lastDeathPressureSnapshot = stagePressureDirector != null ? stagePressureDirector.CurrentPressure01 : 0f;
            lastDeathCause = EvaluateDeathCause(damageSourcePosition);
            lastDeathMissedOption = EvaluateMissedOption();
            lastDeathRealtime = Time.realtimeSinceStartup;

            RuntimeEventBus.Raise(
                RuntimeEventType.Death,
                $"Player died ({lastDeathCause}) | P {lastDeathPressureSnapshot:0.00} | Missed {lastDeathMissedOption} | Deaths {deathCount}",
                this,
                eventStage);
            RestoreFullHealth();
            pendingDeathRespawnReset = true;

            if (regenerateCurrentStageOnDeath && mapSystem != null)
            {
                mapSystem.RegenerateCurrentStageWithVariation();
                return;
            }

            MoveToCheckpoint();
            ApplyDeathRespawnReset();
            pendingDeathRespawnReset = false;
        }

        private void ResolveReferences()
        {
            if (mapSystem == null)
            {
                mapSystem = FindFirstObjectByType<MapSystem>();
            }

            if (visibilitySource == null)
            {
                visibilitySource = GetComponent<PlayerVisibilitySource>();
            }

            if (pulseAbility == null)
            {
                pulseAbility = GetComponent<PlayerEchoPulseAbility>();
            }

            if (decoyAbility == null)
            {
                decoyAbility = GetComponent<PlayerDecoyAbility>();
            }

            if (smokeAbility == null)
            {
                smokeAbility = GetComponent<PlayerSmokeAbility>();
            }

            if (concealmentState == null)
            {
                concealmentState = GetComponent<PlayerConcealmentState>();
            }

            if (playerController == null)
            {
                playerController = GetComponent<PlayerDummyController>();
            }

            if (behaviorTelemetry == null)
            {
                behaviorTelemetry = GetComponent<PlayerBehaviorTelemetry>();
            }

            if (fogSystem == null)
            {
                fogSystem = FindFirstObjectByType<FogOfWarSystem>();
            }

            if (stagePressureDirector == null)
            {
                stagePressureDirector = FindFirstObjectByType<StagePressureDirector>();
            }
        }

        private void SubscribeMapEvents()
        {
            if (mapSystem != null)
            {
                mapSystem.MapGenerated -= HandleMapGenerated;
                mapSystem.MapGenerated += HandleMapGenerated;
            }
        }

        private void UnsubscribeMapEvents()
        {
            if (mapSystem != null)
            {
                mapSystem.MapGenerated -= HandleMapGenerated;
            }
        }

        private void HandleMapGenerated(int stage, System.Collections.Generic.IReadOnlyList<GeneratedMapCell> cells)
        {
            bool stageAdvanced = stage > lastKnownStage;
            lastKnownStage = stage;

            UpdateCheckpointFromMap(stage, cells);
            MoveToCheckpoint();

            if (concealmentState != null)
            {
                concealmentState.ResetConcealment();
            }

            if (pendingDeathRespawnReset)
            {
                ApplyDeathRespawnReset();
                pendingDeathRespawnReset = false;
            }

            if (stageAdvanced && healOnStageAdvance)
            {
                RestoreFullHealth();
            }

            nextSafeHavenHealTime = Time.time + safeHavenHealInterval;
        }

        private void UpdateCheckpointFromMap(int stage, System.Collections.Generic.IReadOnlyList<GeneratedMapCell> cells)
        {
            if (cells == null || cells.Count == 0 || mapSystem == null)
            {
                return;
            }

            Vector2Int startCell = cells[0].position;
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].kind == MapCellKind.Start)
                {
                    startCell = cells[i].position;
                    break;
                }
            }

            float cellSize = mapSystem.CellSize;
            checkpointWorldPosition = new Vector3(startCell.x * cellSize, startCell.y * cellSize, transform.position.z);
            hasCheckpoint = true;
            lastKnownStage = Mathf.Max(lastKnownStage, stage);
        }

        private void TickSafeHavenRecovery()
        {
            if (!healWhileInsideSafeHaven || safeHavenHealPerTick <= 0)
            {
                return;
            }

            if (concealmentState == null)
            {
                concealmentState = GetComponent<PlayerConcealmentState>();
                if (concealmentState == null)
                {
                    return;
                }
            }

            if (!concealmentState.IsInsideSafeHaven)
            {
                if (nextSafeHavenHealTime < Time.time)
                {
                    nextSafeHavenHealTime = Time.time + safeHavenHealInterval;
                }

                return;
            }

            if (currentHealth >= maxHealth)
            {
                nextSafeHavenHealTime = Time.time + safeHavenHealInterval;
                return;
            }

            if (Time.time < nextSafeHavenHealTime)
            {
                return;
            }

            currentHealth = Mathf.Min(maxHealth, currentHealth + safeHavenHealPerTick);
            nextSafeHavenHealTime = Time.time + safeHavenHealInterval;
        }

        private void ApplyDeathRespawnReset()
        {
            ResolveReferences();

            if (concealmentState != null)
            {
                concealmentState.ResetConcealment();
            }

            if (resetFlashlightOnDeath && visibilitySource != null)
            {
                visibilitySource.ResetFlashlightState();
            }

            if (resetPulseCooldownOnDeath && pulseAbility != null)
            {
                pulseAbility.ResetCooldown();
            }

            if (resetDecoyCooldownOnDeath && decoyAbility != null)
            {
                decoyAbility.ResetAbilityState(clearActiveDecoys: true);
            }

            if (resetSmokeCooldownOnDeath && smokeAbility != null)
            {
                smokeAbility.ResetAbilityState(clearActiveSmokes: true);
            }

            if (resetSprintStateOnDeath && playerController != null)
            {
                playerController.ForceResetSprintState(refillStamina: true);
            }

            if (resetFogOnDeath)
            {
                if (mapSystem != null)
                {
                    mapSystem.ForceFogReset();
                }

                if (fogSystem == null)
                {
                    fogSystem = FindFirstObjectByType<FogOfWarSystem>();
                }

                if (fogSystem != null)
                {
                    fogSystem.ResetFogToHidden();
                }
            }

            if (respawnInvulnerableSeconds > 0f)
            {
                invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + respawnInvulnerableSeconds);
            }

            nextSafeHavenHealTime = Time.time + safeHavenHealInterval;
        }


        private string EvaluateDeathCause(Vector2 damageSourcePosition)
        {
            Vector2 playerPosition = transform.position;
            Vector2 source = hasLastDamageSource ? lastDamageSource : damageSourcePosition;
            if (float.IsNaN(source.x) || float.IsInfinity(source.x) || float.IsNaN(source.y) || float.IsInfinity(source.y))
            {
                return "Unknown impact";
            }

            float distance = Vector2.Distance(playerPosition, source);
            if (distance <= 0.8f)
            {
                return "Close contact";
            }

            if (distance <= 2.2f)
            {
                return $"Contact {distance:0.0}m";
            }

            return $"Ranged hit {distance:0.0}m";
        }

        private string EvaluateMissedOption()
        {
            if (smokeAbility != null && smokeAbility.IsReady)
            {
                return "Smoke (R)";
            }

            if (decoyAbility != null && decoyAbility.IsReady)
            {
                return "Decoy (E)";
            }

            if (pulseAbility != null && pulseAbility.IsReady)
            {
                return "Echo (Q)";
            }

            float nextCooldown = float.MaxValue;
            if (smokeAbility != null)
            {
                nextCooldown = Mathf.Min(nextCooldown, smokeAbility.CooldownRemaining);
            }

            if (decoyAbility != null)
            {
                nextCooldown = Mathf.Min(nextCooldown, decoyAbility.CooldownRemaining);
            }

            if (pulseAbility != null)
            {
                nextCooldown = Mathf.Min(nextCooldown, pulseAbility.CooldownRemaining);
            }

            if (nextCooldown < float.MaxValue)
            {
                return $"No ability ready ({nextCooldown:0.0}s)";
            }

            return "No tactical option";
        }
        private void MoveToCheckpoint()
        {
            if (!hasCheckpoint)
            {
                return;
            }

            transform.position = checkpointWorldPosition;

            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }
    }
}















