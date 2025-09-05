using System;
using UnityEngine;

namespace ReGecko.GridSystem
{
    /// <summary>
    /// 子网格辅助类 - 将每个大格细分为5x5小格
    /// *** SubGrid 改动开始 ***
    /// 所有小格坐标必须在中线上，即(2,x)或(x,2)的形式
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
        /// 将大格坐标转换为该大格内第一个小格的坐标（中线）
        /// </summary>
        /// <param name="bigCell">大格坐标</param>
        /// <returns>该大格左下角中线小格的坐标</returns>
        public static Vector2Int BigCellToFirstSubCell(Vector2Int bigCell)
        {
            return new Vector2Int(bigCell.x * SUB_DIV + CENTER_INDEX, bigCell.y * SUB_DIV);
        }

        /// <summary>
        /// 将大格坐标转换为该大格内最后一个小格的坐标（中线）
        /// </summary>
        /// <param name="bigCell">大格坐标</param>
        /// <returns>该大格右上角中线小格的坐标</returns>
        public static Vector2Int BigCellToLastSubCell(Vector2Int bigCell)
        {
            return new Vector2Int(bigCell.x * SUB_DIV + (SUB_DIV - 1), bigCell.y * SUB_DIV + CENTER_INDEX);
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
        /// </summary>
        /// <param name="subCell">小格坐标</param>
        /// <param name="grid">网格配置</param>
        /// <returns>世界坐标</returns>
        public static Vector3 SubCellToWorld(Vector2Int subCell, GridConfig grid)
        {
            // 验证小格坐标是否在中线上
            if (!IsValidSubCell(subCell))
            {
                Debug.LogWarning($"SubCell {subCell} is not on center line, this may cause unexpected behavior");
            }

            // 先转换到大格，再加上小格内的偏移
            Vector2Int bigCell = SubCellToBigCell(subCell);
            Vector3 bigCellWorld = grid.CellToWorld(bigCell);

            // 计算小格在大格内的偏移
            int subX = subCell.x % SUB_DIV;
            int subY = subCell.y % SUB_DIV;

            // 小格偏移量：从大格中心算起，范围是[-0.4, -0.2, 0, 0.2, 0.4]
            float offsetX = (subX - CENTER_INDEX) * SUB_CELL_SIZE;
            float offsetY = (subY - CENTER_INDEX) * SUB_CELL_SIZE;

            return bigCellWorld + new Vector3(offsetX, offsetY, 0f);
        }

        /// <summary>
        /// 将世界坐标转换为小格坐标
        /// 始终返回中线上的小格坐标，必须是(2,x)或(x,2)的形式
        /// </summary>
        /// <param name="world">世界坐标</param>
        /// <param name="grid">网格配置</param>
        /// <returns>小格坐标（在中线上）</returns>
        public static Vector2Int WorldToSubCell(Vector3 world, GridConfig grid)
        {
            // 先转换到大格坐标
            Vector2Int bigCell = grid.WorldToCell(world);
            Vector3 bigCellWorld = grid.CellToWorld(bigCell);

            // 计算在大格内的偏移
            Vector3 offset = world - bigCellWorld;

            // 计算X和Y方向的偏移量
            float xOffset = offset.x / SUB_CELL_SIZE;
            float yOffset = offset.y / SUB_CELL_SIZE;

            // 判断哪个方向更接近中线，选择该方向的中线
            if (Mathf.Abs(xOffset) <= Mathf.Abs(yOffset))
            {
                // X方向更接近中线，使用X方向的中线坐标 (2, y)
                int subY = Mathf.RoundToInt(yOffset + CENTER_INDEX);
                subY = Mathf.Clamp(subY, 0, SUB_DIV - 1);
                return new Vector2Int(bigCell.x * SUB_DIV + CENTER_INDEX, bigCell.y * SUB_DIV + subY);
            }
            else
            {
                // Y方向更接近中线，使用Y方向的中线坐标 (x, 2)
                int subX = Mathf.RoundToInt(xOffset + CENTER_INDEX);
                subX = Mathf.Clamp(subX, 0, SUB_DIV - 1);
                return new Vector2Int(bigCell.x * SUB_DIV + subX, bigCell.y * SUB_DIV + CENTER_INDEX);
            }
        }

        /// <summary>
        /// 检查小格是否在网格边界内
        /// </summary>
        /// <param name="subCell">小格坐标</param>
        /// <param name="grid">网格配置</param>
        /// <returns>是否在边界内</returns>
        public static bool IsSubCellInside(Vector2Int subCell, GridConfig grid)
        {
            Vector2Int bigCell = SubCellToBigCell(subCell);
            return grid.IsInside(bigCell);
        }

        /// <summary>
        /// 获取两个小格之间的曼哈顿距离
        /// </summary>
        /// <param name="from">起始小格</param>
        /// <param name="to">目标小格</param>
        /// <returns>曼哈顿距离</returns>
        public static int SubCellManhattan(Vector2Int from, Vector2Int to)
        {
            return Mathf.Abs(from.x - to.x) + Mathf.Abs(from.y - to.y);
        }

        /// <summary>
        /// 获取小格在其所属大格内的局部坐标
        /// </summary>
        /// <param name="subCell">小格坐标</param>
        /// <returns>局部坐标 (0-4, 0-4)</returns>
        public static Vector2Int GetSubCellLocalPos(Vector2Int subCell)
        {
            return new Vector2Int(subCell.x % SUB_DIV, subCell.y % SUB_DIV);
        }

        /// <summary>
        /// 检查小格是否在大格的中心线上（用于对齐）
        /// </summary>
        /// <param name="subCell">小格坐标</param>
        /// <returns>是否在中心线上</returns>
        public static bool IsSubCellOnCenterLine(Vector2Int subCell)
        {
            Vector2Int localPos = GetSubCellLocalPos(subCell);
            return localPos.x == CENTER_INDEX || localPos.y == CENTER_INDEX;
        }

        /// <summary>
        /// 生成从起始小格到目标小格的中线路径
        /// </summary>
        /// <param name="from">起始小格</param>
        /// <param name="to">目标小格</param>
        /// <returns>中线路径</returns>
        public static Vector2Int[] GenerateCenterLinePath(Vector2Int from, Vector2Int to)
        {
            if (!IsValidSubCell(from) || !IsValidSubCell(to))
            {
                Debug.LogWarning("Invalid sub cell coordinates for path generation");
                return new Vector2Int[] { from };
            }

            // 简化实现：直接返回目标位置
            // 实际应用中可能需要更复杂的路径规划
            return new Vector2Int[] { from, to };
        }
    }
}
// *** SubGrid 改动结束 ***