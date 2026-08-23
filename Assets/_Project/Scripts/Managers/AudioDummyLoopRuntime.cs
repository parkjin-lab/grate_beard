using UnityEngine;

namespace LostBreadcrumbs.Runtime.Managers
{
    [DefaultExecutionOrder(-205)]
    public sealed class AudioDummyLoopRuntime : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AudioManager audioManager;
        [SerializeField] private ThreatReadabilityDirector threatReadabilityDirector;
        [SerializeField] private GameplayRhythmDirector gameplayRhythmDirector;
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource ambienceSource;

        [Header("Behavior")]
        [SerializeField] private bool autoGenerateIfClipMissing = true;
        [SerializeField] private bool autoPlayOnStart = true;
        [SerializeField] private bool autoPlayAssignedClips;
        [SerializeField] private bool adaptPitchWithDucking = true;
        [SerializeField] private bool forceDisableDummyLoops;
        [SerializeField, Min(0.1f)] private float missingReferenceResolveInterval = 0.8f;

        [Header("BGM Loop")]
        [SerializeField, Min(0.5f)] private float bgmLoopSeconds = 2.6f;
        [SerializeField, Min(40f)] private float bgmBaseFrequency = 116f;
        [SerializeField, Min(40f)] private float bgmSecondaryFrequency = 174f;
        [SerializeField, Range(0f, 1f)] private float bgmAmplitude = 0.33f;

        [Header("Ambience Loop")]
        [SerializeField, Min(0.5f)] private float ambienceLoopSeconds = 3.4f;
        [SerializeField, Min(20f)] private float ambienceBaseFrequency = 72f;
        [SerializeField, Min(20f)] private float ambienceSecondaryFrequency = 98f;
        [SerializeField, Range(0f, 1f)] private float ambienceAmplitude = 0.27f;

        [Header("Dread Drone Layer")]
        [SerializeField] private bool enableGeneratedDreadLayer = true;
        [SerializeField, Min(0.5f)] private float dreadLayerLoopSeconds = 8f;
        [SerializeField, Range(20f, 90f)] private float dreadLayerBaseFrequency = 41f;
        [SerializeField, Range(20f, 120f)] private float dreadLayerSecondaryFrequency = 57f;
        [SerializeField, Range(0f, 1f)] private float dreadLayerWhisperAmount = 0.16f;
        [SerializeField, Range(0f, 1f)] private float dreadLayerMaxVolume = 0.075f;
        [SerializeField, Range(0f, 1f)] private float dreadLayerTensionFloor = 0.18f;
        [SerializeField, Range(0f, 1f)] private float dreadLayerReadabilityWeight = 0.72f;
        [SerializeField, Range(0f, 1f)] private float dreadLayerDuckWeight = 0.28f;
        [SerializeField, Range(0f, 1f)] private float dreadLayerRhythmWeight = 0.22f;
        [SerializeField, Range(0.5f, 1.2f)] private float dreadLayerLowPitch = 0.78f;
        [SerializeField, Range(0.5f, 1.2f)] private float dreadLayerHighPitch = 0.94f;
        [SerializeField, Min(0.1f)] private float dreadLayerFadeInSpeed = 1.25f;
        [SerializeField, Min(0.1f)] private float dreadLayerFadeOutSpeed = 2.2f;

        private const string DreadLayerSourceName = "DreadDrone_Runtime";

        private AudioClip generatedBgmClip;
        private AudioClip generatedAmbienceClip;
        private AudioClip generatedDreadLayerClip;
        private AudioSource dreadLayerSource;
        private float nextReferenceResolveTime;
        private float nextDreadLayerResolveTime;
        private float currentDreadLayerTension;
        private float releaseSettleUntil;
        private float releaseSettleDuration = 1.25f;
        private float cachedAmbienceVolume = -1f;

