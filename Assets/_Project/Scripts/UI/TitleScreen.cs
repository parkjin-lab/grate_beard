using System;
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
        private Text startLabel;
        private Text continueLabel;
        private RectTransform startRect;
        private RectTransform continueRect;
        private Action pendingStart;
        private Action pendingContinue;
        private float ignoreInputUntil;
        private bool visible;

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
            if (group != null)
            {
                group.alpha = 1f;
                group.blocksRaycasts = true;
                group.interactable = true;
            }

            if (canvas != null)
            {
                canvas.enabled = true;
            }
        }

        public void HideImmediate()
        {
            visible = false;
            pendingStart = null;
            pendingContinue = null;
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
            HideImmediate();
            callback?.Invoke();
        }

        private void CompleteContinue()
        {
            Action callback = pendingContinue;
            HideImmediate();
            callback?.Invoke();
        }

        private void RefreshChoiceLabels()
        {
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

            if (startRect != null)
            {
                if (showContinue)
                {
                    startRect.anchorMin = new Vector2(0.32f, 0.34f);
                    startRect.anchorMax = new Vector2(0.68f, 0.44f);
                }
                else
                {
                    startRect.anchorMin = new Vector2(0.32f, 0.28f);
                    startRect.anchorMax = new Vector2(0.68f, 0.42f);
                }

                startRect.offsetMin = Vector2.zero;
                startRect.offsetMax = Vector2.zero;
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

            Image background = CreateImage("Title_Frame", canvas.transform, Vector2.zero, Vector2.one, new Color(0.07f, 0.05f, 0.03f, 0.96f));
            Sprite frame = CampaignArt.TryGetBookFrame();
            if (frame != null)
            {
                background.sprite = frame;
                background.color = Color.white;
            }

            Text logo = CreateText(
                "Title_Logo",
                canvas.transform,
                new Vector2(0.18f, 0.52f),
                new Vector2(0.82f, 0.78f),
                72,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Color(0.20f, 0.10f, 0.05f, 1f));
            logo.text = CampaignStoryCopy.TitleLogo;

            startLabel = CreateText(
                "Title_Start",
                canvas.transform,
                new Vector2(0.32f, 0.28f),
                new Vector2(0.68f, 0.42f),
                40,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Color(0.28f, 0.12f, 0.05f, 1f));
            startLabel.text = CampaignStoryCopy.StartLabel;
            startRect = startLabel.rectTransform;

            continueLabel = CreateText(
                "Title_Continue",
                canvas.transform,
                new Vector2(0.32f, 0.18f),
                new Vector2(0.68f, 0.30f),
                36,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Color(0.34f, 0.16f, 0.06f, 1f));
            continueLabel.text = CampaignStoryCopy.ContinueRunLabel;
            continueRect = continueLabel.rectTransform;
            continueLabel.gameObject.SetActive(false);
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
