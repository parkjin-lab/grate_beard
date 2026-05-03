using LostBreadcrumbs.Runtime.Player;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Map
{
    public sealed class EnemyContactDamage : MonoBehaviour
    {
        [SerializeField, Min(1)] private int damagePerHit = 1;
        [SerializeField, Min(0.05f)] private float hitIntervalSeconds = 0.75f;

        private float nextHitTime;

        public void Configure(int damage, float intervalSeconds)
        {
            damagePerHit = Mathf.Max(1, damage);
            hitIntervalSeconds = Mathf.Max(0.05f, intervalSeconds);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryDamage(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryDamage(other);
        }

        private void TryDamage(Collider2D other)
        {
            if (Time.time < nextHitTime)
            {
                return;
            }

            if (!other.CompareTag("Player"))
            {
                return;
            }

            PlayerVitalSystem vital = other.GetComponent<PlayerVitalSystem>();
            if (vital == null)
            {
                vital = other.GetComponentInParent<PlayerVitalSystem>();
            }

            if (vital == null)
            {
                return;
            }

            if (!vital.TryTakeDamage(damagePerHit, transform.position))
            {
                return;
            }

            nextHitTime = Time.time + hitIntervalSeconds;
        }
    }
}
