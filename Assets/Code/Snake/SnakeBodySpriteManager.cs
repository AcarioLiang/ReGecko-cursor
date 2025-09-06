using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using ReGecko.GridSystem; // *** SubGrid 改动开始 ***

namespace ReGecko.SnakeSystem
{
    /// <summary>
    /// 蛇身体图片管理器：动态更新蛇身体各部位的图片
    /// *** SubGrid 改动：支持5段身体细分渲染 ***
    /// </summary>
    public class SnakeBodySpriteManager : MonoBehaviour
    {
        /*
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

        // *** SubGrid 改动：5段身体细分渲染 ***
        [Header("SubGrid 细分渲染")]
        [Tooltip("是否启用5段身体细分渲染")]
        public bool EnableSubSegmentRendering = false;
        [Tooltip("子段预制体")]
        public GameObject SubSegmentPrefab;
        
        // 每个大格身体节点对应的5个子段
        private Dictionary<int, List<GameObject>> _subSegments = new Dictionary<int, List<GameObject>>();
        private Queue<GameObject> _subSegmentPool = new Queue<GameObject>();

        void Awake()
        {
            _snakeController = transform.parent.GetComponent<SnakeController>();
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
            
            // 从配置文件加载图片
            LoadSpritesFromConfig();

            // 获取所有段
            CollectSegments();

            // 初始更新所有段的图片
            UpdateAllSegmentSprites();

            // *** SubGrid 改动：确保子段位置正确初始化 ***
            if (EnableSubSegmentRendering)
            {
                RecreateSubSegments();
                InitializeSubSegmentPositions();
            }
        }

        void CollectSegments()
        {
            _segments.Clear();
            _segmentImages.Clear();
            _segmentRectTransforms.Clear();

            var parentT = transform.parent;
            if (parentT == null)
                return;

            // 获取蛇的所有段
            for (int i = 0; i < parentT.childCount; i++)
            {
                var child = parentT.GetChild(i);
                if (child.name.StartsWith("Head") || child.name.StartsWith("Body_"))
                {
                    _segments.Add(child);
                    
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
            CollectSegments();
            UpdateAllSegmentSprites();
            
            // *** SubGrid 改动：重新创建子段 ***
            if (EnableSubSegmentRendering)
            {
                RecreateSubSegments();
            }
        }

        // *** SubGrid 改动：子段管理方法开始 ***
        
        /// <summary>
        /// 创建或获取子段GameObject
        /// </summary>
        GameObject GetSubSegmentFromPool()
        {
            if (_subSegmentPool.Count > 0)
            {
                var obj = _subSegmentPool.Dequeue();
                obj.SetActive(true);
                return obj;
            }
            
            if (SubSegmentPrefab != null)
            {
                return Instantiate(SubSegmentPrefab, transform);
            }
            
            // 如果没有预制体，创建一个基本的Image对象
            var go = new GameObject("SubSegment");
            go.transform.SetParent(transform);
            var image = go.AddComponent<Image>();
            image.sprite = VerticalBodySprite;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(100f, SubGridHelper.SUB_CELL_SIZE * 100f); // 转换为UI单位
            return go;
        }

        /// <summary>
        /// 回收子段到对象池
        /// </summary>
        void ReturnSubSegmentToPool(GameObject obj)
        {
            obj.SetActive(false);
            _subSegmentPool.Enqueue(obj);
        }

        /// <summary>
        /// 重新创建所有子段
        /// </summary>
        void RecreateSubSegments()
        {
            // 清理现有子段
            foreach (var kvp in _subSegments)
            {
                foreach (var subSeg in kvp.Value)
                {
                    ReturnSubSegmentToPool(subSeg);
                }
            }
            _subSegments.Clear();

            // 为每个身体节点创建5个子段
            var bodyCells = _snakeController.GetBodyCells();
            if (bodyCells == null) return;

            int segmentIndex = 0;
            foreach (var cell in bodyCells)
            {
                var subSegmentList = new List<GameObject>();
                for (int i = 0; i < SubGridHelper.SUB_DIV; i++)
                {
                    var subSegment = GetSubSegmentFromPool();
                    subSegmentList.Add(subSegment);
                }
                _subSegments[segmentIndex] = subSegmentList;
                segmentIndex++;
            }

            // *** SubGrid 改动：创建子段后立即初始化位置 ***
            InitializeSubSegmentPositions();
        }

        /// <summary>
        /// 初始化所有子段的位置
        /// </summary>
        void InitializeSubSegmentPositions()
        {
            if (!EnableSubSegmentRendering)
                return;

            // 为每个身体节点初始化5个子段的位置
            var bodyCells = _snakeController.GetBodyCells();
            if (bodyCells == null) return;

            var grid = _snakeController.GetGrid();
            int segmentIndex = 0;
            
            foreach (var bigCell in bodyCells)
            {
                if (!_subSegments.ContainsKey(segmentIndex))
                {
                    segmentIndex++;
                    continue;
                }

                // 初始化时，所有子段都居中对齐到大格中心
                var centerSubCell = SubGridHelper.BigCellToCenterSubCell(bigCell);
                var bigCellFirstSub = SubGridHelper.BigCellToCenterSubCell(bigCell);
                
                Vector2Int[] subCellPositions = new Vector2Int[SubGridHelper.SUB_DIV];
                for (int i = 0; i < SubGridHelper.SUB_DIV; i++)
                {
                    // 初始状态：所有子段沿Y轴排列，居中对齐
                    subCellPositions[i] = new Vector2Int(
                        bigCellFirstSub.x + SubGridHelper.SUB_DIV / 2,
                        bigCellFirstSub.y + i
                    );
                }
                
                // 更新子段位置
                UpdateSubSegmentPositions(segmentIndex, subCellPositions);
                segmentIndex++;
            }
        }

        /// <summary>
        /// 更新子段位置（由SnakeController调用）
        /// </summary>
        /// <param name="segmentIndex">身体节点索引</param>
        /// <param name="subCellPositions">5个子段的小格坐标</param>
        public void UpdateSubSegmentPositions(int segmentIndex, Vector2Int[] subCellPositions)
        {
            if (!EnableSubSegmentRendering || !_subSegments.ContainsKey(segmentIndex))
                return;

            var subSegmentList = _subSegments[segmentIndex];
            var grid = _snakeController.GetGrid();

            //for (int i = 0; i < Mathf.Min(subSegmentList.Count, subCellPositions.Length); i++)
            //{
            //    var subSegment = subSegmentList[i];
            //    var worldPos = SubGridHelper.SubCellToWorld(subCellPositions[i], grid);
            //    var rt = subSegment.GetComponent<RectTransform>();
            //    if (rt != null)
            //    {
            //        rt.anchoredPosition = new Vector2(worldPos.x, worldPos.y);
            //    }
            //}


            float maxOffset = grid.CellSize * 0.4f;

            for (int i = 0; i < Mathf.Min(subSegmentList.Count, subCellPositions.Length); i++)
            {
                var worldPos = SubGridHelper.SubCellToWorld(subCellPositions[i], grid);
                // 小段沿大段方向线性分布
                float t = (float)i / (SubGridHelper.SUB_DIV - 1); // 0, 0.25, 0.5, 0.75, 1
                float offset = (t - 0.5f) * maxOffset * 2f; // 在 ±maxOffset 范围内分布
                Vector3 pos = worldPos + Vector3.up * offset;

                var rt = subSegmentList[i].GetComponent<RectTransform>();
                if (rt != null)
                {
                    // 小段跟随大段位置
                    rt.anchoredPosition = new Vector2(pos.x, pos.y);

                    // 直线状态下不旋转
                    rt.rotation = Quaternion.Euler(0, 0, 0f);
                }
            }
        }

        /// <summary>
        /// 切换子段渲染模式
        /// </summary>
        public void SetSubSegmentMode(bool useSubSegments)
        {
            EnableSubSegmentRendering = useSubSegments;
            
            if (useSubSegments)
            {
                // 启用子段模式：隐藏原始段，显示子段
                foreach (var segment in _segments)
                {
                    segment.gameObject.SetActive(false);
                }
                
                // 确保子段存在并显示
                if (_subSegments.Count == 0)
                {
                    RecreateSubSegments();
                }
                
                foreach (var kvp in _subSegments)
                {
                    foreach (var subSeg in kvp.Value)
                    {
                        subSeg.SetActive(true);
                    }
                }
            }
            else
            {
                // 禁用子段模式：显示原始段，隐藏子段
                foreach (var segment in _segments)
                {
                    segment.gameObject.SetActive(true);
                }
                
                foreach (var kvp in _subSegments)
                {
                    foreach (var subSeg in kvp.Value)
                    {
                        subSeg.SetActive(false);
                    }
                }
            }
        }

        // *** SubGrid 改动：子段管理方法结束 ***
        
        */
    }
}
