using System.Collections.Generic;
using LostBreadcrumbs.Runtime.Core.Input;
using LostBreadcrumbs.Runtime.Events;
using UnityEngine;
using UnityEngine.Audio;

namespace LostBreadcrumbs.Runtime.Managers
{
    public enum AudioQuickPreset
    {
        Balanced,
        IntenseCombat,
        ChillExploration
    }

    [DefaultExecutionOrder(-240)]
    [RequireComponent(typeof(AudioSource))]
    public sealed partial class AudioManager
    {
        [System.Serializable]
        private sealed class EventAudioRule
        {
            [Range(0f, 2f)] public float volumeMultiplier = 1f;
            [Min(-1f)] public float minIntervalOverride = -1f;
            [Range(0, 256)] public int playPriority = 128;
            public bool bypassBurstLimiter;
            public AudioMixerGroup mixerOverride;
            public AnimationCurve burstVolumeCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.55f);
        }

        private readonly struct EventAudioProfile
        {
            public EventAudioProfile(float frequency, float duration, float volume)
            {
                Frequency = Mathf.Max(60f, frequency);
                Duration = Mathf.Max(0.03f, duration);
                Volume = Mathf.Clamp01(volume);
            }

            public float Frequency { get; }
            public float Duration { get; }
            public float Volume { get; }
        }

        private enum RuntimeStingerKind
        {
            None,
            ExitUnlocked,
            ChaseSpike,
            LockOnWarning,
            EscapeRelief,
            QuietBreathBroken,
            EchoReturn,
            RiskReward,
            RhythmShift,
            SetPieceShift,
            PressureWave,
            Death
        }

        public static AudioManager Instance { get; private set; }

        [Header("Event Audio")]
        [SerializeField] private bool autoListenRuntimeEvents = true;
        [SerializeField, Range(0f, 1f)] private float masterVolume = 0.35f;
        [SerializeField, Min(0f)] private float minPlayInterval = 0.04f;
        [SerializeField, Range(0f, 0.2f)] private float pitchJitter = 0.035f;
        [SerializeField] private bool muted;

        [Header("Mixer Routing")]
        [SerializeField] private AudioMixerGroup eventMixerGroup;

        [Header("Burst Limiter")]
        [SerializeField, Min(0.15f)] private float burstWindowSeconds = 0.85f;
        [SerializeField, Min(1)] private int burstMaxEvents = 8;
        [SerializeField, Range(0f, 1f)] private float burstPriorityGateStart = 0.65f;
        [SerializeField, Range(0, 256)] private int burstLowPriorityThreshold = 120;
        [SerializeField, Range(0f, 1f)] private float minimumBurstVolume = 0.2f;

        [Header("Assigned SFX (Optional)")]
        [SerializeField] private bool preferAssignedClips = true;
        [SerializeField] private AudioClip saveClip;
        [SerializeField] private AudioClip loadClip;
        [SerializeField] private AudioClip runClip;
        [SerializeField] private AudioClip stageClip;
        [SerializeField] private AudioClip objectiveClip;
        [SerializeField] private AudioClip abilityClip;
        [SerializeField] private AudioClip deathClip;

        [Header("Context Stingers")]
        [SerializeField] private bool enableRuntimeStingers = true;
        [SerializeField] private bool suppressEventToneWhenStingerPlays = true;
        [SerializeField] private AudioMixerGroup stingerMixerGroup;
        [SerializeField] private AudioClip exitUnlockedStingerClip;
        [SerializeField] private AudioClip chaseSpikeStingerClip;
        [SerializeField] private AudioClip lockOnWarningStingerClip;
        [SerializeField] private AudioClip escapeReliefStingerClip;
        [SerializeField] private AudioClip quietBreathBrokenStingerClip;
        [SerializeField] private AudioClip echoReturnStingerClip;
        [SerializeField] private AudioClip riskRewardStingerClip;
        [SerializeField] private AudioClip rhythmShiftStingerClip;
        [SerializeField] private AudioClip setPieceShiftStingerClip;
        [SerializeField] private AudioClip pressureWaveStingerClip;
        [SerializeField] private AudioClip deathStingerClip;
        [SerializeField, Range(0f, 2f)] private float exitUnlockedStingerVolume = 1f;
        [SerializeField, Range(0f, 2f)] private float chaseSpikeStingerVolume = 1f;
        [SerializeField, Min(0f)] private float exitUnlockedStingerCooldown = 0.8f;
        [SerializeField, Min(0f)] private float chaseSpikeStingerCooldown = 0.7f;
        [SerializeField, Min(0f)] private float semanticStingerCooldown = 1.6f;
        [SerializeField, Min(0f)] private float majorStingerBudgetCooldown = 3f;
        [SerializeField] private bool logStingerAudio;

        [Header("Stinger Stage Scaling")]
        [SerializeField] private bool scaleStingersByStage = true;
        [SerializeField, Min(1)] private int stingerRampStartStage = 1;
        [SerializeField, Min(2)] private int stingerRampPeakStage = 5;
        [SerializeField, Range(0.6f, 2.2f)] private float minStingerVolumeMultiplier = 0.88f;
        [SerializeField, Range(0.6f, 2.2f)] private float maxStingerVolumeMultiplier = 1.3f;
        [SerializeField, Range(0.8f, 1.3f)] private float minStingerPitch = 0.97f;
        [SerializeField, Range(0.8f, 1.3f)] private float maxStingerPitch = 1.08f;
        [SerializeField, Range(0.4f, 1.4f)] private float minStingerCooldownMultiplier = 1.08f;
        [SerializeField, Range(0.4f, 1.4f)] private float maxStingerCooldownMultiplier = 0.78f;
        [SerializeField, Range(0.5f, 1.7f)] private float minStingerDuckMultiplier = 0.92f;
        [SerializeField, Range(0.5f, 1.7f)] private float maxStingerDuckMultiplier = 1.26f;

