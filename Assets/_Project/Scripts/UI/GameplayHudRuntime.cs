using System;
using System.Collections.Generic;
using System.Text;
using LostBreadcrumbs.Runtime.AI;
using LostBreadcrumbs.Runtime.Events;
using LostBreadcrumbs.Runtime.Managers;
using LostBreadcrumbs.Runtime.Map;
using LostBreadcrumbs.Runtime.Player;
using UnityEngine;
using UnityEngine.UI;

namespace LostBreadcrumbs.Runtime.UI
{
    public sealed class GameplayHudRuntime : MonoBehaviour
    {
        [Header("Auto Build")]
        [SerializeField] private bool autoBuildHud = true;
        [SerializeField] private Font hudFont;

        [Header("Threat")]
        [SerializeField, Min(2f)] private float threatRange = 10f;
        [SerializeField, Min(0.05f)] private float enemyRefreshInterval = 0.35f;
        [SerializeField, Min(0.1f)] private float missingReferenceResolveInterval = 0.8f;

        [Header("Alerts")]
        [SerializeField] private bool showAlertFeed = true;
        [SerializeField, Min(2)] private int maxAlertLines = 6;
        [SerializeField, Min(2f)] private float alertLifetimeSeconds = 10f;
        [SerializeField, Min(24)] private int maxAlertMessageChars = 78;
        [SerializeField, Min(0f)] private float alertDuplicateSuppressSeconds = 0.9f;
        [SerializeField] private bool compactAlertTypeLabels = true;
        [SerializeField] private bool canonicalizeCriticalAlerts = true;
        [SerializeField] private bool includeObjectiveAlerts = true;
        [SerializeField] private bool suppressRoutineObjectiveProgressAlerts = true;
        [SerializeField] private bool includeAbilityAlerts = true;
        [SerializeField] private bool includeDeathAlerts = true;
        [SerializeField] private bool includeStageAlerts;
        [SerializeField] private bool includeRunAlerts;
        [SerializeField] private bool includeSaveLoadAlerts;
        [SerializeField] private bool includeSystemAlerts;

        [Header("Status Colors")]
        [SerializeField] private Color staminaNormalColor = new(0.3f, 0.85f, 1f, 0.95f);
        [SerializeField] private Color quietBreathStaminaColor = new(0.22f, 1f, 0.78f, 0.96f);
        [SerializeField] private Color quietBreathStrainedColor = new(1f, 0.72f, 0.36f, 0.96f);

        private PlayerVitalSystem playerVitals;
        private PlayerDummyController playerController;
        private PlayerEchoPulseAbility pulseAbility;
        private PlayerDecoyAbility decoyAbility;
        private PlayerSmokeAbility smokeAbility;
        private PlayerBehaviorTelemetry telemetry;

        private RectTransform hudRoot;
        private Text hpText;
        private Image hpFill;
        private Text staminaText;
        private Image staminaFill;
        private Text objectiveText;
        private Text abilityText;
        private Text learningText;
        private Text threatText;
        private Image threatBackground;
        private Text alertFeedText;

        private readonly List<EnemyController> cachedEnemies = new(16);
        private readonly List<RuntimeEventRecord> alertFeedRecords = new();
        private RuntimeEventRecord lastAcceptedAlert;
        private float nextEnemyRefreshTime;
        private float nextReferenceResolveTime;
        private Sprite whiteSprite;

        private void Awake()
        {
            TryResolveGameplayRefs(force: true);

            if (autoBuildHud)
            {
                BuildHudIfNeeded();
            }
        }

        private void OnEnable()
        {
            alertFeedRecords.Clear();
            lastAcceptedAlert = default;
            RuntimeEventBus.EventRaised -= HandleRuntimeEvent;
            RuntimeEventBus.EventRaised += HandleRuntimeEvent;
            SeedAlertFeedFromManager();
        }

        private void OnDisable()
        {
            RuntimeEventBus.EventRaised -= HandleRuntimeEvent;
        }
        private void Update()
        {
            TryResolveGameplayRefs();
            RefreshEnemyCache();
            UpdateHud();
        }

