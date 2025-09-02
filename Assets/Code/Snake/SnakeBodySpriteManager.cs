using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using System.Reflection;

namespace ReGecko.SnakeSystem
{
    /// <summary>
    /// 蛇身体图片管理器：动态更新蛇身体各部位的图片
    /// </summary>
    public class SnakeBodySpriteManager : MonoBehaviour
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

        [Header("配置文件")]
        [Tooltip("蛇身体图片配置文件（可选）")]
        public SnakeBodySpriteConfig Config;

        [Header("图片设置")]
        [Tooltip("图片旋转角度")]
        public float RotationAngle = 90f;

        private SnakeController _snakeController;
        private List<Transform> _segments;
        private List<Image> _segmentImages;
        private List<RectTransform> _segmentRectTransforms;
        
        // 小段系统相关
        private bool _isSubSegmentModeEnabled = false;

        void Awake()
        {
            _snakeController = GetComponent<SnakeController>();
            _segments = new List<Transform>();
            _segmentImages = new List<Image>();
            _segmentRectTransforms = new List<RectTransform>();
        }

        void Start()
        {
            // 延迟初始化，等待SnakeController构建完成
            StartCoroutine(InitializeAfterSnakeBuilt());
        }

        void OnValidate()
        {
            // 在Inspector中修改配置后，自动更新
            if (Application.isPlaying)
            {
                LoadSpritesFromConfig();
                UpdateAllSegmentSprites();
            }
        }

        /// <summary>
        /// 从配置文件加载图片
        /// </summary>
        void LoadSpritesFromConfig()
        {
            if (Config == null) return;

            // 从配置文件加载图片
            if (Config.VerticalHeadSprite != null)
                VerticalHeadSprite = Config.VerticalHeadSprite;
            if (Config.VerticalTailSprite != null)
                VerticalTailSprite = Config.VerticalTailSprite;
            if (Config.VerticalBodySprite != null)
                VerticalBodySprite = Config.VerticalBodySprite;
            if (Config.LTurnBodySprite != null)
                LTurnBodySprite = Config.LTurnBodySprite;

            // 从配置文件加载设置
            RotationAngle = Config.RotationAngle;
        }

        System.Collections.IEnumerator InitializeAfterSnakeBuilt()
        {
            // 等待SnakeController构建完成
            yield return new WaitForEndOfFrame();
            
            // 检测是否启用了小段系统
            DetectSubSegmentMode();
            
            // 从配置文件加载图片
            LoadSpritesFromConfig();
            
            // 获取所有段
            CollectSegments();
            
            // 初始更新所有段的图片
            UpdateAllSegmentSprites();
        }
        
        /// <summary>
        /// 检测SnakeController是否启用了小段系统
        /// </summary>
        void DetectSubSegmentMode()
        {
            if (_snakeController != null)
            {
                // 通过反射获取EnableSubSegments字段的值
                var field = _snakeController.GetType().GetField("EnableSubSegments");
                if (field != null)
                {
                    _isSubSegmentModeEnabled = (bool)field.GetValue(_snakeController);
                }
                else
                {
                    _isSubSegmentModeEnabled = false;
                }
            }
        }