        [Header("Per Event Rules")]
        [SerializeField] private EventAudioRule saveRule = new EventAudioRule { volumeMultiplier = 0.9f, minIntervalOverride = 0.05f, playPriority = 96, bypassBurstLimiter = false, burstVolumeCurve = null };
        [SerializeField] private EventAudioRule loadRule = new EventAudioRule { volumeMultiplier = 1f, minIntervalOverride = 0.02f, playPriority = 220, bypassBurstLimiter = true, burstVolumeCurve = null };
        [SerializeField] private EventAudioRule runRule = new EventAudioRule { volumeMultiplier = 0.9f, minIntervalOverride = 0.03f, playPriority = 170, bypassBurstLimiter = false, burstVolumeCurve = null };
        [SerializeField] private EventAudioRule stageRule = new EventAudioRule { volumeMultiplier = 0.85f, minIntervalOverride = 0.04f, playPriority = 165, bypassBurstLimiter = false, burstVolumeCurve = null };
        [SerializeField] private EventAudioRule objectiveRule = new EventAudioRule { volumeMultiplier = 0.8f, minIntervalOverride = 0.04f, playPriority = 150, bypassBurstLimiter = false, burstVolumeCurve = null };
        [SerializeField] private EventAudioRule abilityRule = new EventAudioRule { volumeMultiplier = 0.72f, minIntervalOverride = 0.03f, playPriority = 110, bypassBurstLimiter = false, burstVolumeCurve = null };
        [SerializeField] private EventAudioRule deathRule = new EventAudioRule { volumeMultiplier = 1f, minIntervalOverride = 0f, playPriority = 255, bypassBurstLimiter = true, burstVolumeCurve = null };


