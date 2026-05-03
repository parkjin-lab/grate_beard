using LostBreadcrumbs.Runtime.Managers;
using LostBreadcrumbs.Runtime.Map;
using LostBreadcrumbs.Runtime.Player;
using LostBreadcrumbs.Runtime.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace LostBreadcrumbs.Runtime.UI
{
    [DefaultExecutionOrder(80)]
    public sealed class GameplayFlowGuideRuntime : MonoBehaviour
    {
        [Header("Flow Guide")]
        [SerializeField] private bool buildOnAwake = true;
        [SerializeField, Min(0.05f)] private float refreshInterval = 0.12f;
        [SerializeField, Min(0.1f)] private float missingReferenceResolveInterval = 0.8f;
        [SerializeField] private Vector2 panelSize = new(390f, 216f);
        [SerializeField] private Vector2 panelOffset = new(-18f, -192f);
        [SerializeField] private bool logBuild;

        private Canvas canvas;
        private Font runtimeFont;
        private Text phaseText;
        private Text objectiveText;
        private Text cooldownText;
        private Text controlsText;
        private Text pressureText;
        private Text contextText;

        private StageLoopDirector stageLoop;
        private MapSystem mapSystem;
        private PlayerEchoPulseAbility pulseAbility;
        private PlayerDecoyAbility decoyAbility;
        private PlayerSmokeAbility smokeAbility;
        private StagePressureDirector stagePressure;
        private ThreatReadabilityDirector threatReadability;

        private float nextRefreshAt;
        private float nextReferenceResolveTime;

        private void Awake()
        {
            if (buildOnAwake)
            {
                BuildGuideIfNeeded();
            }

            TryResolveReferences(force: true);
        }

        private void Start()
        {
            BuildGuideIfNeeded();
            TryResolveReferences(force: true);
            RefreshGuide(force: true);
        }

        private void Update()
        {
            if (canvas == null)
            {
                BuildGuideIfNeeded();
            }

            TryResolveReferences();

            if (Time.unscaledTime < nextRefreshAt)
            {
                return;
            }

            nextRefreshAt = Time.unscaledTime + Mathf.Max(0.05f, refreshInterval);
            RefreshGuide(force: false);
        }

        private void BuildGuideIfNeeded()
        {
            if (canvas != null && phaseText != null)
            {
                return;
            }

            runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Canvas existingCanvas = GetComponentInChildren<Canvas>(true);
            if (existingCanvas != null)
            {
                canvas = existingCanvas;
            }
            else
            {
                GameObject canvasObject = new("FlowGuide_Canvas");
                canvasObject.transform.SetParent(transform, false);

                canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 109;

                CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                GraphicRaycaster raycaster = canvasObject.AddComponent<GraphicRaycaster>();
                raycaster.enabled = false;
            }

            RectTransform rootRect = canvas.GetComponent<RectTransform>();
            if (rootRect == null)
            {
                rootRect = canvas.gameObject.AddComponent<RectTransform>();
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.one;
                rootRect.offsetMin = Vector2.zero;
                rootRect.offsetMax = Vector2.zero;
            }

            Transform panelTransform = canvas.transform.Find("FlowGuide_Panel");
            RectTransform panelRect;
            if (panelTransform != null)
            {
                panelRect = panelTransform as RectTransform;
            }
            else
            {
                GameObject panelObject = new("FlowGuide_Panel");
                panelObject.transform.SetParent(canvas.transform, false);
                panelRect = panelObject.AddComponent<RectTransform>();
                Image panelImage = panelObject.AddComponent<Image>();
                panelImage.color = new Color(0.05f, 0.07f, 0.1f, 0.78f);

                VerticalLayoutGroup layout = panelObject.AddComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(12, 12, 10, 10);
                layout.spacing = 5f;
                layout.childAlignment = TextAnchor.UpperLeft;
                layout.childControlHeight = true;
                layout.childControlWidth = true;
                layout.childForceExpandHeight = false;
                layout.childForceExpandWidth = true;

                ContentSizeFitter fitter = panelObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            }

            panelRect.anchorMin = new Vector2(1f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(1f, 1f);
            panelRect.sizeDelta = panelSize;
            panelRect.anchoredPosition = panelOffset;

            phaseText = EnsureLineText(panelRect, "FlowGuide_Phase", 17, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.95f, 0.96f, 1f, 1f));
            objectiveText = EnsureLineText(panelRect, "FlowGuide_Objective", 14, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.92f, 0.96f, 1f, 0.98f));
            cooldownText = EnsureLineText(panelRect, "FlowGuide_Cooldowns", 14, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.9f, 0.93f, 1f, 0.96f));
            controlsText = EnsureLineText(panelRect, "FlowGuide_Controls", 13, FontStyle.Italic, TextAnchor.UpperLeft, new Color(0.86f, 0.9f, 1f, 0.9f));
            pressureText = EnsureLineText(panelRect, "FlowGuide_Pressure", 14, FontStyle.Normal, TextAnchor.UpperLeft, new Color(1f, 0.86f, 0.78f, 0.97f));
            contextText = EnsureLineText(panelRect, "FlowGuide_Context", 13, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.88f, 0.95f, 0.86f, 0.93f));

            if (logBuild)
            {
                Debug.Log("GameplayFlowGuideRuntime: dummy flow guide panel ready.", this);
            }
        }

        private Text EnsureLineText(Transform parent, string name, int fontSize, FontStyle style, TextAnchor anchor, Color color)
        {
            Transform existing = parent.Find(name);
            Text text;
            if (existing != null)
            {
                text = existing.GetComponent<Text>();
            }
            else
            {
                GameObject textObject = new(name);
                textObject.transform.SetParent(parent, false);
                text = textObject.AddComponent<Text>();
                LayoutElement layout = textObject.AddComponent<LayoutElement>();
                layout.preferredHeight = fontSize + 8f;
            }

            text.font = runtimeFont;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private void ResolveReferences()
        {
            stageLoop ??= StageLoopDirector.Instance;
            mapSystem ??= FindFirstObjectByType<MapSystem>();
            pulseAbility ??= FindFirstObjectByType<PlayerEchoPulseAbility>();
            decoyAbility ??= FindFirstObjectByType<PlayerDecoyAbility>();
            smokeAbility ??= FindFirstObjectByType<PlayerSmokeAbility>();
            stagePressure ??= FindFirstObjectByType<StagePressureDirector>();
            threatReadability ??= FindFirstObjectByType<ThreatReadabilityDirector>();
        }

        private void TryResolveReferences(bool force = false)
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

            ResolveReferences();
        }

        private bool HasAllReferences()
        {
            return stageLoop != null
                && mapSystem != null
                && pulseAbility != null
                && decoyAbility != null
                && smokeAbility != null
                && stagePressure != null
                && threatReadability != null;
        }

        private void RefreshGuide(bool force)
        {
            if (phaseText == null || objectiveText == null || cooldownText == null || controlsText == null || pressureText == null || contextText == null)
            {
                return;
            }

            int stage = mapSystem != null ? Mathf.Max(1, mapSystem.CurrentStage) : 1;
            int collected = stageLoop != null ? stageLoop.CollectedBreadcrumbs : 0;
            int required = stageLoop != null ? stageLoop.RequiredBreadcrumbs : 0;
            bool exitUnlocked = stageLoop != null && stageLoop.ExitUnlocked;
            int hookCount = mapSystem != null ? Mathf.Max(0, mapSystem.LastArchetypeHookCount) : 0;
            int safeHavenCount = stageLoop != null ? stageLoop.ActiveSafeHavenCount : 0;

            float totalPressure = stagePressure != null ? stagePressure.CurrentPressure01 : 0f;
            float stagePressureValue = stagePressure != null ? stagePressure.CurrentStagePressure01 : 0f;
            float readabilityPressure = threatReadability != null ? threatReadability.CurrentReadabilityPressure : totalPressure;

            string phase = EvaluatePhaseLabel(collected, required, exitUnlocked, totalPressure);
            phaseText.text = "Flow Step: " + phase;
            phaseText.color = EvaluatePhaseColor(totalPressure, exitUnlocked);

            objectiveText.text = $"Objective: Breadcrumb {collected}/{Mathf.Max(0, required)} | Stage {stage} | Exit {(exitUnlocked ? "OPEN" : "LOCKED")}";
            cooldownText.text = $"Cooldowns: Echo {FormatSeconds(pulseAbility != null ? pulseAbility.CooldownRemaining : -1f)} | Decoy {FormatSeconds(decoyAbility != null ? decoyAbility.CooldownRemaining : -1f)} | Smoke {FormatSeconds(smokeAbility != null ? smokeAbility.CooldownRemaining : -1f)}";
            controlsText.text = "Controls: Move WASD | Sprint Shift | Echo Q | Decoy E | Smoke R | Flashlight F";
            pressureText.text = $"Danger: StageP {stagePressureValue:0.00} | ThreatP {readabilityPressure:0.00} | TotalP {totalPressure:0.00}";
            contextText.text = $"Map Cues: Hook Sigils {hookCount} (spin/blink = pre-noise warning), Safe Havens {safeHavenCount}";

            if (force)
            {
                nextRefreshAt = Time.unscaledTime + Mathf.Max(0.05f, refreshInterval);
            }
        }

        private static string FormatSeconds(float seconds)
        {
            if (seconds < 0f)
            {
                return "N/A";
            }

            if (seconds <= 0.05f)
            {
                return "Ready";
            }

            return seconds.ToString("0.0") + "s";
        }

        private static string EvaluatePhaseLabel(int collected, int required, bool exitUnlocked, float totalPressure)
        {
            if (required <= 0)
            {
                return "Stage Init";
            }

            if (exitUnlocked)
            {
                return totalPressure >= 0.72f ? "Escape Under Chase" : "Escape Route";
            }

            if (collected <= 0)
            {
                return "Explore And Scan";
            }

            if (collected < required)
            {
                return totalPressure >= 0.68f ? "Collect While Evading" : "Collect Breadcrumbs";
            }

            return "Unlock Exit";
        }

        private static Color EvaluatePhaseColor(float totalPressure, bool exitUnlocked)
        {
            if (exitUnlocked)
            {
                return new Color(1f, 0.88f, 0.46f, 1f);
            }

            if (totalPressure >= 0.75f)
            {
                return new Color(1f, 0.45f, 0.45f, 1f);
            }

            if (totalPressure >= 0.45f)
            {
                return new Color(1f, 0.78f, 0.42f, 1f);
            }

            return new Color(0.78f, 0.96f, 1f, 1f);
        }
    }
}
