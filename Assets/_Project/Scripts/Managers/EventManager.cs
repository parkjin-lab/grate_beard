using System.Collections.Generic;
using LostBreadcrumbs.Runtime.Events;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Managers
{
    [DefaultExecutionOrder(-250)]
    public sealed class EventManager : ManagerBase
    {
        public static EventManager Instance { get; private set; }

        [Header("Runtime Event Log")]
        [SerializeField, Min(5)] private int maxRecentEvents = 40;
        [SerializeField] private bool logEventsToConsole;

        private readonly List<RuntimeEventRecord> recentEvents = new();

        public IReadOnlyList<RuntimeEventRecord> RecentEvents => recentEvents;

        protected override void Awake()
        {
            base.Awake();

            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnEnable()
        {
            RuntimeEventBus.EventRaised -= HandleRuntimeEvent;
            RuntimeEventBus.EventRaised += HandleRuntimeEvent;
        }

        private void OnDisable()
        {
            RuntimeEventBus.EventRaised -= HandleRuntimeEvent;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public bool TryGetLatestEvent(out RuntimeEventRecord record)
        {
            if (recentEvents.Count <= 0)
            {
                record = default;
                return false;
            }

            record = recentEvents[recentEvents.Count - 1];
            return true;
        }

        private void HandleRuntimeEvent(RuntimeEventRecord record)
        {
            if (!record.IsValid)
            {
                return;
            }

            recentEvents.Add(record);
            TrimBuffer();

            if (logEventsToConsole)
            {
                Debug.Log($"[Event] {record.TimeLabel} [{record.TypeLabel}] {record.Message}", this);
            }
        }

        private void TrimBuffer()
        {
            int safeMax = Mathf.Max(5, maxRecentEvents);
            int overflow = recentEvents.Count - safeMax;
            if (overflow <= 0)
            {
                return;
            }

            recentEvents.RemoveRange(0, overflow);
        }
    }
}