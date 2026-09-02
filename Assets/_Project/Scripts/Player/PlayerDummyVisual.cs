using System;
using System.Collections.Generic;
using LostBreadcrumbs.Runtime.Map;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LostBreadcrumbs.Runtime.Player
{
    public sealed class PlayerDummyVisual : MonoBehaviour
    {
        [Header("Visual Mode")]
        [SerializeField] private bool preferUndeadSurvivorArt = true;
        [SerializeField] private string undeadFarmerSpriteSheetPath = "Assets/Undead Survivor/Sprites/Farmer 0.png";

        [Header("Undead Survivor Animation")]
        [SerializeField, Min(1f)] private float animationFps = 10f;
        [SerializeField, Min(0f)] private float movingThreshold = 0.03f;
        [SerializeField, Min(0.1f)] private float undeadAvatarScale = 1f;
        [SerializeField] private bool flipByMovementX = true;
        [SerializeField] private Sprite[] runFrames;
        [SerializeField] private Sprite[] standFrames;
        [SerializeField] private Sprite[] deadFrames;

        [Header("Debug Fallback")]
        [SerializeField] private Color bodyColor = new(0.3f, 0.95f, 1f, 1f);
        [SerializeField] private Color arrowColor = new(1f, 0.95f, 0.3f, 1f);
        [SerializeField, Min(0.2f)] private float avatarScale = 0.65f;
        [SerializeField] private bool addCollision = true;

        // Painted sibling body when undead frames are unavailable; collider radius stays 0.35.
        private const float PlayerBodyArtScale = 0.85f;

        private static Sprite cachedSprite;

        private Transform avatar;
        private SpriteRenderer bodyRenderer;
        private Transform facingArrow;
        private PlayerDummyController movementSource;
        private bool usingUndeadVisual;
        private Vector3 lastPosition;
        private float animationTimer;
        private int frameIndex;

        private void Awake()
        {
            movementSource = GetComponent<PlayerDummyController>();
            flipByMovementX = true;
            avatar = EnsureChild(transform, "DummyAvatar");
            avatar.localPosition = Vector3.zero;
            avatar.localRotation = Quaternion.identity;

            bodyRenderer = GetOrAdd<SpriteRenderer>(avatar.gameObject);
            bodyRenderer.sortingOrder = 20;

            usingUndeadVisual = preferUndeadSurvivorArt && TryPrepareUndeadVisualFrames();
            if (usingUndeadVisual)
            {
                flipByMovementX = true;
                avatar.localScale = Vector3.one * undeadAvatarScale;
                bodyRenderer.color = Color.white;
                bodyRenderer.sprite = EvaluateInitialFrame();
                DestroyChildIfExists(avatar, "FacingArrow");
            }
            else if (!TrySetupPaintedBodyVisual())
            {
                SetupDebugFallbackVisual();
            }

            if (addCollision)
            {
                CircleCollider2D collider = GetOrAdd<CircleCollider2D>(gameObject);
                collider.radius = 0.35f;
            }

            Rigidbody2D rb = GetOrAdd<Rigidbody2D>(gameObject);
            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            lastPosition = transform.position;
        }

        private void Update()
        {
            if (Time.timeScale <= 0.0001f)
            {
                return;
            }

            ApplyFacing();
            if (facingArrow != null)
            {
                float sign = movementSource != null ? movementSource.FacingSignX : 1f;
                facingArrow.localPosition = new Vector3(0.8f * sign, 0f, 0f);
            }

            if (!usingUndeadVisual || bodyRenderer == null)
            {
                return;
            }

            Vector3 currentPosition = transform.position;
            bool isMoving = movementSource != null
                ? movementSource.MoveInput.sqrMagnitude > 0.01f
                : Vector3.Distance(currentPosition, lastPosition) > movingThreshold;
            lastPosition = currentPosition;

            Sprite[] activeFrames = isMoving && runFrames != null && runFrames.Length > 0
                ? runFrames
                : standFrames;

            if (activeFrames == null || activeFrames.Length == 0)
            {
                activeFrames = runFrames;
            }

            if (activeFrames == null || activeFrames.Length == 0)
            {
                return;
            }

            animationTimer += Time.deltaTime * Mathf.Max(1f, animationFps);
            if (animationTimer >= 1f)
            {
                int advance = Mathf.FloorToInt(animationTimer);
                animationTimer -= advance;
                frameIndex = (frameIndex + advance) % activeFrames.Length;
            }

            bodyRenderer.sprite = activeFrames[frameIndex % activeFrames.Length];
        }

        private void ApplyFacing()
        {
            if (!flipByMovementX || bodyRenderer == null)
            {
                return;
            }

            if (movementSource != null)
            {
                bodyRenderer.flipX = movementSource.FacingSignX < 0f;
                return;
            }

            float dx = transform.position.x - lastPosition.x;
            if (Mathf.Abs(dx) > 0.001f)
            {
                bodyRenderer.flipX = dx < 0f;
            }
        }

        private bool TrySetupPaintedBodyVisual()
        {
            Sprite bodyArt = MapReadableArt.TryGetPlayerBodySprite();
            if (bodyArt == null || bodyRenderer == null || avatar == null)
            {
                return false;
            }

            flipByMovementX = true;
            avatar.localScale = Vector3.one * PlayerBodyArtScale;
            bodyRenderer.sprite = bodyArt;
            bodyRenderer.color = Color.white;
            bodyRenderer.flipX = false;
            DestroyChildIfExists(avatar, "FacingArrow");
            facingArrow = null;
            return true;
        }

        private void SetupDebugFallbackVisual()
        {
            avatar.localScale = Vector3.one * avatarScale;

            bodyRenderer.sprite = GetDebugSprite();
            bodyRenderer.color = bodyColor;
            bodyRenderer.flipX = false;

            Transform arrow = EnsureChild(avatar, "FacingArrow");
            facingArrow = arrow;
            arrow.localPosition = new Vector3(0.8f, 0f, 0f);
            arrow.localRotation = Quaternion.identity;
            arrow.localScale = new Vector3(0.5f, 0.18f, 1f);

            SpriteRenderer arrowRenderer = GetOrAdd<SpriteRenderer>(arrow.gameObject);
            arrowRenderer.sprite = GetDebugSprite();
            arrowRenderer.color = arrowColor;
            arrowRenderer.sortingOrder = 21;
        }

        private Sprite EvaluateInitialFrame()
        {
            if (standFrames != null && standFrames.Length > 0)
            {
                return standFrames[0];
            }

            if (runFrames != null && runFrames.Length > 0)
            {
                return runFrames[0];
            }

            return null;
        }

        private bool TryPrepareUndeadVisualFrames()
        {
            bool hasExistingFrames = (runFrames != null && runFrames.Length > 0)
                                     || (standFrames != null && standFrames.Length > 0);
            if (hasExistingFrames)
            {
                return true;
            }

#if UNITY_EDITOR
            if (string.IsNullOrWhiteSpace(undeadFarmerSpriteSheetPath))
            {
                return false;
            }

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(undeadFarmerSpriteSheetPath);
            if (assets == null || assets.Length == 0)
            {
                return false;
            }

            List<Sprite> run = new();
            List<Sprite> stand = new();
            List<Sprite> dead = new();

            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is not Sprite sprite)
                {
                    continue;
                }

                string name = sprite.name ?? string.Empty;
                if (name.StartsWith("Run ", StringComparison.OrdinalIgnoreCase))
                {
                    run.Add(sprite);
                }
                else if (name.StartsWith("Stand ", StringComparison.OrdinalIgnoreCase))
                {
                    stand.Add(sprite);
                }
                else if (name.StartsWith("Dead ", StringComparison.OrdinalIgnoreCase))
                {
                    dead.Add(sprite);
                }
            }

            SortFrames(run);
            SortFrames(stand);
            SortFrames(dead);

            if (run.Count > 0)
            {
                runFrames = run.ToArray();
            }

            if (stand.Count > 0)
            {
                standFrames = stand.ToArray();
            }

            if (dead.Count > 0)
            {
                deadFrames = dead.ToArray();
            }

            if ((runFrames != null && runFrames.Length > 0) || (standFrames != null && standFrames.Length > 0))
            {
                EditorUtility.SetDirty(this);
            }
#endif

            return (runFrames != null && runFrames.Length > 0) || (standFrames != null && standFrames.Length > 0);
        }

        private static void SortFrames(List<Sprite> frames)
        {
            if (frames == null || frames.Count <= 1)
            {
                return;
            }

            frames.Sort((a, b) => ExtractTrailingNumber(a.name).CompareTo(ExtractTrailingNumber(b.name)));
        }

        private static int ExtractTrailingNumber(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0;
            }

            int space = value.LastIndexOf(' ');
            if (space < 0 || space + 1 >= value.Length)
            {
                return 0;
            }

            return int.TryParse(value.Substring(space + 1), out int result) ? result : 0;
        }

        private static void DestroyChildIfExists(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }

        private static Transform EnsureChild(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null)
            {
                return child;
            }

            GameObject childObject = new(childName);
            childObject.transform.SetParent(parent, false);
            return childObject.transform;
        }

        private static T GetOrAdd<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            if (component == null)
            {
                component = gameObject.AddComponent<T>();
            }

            return component;
        }

        private static Sprite GetDebugSprite()
        {
            if (cachedSprite != null)
            {
                return cachedSprite;
            }

            Texture2D texture = new(1, 1, TextureFormat.RGBA32, false)
            {
                name = "PlayerDummyVisualTexture",
                hideFlags = HideFlags.HideAndDontSave
            };

            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            cachedSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            cachedSprite.name = "PlayerDummyVisualSprite";
            cachedSprite.hideFlags = HideFlags.HideAndDontSave;
            return cachedSprite;
        }
    }
}
