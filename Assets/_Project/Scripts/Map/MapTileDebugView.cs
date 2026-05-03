using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LostBreadcrumbs.Runtime.Map
{
    public sealed class MapTileDebugView : MonoBehaviour
    {
        [SerializeField] private MapCellKind kind;
        [SerializeField] private bool isMainPath;
        [SerializeField] private int order;

        public void Apply(GeneratedMapCell cell)
        {
            kind = cell.kind;
            isMainPath = cell.isMainPath;
            order = cell.order;
        }

        public Color GetTintColor()
        {
            Color color = kind switch
            {
                MapCellKind.Start => new Color(0.3f, 0.9f, 0.3f),
                MapCellKind.Corridor => new Color(0.85f, 0.85f, 0.85f),
                MapCellKind.Fork => new Color(1f, 0.6f, 0.2f),
                MapCellKind.Room => new Color(0.6f, 0.8f, 1f),
                MapCellKind.Hideout => new Color(0.2f, 1f, 0.7f),
                MapCellKind.Risk => new Color(1f, 0.35f, 0.35f),
                MapCellKind.Exit => new Color(1f, 0.9f, 0.2f),
                _ => Color.white
            };

            if (!isMainPath)
            {
                color *= 0.85f;
            }

            color.a = 0.9f;
            return color;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = GetTintColor();
            Gizmos.DrawCube(transform.position, Vector3.one * 0.9f);

#if UNITY_EDITOR
            Handles.color = Color.white;
            Handles.Label(transform.position + Vector3.up * 0.55f, $"{order}:{kind}");
#endif
        }
    }
}