        private void TryResolveGameplayRefs(bool force = false)
        {
            if (!force)
            {
                if (HasAllGameplayRefs())
                {
                    return;
                }

                if (Time.unscaledTime < nextReferenceResolveTime)
                {
                    return;
                }

                nextReferenceResolveTime = Time.unscaledTime + Mathf.Max(0.1f, missingReferenceResolveInterval);
            }

            ResolveGameplayRefs();
        }

        private bool HasAllGameplayRefs()
        {
            return playerVitals != null
                   && playerController != null
                   && pulseAbility != null
                   && decoyAbility != null
                   && smokeAbility != null
                   && telemetry != null;
        }

        private void ResolveGameplayRefs()
        {
            if (playerVitals == null)
            {
                playerVitals = FindFirstObjectByType<PlayerVitalSystem>();
            }

            if (playerController == null)
            {
                playerController = FindFirstObjectByType<PlayerDummyController>();
            }

            if (pulseAbility == null)
            {
                pulseAbility = FindFirstObjectByType<PlayerEchoPulseAbility>();
            }

            if (decoyAbility == null)
            {
                decoyAbility = FindFirstObjectByType<PlayerDecoyAbility>();
            }

            if (smokeAbility == null)
            {
                smokeAbility = FindFirstObjectByType<PlayerSmokeAbility>();
            }

            if (telemetry == null)
            {
                telemetry = FindFirstObjectByType<PlayerBehaviorTelemetry>();
            }
        }

        private void RefreshEnemyCache()
        {
            if (Time.time < nextEnemyRefreshTime)
            {
                return;
            }

            nextEnemyRefreshTime = Time.time + enemyRefreshInterval;
            EnemyController.CopyActiveControllers(cachedEnemies);
        }

