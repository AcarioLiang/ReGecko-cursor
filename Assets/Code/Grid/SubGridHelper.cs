using System;
using UnityEngine;

namespace ReGecko.GridSystem
{
    /// <summary>
    /// 子网格辅助类 - 将每个大格细分为5x5小格
    /// *** SubGrid 改动开始 ***
    /// 所有小格坐标必须在中线上，即(2,x)或(x,2)的形式（全局为5n+2）
    /// </summary>
    public static class SubGridHelper
    {
        public const int SUB_DIV = 5; // 每个大格细分为5x5小格
        public const float SUB_CELL_SIZE = 0.2f; // 小格尺寸 = CellSize / 5f = 1.0f / 5f
        public const int CENTER_INDEX = SUB_DIV / 2; // 中线索引 = 2

        /// <summary>
        /// 验证小格坐标是否在中线上
        /// </summary>
        /// <param name="subCell">小格坐标</param>
        /// <returns>是否在中线上</returns>
        public static bool IsValidSubCell(Vector2Int subCell)
        {
            Vector2Int localPos = GetSubCellLocalPos(subCell);
            return localPos.x == CENTER_INDEX || localPos.y == CENTER_INDEX;
        }

        /// <summary>
        /// 将大格坐标转换为该大格内左侧中线的小格坐标（xLocal=0, yLocal=CENTER_INDEX）
        /// </summary>
        public static Vector2Int BigCellToLeftSubCell(Vector2Int bigCell)
        {
            return new Vector2Int(bigCell.x * SUB_DIV + 0, bigCell.y * SUB_DIV + CENTER_INDEX);
        }

        /// <summary>
        /// 将大格坐标转换为该大格内右侧中线的小格坐标（xLocal=SUB_DIV-1, yLocal=CENTER_INDEX）
        /// </summary>
        public static Vector2Int BigCellToRightSubCell(Vector2Int bigCell)
        {
            return new Vector2Int(bigCell.x * SUB_DIV + (SUB_DIV - 1), bigCell.y * SUB_DIV + CENTER_INDEX);
        }

        /// <summary>
        /// 将大格坐标转换为该大格内竖直中线最上的小格坐标（xLocal=CENTER_INDEX, yLocal=SUB_DIV-1）
        /// </summary>
        public static Vector2Int BigCellToTopSubCell(Vector2Int bigCell)
        {
            return new Vector2Int(bigCell.x * SUB_DIV + CENTER_INDEX, bigCell.y * SUB_DIV + (SUB_DIV - 1));
        }

        /// <summary>
        /// 将大格坐标转换为该大格内竖直中线最下的小格坐标（xLocal=CENTER_INDEX, yLocal=0）
        /// </summary>
        public static Vector2Int BigCellToBottomSubCell(Vector2Int bigCell)
        {
            return new Vector2Int(bigCell.x * SUB_DIV + CENTER_INDEX, bigCell.y * SUB_DIV + 0);
        }

        /// <summary>
        /// 将大格坐标转换为该大格中心小格的坐标
        /// </summary>
        /// <param name="bigCell">大格坐标</param>
        /// <returns>该大格中心小格的坐标</returns>
        public static Vector2Int BigCellToCenterSubCell(Vector2Int bigCell)
        {
            return new Vector2Int(bigCell.x * SUB_DIV + CENTER_INDEX, bigCell.y * SUB_DIV + CENTER_INDEX);
        }

        /// <summary>
        /// 将小格坐标转换为对应的大格坐标
        /// </summary>
        /// <param name="subCell">小格坐标</param>
        /// <returns>对应的大格坐标</returns>
        public static Vector2Int SubCellToBigCell(Vector2Int subCell)
        {
            return new Vector2Int(subCell.x / SUB_DIV, subCell.y / SUB_DIV);
        }

        /// <summary>
        /// 将小格坐标转换为世界坐标
        /// 非中线输入将被自动投影到同一大格最近的中线位置
        /// </summary>
        public static Vector3 SubCellToWorld(Vector2Int subCell, GridConfig grid)
        {
            Vector2Int bigCell = SubCellToBigCell(subCell);
            Vector3 bigCellWorld = grid.CellToWorld(bigCell);

            // 计算（并必要时投影）在大格内的局部坐标
            Vector2Int local = GetSubCellLocalPos(subCell);
            if (local.x != CENTER_INDEX && local.y != CENTER_INDEX)
            {
                // 投影到最近的一条中线（与 WorldToSubCell 的“更靠近哪条中线”规则一致）
                int dx = Mathf.Abs(local.x - CENTER_INDEX);
                int dy = Mathf.Abs(local.y - CENTER_INDEX);
                if (dx <= dy)
                    local = new Vector2Int(CENTER_INDEX, local.y);
                else
                    local = new Vector2Int(local.x, CENTER_INDEX);
            }

            // 局部偏移：从大格中心算起，范围是[-0.4, -0.2, 0, 0.2, 0.4] * CellSize
            float unit = SUB_CELL_SIZE * grid.CellSize;
            float offsetX = (local.x - CENTER_INDEX) * unit;
            float offsetY = (local.y - CENTER_INDEX) * unit;

            return bigCellWorld + new Vector3(offsetX, offsetY, 0f);
        }