        [Header("Runtime Ducking")]
        [SerializeField] private bool enableRuntimeDucking = true;
        [SerializeField] private bool useMixerExposedParameters;
        [SerializeField] private AudioMixer duckingMixer;
        [SerializeField] private string musicVolumeParam = "MusicVolumeDb";
        [SerializeField] private string ambienceVolumeParam = "AmbienceVolumeDb";
        [SerializeField, Range(0f, 1f)] private float normalMusicVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] private float combatMusicVolume = 0.52f;
        [SerializeField, Range(0f, 1f)] private float normalAmbienceVolume = 0.85f;
        [SerializeField, Range(0f, 1f)] private float combatAmbienceVolume = 0.58f;
        [SerializeField, Min(0f)] private float normalMusicDb = 0f;
        [SerializeField, Min(-80f)] private float combatMusicDb = -8f;
        [SerializeField, Min(0f)] private float normalAmbienceDb = 0f;
        [SerializeField, Min(-80f)] private float combatAmbienceDb = -6f;
        [SerializeField, Min(0.1f)] private float duckingLerpSpeed = 4.5f;
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource ambienceSource;
        [Header("Debug")]
        [SerializeField] private KeyCode muteToggleKey = KeyCode.F4;
        [SerializeField] private bool logEventAudio;

        private readonly Dictionary<RuntimeEventType, AudioClip> toneCache = new();
        private readonly Dictionary<RuntimeStingerKind, AudioClip> stingerToneCache = new();
        private readonly Queue<float> recentPlayTimes = new();

        private AudioSource eventSource;
        private AudioSource stingerSource;
        private float lastPlayTime;
        private float nextExitUnlockedStingerTime;
        private float nextChaseSpikeStingerTime;
        private float nextSemanticStingerTime;
        private float nextMajorStingerTime;
        private int suppressedRuntimeStingerCount;
        private float burstLevelNormalized;
        private RuntimeEventType lastPlayedEventType;
        private string lastPlaySource = "-";
        private float combatDuckTarget;
        private float combatDuckCurrent;
        private float eventDuckBoost;
        private float lastStingerStageIntensity;
        private string lastRuntimeStingerLabel = "-";
        private string lastRuntimeStingerSource = "-";
        private float lastRuntimeStingerVolume;
        private float lastRuntimeStingerPitch = 1f;
        private float lastRuntimeStingerPlayedAt = -1f;

        public bool Muted => muted;
        public float MasterVolume => masterVolume;
        public bool PreferAssignedClips => preferAssignedClips;
        public int AssignedClipCount => CountAssignedClips();
        public int AssignedStingerClipCount => CountAssignedStingerClips();
        public float BurstLevelNormalized => burstLevelNormalized;
        public int RecentBurstCount => recentPlayTimes.Count;
        public RuntimeEventType LastPlayedEventType => lastPlayedEventType;
        public string LastPlaySource => lastPlaySource;
        public float CombatDuckTarget => combatDuckTarget;
        public float CombatDuckCurrent => combatDuckCurrent;
        public float EffectiveDuck => Mathf.Clamp01(combatDuckCurrent + eventDuckBoost);
        public bool RuntimeDuckingEnabled => enableRuntimeDucking;
        public float LastStingerStageIntensity => lastStingerStageIntensity;
        public bool HasRuntimeStingerTelemetry => lastRuntimeStingerPlayedAt >= 0f;
        public string LastRuntimeStingerLabel => string.IsNullOrWhiteSpace(lastRuntimeStingerLabel) ? "-" : lastRuntimeStingerLabel;
        public string LastRuntimeStingerSource => string.IsNullOrWhiteSpace(lastRuntimeStingerSource) ? "-" : lastRuntimeStingerSource;
        public float LastRuntimeStingerVolume => lastRuntimeStingerVolume;
        public float LastRuntimeStingerPitch => lastRuntimeStingerPitch;
        public float LastRuntimeStingerAge => lastRuntimeStingerPlayedAt < 0f ? -1f : Mathf.Max(0f, Time.unscaledTime - lastRuntimeStingerPlayedAt);
        public int SuppressedRuntimeStingerCount => Mathf.Max(0, suppressedRuntimeStingerCount);

        protected override void Awake()
        {
            base.Awake();

            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            EnsureAudioSource();
            EnsureStingerSource();
            EnsureRuleCurves();
            EnsureStingerProfiles();
        }

        private void OnValidate()
        {
            EnsureRuleCurves();
            EnsureStingerProfiles();
        }

        private void OnEnable()
        {
            if (autoListenRuntimeEvents)
            {
                RuntimeEventBus.EventRaised -= HandleRuntimeEvent;
                RuntimeEventBus.EventRaised += HandleRuntimeEvent;
            }
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

        private void Update()
        {
            if (RuntimeInputAdapter.GetKeyDown(muteToggleKey))
            {
                SetMuted(!muted);
            }

            PruneBurstWindow(Time.unscaledTime);
            TickRuntimeDucking(Time.unscaledDeltaTime);
        }

        [ContextMenu("Debug/Test Death Event Audio")]
        private void DebugTestDeathAudio()
        {
            PlayEventTone(RuntimeEventType.Death);
        }

        [ContextMenu("Debug/Test Exit Unlocked Stinger")]
        private void DebugTestExitUnlockedStinger()
        {
            TryPlayStinger(RuntimeStingerKind.ExitUnlocked, bypassCooldown: true, stageIntensity01: 1f);
        }

        [ContextMenu("Debug/Test Chase Spike Stinger")]
        private void DebugTestChaseSpikeStinger()
        {
            TryPlayStinger(RuntimeStingerKind.ChaseSpike, bypassCooldown: true, stageIntensity01: 1f);
        }

        public void SetMuted(bool isMuted)
        {
            muted = isMuted;

            if (logEventAudio)
            {
                Debug.Log($"[AudioManager] Event audio muted={muted}", this);
            }
        }


        public void ApplyQuickPreset(AudioQuickPreset preset)
        {
            switch (preset)
            {
                case AudioQuickPreset.IntenseCombat:
                    masterVolume = 0.42f;
                    minPlayInterval = 0.03f;
                    burstWindowSeconds = 0.9f;
                    burstMaxEvents = 10;
                    burstPriorityGateStart = 0.72f;
                    burstLowPriorityThreshold = 110;
                    minimumBurstVolume = 0.25f;
                    enableRuntimeDucking = true;
                    duckingLerpSpeed = 6.5f;
                    normalMusicVolume = 0.95f;
                    combatMusicVolume = 0.4f;
                    normalAmbienceVolume = 0.88f;
                    combatAmbienceVolume = 0.45f;
                    normalMusicDb = 0f;
                    combatMusicDb = -12f;
                    normalAmbienceDb = 0f;
                    combatAmbienceDb = -9f;
                    break;

                case AudioQuickPreset.ChillExploration:
                    masterVolume = 0.28f;
                    minPlayInterval = 0.06f;
                    burstWindowSeconds = 0.75f;
                    burstMaxEvents = 6;
                    burstPriorityGateStart = 0.58f;
                    burstLowPriorityThreshold = 135;
                    minimumBurstVolume = 0.35f;
                    enableRuntimeDucking = true;
                    duckingLerpSpeed = 3.2f;
                    normalMusicVolume = 0.82f;
                    combatMusicVolume = 0.65f;
                    normalAmbienceVolume = 0.78f;
                    combatAmbienceVolume = 0.68f;
                    normalMusicDb = 0f;
                    combatMusicDb = -5f;
                    normalAmbienceDb = 0f;
                    combatAmbienceDb = -4f;
                    break;

                default:
                    masterVolume = 0.35f;
                    minPlayInterval = 0.04f;
                    burstWindowSeconds = 0.85f;
                    burstMaxEvents = 8;
                    burstPriorityGateStart = 0.65f;
                    burstLowPriorityThreshold = 120;
                    minimumBurstVolume = 0.2f;
                    enableRuntimeDucking = true;
                    duckingLerpSpeed = 4.5f;
                    normalMusicVolume = 0.9f;
                    combatMusicVolume = 0.52f;
                    normalAmbienceVolume = 0.85f;
                    combatAmbienceVolume = 0.58f;
                    normalMusicDb = 0f;
                    combatMusicDb = -8f;
                    normalAmbienceDb = 0f;
                    combatAmbienceDb = -6f;
                    break;
            }

            EnsureRuleCurves();
        }
        public bool PlayEventTone(RuntimeEventType type)
        {
            EnsureAudioSource();

            if (eventSource == null || muted)
            {
                return false;
            }

            EventAudioProfile profile = GetProfile(type);
            if (profile.Volume <= 0f)
            {
                return false;
            }

            EventAudioRule rule = GetRule(type) ?? abilityRule ?? new EventAudioRule();
            float now = Time.unscaledTime;
            PruneBurstWindow(now);

            float effectiveInterval = rule != null && rule.minIntervalOverride >= 0f
                ? rule.minIntervalOverride
                : minPlayInterval;

            if (!rule.bypassBurstLimiter && effectiveInterval > 0f && now - lastPlayTime < effectiveInterval)
            {
                return false;
            }

            float burst01 = ComputeBurstLevelNormalized();
            if (!rule.bypassBurstLimiter && burst01 >= burstPriorityGateStart && rule.playPriority < burstLowPriorityThreshold)
            {
                return false;
            }

            float attenuation = rule.bypassBurstLimiter ? 1f : Mathf.Max(minimumBurstVolume, EvaluateBurstAttenuation(rule, burst01));
            float finalVolume = Mathf.Clamp01(masterVolume) * profile.Volume * Mathf.Clamp(rule.volumeMultiplier, 0f, 2f) * attenuation;
            if (finalVolume <= 0.0001f)
            {
                return false;
            }

            eventSource.outputAudioMixerGroup = rule.mixerOverride != null ? rule.mixerOverride : eventMixerGroup;
            eventSource.priority = Mathf.Clamp(256 - Mathf.Clamp(rule.playPriority, 0, 256), 0, 256);
            eventSource.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);

            bool playedFromClip = TryPlayAssignedClip(type, finalVolume);
            if (!playedFromClip)
            {
                AudioClip tone = GetOrCreateToneClip(type, profile);
                if (tone == null)
                {
                    return false;
                }

                eventSource.PlayOneShot(tone, finalVolume);
            }

            recentPlayTimes.Enqueue(now);
            PruneBurstWindow(now);
            burstLevelNormalized = ComputeBurstLevelNormalized();

            lastPlayTime = now;
            lastPlayedEventType = type;
            lastPlaySource = playedFromClip ? "clip" : "tone";

            if (logEventAudio)
            {
                Debug.Log($"[AudioManager] Played {type} ({lastPlaySource}, volume={finalVolume:0.00}, burst={burstLevelNormalized:0.00}, priority={rule.playPriority})", this);
            }

            return true;
        }


        public void SetCombatIntensity(float intensityNormalized)
        {
            combatDuckTarget = Mathf.Clamp01(intensityNormalized);
        }

        public void SetDuckingSourcesForEditor(AudioSource bgmSource, AudioSource ambienceAudioSource)
        {
            musicSource = bgmSource;
            ambienceSource = ambienceAudioSource;
        }

        private void TickRuntimeDucking(float deltaTime)
        {
            if (!enableRuntimeDucking)
            {
                return;
            }

            float safeDelta = Mathf.Max(0f, deltaTime);
            combatDuckCurrent = Mathf.MoveTowards(combatDuckCurrent, combatDuckTarget, duckingLerpSpeed * safeDelta);
            eventDuckBoost = Mathf.MoveTowards(eventDuckBoost, 0f, 1.3f * safeDelta);

            float effective = Mathf.Clamp01(combatDuckCurrent + eventDuckBoost);

            if (useMixerExposedParameters && duckingMixer != null)
            {
                if (!string.IsNullOrWhiteSpace(musicVolumeParam))
                {
                    float musicDb = Mathf.Lerp(normalMusicDb, combatMusicDb, effective);
                    duckingMixer.SetFloat(musicVolumeParam, musicDb);
                }

                if (!string.IsNullOrWhiteSpace(ambienceVolumeParam))
                {
                    float ambienceDb = Mathf.Lerp(normalAmbienceDb, combatAmbienceDb, effective);
                    duckingMixer.SetFloat(ambienceVolumeParam, ambienceDb);
                }

                return;
            }

            if (musicSource != null)
            {
                musicSource.volume = Mathf.Lerp(normalMusicVolume, combatMusicVolume, effective);
            }

            if (ambienceSource != null)
            {
                ambienceSource.volume = Mathf.Lerp(normalAmbienceVolume, combatAmbienceVolume, effective);
            }
        }
        private bool TryPlayAssignedClip(RuntimeEventType type, float volume)
        {
            if (!preferAssignedClips)
            {
                return false;
            }

            AudioClip clip = GetAssignedClip(type);
            if (clip == null)
            {
                return false;
            }

            eventSource.PlayOneShot(clip, volume);
            return true;
        }
        private void HandleRuntimeEvent(RuntimeEventRecord record)
        {
            if (!record.IsValid)
            {
                return;
            }

            PushEventDuckBoost(record.Type);
            bool playedStinger = TryPlayRuntimeStinger(record, out RuntimeStingerKind playedKind, out float stageIntensity01);
            if (playedStinger && suppressEventToneWhenStingerPlays)
            {
                if (logStingerAudio || logEventAudio)
                {
                    Debug.Log($"[AudioManager] Suppressed base tone due to stinger: {playedKind} (stageIntensity={stageIntensity01:0.00})", this);
                }

                return;
            }

            PlayEventTone(record.Type);
        }

        private bool TryPlayRuntimeStinger(RuntimeEventRecord record, out RuntimeStingerKind playedKind, out float stageIntensity01)
        {
            playedKind = RuntimeStingerKind.None;
            stageIntensity01 = 0f;

            if (!enableRuntimeStingers || muted || !record.IsValid)
            {
                return false;
            }

            RuntimeStingerKind kind = ResolveStingerKind(record);
            if (kind == RuntimeStingerKind.None)
            {
                return false;
            }

            stageIntensity01 = EvaluateStingerStageIntensity(record.Stage);

            float now = Time.unscaledTime;
            if (IsStingerOnCooldown(kind, now) || IsMajorStingerBudgetBlocked(kind, now))
            {
                suppressedRuntimeStingerCount++;
                return false;
            }

            if (!TryPlayStinger(kind, bypassCooldown: true, stageIntensity01: stageIntensity01))
            {
                return false;
            }

            MarkStingerCooldown(kind, now, stageIntensity01);
            MarkMajorStingerBudget(kind, now);
            PushStingerDuckBoost(kind, stageIntensity01);
            lastStingerStageIntensity = stageIntensity01;
            playedKind = kind;
            return true;
        }

        private RuntimeStingerKind ResolveStingerKind(RuntimeEventRecord record)
        {
            RuntimeStingerKind semanticKind = record.Semantic switch
            {
                RuntimeEventSemantic.ExitUnlocked => RuntimeStingerKind.ExitUnlocked,
                RuntimeEventSemantic.LockOnWarning => RuntimeStingerKind.LockOnWarning,
                RuntimeEventSemantic.ChaseStarted => RuntimeStingerKind.ChaseSpike,
                RuntimeEventSemantic.EscapeRelief => RuntimeStingerKind.EscapeRelief,
                RuntimeEventSemantic.QuietBreathBroken => RuntimeStingerKind.QuietBreathBroken,
                RuntimeEventSemantic.EchoReturn => RuntimeStingerKind.EchoReturn,
                RuntimeEventSemantic.RiskReward => RuntimeStingerKind.RiskReward,
                RuntimeEventSemantic.PressureWave => RuntimeStingerKind.PressureWave,
                RuntimeEventSemantic.SetPieceShift => RuntimeStingerKind.SetPieceShift,
                RuntimeEventSemantic.RhythmShift => RuntimeStingerKind.RhythmShift,
                _ => RuntimeStingerKind.None
            };
            if (semanticKind != RuntimeStingerKind.None)
            {
                return semanticKind;
            }

            if (record.Type == RuntimeEventType.Death)
            {
                return RuntimeStingerKind.Death;
            }

            if (record.Type == RuntimeEventType.Objective && ContainsEventKeyword(record.Message, "exit unlocked"))
            {
                return RuntimeStingerKind.ExitUnlocked;
            }

            if (record.Type == RuntimeEventType.System && ContainsEventKeyword(record.Message, "chase started"))
            {
                return RuntimeStingerKind.ChaseSpike;
            }

            return RuntimeStingerKind.None;
        }

        private static bool ContainsEventKeyword(string message, string keyword)
        {
            if (string.IsNullOrWhiteSpace(message) || string.IsNullOrWhiteSpace(keyword))
            {
                return false;
            }

            return message.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool IsStingerOnCooldown(RuntimeStingerKind kind, float now)
        {
            return kind switch
            {
                RuntimeStingerKind.ExitUnlocked => now < nextExitUnlockedStingerTime,
                RuntimeStingerKind.ChaseSpike => now < nextChaseSpikeStingerTime,
                RuntimeStingerKind.None => false,
                _ => now < nextSemanticStingerTime
            };
        }

        private bool IsMajorStingerBudgetBlocked(RuntimeStingerKind kind, float now)
        {
            return IsMajorRuntimeStinger(kind) && now < nextMajorStingerTime;
        }

        private static bool IsMajorRuntimeStinger(RuntimeStingerKind kind)
        {
            return kind switch
            {
                RuntimeStingerKind.EscapeRelief => false,
                RuntimeStingerKind.RhythmShift => false,
                RuntimeStingerKind.None => false,
                _ => true
            };
        }

        private void MarkStingerCooldown(RuntimeStingerKind kind, float now, float stageIntensity01)
        {
            float stageCooldownMultiplier = EvaluateStingerCooldownMultiplier(stageIntensity01);
            switch (kind)
            {
                case RuntimeStingerKind.ExitUnlocked:
                    nextExitUnlockedStingerTime = now + Mathf.Max(0f, exitUnlockedStingerCooldown * stageCooldownMultiplier);
                    break;
                case RuntimeStingerKind.ChaseSpike:
                    nextChaseSpikeStingerTime = now + Mathf.Max(0f, chaseSpikeStingerCooldown * stageCooldownMultiplier);
                    break;
                case RuntimeStingerKind.None:
                    break;
                default:
                    nextSemanticStingerTime = now + Mathf.Max(0f, semanticStingerCooldown * stageCooldownMultiplier);
                    break;
            }
        }

        private void MarkMajorStingerBudget(RuntimeStingerKind kind, float now)
        {
            if (!IsMajorRuntimeStinger(kind))
            {
                return;
            }

            nextMajorStingerTime = now + Mathf.Max(0f, majorStingerBudgetCooldown);
        }

        private bool TryPlayStinger(RuntimeStingerKind kind, bool bypassCooldown, float stageIntensity01)
        {
            if (!enableRuntimeStingers || muted || kind == RuntimeStingerKind.None)
            {
                return false;
            }

            EnsureStingerSource();
            EnsureStingerProfiles();
            if (stingerSource == null)
            {
                return false;
            }

            stageIntensity01 = Mathf.Clamp01(stageIntensity01);

            float now = Time.unscaledTime;
            if (!bypassCooldown && IsStingerOnCooldown(kind, now))
            {
                return false;
            }

            AudioClip clip = GetAssignedStingerClip(kind);

            float volumeScale = kind switch
            {
                RuntimeStingerKind.ExitUnlocked => exitUnlockedStingerVolume,
                RuntimeStingerKind.ChaseSpike => chaseSpikeStingerVolume,
                RuntimeStingerKind.LockOnWarning => 0.82f,
                RuntimeStingerKind.EscapeRelief => 0.72f,
                RuntimeStingerKind.QuietBreathBroken => 0.92f,
                RuntimeStingerKind.EchoReturn => 0.78f,
                RuntimeStingerKind.RiskReward => 0.86f,
                RuntimeStingerKind.RhythmShift => 0.7f,
                RuntimeStingerKind.SetPieceShift => 0.92f,
                RuntimeStingerKind.PressureWave => 0.82f,
                RuntimeStingerKind.Death => 1.18f,
                _ => 1f
            };

            if (clip == null)
            {
                clip = GetOrCreateStingerToneClip(kind);
                if (clip == null)
                {
                    return false;
                }
            }

            float stageVolumeMul = EvaluateStingerVolumeMultiplier(stageIntensity01);
            float finalVolume = Mathf.Clamp01(masterVolume) * Mathf.Clamp(volumeScale, 0f, 2f) * stageVolumeMul;
            if (finalVolume <= 0.0001f)
            {
                return false;
            }

            float stagePitch = EvaluateStingerPitch(kind, stageIntensity01);

            stingerSource.outputAudioMixerGroup = stingerMixerGroup != null ? stingerMixerGroup : eventMixerGroup;
            stingerSource.pitch = stagePitch;
            stingerSource.priority = 48;
            stingerSource.PlayOneShot(clip, finalVolume);

            lastRuntimeStingerLabel = kind.ToString();
            lastRuntimeStingerSource = IsAssignedStingerClip(kind, clip) ? "clip" : "tone";
            lastRuntimeStingerVolume = finalVolume;
            lastRuntimeStingerPitch = stagePitch;
            lastRuntimeStingerPlayedAt = now;

            if (!bypassCooldown)
            {
                MarkStingerCooldown(kind, now, stageIntensity01);
            }

            if (logStingerAudio)
            {
                Debug.Log($"[AudioManager] Stinger {kind} ({lastRuntimeStingerSource}, volume={finalVolume:0.00}, pitch={stagePitch:0.00}, stageIntensity={stageIntensity01:0.00})", this);
            }

            return true;
        }

        private void PushStingerDuckBoost(RuntimeStingerKind kind, float stageIntensity01)
        {
            float boost = kind switch
            {
                RuntimeStingerKind.ExitUnlocked => 0.24f,
                RuntimeStingerKind.ChaseSpike => 0.34f,
                RuntimeStingerKind.LockOnWarning => 0.22f,
                RuntimeStingerKind.EscapeRelief => 0.18f,
                RuntimeStingerKind.QuietBreathBroken => 0.28f,
                RuntimeStingerKind.EchoReturn => 0.2f,
                RuntimeStingerKind.RiskReward => 0.24f,
                RuntimeStingerKind.SetPieceShift => 0.3f,
                RuntimeStingerKind.PressureWave => 0.26f,
                RuntimeStingerKind.Death => 0.45f,
                _ => 0.1f
            };

            boost *= EvaluateStingerDuckMultiplier(stageIntensity01);
            eventDuckBoost = Mathf.Max(eventDuckBoost, boost);
        }

        private void PushEventDuckBoost(RuntimeEventType type)
        {
            float boost = type switch
            {
                RuntimeEventType.Death => 0.45f,
                RuntimeEventType.Load => 0.3f,
                RuntimeEventType.Stage => 0.22f,
                RuntimeEventType.Objective => 0.16f,
                _ => 0.1f
            };

            eventDuckBoost = Mathf.Max(eventDuckBoost, boost);
        }
        private void EnsureAudioSource()
        {
            if (eventSource != null)
            {
                return;
            }

            eventSource = GetComponent<AudioSource>();
            if (eventSource == null)
            {
                eventSource = gameObject.AddComponent<AudioSource>();
            }

            eventSource.playOnAwake = false;
            eventSource.loop = false;
            eventSource.spatialBlend = 0f;
            eventSource.volume = 1f;
            eventSource.outputAudioMixerGroup = eventMixerGroup;
            eventSource.priority = 128;
        }

        private void EnsureStingerSource()
        {
            if (stingerSource != null)
            {
                stingerSource.outputAudioMixerGroup = stingerMixerGroup != null ? stingerMixerGroup : eventMixerGroup;
                return;
            }

            AudioSource[] sources = GetComponents<AudioSource>();
            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource source = sources[i];
                if (source != null && source != eventSource)
                {
                    stingerSource = source;
                    break;
                }
            }

            if (stingerSource == null)
            {
                stingerSource = gameObject.AddComponent<AudioSource>();
            }

            stingerSource.playOnAwake = false;
            stingerSource.loop = false;
            stingerSource.spatialBlend = 0f;
            stingerSource.volume = 1f;
            stingerSource.priority = 64;
            stingerSource.outputAudioMixerGroup = stingerMixerGroup != null ? stingerMixerGroup : eventMixerGroup;
        }

        private void EnsureStingerProfiles()
        {
            exitUnlockedStingerVolume = Mathf.Clamp(exitUnlockedStingerVolume, 0f, 2f);
            chaseSpikeStingerVolume = Mathf.Clamp(chaseSpikeStingerVolume, 0f, 2f);
            exitUnlockedStingerCooldown = Mathf.Max(0f, exitUnlockedStingerCooldown);
            chaseSpikeStingerCooldown = Mathf.Max(0f, chaseSpikeStingerCooldown);
            semanticStingerCooldown = Mathf.Max(0f, semanticStingerCooldown);
            majorStingerBudgetCooldown = Mathf.Max(0f, majorStingerBudgetCooldown);

            stingerRampStartStage = Mathf.Max(1, stingerRampStartStage);
            stingerRampPeakStage = Mathf.Max(stingerRampStartStage + 1, stingerRampPeakStage);

            minStingerVolumeMultiplier = Mathf.Clamp(minStingerVolumeMultiplier, 0.6f, 2.2f);
            maxStingerVolumeMultiplier = Mathf.Clamp(maxStingerVolumeMultiplier, 0.6f, 2.2f);
            if (maxStingerVolumeMultiplier < minStingerVolumeMultiplier)
            {
                float swapVolume = minStingerVolumeMultiplier;
                minStingerVolumeMultiplier = maxStingerVolumeMultiplier;
                maxStingerVolumeMultiplier = swapVolume;
            }

            minStingerPitch = Mathf.Clamp(minStingerPitch, 0.8f, 1.3f);
            maxStingerPitch = Mathf.Clamp(maxStingerPitch, 0.8f, 1.3f);
            if (maxStingerPitch < minStingerPitch)
            {
                float swapPitch = minStingerPitch;
                minStingerPitch = maxStingerPitch;
                maxStingerPitch = swapPitch;
            }

            minStingerCooldownMultiplier = Mathf.Clamp(minStingerCooldownMultiplier, 0.4f, 1.4f);
            maxStingerCooldownMultiplier = Mathf.Clamp(maxStingerCooldownMultiplier, 0.4f, 1.4f);
            if (maxStingerCooldownMultiplier < minStingerCooldownMultiplier)
            {
                float swapCooldown = minStingerCooldownMultiplier;
                minStingerCooldownMultiplier = maxStingerCooldownMultiplier;
                maxStingerCooldownMultiplier = swapCooldown;
            }

            minStingerDuckMultiplier = Mathf.Clamp(minStingerDuckMultiplier, 0.5f, 1.7f);
            maxStingerDuckMultiplier = Mathf.Clamp(maxStingerDuckMultiplier, 0.5f, 1.7f);
            if (maxStingerDuckMultiplier < minStingerDuckMultiplier)
            {
                float swapDuck = minStingerDuckMultiplier;
                minStingerDuckMultiplier = maxStingerDuckMultiplier;
                maxStingerDuckMultiplier = swapDuck;
            }
        }

        private void EnsureRuleCurves()
        {
            EnsureRuleCurve(saveRule, 0.62f);
            EnsureRuleCurve(loadRule, 0.9f);
            EnsureRuleCurve(runRule, 0.72f);
            EnsureRuleCurve(stageRule, 0.7f);
            EnsureRuleCurve(objectiveRule, 0.66f);
            EnsureRuleCurve(abilityRule, 0.54f);
            EnsureRuleCurve(deathRule, 1f);
        }

        private static void EnsureRuleCurve(EventAudioRule rule, float endValue)
        {
            if (rule == null)
            {
                return;
            }

            if (rule.burstVolumeCurve == null || rule.burstVolumeCurve.length == 0)
            {
                rule.burstVolumeCurve = AnimationCurve.Linear(0f, 1f, 1f, Mathf.Clamp01(endValue));
            }
        }

        private float EvaluateStingerStageIntensity(int stage)
        {
            if (!scaleStingersByStage)
            {
                return 0f;
            }

            int startStage = Mathf.Max(1, stingerRampStartStage);
            int peakStage = Mathf.Max(startStage + 1, stingerRampPeakStage);
            float t = Mathf.InverseLerp(startStage, peakStage, Mathf.Max(1, stage));
            return Mathf.SmoothStep(0f, 1f, t);
        }

        private float EvaluateStingerVolumeMultiplier(float stageIntensity01)
        {
            return Mathf.Lerp(minStingerVolumeMultiplier, maxStingerVolumeMultiplier, Mathf.Clamp01(stageIntensity01));
        }

        private float EvaluateStingerCooldownMultiplier(float stageIntensity01)
        {
            return Mathf.Lerp(minStingerCooldownMultiplier, maxStingerCooldownMultiplier, Mathf.Clamp01(stageIntensity01));
        }

        private float EvaluateStingerDuckMultiplier(float stageIntensity01)
        {
            return Mathf.Lerp(minStingerDuckMultiplier, maxStingerDuckMultiplier, Mathf.Clamp01(stageIntensity01));
        }

        private float EvaluateStingerPitch(RuntimeStingerKind kind, float stageIntensity01)
        {
            float basePitch = Mathf.Lerp(minStingerPitch, maxStingerPitch, Mathf.Clamp01(stageIntensity01));
            if (kind == RuntimeStingerKind.ChaseSpike)
            {
                basePitch += 0.015f;
            }
            else if (kind == RuntimeStingerKind.EscapeRelief)
            {
                basePitch -= 0.035f;
            }
            else if (kind == RuntimeStingerKind.Death)
            {
                basePitch -= 0.08f;
            }

            return Mathf.Clamp(basePitch, 0.8f, 1.3f);
        }

        private void PruneBurstWindow(float now)
        {
            float window = Mathf.Max(0.15f, burstWindowSeconds);
            while (recentPlayTimes.Count > 0 && now - recentPlayTimes.Peek() > window)
            {
                recentPlayTimes.Dequeue();
            }

            burstLevelNormalized = ComputeBurstLevelNormalized();
        }

        private float ComputeBurstLevelNormalized()
        {
            int maxEvents = Mathf.Max(1, burstMaxEvents);
            return Mathf.Clamp01(recentPlayTimes.Count / (float)maxEvents);
        }

        private static float EvaluateBurstAttenuation(EventAudioRule rule, float burst01)
        {
            if (rule == null || rule.burstVolumeCurve == null || rule.burstVolumeCurve.length == 0)
            {
                return Mathf.Lerp(1f, 0.55f, burst01);
            }

            return Mathf.Clamp01(rule.burstVolumeCurve.Evaluate(Mathf.Clamp01(burst01)));
        }

        private AudioClip GetOrCreateToneClip(RuntimeEventType type, EventAudioProfile profile)
        {
            if (toneCache.TryGetValue(type, out AudioClip cached) && cached != null)
            {
                return cached;
            }

            AudioClip created = CreateToneClip(type, profile.Frequency, profile.Duration);
            toneCache[type] = created;
            return created;
        }

        private AudioClip GetOrCreateStingerToneClip(RuntimeStingerKind kind)
        {
            if (kind == RuntimeStingerKind.None)
            {
                return null;
            }

            if (stingerToneCache.TryGetValue(kind, out AudioClip cached) && cached != null)
            {
                return cached;
            }

            AudioClip created = CreateStingerToneClip(kind);
            stingerToneCache[kind] = created;
            return created;
        }

        private static AudioClip CreateStingerToneClip(RuntimeStingerKind kind)
        {
            return kind switch
            {
                RuntimeStingerKind.ExitUnlocked => CreatePatternToneClip(
                    "Stinger_ExitUnlocked",
                    new[] { 530f, 670f, 860f },
                    new[] { 0.06f, 0.08f, 0.12f },
                    0.018f),
                RuntimeStingerKind.ChaseSpike => CreatePatternToneClip(
                    "Stinger_ChaseSpike",
                    new[] { 300f, 360f, 440f, 320f },
                    new[] { 0.05f, 0.05f, 0.07f, 0.09f },
                    0.012f),
                RuntimeStingerKind.LockOnWarning => CreatePatternToneClip(
                    "Stinger_LockOnWarning",
                    new[] { 220f, 220f, 320f },
                    new[] { 0.045f, 0.045f, 0.12f },
                    0.04f),
                RuntimeStingerKind.EscapeRelief => CreatePatternToneClip(
                    "Stinger_EscapeRelief",
                    new[] { 280f, 420f, 560f },
                    new[] { 0.12f, 0.14f, 0.18f },
                    0.028f),
                RuntimeStingerKind.QuietBreathBroken => CreatePatternToneClip(
                    "Stinger_QuietBreathBroken",
                    new[] { 620f, 260f },
                    new[] { 0.04f, 0.18f },
                    0.018f),
                RuntimeStingerKind.EchoReturn => CreatePatternToneClip(
                    "Stinger_EchoReturn",
                    new[] { 720f, 520f, 380f },
                    new[] { 0.055f, 0.075f, 0.12f },
                    0.026f),
                RuntimeStingerKind.RiskReward => CreatePatternToneClip(
                    "Stinger_RiskReward",
                    new[] { 420f, 580f, 310f },
                    new[] { 0.055f, 0.08f, 0.12f },
                    0.018f),
                RuntimeStingerKind.RhythmShift => CreatePatternToneClip(
                    "Stinger_RhythmShift",
                    new[] { 360f, 450f },
                    new[] { 0.07f, 0.11f },
                    0.04f),
                RuntimeStingerKind.SetPieceShift => CreatePatternToneClip(
                    "Stinger_SetPieceShift",
                    new[] { 180f, 260f, 380f, 240f },
                    new[] { 0.06f, 0.06f, 0.09f, 0.16f },
                    0.016f),
                RuntimeStingerKind.PressureWave => CreatePatternToneClip(
                    "Stinger_PressureWave",
                    new[] { 160f, 230f },
                    new[] { 0.08f, 0.16f },
                    0.012f),
                RuntimeStingerKind.Death => CreatePatternToneClip(
                    "Stinger_Death",
                    new[] { 210f, 130f, 82f },
                    new[] { 0.12f, 0.18f, 0.32f },
                    0.02f),
                _ => null
            };
        }

        private static AudioClip CreatePatternToneClip(string clipName, float[] frequencies, float[] durations, float gapSeconds)
        {
            if (frequencies == null || durations == null || frequencies.Length == 0 || durations.Length == 0)
            {
                return null;
            }

            int count = Mathf.Min(frequencies.Length, durations.Length);
            if (count <= 0)
            {
                return null;
            }

            const int sampleRate = 44100;
            int gapSamples = Mathf.Max(0, Mathf.RoundToInt(Mathf.Max(0f, gapSeconds) * sampleRate));
            int totalSamples = 0;
            for (int i = 0; i < count; i++)
            {
                totalSamples += Mathf.Max(2, Mathf.CeilToInt(Mathf.Max(0.03f, durations[i]) * sampleRate));
                if (i < count - 1)
                {
                    totalSamples += gapSamples;
                }
            }

            if (totalSamples <= 0)
            {
                return null;
            }

            float[] data = new float[totalSamples];
            int cursor = 0;
            for (int i = 0; i < count; i++)
            {
                int segmentSamples = Mathf.Max(2, Mathf.CeilToInt(Mathf.Max(0.03f, durations[i]) * sampleRate));
                float frequency = Mathf.Max(60f, frequencies[i]);
                for (int s = 0; s < segmentSamples && cursor + s < data.Length; s++)
                {
                    float t = s / (float)sampleRate;
                    float wave = Mathf.Sin(2f * Mathf.PI * frequency * t);
                    float envelope = EvaluateEnvelope(segmentSamples <= 1 ? 1f : s / (float)(segmentSamples - 1));
                    data[cursor + s] = wave * envelope * 0.9f;
                }

                cursor += segmentSamples;
                if (i < count - 1)
                {
                    cursor += gapSamples;
                }
            }

            AudioClip clip = AudioClip.Create(clipName, totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateToneClip(RuntimeEventType type, float frequency, float duration)
        {
            const int sampleRate = 44100;
            int sampleCount = Mathf.Max(2, Mathf.CeilToInt(duration * sampleRate));
            float[] data = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float wave = Mathf.Sin(2f * Mathf.PI * frequency * t);
                float envelope = EvaluateEnvelope(i / (float)(sampleCount - 1));
                data[i] = wave * envelope;
            }

            AudioClip clip = AudioClip.Create($"EvtTone_{type}", sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float EvaluateEnvelope(float normalizedTime)
        {
            float attack = Mathf.Clamp01(normalizedTime / 0.08f);
            float release = Mathf.Clamp01((1f - normalizedTime) / 0.22f);
            return Mathf.Min(attack, release);
        }

        private AudioClip GetAssignedClip(RuntimeEventType type)
        {
            return type switch
            {
                RuntimeEventType.Save => saveClip,
                RuntimeEventType.Load => loadClip,
                RuntimeEventType.Run => runClip,
                RuntimeEventType.Stage => stageClip,
                RuntimeEventType.Objective => objectiveClip,
                RuntimeEventType.Ability => abilityClip,
                RuntimeEventType.Death => deathClip,
                _ => null
            };
        }

        private AudioClip GetAssignedStingerClip(RuntimeStingerKind kind)
        {
            return kind switch
            {
                RuntimeStingerKind.ExitUnlocked => exitUnlockedStingerClip,
                RuntimeStingerKind.ChaseSpike => chaseSpikeStingerClip,
                RuntimeStingerKind.LockOnWarning => lockOnWarningStingerClip,
                RuntimeStingerKind.EscapeRelief => escapeReliefStingerClip,
                RuntimeStingerKind.QuietBreathBroken => quietBreathBrokenStingerClip,
                RuntimeStingerKind.EchoReturn => echoReturnStingerClip,
                RuntimeStingerKind.RiskReward => riskRewardStingerClip,
                RuntimeStingerKind.RhythmShift => rhythmShiftStingerClip,
                RuntimeStingerKind.SetPieceShift => setPieceShiftStingerClip,
                RuntimeStingerKind.PressureWave => pressureWaveStingerClip,
                RuntimeStingerKind.Death => deathStingerClip,
                _ => null
            };
        }

        private bool IsAssignedStingerClip(RuntimeStingerKind kind, AudioClip clip)
        {
            return clip != null && GetAssignedStingerClip(kind) == clip;
        }

        private int CountAssignedClips()
        {
            int count = 0;
            if (saveClip != null) count++;
            if (loadClip != null) count++;
            if (runClip != null) count++;
            if (stageClip != null) count++;
            if (objectiveClip != null) count++;
            if (abilityClip != null) count++;
            if (deathClip != null) count++;
            return count;
        }

        private int CountAssignedStingerClips()
        {
            int count = 0;
            if (exitUnlockedStingerClip != null) count++;
            if (chaseSpikeStingerClip != null) count++;
            if (lockOnWarningStingerClip != null) count++;
            if (escapeReliefStingerClip != null) count++;
            if (quietBreathBrokenStingerClip != null) count++;
            if (echoReturnStingerClip != null) count++;
            if (riskRewardStingerClip != null) count++;
            if (rhythmShiftStingerClip != null) count++;
            if (setPieceShiftStingerClip != null) count++;
            if (pressureWaveStingerClip != null) count++;
            if (deathStingerClip != null) count++;
            return count;
        }

        private EventAudioRule GetRule(RuntimeEventType type)
        {
            return type switch
            {
                RuntimeEventType.Save => saveRule,
                RuntimeEventType.Load => loadRule,
                RuntimeEventType.Run => runRule,
                RuntimeEventType.Stage => stageRule,
                RuntimeEventType.Objective => objectiveRule,
                RuntimeEventType.Ability => abilityRule,
                RuntimeEventType.Death => deathRule,
                _ => abilityRule
            };
        }

        private static EventAudioProfile GetProfile(RuntimeEventType type)
        {
            return type switch
            {
                RuntimeEventType.Death => new EventAudioProfile(180f, 0.24f, 1f),
                RuntimeEventType.Load => new EventAudioProfile(460f, 0.19f, 0.92f),
                RuntimeEventType.Run => new EventAudioProfile(390f, 0.14f, 0.68f),
                RuntimeEventType.Stage => new EventAudioProfile(620f, 0.16f, 0.58f),
                RuntimeEventType.Objective => new EventAudioProfile(740f, 0.13f, 0.55f),
                RuntimeEventType.Ability => new EventAudioProfile(840f, 0.09f, 0.42f),
                RuntimeEventType.Save => new EventAudioProfile(520f, 0.08f, 0.38f),
                _ => new EventAudioProfile(500f, 0.08f, 0.34f)
            };
        }
    }
}

