        private void BuildHudIfNeeded()
        {
            if (hudRoot != null)
            {
                return;
            }

            Canvas canvas = GetComponentInChildren<Canvas>(true);
            if (canvas == null)
            {
                GameObject canvasObject = new("HUD_Canvas");
                canvasObject.transform.SetParent(transform, false);
                canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 110;
                CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                canvasObject.AddComponent<GraphicRaycaster>();
            }

            GameObject rootObject = CreateUiObject("HUD_Root", canvas.transform);
            hudRoot = rootObject.GetComponent<RectTransform>();
            hudRoot.anchorMin = new Vector2(0f, 1f);
            hudRoot.anchorMax = new Vector2(0f, 1f);
            hudRoot.pivot = new Vector2(0f, 1f);
            hudRoot.anchoredPosition = new Vector2(20f, -20f);
            hudRoot.sizeDelta = new Vector2(470f, 250f);

            Image panel = rootObject.AddComponent<Image>();
            panel.sprite = GetWhiteSprite();
            panel.color = new Color(0.04f, 0.06f, 0.08f, 0.76f);
            panel.type = Image.Type.Sliced;

            hpText = CreateText("HP_Text", hudRoot, new Vector2(16f, -16f), new Vector2(420f, 24f), 20, FontStyle.Bold);
            hpFill = CreateBar("HP_Bar", hudRoot, new Vector2(16f, -46f), new Vector2(420f, 14f), new Color(0.95f, 0.25f, 0.3f, 0.95f));

            staminaText = CreateText("Stamina_Text", hudRoot, new Vector2(16f, -68f), new Vector2(420f, 24f), 18, FontStyle.Bold);
            staminaFill = CreateBar("Stamina_Bar", hudRoot, new Vector2(16f, -98f), new Vector2(420f, 14f), staminaNormalColor);

            objectiveText = CreateText("Objective_Text", hudRoot, new Vector2(16f, -126f), new Vector2(430f, 24f), 16, FontStyle.Bold);
            abilityText = CreateText("Ability_Text", hudRoot, new Vector2(16f, -152f), new Vector2(430f, 50f), 15, FontStyle.Normal);
            learningText = CreateText("Learning_Text", hudRoot, new Vector2(16f, -204f), new Vector2(430f, 42f), 14, FontStyle.Italic);

            GameObject threatObject = CreateUiObject("Threat_Banner", canvas.transform);
            RectTransform threatRect = threatObject.GetComponent<RectTransform>();
            threatRect.anchorMin = new Vector2(0.5f, 1f);
            threatRect.anchorMax = new Vector2(0.5f, 1f);
            threatRect.pivot = new Vector2(0.5f, 1f);
            threatRect.anchoredPosition = new Vector2(0f, -22f);
            threatRect.sizeDelta = new Vector2(460f, 40f);

            threatBackground = threatObject.AddComponent<Image>();
            threatBackground.sprite = GetWhiteSprite();
            threatBackground.color = new Color(0.15f, 0.2f, 0.24f, 0.85f);

            threatText = CreateText("Threat_Text", threatRect, new Vector2(10f, -4f), new Vector2(440f, 32f), 20, FontStyle.Bold, TextAnchor.MiddleCenter);
            threatText.text = "위험도: 안정";

            GameObject alertObject = CreateUiObject("AlertFeed_Panel", canvas.transform);
            RectTransform alertRect = alertObject.GetComponent<RectTransform>();
            alertRect.anchorMin = new Vector2(1f, 1f);
            alertRect.anchorMax = new Vector2(1f, 1f);
            alertRect.pivot = new Vector2(1f, 1f);
            alertRect.anchoredPosition = new Vector2(-20f, -18f);
            alertRect.sizeDelta = new Vector2(520f, 136f);

            Image alertBackground = alertObject.AddComponent<Image>();
            alertBackground.sprite = GetWhiteSprite();
            alertBackground.color = new Color(0.03f, 0.04f, 0.06f, 0.72f);

            alertFeedText = CreateText("AlertFeed_Text", alertRect, new Vector2(12f, -10f), new Vector2(496f, 116f), 13, FontStyle.Normal, TextAnchor.UpperLeft);
            alertFeedText.horizontalOverflow = HorizontalWrapMode.Wrap;
            alertFeedText.verticalOverflow = VerticalWrapMode.Truncate;
            alertFeedText.text = "Event feed standby";
        }

        private void UpdateHud()
        {
            if (hudRoot == null)
            {
                return;
            }

            UpdateHealthAndStamina();
            UpdateObjectives();
            UpdateAbilityLine();
            UpdateLearningLine();
            UpdateThreatBanner();
            UpdateAlertFeed();
        }

        private void UpdateHealthAndStamina()
        {
            if (playerVitals != null)
            {
                hpText.text = $"체력 {playerVitals.CurrentHealth}/{playerVitals.MaxHealth}";
                hpFill.fillAmount = Mathf.Clamp01(playerVitals.MaxHealth > 0 ? (float)playerVitals.CurrentHealth / playerVitals.MaxHealth : 0f);
            }
            else
            {
                hpText.text = "체력 정보 없음";
                hpFill.fillAmount = 0f;
            }

            if (playerController != null)
            {
                float quietRemaining = playerController.TemporaryNoiseDampeningRemaining;
                bool quietStrained = playerController.IsTemporaryNoiseDampeningStrained;
                string breathLabel = quietStrained ? "숨 가쁨" : "숨";
                staminaText.text = quietRemaining > 0.05f
                    ? $"스태미나 {playerController.CurrentStamina:0.0}/{playerController.MaxStamina:0.0}  {breathLabel} {quietRemaining:0.0}s"
                    : $"스태미나 {playerController.CurrentStamina:0.0}/{playerController.MaxStamina:0.0}";
                staminaFill.fillAmount = Mathf.Clamp01(playerController.MaxStamina > 0f ? playerController.CurrentStamina / playerController.MaxStamina : 0f);
                float breathPulse = quietRemaining > 0.05f
                    ? (quietStrained
                        ? 0.72f + Mathf.Sin(Time.unscaledTime * 9.2f) * 0.26f
                        : 0.65f + Mathf.Sin(Time.unscaledTime * 5.4f) * 0.18f)
                    : 0f;
                staminaFill.color = quietRemaining > 0.05f
                    ? Color.Lerp(staminaNormalColor, quietStrained ? quietBreathStrainedColor : quietBreathStaminaColor, Mathf.Clamp01(breathPulse))
                    : staminaNormalColor;
            }
            else
            {
                staminaText.text = "스태미나 정보 없음";
                staminaFill.fillAmount = 0f;
                staminaFill.color = staminaNormalColor;
            }
        }

