using UnityEngine;

namespace LostBreadcrumbs.Runtime.Player
{
    public sealed class PlayerVisibilitySource : MonoBehaviour
    {
        [SerializeField] private bool flashlightEnabled;
        [SerializeField, Min(0.5f)] private float flashlightRange = 6f;
        [SerializeField, Range(10f, 180f)] private float flashlightAngle = 45f;
        [SerializeField] private Transform flashlightForward;

        private float runtimeFlashlightRangeMultiplier = 1f;
        private float runtimeFlashlightAngleMultiplier = 1f;

        public bool FlashlightEnabled => flashlightEnabled;
        public float FlashlightRange => flashlightRange * runtimeFlashlightRangeMultiplier;
        public float FlashlightAngle => Mathf.Clamp(flashlightAngle * runtimeFlashlightAngleMultiplier, 10f, 180f);
        public float RuntimeFlashlightRangeMultiplier => runtimeFlashlightRangeMultiplier;
        public float RuntimeFlashlightAngleMultiplier => runtimeFlashlightAngleMultiplier;

        public Vector2 CurrentForward
        {
            get
            {
                Vector2 forward = flashlightForward != null
                    ? (Vector2)flashlightForward.right
                    : (Vector2)transform.right;

                if (forward.sqrMagnitude < 0.001f)
                {
                    forward = Vector2.right;
                }

                return forward.normalized;
            }
        }

        public void ApplyRuntimeModifiers(float rangeMultiplier, float angleMultiplier)
        {
            runtimeFlashlightRangeMultiplier = Mathf.Clamp(rangeMultiplier, 0.35f, 2.6f);
            runtimeFlashlightAngleMultiplier = Mathf.Clamp(angleMultiplier, 0.5f, 2f);
        }

        public void ResetRuntimeModifiers()
        {
            ApplyRuntimeModifiers(1f, 1f);
        }

        public void ToggleFlashlight()
        {
            flashlightEnabled = !flashlightEnabled;
        }

        public void SetFlashlightEnabled(bool enabled)
        {
            flashlightEnabled = enabled;
        }

        public void ResetFlashlightState()
        {
            flashlightEnabled = false;
        }

        public bool IsPointInsideFlashlight(Vector2 point)
        {
            if (!flashlightEnabled)
            {
                return false;
            }

            Vector2 origin = transform.position;
            Vector2 toPoint = point - origin;
            if (toPoint.sqrMagnitude > FlashlightRange * FlashlightRange)
            {
                return false;
            }

            float angle = Vector2.Angle(CurrentForward, toPoint.normalized);
            return angle <= FlashlightAngle * 0.5f;
        }
    }
}
