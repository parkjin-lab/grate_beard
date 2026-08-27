using System.Collections;
using LostBreadcrumbs.Runtime.Events;
using LostBreadcrumbs.Runtime.Map;
using LostBreadcrumbs.Runtime.Systems;
using LostBreadcrumbs.Runtime.UI;
using UnityEngine;
using UnityEngine.UI;

namespace LostBreadcrumbs.Runtime.Managers
{
    [DefaultExecutionOrder(-400)]
    public sealed class StageManager : ManagerBase
    {
        public static StageManager ActiveInstance { get; private set; }

        [SerializeField] private int currentStageIndex = 1;
        [SerializeField] private bool showTitleOnPlay = true;
        [SerializeField, Min(0.05f)] private float fadeSeconds = 0.32f;

        private MapSystem mapSystem;
        private TitleScreen titleScreen;
        private BookUI bookUi;
        private CanvasGroup fadeGroup;
        private bool campaignStarted;
        private bool bookBusy;
        private bool holdUnlockCueShownThisRun;
        private float restoredTimeScale = 1f;

        public int CurrentStageIndex => Mathf.Max(1, currentStageIndex);

        public static int ResolvedStageIndex =>
            ActiveInstance != null ? ActiveInstance.CurrentStageIndex : 1;

        public static bool IsSmokeUnlocked =>
            ActiveInstance == null || ActiveInstance.CurrentStageIndex >= 2;

        public static bool IsOverchargeHoldUnlocked =>
            ActiveInstance == null || ActiveInstance.CurrentStageIndex >= 3;

        private void OnEnable()
        {
            ActiveInstance = this;
        }

        protected override void Awake()
        {
            base.Awake();
            ActiveInstance = this;
            ResolveMap();
            SyncStageFromMap();
            EnsureCampaignUi();

            if (ShouldHoldAtTitle())
            {
                FreezeGameplay();
            }
        }

        private void Start()
        {
            ResolveMap();
            SyncStageFromMap();
            EnsureCampaignUi();

            if (RegressionChecklistRunner.IsRegressionRunActive || !showTitleOnPlay)
            {
                BeginGameplayImmediate();
                return;
            }

            holdUnlockCueShownThisRun = false;
            FreezeGameplay();
            titleScreen.Show(BeginPrologueFromTitle);
        }

        private void Update()
        {
            if (!campaignStarted && RegressionChecklistRunner.IsRegressionRunActive)
            {
                BeginGameplayImmediate();
            }
        }

        private void OnDestroy()
        {
            if (ActiveInstance == this)
            {
                ActiveInstance = null;
            }

            if (!campaignStarted)
            {
                Time.timeScale = Mathf.Max(0.0001f, restoredTimeScale);
            }
        }

        public bool TryHandleStageClear()
        {
            if (RegressionChecklistRunner.IsRegressionRunActive)
            {
                return false;
            }

            if (bookBusy)
            {
                return true;
            }

            bookBusy = true;
            StartCoroutine(StageClearRoutine());
            return true;
        }

        private void BeginPrologueFromTitle()
        {
            if (bookUi == null)
            {
                BeginGameplayImmediate();
                return;
            }

            bookUi.ShowPage(
                CampaignStoryCopy.PrologueAndStage1,
                CampaignArt.TryGetStage1Illustration(),
                () => StartCoroutine(EnterStageOneRoutine()));
        }

        private IEnumerator EnterStageOneRoutine()
        {
            yield return FadeTo(1f);
            titleScreen?.HideImmediate();
            bookUi?.HideImmediate();
            campaignStarted = true;
            bookBusy = false;
            RestoreGameplay();
            yield return FadeTo(0f);
            TryAnnounceHoldUnlock();
        }

        private IEnumerator StageClearRoutine()
        {
            int clearedStage = CurrentStageIndex;
            FreezeGameplay();
            yield return FadeTo(1f);

            if (TryGetClearPage(clearedStage, out string text, out Sprite illustration))
            {
                bool pageDone = false;
                bookUi.ShowPage(text, illustration, () => pageDone = true);
                yield return FadeTo(0f);
                while (!pageDone)
                {
                    yield return null;
                }

                yield return FadeTo(1f);
            }

            if (clearedStage >= 5)
            {
                yield return ShowEndingAndReturnToTitle();
                yield break;
            }

            currentStageIndex = Mathf.Max(1, clearedStage + 1);
            ResolveMap();
            if (mapSystem != null)
            {
                mapSystem.GenerateNextStage();
            }

            SyncStageFromMap();
            campaignStarted = true;
            RestoreGameplay();
            yield return FadeTo(0f);
            bookBusy = false;
            TryAnnounceHoldUnlock();
        }