        private void UpdateObjectives()
        {
            StageLoopDirector stageLoop = StageLoopDirector.Instance;
            if (stageLoop == null)
            {
                objectiveText.text = "목표: 스테이지 준비 중";
                return;
            }

            string momentum = stageLoop.HasBreadcrumbMomentum
                ? $"  |  연쇄 x{stageLoop.BreadcrumbMomentumLevel} {stageLoop.BreadcrumbMomentumRemaining:0.0}s"
                : string.Empty;
            string exitCache = string.Empty;
            if (stageLoop.ExitChoiceCacheActive)
            {
                float distance = playerController != null
                    ? Vector2.Distance(playerController.transform.position, stageLoop.ExitChoiceCacheWorldPosition)
                    : 0f;
                exitCache = playerController != null ? $"  |  보상 {distance:0.0}m" : "  |  보상 노출";
            }

            string riskCache = string.Empty;
            if (playerController != null
                && stageLoop.TryGetNearestRiskCacheTarget(playerController.transform.position, out _, out float riskDistance))
            {
                riskCache = $"  |  위험보상 {riskDistance:0.0}m";
            }
            else if (stageLoop.ActiveRiskCacheCount > 0)
            {
                riskCache = "  |  위험보상";
            }

            objectiveText.text = $"목표: Breadcrumb {stageLoop.CollectedBreadcrumbs}/{stageLoop.RequiredBreadcrumbs}  |  스테이지 {stageLoop.CurrentStage}  |  출구 {(stageLoop.ExitUnlocked ? "OPEN" : "LOCKED")}{momentum}{exitCache}{riskCache}";
        }

        private void UpdateAbilityLine()
        {
            string pulse;
            if (pulseAbility != null && pulseAbility.IsEchoReturnWarningActive)
            {
                string count = pulseAbility.LastEchoReturnThreatCount > 1 ? $" x{pulseAbility.LastEchoReturnThreatCount}" : string.Empty;
                pulse = $"Q 응답{count} {pulseAbility.LastEchoReturnDistance:0.0}m";
            }
            else if (pulseAbility != null && pulseAbility.IsEchoResonating)
            {
                pulse = $"Q 잔향 {pulseAbility.EchoResonanceRemaining:0.0}s";
            }
            else
            {
                pulse = FormatAbility("Q 펄스", pulseAbility != null && pulseAbility.IsReady, pulseAbility != null ? pulseAbility.CooldownRemaining : 0f);
            }

            string decoy = FormatAbility("E 디코이", decoyAbility != null && decoyAbility.IsReady, decoyAbility != null ? decoyAbility.CooldownRemaining : 0f);
            string smoke = FormatAbility("R 스모크", smokeAbility != null && smokeAbility.IsReady, smokeAbility != null ? smokeAbility.CooldownRemaining : 0f);

            string scanStatus = BuildEchoObjectiveScanStatus();
            abilityText.text = string.IsNullOrEmpty(scanStatus)
                ? $"{pulse}   |   {decoy}   |   {smoke}"
                : $"{pulse}   |   {decoy}   |   {smoke}\n{scanStatus}";
        }

