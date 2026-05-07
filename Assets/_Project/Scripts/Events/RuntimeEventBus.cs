using System;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Events
{
    public enum RuntimeEventType
    {
        System,
        Save,
        Load,
        Run,
        Stage,
        Ability,
        Death,
        Objective
    }

    public enum RuntimeEventSemantic
    {
        None,
        ExitUnlocked,
        LockOnWarning,
        ChaseStarted,
        ChaseDisengaged,
        EscapeRelief,
        QuietBreathBroken,
        EchoReturn,
        EchoChoiceScan,
        RiskReward,
        SafeHavenThin,
        PressureWave,
        SetPieceShift,
        HauntedRoom
    }

    public readonly struct RuntimeEventRecord
    {
        public RuntimeEventRecord(
            RuntimeEventType type,
            string message,
            UnityEngine.Object source,
            int sequence,
            int stage,
            float realtimeSinceStartup,
            RuntimeEventSemantic semantic)
        {
            Type = type;
            Message = message ?? string.Empty;
            Source = source;
            Sequence = Mathf.Max(0, sequence);
            Stage = Mathf.Max(0, stage);
            RealtimeSinceStartup = Mathf.Max(0f, realtimeSinceStartup);
            Semantic = semantic;
        }

        public RuntimeEventType Type { get; }
        public string Message { get; }
        public UnityEngine.Object Source { get; }
        public int Sequence { get; }
        public int Stage { get; }
        public float RealtimeSinceStartup { get; }
        public RuntimeEventSemantic Semantic { get; }
        public bool HasStage => Stage > 0;
        public bool IsValid => !string.IsNullOrWhiteSpace(Message);

        public string TypeLabel => Type switch
        {
            RuntimeEventType.System => "System",
            RuntimeEventType.Save => "Save",
            RuntimeEventType.Load => "Load",
            RuntimeEventType.Run => "Run",
            RuntimeEventType.Stage => "Stage",
            RuntimeEventType.Ability => "Ability",
            RuntimeEventType.Death => "Death",
            RuntimeEventType.Objective => "Objective",
            _ => "Event"
        };

        public string TimeLabel
        {
            get
            {
                TimeSpan elapsed = TimeSpan.FromSeconds(RealtimeSinceStartup);
                return $"{(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}";
            }
        }
    }

    public static class RuntimeEventBus
    {
        public static event Action<RuntimeEventRecord> EventRaised;

        public static RuntimeEventRecord LastEvent { get; private set; }
        public static int Sequence { get; private set; }
        public static bool IsPublishingSuppressed => suppressionDepth > 0;
        public static int SuppressionDepth => suppressionDepth;

        private static int suppressionDepth;

        public static void PushSuppression()
        {
            suppressionDepth = Mathf.Max(0, suppressionDepth + 1);
        }

        public static void PopSuppression()
        {
            suppressionDepth = Mathf.Max(0, suppressionDepth - 1);
        }

        public static EventSuppressionScope CreateSuppressionScope()
        {
            PushSuppression();
            return new EventSuppressionScope(active: true);
        }

        public static RuntimeEventRecord Raise(
            RuntimeEventType type,
            string message,
            UnityEngine.Object source = null,
            int stage = 0,
            bool allowWhenSuppressed = false,
            RuntimeEventSemantic semantic = RuntimeEventSemantic.None)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return default;
            }

            if (IsPublishingSuppressed && !allowWhenSuppressed)
            {
                return default;
            }

            Sequence++;
            RuntimeEventRecord record = new(
                type,
                message.Trim(),
                source,
                Sequence,
                stage,
                Time.realtimeSinceStartup,
                semantic);

            LastEvent = record;
            EventRaised?.Invoke(record);
            return record;
        }

        public readonly struct EventSuppressionScope : IDisposable
        {
            private readonly bool active;

            internal EventSuppressionScope(bool active)
            {
                this.active = active;
            }

            public void Dispose()
            {
                if (!active)
                {
                    return;
                }

                PopSuppression();
            }
        }
    }
}
