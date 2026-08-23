using System;
using System.Collections.Generic;
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

        private static readonly List<BreadcrumbPickup> activePickups = new(16);

        public static void CopyActivePickups(List<BreadcrumbPickup> output)
        {
            CopyActive(activePickups, output);
        }

        private static void CopyActive<T>(List<T> source, List<T> output) where T : Behaviour
        {
            if (output == null)
            {
                return;
            }

            output.Clear();
            for (int i = source.Count - 1; i >= 0; i--)
            {
                T item = source[i];
                if (item == null)
                {
                    source.RemoveAt(i);
                    continue;
                }

                if (!item.isActiveAndEnabled)
                {
                    continue;
                }

                output.Add(item);
            }
        }

        private void Awake()
        {
            initialScale = transform.localScale;
        }

        private void OnEnable()
        {
            if (!activePickups.Contains(this))
            {
                activePickups.Add(this);
            }
        }

        private void OnDisable()
        {
            activePickups.Remove(this);
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



