using LostBreadcrumbs.Runtime.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace LostBreadcrumbs.Runtime.UI
{
    [DefaultExecutionOrder(130)]
    public sealed class DreadScreenOverlayRuntime : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ThreatReadabilityDirector threatReadabilityDirector;
        [SerializeField] private Canvas targetCanvas;

        [Header("Build")]
        [SerializeField] private bool buildOnAwake = true;
        [SerializeField] private int overlaySortingOrder = 108;
        [SerializeField, Min(32)] private int textureSize = 256;
        [SerializeField] private bool hideDuringRegressionChecklist = true;

        [Header("Pressure Response")]
        [SerializeField, Range(0f, 1f)] private float pressureFadeStart = 0.34f;
        [SerializeField, Range(0f, 1f)] private float pressureFadeFull = 0.9f;
        [SerializeField, Range(0f, 1f)] private float flashlightDreadWeight = 0.22f;
        [SerializeField, Range(0f, 1f)] private float maxEdgeAlpha = 0.32f;
        [SerializeField, Range(0f, 0.12f)] private float breathAlpha = 0.025f;
        [SerializeField, Min(0.05f)] private float breathSpeed = 0.58f;
        [SerializeField, Min(0.1f)] private float fadeInSpeed = 2.2f;
        [SerializeField, Min(0.1f)] private float fadeOutSpeed = 3.6f;
        [SerializeField, Min(0.1f)] private float missingReferenceResolveInterval = 0.75f;

        [Header("Color")]
        [SerializeField] private Color lowPressureEdgeColor = new(0.015f, 0.012f, 0.018f, 1f);
        [SerializeField] private Color highPressureEdgeColor = new(0.18f, 0.012f, 0.026f, 1f);

        private const string CanvasName = "DreadOverlay_Canvas";
        private const string ImageName = "DreadOverlay_Vignette";

        private RawImage overlayImage;
        private Texture2D vignetteTexture;
        private Canvas ownedCanvas;
        private float currentAlpha;
        private float nextReferenceResolveTime;

        private void Awake()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (buildOnAwake)
            {
                EnsureOverlay();
            }

            TryResolveReferences(force: true);
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            EnsureOverlay();
            TryResolveReferences(force: true);
            ApplyOverlay(0f, 0f);
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            ApplyOverlay(0f, 0f);
        }

        private void OnDestroy()
        {
            if (vignetteTexture != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(vignetteTexture);
                }
                else
                {
                    DestroyImmediate(vignetteTexture);
                }

                vignetteTexture = null;
            }
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            EnsureOverlay();

            if (hideDuringRegressionChecklist && RegressionChecklistRunner.IsRegressionRunActive)
            {
                currentAlpha = 0f;
                ApplyOverlay(0f, 0f);
                SetOverlayVisible(false);
                return;
            }

            SetOverlayVisible(true);
            TryResolveReferences();

            float pressure = 0f;
            float flashlightDread = 0f;
            if (threatReadabilityDirector != null)
            {
                pressure = Mathf.Clamp01(threatReadabilityDirector.CurrentReadabilityPressure);
                flashlightDread = Mathf.Clamp01(threatReadabilityDirector.CurrentFlashlightDread);
            }

            float combinedPressure = Mathf.Clamp01(pressure + flashlightDread * flashlightDreadWeight);
            float pressureFade = Mathf.InverseLerp(
                Mathf.Min(pressureFadeStart, pressureFadeFull),
                Mathf.Max(pressureFadeStart + 0.01f, pressureFadeFull),
                combinedPressure);
            pressureFade = Mathf.SmoothStep(0f, 1f, pressureFade);

            float breath = (Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * breathSpeed) + 1f) * 0.5f;
            float targetAlpha = pressureFade * maxEdgeAlpha + pressureFade * breath * breathAlpha;
            targetAlpha = Mathf.Clamp(targetAlpha, 0f, maxEdgeAlpha + breathAlpha);

            float speed = targetAlpha > currentAlpha ? fadeInSpeed : fadeOutSpeed;
            float t = 1f - Mathf.Exp(-Mathf.Max(0.1f, speed) * Time.unscaledDeltaTime);
            currentAlpha = Mathf.Lerp(currentAlpha, targetAlpha, t);

            ApplyOverlay(currentAlpha, Mathf.Clamp01(pressureFade + flashlightDread * 0.25f));
        }

        public void SetThreatSourceForEditor(ThreatReadabilityDirector director)
        {
            threatReadabilityDirector = director;
        }

        public void SetTargetCanvasForEditor(Canvas canvasRef)
        {
            targetCanvas = canvasRef;
        }

        private void TryResolveReferences(bool force = false)
        {
            if (!force && threatReadabilityDirector != null)
            {
                return;
            }

            if (!force && Time.unscaledTime < nextReferenceResolveTime)
            {
                return;
            }

            nextReferenceResolveTime = Time.unscaledTime + Mathf.Max(0.1f, missingReferenceResolveInterval);

            if (threatReadabilityDirector == null)
            {
                threatReadabilityDirector = FindFirstObjectByType<ThreatReadabilityDirector>();
            }
        }

        private void EnsureOverlay()
        {
            Canvas canvas = targetCanvas != null ? targetCanvas : EnsureOwnedCanvas();
            if (canvas == null)
            {
                return;
            }

            if (vignetteTexture == null)
            {
                vignetteTexture = CreateVignetteTexture(Mathf.Max(32, textureSize));
            }

            if (overlayImage == null || overlayImage.transform.parent != canvas.transform)
            {
                Transform existing = canvas.transform.Find(ImageName);
                overlayImage = existing != null ? existing.GetComponent<RawImage>() : null;

                if (overlayImage == null)
                {
                    GameObject imageObject = new(ImageName);
                    imageObject.transform.SetParent(canvas.transform, false);

                    RectTransform rect = imageObject.AddComponent<RectTransform>();
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;

                    overlayImage = imageObject.AddComponent<RawImage>();
                }

                overlayImage.raycastTarget = false;
                overlayImage.texture = vignetteTexture;
                overlayImage.transform.SetAsFirstSibling();
            }
        }

        private Canvas EnsureOwnedCanvas()
        {
            if (ownedCanvas != null)
            {
                return ownedCanvas;
            }

            Transform existing = transform.Find(CanvasName);
            if (existing != null)
            {
                ownedCanvas = existing.GetComponent<Canvas>();
            }

            if (ownedCanvas == null)
            {
                GameObject canvasObject = new(CanvasName);
                canvasObject.transform.SetParent(transform, false);
                ownedCanvas = canvasObject.AddComponent<Canvas>();
                ownedCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                ownedCanvas.sortingOrder = overlaySortingOrder;

                CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                GraphicRaycaster raycaster = canvasObject.AddComponent<GraphicRaycaster>();
                raycaster.enabled = false;
            }
            else
            {
                ownedCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                ownedCanvas.sortingOrder = overlaySortingOrder;
            }

            return ownedCanvas;
        }

        private void ApplyOverlay(float alpha, float redBlend)
        {
            if (overlayImage == null)
            {
                return;
            }

            Color color = Color.Lerp(lowPressureEdgeColor, highPressureEdgeColor, Mathf.Clamp01(redBlend));
            color.a = Mathf.Clamp01(alpha);
            overlayImage.color = color;
        }

        private void SetOverlayVisible(bool visible)
        {
            if (overlayImage != null && overlayImage.gameObject.activeSelf != visible)
            {
                overlayImage.gameObject.SetActive(visible);
            }

            if (ownedCanvas != null && ownedCanvas.gameObject.activeSelf != visible)
            {
                ownedCanvas.gameObject.SetActive(visible);
            }
        }

        private static Texture2D CreateVignetteTexture(int size)
        {
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                name = "Runtime_DreadVignette",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color32[] pixels = new Color32[size * size];
            float inv = 1f / Mathf.Max(1, size - 1);
            for (int y = 0; y < size; y++)
            {
                float ny = y * inv * 2f - 1f;
                for (int x = 0; x < size; x++)
                {
                    float nx = x * inv * 2f - 1f;
                    float radial = Mathf.Sqrt(nx * nx + ny * ny);
                    float edge = Smooth01(0.42f, 1.05f, radial);
                    float side = Mathf.Max(Mathf.Pow(Mathf.Abs(nx), 3.6f), Mathf.Pow(Mathf.Abs(ny), 3.6f));
                    float alpha = Mathf.Clamp01(Mathf.Max(edge, side * 0.74f));
                    byte a = (byte)Mathf.RoundToInt(alpha * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static float Smooth01(float min, float max, float value)
        {
            if (max <= min)
            {
                return value >= max ? 1f : 0f;
            }

            float t = Mathf.Clamp01((value - min) / (max - min));
            return t * t * (3f - 2f * t);
        }
    }
}
