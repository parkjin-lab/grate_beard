using LostBreadcrumbs.Runtime.Player;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Map
{
    public sealed class SafeHavenZone : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float radius = 0.7f;

        private PlayerConcealmentState activePlayerConcealment;

        public void Configure(float targetRadius)
        {
            radius = Mathf.Max(0.1f, targetRadius);

            CircleCollider2D collider = GetComponent<CircleCollider2D>();
            if (collider != null)
            {
                collider.radius = radius;
                collider.isTrigger = true;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player"))
            {
                return;
            }

            PlayerConcealmentState concealment = ResolvePlayerConcealment(other);
            if (concealment == null)
            {
                return;
            }

            activePlayerConcealment = concealment;
            activePlayerConcealment.EnterSafeHaven();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player"))
            {
                return;
            }

            PlayerConcealmentState concealment = ResolvePlayerConcealment(other);
            if (concealment == null)
            {
                return;
            }

            concealment.ExitSafeHaven();
            if (activePlayerConcealment == concealment)
            {
                activePlayerConcealment = null;
            }
        }

        private static PlayerConcealmentState ResolvePlayerConcealment(Collider2D collider)
        {
            if (collider == null)
            {
                return null;
            }

            PlayerConcealmentState concealment = collider.GetComponent<PlayerConcealmentState>();
            if (concealment != null)
            {
                return concealment;
            }

            concealment = collider.GetComponentInParent<PlayerConcealmentState>();
            if (concealment != null)
            {
                return concealment;
            }

            GameObject playerObject = null;
            try
            {
                playerObject = GameObject.FindGameObjectWithTag("Player");
            }
            catch (UnityException)
            {
                // Tag setup may not be complete in edit-time context.
            }

            if (playerObject == null)
            {
                playerObject = collider.gameObject;
            }

            concealment = playerObject.GetComponent<PlayerConcealmentState>();
            if (concealment == null)
            {
                concealment = playerObject.AddComponent<PlayerConcealmentState>();
            }

            return concealment;
        }

        private void OnDisable()
        {
            if (activePlayerConcealment != null)
            {
                activePlayerConcealment.ExitSafeHaven();
                activePlayerConcealment = null;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.25f, 1f, 0.85f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