        private string BuildEchoObjectiveScanStatus()
        {
            if (playerController == null || playerController.EchoObjectiveScanStatusRemaining <= 0.05f)
            {
                return string.Empty;
            }

            string primary = playerController.LastEchoObjectivePrimaryWasExit ? "출구" : "빵조각";
            int choices = Mathf.Max(0, playerController.LastEchoObjectiveChoiceScanCount);
            if (choices <= 0)
            {
                return $"Space Echo: {primary} 경로 {playerController.EchoObjectiveScanStatusRemaining:0.0}s";
            }

            return $"Space Echo: {primary} + 선택 {choices}개 {playerController.EchoObjectiveScanStatusRemaining:0.0}s";
        }

        private void UpdateLearningLine()
        {
            if (telemetry == null)
            {
                learningText.text = "학습 텔레메트리 연결 대기";
                return;
            }

            LearningSnapshot snapshot = telemetry.GetSnapshot();
            learningText.text = $"학습단계 {snapshot.Phase}  |  점수 {snapshot.BehaviorScore:0.00}  |  가중치 L/P {snapshot.LearningWeight:0.00}/{snapshot.PredictionWeight:0.00}";
        }

        private void UpdateThreatBanner()
        {
            float score = EvaluateThreatScore(out bool chasing, out float minDistance);
            string status;
            Color color;

            if (chasing || score >= 0.95f)
            {
                status = "추격 중";
                color = new Color(0.95f, 0.2f, 0.22f, 0.95f);
            }
            else if (score >= 0.72f)
            {
                status = "위험";
                color = new Color(1f, 0.45f, 0.2f, 0.92f);
            }
            else if (score >= 0.48f)
            {
                status = "경계";
                color = new Color(1f, 0.8f, 0.25f, 0.9f);
            }
            else if (score >= 0.24f)
            {
                status = "주의";
                color = new Color(0.55f, 0.88f, 1f, 0.9f);
            }
            else
            {
                status = "안정";
                color = new Color(0.35f, 0.95f, 0.6f, 0.9f);
            }

            float pulse = 0.82f + Mathf.Sin(Time.time * 5.2f) * 0.06f;
            threatBackground.color = new Color(color.r * 0.4f, color.g * 0.4f, color.b * 0.4f, Mathf.Clamp01(pulse));
            threatText.color = color;

            string distanceText = minDistance < float.MaxValue ? $"{minDistance:0.0}m" : "-";
            threatText.text = $"위험도: {status}  (점수 {score:0.00}, 최근접 적 {distanceText})";
        }

        private float EvaluateThreatScore(out bool chasing, out float minDistance)
        {
            chasing = false;
            minDistance = float.MaxValue;

            if (playerController == null || cachedEnemies.Count == 0)
            {
                return 0f;
            }

            Vector2 origin = playerController.transform.position;
            float maxScore = 0f;

            for (int i = 0; i < cachedEnemies.Count; i++)
            {
                EnemyController enemy = cachedEnemies[i];
                if (enemy == null)
                {
                    continue;
                }

                float distance = Vector2.Distance(origin, enemy.transform.position);
                minDistance = Mathf.Min(minDistance, distance);

                float distanceFactor = 1f - Mathf.Clamp01(distance / threatRange);
                float stateWeight = enemy.CurrentState switch
                {
                    EnemyStateId.Chase => 1f,
                    EnemyStateId.Investigate => 0.68f,
                    EnemyStateId.Search => 0.56f,
                    EnemyStateId.Suspicion => 0.42f,
                    EnemyStateId.Return => 0.28f,
                    EnemyStateId.Stunned => 0.14f,
                    _ => 0.18f
                };

                float score = stateWeight * Mathf.Lerp(0.2f, 1f, distanceFactor) + enemy.Suspicion * 0.32f;
                if (enemy.CurrentState == EnemyStateId.Chase)
                {
                    chasing = true;
                    score = Mathf.Max(score, 0.95f);
                }

                maxScore = Mathf.Max(maxScore, score);
            }

            if (playerVitals != null && playerVitals.IsInvulnerable)
            {
                maxScore *= 0.75f;
            }

            return Mathf.Clamp01(maxScore);
        }

