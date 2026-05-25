using System;
using LostBreadcrumbs.Runtime.Managers;
using LostBreadcrumbs.Runtime.Player;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Map
{
    public sealed class RiskCachePickup : MonoBehaviour
    {
        public event Action<RiskCachePickup> Collected;

        [SerializeField, Min(0.1f)] private float staminaRecoverAmount = 0.75f;
        [SerializeField, Min(0f)] private float pulseCooldownRefundSeconds = 1.5f;
        [SerializeField, Min(0f)] private float pickupNoiseLoudness = 1.15f;
        [SerializeField, Min(0f)] private float pickupNoiseRadius = 6.6f;
        [SerializeField] private float pulseSpeed = 3.4f;
        [SerializeField] private float pulseScale = 0.1f;
        [SerializeField] private GameplayRhythmDirector rhythmDirector;

        [Header("Rhythm Wager")]
        [SerializeField, Range(0.25f, 2.5f)] private float calmRewardMultiplier = 0.92f;
        [SerializeField, Range(0.25f, 2.5f)] private float buildRewardMultiplier = 1.32f;
        [SerializeField, Range(0.25f, 2.5f)] private float spikeRewardMultiplier = 1.55f;
        [SerializeField, Range(0.25f, 2.5f)] private float releaseRewardMultiplier = 0.78f;
        [SerializeField, Range(0.25f, 2.5f)] private float calmNoiseMultiplier = 0.82f;
        [SerializeField, Range(0.25f, 2.5f)] private float buildNoiseMultiplier = 1.08f;
        [SerializeField, Range(0.25f, 2.5f)] private float spikeNoiseMultiplier = 1.48f;
        [SerializeField, Range(0.25f, 2.5f)] private float releaseNoiseMultiplier = 0.68f;

        private Vector3 initialScale;

        public float LastRecoveredStamina { get; private set; }
        public float LastPulseCooldownRefund { get; private set; }
        public float LastRewardMultiplier { get; private set; } = 1f;
        public float LastNoiseMultiplier { get; private set; } = 1f;
        public string LastRhythmPhaseLabel { get; private set; } = "None";

        private void Awake()
        {
            initialScale = transform.localScale;
        }

        private void Update()
        {
            float wave = Mathf.Sin(Time.time * pulseSpeed) * pulseScale;
            transform.localScale = initialScale * (1f + wave);
        }

        public void Configure(
            float recoverAmount,
            float cooldownRefundSeconds,
            float noiseLoudness,
            float noiseRadius)
        {
            staminaRecoverAmount = Mathf.Max(0.1f, recoverAmount);
            pulseCooldownRefundSeconds = Mathf.Max(0f, cooldownRefundSeconds);
            pickupNoiseLoudness = Mathf.Max(0f, noiseLoudness);
            pickupNoiseRadius = Mathf.Max(0f, noiseRadius);
        }

        public void ConfigureRhythmWager(
            GameplayRhythmDirector rhythm,
            float calmReward,
            float buildReward,
            float spikeReward,
            float releaseReward,
            float calmNoise,
            float buildNoise,
            float spikeNoise,
            float releaseNoise)
        {
            rhythmDirector = rhythm;
            calmRewardMultiplier = Mathf.Clamp(calmReward, 0.25f, 2.5f);
            buildRewardMultiplier = Mathf.Clamp(buildReward, 0.25f, 2.5f);
            spikeRewardMultiplier = Mathf.Clamp(spikeReward, 0.25f, 2.5f);
            releaseRewardMultiplier = Mathf.Clamp(releaseReward, 0.25f, 2.5f);
            calmNoiseMultiplier = Mathf.Clamp(calmNoise, 0.25f, 2.5f);
            buildNoiseMultiplier = Mathf.Clamp(buildNoise, 0.25f, 2.5f);
            spikeNoiseMultiplier = Mathf.Clamp(spikeNoise, 0.25f, 2.5f);
            releaseNoiseMultiplier = Mathf.Clamp(releaseNoise, 0.25f, 2.5f);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (RegressionChecklistRunner.IsRegressionRunActive)
            {
                return;
            }

            if (!other.CompareTag("Player"))
            {
                return;
            }

            PlayerDummyController player = other.GetComponentInParent<PlayerDummyController>();
            if (player == null)
            {
                return;
            }

            GameplayRhythmPhase phase = rhythmDirector != null ? rhythmDirector.CurrentPhase : GameplayRhythmPhase.Calm;
            LastRhythmPhaseLabel = rhythmDirector != null ? rhythmDirector.CurrentPhaseLabel : "None";
            LastRewardMultiplier = EvaluateRewardMultiplier(phase);
            LastNoiseMultiplier = EvaluateNoiseMultiplier(phase);
            LastRecoveredStamina = player.RecoverStamina(staminaRecoverAmount * LastRewardMultiplier);
            LastPulseCooldownRefund = RefundPulseCooldown(player, LastRewardMultiplier);
            EmitPickupNoise();
            Collected?.Invoke(this);
            Destroy(gameObject);
        }

        private float RefundPulseCooldown(PlayerDummyController player, float rewardMultiplier)
        {
            if (pulseCooldownRefundSeconds <= 0f || player == null)
            {
                return 0f;
            }

            PlayerEchoPulseAbility pulse = player.GetComponent<PlayerEchoPulseAbility>();
            if (pulse == null)
            {
                return 0f;
            }

            float before = pulse.CooldownRemaining;
            float after = Mathf.Max(0f, before - pulseCooldownRefundSeconds * Mathf.Max(0f, rewardMultiplier));
            pulse.SetCooldownRemainingForRuntime(after);
            return Mathf.Max(0f, before - after);
        }

        private void EmitPickupNoise()
        {
            if (pickupNoiseLoudness <= 0f || pickupNoiseRadius <= 0f || NoiseManager.Instance == null)
            {
                return;
            }

            NoiseManager.Instance.EmitNoise(
                transform.position,
                pickupNoiseLoudness * LastNoiseMultiplier,
                pickupNoiseRadius * Mathf.Lerp(0.82f, 1.18f, Mathf.Clamp01(LastNoiseMultiplier - 0.25f)),
                NoiseKind.ItemUse,
                gameObject);
        }

        private float EvaluateRewardMultiplier(GameplayRhythmPhase phase)
        {
            return phase switch
            {
                GameplayRhythmPhase.Build => buildRewardMultiplier,
                GameplayRhythmPhase.Spike => spikeRewardMultiplier,
                GameplayRhythmPhase.Release => releaseRewardMultiplier,
                _ => calmRewardMultiplier
            };
        }

        private float EvaluateNoiseMultiplier(GameplayRhythmPhase phase)
        {
            return phase switch
            {
                GameplayRhythmPhase.Build => buildNoiseMultiplier,
                GameplayRhythmPhase.Spike => spikeNoiseMultiplier,
                GameplayRhythmPhase.Release => releaseNoiseMultiplier,
                _ => calmNoiseMultiplier
            };
        }
    }
}
