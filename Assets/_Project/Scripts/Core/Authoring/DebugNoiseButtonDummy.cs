using LostBreadcrumbs.Runtime.Managers;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Authoring
{
    public sealed class DebugNoiseButtonDummy : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float loudness = 2.2f;
        [SerializeField, Min(0.5f)] private float radius = 6f;

        [ContextMenu("Emit Test Noise")]
        public void EmitTestNoise()
        {
            if (NoiseManager.Instance == null)
            {
                Debug.LogWarning("NoiseManager is missing in scene.", this);
                return;
            }

            NoiseManager.Instance.EmitNoise(transform.position, loudness, radius, NoiseKind.Decoy, gameObject);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
