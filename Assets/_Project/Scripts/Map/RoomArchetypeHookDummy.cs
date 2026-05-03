using System.Collections.Generic;
using LostBreadcrumbs.Runtime.Managers;
using LostBreadcrumbs.Runtime.Player;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Map
{
    public enum RoomArchetypeHookVariant
    {
        LooseMetal,
        HangingChain,
        CrackedGlass,
        RustedVent,
        ClothRustle,
        AlarmDebris
    }

    public sealed class RoomArchetypeHookDummy : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private MapCellKind sourceCellKind = MapCellKind.Room;
        [SerializeField] private RoomArchetypeHookVariant variant = RoomArchetypeHookVariant.LooseMetal;

        [Header("Trigger")]
        [SerializeField, Min(0.1f)] private float triggerRadius = 0.32f;
        [SerializeField] private bool emitOnPlayerEnter = true;
        [SerializeField] private bool emitWhilePlayerInside = true;
        [SerializeField, Min(0.05f)] private float cooldownSeconds = 7f;
        [SerializeField] private bool ensureTriggerCollider = true;

        [Header("Noise")]
        [SerializeField] private NoiseKind noiseKind = NoiseKind.ItemUse;
        [SerializeField, Min(0.1f)] private float noiseLoudness = 1f;
        [SerializeField, Min(0.5f)] private float noiseRadius = 6f;

        [Header("Visual")]
        [SerializeField] private Color idleColor = new(0.92f, 0.54f, 0.32f, 0.86f);
        [SerializeField] private Color activeColor = new(1f, 0.74f, 0.42f, 0.98f);
        [SerializeField, Min(0.1f)] private float breatheSpeed = 2.1f;

        [Header("Telegraph")]
        [SerializeField] private bool usePreEmitTelegraph = true;
        [SerializeField, Min(0.02f)] private float preEmitLeadTime = 0.34f;
        [SerializeField] private Color warningColor = new(1f, 0.28f, 0.2f, 0.94f);
        [SerializeField, Range(0f, 0.45f)] private float telegraphScaleBoost = 0.16f;
        [SerializeField, Min(0.1f)] private float warningPulseSpeed = 11f;

        [Header("Stage Readability")]
        [SerializeField] private bool scaleTelegraphByStage = true;
        [SerializeField, Min(1)] private int telegraphRampStartStage = 1;
        [SerializeField, Min(2)] private int telegraphRampPeakStage = 5;
        [SerializeField, Range(0.4f, 2f)] private float lowStageTelegraphLeadMultiplier = 0.82f;
        [SerializeField, Range(0.4f, 2f)] private float highStageTelegraphLeadMultiplier = 1.34f;
        [SerializeField, Range(0.5f, 2f)] private float lowStageWarningPulseSpeedMultiplier = 0.82f;
        [SerializeField, Range(0.5f, 2f)] private float highStageWarningPulseSpeedMultiplier = 1.22f;
        [SerializeField, Range(0.4f, 2f)] private float lowStageTelegraphScaleMultiplier = 0.72f;
        [SerializeField, Range(0.4f, 2f)] private float highStageTelegraphScaleMultiplier = 1.28f;
        [SerializeField, Range(0.6f, 1.4f)] private float lowStageWarningAlphaMultiplier = 0.88f;
        [SerializeField, Range(0.6f, 1.6f)] private float highStageWarningAlphaMultiplier = 1.12f;

        [Header("Risk Room Bonus")]
        [SerializeField] private bool grantRiskRoomAdrenalineBonus = true;
        [SerializeField, Range(0f, 1f)] private float riskRoomBonusChance = 0.42f;
        [SerializeField, Range(0f, 0.5f)] private float riskRoomBonusPressureChanceBonus = 0.16f;
        [SerializeField, Min(0.05f)] private float riskRoomBonusStamina = 0.65f;
        [SerializeField, Min(0.2f)] private float riskRoomBonusPulseRadius = 1.25f;
        [SerializeField, Min(0.1f)] private float riskRoomBonusPulseDuration = 0.42f;
        [SerializeField] private Color riskRoomBonusPulseColor = new(1f, 0.86f, 0.24f, 0.9f);
        [SerializeField] private int riskRoomBonusPulseSortingOrder = 39;

        [Header("Cue Authoring")]
        [SerializeField] private bool buildVariantVisual = true;
        [SerializeField] private bool createVisualRendererIfMissing = true;
        [SerializeField, Range(0.2f, 0.9f)] private float glyphScaleRatio = 0.58f;
        [SerializeField, Min(0f)] private float glyphSpinSpeed = 32f;
        [SerializeField, Min(0f)] private float accentOrbitRadius = 0.2f;
        [SerializeField, Min(0f)] private float accentOrbitSpeed = 2.6f;

        private bool playerInside;
        private float nextEmitTime;
        private float flashUntil;
        private readonly float flashDuration = 0.14f;
        private int triggerCount;
        private SpriteRenderer spriteRenderer;
        private SpriteRenderer glyphRenderer;
        private SpriteRenderer accentRenderer;
        private Transform glyphTransform;
        private Transform accentTransform;
        private CircleCollider2D triggerCollider;
        private int configuredStage = 1;
        private float configuredStagePressure01;
        private float stageReadability01;
        private bool riskRoomBonusAttempted;
        private PlayerDummyController currentPlayer;

        private static Sprite auraSprite;
        private static Sprite accentSprite;
        private static readonly Dictionary<RoomArchetypeHookVariant, Sprite> variantSpriteCache = new();

        public MapCellKind SourceCellKind => sourceCellKind;
        public RoomArchetypeHookVariant Variant => variant;
        public int TriggerCount => triggerCount;
        public bool IsPlayerInside => playerInside;
        public float CooldownRemaining => Mathf.Max(0f, nextEmitTime - Time.time);
        public bool IsPreEmitWarning => EvaluateTelegraphIntensity() > 0f;
        public int ConfiguredStage => configuredStage;
        public float StageReadability01 => stageReadability01;
        public float EffectiveTelegraphLeadTime => Mathf.Max(0.02f, preEmitLeadTime * EvaluateStageLeadMultiplier());
        public float EffectiveWarningPulseSpeed => Mathf.Max(0.1f, warningPulseSpeed * EvaluateStagePulseMultiplier());

        public void Configure(
            MapCellKind cellKind,
            RoomArchetypeHookVariant hookVariant,
            float configuredTriggerRadius,
            float configuredLoudness,
            float configuredRadius,
            float configuredCooldown,
            bool enableCollider,
            int randomSeed,
            int stageIndex,
            float stagePressure01)
        {
            sourceCellKind = cellKind;
            variant = hookVariant;
            triggerRadius = Mathf.Max(0.1f, configuredTriggerRadius);
            noiseLoudness = Mathf.Max(0.1f, configuredLoudness);
            noiseRadius = Mathf.Max(0.5f, configuredRadius);
            cooldownSeconds = Mathf.Max(0.05f, configuredCooldown);
            ensureTriggerCollider = enableCollider;
            configuredStage = Mathf.Max(1, stageIndex);
            configuredStagePressure01 = Mathf.Clamp01(stagePressure01);
            stageReadability01 = EvaluateStageReadability01(configuredStage, configuredStagePressure01);

            unchecked
            {
                int safeSeed = randomSeed == int.MinValue ? 7919 : Mathf.Abs(randomSeed);
                Random.State backup = Random.state;
                Random.InitState(safeSeed);

                float hueShift = Random.Range(-0.04f, 0.06f);
                Color.RGBToHSV(idleColor, out float h, out float s, out float v);
                h = Mathf.Repeat(h + hueShift, 1f);
                s = Mathf.Clamp01(s * Random.Range(0.9f, 1.15f));
                v = Mathf.Clamp01(v * Random.Range(0.9f, 1.18f));
                idleColor = Color.HSVToRGB(h, s, v);
                idleColor.a = 0.84f;

                Color.RGBToHSV(activeColor, out h, out s, out v);
                h = Mathf.Repeat(h + hueShift * 0.7f, 1f);
                s = Mathf.Clamp01(s * Random.Range(0.94f, 1.2f));
                v = Mathf.Clamp01(v * Random.Range(0.95f, 1.2f));
                activeColor = Color.HSVToRGB(h, s, v);
                activeColor.a = 0.98f;

                Color.RGBToHSV(warningColor, out h, out s, out v);
                h = Mathf.Repeat(h + hueShift * 0.48f, 1f);
                s = Mathf.Clamp01(s * Random.Range(0.95f, 1.18f));
                v = Mathf.Clamp01(v * Random.Range(0.9f, 1.24f));
                warningColor = Color.HSVToRGB(h, s, v);
                warningColor.a = 0.95f;

                Random.state = backup;
            }

            EnsureRuntimeComponents();
        }

        private void Awake()
        {
            EnsureRuntimeComponents();
        }

        private void OnEnable()
        {
            stageReadability01 = EvaluateStageReadability01(configuredStage, configuredStagePressure01);
            playerInside = false;
            currentPlayer = null;
            riskRoomBonusAttempted = false;
            nextEmitTime = Time.time + Mathf.Min(0.9f, cooldownSeconds * 0.35f);
        }

        private void Update()
        {
            TickVisual();

            if (!emitWhilePlayerInside || !playerInside)
            {
                return;
            }

            if (Time.time >= nextEmitTime)
            {
                TryEmitNoise();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerDummyController player = GetPlayerController(other);
            if (player == null && !IsPlayerCollider(other))
            {
                return;
            }

            playerInside = true;
            currentPlayer = player;
            if (emitOnPlayerEnter)
            {
                TryEmitNoise();
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            PlayerDummyController player = GetPlayerController(other);
            if (player == null && !IsPlayerCollider(other))
            {
                return;
            }

            playerInside = false;
            if (currentPlayer == null || player == null || currentPlayer == player)
            {
                currentPlayer = null;
            }
        }

        private bool TryEmitNoise()
        {
            if (Time.time < nextEmitTime)
            {
                return false;
            }

            if (NoiseManager.Instance == null)
            {
                nextEmitTime = Time.time + cooldownSeconds;
                return false;
            }

            NoiseManager.Instance.EmitNoise(transform.position, noiseLoudness, noiseRadius, noiseKind, gameObject);
            triggerCount++;
            nextEmitTime = Time.time + cooldownSeconds;
            flashUntil = Time.time + flashDuration;
            TryGrantRiskRoomBonus(currentPlayer);
            return true;
        }

        private void TryGrantRiskRoomBonus(PlayerDummyController player)
        {
            if (!grantRiskRoomAdrenalineBonus || riskRoomBonusAttempted || sourceCellKind != MapCellKind.Risk || player == null)
            {
                return;
            }

            riskRoomBonusAttempted = true;
            float effectiveChance = Mathf.Clamp01(riskRoomBonusChance + configuredStagePressure01 * riskRoomBonusPressureChanceBonus);
            if (Random.value > effectiveChance)
            {
                return;
            }

            float recovered = player.RecoverStamina(riskRoomBonusStamina);
            if (recovered <= 0f)
            {
                return;
            }

            SpawnRiskRoomBonusPulse();
        }

        private void SpawnRiskRoomBonusPulse()
        {
            Transform vfxRoot = EnsureScenePath("Scene_Root/GameRoot/Runtime/VFX/RiskRoomBonus");
            GameObject visualObject = new("RiskRoomBonusPulse");
            if (vfxRoot != null)
            {
                visualObject.transform.SetParent(vfxRoot, false);
            }

            visualObject.transform.position = new Vector3(transform.position.x, transform.position.y, 0f);
            EchoPulseVisualDummy visual = visualObject.AddComponent<EchoPulseVisualDummy>();
            visual.Configure(
                Mathf.Max(0.2f, riskRoomBonusPulseRadius),
                riskRoomBonusPulseColor,
                Mathf.Max(0.1f, riskRoomBonusPulseDuration),
                1,
                0f,
                riskRoomBonusPulseSortingOrder);
        }

        private void TickVisual()
        {
            float breathe = 0.5f + Mathf.Sin((Time.time + transform.position.sqrMagnitude * 0.05f) * breatheSpeed) * 0.5f;
            float telegraphIntensity = EvaluateTelegraphIntensity();
            float warningPulseSpeedScaled = Mathf.Max(0.1f, warningPulseSpeed * EvaluateStagePulseMultiplier());
            float warningPulse = 0.5f + Mathf.Sin((Time.time + transform.position.sqrMagnitude * 0.09f) * warningPulseSpeedScaled) * 0.5f;
            float warningBlend = telegraphIntensity * Mathf.Lerp(0.62f, 1f, warningPulse);
            warningBlend = Mathf.Clamp01(warningBlend * EvaluateStageTelegraphScaleMultiplier());
            float stageScaleBoost = telegraphScaleBoost * Mathf.Lerp(0.72f, 1.28f, stageReadability01);
            float scale = Mathf.Lerp(0.92f, 1.06f, breathe) * Mathf.Lerp(1f, 1f + stageScaleBoost, warningBlend);
            transform.localScale = Vector3.one * Mathf.Max(0.1f, triggerRadius * 2f * scale);

            Color currentColor = idleColor;
            if (Time.time < flashUntil)
            {
                float t = 1f - Mathf.Clamp01((flashUntil - Time.time) / Mathf.Max(0.02f, flashDuration));
                currentColor = Color.Lerp(activeColor, idleColor, t);
            }
            else if (warningBlend > 0.001f)
            {
                Color stageWarningColor = warningColor;
                stageWarningColor.a = Mathf.Clamp01(stageWarningColor.a * EvaluateStageWarningAlphaMultiplier());
                currentColor = Color.Lerp(idleColor, stageWarningColor, warningBlend);
            }

            if (spriteRenderer != null)
            {
                Color auraColor = currentColor;
                auraColor.a = Mathf.Clamp01(Mathf.Lerp(0.34f, currentColor.a, 0.6f));
                spriteRenderer.color = auraColor;
            }

            if (!buildVariantVisual || glyphTransform == null || glyphRenderer == null)
            {
                return;
            }

            float glyphScale = Mathf.Lerp(0.92f, 1.1f, breathe) * Mathf.Lerp(1f, 1.14f, warningBlend);
            glyphTransform.localScale = Vector3.one * Mathf.Max(0.1f, glyphScaleRatio * glyphScale);
            glyphTransform.localRotation = Quaternion.Euler(0f, 0f, EvaluateGlyphRotation(warningBlend));

            Color glyphColor = Color.Lerp(currentColor, activeColor, Mathf.Lerp(0.18f, 0.34f, warningBlend));
            glyphColor.a = 0.98f;
            glyphRenderer.color = glyphColor;

            if (accentTransform == null || accentRenderer == null)
            {
                return;
            }

            float orbitPhase = (Time.time * Mathf.Max(0f, accentOrbitSpeed) + transform.position.x * 0.17f + transform.position.y * 0.23f) * Mathf.PI * 2f;
            accentTransform.localPosition = new Vector3(Mathf.Cos(orbitPhase), Mathf.Sin(orbitPhase), 0f) * Mathf.Max(0f, accentOrbitRadius);
            accentTransform.localScale = Vector3.one * Mathf.Lerp(0.14f, 0.24f, warningBlend);

            Color accentColor = Color.Lerp(idleColor, warningColor, Mathf.Lerp(0.3f, 1f, warningBlend));
            if (Time.time < flashUntil)
            {
                accentColor = Color.Lerp(accentColor, activeColor, 0.58f);
            }

            accentColor.a = Mathf.Clamp01(0.72f + warningBlend * 0.28f);
            accentRenderer.color = accentColor;
            accentRenderer.enabled = playerInside || warningBlend > 0.01f || Time.time < flashUntil;
        }

        private float EvaluateGlyphRotation(float warningBlend)
        {
            float t = Time.time;
            float seed = transform.position.x * 0.77f + transform.position.y * 0.38f;
            return variant switch
            {
                RoomArchetypeHookVariant.LooseMetal => Mathf.Sin(t * 2.2f + seed) * 8f,
                RoomArchetypeHookVariant.HangingChain => Mathf.Sin(t * 3.4f + seed) * 13f,
                RoomArchetypeHookVariant.CrackedGlass => Mathf.Repeat(t * glyphSpinSpeed * Mathf.Lerp(0.17f, 0.48f, warningBlend), 360f),
                RoomArchetypeHookVariant.RustedVent => Mathf.Sin(t * 1.7f + seed) * 4f,
                RoomArchetypeHookVariant.ClothRustle => Mathf.Sin(t * 4.6f + seed) * 18f,
                RoomArchetypeHookVariant.AlarmDebris => Mathf.Sin(t * 5.4f + seed) * Mathf.Lerp(7f, 20f, warningBlend),
                _ => 0f
            };
        }

        private float EvaluateTelegraphIntensity()
        {
            if (!usePreEmitTelegraph || !playerInside || !emitWhilePlayerInside)
            {
                return 0f;
            }

            float leadTime = Mathf.Max(0.02f, preEmitLeadTime * EvaluateStageLeadMultiplier());
            float remaining = Mathf.Max(0f, nextEmitTime - Time.time);
            if (remaining <= 0f || remaining > leadTime)
            {
                return 0f;
            }

            float normalized = 1f - Mathf.Clamp01(remaining / leadTime);
            return Mathf.SmoothStep(0f, 1f, normalized);
        }

        private float EvaluateStageReadability01(int stageIndex, float stagePressure)
        {
            if (!scaleTelegraphByStage)
            {
                return Mathf.Clamp01(stagePressure);
            }

            int startStage = Mathf.Max(1, telegraphRampStartStage);
            int peakStage = Mathf.Max(startStage + 1, telegraphRampPeakStage);
            float stage01 = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(startStage, peakStage, Mathf.Max(1, stageIndex)));
            return Mathf.Clamp01(stage01 * 0.72f + Mathf.Clamp01(stagePressure) * 0.28f);
        }

        private float EvaluateStageLeadMultiplier()
        {
            if (!scaleTelegraphByStage)
            {
                return 1f;
            }

            return Mathf.Lerp(lowStageTelegraphLeadMultiplier, highStageTelegraphLeadMultiplier, stageReadability01);
        }

        private float EvaluateStagePulseMultiplier()
        {
            if (!scaleTelegraphByStage)
            {
                return 1f;
            }

            return Mathf.Lerp(lowStageWarningPulseSpeedMultiplier, highStageWarningPulseSpeedMultiplier, stageReadability01);
        }

        private float EvaluateStageTelegraphScaleMultiplier()
        {
            if (!scaleTelegraphByStage)
            {
                return 1f;
            }

            return Mathf.Lerp(lowStageTelegraphScaleMultiplier, highStageTelegraphScaleMultiplier, stageReadability01);
        }

        private float EvaluateStageWarningAlphaMultiplier()
        {
            if (!scaleTelegraphByStage)
            {
                return 1f;
            }

            return Mathf.Lerp(lowStageWarningAlphaMultiplier, highStageWarningAlphaMultiplier, stageReadability01);
        }

        private void EnsureRuntimeComponents()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null && createVisualRendererIfMissing)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = GetAuraSprite();
                spriteRenderer.color = idleColor;
            }

            if (buildVariantVisual)
            {
                EnsureVariantVisualChildren();
            }

            triggerCollider = GetComponent<CircleCollider2D>();
            if (ensureTriggerCollider)
            {
                if (triggerCollider == null)
                {
                    triggerCollider = gameObject.AddComponent<CircleCollider2D>();
                }

                triggerCollider.isTrigger = true;
                triggerCollider.radius = 0.5f;
            }
        }

        private void EnsureVariantVisualChildren()
        {
            glyphTransform = transform.Find("HookCue_Glyph");
            if (glyphTransform == null)
            {
                GameObject glyphObject = new("HookCue_Glyph");
                glyphObject.transform.SetParent(transform, false);
                glyphTransform = glyphObject.transform;
            }

            glyphRenderer = glyphTransform.GetComponent<SpriteRenderer>();
            if (glyphRenderer == null)
            {
                glyphRenderer = glyphTransform.gameObject.AddComponent<SpriteRenderer>();
            }

            glyphRenderer.sprite = GetVariantSprite(variant);
            glyphRenderer.sortingOrder = (spriteRenderer != null ? spriteRenderer.sortingOrder : 22) + 1;
            glyphRenderer.color = idleColor;

            accentTransform = transform.Find("HookCue_Accent");
            if (accentTransform == null)
            {
                GameObject accentObject = new("HookCue_Accent");
                accentObject.transform.SetParent(transform, false);
                accentTransform = accentObject.transform;
            }

            accentRenderer = accentTransform.GetComponent<SpriteRenderer>();
            if (accentRenderer == null)
            {
                accentRenderer = accentTransform.gameObject.AddComponent<SpriteRenderer>();
            }

            accentRenderer.sprite = GetAccentSprite();
            accentRenderer.sortingOrder = glyphRenderer.sortingOrder + 1;
            accentRenderer.color = warningColor;
        }

        private static bool IsPlayerCollider(Collider2D collider)
        {
            if (collider == null)
            {
                return false;
            }

            return GetPlayerController(collider) != null || collider.CompareTag("Player");
        }

        private static PlayerDummyController GetPlayerController(Collider2D collider)
        {
            if (collider == null)
            {
                return null;
            }

            PlayerDummyController player = collider.GetComponent<PlayerDummyController>();
            if (player != null)
            {
                return player;
            }

            return collider.GetComponentInParent<PlayerDummyController>();
        }

        private static Transform EnsureScenePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            string[] parts = path.Split('/');
            GameObject root = GameObject.Find(parts[0]);
            if (root == null)
            {
                root = new GameObject(parts[0]);
            }

            Transform current = root.transform;
            for (int i = 1; i < parts.Length; i++)
            {
                Transform child = current.Find(parts[i]);
                if (child == null)
                {
                    GameObject childObject = new(parts[i]);
                    childObject.transform.SetParent(current, false);
                    child = childObject.transform;
                }

                current = child;
            }

            return current;
        }

        private static Sprite GetAuraSprite()
        {
            if (auraSprite != null)
            {
                return auraSprite;
            }

            const int size = 96;
            float[] alpha = new float[size * size];
            Vector2 center = new(size * 0.5f, size * 0.5f);
            StampDisc(alpha, size, center, 34f, 14f, 0.92f);
            StampRing(alpha, size, center, 34f, 2.8f, 4.2f, 0.78f);
            auraSprite = CreateSpriteFromAlpha(alpha, size, "HookCueAura");
            return auraSprite;
        }

        private static Sprite GetAccentSprite()
        {
            if (accentSprite != null)
            {
                return accentSprite;
            }

            const int size = 48;
            float[] alpha = new float[size * size];
            Vector2 center = new(size * 0.5f, size * 0.5f);
            StampDisc(alpha, size, center, 10f, 6f, 1f);
            accentSprite = CreateSpriteFromAlpha(alpha, size, "HookCueAccent");
            return accentSprite;
        }

        private static Sprite GetVariantSprite(RoomArchetypeHookVariant hookVariant)
        {
            if (variantSpriteCache.TryGetValue(hookVariant, out Sprite cached) && cached != null)
            {
                return cached;
            }

            Sprite sprite = CreateVariantSprite(hookVariant);
            variantSpriteCache[hookVariant] = sprite;
            return sprite;
        }

        private static Sprite CreateVariantSprite(RoomArchetypeHookVariant hookVariant)
        {
            const int size = 96;
            float[] alpha = new float[size * size];
            Vector2 center = new(size * 0.5f, size * 0.5f);

            switch (hookVariant)
            {
                case RoomArchetypeHookVariant.LooseMetal:
                {
                    Vector2[] plate =
                    {
                        new(22f, 35f),
                        new(66f, 28f),
                        new(74f, 57f),
                        new(30f, 68f),
                        new(22f, 35f)
                    };
                    StampPolyline(alpha, size, plate, 3.8f, 1.4f, 1f);
                    StampDisc(alpha, size, new Vector2(34f, 45f), 3.4f, 1.8f, 0.86f);
                    StampDisc(alpha, size, new Vector2(58f, 56f), 3.4f, 1.8f, 0.86f);
                    break;
                }
                case RoomArchetypeHookVariant.HangingChain:
                {
                    for (int i = 0; i < 3; i++)
                    {
                        float y = 26f + i * 22f;
                        Vector2 ringCenter = new(center.x, y);
                        StampRing(alpha, size, ringCenter, 9f, 2.8f, 1.6f, 1f);
                        if (i < 2)
                        {
                            StampSegment(alpha, size, ringCenter + Vector2.up * 8f, ringCenter + Vector2.up * 14f, 2.2f, 1.3f, 0.96f);
                        }
                    }

                    break;
                }
                case RoomArchetypeHookVariant.CrackedGlass:
                {
                    float[] angles = { 12f, 61f, 125f, 203f, 276f, 334f };
                    for (int i = 0; i < angles.Length; i++)
                    {
                        Vector2 dir = new(Mathf.Cos(angles[i] * Mathf.Deg2Rad), Mathf.Sin(angles[i] * Mathf.Deg2Rad));
                        Vector2 tip = center + dir * 33f;
                        StampSegment(alpha, size, center, tip, 1.8f, 1.05f, 1f);

                        Vector2 branchStart = center + dir * 17f;
                        Vector2 branchDir = new(Mathf.Cos((angles[i] + 38f) * Mathf.Deg2Rad), Mathf.Sin((angles[i] + 38f) * Mathf.Deg2Rad));
                        StampSegment(alpha, size, branchStart, branchStart + branchDir * 9f, 1.3f, 1f, 0.86f);
                    }

                    StampRing(alpha, size, center, 8f, 1.8f, 1.2f, 0.72f);
                    break;
                }
                case RoomArchetypeHookVariant.RustedVent:
                {
                    Vector2 p1 = new(24f, 28f);
                    Vector2 p2 = new(72f, 28f);
                    Vector2 p3 = new(72f, 68f);
                    Vector2 p4 = new(24f, 68f);
                    StampSegment(alpha, size, p1, p2, 2.6f, 1.4f, 1f);
                    StampSegment(alpha, size, p2, p3, 2.6f, 1.4f, 1f);
                    StampSegment(alpha, size, p3, p4, 2.6f, 1.4f, 1f);
                    StampSegment(alpha, size, p4, p1, 2.6f, 1.4f, 1f);

                    for (int i = 0; i < 4; i++)
                    {
                        float y = 36f + i * 8f;
                        StampSegment(alpha, size, new Vector2(30f, y), new Vector2(66f, y), 2.1f, 1.2f, 0.95f);
                    }

                    break;
                }
                case RoomArchetypeHookVariant.ClothRustle:
                {
                    List<Vector2> waveA = new();
                    List<Vector2> waveB = new();
                    for (int i = 0; i <= 10; i++)
                    {
                        float t = i / 10f;
                        float x = Mathf.Lerp(18f, 78f, t);
                        float yA = 52f + Mathf.Sin(t * Mathf.PI * 2f) * 9f;
                        float yB = 42f + Mathf.Sin((t * Mathf.PI * 2f) + 0.7f) * 7f;
                        waveA.Add(new Vector2(x, yA));
                        waveB.Add(new Vector2(x, yB));
                    }

                    StampPolyline(alpha, size, waveA, 2.6f, 1.2f, 1f);
                    StampPolyline(alpha, size, waveB, 2.3f, 1.1f, 0.86f);
                    StampSegment(alpha, size, new Vector2(22f, 58f), new Vector2(22f, 36f), 1.9f, 1.1f, 0.74f);
                    break;
                }
                case RoomArchetypeHookVariant.AlarmDebris:
                {
                    Vector2 a = new(48f, 74f);
                    Vector2 b = new(22f, 28f);
                    Vector2 c = new(74f, 28f);
                    StampSegment(alpha, size, a, b, 3f, 1.5f, 1f);
                    StampSegment(alpha, size, b, c, 3f, 1.5f, 1f);
                    StampSegment(alpha, size, c, a, 3f, 1.5f, 1f);
                    StampSegment(alpha, size, new Vector2(48f, 59f), new Vector2(48f, 42f), 3.4f, 1.5f, 0.98f);
                    StampDisc(alpha, size, new Vector2(48f, 34f), 3.2f, 1.6f, 0.98f);
                    StampSegment(alpha, size, new Vector2(30f, 20f), new Vector2(24f, 14f), 1.8f, 1f, 0.8f);
                    StampSegment(alpha, size, new Vector2(66f, 20f), new Vector2(72f, 14f), 1.8f, 1f, 0.8f);
                    break;
                }
            }

            return CreateSpriteFromAlpha(alpha, size, $"HookCue_{hookVariant}");
        }

        private static Sprite CreateSpriteFromAlpha(float[] alphaMap, int size, string name)
        {
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                name = name + "_Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color32[] pixels = new Color32[size * size];
            for (int i = 0; i < alphaMap.Length; i++)
            {
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(alphaMap[i]) * 255f);
                pixels[i] = new Color32(255, 255, 255, alpha);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                size);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static void StampDisc(float[] alphaMap, int size, Vector2 center, float radius, float feather, float opacity)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(center.x - radius - feather - 1f));
            int maxX = Mathf.Min(size - 1, Mathf.CeilToInt(center.x + radius + feather + 1f));
            int minY = Mathf.Max(0, Mathf.FloorToInt(center.y - radius - feather - 1f));
            int maxY = Mathf.Min(size - 1, Mathf.CeilToInt(center.y + radius + feather + 1f));

            float safeFeather = Mathf.Max(0.01f, feather);
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2 p = new(x + 0.5f, y + 0.5f);
                    float distance = Vector2.Distance(p, center);
                    if (distance > radius + safeFeather)
                    {
                        continue;
                    }

                    float alpha = 1f - Mathf.InverseLerp(radius, radius + safeFeather, distance);
                    PlotAlpha(alphaMap, size, x, y, alpha * opacity);
                }
            }
        }

        private static void StampRing(float[] alphaMap, int size, Vector2 center, float ringRadius, float halfThickness, float feather, float opacity)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(center.x - ringRadius - halfThickness - feather - 1f));
            int maxX = Mathf.Min(size - 1, Mathf.CeilToInt(center.x + ringRadius + halfThickness + feather + 1f));
            int minY = Mathf.Max(0, Mathf.FloorToInt(center.y - ringRadius - halfThickness - feather - 1f));
            int maxY = Mathf.Min(size - 1, Mathf.CeilToInt(center.y + ringRadius + halfThickness + feather + 1f));

            float safeFeather = Mathf.Max(0.01f, feather);
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2 p = new(x + 0.5f, y + 0.5f);
                    float distance = Vector2.Distance(p, center);
                    float edgeDistance = Mathf.Abs(distance - ringRadius);
                    if (edgeDistance > halfThickness + safeFeather)
                    {
                        continue;
                    }

                    float alpha = 1f - Mathf.InverseLerp(halfThickness, halfThickness + safeFeather, edgeDistance);
                    PlotAlpha(alphaMap, size, x, y, alpha * opacity);
                }
            }
        }

        private static void StampSegment(float[] alphaMap, int size, Vector2 from, Vector2 to, float halfThickness, float feather, float opacity)
        {
            float minXf = Mathf.Min(from.x, to.x) - halfThickness - feather - 1f;
            float maxXf = Mathf.Max(from.x, to.x) + halfThickness + feather + 1f;
            float minYf = Mathf.Min(from.y, to.y) - halfThickness - feather - 1f;
            float maxYf = Mathf.Max(from.y, to.y) + halfThickness + feather + 1f;

            int minX = Mathf.Max(0, Mathf.FloorToInt(minXf));
            int maxX = Mathf.Min(size - 1, Mathf.CeilToInt(maxXf));
            int minY = Mathf.Max(0, Mathf.FloorToInt(minYf));
            int maxY = Mathf.Min(size - 1, Mathf.CeilToInt(maxYf));
            float safeFeather = Mathf.Max(0.01f, feather);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2 p = new(x + 0.5f, y + 0.5f);
                    float distance = DistanceToSegment(p, from, to);
                    if (distance > halfThickness + safeFeather)
                    {
                        continue;
                    }

                    float alpha = 1f - Mathf.InverseLerp(halfThickness, halfThickness + safeFeather, distance);
                    PlotAlpha(alphaMap, size, x, y, alpha * opacity);
                }
            }
        }

        private static void StampPolyline(float[] alphaMap, int size, IReadOnlyList<Vector2> points, float halfThickness, float feather, float opacity)
        {
            if (points == null || points.Count < 2)
            {
                return;
            }

            for (int i = 0; i < points.Count - 1; i++)
            {
                StampSegment(alphaMap, size, points[i], points[i + 1], halfThickness, feather, opacity);
            }
        }

        private static float DistanceToSegment(Vector2 point, Vector2 from, Vector2 to)
        {
            Vector2 segment = to - from;
            float lengthSqr = segment.sqrMagnitude;
            if (lengthSqr <= Mathf.Epsilon)
            {
                return Vector2.Distance(point, from);
            }

            float t = Mathf.Clamp01(Vector2.Dot(point - from, segment) / lengthSqr);
            Vector2 projection = from + segment * t;
            return Vector2.Distance(point, projection);
        }

        private static void PlotAlpha(float[] alphaMap, int size, int x, int y, float alpha)
        {
            int index = y * size + x;
            if (index < 0 || index >= alphaMap.Length)
            {
                return;
            }

            float clamped = Mathf.Clamp01(alpha);
            if (clamped <= alphaMap[index])
            {
                return;
            }

            alphaMap[index] = clamped;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(idleColor.r, idleColor.g, idleColor.b, 0.8f);
            Gizmos.DrawWireSphere(transform.position, triggerRadius);

            Gizmos.color = new Color(activeColor.r, activeColor.g, activeColor.b, 0.36f);
            Gizmos.DrawWireSphere(transform.position, noiseRadius);
        }
    }
}

