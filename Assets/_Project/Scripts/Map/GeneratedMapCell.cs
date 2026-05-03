using System;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Map
{
    public enum MapCellKind
    {
        Start,
        Corridor,
        Fork,
        Room,
        Hideout,
        Risk,
        Exit
    }

    [Serializable]
    public struct GeneratedMapCell
    {
        public GeneratedMapCell(Vector2Int position, MapCellKind kind, bool isMainPath, int order)
        {
            this.position = position;
            this.kind = kind;
            this.isMainPath = isMainPath;
            this.order = order;
        }

        public Vector2Int position;
        public MapCellKind kind;
        public bool isMainPath;
        public int order;
    }
}
