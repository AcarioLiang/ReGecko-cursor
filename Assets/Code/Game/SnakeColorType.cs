using System;
using UnityEngine;

namespace ReGecko.Game
{
    /// <summary>
    /// 蛇的颜色类型枚举
    /// </summary>
    [Serializable]
    public enum SnakeColorType
    {
        /// <summary>
        /// 红色
        /// </summary>
        Red = 0,
        
        /// <summary>
        /// 蓝色
        /// </summary>
        Blue = 1,
        
        /// <summary>
        /// 绿色
        /// </summary>
        Green = 2,
        
        /// <summary>
        /// 黄色
        /// </summary>
        Yellow = 3,
        
        /// <summary>
        /// 紫色
        /// </summary>
        Purple = 4,
        
        /// <summary>
        /// 橙色
        /// </summary>
        Orange = 5,
        
        /// <summary>
        /// 青色
        /// </summary>
        Cyan = 6,
        
        /// <summary>
        /// 粉色
        /// </summary>
        Pink = 7
    }

    /// <summary>
    /// 颜色类型扩展方法
    /// </summary>
    public static class SnakeColorTypeExtensions
    {
        /// <summary>
        /// 获取颜色类型对应的Unity Color
        /// </summary>
        public static Color ToUnityColor(this SnakeColorType colorType)
        {
            switch (colorType)
            {
                case SnakeColorType.Red:
                    return Color.red;
                case SnakeColorType.Blue:
                    return Color.blue;
                case SnakeColorType.Green:
                    return Color.green;
                case SnakeColorType.Yellow:
                    return Color.yellow;
                case SnakeColorType.Purple:
                    return new Color(0.5f, 0f, 1f); // 紫色
                case SnakeColorType.Orange:
                    return new Color(1f, 0.5f, 0f); // 橙色
                case SnakeColorType.Cyan:
                    return Color.cyan;
                case SnakeColorType.Pink:
                    return new Color(1f, 0.75f, 0.8f); // 粉色
                default:
                    return Color.white;
            }
        }

        /// <summary>
        /// 获取颜色类型的显示名称
        /// </summary>
        public static string GetDisplayName(this SnakeColorType colorType)
        {
            switch (colorType)
            {
                case SnakeColorType.Red:
                    return "红色";
                case SnakeColorType.Blue:
                    return "蓝色";
                case SnakeColorType.Green:
                    return "绿色";
                case SnakeColorType.Yellow:
                    return "黄色";
                case SnakeColorType.Purple:
                    return "紫色";
                case SnakeColorType.Orange:
                    return "橙色";
                case SnakeColorType.Cyan:
                    return "青色";
                case SnakeColorType.Pink:
                    return "粉色";
                default:
                    return "未知";
            }
        }
    }
}