        private void HandleRuntimeEvent(RuntimeEventRecord record)
        {
            if (!showAlertFeed || !record.IsValid || !IsAlertEventAllowed(record.Type) || IsRoutineAlertSuppressed(record))
            {
                return;
            }
            if (ShouldSuppressDuplicateAlert(record))
            {
                return;
            }

            alertFeedRecords.Add(record);
            lastAcceptedAlert = record;
            TrimAlertFeed();
        }

        private void SeedAlertFeedFromManager()
        {
            if (EventManager.Instance == null)
            {
                return;
            }

            IReadOnlyList<RuntimeEventRecord> recent = EventManager.Instance.RecentEvents;
            if (recent == null || recent.Count == 0)
            {
                return;
            }

            int safeMax = Mathf.Max(2, maxAlertLines);
            for (int i = recent.Count - 1; i >= 0 && alertFeedRecords.Count < safeMax; i--)
            {
                if (IsAlertEventAllowed(recent[i].Type) && !IsRoutineAlertSuppressed(recent[i]))
                {
                    alertFeedRecords.Insert(0, recent[i]);
                }
            }

            if (alertFeedRecords.Count > 0)
            {
                lastAcceptedAlert = alertFeedRecords[alertFeedRecords.Count - 1];
            }

            TrimAlertFeed();
        }

        private bool IsAlertEventAllowed(RuntimeEventType type)
        {
            return type switch
            {
                RuntimeEventType.Objective => includeObjectiveAlerts,
                RuntimeEventType.Ability => includeAbilityAlerts,
                RuntimeEventType.Death => includeDeathAlerts,
                RuntimeEventType.Stage => includeStageAlerts,
                RuntimeEventType.Run => includeRunAlerts,
                RuntimeEventType.Save => includeSaveLoadAlerts,
                RuntimeEventType.Load => includeSaveLoadAlerts,
                RuntimeEventType.System => includeSystemAlerts,
                _ => false
            };
        }

        private bool IsRoutineAlertSuppressed(RuntimeEventRecord record)
        {
            if (!record.IsValid)
            {
                return true;
            }

            if (suppressRoutineObjectiveProgressAlerts
                && record.Type == RuntimeEventType.Objective
                && record.Semantic == RuntimeEventSemantic.None
                && LooksLikeBreadcrumbProgress(record.Message))
            {
                return true;
            }

            return false;
        }

        private void TrimAlertFeed()
        {
            if (alertFeedRecords.Count == 0)
            {
                return;
            }

            float safeLifetime = Mathf.Max(2f, alertLifetimeSeconds);
            float threshold = Mathf.Max(0f, Time.realtimeSinceStartup - safeLifetime);

            for (int i = alertFeedRecords.Count - 1; i >= 0; i--)
            {
                if (alertFeedRecords[i].RealtimeSinceStartup < threshold)
                {
                    alertFeedRecords.RemoveAt(i);
                }
            }

            int maxLines = Mathf.Max(2, maxAlertLines);
            int overflow = alertFeedRecords.Count - maxLines;
            if (overflow > 0)
            {
                alertFeedRecords.RemoveRange(0, overflow);
            }
        }

        private void UpdateAlertFeed()
        {
            if (alertFeedText == null)
            {
                return;
            }

            if (!showAlertFeed)
            {
                alertFeedText.text = string.Empty;
                return;
            }

            TrimAlertFeed();

            if (alertFeedRecords.Count == 0)
            {
                alertFeedText.text = "Event feed standby";
                return;
            }

            int maxLines = Mathf.Max(2, maxAlertLines);
            int start = Mathf.Max(0, alertFeedRecords.Count - maxLines);
            StringBuilder builder = new();

            for (int i = alertFeedRecords.Count - 1; i >= start; i--)
            {
                RuntimeEventRecord record = alertFeedRecords[i];
                builder.Append(BuildAlertLine(record));

                if (i > start)
                {
                    builder.AppendLine();
                }
            }

            alertFeedText.text = builder.ToString();
        }

