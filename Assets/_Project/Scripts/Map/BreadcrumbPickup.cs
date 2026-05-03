using System;
using LostBreadcrumbs.Runtime.Managers;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Map
{
    public sealed class BreadcrumbPickup : MonoBehaviour
    {
        public event Action<BreadcrumbPickup> Collected;

        [SerializeField] private int value = 1;
        [SerializeField] private float pulseSpeed = 3f;
        [SerializeField] private float pulseScale = 0.08f;

        private Vector3 initialScale;

        public int Value => value;

        private void Awake()
        {
            initialScale = transform.localScale;
        }

        private void Update()
        {
            float wave = Mathf.Sin(Time.time * pulseSpeed) * pulseScale;
            transform.localScale = initialScale * (1f + wave);
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

            Collected?.Invoke(this);
            Destroy(gameObject);
        }
    }
}