        /// <summary>
        /// 将世界坐标转换为小格坐标（始终返回中线上的小格坐标）
        /// </summary>
        public static Vector2Int WorldToSubCell(Vector3 world, GridConfig grid)
        {
            Vector2Int bigCell = grid.WorldToCell(world);
            Vector3 bigCellWorld = grid.CellToWorld(bigCell);

            Vector3 offset = world - bigCellWorld;
            float unit = SUB_CELL_SIZE * grid.CellSize; // = grid.CellSize / 5f

            float xUnits = offset.x / unit;
            float yUnits = offset.y / unit;

            // 判断更靠近哪条中线：若更靠近竖直中线 → 返回(2, y)，否则返回(x, 2)
            if (Mathf.Abs(xUnits) <= Mathf.Abs(yUnits))
            {
                int subY = Mathf.RoundToInt(CENTER_INDEX + yUnits);
                subY = Mathf.Clamp(subY, 0, SUB_DIV - 1);
                return new Vector2Int(bigCell.x * SUB_DIV + CENTER_INDEX, bigCell.y * SUB_DIV + subY);
            }
            else
            {
                int subX = Mathf.RoundToInt(CENTER_INDEX + xUnits);
                subX = Mathf.Clamp(subX, 0, SUB_DIV - 1);
                return new Vector2Int(bigCell.x * SUB_DIV + subX, bigCell.y * SUB_DIV + CENTER_INDEX);
            }
        }

        /// <summary>
        /// 检查小格是否在网格边界内（按大格边界判断）
        /// </summary>
        public static bool IsSubCellInside(Vector2Int subCell, GridConfig grid)
        {
            Vector2Int bigCell = SubCellToBigCell(subCell);
            return grid.IsInside(bigCell);
        }

        /// <summary>
        /// 获取两个小格之间的曼哈顿距离
        /// </summary>
        public static int SubCellManhattan(Vector2Int from, Vector2Int to)
        {
            return Mathf.Abs(from.x - to.x) + Mathf.Abs(from.y - to.y);
        }

        /// <summary>
        /// 获取小格在其所属大格内的局部坐标 (0-4, 0-4)，对负数也安全
        /// </summary>
        public static Vector2Int GetSubCellLocalPos(Vector2Int subCell)
        {
            return new Vector2Int(Mod(subCell.x, SUB_DIV), Mod(subCell.y, SUB_DIV));
        }

        /// <summary>
        /// 检查小格是否在大格的中心线上（用于对齐）
        /// </summary>
        public static bool IsSubCellOnCenterLine(Vector2Int subCell)
        {
            Vector2Int localPos = GetSubCellLocalPos(subCell);
            return localPos.x == CENTER_INDEX || localPos.y == CENTER_INDEX;
        }

        /// <summary>
        /// 生成从起始小格到目标小格的中线路径（最短曼哈顿路径，沿中线转角）
        /// </summary>
        public static Vector2Int[] GenerateCenterLinePath(Vector2Int from, Vector2Int to)
        {
            if (!IsValidSubCell(from) || !IsValidSubCell(to))
            {
                Debug.LogWarning("Invalid sub cell coordinates for path generation");
                // 投影到最近中线后再生成路径
                from = SnapToCenterLine(from);
                to = SnapToCenterLine(to);
            }

            if (from == to) return new[] { from };

            bool fromVertical = GetSubCellLocalPos(from).x == CENTER_INDEX;
            bool toVertical = GetSubCellLocalPos(to).x == CENTER_INDEX;
            bool sameX = from.x == to.x;
            bool sameY = from.y == to.y;

            // 同一直线（无需拐弯）
            if (sameX || sameY)
                return new[] { from, to };

            // 一垂一直 → 交点必为(5n+2, 5m+2)
            if (fromVertical && !toVertical)
            {
                Vector2Int cross = new Vector2Int(from.x, to.y);
                return new[] { from, cross, to };
            }
            if (!fromVertical && toVertical)
            {
                Vector2Int cross = new Vector2Int(to.x, from.y);
                return new[] { from, cross, to };
            }

            // 同为竖线：需经最近的水平中线行拐弯
            if (fromVertical && toVertical)
            {
                int yMid = (from.y / SUB_DIV) * SUB_DIV + CENTER_INDEX;
                Vector2Int a = new Vector2Int(from.x, yMid);
                Vector2Int b = new Vector2Int(to.x, yMid);
                return new[] { from, a, b, to };
            }

            // 同为横线：需经最近的竖直中线列拐弯
            int xMid = (from.x / SUB_DIV) * SUB_DIV + CENTER_INDEX;
            Vector2Int c = new Vector2Int(xMid, from.y);
            Vector2Int d = new Vector2Int(xMid, to.y);
            return new[] { from, c, d, to };
        }

        // --- 私有工具 ---

        private static int Mod(int a, int m)
        {
            int r = a % m;
            return r < 0 ? r + m : r;
        }

        private static Vector2Int SnapToCenterLine(Vector2Int subCell)
        {
            Vector2Int local = GetSubCellLocalPos(subCell);
            if (local.x == CENTER_INDEX || local.y == CENTER_INDEX) return subCell;

            int dx = Mathf.Abs(local.x - CENTER_INDEX);
            int dy = Mathf.Abs(local.y - CENTER_INDEX);
            if (dx <= dy)
            {
                int snappedX = (subCell.x / SUB_DIV) * SUB_DIV + CENTER_INDEX;
                return new Vector2Int(snappedX, subCell.y);
            }
            else
            {
                int snappedY = (subCell.y / SUB_DIV) * SUB_DIV + CENTER_INDEX;
                return new Vector2Int(subCell.x, snappedY);
            }
        }
    }
}