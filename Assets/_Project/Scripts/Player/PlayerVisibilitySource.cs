using UnityEngine;

namespace LostBreadcrumbs.Runtime.Player
{
    public sealed class PlayerVisibilitySource : MonoBehaviour
    {
        [SerializeField] private bool flashlightEnabled;
        [SerializeField, Min(0.5f)] private float flashlightRange = 6f;
        [SerializeField, Range(10f, 180f)] private float flashlightAngle = 45f;
        [SerializeField] private Transform flashlightForward;

        private PlayerDummyController movementSource;
        private float runtimeFlashlightRangeMultiplier = 1f;
        private float runtimeFlashlightAngleMultiplier = 1f;
        private float runtimeDreadFlashlightRangeMultiplier = 1f;
        private float runtimeDreadFlashlightAngleMultiplier = 1f;

        public bool FlashlightEnabled => flashlightEnabled;
        public float FlashlightRange => flashlightRange * runtimeFlashlightRangeMultiplier * runtimeDreadFlashlightRangeMultiplier;
        public float FlashlightAngle => Mathf.Clamp(flashlightAngle * runtimeFlashlightAngleMultiplier * runtimeDreadFlashlightAngleMultiplier, 10f, 180f);
        public float RuntimeFlashlightRangeMultiplier => runtimeFlashlightRangeMultiplier;
        public float RuntimeFlashlightAngleMultiplier => runtimeFlashlightAngleMultiplier;
        public float RuntimeDreadFlashlightRangeMultiplier => runtimeDreadFlashlightRangeMultiplier;
        public float RuntimeDreadFlashlightAngleMultiplier => runtimeDreadFlashlightAngleMultiplier;

        public Vector2 CurrentForward
        {
            get
            {
                Vector2 forward;
                if (flashlightForward != null)
                {
                    forward = flashlightForward.right;
                }
                else
                {
                    if (movementSource == null)
                    {
                        movementSource = GetComponent<PlayerDummyController>();
                    }

                    forward = movementSource != null
                        ? movementSource.FacingDirection
                        : (Vector2)transform.right;
                }

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

        public void ApplyDreadRuntimeModifiersForEditor(float rangeMultiplier, float angleMultiplier)
        {
            runtimeDreadFlashlightRangeMultiplier = Mathf.Clamp(rangeMultiplier, 0.35f, 1.25f);
            runtimeDreadFlashlightAngleMultiplier = Mathf.Clamp(angleMultiplier, 0.5f, 1.25f);
        }

        public void ResetDreadRuntimeModifiersForEditor()
        {
            ApplyDreadRuntimeModifiersForEditor(1f, 1f);
        }

        public void ToggleFlashlight()
        {
            flashlightEnabled = !flashlightEnabled;
        }

        public void SetFlashlightEnabled(bool enabled)
        {
            flashlightEnabled = enabled;
        }

        public void ResetFlashlightState(bool clearDreadModifiers = false)
        {
            flashlightEnabled = false;

            if (clearDreadModifiers)
            {
                ResetDreadRuntimeModifiersForEditor();
            }
        }

        public void ResetForRespawn()
        {
            ResetFlashlightState(clearDreadModifiers: true);
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
