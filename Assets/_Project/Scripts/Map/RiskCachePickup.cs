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

        private Vector3 initialScale;

        public float LastRecoveredStamina { get; private set; }
        public float LastPulseCooldownRefund { get; private set; }

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

            LastRecoveredStamina = player.RecoverStamina(staminaRecoverAmount);
            LastPulseCooldownRefund = RefundPulseCooldown(player);
            EmitPickupNoise();
            Collected?.Invoke(this);
            Destroy(gameObject);
        }

        private float RefundPulseCooldown(PlayerDummyController player)
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
            float after = Mathf.Max(0f, before - pulseCooldownRefundSeconds);
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
                pickupNoiseLoudness,
                pickupNoiseRadius,
                NoiseKind.ItemUse,
                gameObject);
        }
    }
}
