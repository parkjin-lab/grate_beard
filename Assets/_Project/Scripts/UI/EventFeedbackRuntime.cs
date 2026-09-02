using LostBreadcrumbs.Runtime.Core;
using LostBreadcrumbs.Runtime.Events;
using LostBreadcrumbs.Runtime.Map;
using LostBreadcrumbs.Runtime.Player;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LostBreadcrumbs.Runtime.UI
{
    [DefaultExecutionOrder(120)]
    public sealed class EventFeedbackRuntime : MonoBehaviour
    {
        private readonly struct PriorityCuePayload
        {
            public PriorityCuePayload(string text, Color backgroundColor, Color textColor, int stage)
            {
                Text = string.IsNullOrWhiteSpace(text) ? "ALERT" : text.Trim();
                BackgroundColor = backgroundColor;
                TextColor = textColor;
                Stage = Mathf.Max(1, stage);
            }

            public string Text { get; }
            public Color BackgroundColor { get; }
            public Color TextColor { get; }
            public int Stage { get; }
        }

        [Header("Flash Overlay")]
        [SerializeField] private bool enableScreenFlash = true;
        [SerializeField] private Canvas targetCanvas;
        [SerializeField, Min(0.5f)] private float flashFadeSpeed = 4.2f;

        [Header("Camera Impulse")]
        [SerializeField] private bool enableCameraImpulse = true;
        [SerializeField] private CameraFollow2D cameraFollow;

        [Header("Death Recap Toast")]
        [SerializeField] private bool enableDeathRecapToast = true;
        [SerializeField, Min(0.4f)] private float deathRecapDuration = 3.1f;
        [SerializeField, Min(0f)] private float deathRecapHoldSeconds = 1.1f;
        [SerializeField, Min(0.5f)] private float deathRecapFadeSpeed = 6f;
        [SerializeField, Min(0.5f)] private float deathRecapFadeOutSpeed = 4.2f;
        [SerializeField] private Vector2 deathRecapSize = new(980f, 112f);
        [SerializeField] private Vector2 deathRecapOffset = new(0f, -72f);
        [SerializeField, Min(12)] private int deathRecapFontSize = 20;

        [Header("Priority Cue Toast")]
        [SerializeField] private bool enablePriorityCueToast = true;
        [SerializeField, Min(0.5f)] private float priorityCueDuration = 1.8f;
        [SerializeField, Min(0.1f)] private float priorityCueFadeSpeed = 7.2f;
        [SerializeField, Min(0f)] private float priorityCueHoldSeconds = 0.55f;
        [SerializeField, Min(0f)] private float priorityCueDuplicateSuppressSeconds = 0.9f;
        [SerializeField, Min(1)] private int maxQueuedPriorityCues = 4;
        [SerializeField] private Vector2 priorityCueSize = new(740f, 52f);
        [SerializeField] private Vector2 priorityCueOffset = new(0f, -16f);
        [SerializeField, Min(12)] private int priorityCueFontSize = 22;

        [Header("Stage-Scaled Cue Tuning")]
        [SerializeField] private bool useStageScaledCueTuning = true;
        [SerializeField, Min(1)] private int cueScaleStartStage = 1;
        [SerializeField, Min(2)] private int cueScalePeakStage = 7;
        [SerializeField, Range(0.6f, 1.6f)] private float priorityCueDurationStartScale = 0.96f;
        [SerializeField, Range(0.6f, 1.6f)] private float priorityCueDurationPeakScale = 1.22f;
        [SerializeField, Range(0.6f, 1.6f)] private float priorityCueHoldStartScale = 0.95f;
        [SerializeField, Range(0.6f, 1.6f)] private float priorityCueHoldPeakScale = 1.16f;
        [SerializeField, Range(0.6f, 1.8f)] private float priorityCueFadeSpeedStartScale = 1f;
        [SerializeField, Range(0.6f, 1.8f)] private float priorityCueFadeSpeedPeakScale = 1.15f;
        [SerializeField, Range(0f, 0.35f)] private float priorityCueAlphaBoostAtPeak = 0.12f;
        [SerializeField, Range(0.9f, 1.2f)] private float priorityCueFontScaleAtPeak = 1.08f;
        [SerializeField, Range(0.6f, 1.6f)] private float deathRecapDurationStartScale = 1f;
        [SerializeField, Range(0.6f, 1.6f)] private float deathRecapDurationPeakScale = 1.2f;
        [SerializeField, Range(0.6f, 1.6f)] private float deathRecapHoldStartScale = 1f;
        [SerializeField, Range(0.6f, 1.6f)] private float deathRecapHoldPeakScale = 1.16f;
        [SerializeField, Range(0.9f, 1.2f)] private float deathRecapFontScaleAtPeak = 1.08f;

        [Header("Debug")]
        [SerializeField] private bool logEvents;
        [SerializeField, Min(0.1f)] private float missingReferenceResolveInterval = 0.8f;

        private Image flashImage;
        private Color flashColor = Color.clear;
        private float flashAlpha;

        private StageLoopDirector stageLoopDirector;
        private PlayerVitalSystem playerVitalSystem;
        private CanvasGroup deathRecapGroup;
        private Text deathRecapText;
        private Image deathRecapBackground;
        private float deathRecapHoldUntil;
        private float deathRecapHideAt;
        private Font runtimeFont;

        private CanvasGroup priorityCueGroup;
        private Text priorityCueText;
        private Image priorityCueBackground;
        private readonly Queue<PriorityCuePayload> priorityCueQueue = new();
        private float priorityCueHoldUntil;
        private float priorityCueHideAt;
        private float activePriorityCueFadeSpeed;
        private string lastPriorityCueText = string.Empty;
        private float priorityCueDuplicateSuppressUntil;
        private float nextReferenceResolveTime;

        private void Awake()
        {
            TryResolveReferences(force: true);
            EnsureOverlay();
        }

        private void OnEnable()
        {
            priorityCueQueue.Clear();
            lastPriorityCueText = string.Empty;
            priorityCueDuplicateSuppressUntil = 0f;
            activePriorityCueFadeSpeed = Mathf.Max(0.5f, priorityCueFadeSpeed);
            RuntimeEventBus.EventRaised -= HandleRuntimeEvent;
            RuntimeEventBus.EventRaised += HandleRuntimeEvent;
        }

        private void OnDisable()
        {
            priorityCueQueue.Clear();
            RuntimeEventBus.EventRaised -= HandleRuntimeEvent;
        }

        private void Update()
        {
            if (flashImage != null)
            {
                flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, flashFadeSpeed * Time.deltaTime);
                Color color = flashColor;
                color.a = flashAlpha;
                flashImage.color = color;
            }

            TickDeathRecapFade();
            TickPriorityCueFade();
        }

        public void SetCameraForEditor(CameraFollow2D targetCameraFollow)
        {
            cameraFollow = targetCameraFollow;
        }

        public void SetTargetCanvasForEditor(Canvas canvasRef)
        {
            targetCanvas = canvasRef;
        }

        public void SetRuntimeSourcesForEditor(PlayerVitalSystem vitalSystem, StageLoopDirector loopDirector)
        {
            playerVitalSystem = vitalSystem;
            stageLoopDirector = loopDirector;
        }

        private void HandleRuntimeEvent(RuntimeEventRecord record)
        {
            if (!record.IsValid)
            {
                return;
            }

            TryResolveReferences();

            GetEventFeedback(record.Type, out Color color, out float alpha, out float impulseAmplitude, out float impulseDuration);

            if (enableScreenFlash)
            {
                TriggerFlash(color, alpha);
            }

            if (enableCameraImpulse && cameraFollow != null && impulseAmplitude > 0f)
            {
                cameraFollow.AddImpulse(impulseAmplitude, impulseDuration);
            }

            if (enableDeathRecapToast && record.Type == RuntimeEventType.Death)
            {
                ShowDeathRecap(record);
            }

            if (enablePriorityCueToast)
            {
                TryQueuePriorityCue(record);
            }

            if (logEvents)
            {
                Debug.Log($"[EventFeedback] {record.Type} -> flash:{alpha:0.00}, shake:{impulseAmplitude:0.00}", this);
            }
        }

        private void TriggerFlash(Color color, float alpha)
        {
            if (flashImage == null)
            {
                EnsureOverlay();
            }

            if (flashImage == null)
            {
                return;
            }

            flashColor = color;
            flashAlpha = Mathf.Max(flashAlpha, Mathf.Clamp01(alpha));

            Color shown = flashColor;
            shown.a = flashAlpha;
            flashImage.color = shown;
        }

        private void GetEventFeedback(RuntimeEventType type, out Color color, out float alpha, out float impulseAmplitude, out float impulseDuration)
        {
            color = new Color(0.6f, 0.8f, 1f, 0f);
            alpha = 0.1f;
            impulseAmplitude = 0f;
            impulseDuration = 0.12f;

            switch (type)
            {
                case RuntimeEventType.Death:
                    color = new Color(1f, 0.1f, 0.12f, 0f);
                    alpha = 0.36f;
                    impulseAmplitude = 0.44f;
                    impulseDuration = 0.3f;
                    break;
                case RuntimeEventType.Stage:
                    color = new Color(0.95f, 0.95f, 1f, 0f);
                    alpha = 0.22f;
                    impulseAmplitude = 0.2f;
                    impulseDuration = 0.22f;
                    break;
                case RuntimeEventType.Objective:
                    color = new Color(0.35f, 1f, 0.62f, 0f);
                    alpha = 0.18f;
                    impulseAmplitude = 0.12f;
                    impulseDuration = 0.16f;
                    break;
                case RuntimeEventType.Ability:
                    color = new Color(0.35f, 0.8f, 1f, 0f);
                    alpha = 0.13f;
                    impulseAmplitude = 0.1f;
                    impulseDuration = 0.1f;
                    break;
                case RuntimeEventType.Load:
                    color = new Color(0.8f, 1f, 1f, 0f);
                    alpha = 0.24f;
                    impulseAmplitude = 0.22f;
                    impulseDuration = 0.2f;
                    break;
                case RuntimeEventType.Run:
                    color = new Color(1f, 0.85f, 0.35f, 0f);
                    alpha = 0.19f;
                    impulseAmplitude = 0.14f;
                    impulseDuration = 0.14f;
                    break;
                case RuntimeEventType.Save:
                    color = new Color(0.3f, 0.95f, 1f, 0f);
                    alpha = 0.12f;
                    impulseAmplitude = 0f;
                    impulseDuration = 0.08f;
                    break;
            }
        }

        private void ShowDeathRecap(RuntimeEventRecord record)
        {
            EnsureDeathRecapToast();
            if (deathRecapText == null || deathRecapGroup == null)
            {
                return;
            }

            int stage = ResolveEventStage(record);
            float stageIntensity01 = EvaluateStageCueIntensity01(stage);
            float pressure = playerVitalSystem != null ? playerVitalSystem.LastDeathPressureSnapshot : 0f;
            deathRecapText.text = BuildDeathRecapMessage(stage, pressure);
            UpdateDeathRecapTheme(pressure, stageIntensity01);
            deathRecapText.fontSize = Mathf.Max(12, Mathf.RoundToInt(deathRecapFontSize * Mathf.Lerp(1f, deathRecapFontScaleAtPeak, stageIntensity01)));

            float now = Time.unscaledTime;
            float holdScale = useStageScaledCueTuning
                ? Mathf.Lerp(deathRecapHoldStartScale, deathRecapHoldPeakScale, stageIntensity01)
                : 1f;
            float durationScale = useStageScaledCueTuning
                ? Mathf.Lerp(deathRecapDurationStartScale, deathRecapDurationPeakScale, stageIntensity01)
                : 1f;
            deathRecapHoldUntil = now + Mathf.Max(0f, deathRecapHoldSeconds * holdScale);
            deathRecapHideAt = deathRecapHoldUntil + Mathf.Max(0.4f, deathRecapDuration * durationScale);
            deathRecapGroup.alpha = 1f;
        }

        private string BuildDeathRecapMessage(int stage, float pressure)
        {
            string cause = playerVitalSystem != null ? playerVitalSystem.LastDeathCause : "Unknown impact";
            string missed = playerVitalSystem != null ? playerVitalSystem.LastDeathMissedOption : "No tactical option";

            if (string.IsNullOrWhiteSpace(cause))
            {
                cause = "Unknown impact";
            }

            if (string.IsNullOrWhiteSpace(missed))
            {
                missed = "No tactical option";
            }

            string compactCause = TrimRecapToken(cause, 44);
            string compactMissed = TrimRecapToken(missed, 44);
            string tip = BuildDeathTip(cause, missed, pressure);
            int safeStage = Mathf.Max(1, stage);
            return $"DEATH RECAP  Stage {safeStage} | Pressure {pressure:0.00}\nCause: {compactCause}\nTip: {TrimRecapToken($"{compactMissed}. {tip}", 92)}";
        }

        private static string BuildDeathTip(string cause, string missed, float pressure)
        {
            string merged = $"{cause} {missed}";
            if (ContainsKeyword(merged, "smoke"))
            {
                return "Smoke first, then break line-of-sight.";
            }

            if (ContainsKeyword(merged, "decoy"))
            {
                return "Throw decoy to split enemy vectors.";
            }

            if (ContainsKeyword(merged, "stamina") || ContainsKeyword(merged, "sprint"))
            {
                return "Save stamina for last-corner disengage.";
            }

            if (pressure >= 0.8f)
            {
                return "Pressure is high: choose shorter routes and avoid open cells.";
            }

            return "Use Echo first, then commit to a single safe route.";
        }

        private void UpdateDeathRecapTheme(float pressure, float stageIntensity01)
        {
            if (deathRecapBackground == null || deathRecapText == null)
            {
                return;
            }

            float t = Mathf.Clamp01(pressure * 0.72f + stageIntensity01 * 0.28f);
            deathRecapBackground.color = Color.Lerp(
                new Color(0.1f, 0.07f, 0.08f, 0.8f),
                new Color(0.32f, 0.07f, 0.08f, 0.9f),
                t);
            deathRecapText.color = Color.Lerp(
                new Color(1f, 0.92f, 0.9f, 1f),
                new Color(1f, 0.8f, 0.78f, 1f),
                t);
        }

        private static string TrimRecapToken(string source, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return "-";
            }

            string trimmed = source.Trim();
            int safeLength = Mathf.Max(8, maxLength);
            if (trimmed.Length <= safeLength)
            {
                return trimmed;
            }

            return trimmed.Substring(0, safeLength - 1) + "...";
        }

        private void TickDeathRecapFade()
        {
            if (deathRecapGroup == null)
            {
                return;
            }

            float now = Time.unscaledTime;
            float target;
            if (now <= deathRecapHoldUntil)
            {
                target = 1f;
            }
            else if (now >= deathRecapHideAt)
            {
                target = 0f;
            }
            else
            {
                float t = Mathf.InverseLerp(deathRecapHoldUntil, deathRecapHideAt, now);
                target = 1f - t;
            }

            float fadeSpeed = target < deathRecapGroup.alpha
                ? Mathf.Max(0.5f, deathRecapFadeOutSpeed)
                : Mathf.Max(0.5f, deathRecapFadeSpeed);
            deathRecapGroup.alpha = Mathf.MoveTowards(deathRecapGroup.alpha, target, fadeSpeed * Time.unscaledDeltaTime);
        }

        private void TryQueuePriorityCue(RuntimeEventRecord record)
        {
            if (!TryBuildPriorityCue(record, out PriorityCuePayload payload))
            {
                return;
            }

            float now = Time.unscaledTime;
            string normalizedText = payload.Text.Trim();
            bool duplicated = string.Equals(lastPriorityCueText, normalizedText, System.StringComparison.OrdinalIgnoreCase)
                             && now < priorityCueDuplicateSuppressUntil;
            if (duplicated)
            {
                return;
            }

            lastPriorityCueText = normalizedText;
            priorityCueDuplicateSuppressUntil = now + Mathf.Max(0f, priorityCueDuplicateSuppressSeconds);

            int safeQueueCap = Mathf.Max(1, maxQueuedPriorityCues);
            while (priorityCueQueue.Count >= safeQueueCap)
            {
                priorityCueQueue.Dequeue();
            }

            priorityCueQueue.Enqueue(payload);
            if (priorityCueGroup != null && priorityCueGroup.alpha <= 0.001f)
            {
                ShowNextPriorityCue();
            }
        }

        private bool TryBuildPriorityCue(RuntimeEventRecord record, out PriorityCuePayload payload)
        {
            payload = default;
            if (!record.IsValid)
            {
                return false;
            }

            int stage = ResolveEventStage(record);
            string message = record.Message ?? string.Empty;
            if (message.Contains("멀리까지 들릴 수 있다"))
            {
                payload = new PriorityCuePayload("멀리까지 들릴 수 있다", new Color(0.16f, 0.22f, 0.38f, 0.9f), new Color(0.88f, 0.94f, 1f, 1f), stage);
                return true;
            }

            if (record.Semantic == RuntimeEventSemantic.RhythmShift && IsBuildReturnCue(message))
            {
                payload = new PriorityCuePayload("다시 빨라진다", new Color(0.34f, 0.12f, 0.08f, 0.9f), new Color(1f, 0.9f, 0.82f, 1f), stage);
                return true;
            }

            if (record.Semantic == RuntimeEventSemantic.EscapeRelief && message.Contains("숨이 트인다"))
            {
                payload = new PriorityCuePayload("숨이 트인다", new Color(0.08f, 0.28f, 0.24f, 0.9f), new Color(0.84f, 1f, 0.94f, 1f), stage);
                return true;
            }

            payload = record.Semantic switch
            {
                RuntimeEventSemantic.ExitUnlocked => new PriorityCuePayload("출구 열림 - 지금 나가", new Color(0.08f, 0.4f, 0.22f, 0.9f), new Color(0.92f, 1f, 0.92f, 1f), stage),
                RuntimeEventSemantic.LockOnWarning => new PriorityCuePayload("곧 덮쳐온다", new Color(0.62f, 0.28f, 0.05f, 0.9f), new Color(1f, 0.93f, 0.84f, 1f), stage),
                RuntimeEventSemantic.ChaseStarted => new PriorityCuePayload("쫓긴다", new Color(0.62f, 0.1f, 0.1f, 0.92f), new Color(1f, 0.88f, 0.88f, 1f), stage),
                RuntimeEventSemantic.ChaseDisengaged => new PriorityCuePayload("따돌렸다", new Color(0.2f, 0.28f, 0.52f, 0.9f), new Color(0.9f, 0.94f, 1f, 1f), stage),
                RuntimeEventSemantic.EscapeRelief => new PriorityCuePayload("숨 돌릴 틈", new Color(0.08f, 0.28f, 0.24f, 0.9f), new Color(0.84f, 1f, 0.94f, 1f), stage),
                RuntimeEventSemantic.QuietBreathBroken => new PriorityCuePayload("숨이 흐트러졌다", new Color(0.58f, 0.16f, 0.08f, 0.92f), new Color(1f, 0.9f, 0.82f, 1f), stage),
                RuntimeEventSemantic.EchoReturn => new PriorityCuePayload("메아리가 돌아온다", new Color(0.5f, 0.08f, 0.08f, 0.9f), new Color(1f, 0.86f, 0.82f, 1f), stage),
                RuntimeEventSemantic.EchoChoiceScan => new PriorityCuePayload("갈림길이 보인다", new Color(0.18f, 0.22f, 0.34f, 0.9f), new Color(0.9f, 0.96f, 1f, 1f), stage),
                RuntimeEventSemantic.RiskReward => new PriorityCuePayload("위험 보상 획득", new Color(0.58f, 0.18f, 0.06f, 0.92f), new Color(1f, 0.9f, 0.78f, 1f), stage),
                RuntimeEventSemantic.SafeHavenThin => new PriorityCuePayload("안식처가 흔들린다", new Color(0.42f, 0.08f, 0.12f, 0.9f), new Color(1f, 0.86f, 0.82f, 1f), stage),
                RuntimeEventSemantic.PressureWave => new PriorityCuePayload("기척이 번진다", new Color(0.5f, 0.04f, 0.08f, 0.9f), new Color(1f, 0.84f, 0.82f, 1f), stage),
                RuntimeEventSemantic.SetPieceShift => new PriorityCuePayload("공간이 바뀐다", new Color(0.38f, 0.22f, 0.08f, 0.9f), new Color(1f, 0.93f, 0.84f, 1f), stage),
                RuntimeEventSemantic.RhythmShift => new PriorityCuePayload("박자가 바뀐다", new Color(0.28f, 0.1f, 0.18f, 0.9f), new Color(1f, 0.88f, 0.92f, 1f), stage),
                RuntimeEventSemantic.HauntedRoom => new PriorityCuePayload("방이 깨어난다", new Color(0.22f, 0.08f, 0.1f, 0.82f), new Color(1f, 0.86f, 0.82f, 1f), stage),
                _ => default
            };
            if (!string.IsNullOrWhiteSpace(payload.Text))
            {
                return true;
            }

            switch (record.Type)
            {
                case RuntimeEventType.Death:
                    payload = new PriorityCuePayload("쓰러졌다 - 다시 숨을 골라", new Color(0.55f, 0.08f, 0.1f, 0.9f), new Color(1f, 0.92f, 0.9f, 1f), stage);
                    return true;
                case RuntimeEventType.Objective when ContainsKeyword(message, "exit unlocked"):
                    payload = new PriorityCuePayload("출구 열림 - 지금 나가", new Color(0.08f, 0.4f, 0.22f, 0.9f), new Color(0.92f, 1f, 0.92f, 1f), stage);
                    return true;
                case RuntimeEventType.System when ContainsKeyword(message, "lock-on warning"):
                    payload = new PriorityCuePayload("곧 덮쳐온다", new Color(0.62f, 0.28f, 0.05f, 0.9f), new Color(1f, 0.93f, 0.84f, 1f), stage);
                    return true;
                case RuntimeEventType.System when ContainsKeyword(message, "chase started"):
                    payload = new PriorityCuePayload("쫓긴다", new Color(0.62f, 0.1f, 0.1f, 0.92f), new Color(1f, 0.88f, 0.88f, 1f), stage);
                    return true;
                case RuntimeEventType.System when ContainsKeyword(message, "chase disengaged"):
                    payload = new PriorityCuePayload("따돌렸다", new Color(0.2f, 0.28f, 0.52f, 0.9f), new Color(0.9f, 0.94f, 1f, 1f), stage);
                    return true;
                case RuntimeEventType.Stage when ContainsKeyword(message, "setpiece"):
                    payload = new PriorityCuePayload("공간이 바뀐다", new Color(0.38f, 0.22f, 0.08f, 0.9f), new Color(1f, 0.93f, 0.84f, 1f), stage);
                    return true;
                case RuntimeEventType.System when ContainsKeyword(message, "haunted room"):
                    payload = new PriorityCuePayload("방이 깨어난다", new Color(0.22f, 0.08f, 0.1f, 0.82f), new Color(1f, 0.86f, 0.82f, 1f), stage);
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsBuildReturnCue(string message)
        {
            return ContainsKeyword(message, "다시 빨라진다")
                   || ContainsKeyword(message, "build returning");
        }

        private void TickPriorityCueFade()
        {
            if (!enablePriorityCueToast)
            {
                return;
            }

            EnsurePriorityCueToast();
            if (priorityCueGroup == null)
            {
                return;
            }

            if (priorityCueGroup.alpha <= 0.001f && priorityCueQueue.Count > 0)
            {
                ShowNextPriorityCue();
            }

            if (priorityCueGroup.alpha <= 0.001f && priorityCueQueue.Count <= 0)
            {
                return;
            }

            float now = Time.unscaledTime;
            float target;
            if (now <= priorityCueHoldUntil)
            {
                target = 1f;
            }
            else if (now >= priorityCueHideAt)
            {
                target = 0f;
            }
            else
            {
                float t = Mathf.InverseLerp(priorityCueHoldUntil, priorityCueHideAt, now);
                target = 1f - t;
            }

            priorityCueGroup.alpha = Mathf.MoveTowards(
                priorityCueGroup.alpha,
                target,
                Mathf.Max(0.5f, activePriorityCueFadeSpeed) * Time.unscaledDeltaTime);

            if (target <= 0f && priorityCueGroup.alpha <= 0.001f && priorityCueQueue.Count > 0)
            {
                ShowNextPriorityCue();
            }
        }

        private void ShowNextPriorityCue()
        {
            EnsurePriorityCueToast();
            if (priorityCueGroup == null || priorityCueText == null || priorityCueBackground == null || priorityCueQueue.Count <= 0)
            {
                return;
            }

            PriorityCuePayload payload = priorityCueQueue.Dequeue();
            float stageIntensity01 = EvaluateStageCueIntensity01(payload.Stage);
            float durationScale = useStageScaledCueTuning
                ? Mathf.Lerp(priorityCueDurationStartScale, priorityCueDurationPeakScale, stageIntensity01)
                : 1f;
            float holdScale = useStageScaledCueTuning
                ? Mathf.Lerp(priorityCueHoldStartScale, priorityCueHoldPeakScale, stageIntensity01)
                : 1f;
            float fadeScale = useStageScaledCueTuning
                ? Mathf.Lerp(priorityCueFadeSpeedStartScale, priorityCueFadeSpeedPeakScale, stageIntensity01)
                : 1f;
            activePriorityCueFadeSpeed = Mathf.Max(0.5f, priorityCueFadeSpeed * fadeScale);

            Color boostedBackground = payload.BackgroundColor;
            float alphaBoost = useStageScaledCueTuning ? Mathf.Lerp(0f, priorityCueAlphaBoostAtPeak, stageIntensity01) : 0f;
            boostedBackground.a = Mathf.Clamp01(boostedBackground.a + alphaBoost);
            Color boostedText = Color.Lerp(payload.TextColor, Color.white, useStageScaledCueTuning ? stageIntensity01 * 0.22f : 0f);

            priorityCueText.text = payload.Text;
            priorityCueText.color = boostedText;
            priorityCueText.fontSize = Mathf.Max(12, Mathf.RoundToInt(priorityCueFontSize * Mathf.Lerp(1f, priorityCueFontScaleAtPeak, stageIntensity01)));
            priorityCueBackground.color = boostedBackground;

            float now = Time.unscaledTime;
            priorityCueHoldUntil = now + Mathf.Max(0f, priorityCueHoldSeconds * holdScale);
            priorityCueHideAt = priorityCueHoldUntil + Mathf.Max(0.5f, priorityCueDuration * durationScale);
            priorityCueGroup.alpha = 1f;
        }

        private int ResolveEventStage(RuntimeEventRecord record)
        {
            if (record.HasStage)
            {
                return Mathf.Max(1, record.Stage);
            }

            TryResolveReferences();
            if (stageLoopDirector != null)
            {
                return Mathf.Max(1, stageLoopDirector.CurrentStage);
            }

            if (playerVitalSystem != null)
            {
                return Mathf.Max(1, playerVitalSystem.LastDeathStage);
            }

            return 1;
        }

        private float EvaluateStageCueIntensity01(int stage)
        {
            if (!useStageScaledCueTuning)
            {
                return 0f;
            }

            int start = Mathf.Max(1, cueScaleStartStage);
            int peak = Mathf.Max(start + 1, cueScalePeakStage);
            int safeStage = Mathf.Max(1, stage);
            return Mathf.Clamp01(Mathf.InverseLerp(start, peak, safeStage));
        }

        private static bool ContainsKeyword(string source, string keyword)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(keyword))
            {
                return false;
            }

            return source.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void TryResolveReferences(bool force = false)
        {
            if (!force)
            {
                if (HasResolvedCoreReferences())
                {
                    return;
                }

                if (Time.unscaledTime < nextReferenceResolveTime)
                {
                    return;
                }

                nextReferenceResolveTime = Time.unscaledTime + Mathf.Max(0.1f, missingReferenceResolveInterval);
            }

            ResolveReferences();
        }

        private bool HasResolvedCoreReferences()
        {
            bool hasCanvas = targetCanvas != null;
            bool hasCamera = !enableCameraImpulse || cameraFollow != null;
            bool hasVitals = playerVitalSystem != null;
            bool hasStage = stageLoopDirector != null;
            bool hasFont = runtimeFont != null;
            return hasCanvas && hasCamera && hasVitals && hasStage && hasFont;
        }

        private void ResolveReferences()
        {
            if (targetCanvas == null)
            {
                GameplayHudRuntime hud = FindFirstObjectByType<GameplayHudRuntime>();
                if (hud != null)
                {
                    targetCanvas = hud.GetComponentInChildren<Canvas>(true);
                }
            }

            if (targetCanvas == null)
            {
                GameObject hudCanvasObject = GameObject.Find("HUD_Canvas");
                if (hudCanvasObject != null)
                {
                    targetCanvas = hudCanvasObject.GetComponent<Canvas>();
                }
            }

            if (targetCanvas == null)
            {
                targetCanvas = FindFirstObjectByType<Canvas>();
            }

            if (cameraFollow == null)
            {
                cameraFollow = FindFirstObjectByType<CameraFollow2D>();
            }

            if (playerVitalSystem == null)
            {
                playerVitalSystem = FindFirstObjectByType<PlayerVitalSystem>();
            }

            if (stageLoopDirector == null)
            {
                stageLoopDirector = StageLoopDirector.Instance;
                if (stageLoopDirector == null)
                {
                    stageLoopDirector = FindFirstObjectByType<StageLoopDirector>();
                }
            }

            if (runtimeFont == null)
            {
                runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
        }

        private static Canvas CreateFallbackCanvas()
        {
            GameObject existing = GameObject.Find("EventFeedbackCanvas");
            if (existing != null)
            {
                return existing.GetComponent<Canvas>();
            }

            GameObject canvasObject = new("EventFeedbackCanvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 240;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            return canvas;
        }

        private void EnsureOverlay()
        {
            TryResolveReferences();
            if (targetCanvas == null)
            {
                targetCanvas = CreateFallbackCanvas();
                if (targetCanvas == null)
                {
                    return;
                }
            }

            if (enableScreenFlash)
            {
                if (flashImage == null)
                {
                    Transform existing = targetCanvas.transform.Find("EventFlashOverlay");
                    if (existing != null)
                    {
                        flashImage = existing.GetComponent<Image>();
                    }

                    if (flashImage == null)
                    {
                        GameObject overlay = new("EventFlashOverlay");
                        overlay.transform.SetParent(targetCanvas.transform, false);

                        RectTransform rect = overlay.AddComponent<RectTransform>();
                        rect.anchorMin = Vector2.zero;
                        rect.anchorMax = Vector2.one;
                        rect.offsetMin = Vector2.zero;
                        rect.offsetMax = Vector2.zero;
                        rect.SetAsLastSibling();

                        flashImage = overlay.AddComponent<Image>();
                    }
                }

                if (flashImage != null)
                {
                    flashImage.color = Color.clear;
                    flashImage.raycastTarget = false;
                }
            }

            if (enableDeathRecapToast)
            {
                EnsureDeathRecapToast();
            }

            if (enablePriorityCueToast)
            {
                EnsurePriorityCueToast();
            }
        }

        private void EnsureDeathRecapToast()
        {
            if (targetCanvas == null)
            {
                return;
            }

            Transform existing = targetCanvas.transform.Find("DeathRecapToast");
            RectTransform rootRect;
            if (existing != null)
            {
                rootRect = existing as RectTransform;
            }
            else
            {
                GameObject toastObject = new("DeathRecapToast");
                toastObject.transform.SetParent(targetCanvas.transform, false);
                rootRect = toastObject.AddComponent<RectTransform>();

                Image background = toastObject.AddComponent<Image>();
                background.color = new Color(0.12f, 0.04f, 0.06f, 0.82f);
                background.raycastTarget = false;
            }

            if (rootRect == null)
            {
                return;
            }

            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.sizeDelta = deathRecapSize;
            rootRect.anchoredPosition = deathRecapOffset;
            deathRecapBackground = rootRect.GetComponent<Image>();

            deathRecapGroup = rootRect.GetComponent<CanvasGroup>();
            if (deathRecapGroup == null)
            {
                deathRecapGroup = rootRect.gameObject.AddComponent<CanvasGroup>();
            }

            deathRecapGroup.alpha = Mathf.Clamp01(deathRecapGroup.alpha);
            deathRecapGroup.interactable = false;
            deathRecapGroup.blocksRaycasts = false;

            Transform textTransform = rootRect.Find("Text");
            if (textTransform == null)
            {
                GameObject textObject = new("Text");
                textObject.transform.SetParent(rootRect, false);
                RectTransform textRect = textObject.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(18f, 10f);
                textRect.offsetMax = new Vector2(-18f, -10f);
                deathRecapText = textObject.AddComponent<Text>();
            }
            else
            {
                deathRecapText = textTransform.GetComponent<Text>();
                if (deathRecapText == null)
                {
                    deathRecapText = textTransform.gameObject.AddComponent<Text>();
                }
            }

            if (deathRecapText == null)
            {
                return;
            }

            deathRecapText.font = runtimeFont;
            deathRecapText.fontSize = deathRecapFontSize;
            deathRecapText.fontStyle = FontStyle.Bold;
            deathRecapText.alignment = TextAnchor.UpperLeft;
            deathRecapText.color = new Color(1f, 0.9f, 0.88f, 1f);
            deathRecapText.horizontalOverflow = HorizontalWrapMode.Wrap;
            deathRecapText.verticalOverflow = VerticalWrapMode.Overflow;
            deathRecapText.raycastTarget = false;
            deathRecapText.text = string.Empty;
        }

        private void EnsurePriorityCueToast()
        {
            if (targetCanvas == null)
            {
                return;
            }

            Transform existing = targetCanvas.transform.Find("PriorityCueToast");
            RectTransform rootRect;
            if (existing != null)
            {
                rootRect = existing as RectTransform;
            }
            else
            {
                GameObject toastObject = new("PriorityCueToast");
                toastObject.transform.SetParent(targetCanvas.transform, false);
                rootRect = toastObject.AddComponent<RectTransform>();

                Image background = toastObject.AddComponent<Image>();
                background.color = new Color(0.12f, 0.12f, 0.16f, 0.88f);
                background.raycastTarget = false;
            }

            if (rootRect == null)
            {
                return;
            }

            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.sizeDelta = priorityCueSize;
            rootRect.anchoredPosition = priorityCueOffset;

            priorityCueBackground = rootRect.GetComponent<Image>();
            if (priorityCueBackground != null)
            {
                priorityCueBackground.raycastTarget = false;
            }

            priorityCueGroup = rootRect.GetComponent<CanvasGroup>();
            if (priorityCueGroup == null)
            {
                priorityCueGroup = rootRect.gameObject.AddComponent<CanvasGroup>();
            }

            priorityCueGroup.alpha = Mathf.Clamp01(priorityCueGroup.alpha);
            priorityCueGroup.interactable = false;
            priorityCueGroup.blocksRaycasts = false;

            Transform textTransform = rootRect.Find("Text");
            if (textTransform == null)
            {
                GameObject textObject = new("Text");
                textObject.transform.SetParent(rootRect, false);
                RectTransform textRect = textObject.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(14f, 6f);
                textRect.offsetMax = new Vector2(-14f, -6f);
                priorityCueText = textObject.AddComponent<Text>();
            }
            else
            {
                priorityCueText = textTransform.GetComponent<Text>();
                if (priorityCueText == null)
                {
                    priorityCueText = textTransform.gameObject.AddComponent<Text>();
                }
            }

            if (priorityCueText == null)
            {
                return;
            }

            priorityCueText.font = runtimeFont;
            priorityCueText.fontSize = priorityCueFontSize;
            priorityCueText.fontStyle = FontStyle.Bold;
            priorityCueText.alignment = TextAnchor.MiddleCenter;
            priorityCueText.color = new Color(0.94f, 0.96f, 1f, 1f);
            priorityCueText.horizontalOverflow = HorizontalWrapMode.Wrap;
            priorityCueText.verticalOverflow = VerticalWrapMode.Truncate;
            priorityCueText.raycastTarget = false;
            if (string.IsNullOrWhiteSpace(priorityCueText.text))
            {
                priorityCueText.text = string.Empty;
            }
        }
    }
}
