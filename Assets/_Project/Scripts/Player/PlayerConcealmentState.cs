using UnityEngine;

namespace LostBreadcrumbs.Runtime.Player
{
    public sealed class PlayerConcealmentState : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float graceAfterExitSeconds = 0.45f;
        [SerializeField, Range(0.1f, 1f)] private float insideNoiseMultiplier = 0.35f;
        [SerializeField, Range(0.1f, 1f)] private float graceNoiseMultiplier = 0.7f;

        private int safeHavenOverlapCount;
        private float concealedUntil;

        public bool IsInsideSafeHaven => safeHavenOverlapCount > 0;
        public bool IsInGraceConcealment => safeHavenOverlapCount <= 0 && Time.time < concealedUntil;
        public bool IsConcealedFromEnemies => safeHavenOverlapCount > 0 || Time.time < concealedUntil;
        public float CurrentNoiseMultiplier => IsInsideSafeHaven
            ? insideNoiseMultiplier
            : (IsInGraceConcealment ? graceNoiseMultiplier : 1f);

        public float ConcealedRemainingSeconds => safeHavenOverlapCount > 0
            ? float.PositiveInfinity
            : Mathf.Max(0f, concealedUntil - Time.time);

        public void EnterSafeHaven()
        {
            safeHavenOverlapCount++;
            if (safeHavenOverlapCount < 0)
            {
                safeHavenOverlapCount = 0;
            }

            concealedUntil = 0f;
        }

        public void ExitSafeHaven()
        {
            safeHavenOverlapCount = Mathf.Max(0, safeHavenOverlapCount - 1);
            if (safeHavenOverlapCount == 0)
            {
                concealedUntil = Time.time + graceAfterExitSeconds;
            }
        }

        public void ResetConcealment()
        {
            safeHavenOverlapCount = 0;
            concealedUntil = 0f;
        }
    }
}
