using System;
using LostBreadcrumbs.Runtime.Managers;
using LostBreadcrumbs.Runtime.Player;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Map
{
    public sealed class StaminaPickup : MonoBehaviour
    {
        public event Action<StaminaPickup> Collected;

        [SerializeField, Min(0.1f)] private float staminaRecoverAmount = 1.2f;
        [SerializeField, Min(0f)] private float pickupNoiseLoudness = 0.4f;
        [SerializeField, Min(0f)] private float pickupNoiseRadius = 2.8f;
        [SerializeField] private float pulseSpeed = 2.8f;
        [SerializeField] private float pulseScale = 0.07f;

        private Vector3 initialScale;

        public float StaminaRecoverAmount => staminaRecoverAmount;

        private void Awake()
        {
            initialScale = transform.localScale;
        }

        private void Update()
        {
            float wave = Mathf.Sin(Time.time * pulseSpeed) * pulseScale;
            transform.localScale = initialScale * (1f + wave);
        }

        public void Configure(float recoverAmount)
        {
            staminaRecoverAmount = Mathf.Max(0.1f, recoverAmount);
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

            player.RecoverStamina(staminaRecoverAmount);
            player.GetComponent<PlayerBehaviorTelemetry>()?.RegisterStaminaPickup();
            EmitPickupNoise();
            Collected?.Invoke(this);
            Destroy(gameObject);
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




