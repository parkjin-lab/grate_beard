using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LostBreadcrumbs.Runtime.Authoring
{
    public enum AuthoringMarkerType
    {
        Generic,
        EnemySpawn,
        PatrolPoint,
        Hideout,
        NoiseTest,
        FlashlightZone,
        Fork,
        Corridor
    }

    public sealed class AuthoringMarkerGizmo : MonoBehaviour
    {
        [SerializeField] private AuthoringMarkerType markerType = AuthoringMarkerType.Generic;
        [SerializeField, Min(0.05f)] private float markerSize = 0.5f;

        public AuthoringMarkerType MarkerType
        {
            get => markerType;
            set => markerType = value;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = GetColor(markerType);
            Gizmos.DrawWireCube(transform.position, Vector3.one * markerSize);

            Vector3 top = transform.position + Vector3.up * (markerSize * 0.5f);
            Gizmos.DrawLine(top, top + Vector3.up * markerSize * 0.7f);

#if UNITY_EDITOR
            Handles.color = Gizmos.color;
            Handles.Label(top + Vector3.up * markerSize * 0.8f, markerType.ToString());
#endif
        }

        private static Color GetColor(AuthoringMarkerType type)
        {
            return type switch
            {
                AuthoringMarkerType.EnemySpawn => new Color(1f, 0.2f, 0.2f),
                AuthoringMarkerType.PatrolPoint => new Color(1f, 0.6f, 0.2f),
                AuthoringMarkerType.Hideout => new Color(0.2f, 1f, 0.6f),
                AuthoringMarkerType.NoiseTest => new Color(0.2f, 0.6f, 1f),
                AuthoringMarkerType.FlashlightZone => new Color(1f, 1f, 0.2f),
                AuthoringMarkerType.Fork => new Color(1f, 0.3f, 1f),
                AuthoringMarkerType.Corridor => new Color(0.6f, 0.8f, 1f),
                _ => new Color(0.8f, 0.8f, 0.8f)
            };
        }
    }
}
