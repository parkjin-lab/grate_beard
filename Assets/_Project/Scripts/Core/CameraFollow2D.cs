using LostBreadcrumbs.Runtime.Player;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Core
{
    public sealed class CameraFollow2D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new(0f, 0f, -10f);
        [SerializeField, Min(0f)] private float smoothSpeed = 8f;
        [SerializeField] private bool snapOnStart = true;
        [SerializeField, Min(0.1f)] private float missingTargetResolveInterval = 0.8f;

        [Header("Bounds")]
        [SerializeField] private bool clampToBounds = true;
        [SerializeField] private Vector2 boundsCenter = Vector2.zero;
        [SerializeField] private Vector2 boundsSize = new(40f, 30f);
        [SerializeField, Min(0f)] private float boundsPadding = 0.4f;

        [Header("Motion")]
        [SerializeField, Min(0f)] private float lookAheadDistance = 0.7f;
        [SerializeField, Min(0f)] private float lookAheadSmoothing = 7f;

        [Header("Runtime Tuning")]
        [SerializeField, Range(0.5f, 2f)] private float runtimeLookAheadMultiplier = 1f;
        [SerializeField, Range(0.5f, 2f)] private float runtimeSmoothMultiplier = 1f;
        [SerializeField, Range(0.5f, 2f)] private float runtimeLookAheadSmoothingMultiplier = 1f;

        [Header("Impulse")]
        [SerializeField, Min(0f)] private float maxImpulseAmplitude = 0.9f;

        private float impulseAmplitude;
        private float impulseDuration;
        private float impulseEndTime;

        private Camera cachedCamera;
        private Vector3 previousTargetPosition;
        private bool hasPreviousTargetPosition;
        private Vector3 lookAheadOffset;
        private float nextTargetResolveTime;

        public bool HasBounds => clampToBounds && boundsSize.x > 0.01f && boundsSize.y > 0.01f;
        public Vector2 BoundsCenter => boundsCenter;
        public Vector2 BoundsSize => boundsSize;
        public float BoundsPadding => boundsPadding;
        public float CurrentLookAheadMagnitude => new Vector2(lookAheadOffset.x, lookAheadOffset.y).magnitude;
        public float RuntimeLookAheadMultiplier => runtimeLookAheadMultiplier;
        public float RuntimeSmoothMultiplier => runtimeSmoothMultiplier;
        public float RuntimeLookAheadSmoothingMultiplier => runtimeLookAheadSmoothingMultiplier;

        private void Start()
        {
            cachedCamera = GetComponent<Camera>();
            TryFindPlayer(force: true);

            if (target != null)
            {
                previousTargetPosition = target.position;
                hasPreviousTargetPosition = true;
            }

            if (snapOnStart && target != null)
            {
                Vector3 snapped = target.position + offset;
                transform.position = ClampToBounds(snapped);
            }
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                TryFindPlayer();
                if (target == null)
                {
                    return;
                }
            }

            float dt = Mathf.Max(0.0001f, Time.deltaTime);
            if (!hasPreviousTargetPosition)
            {
                previousTargetPosition = target.position;
                hasPreviousTargetPosition = true;
            }

            Vector2 targetVelocity = ((Vector2)target.position - (Vector2)previousTargetPosition) / dt;
            previousTargetPosition = target.position;

            float effectiveLookAheadDistance = lookAheadDistance * Mathf.Clamp(runtimeLookAheadMultiplier, 0.5f, 2f);
            float effectiveLookAheadSmoothing = lookAheadSmoothing * Mathf.Clamp(runtimeLookAheadSmoothingMultiplier, 0.5f, 2f);
            float effectiveSmoothSpeed = smoothSpeed * Mathf.Clamp(runtimeSmoothMultiplier, 0.5f, 2f);

            Vector2 desiredLookAhead = targetVelocity.sqrMagnitude > 0.0001f
                ? targetVelocity.normalized * effectiveLookAheadDistance
                : Vector2.zero;

            float lookAheadLerp = effectiveLookAheadSmoothing <= 0f
                ? 1f
                : 1f - Mathf.Exp(-effectiveLookAheadSmoothing * dt);
            Vector3 desiredLookAheadOffset = new(desiredLookAhead.x, desiredLookAhead.y, 0f);
            lookAheadOffset = Vector3.Lerp(lookAheadOffset, desiredLookAheadOffset, lookAheadLerp);

            Vector3 desired = target.position + offset + lookAheadOffset;
            Vector3 basePosition;

            if (effectiveSmoothSpeed <= 0f)
            {
                basePosition = desired;
            }
            else
            {
                basePosition = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-effectiveSmoothSpeed * dt));
            }

            basePosition = ClampToBounds(basePosition);
            transform.position = basePosition + ComputeImpulseOffset();
        }

        public void SetTargetForEditor(Transform followTarget)
        {
            target = followTarget;
            if (target != null)
            {
                previousTargetPosition = target.position;
                hasPreviousTargetPosition = true;
            }
        }

        public void SetFollowBoundsForEditor(Vector2 center, Vector2 size, float padding = 0f)
        {
            clampToBounds = true;
            boundsCenter = center;
            boundsSize = new Vector2(Mathf.Max(2f, size.x), Mathf.Max(2f, size.y));
            boundsPadding = Mathf.Max(0f, padding);
        }

        public void ClearFollowBoundsForEditor()
        {
            clampToBounds = false;
        }

        public void AddImpulse(float amplitude, float duration = 0.16f)
        {
            if (amplitude <= 0f)
            {
                return;
            }

            impulseAmplitude = Mathf.Max(impulseAmplitude, Mathf.Min(maxImpulseAmplitude, amplitude));
            impulseDuration = Mathf.Max(0.05f, duration);
            impulseEndTime = Time.time + impulseDuration;
        }

        public void ApplyRuntimeTuningForEditor(float lookAheadMultiplier, float smoothMultiplier, float lookAheadSmoothingMultiplier)
        {
            runtimeLookAheadMultiplier = Mathf.Clamp(lookAheadMultiplier, 0.5f, 2f);
            runtimeSmoothMultiplier = Mathf.Clamp(smoothMultiplier, 0.5f, 2f);
            runtimeLookAheadSmoothingMultiplier = Mathf.Clamp(lookAheadSmoothingMultiplier, 0.5f, 2f);
        }

        public void ResetRuntimeTuningForEditor()
        {
            runtimeLookAheadMultiplier = 1f;
            runtimeSmoothMultiplier = 1f;
            runtimeLookAheadSmoothingMultiplier = 1f;
        }

        private Vector3 ClampToBounds(Vector3 position)
        {
            if (!HasBounds)
            {
                return position;
            }

            if (cachedCamera == null)
            {
                cachedCamera = GetComponent<Camera>();
            }

            if (cachedCamera == null || !cachedCamera.orthographic)
            {
                return position;
            }

            float halfHeight = cachedCamera.orthographicSize;
            float halfWidth = halfHeight * Mathf.Max(0.1f, cachedCamera.aspect);

            Vector2 halfBounds = boundsSize * 0.5f;
            float minX = boundsCenter.x - halfBounds.x + halfWidth + boundsPadding;
            float maxX = boundsCenter.x + halfBounds.x - halfWidth - boundsPadding;
            float minY = boundsCenter.y - halfBounds.y + halfHeight + boundsPadding;
            float maxY = boundsCenter.y + halfBounds.y - halfHeight - boundsPadding;

            if (minX > maxX)
            {
                float centerX = boundsCenter.x;
                minX = centerX;
                maxX = centerX;
            }

            if (minY > maxY)
            {
                float centerY = boundsCenter.y;
                minY = centerY;
                maxY = centerY;
            }

            position.x = Mathf.Clamp(position.x, minX, maxX);
            position.y = Mathf.Clamp(position.y, minY, maxY);
            return position;
        }

        private Vector3 ComputeImpulseOffset()
        {
            if (Time.time >= impulseEndTime || impulseAmplitude <= 0f)
            {
                impulseAmplitude = 0f;
                return Vector3.zero;
            }

            float remaining = Mathf.Clamp01((impulseEndTime - Time.time) / Mathf.Max(0.05f, impulseDuration));
            float dampedAmplitude = impulseAmplitude * remaining;
            Vector2 jitter = Random.insideUnitCircle * dampedAmplitude;
            return new Vector3(jitter.x, jitter.y, 0f);
        }

        private void TryFindPlayer(bool force = false)
        {
            if (!force)
            {
                if (Time.unscaledTime < nextTargetResolveTime)
                {
                    return;
                }

                nextTargetResolveTime = Time.unscaledTime + Mathf.Max(0.1f, missingTargetResolveInterval);
            }

            PlayerDummyController playerController = PlayerDummyController.ActiveInstance;
            if (playerController != null)
            {
                target = playerController.transform;
                previousTargetPosition = target.position;
                hasPreviousTargetPosition = true;
            }
        }
    }
}