        void CollectSegments()
        {
            _segments.Clear();
            _segmentImages.Clear();
            _segmentRectTransforms.Clear();

            // 获取蛇的所有主段（不包括小段）
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                
                // 检查是否为主段（排除小段）
                bool isMainSegment = false;
                if (child.name.StartsWith("Head") || child.name.StartsWith("Body_"))
                {
                    // 如果启用了小段系统，需要确保这是主段而不是小段
                    if (_isSubSegmentModeEnabled)
                    {
                        // 小段的名字格式为 "SubSegment_X_Y"，主段名字不包含"SubSegment"
                        isMainSegment = !child.name.Contains("SubSegment");
                    }
                    else
                    {
                        // 没有小段系统时，所有符合条件的都是主段
                        isMainSegment = true;
                    }
                }
                
                if (isMainSegment)
                {
                    _segments.Add(child);
                    
                    // 在小段模式下，主段的Image组件可能被禁用，但我们仍需要引用它
                    var image = child.GetComponent<Image>();
                    if (image != null)
                    {
                        _segmentImages.Add(image);
                    }
                    else
                    {
                        _segmentImages.Add(null);
                    }
                    
                    var rt = child.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        _segmentRectTransforms.Add(rt);
                    }
                    else
                    {
                        _segmentRectTransforms.Add(null);
                    }
                }
            }
        }

        /// <summary>
        /// 更新所有段的图片
        /// </summary>
        public void UpdateAllSegmentSprites()
        {
            if (_segments.Count == 0) return;

            var bodyCells = _snakeController.GetBodyCells();
            if (bodyCells == null || bodyCells.Count == 0) return;

            // 更新蛇头
            if (_segments.Count > 0 && _segmentImages[0] != null)
            {
                UpdateHeadSprite(bodyCells);
            }

            // 更新身体段
            for (int i = 1; i < _segments.Count - 1; i++)
            {
                if (_segmentImages[i] != null)
                {
                    UpdateBodySprite(i, bodyCells);
                }
            }

            // 更新蛇尾
            if (_segments.Count > 1 && _segmentImages[_segments.Count - 1] != null)
            {
                UpdateTailSprite(bodyCells);
            }
            
            // 如果启用了小段系统，同步小段的图片
            if (_isSubSegmentModeEnabled)
            {
                SyncSubSegmentSprites();
            }
        }
        
        /// <summary>
        /// 同步小段的图片到主段的设置
        /// </summary>
        void SyncSubSegmentSprites()
        {
            for (int segmentIndex = 0; segmentIndex < _segments.Count; segmentIndex++)
            {
                var mainSegment = _segments[segmentIndex];
                var mainImage = _segmentImages[segmentIndex];
                var mainRT = _segmentRectTransforms[segmentIndex];
                
                if (mainSegment == null || mainImage == null) continue;
                
                // 找到该主段对应的所有小段
                var subSegments = FindSubSegments(mainSegment, segmentIndex);
                
                foreach (var subSegment in subSegments)
                {
                    var subImage = subSegment.GetComponent<Image>();
                    var subRT = subSegment.GetComponent<RectTransform>();
                    
                    if (subImage != null && subRT != null)
                    {
                        // 复制主段的图片和旋转设置到小段
                        subImage.sprite = mainImage.sprite;
                        subImage.color = mainImage.color;
                        // 注意：不复制旋转，因为小段有自己的位置逻辑
                        // subRT.rotation = mainRT.rotation;
                    }
                }
            }
        }
        
        /// <summary>
        /// 查找指定主段对应的所有小段
        /// </summary>
        List<Transform> FindSubSegments(Transform mainSegment, int segmentIndex)
        {
            var subSegments = new List<Transform>();
            
            // 遍历SnakeController下的所有子对象，找到对应的小段
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                
                // 小段命名格式：SubSegment_主段索引_小段索引
                if (child.name.StartsWith($"SubSegment_{segmentIndex}_"))
                {
                    subSegments.Add(child);
                }
            }
            
            return subSegments;
        }

        /// <summary>
        /// 更新蛇头图片
        /// </summary>
        void UpdateHeadSprite(LinkedList<Vector2Int> bodyCells)
        {
            if (bodyCells.Count < 2) return;

            var headCell = bodyCells.First.Value;
            var nextCell = bodyCells.First.Next.Value;
            
            var direction = nextCell - headCell;
            var isHorizontal = Mathf.Abs(direction.x) > Mathf.Abs(direction.y);
            
            var image = _segmentImages[0];
            var rt = _segmentRectTransforms[0];
            
            if (isHorizontal)
            {
                // 水平方向：使用竖直图片并旋转90度
                image.sprite = VerticalHeadSprite;
                if(headCell.x < nextCell.x)
                {
                    rt.rotation = Quaternion.Euler(0, 0, 360 - RotationAngle);
                }
                else
                {
                    rt.rotation = Quaternion.Euler(0, 0, RotationAngle);
                }
            }
            else
            {
                // 竖直方向：使用竖直图片，不旋转
                image.sprite = VerticalHeadSprite;
                rt.rotation = Quaternion.identity;

                if (headCell.y < nextCell.y)
                {
                    rt.rotation = Quaternion.identity;
                }
                else
                {
                    rt.rotation = Quaternion.Euler(0, 0, 180);
                }
            }
        }

        /// <summary>
        /// 更新身体段图片
        /// </summary>
        void UpdateBodySprite(int segmentIndex, LinkedList<Vector2Int> bodyCells)
        {
            if (bodyCells.Count < 3 || segmentIndex >= bodyCells.Count - 1) return;

            var bodyCellsList = bodyCells.ToList();
            var currentCell = bodyCellsList[segmentIndex];
            var prevCell = bodyCellsList[segmentIndex - 1];
            var nextCell = bodyCellsList[segmentIndex + 1];
            
            var prevDirection = currentCell - prevCell;
            var nextDirection = nextCell - currentCell;
            
            var image = _segmentImages[segmentIndex];
            var rt = _segmentRectTransforms[segmentIndex];
            
            // 判断是否为拐角
            bool isCorner = IsCorner(prevDirection, nextDirection);
            
            if (isCorner)
            {
                // 拐角：使用L形状图片
                image.sprite = LTurnBodySprite;
                float rotation = CalculateCornerRotation(prevDirection, nextDirection);
                rt.rotation = Quaternion.Euler(0, 0, rotation);
            }
            else
            {
                // 直线：使用竖直身体图片
                image.sprite = VerticalBodySprite;
                
                // 判断整体方向
                var overallDirection = prevDirection + nextDirection;
                var isHorizontal = Mathf.Abs(overallDirection.x) > Mathf.Abs(overallDirection.y);
                
                if (isHorizontal)
                {
                    // 水平方向：旋转90度
                    rt.rotation = Quaternion.Euler(0, 0, RotationAngle);
                }
                else
                {
                    // 竖直方向：不旋转
                    rt.rotation = Quaternion.identity;
                }
            }
        }

        /// <summary>
        /// 更新蛇尾图片
        /// </summary>
        void UpdateTailSprite(LinkedList<Vector2Int> bodyCells)
        {
            if (bodyCells.Count < 2) return;

            var tailCell = bodyCells.Last.Value;
            var prevCell = bodyCells.Last.Previous.Value;
            
            var direction = tailCell - prevCell;
            var isHorizontal = Mathf.Abs(direction.x) > Mathf.Abs(direction.y);
            
            var image = _segmentImages[_segments.Count - 1];
            var rt = _segmentRectTransforms[_segments.Count - 1];
            
            if (isHorizontal)
            {
                // 水平方向：使用竖直图片并旋转90度
                image.sprite = VerticalTailSprite;
                if (tailCell.x < prevCell.x)
                {
                    rt.rotation = Quaternion.Euler(0, 0, RotationAngle);
                }
                else
                {
                    rt.rotation = Quaternion.Euler(0, 0, 360 - RotationAngle);
                }
            }
            else
            {
                // 竖直方向：使用竖直图片，不旋转
                image.sprite = VerticalTailSprite;

                if (tailCell.y < prevCell.y)
                {
                    rt.rotation = Quaternion.Euler(0, 0, 180);
                }
                else
                {
                    rt.rotation = Quaternion.identity;
                }
            }
        }

        /// <summary>
        /// 判断是否为拐角
        /// </summary>
        bool IsCorner(Vector2Int prevDir, Vector2Int nextDir)
        {
            // 如果前后方向不同，则为拐角
            return prevDir != nextDir;
        }

        /// <summary>
        /// 计算拐角的旋转角度
        /// </summary>
        float CalculateCornerRotation(Vector2Int prevDir, Vector2Int nextDir)
        {
            // 将Vector2Int转换为Vector2并标准化
            Vector2 prevNormalized = new Vector2(prevDir.x, prevDir.y).normalized;
            Vector2 nextNormalized = new Vector2(nextDir.x, nextDir.y).normalized;
            
            // 计算角度差
            float angle = Vector2.SignedAngle(prevNormalized, nextNormalized);

            float offset = 90f;
            // 根据L图片的默认方向调整旋转
            // 假设L图片默认是向右上角弯曲
            if (prevDir.x > 0 && nextDir.y > 0) // 右 -> 上
                return offset + 0f;
            else if (prevDir.x > 0 && nextDir.y < 0) // 右 -> 下
                return offset + 90f;
            else if (prevDir.x < 0 && nextDir.y > 0) // 左 -> 上
                return offset + -90f;
            else if (prevDir.x < 0 && nextDir.y < 0) // 左 -> 下
                return offset + 180f;
            else if (prevDir.y > 0 && nextDir.x > 0) // 上 -> 右
                return offset + 180f;
            else if (prevDir.y > 0 && nextDir.x < 0) // 上 -> 左
                return offset + 90f;
            else if (prevDir.y < 0 && nextDir.x > 0) // 下 -> 右
                return offset + 270f;
            else if (prevDir.y < 0 && nextDir.x < 0) // 下 -> 左
                return offset + 0f;

            return offset;
        }

        /// <summary>
        /// 强制刷新所有段的图片
        /// </summary>
        public void ForceRefreshAllSprites()
        {
            // 重新检测小段模式（可能在运行时改变）
            DetectSubSegmentMode();
            CollectSegments();
            UpdateAllSegmentSprites();
        }

        /// <summary>
        /// 当蛇移动后调用，更新图片
        /// </summary>
        public void OnSnakeMoved()
        {
            UpdateAllSegmentSprites();
        }

        /// <summary>
        /// 当蛇长度改变后调用，重新收集段并更新图片
        /// </summary>
        public void OnSnakeLengthChanged()
        {
            // 重新检测小段模式（长度改变时可能启用/禁用小段）
            DetectSubSegmentMode();
            CollectSegments();
            UpdateAllSegmentSprites();
        }
        
        /// <summary>
        /// 当小段模式状态改变时调用
        /// </summary>
        public void OnSubSegmentModeChanged()
        {
            DetectSubSegmentMode();
            CollectSegments();
            UpdateAllSegmentSprites();
        }

    }
}
