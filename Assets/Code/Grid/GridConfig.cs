using System;
using UnityEngine;

namespace ReGecko.GridSystem
{
    [Serializable]
    public struct GridConfig
    {
        public int Width;
        public int Height;
        public float CellSize;

        public bool IsValid()
        {
            return Width != 0 && Height != 0;

        }

        // 阻挡占位：暂时全部为无阻挡
        public bool HasBlock(int x, int y)
        {
            return false;
        }

        public bool IsInsideSub(Vector2Int subCell)
        {
            return subCell.x >= 0 && subCell.x < Width * SubGridHelper.SUB_DIV && subCell.y >= 0 && subCell.y < Height * SubGridHelper.SUB_DIV &&
                SubGridHelper.IsValidSubCell(subCell);
        }

        public bool IsInside(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Height;
        }

        public bool IsInside(int x, int y)
        {
            return x >= 0 && x < Width && y >= 0 && y < Height;
        }

        public Vector3 CellToWorld(Vector2Int cell)
        {
            // 以网格中心为原点的坐标系
            float centerX = (Width - 1) * 0.5f * CellSize;
            float centerY = (Height - 1) * 0.5f * CellSize;
            float worldX = cell.x * CellSize - centerX;
            float worldY = cell.y * CellSize - centerY;
            return new Vector3(worldX, worldY, 0f);
        }

        public Vector2Int WorldToCell(Vector3 world)
        {
            // 转换回网格坐标（考虑中心原点）
            float centerX = (Width - 1) * 0.5f * CellSize;
            float centerY = (Height - 1) * 0.5f * CellSize;
            int x = Mathf.RoundToInt((world.x + centerX) / CellSize);
            int y = Mathf.RoundToInt((world.y + centerY) / CellSize);
            return new Vector2Int(x, y);
        }

        public Vector3 GetGridCenterWorld()
        {
            // 在新的中心原点坐标系中，网格中心就是(0,0)
            return Vector3.zero;
        }
    }
}


