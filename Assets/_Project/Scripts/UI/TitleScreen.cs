using System;
using LostBreadcrumbs.Runtime.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace LostBreadcrumbs.Runtime.UI
{
    public sealed class TitleScreen : MonoBehaviour
    {
        public static TitleScreen ActiveInstance { get; private set; }

        [SerializeField] private bool autoBuild = true;
        [SerializeField] private Font uiFont;
        [SerializeField, Min(0f)] private float inputGraceSeconds = 0.12f;

        private Canvas canvas;
        private CanvasGroup group;
        private Image frameImage;
        private Image startInkLine;
        private Image continueInkLine;
        private Text logoLabel;
        private Text startLabel;
        private Text continueLabel;
        private RectTransform startRect;
        private RectTransform continueRect;
        private Action pendingStart;
        private Action pendingContinue;
        private float ignoreInputUntil;
        private bool visible;
        private Coroutine fadeRoutine;

        public bool IsShowing => visible;
        public bool HasContinueOption => pendingContinue != null && continueLabel != null && continueLabel.gameObject.activeSelf;

        public static TitleScreen EnsureInstance()
        {
            if (ActiveInstance != null)
            {
                return ActiveInstance;
            }

            TitleScreen existing = FindFirstObjectByType<TitleScreen>();
            if (existing != null)
            {
                return existing;
            }

            GameObject host = new("TitleScreen");
            return host.AddComponent<TitleScreen>();
        }

        private void Awake()
        {
            ActiveInstance = this;
            if (autoBuild)
            {
                BuildIfNeeded();
            }

            HideImmediate();
        }

        private void OnDestroy()
        {
            if (ActiveInstance == this)
            {
                ActiveInstance = null;
            }
        }

        private void Update()
        {
            if (!visible || Time.unscaledTime < ignoreInputUntil)
            {
                return;
            }

            if (CampaignUiInput.ConfirmPressed())
            {
                if (HasContinueOption)
                {
                    CompleteContinue();
                }
                else
                {
                    CompleteStart();
                }

                return;
            }

            if (!CampaignUiInput.PrimaryClickDown())
            {
                return;
            }

            Vector2 pointer = CampaignUiInput.PointerScreenPosition();
            if (HasContinueOption && ContainsScreenPoint(continueRect, pointer))
            {
                CompleteContinue();
                return;
            }

            if (ContainsScreenPoint(startRect, pointer))
            {
                CompleteStart();
            }
        }

        public void Show(Action onStart, Action onContinue = null)
        {
            BuildIfNeeded();
            pendingStart = onStart;
            pendingContinue = onContinue;
            RefreshChoiceLabels();
            visible = true;
            ignoreInputUntil = Time.unscaledTime + Mathf.Max(0.05f, inputGraceSeconds);
            if (canvas != null)
            {
                canvas.enabled = true;
            }

            if (group != null)
            {
                group.blocksRaycasts = true;
                group.interactable = true;
            }

            StartFade(1f);
        }

        public void HideImmediate()
        {
            visible = false;
            pendingStart = null;
            pendingContinue = null;
            StopFade();
            if (group != null)
            {
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;
            }

            if (canvas != null)
            {
                canvas.enabled = false;
            }
        }

        private void CompleteStart()
        {
            Action callback = pendingStart;
            DismissChoices();
            AudioDummyLoopRuntime.TryPlayPageTurnRustle();
            callback?.Invoke();
        }

        private void CompleteContinue()
        {
            Action callback = pendingContinue;
            DismissChoices();
            AudioDummyLoopRuntime.TryPlayPageTurnRustle();
            callback?.Invoke();
        }

        private void DismissChoices()
        {
            visible = false;
            pendingStart = null;
            pendingContinue = null;
            if (group != null)
            {
                group.blocksRaycasts = false;
                group.interactable = false;
            }
        }

        private void RefreshChoiceLabels()
        {
            if (logoLabel != null)
            {
                logoLabel.text = CampaignStoryCopy.TitleLogo;
            }

            if (startLabel != null)
            {
                startLabel.text = CampaignStoryCopy.StartLabel;
            }

            bool showContinue = pendingContinue != null;
            if (continueLabel != null)
            {
                continueLabel.gameObject.SetActive(showContinue);
                continueLabel.text = CampaignStoryCopy.ContinueRunLabel;
            }

            if (continueInkLine != null)
            {
                continueInkLine.gameObject.SetActive(showContinue);
            }

            if (startRect != null)
            {
                if (showContinue)
                {
                    startRect.anchorMin = new Vector2(0.56f, 0.36f);
                    startRect.anchorMax = new Vector2(0.86f, 0.46f);
                }
                else
                {
                    startRect.anchorMin = new Vector2(0.56f, 0.30f);
                    startRect.anchorMax = new Vector2(0.86f, 0.42f);
                }

                startRect.offsetMin = Vector2.zero;
                startRect.offsetMax = Vector2.zero;
            }

            if (startInkLine != null && startRect != null)
            {
                RectTransform lineRect = startInkLine.rectTransform;
                lineRect.anchorMin = new Vector2(startRect.anchorMin.x + 0.04f, startRect.anchorMin.y - 0.01f);
                lineRect.anchorMax = new Vector2(startRect.anchorMax.x - 0.04f, startRect.anchorMin.y + 0.008f);
                lineRect.offsetMin = Vector2.zero;
                lineRect.offsetMax = Vector2.zero;
            }
        }

        private void BuildIfNeeded()
        {
            if (canvas != null)
            {
                return;
            }

            GameObject canvasObject = new("TitleScreen_Canvas");
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 240;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            group = canvasObject.AddComponent<CanvasGroup>();

            CreateImage("Title_Desk", canvas.transform, Vector2.zero, Vector2.one, new Color(0.08f, 0.05f, 0.03f, 1f));
            frameImage = CreateImage("Title_Frame", canvas.transform, Vector2.zero, Vector2.one, new Color(0.16f, 0.1f, 0.06f, 0.96f));
            Sprite frame = CampaignArt.TryGetBookFrame();
            if (frame != null)
            {
                frameImage.sprite = frame;
                frameImage.color = Color.white;
                frameImage.preserveAspect = true;
            }

            logoLabel = CreateText(
                "Title_Logo",
                canvas.transform,
                new Vector2(0.54f, 0.52f),
                new Vector2(0.88f, 0.72f),
                54,
                FontStyle.Normal,
                TextAnchor.MiddleCenter,
                new Color(0.24f, 0.12f, 0.06f, 0.96f));
            logoLabel.text = CampaignStoryCopy.TitleLogo;

            startLabel = CreateText(
                "Title_Start",
                canvas.transform,
                new Vector2(0.56f, 0.30f),
                new Vector2(0.86f, 0.42f),
                34,
                FontStyle.Italic,
                TextAnchor.MiddleCenter,
                new Color(0.30f, 0.14f, 0.07f, 0.94f));
            startLabel.text = CampaignStoryCopy.StartLabel;
            startRect = startLabel.rectTransform;
            startInkLine = CreateImage(
                "Title_StartInk",
                canvas.transform,
                new Vector2(0.60f, 0.29f),
                new Vector2(0.82f, 0.305f),
                new Color(0.28f, 0.14f, 0.07f, 0.42f));

            continueLabel = CreateText(
                "Title_Continue",
                canvas.transform,
                new Vector2(0.56f, 0.20f),
                new Vector2(0.86f, 0.30f),
                30,
                FontStyle.Italic,
                TextAnchor.MiddleCenter,
                new Color(0.32f, 0.16f, 0.08f, 0.9f));
            continueLabel.text = CampaignStoryCopy.ContinueRunLabel;
            continueRect = continueLabel.rectTransform;
            continueInkLine = CreateImage(
                "Title_ContinueInk",
                canvas.transform,
                new Vector2(0.60f, 0.19f),
                new Vector2(0.82f, 0.205f),
                new Color(0.28f, 0.14f, 0.07f, 0.36f));
            continueLabel.gameObject.SetActive(false);
            continueInkLine.gameObject.SetActive(false);
        }

        private void StartFade(float targetAlpha)
        {
            StopFade();
            if (group == null)
            {
                return;
            }

            fadeRoutine = StartCoroutine(FadeRoutine(targetAlpha));
        }

        private void StopFade()
        {
            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
                fadeRoutine = null;
            }
        }

        private System.Collections.IEnumerator FadeRoutine(float targetAlpha)
        {
            float start = group.alpha;
            float duration = 0.22f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(start, targetAlpha, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            group.alpha = targetAlpha;
            fadeRoutine = null;
        }

        private static bool ContainsScreenPoint(RectTransform rect, Vector2 screenPoint)
        {
            if (rect == null || !rect.gameObject.activeInHierarchy)
            {
                return false;
            }

            return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, null);
        }

        private static Image CreateImage(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            GameObject obj = new(name);
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = obj.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private Text CreateText(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            int fontSize,
            FontStyle style,
            TextAnchor anchor,
            Color color)
        {
            GameObject obj = new(name);
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Text text = obj.AddComponent<Text>();
            text.font = uiFont != null ? uiFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }
}