        public bool HasGeneratedBgmClip => generatedBgmClip != null;
        public bool HasGeneratedAmbienceClip => generatedAmbienceClip != null;
        public bool HasGeneratedDreadLayerClip => generatedDreadLayerClip != null;
        public bool IsBgmPlaying => bgmSource != null && bgmSource.isPlaying;
        public bool IsAmbiencePlaying => ambienceSource != null && ambienceSource.isPlaying;
        public bool IsDreadLayerPlaying => dreadLayerSource != null && dreadLayerSource.isPlaying;
        public bool BgmUsingGeneratedClip => generatedBgmClip != null && bgmSource != null && bgmSource.clip == generatedBgmClip;
        public bool AmbienceUsingGeneratedClip => generatedAmbienceClip != null && ambienceSource != null && ambienceSource.clip == generatedAmbienceClip;
        public float CurrentDreadLayerTension => currentDreadLayerTension;
        public float CurrentRhythmTempo => gameplayRhythmDirector != null ? gameplayRhythmDirector.CurrentTempo01 : 0f;
        public bool ForceDisableDummyLoops => forceDisableDummyLoops;
        public float CurrentReleaseAmbientSettle => EvaluateReleaseAmbientSettle01();

        private void Awake()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            TryResolveRefs(force: true);
            TryResolveDreadLayerRefs(force: true);
        }

        private void Start()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            TryResolveRefs(force: true);
            TryResolveDreadLayerRefs(force: true);

            if (forceDisableDummyLoops)
            {
                DisableGeneratedLoops();
                return;
            }

            EnsureDummyClips();

            if (autoPlayOnStart)
            {
                TryPlayManagedLoop(bgmSource, generatedBgmClip);
                TryPlayManagedLoop(ambienceSource, generatedAmbienceClip);
            }

            UpdateDreadLayer();
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            TryResolveRefs();

            if (forceDisableDummyLoops)
            {
                DisableGeneratedLoops();
                return;
            }

            if (autoGenerateIfClipMissing)
            {
                EnsureDummyClips();
            }

            if (autoPlayOnStart)
            {
                TryPlayManagedLoop(bgmSource, generatedBgmClip);
                TryPlayManagedLoop(ambienceSource, generatedAmbienceClip);
            }

            if (adaptPitchWithDucking && audioManager != null)
            {
                float duck = audioManager.EffectiveDuck;
                float rhythmTempo = CurrentRhythmTempo;
                if (bgmSource != null)
                {
                    bgmSource.pitch = Mathf.Lerp(1f, 1.06f, duck) * Mathf.Lerp(0.97f, 1.04f, rhythmTempo);
                }

                if (ambienceSource != null)
                {
                    ambienceSource.pitch = Mathf.Lerp(1f, 0.96f, duck) * Mathf.Lerp(0.99f, 0.94f, rhythmTempo);
                }
            }

            ApplyReleaseAmbientSettleMix();
            UpdateDreadLayer();
        }

        public void BeginReleaseAmbientSettle(float durationSeconds)
        {
            releaseSettleDuration = Mathf.Clamp(durationSeconds, 1f, 1.5f);
            releaseSettleUntil = Time.unscaledTime + releaseSettleDuration;
            if (ambienceSource != null && cachedAmbienceVolume < 0f)
            {
                cachedAmbienceVolume = ambienceSource.volume;
            }
        }

        public void SetSourcesForEditor(
            AudioManager manager,
            AudioSource bgm,
            AudioSource ambience,
            GameplayRhythmDirector rhythmDirector = null)
        {
            audioManager = manager;
            bgmSource = bgm;
            ambienceSource = ambience;
            if (rhythmDirector != null)
            {
                gameplayRhythmDirector = rhythmDirector;
            }
        }

        public void SetForceDisableDummyLoopsForEditor(bool forceDisable)
        {
            forceDisableDummyLoops = forceDisable;

            if (forceDisableDummyLoops)
            {
                DisableGeneratedLoops();
            }
        }

        private void DisableGeneratedLoops()
        {
            ResetPitch();
            DisableGeneratedLoopOnSource(bgmSource, generatedBgmClip);
            DisableGeneratedLoopOnSource(ambienceSource, generatedAmbienceClip);
            DisableDreadLayer();
        }

        private static void DisableGeneratedLoopOnSource(AudioSource source, AudioClip generatedClip)
        {
            if (source == null || source.clip != generatedClip)
            {
                return;
            }

            if (source.isPlaying)
            {
                source.Stop();
            }

            source.clip = null;
        }

        private void ResetPitch()
        {
            if (bgmSource != null)
            {
                bgmSource.pitch = 1f;
            }

            if (ambienceSource != null)
            {
                ambienceSource.pitch = 1f;
            }

            if (dreadLayerSource != null)
            {
                dreadLayerSource.pitch = 1f;
            }
        }

        private void EnsureDummyClips()
        {
            if (bgmSource != null && bgmSource.clip == null && autoGenerateIfClipMissing)
            {
                generatedBgmClip ??= CreateLoopClip("DummyBGM", bgmLoopSeconds, bgmBaseFrequency, bgmSecondaryFrequency, bgmAmplitude);
                bgmSource.clip = generatedBgmClip;
                bgmSource.loop = true;
            }

            if (ambienceSource != null && ambienceSource.clip == null && autoGenerateIfClipMissing)
            {
                generatedAmbienceClip ??= CreateLoopClip("DummyAmbience", ambienceLoopSeconds, ambienceBaseFrequency, ambienceSecondaryFrequency, ambienceAmplitude);
                ambienceSource.clip = generatedAmbienceClip;
                ambienceSource.loop = true;
            }
        }

        private void UpdateDreadLayer()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (!enableGeneratedDreadLayer)
            {
                DisableDreadLayer();
                return;
            }

            TryResolveDreadLayerRefs();
            EnsureDreadLayer();

            if (dreadLayerSource == null || dreadLayerSource.clip == null)
            {
                return;
            }

            float targetTension = EvaluateDreadLayerTension();
            float deltaTime = Mathf.Max(0f, Time.unscaledDeltaTime);
            float fadeSpeed = targetTension >= currentDreadLayerTension
                ? dreadLayerFadeInSpeed
                : dreadLayerFadeOutSpeed;

            currentDreadLayerTension = Mathf.MoveTowards(
                currentDreadLayerTension,
                targetTension,
                Mathf.Max(0.1f, fadeSpeed) * deltaTime);

            float audibility = EvaluateDreadLayerAudibility(currentDreadLayerTension);
            float settle = EvaluateReleaseAmbientSettle01();
            dreadLayerSource.volume = Mathf.Clamp01(dreadLayerMaxVolume) * audibility * Mathf.Lerp(1f, 0.18f, settle);
            dreadLayerSource.pitch = Mathf.Lerp(
                Mathf.Min(dreadLayerLowPitch, dreadLayerHighPitch),
                Mathf.Max(dreadLayerLowPitch, dreadLayerHighPitch),
                audibility);

            if (autoPlayOnStart && !dreadLayerSource.isPlaying)
            {
                dreadLayerSource.Play();
            }
        }

        private void EnsureDreadLayer()
        {
            if (!Application.isPlaying || forceDisableDummyLoops || !enableGeneratedDreadLayer)
            {
                return;
            }

            if (dreadLayerSource == null)
            {
                Transform existingSource = transform.Find(DreadLayerSourceName);
                if (existingSource != null)
                {
                    dreadLayerSource = existingSource.GetComponent<AudioSource>();
                }

                if (dreadLayerSource == null)
                {
                    GameObject sourceObject = new(DreadLayerSourceName);
                    sourceObject.transform.SetParent(transform, false);
                    dreadLayerSource = sourceObject.AddComponent<AudioSource>();
                }
            }

            generatedDreadLayerClip ??= CreateDreadLayerClip(
                "GeneratedDreadDrone",
                dreadLayerLoopSeconds,
                dreadLayerBaseFrequency,
                dreadLayerSecondaryFrequency,
                dreadLayerWhisperAmount);

            dreadLayerSource.enabled = true;
            dreadLayerSource.playOnAwake = false;
            dreadLayerSource.loop = true;
            dreadLayerSource.spatialBlend = 0f;
            dreadLayerSource.dopplerLevel = 0f;
            dreadLayerSource.priority = 190;

            if (dreadLayerSource.clip != generatedDreadLayerClip)
            {
                dreadLayerSource.clip = generatedDreadLayerClip;
            }
        }

        private void DisableDreadLayer()
        {
            currentDreadLayerTension = 0f;

            if (dreadLayerSource == null)
            {
                return;
            }

            dreadLayerSource.volume = 0f;
            dreadLayerSource.pitch = 1f;

            if (dreadLayerSource.isPlaying)
            {
                dreadLayerSource.Stop();
            }

            if (dreadLayerSource.clip == generatedDreadLayerClip)
            {
                dreadLayerSource.clip = null;
            }

            dreadLayerSource.enabled = false;
        }

        private float EvaluateDreadLayerTension()
        {
            float readabilityPressure = threatReadabilityDirector != null
                ? threatReadabilityDirector.CurrentReadabilityPressure
                : 0f;
            float duckPressure = audioManager != null ? audioManager.EffectiveDuck : 0f;
            float rhythmPressure = gameplayRhythmDirector != null ? gameplayRhythmDirector.CurrentRhythmIntensity : 0f;

            float readabilityWeight = Mathf.Clamp01(dreadLayerReadabilityWeight);
            float duckWeight = Mathf.Clamp01(dreadLayerDuckWeight);
            float rhythmWeight = Mathf.Clamp01(dreadLayerRhythmWeight);
            float totalWeight = readabilityWeight + duckWeight + rhythmWeight;

            if (totalWeight <= 0.0001f)
            {
                return 0f;
            }

            return Mathf.Clamp01(
                ((readabilityPressure * readabilityWeight)
                    + (duckPressure * duckWeight)
                    + (rhythmPressure * rhythmWeight))
                / totalWeight);
        }

        private void ApplyReleaseAmbientSettleMix()
        {
            if (ambienceSource == null)
            {
                return;
            }

            float settle = EvaluateReleaseAmbientSettle01();
            if (cachedAmbienceVolume < 0f)
            {
                return;
            }

            ambienceSource.volume = cachedAmbienceVolume * Mathf.Lerp(1f, 0.52f, settle);
            if (settle <= 0f)
            {
                cachedAmbienceVolume = -1f;
            }
        }

        private float EvaluateReleaseAmbientSettle01()
        {
            if (releaseSettleUntil <= 0f)
            {
                return 0f;
            }

            float remaining = releaseSettleUntil - Time.unscaledTime;
            if (remaining <= 0f)
            {
                releaseSettleUntil = 0f;
                return 0f;
            }

            float duration = Mathf.Max(0.1f, releaseSettleDuration);
            float elapsed = duration - remaining;
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.2f));
            float fall = 1f - Mathf.SmoothStep(0.62f, 1f, Mathf.Clamp01(elapsed / duration));
            return Mathf.Clamp01(rise * fall);
        }

        private float EvaluateDreadLayerAudibility(float tension)
        {
            if (dreadLayerTensionFloor >= 0.999f)
            {
                return tension >= 1f ? 1f : 0f;
            }

            float audibility = Mathf.InverseLerp(Mathf.Clamp01(dreadLayerTensionFloor), 1f, tension);
            return Mathf.SmoothStep(0f, 1f, audibility);
        }

        private void TryPlayManagedLoop(AudioSource source, AudioClip generatedClip)
        {
            if (source == null || source.clip == null || source.isPlaying)
            {
                return;
            }

            if (!autoPlayAssignedClips && source.clip != generatedClip)
            {
                return;
            }

            source.loop = true;
            source.Play();
        }

        private static AudioClip CreateLoopClip(string clipName, float durationSeconds, float baseFrequency, float secondaryFrequency, float amplitude)
        {
            const int sampleRate = 44100;
            int sampleCount = Mathf.Max(2, Mathf.CeilToInt(durationSeconds * sampleRate));
            float[] data = new float[sampleCount];

            float amp = Mathf.Clamp(amplitude, 0f, 1f);
            float primaryAmp = amp * 0.72f;
            float secondaryAmp = amp * 0.38f;
            float tertiaryAmp = amp * 0.2f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float primary = Mathf.Sin(2f * Mathf.PI * baseFrequency * t) * primaryAmp;
                float secondary = Mathf.Sin(2f * Mathf.PI * secondaryFrequency * t + 0.7f) * secondaryAmp;
                float tertiary = Mathf.Sin(2f * Mathf.PI * (secondaryFrequency * 0.5f) * t + 1.2f) * tertiaryAmp;

                float blend = primary + secondary + tertiary;
                data[i] = Mathf.Clamp(blend, -0.95f, 0.95f);
            }

            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateDreadLayerClip(string clipName, float durationSeconds, float baseFrequency, float secondaryFrequency, float whisperAmount)
        {
            const int sampleRate = 44100;
            int sampleCount = Mathf.Max(2, Mathf.CeilToInt(durationSeconds * sampleRate));
            float[] data = new float[sampleCount];

            float primaryFrequency = Mathf.Max(1f, baseFrequency);
            float secondaryFrequencySafe = Mathf.Max(1f, secondaryFrequency);
            float whisperAmp = Mathf.Clamp01(whisperAmount) * 0.18f;
            float edgeFadeSamples = Mathf.Max(1f, sampleRate * 0.08f);
            float filteredWhisper = 0f;
            System.Random whisperRandom = new(739391);

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float slowBreath = 0.55f + (0.45f * Mathf.Sin(2f * Mathf.PI * 0.11f * t + 0.4f));
                float primary = Mathf.Sin(2f * Mathf.PI * primaryFrequency * t) * 0.34f;
                float secondary = Mathf.Sin(2f * Mathf.PI * secondaryFrequencySafe * t + 1.1f) * 0.18f;
                float undertone = Mathf.Sin(2f * Mathf.PI * (primaryFrequency * 0.5f) * t + 2.3f) * 0.16f;

                float whisperNoise = ((float)whisperRandom.NextDouble() * 2f) - 1f;
                filteredWhisper = Mathf.Lerp(filteredWhisper, whisperNoise, 0.018f);

                float edge = Mathf.Min(i, sampleCount - 1 - i) / edgeFadeSamples;
                float edgeFade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge));
                float drone = (primary + secondary + undertone) * (0.78f + (slowBreath * 0.22f));
                float whisper = filteredWhisper * whisperAmp * slowBreath;

                data[i] = Mathf.Clamp((drone + whisper) * edgeFade, -0.95f, 0.95f);
            }

            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void ResolveRefs()
        {
            if (audioManager == null)
            {
                audioManager = AudioManager.Instance != null
                    ? AudioManager.Instance
                    : FindFirstObjectByType<AudioManager>();
            }

            if (gameplayRhythmDirector == null)
            {
                gameplayRhythmDirector = FindFirstObjectByType<GameplayRhythmDirector>();
            }

            if (bgmSource == null || ambienceSource == null)
            {
                Transform root = FindAudioEmittersRoot();
                if (root != null)
                {
                    if (bgmSource == null)
                    {
                        Transform bgm = root.Find("BGM_Dummy");
                        if (bgm != null)
                        {
                            bgmSource = bgm.GetComponent<AudioSource>();
                        }
                    }

                    if (ambienceSource == null)
                    {
                        Transform ambience = root.Find("Ambience_Dummy");
                        if (ambience != null)
                        {
                            ambienceSource = ambience.GetComponent<AudioSource>();
                        }
                    }
                }
            }
        }

        private void TryResolveRefs(bool force = false)
        {
            if (!force)
            {
                if (HasAllReferences())
                {
                    return;
                }

                if (Time.unscaledTime < nextReferenceResolveTime)
                {
                    return;
                }

                nextReferenceResolveTime = Time.unscaledTime + Mathf.Max(0.1f, missingReferenceResolveInterval);
            }

            ResolveRefs();
        }

        private void TryResolveDreadLayerRefs(bool force = false)
        {
            if (!Application.isPlaying || (threatReadabilityDirector != null && gameplayRhythmDirector != null))
            {
                return;
            }

            if (!force)
            {
                if (Time.unscaledTime < nextDreadLayerResolveTime)
                {
                    return;
                }

                nextDreadLayerResolveTime = Time.unscaledTime + Mathf.Max(0.1f, missingReferenceResolveInterval);
            }

            if (threatReadabilityDirector == null)
            {
                threatReadabilityDirector = FindFirstObjectByType<ThreatReadabilityDirector>();
            }

            if (gameplayRhythmDirector == null)
            {
                gameplayRhythmDirector = FindFirstObjectByType<GameplayRhythmDirector>();
            }
        }

        private bool HasAllReferences()
        {
            return audioManager != null && bgmSource != null && ambienceSource != null;
        }

        private static Transform FindAudioEmittersRoot()
        {
            GameObject root = GameObject.Find("Scene_Root");
            if (root == null)
            {
                return null;
            }

            Transform gameRoot = root.transform.Find("GameRoot");
            if (gameRoot == null)
            {
                return null;
            }

            Transform runtime = gameRoot.Find("Runtime");
            return runtime != null ? runtime.Find("AudioEmitters") : null;
        }
    }
}
