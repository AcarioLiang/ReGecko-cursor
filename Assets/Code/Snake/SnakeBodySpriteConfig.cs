using UnityEngine;

namespace ReGecko.SnakeSystem
{
    /// <summary>
    /// 蛇身体图片配置文件
    /// </summary>
    [CreateAssetMenu(fileName = "SnakeBodySpriteConfig", menuName = "ReGecko/Snake Body Sprite Config")]
    public class SnakeBodySpriteConfig : ScriptableObject
    {
        [Header("蛇身体图片资源")]
        [Tooltip("竖直方向的蛇头图片")]
        public Sprite VerticalHeadSprite;
        
        [Tooltip("竖直方向的蛇尾图片")]
        public Sprite VerticalTailSprite;
        
        [Tooltip("竖直方向的身体图片")]
        public Sprite VerticalBodySprite;
        
        [Tooltip("L方向转弯的身体图片")]
        public Sprite LTurnBodySprite;

        [Header("图片设置")]
        [Tooltip("图片旋转角度")]
        [Range(0f, 360f)]
        public float RotationAngle = 90f;
        
        [Tooltip("图片缩放")]
        [Range(0.1f, 2f)]
        public float Scale = 1f;
        
        [Tooltip("图片颜色")]
        public Color TintColor = Color.white;
    }
}
