using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LostBreadcrumbs.Runtime.UI
{
    public sealed class BookUI : MonoBehaviour
    {
        public static BookUI ActiveInstance { get; private set; }

        [SerializeField] private bool autoBuild = true;
        [SerializeField] private Font uiFont;
        [SerializeField, Min(0f)] private float inputGraceSeconds = 0.16f;

        private Canvas canvas;
        private CanvasGroup group;
        private Image background;
        private Image illustration;
        private Text narration;
        private Text continueHint;
        private Action pendingComplete;
        private float ignoreInputUntil;
        private bool pageVisible;
        private bool completing;
        private Coroutine fadeRoutine;

        public bool IsShowing => pageVisible;

        public static BookUI EnsureInstance()
        {
            if (ActiveInstance != null)
            {
                return ActiveInstance;
            }

            BookUI existing = FindFirstObjectByType<BookUI>();
            if (existing != null)
            {
                return existing;
            }

            GameObject host = new("BookUI");
            return host.AddComponent<BookUI>();
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
            if (!pageVisible || Time.unscaledTime < ignoreInputUntil)
            {
                return;
            }

            if (CampaignUiInput.SkipPressed() || CampaignUiInput.ConfirmPressed())
            {
                CompletePage();
            }
        }

        public void ShowPage(string text, Sprite illust = null, Action onComplete = null)
        {
            ShowPage(text, illust, null, onComplete);
        }

        public void ShowPage(string text, Sprite illust, string continueLabel, Action onComplete)
        {
            BuildIfNeeded();
            StopFade();
            completing = false;
            pendingComplete = onComplete;
            pageVisible = true;
            ignoreInputUntil = Time.unscaledTime + Mathf.Max(0.05f, inputGraceSeconds);

            if (group != null)
            {
                group.blocksRaycasts = true;
                group.interactable = true;
            }

            if (canvas != null)
            {
                canvas.enabled = true;
            }

            if (narration != null)
            {
                narration.text = string.IsNullOrWhiteSpace(text) ? string.Empty : text;
            }

            if (continueHint != null)
            {
                continueHint.text = string.IsNullOrWhiteSpace(continueLabel)
                    ? CampaignStoryCopy.ContinueHint
                    : continueLabel;
            }

            if (illustration != null)
            {
                illustration.sprite = illust;
                illustration.enabled = illust != null;
                illustration.color = Color.white;
            }

            if (background != null)
            {
                Sprite frame = CampaignArt.TryGetBookFrame();
                if (frame != null)
                {
                    background.sprite = frame;
                    background.color = Color.white;
                    background.preserveAspect = true;
                }
            }

            StartFade(1f, null);
        }

        public void HideImmediate()
        {
            StopFade();
            completing = false;
            pageVisible = false;
            pendingComplete = null;
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

        private void CompletePage()
        {
            if (completing)
            {
                return;
            }

            completing = true;
            pageVisible = false;
            Action callback = pendingComplete;
            pendingComplete = null;
            StartFade(0f, () =>
            {
                if (group != null)
                {
                    group.blocksRaycasts = false;
                    group.interactable = false;
                }

                if (canvas != null)
                {
                    canvas.enabled = false;
                }

                completing = false;
                callback?.Invoke();
            });
        }

        private void BuildIfNeeded()
        {
            if (canvas != null)
            {
                return;
            }

            GameObject canvasObject = new("BookUI_Canvas");
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 250;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            group = canvasObject.AddComponent<CanvasGroup>();

            CreateImage("Book_Desk", canvas.transform, Vector2.zero, Vector2.one, new Color(0.08f, 0.05f, 0.03f, 1f));
            background = CreateImage("Book_Frame", canvas.transform, Vector2.zero, Vector2.one, new Color(0.16f, 0.1f, 0.06f, 0.96f));
            Sprite frame = CampaignArt.TryGetBookFrame();
            if (frame != null)
            {
                background.sprite = frame;
                background.color = Color.white;
                background.preserveAspect = true;
            }

            illustration = CreateImage("IllustrationSlot", canvas.transform, new Vector2(0.09f, 0.24f), new Vector2(0.46f, 0.78f), Color.white);
            illustration.preserveAspect = true;
            illustration.enabled = false;

            narration = CreateText(
                "NarrationText",
                canvas.transform,
                new Vector2(0.53f, 0.30f),
                new Vector2(0.90f, 0.74f),
                32,
                FontStyle.Normal,
                TextAnchor.UpperLeft,
                new Color(0.22f, 0.12f, 0.07f, 1f));
            narration.horizontalOverflow = HorizontalWrapMode.Wrap;
            narration.verticalOverflow = VerticalWrapMode.Overflow;

            continueHint = CreateText(
                "ContinueHint",
                canvas.transform,
                new Vector2(0.53f, 0.17f),
                new Vector2(0.90f, 0.26f),
                22,
                FontStyle.Italic,
                TextAnchor.MiddleCenter,
                new Color(0.32f, 0.18f, 0.08f, 0.92f));
            continueHint.text = CampaignStoryCopy.ContinueHint;
        }

        private void StartFade(float targetAlpha, Action onDone)
        {
            StopFade();
            if (group == null)
            {
                onDone?.Invoke();
                return;
            }

            fadeRoutine = StartCoroutine(FadeRoutine(targetAlpha, onDone));
        }

        private void StopFade()
        {
            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
                fadeRoutine = null;
            }
        }

        private IEnumerator FadeRoutine(float targetAlpha, Action onDone)
        {
            float start = group.alpha;
            float duration = targetAlpha > 0.5f ? 0.22f : 0.16f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(start, targetAlpha, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            group.alpha = targetAlpha;
            fadeRoutine = null;
            onDone?.Invoke();
        }

        private Image CreateImage(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
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
            rect.offsetMin = new Vector2(8f, 8f);
            rect.offsetMax = new Vector2(-8f, -8f);
            Text text = obj.AddComponent<Text>();
            text.font = uiFont != null ? uiFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }
}
