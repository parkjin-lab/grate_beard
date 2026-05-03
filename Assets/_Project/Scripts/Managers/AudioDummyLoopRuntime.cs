using UnityEngine;

namespace LostBreadcrumbs.Runtime.Managers
{
    [DefaultExecutionOrder(-205)]
    public sealed class AudioDummyLoopRuntime : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AudioManager audioManager;
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource ambienceSource;

        [Header("Behavior")]
        [SerializeField] private bool autoGenerateIfClipMissing = true;
        [SerializeField] private bool autoPlayOnStart = true;
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

        private AudioClip generatedBgmClip;
        private AudioClip generatedAmbienceClip;
        private float nextReferenceResolveTime;

        public bool HasGeneratedBgmClip => generatedBgmClip != null;
        public bool HasGeneratedAmbienceClip => generatedAmbienceClip != null;
        public bool IsBgmPlaying => bgmSource != null && bgmSource.isPlaying;
        public bool IsAmbiencePlaying => ambienceSource != null && ambienceSource.isPlaying;
        public bool BgmUsingGeneratedClip => bgmSource != null && bgmSource.clip == generatedBgmClip;
        public bool AmbienceUsingGeneratedClip => ambienceSource != null && ambienceSource.clip == generatedAmbienceClip;
        public bool ForceDisableDummyLoops => forceDisableDummyLoops;

        private void Awake()
        {
            TryResolveRefs(force: true);
        }

        private void Start()
        {
            TryResolveRefs(force: true);

            if (forceDisableDummyLoops)
            {
                DisableGeneratedLoops();
                return;
            }

            EnsureDummyClips();

            if (autoPlayOnStart)
            {
                TryPlayLoop(bgmSource);
                TryPlayLoop(ambienceSource);
            }
        }

        private void Update()
        {
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
                TryPlayLoop(bgmSource);
                TryPlayLoop(ambienceSource);
            }

            if (adaptPitchWithDucking && audioManager != null)
            {
                float duck = audioManager.EffectiveDuck;
                if (bgmSource != null)
                {
                    bgmSource.pitch = Mathf.Lerp(1f, 1.06f, duck);
                }

                if (ambienceSource != null)
                {
                    ambienceSource.pitch = Mathf.Lerp(1f, 0.96f, duck);
                }
            }
        }

        public void SetSourcesForEditor(AudioManager manager, AudioSource bgm, AudioSource ambience)
        {
            audioManager = manager;
            bgmSource = bgm;
            ambienceSource = ambience;
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

        private static void TryPlayLoop(AudioSource source)
        {
            if (source == null || source.clip == null || source.isPlaying)
            {
                return;
            }

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

        private void ResolveRefs()
        {
            if (audioManager == null)
            {
                audioManager = AudioManager.Instance != null
                    ? AudioManager.Instance
                    : FindFirstObjectByType<AudioManager>();
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