        private IEnumerator ShowEndingAndReturnToTitle()
        {
            if (bookUi != null)
            {
                bool endingDone = false;
                bookUi.ShowPage(
                    CampaignStoryCopy.Ending,
                    null,
                    CampaignStoryCopy.EndingContinueHint,
                    () => endingDone = true);
                yield return FadeTo(0f);
                while (!endingDone)
                {
                    yield return null;
                }

                yield return FadeTo(1f);
            }

            ReturnToTitleAfterEnding();
            yield return FadeTo(0f);
            bookBusy = false;
        }

        private void ReturnToTitleAfterEnding()
        {
            ResolveMap();
            if (mapSystem != null)
            {
                mapSystem.ResetAndGenerate();
            }

            currentStageIndex = 1;
            SyncStageFromMap();
            holdUnlockCueShownThisRun = false;
            campaignStarted = false;
            bookUi?.HideImmediate();
            FreezeGameplay();
            titleScreen?.Show(BeginPrologueFromTitle);
        }

        private static bool TryGetClearPage(int clearedStage, out string text, out Sprite illustration)
        {
            illustration = null;
            switch (clearedStage)
            {
                case 1:
                    text = CampaignStoryCopy.PrologueAndStage1;
                    illustration = CampaignArt.TryGetStage1Illustration();
                    return true;
                case 2:
                    text = CampaignStoryCopy.Stage2;
                    return true;
                case 3:
                    text = CampaignStoryCopy.Stage3;
                    return true;
                case 4:
                    text = CampaignStoryCopy.Stage4;
                    return true;
                case 5:
                    text = CampaignStoryCopy.Stage5;
                    return true;
                default:
                    text = null;
                    return false;
            }
        }

        private void BeginGameplayImmediate()
        {
            titleScreen?.HideImmediate();
            bookUi?.HideImmediate();
            campaignStarted = true;
            bookBusy = false;
            RestoreGameplay();
            if (fadeGroup != null)
            {
                fadeGroup.alpha = 0f;
                fadeGroup.blocksRaycasts = false;
            }

            TryAnnounceHoldUnlock();
        }

        private void TryAnnounceHoldUnlock()
        {
            if (holdUnlockCueShownThisRun
                || RegressionChecklistRunner.IsRegressionRunActive
                || CurrentStageIndex < 3)
            {
                return;
            }

            holdUnlockCueShownThisRun = true;
            RuntimeEventBus.Raise(
                RuntimeEventType.Ability,
                CampaignStoryCopy.HoldUnlockCue,
                this,
                CurrentStageIndex);
        }

        private bool ShouldHoldAtTitle()
        {
            return showTitleOnPlay && !RegressionChecklistRunner.IsRegressionRunActive;
        }

        private void FreezeGameplay()
        {
            if (Time.timeScale > 0.0001f)
            {
                restoredTimeScale = Time.timeScale;
            }

            Time.timeScale = 0f;
        }

        private void RestoreGameplay()
        {
            Time.timeScale = Mathf.Max(0.0001f, restoredTimeScale);
        }

        private void ResolveMap()
        {
            if (mapSystem == null)
            {
                mapSystem = FindFirstObjectByType<MapSystem>();
            }
        }

        private void SyncStageFromMap()
        {
            if (mapSystem != null)
            {
                currentStageIndex = Mathf.Max(1, mapSystem.CurrentStage);
            }
        }

        private void EnsureCampaignUi()
        {
            titleScreen = TitleScreen.EnsureInstance();
            bookUi = BookUI.EnsureInstance();
            EnsureFadeOverlay();
        }

        private void EnsureFadeOverlay()
        {
            if (fadeGroup != null)
            {
                return;
            }

            GameObject canvasObject = new("CampaignFade_Canvas");
            canvasObject.transform.SetParent(transform, false);
            Canvas fadeCanvas = canvasObject.AddComponent<Canvas>();
            fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            fadeCanvas.sortingOrder = 280;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            GameObject imageObject = new("CampaignFade");
            imageObject.transform.SetParent(canvasObject.transform, false);
            RectTransform rect = imageObject.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = imageObject.AddComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;
            fadeGroup = canvasObject.AddComponent<CanvasGroup>();
            fadeGroup.alpha = 0f;
            fadeGroup.blocksRaycasts = false;
        }

        private IEnumerator FadeTo(float target)
        {
            EnsureFadeOverlay();
            float start = fadeGroup.alpha;
            float duration = Mathf.Max(0.05f, fadeSeconds);
            float elapsed = 0f;
            fadeGroup.blocksRaycasts = target > 0.5f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                fadeGroup.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            fadeGroup.alpha = target;
            fadeGroup.blocksRaycasts = target > 0.5f;
        }
    }
}