        private bool ShouldSuppressDuplicateAlert(RuntimeEventRecord record)
        {
            if (!lastAcceptedAlert.IsValid)
            {
                return false;
            }

            if (record.Type != lastAcceptedAlert.Type)
            {
                return false;
            }

            float suppressWindow = Mathf.Max(0f, alertDuplicateSuppressSeconds);
            if (suppressWindow <= 0f)
            {
                return false;
            }

            float delta = record.RealtimeSinceStartup - lastAcceptedAlert.RealtimeSinceStartup;
            if (delta > suppressWindow)
            {
                return false;
            }

            if (record.Semantic != RuntimeEventSemantic.None || lastAcceptedAlert.Semantic != RuntimeEventSemantic.None)
            {
                if (record.Semantic != RuntimeEventSemantic.None && record.Semantic == lastAcceptedAlert.Semantic)
                {
                    return true;
                }

                if (record.Semantic != lastAcceptedAlert.Semantic)
                {
                    return false;
                }
            }

            string current = NormalizeAlertMessage(record.Message);
            string previous = NormalizeAlertMessage(lastAcceptedAlert.Message);
            return string.Equals(current, previous, StringComparison.OrdinalIgnoreCase);
        }

        private string BuildAlertLine(RuntimeEventRecord record)
        {
            string message = BuildAlertMessage(record);
            string typeLabel = compactAlertTypeLabels ? GetCompactTypeLabel(record.Type) : record.TypeLabel;
            return $"[{record.TimeLabel}] {typeLabel} > {message}";
        }

        private string BuildAlertMessage(RuntimeEventRecord record)
        {
            if (canonicalizeCriticalAlerts && TryGetCanonicalAlertMessage(record, out string canonical))
            {
                return canonical;
            }

            int safeMax = Mathf.Max(24, maxAlertMessageChars);
            return TrimAlertToken(record.Message, safeMax);
        }

        private bool TryGetCanonicalAlertMessage(RuntimeEventRecord record, out string message)
        {
            message = string.Empty;
            string source = record.Message ?? string.Empty;

            if (record.Type == RuntimeEventType.Death)
            {
                message = "RUN FAILED";
                return true;
            }

            message = record.Semantic switch
            {
                RuntimeEventSemantic.ExitUnlocked => "EXIT OPEN",
                RuntimeEventSemantic.LockOnWarning => "LOCK-ON WARNING",
                RuntimeEventSemantic.ChaseStarted => "CHASE STARTED",
                RuntimeEventSemantic.ChaseDisengaged => "CHASE DISENGAGED",
                RuntimeEventSemantic.EscapeRelief => "BREATH FOUND",
                RuntimeEventSemantic.QuietBreathBroken => "BREATH BROKE",
                RuntimeEventSemantic.EchoReturn => "ECHO RETURN",
                RuntimeEventSemantic.EchoChoiceScan => "ECHO CHOICES",
                RuntimeEventSemantic.RiskReward => "RISK CACHE TAKEN",
                RuntimeEventSemantic.SafeHavenThin => "HAVEN THINS",
                RuntimeEventSemantic.PressureWave => "PRESSURE WAVE",
                RuntimeEventSemantic.SetPieceShift => "SET-PIECE SHIFT",
                _ => string.Empty
            };
            if (!string.IsNullOrEmpty(message))
            {
                return true;
            }

            if (record.Type == RuntimeEventType.Objective && ContainsKeyword(source, "exit unlocked"))
            {
                message = "EXIT OPEN";
                return true;
            }

            if (record.Type == RuntimeEventType.System && ContainsKeyword(source, "lock-on warning"))
            {
                message = "LOCK-ON WARNING";
                return true;
            }

            if (record.Type == RuntimeEventType.System && ContainsKeyword(source, "chase started"))
            {
                message = "CHASE STARTED";
                return true;
            }

            if (record.Type == RuntimeEventType.System && ContainsKeyword(source, "chase disengaged"))
            {
                message = "CHASE DISENGAGED";
                return true;
            }

            if (record.Type == RuntimeEventType.Stage && ContainsKeyword(source, "setpiece"))
            {
                message = "SET-PIECE SHIFT";
                return true;
            }

            return false;
        }

