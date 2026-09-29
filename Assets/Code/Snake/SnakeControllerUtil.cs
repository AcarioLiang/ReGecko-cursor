using System.Collections.Generic;
using ReGecko.GridSystem;
using UnityEngine;

namespace ReGecko.SnakeSystem
{
    static class SnakeControllerUtil
    {
        public static readonly Vector2Int InvalidCell = new Vector2Int(-1, -1);

        public static readonly Vector2Int[] CardinalDirections =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1)
        };

        public static Vector2Int[] ForwardLeftRight(Vector2Int direction)
        {
            return new[]
            {
                direction,
                new Vector2Int(-direction.y, direction.x),
                new Vector2Int(direction.y, -direction.x)
            };
        }

        public static Vector2Int DirectionBetween(Vector2Int from, Vector2Int to)
        {
            return new Vector2Int(
                Mathf.Clamp(from.x - to.x, -1, 1),
                Mathf.Clamp(from.y - to.y, -1, 1));
        }

        public static Vector2Int GetCellAt(LinkedList<Vector2Int> cells, int index, Vector2Int fallback)
        {
            if (cells == null || index < 0 || index >= cells.Count)
            {
                return fallback;
            }

            var node = cells.First;
            for (int i = 0; i < index && node != null; i++)
            {
                node = node.Next;
            }

            return node?.Value ?? fallback;
        }

        public static Vector3 ScreenToGridWorld(Vector3 screen, RectTransform gridRect, Camera camera, GridConfig grid)
        {
            if (gridRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(gridRect, screen, camera, out Vector2 localPoint))
            {
                return new Vector3(localPoint.x, localPoint.y, 0f);
            }

            Vector2 screenSize = new Vector2(Screen.width, Screen.height);
            Vector2 normalizedScreen = new Vector2(screen.x / screenSize.x, screen.y / screenSize.y);

            float gridWidth = grid.Width * grid.CellSize;
            float gridHeight = grid.Height * grid.CellSize;
            float worldX = (normalizedScreen.x - 0.5f) * gridWidth;
            float worldY = (normalizedScreen.y - 0.5f) * gridHeight;

            return new Vector3(worldX, worldY, 0f);
        }
    }
}