        private static string GetCompactTypeLabel(RuntimeEventType type)
        {
            return type switch
            {
                RuntimeEventType.Objective => "OBJ",
                RuntimeEventType.Ability => "ABL",
                RuntimeEventType.Death => "DTH",
                RuntimeEventType.Stage => "STG",
                RuntimeEventType.Run => "RUN",
                RuntimeEventType.Save => "SAVE",
                RuntimeEventType.Load => "LOAD",
                RuntimeEventType.System => "SYS",
                _ => "EVT"
            };
        }

        private static string TrimAlertToken(string source, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return "-";
            }

            string normalized = NormalizeAlertMessage(source);
            int safeLength = Mathf.Max(8, maxLength);
            if (normalized.Length <= safeLength)
            {
                return normalized;
            }

            return normalized.Substring(0, safeLength - 1) + "...";
        }

        private static string NormalizeAlertMessage(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return string.Empty;
            }

            return source.Trim().Replace('\n', ' ').Replace('\r', ' ');
        }

        private static bool ContainsKeyword(string source, string keyword)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(keyword))
            {
                return false;
            }

            return source.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool LooksLikeBreadcrumbProgress(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            string normalized = source.Trim();
            return normalized.StartsWith("Breadcrumb ", StringComparison.OrdinalIgnoreCase)
                   && normalized.IndexOf("/", StringComparison.OrdinalIgnoreCase) > 0;
        }

        private string FormatAbility(string label, bool ready, float cooldown)
        {
            return ready ? $"{label}: 준비" : $"{label}: {cooldown:0.0}s";
        }

        private Image CreateBar(string name, RectTransform parent, Vector2 anchoredPosition, Vector2 size, Color fillColor)
        {
            GameObject backgroundObject = CreateUiObject(name + "_Bg", parent);
            RectTransform bgRect = backgroundObject.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 1f);
            bgRect.anchorMax = new Vector2(0f, 1f);
            bgRect.pivot = new Vector2(0f, 1f);
            bgRect.anchoredPosition = anchoredPosition;
            bgRect.sizeDelta = size;

            Image background = backgroundObject.AddComponent<Image>();
            background.sprite = GetWhiteSprite();
            background.color = new Color(0f, 0f, 0f, 0.55f);

            GameObject fillObject = CreateUiObject(name + "_Fill", bgRect);
            RectTransform fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);

            Image fill = fillObject.AddComponent<Image>();
            fill.sprite = GetWhiteSprite();
            fill.color = fillColor;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 1f;
            return fill;
        }

        private Text CreateText(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, int fontSize, FontStyle style, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            GameObject textObject = CreateUiObject(name, parent);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 1f);
            textRect.anchorMax = new Vector2(0f, 1f);
            textRect.pivot = new Vector2(0f, 1f);
            textRect.anchoredPosition = anchoredPosition;
            textRect.sizeDelta = size;

            Text text = textObject.AddComponent<Text>();
            text.font = hudFont != null ? hudFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = new Color(0.93f, 0.96f, 1f, 1f);
            text.text = string.Empty;
            return text;
        }

        private GameObject CreateUiObject(string name, Transform parent)
        {
            GameObject obj = new(name);
            obj.transform.SetParent(parent, false);
            obj.AddComponent<RectTransform>();
            return obj;
        }

        private Sprite GetWhiteSprite()
        {
            if (whiteSprite != null)
            {
                return whiteSprite;
            }

            Texture2D texture = new(1, 1, TextureFormat.RGBA32, false)
            {
                name = "HUD_WhiteTexture",
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            whiteSprite.name = "HUD_WhiteSprite";
            whiteSprite.hideFlags = HideFlags.HideAndDontSave;
            return whiteSprite;
        }
    }
}
