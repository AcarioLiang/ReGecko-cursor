using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ReGecko.GridSystem;
using ReGecko.Grid.Entities;
using System.Collections;
using ReGecko.GameCore.Flow;
using System.Linq;

namespace ReGecko.SnakeSystem
{
    public class SnakeController : BaseSnake
    {
        [Header("SnakeController特有属性")]
        // 拖拽相关
        bool _startMove;
        bool _dragging;
        bool _dragFromHead;

        // *** SubGrid 改动开始 ***
        [Header("SubGrid 小格移动系统")]
        [Tooltip("是否启用小格移动系统")]
        public bool EnableSubGridMovement = true;
        [Tooltip("小格移动速度（小格/秒）")]
        public float MoveSpeedSubCellsPerSecond = 25f; // 5倍于原来的大格速度

        private List<GameObject> _subSegments = new List<GameObject>();// 每段的5个小段
        private Queue<GameObject> _subSegmentPool = new Queue<GameObject>();

        protected readonly LinkedList<Vector2Int> _subBodyCells = new LinkedList<Vector2Int>(); // 离散身体占用格，头在First
        private readonly List<RectTransform> _cachedSubRectTransforms = new List<RectTransform>();
        private List<Image> _segmentImages = new List<Image>();

        // 小格移动相关
        private Vector2Int _currentHeadCell;
        private Vector2Int _currentTailCell;
        private Vector2Int _currentHeadSubCell;
        private Vector2Int _currentTailSubCell;
        private Vector2Int _lastSampledSubCell; // 上次采样的手指小格
        private LinkedList<Vector2Int> _subCellPathQueue = new LinkedList<Vector2Int>(); // 小格路径队列
        private LinkedList<Vector2Int> _headCellToBodyPathQueue = new LinkedList<Vector2Int>(); // 小格路径队列
        private float _subCellMoveAccumulator; // 小格移动累积器
        // *** SubGrid 改动结束 ***

        //优化缓存
        Vector3 _lastMousePos;
        Canvas _parentCanvas;


        // 拖动优化：减少更新频率
        private float _lastDragUpdateTime = 0f;
        private const float DRAG_UPDATE_INTERVAL = 0.016f; // 约60FPS更新频率

        // 路径队列优化
        private const int MAX_PATH_QUEUE_SIZE = 10; // 路径队列最大长度
        private const int PATH_QUEUE_TRIM_SIZE = 10; // 超出限制时保留的路径数量

        enum DragAxis { None, X, Y }
        DragAxis _dragAxis = DragAxis.None;

        Coroutine _consumeCoroutine;

        public override void Initialize(GridConfig grid, GridEntityManager entityManager = null, SnakeManager snakeManager = null)
        {
            _grid = grid;
            _entityManager = entityManager ?? FindObjectOfType<GridEntityManager>();
            _snakeManager = snakeManager ?? FindObjectOfType<SnakeManager>();
            _parentCanvas = GetComponentInParent<Canvas>();

            // *** SubGrid 改动：确保子段位置正确初始化 ***
            if (EnableSubGridMovement)
            {
                RecreateSubSegments();
                InitializeSubSegmentPositions();

                LoadSpritesFromConfig();
                UpdateAllSegmentSprites();
            }
        }


        /// <summary>
        /// 从配置文件加载图片
        /// </summary>
        void LoadSpritesFromConfig()
        {
            if (Config == null && GameContext.SnakeBodyConfig == null) return;

            if (Config == null)
                Config = GameContext.SnakeBodyConfig;

            // 从配置文件加载图片
            if (Config.VerticalHeadSprite != null)
                VerticalHeadSprite = Config.VerticalHeadSprite;
            if (Config.VerticalTailSprite != null)
                VerticalTailSprite = Config.VerticalTailSprite;
            if (Config.VerticalBodySprite != null)
                VerticalBodySprite = Config.VerticalBodySprite;

        }

        /// <summary>
        /// 更新所有段的图片
        /// </summary>
        public void UpdateAllSegmentSprites()
        {
            if (_subSegments.Count == 0) return;

            if (_subBodyCells == null || _subBodyCells.Count == 0) return;

            // 更新蛇头
            if (_subSegments.Count > 0 && _subSegments[0] != null)
            {
                UpdateHeadSprite();
            }

            // 更新身体段,蛇尾
            UpdateBodySprite();

        }

        /// <summary>
        /// 更新蛇头图片
        /// </summary>
        void UpdateHeadSprite()
        {
            if (_subBodyCells.Count < 2) return;
            if (VerticalHeadSprite == null) return;

            var headCell = _subBodyCells.First.Value;
            var nextCell = _subBodyCells.First.Next.Value;

            var direction = nextCell - headCell;
            var isHorizontal = Mathf.Abs(direction.x) > Mathf.Abs(direction.y);

            var image = _segmentImages[0];
            var rt = _subSegments[0].GetComponent<RectTransform>();

            if (isHorizontal)
            {
                // 水平方向：使用竖直图片并旋转90度
                image.sprite = VerticalHeadSprite;
                if (headCell.x < nextCell.x)
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
        void UpdateBodySprite()
        {
            if (VerticalBodySprite == null) return;
            if (_subBodyCells.Count < 6) return;

            var bodyCellsList = _subBodyCells.ToList();
            for (int segmentIndex = 1; segmentIndex < _subSegments.Count; )
            {
                if (segmentIndex > _subSegments.Count) break;

                var currentCell = bodyCellsList[segmentIndex];
                bool isSameX = true;
                bool isSameY = true;
                for (int step = 1; step < SubGridHelper.SUB_DIV; step++)
                {
                    if (segmentIndex + step < _subSegments.Count)
                    {
                        var nextCell = bodyCellsList[segmentIndex + step];
                        if (isSameX && currentCell.x == nextCell.x)
                        {
                            isSameX = true;
                        }
                        else
                        {
                            isSameX = false;
                        }

                        if (isSameY && currentCell.y == nextCell.y)
                        {
                            isSameY = true;
                        }
                        else
                        {
                            isSameY = false;
                        }
                    }
                }
                if (!isSameX && !isSameY)
                {
                    // 需要计算各个部位的角度，已22.5度分隔
                    // 拐角
                    // 计算角度：
                    if (segmentIndex < _subSegments.Count - 5)
                    {
                        //全部在大格里

                        Vector2Int[] fivecells = new Vector2Int[5];
                        for (int step = 0; step < SubGridHelper.SUB_DIV; step++)
                        {
                            fivecells[step] = bodyCellsList[segmentIndex + step];
                        }

                        var startAngle = 0;
                        var offsetAngle = -SubRotationAngle;
                        var dir = GetFiveSubTurnDirection8(fivecells);
                        if(dir == TurnDirection8.RightToUp)
                        {
                            ///*
                            startAngle = 90;
                            offsetAngle = SubRotationAngle;
                        }
                        else if (dir == TurnDirection8.RightToDown)
                        {
                            ///*
                            startAngle = 90;
                            offsetAngle = -SubRotationAngle;
                        }
                        else if (dir == TurnDirection8.LeftToUp)
                        {
                            ///*
                            startAngle = 90;
                            offsetAngle = -SubRotationAngle;
                        }
                        else if (dir == TurnDirection8.LeftToDown)
                        {
                            ///*
                            startAngle = 90;
                            offsetAngle = SubRotationAngle;
                        }
                        else if (dir == TurnDirection8.UpToRight)
                        {
                            ///*
                            startAngle = 0;
                            offsetAngle = -SubRotationAngle;
                        }
                        else if (dir == TurnDirection8.UpToLeft)
                        {
                            ///*
                            startAngle = 0;
                            offsetAngle = SubRotationAngle;
                        }
                        else if (dir == TurnDirection8.DownToRight)
                        {
                            ///*
                            startAngle = 0;
                            offsetAngle = SubRotationAngle;
                        }
                        else if (dir == TurnDirection8.DownToLeft)
                        {
                            ///*
                            startAngle = 0;
                            offsetAngle = -SubRotationAngle;
                        }
                        if(segmentIndex == 1)
                        {
                            Debug.Log("1=========dir ==> " + dir);
                        }

                        for (int step = 0; step < SubGridHelper.SUB_DIV; step++)
                        {
                            if (segmentIndex + step >= _subSegments.Count) break;

                            var image = _segmentImages[segmentIndex + step];
                            var rt = _subSegments[segmentIndex + step].GetComponent<RectTransform>();

                            if (segmentIndex + step == _subSegments.Count - 1)
                            {
                                //蛇尾
                                image.sprite = VerticalTailSprite;
                            }
                            else
                                image.sprite = VerticalBodySprite;


                            rt.rotation = Quaternion.Euler(0, 0, startAngle + step * offsetAngle);

                        }

                    }
                    else
                    {
                        //不够五小段
                        var lastSegmentsCount = _subSegments.Count - segmentIndex;
                        Vector2Int[] fivecells = new Vector2Int[lastSegmentsCount];
                        for (int step = 0; step < lastSegmentsCount; step++)
                        {
                            fivecells[step] = bodyCellsList[segmentIndex + step];
                        }

                        var startAngle = 0;
                        var offsetAngle = -SubRotationAngle;
                        var dir = GetFiveSubTurnDirection8(fivecells);
                        if (dir == TurnDirection8.RightToUp)
                        {
                            ///*
                            startAngle = 90;
                            offsetAngle = SubRotationAngle;
                        }
                        else if (dir == TurnDirection8.RightToDown)
                        {
                            ///*
                            startAngle = 90;
                            offsetAngle = -SubRotationAngle;
                        }
                        else if (dir == TurnDirection8.LeftToUp)
                        {
                            ///*
                            startAngle = 90;
                            offsetAngle = -SubRotationAngle;
                        }
                        else if (dir == TurnDirection8.LeftToDown)
                        {
                            ///*
                            startAngle = 90;
                            offsetAngle = SubRotationAngle;
                        }
                        else if (dir == TurnDirection8.UpToRight)
                        {
                            ///*
                            startAngle = 0;
                            offsetAngle = -SubRotationAngle;
                        }
                        else if (dir == TurnDirection8.UpToLeft)
                        {
                            ///*
                            startAngle = 0;
                            offsetAngle = SubRotationAngle;
                        }
                        else if (dir == TurnDirection8.DownToRight)
                        {
                            ///*
                            startAngle = 0;
                            offsetAngle = SubRotationAngle;
                        }
                        else if (dir == TurnDirection8.DownToLeft)
                        {
                            ///*
                            startAngle = 0;
                            offsetAngle = -SubRotationAngle;
                        }
        

                        for (int step = 0; step < lastSegmentsCount; step++)
                        {
                            if (segmentIndex + step >= _subSegments.Count) break;

                            var image = _segmentImages[segmentIndex + step];
                            var rt = _subSegments[segmentIndex + step].GetComponent<RectTransform>();

                            if (segmentIndex + step == _subSegments.Count - 1)
                            {
                                //蛇尾
                                image.sprite = VerticalTailSprite;
                            }
                            else
                                image.sprite = VerticalBodySprite;


                            rt.rotation = Quaternion.Euler(0, 0, startAngle + step * offsetAngle);

                        }

                    }


                    
                }
                else if (isSameX)
                {
                    // 五小段均为直线：使用竖直身体图片
                    for (int step = 0; step < SubGridHelper.SUB_DIV; step++)
                    {
                        if(segmentIndex + step >= _subSegments.Count) break;

                        var image = _segmentImages[segmentIndex + step];
                        var rt = _subSegments[segmentIndex + step].GetComponent<RectTransform>();

                        if(segmentIndex + step == _subSegments.Count - 1)
                        {
                            //蛇尾
                            image.sprite = VerticalTailSprite;
                        }else
                            image.sprite = VerticalBodySprite;

                        // 水平方向：
                        rt.rotation = Quaternion.identity; 
                    }
                }
                else if (isSameY)
                {
                    // 五小段均为直线：使用竖直身体图片旋转90度
                    for (int step = 0; step < SubGridHelper.SUB_DIV; step++)
                    {
                        if (segmentIndex + step >= _subSegments.Count) break;

                        var image = _segmentImages[segmentIndex + step];
                        var rt = _subSegments[segmentIndex + step].GetComponent<RectTransform>();

                        if (segmentIndex + step == _subSegments.Count - 1)
                        {
                            //蛇尾
                            image.sprite = VerticalTailSprite;
                        }
                        else
                            image.sprite = VerticalBodySprite;

                        // 水平方向：旋转90度
                        rt.rotation = Quaternion.Euler(0, 0, RotationAngle);
                    }
                }


                segmentIndex += 5;
            }

            
        }

        public enum TurnDirection8
        {
            RightToUp,    // → 再 ↑
            RightToDown,  // → 再 ↓
            LeftToUp,     // ← 再 ↑
            LeftToDown,   // ← 再 ↓
            UpToRight,    // ↑ 再 →
            UpToLeft,     // ↑ 再 ←
            DownToRight,  // ↓ 再 →
            DownToLeft    // ↓ 再 ←
        }

        /// <summary>
        /// 计算5小段整体的转弯方向（八方向）
        /// fiveSubCells 为该段5个小格，按身体顺序（头->尾或尾->头）连续排列
        /// 仅在已判定转弯（!isSameX && !isSameY）时调用
        /// </summary>
        public static TurnDirection8 GetFiveSubTurnDirection8(IReadOnlyList<Vector2Int> fiveSubCells)
        {
            Vector2Int dirIn = Vector2Int.zero, dirOut = Vector2Int.zero;

            // 入口方向：从头开始找第一个非零步进
            for (int i = 1; i < fiveSubCells.Count; i++)
            {
                var d = fiveSubCells[i] - fiveSubCells[i - 1];
                if (d.x != 0 || d.y != 0)
                {
                    dirIn = new Vector2Int(Mathf.Clamp(d.x, -1, 1), Mathf.Clamp(d.y, -1, 1));
                    break;
                }
            }

            // 出口方向：从尾开始找第一个非零步进
            for (int i = fiveSubCells.Count - 1; i >= 1; i--)
            {
                var d = fiveSubCells[i] - fiveSubCells[i - 1];
                if (d.x != 0 || d.y != 0)
                {
                    dirOut = new Vector2Int(Mathf.Clamp(d.x, -1, 1), Mathf.Clamp(d.y, -1, 1));
                    break;
                }
            }

            // 兜底
            if (dirIn == Vector2Int.zero || dirOut == Vector2Int.zero)
                return TurnDirection8.RightToUp;

            // 入口水平 + 出口垂直
            if (dirIn.x != 0 && dirOut.y != 0)
            {
                if (dirIn.x > 0 && dirOut.y > 0) return TurnDirection8.RightToUp;
                if (dirIn.x > 0 && dirOut.y < 0) return TurnDirection8.RightToDown;
                if (dirIn.x < 0 && dirOut.y > 0) return TurnDirection8.LeftToUp;
                return TurnDirection8.LeftToDown;
            }

            // 入口垂直 + 出口水平
            if (dirIn.y != 0 && dirOut.x != 0)
            {
                if (dirIn.y > 0 && dirOut.x > 0) return TurnDirection8.UpToRight;
                if (dirIn.y > 0 && dirOut.x < 0) return TurnDirection8.UpToLeft;
                if (dirIn.y < 0 && dirOut.x > 0) return TurnDirection8.DownToRight;
                return TurnDirection8.DownToLeft;
            }

            return TurnDirection8.RightToUp;
        }

        public override void UpdateGridConfig(GridConfig newGrid)
        {
            _grid = newGrid;

            // 更新小段的大小
            if (EnableSubGridMovement)
            {
                float subSegmentSize = _grid.CellSize / SubGridHelper.SUB_DIV;
                for (int i = 0; i < _subSegments.Count; i++)
                {
                    var rt = _subSegments[i].GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.sizeDelta = new Vector2(_grid.CellSize, subSegmentSize);
                    }
                }
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

            // 如果没有预制体，创建一个基本的Image对象
            var go = new GameObject("SubSegment");
            go.transform.SetParent(transform);
            var image = go.AddComponent<Image>();
            image.sprite = BodySprite;
            image.color = BodyColor;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(_grid.CellSize * 0.8f, SubGridHelper.SUB_CELL_SIZE * _grid.CellSize); // 转换为UI单位
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
            foreach (var subSeg in _subSegments)
            {
                ReturnSubSegmentToPool(subSeg);
            }
            _subSegments.Clear();


            _cachedSubRectTransforms.Clear();
            RectTransform rt = null;
            Image img = null;
            for (int segmentIndex = 0; segmentIndex < Mathf.Max(1, Length); segmentIndex++)
            {
                var subSegmentList = new List<GameObject>();
                for (int i = 0; i < SubGridHelper.SUB_DIV; i++)
                {
                    var subSegment = GetSubSegmentFromPool();
                    subSegment.name = ($"SubSegment_{segmentIndex}_{i}");
                    subSegmentList.Add(subSegment);
                    rt = subSegment.GetComponent<RectTransform>();
                    img = subSegment.GetComponent<Image>();
                    _cachedSubRectTransforms.Add(rt);
                    _segmentImages.Add(img);
                    _subSegments.Add(subSegment);
                }
      

            }
            
        }

        /// <summary>
        /// 初始化所有子段的位置
        /// </summary>
        void InitializeSubSegmentPositions()
        {
            if (!EnableSubGridMovement)
                return;

            // 为每个身体节点初始化5个子段的位置
            var bodyCells = InitialBodyCells;
            if (bodyCells == null) return;

            var grid = GetGrid();

            _subBodyCells.Clear();

            int segmentIndex = 0;
            foreach (var bigCell in bodyCells)
            {
                // 初始化时，所有子段都居中对齐到大格中心
                var bigCellFirstSub = SubGridHelper.BigCellToFirstSubCell(bigCell);

                Vector2Int[] subCellPositions = new Vector2Int[SubGridHelper.SUB_DIV];
                for (int i = 0; i < SubGridHelper.SUB_DIV; i++)
                {
                    // 初始状态：所有子段沿Y轴排列，居中对齐
                    subCellPositions[i] = new Vector2Int(
                        bigCellFirstSub.x,
                        bigCellFirstSub.y + i
                    );

                    // 同步到链表与可视
                    _subBodyCells.AddLast(subCellPositions[i]);
                }

                // 更新子段位置
                UpdateSubSegmentPositions(segmentIndex, subCellPositions);


                segmentIndex++;
            }

            _currentHeadSubCell = _subBodyCells.First.Value;
            _currentTailSubCell = _subBodyCells.Last.Value;
            _lastSampledSubCell = _currentHeadSubCell;

        }


        /// <summary>
        /// 更新子段位置（由SnakeController调用）
        /// </summary>
        /// <param name="segmentIndex">身体节点索引</param>
        /// <param name="subCellPositions">5个子段的小格坐标</param>
        public void UpdateSubSegmentPositions(int segmentIndex, Vector2Int[] subCellPositions)
        {
            if (!EnableSubGridMovement)
                return;

            var grid = GetGrid();

            for (int i = 0; i < subCellPositions.Length; i++)
            {
                int curSubIndex = segmentIndex * SubGridHelper.SUB_DIV + i;
                if(curSubIndex < _subSegments.Count)
                {
                    var cell = subCellPositions[i];
                    var worldPos = SubGridHelper.SubCellToWorld(cell, _grid);

                    var rt = _subSegments[curSubIndex].GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchoredPosition = new Vector2(worldPos.x, worldPos.y);
                        rt.rotation = Quaternion.Euler(0, 0, 0f);
                    }
                }
                
            }

        }


        void Update()
        {
            if (IsControllable)
            {
                HandleInput();
            }
        }

        public override void UpdateMovement()
        {

            // 如果蛇已被完全消除或组件被销毁，停止所有移动更新
            if (_subBodyCells.Count == 0 || !IsAlive() || _cachedSubRectTransforms.Count == 0)
            {
                return;
            }

            if (!_dragging)
                return;

            if (_consuming)
            {
                // 清理已销毁的组件
                CleanupCachedComponents();
                return;
            }

            UpdateSubGridMovement();
        }

        // *** SubGrid 改动：新的小格移动逻辑 ***
        void UpdateSubGridMovement()
        {
            // 采样当前手指所在小格，扩充小格路径队列
            var world = ScreenToWorld(Input.mousePosition);
            var targetSubCell = SubGridHelper.WorldToSubCell(world, _grid);
            if (targetSubCell != _lastSampledSubCell)
            {
                // 更新主方向：按更大位移轴确定
                var delta = targetSubCell - (_dragFromHead ? _currentHeadSubCell : _currentTailSubCell);
                _dragAxis = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) ? DragAxis.X : DragAxis.Y;

                EnqueueSubCellPath(_lastSampledSubCell, targetSubCell, _subCellPathQueue);
                _lastSampledSubCell = targetSubCell;
            }



            // 处理小格移动
            if (_subCellPathQueue.Count > 0)
            {
                float subCellSpeed = MoveSpeedSubCellsPerSecond;
                _subCellMoveAccumulator += subCellSpeed * Time.deltaTime;

                var nextSubCell = _subCellPathQueue.First.Value;

                // 检查目标点合法性
                if (!CheckNextCell(nextSubCell))
                {
                    return;
                }

                if (_dragFromHead)
                {
                    AdvanceHeadToSubCell(nextSubCell);
                }
                else
                {
                    AdvanceTailToSubCell(nextSubCell);
                }


                // 洞检测：若拖动端在洞位置或临近洞，且颜色匹配，触发吞噬todo
                var currentBigCell = _dragFromHead ? _currentHeadCell : _currentTailCell;
                var hole = FindHoleAtOrAdjacent(currentBigCell);
                if (hole != null && hole.CanInteractWithSnake(this))
                {
                    _consumeCoroutine ??= StartCoroutine(CoConsume(hole, _dragFromHead));
                    return;
                }

                //更新身体图片
                UpdateAllSegmentSprites();
            }
        }


        bool CheckNextCell(Vector2Int nextSubCell)
        {

            if (!EnableSubGridMovement || _cachedSubRectTransforms.Count == 0 || _subBodyCells.Count == 0 || _subCellPathQueue.Count == 0)
                return false;

            Vector2Int curCheckSubCell = _currentHeadSubCell;
            if (!_dragFromHead)
            {
                curCheckSubCell = _currentTailSubCell;
            }


            Vector2Int nextBigCell = SubGridHelper.SubCellToBigCell(nextSubCell);
            var curHeadBigCell = SubGridHelper.SubCellToBigCell(curCheckSubCell);
            if(nextBigCell == curHeadBigCell)
            {
                //当前大格默认是可行走
                return true;
            }

            // 必须相邻
            if (Manhattan(curHeadBigCell, nextBigCell) != 1) return false;
            // 检查网格边界
            if (!_grid.IsInside(nextBigCell)) return false;
            // 使用与IsPathBlocked相同的阻挡检测逻辑，支持颜色匹配
            if (IsPathBlocked(nextBigCell)) return false;
            // 占用校验：允许进入原尾 todo
            //var tailCell = _subBodyCells.Last.Value;
            //if (IsOccupiedBySelf(nextBigCell) && nextBigCell != tailCell) return false;

            return true;
        }

        void EnqueueSubCellPath(Vector2Int fromSubCell, Vector2Int toSubCell, LinkedList<Vector2Int> pathList)
        {
            if (pathList == null) return;
            pathList.Clear();
            if (fromSubCell == toSubCell) return;

            // 1) 参数校验：必须在中线上
            bool FromOnCenter = SubGridHelper.IsValidSubCell(fromSubCell);
            bool ToOnCenter = SubGridHelper.IsValidSubCell(toSubCell);
            if (!FromOnCenter || !ToOnCenter)
            {
                Debug.LogError($"EnqueueSubCellPath: 参数不在中线 from={fromSubCell}, to={toSubCell}");
                return;
            }

            // 2) 最短中线路径（仅在 x%5==2 的竖线和 y%5==2 的横线行走）
            // 核心思想：最多一次转向（L型），必要时两段直线组合；全程在中线网格上，长度等于曼哈顿距离。
            Vector2Int cur = fromSubCell;

            bool fromOnVertical = (fromSubCell.x % SubGridHelper.SUB_DIV) == SubGridHelper.CENTER_INDEX;
            bool fromOnHorizontal = (fromSubCell.y % SubGridHelper.SUB_DIV) == SubGridHelper.CENTER_INDEX;
            bool toOnVertical = (toSubCell.x % SubGridHelper.SUB_DIV) == SubGridHelper.CENTER_INDEX;
            bool toOnHorizontal = (toSubCell.y % SubGridHelper.SUB_DIV) == SubGridHelper.CENTER_INDEX;

            // 局部函数：按轴追加（包含终点），逐格入队，保证连续性与合法性
            void AppendVertical(int yTarget)
            {
                int step = yTarget > cur.y ? 1 : -1;
                while (cur.y != yTarget)
                {
                    var next = new Vector2Int(cur.x, cur.y + step);
                    // 安全校验（理论上总为真）
                    if (!SubGridHelper.IsValidSubCell(next) || !SubGridHelper.IsSubCellInside(next, _grid))
                    {
                        Debug.LogError($"EnqueueSubCellPath: 竖向越界/非法 next={next}");
                        break;
                    }
                    pathList.AddLast(next);
                    cur = next;
                }
            }
            void AppendHorizontal(int xTarget)
            {
                int step = xTarget > cur.x ? 1 : -1;
                while (cur.x != xTarget)
                {
                    var next = new Vector2Int(cur.x + step, cur.y);
                    // 安全校验（理论上总为真）
                    if (!SubGridHelper.IsValidSubCell(next) || !SubGridHelper.IsSubCellInside(next, _grid))
                    {
                        Debug.LogError($"EnqueueSubCellPath: 横向越界/非法 next={next}");
                        break;
                    }
                    pathList.AddLast(next);
                    cur = next;
                }
            }

            // 同线直走（无需拐弯）
            if (fromSubCell.x == toSubCell.x && fromOnVertical && toOnVertical)
            {
                AppendVertical(toSubCell.y);
            }
            else if (fromSubCell.y == toSubCell.y && fromOnHorizontal && toOnHorizontal)
            {
                AppendHorizontal(toSubCell.x);
            }
            else
            {
                // 需要一次转向：
                // 情况A：起点在竖线 → 先竖后横（若终点在竖线，再补一段竖）
                if (fromOnVertical)
                {
                    // 选用的横线：若终点在横线，则用终点所在横线；否则用终点所在大格的中横线
                    int yPivot = toOnHorizontal ? toSubCell.y : (toSubCell.y / SubGridHelper.SUB_DIV) * SubGridHelper.SUB_DIV + SubGridHelper.CENTER_INDEX;
                    AppendVertical(yPivot);
                    // 横向走到终点所在竖线（无论终点在横线或竖线，都直接走到 to.x）
                    AppendHorizontal(toSubCell.x);
                    // 若终点在竖线，还需竖向对齐 y
                    if (toOnVertical && cur != toSubCell)
                        AppendVertical(toSubCell.y);
                }
                // 情况B：起点在横线 → 先横后竖（若终点在横线，再补一段横）
                else if (fromOnHorizontal)
                {
                    // 选用的竖线：若终点在竖线，则用终点所在竖线；否则用终点所在大格的中竖线
                    int xPivot = toOnVertical ? toSubCell.x : (toSubCell.x / SubGridHelper.SUB_DIV) * SubGridHelper.SUB_DIV + SubGridHelper.CENTER_INDEX;
                    AppendHorizontal(xPivot);
                    // 竖向走到终点所在横线（无论终点在横线或竖线，都直接走到 to.y）
                    AppendVertical(toSubCell.y);
                    // 若终点在横线，还需横向对齐 x
                    if (toOnHorizontal && cur != toSubCell)
                        AppendHorizontal(toSubCell.x);
                }
                else
                {
                    // 理论上不会到这：from 不在任何中线（已在前面校验过）
                    Debug.LogError($"EnqueueSubCellPath: 起点不在中线 from={fromSubCell}");
                }
            }

            // 3) 返回列表不包含终点（与示例一致）
            //if (pathList.Count > 0 && pathList.Last.Value == toSubCell)
            //    pathList.RemoveLast();

        }
        bool AdvanceHeadToSubCell(Vector2Int nextSubCell)
        {
            if (_subCellPathQueue.Count == 0)
            {
                Debug.LogWarning("SubCell path queue is empty");
                return false;
            }

            // 1) 取出首元素作为新头
            var newHeadSubCell = _subCellPathQueue.First.Value;
            _subCellPathQueue.RemoveFirst();

            // 2) 需要能够访问原身体第六格（索引5）
            if (_subBodyCells.Count < 6)
            {
                Debug.LogWarning("Snake body is too short (need at least 6 sub-cells).");
                return false;
            }

            var sixthBodyCell = GetSubBodyCellAtIndex(5);
            if (sixthBodyCell == Vector2Int.zero)
            {
                Debug.LogError("Failed to get sixth body cell");
                return false;
            }

            // 3) 计算新头到原第六格的最短中线路径（列表不含终点，正好可取前4步）
            _headCellToBodyPathQueue.Clear();
            EnqueueSubCellPath(newHeadSubCell, sixthBodyCell, _headCellToBodyPathQueue);

            // 4) 原地更新_subBodyCells：
            //    - 前5格：newHead + 路径前4步（路径不足则重复上一步以保持连续）
            //    - 其余格：从尾到索引5逐个赋值为前一格，实现“向前趋近一格”
            // 更新前5格
            var node = _subBodyCells.First;
            node.Value = newHeadSubCell;

            int filled = 1;
            var p = _headCellToBodyPathQueue.First;
            
            while (filled < 5 && node != null)
            {
                node = node.Next;
                if (node == null) break;

                if (p != null)
                {
                    node.Value = p.Value;
                    p = p.Next;
                }
                else
                {
                    // 路径不足4步时，重复前一格，保证连续
                    node.Value = node.Previous.Value;
                }
                filled++;
            }

            if(_headCellToBodyPathQueue.Count >= 5)
            {
                var newBody = _headCellToBodyPathQueue.ElementAt(4);

                InsertAtPosition(_subBodyCells, 4, newBody);
                _subBodyCells.RemoveLast();
            }


            // 5) 刷新缓存
            _currentHeadSubCell = _subBodyCells.First.Value;
            _currentTailSubCell = _subBodyCells.Last.Value;
            _currentHeadCell = SubGridHelper.SubCellToBigCell(_currentHeadSubCell);
            _currentTailCell = SubGridHelper.SubCellToBigCell(_currentTailSubCell);
            UpdateCachedSubRectTransformsFromSubBodyCells();

            return true;
        }
        /// <summary>
        /// 获取指定索引的身体格子
        /// </summary>
        private Vector2Int GetSubBodyCellAtIndex(int index)
        {
            if (index >= _subBodyCells.Count || index < 0)
                return Vector2Int.zero;

            var node = _subBodyCells.First;
            for (int i = 0; i < index && node != null; i++)
            {
                node = node.Next;
            }
            return node?.Value ?? Vector2Int.zero;
        }

        // 在指定位置插入新元素的方法
        public static void InsertAtPosition(LinkedList<Vector2Int> list, int position, Vector2Int newValue)
        {
            if (list == null)
                throw new System.ArgumentNullException(nameof(list));

            if (position < 1 || position > list.Count + 1)
                throw new System.ArgumentOutOfRangeException(nameof(position), "位置超出范围");

            // 如果插入位置是开头
            if (position == 1)
            {
                list.AddFirst(newValue);
                return;
            }

            // 如果插入位置是末尾
            if (position == list.Count + 1)
            {
                list.AddLast(newValue);
                return;
            }

            // 找到位置前的节点（第 position-1 个节点）
            LinkedListNode<Vector2Int> currentNode = list.First;
            for (int i = 1; i < position - 1; i++)
            {
                currentNode = currentNode.Next;
            }

            // 在当前节点后插入新元素
            list.AddAfter(currentNode, newValue);
        }


        /// <summary>
        /// 更新身体格子列表：前五格使用新路径，其余向前趋近一格
        /// </summary>
        private void UpdateSubBodyCellsWithNewPath(Vector2Int newHeadSubCell, LinkedList<Vector2Int> headToFifthPath)
        {
            // 将LinkedList转换为数组进行批量操作（性能优化）
            var bodyArray = new Vector2Int[_subBodyCells.Count];
            int index = 0;
            foreach (var cell in _subBodyCells)
            {
                bodyArray[index++] = cell;
            }

            // 更新前五格：新头部 + 计算出的四格路径
            bodyArray[0] = newHeadSubCell; // 新头部

            int pathIndex = 1;
            var pathNode = headToFifthPath.First;
            while (pathNode != null && pathIndex < 5)
            {
                bodyArray[pathIndex] = pathNode.Value;
                pathNode = pathNode.Next;
                pathIndex++;
            }

            // 其余身体向前趋近一格：从索引5开始，每个位置使用前一个位置的值
            for (int i = 5; i < bodyArray.Length - 1; i++)
            {
                bodyArray[i] = bodyArray[i - 1];
            }

            // 重建LinkedList（批量操作，性能更好）
            _subBodyCells.Clear();
            for (int i = 0; i < bodyArray.Length - 1; i++) // 移除最后一格（尾部前进）
            {
                _subBodyCells.AddLast(bodyArray[i]);
            }
        }

        /// <summary>
        /// 根据_subBodyCells更新_cachedSubRectTransforms
        /// </summary>
        private void UpdateCachedSubRectTransformsFromSubBodyCells()
        {
            if (!EnableSubGridMovement || _cachedSubRectTransforms.Count == 0 || _subBodyCells.Count == 0)
                return;

            // 遍历身体节点和对应的RectTransform
            for (int segmentIndex = 0; segmentIndex < _cachedSubRectTransforms.Count; segmentIndex++)
            {
                var rt = _cachedSubRectTransforms[segmentIndex];
                if (rt != null)
                {
                    var subCell = _subBodyCells.ElementAt(segmentIndex);
                    var worldPos = SubGridHelper.SubCellToWorld(subCell, _grid);

                    rt.anchoredPosition = new Vector2(worldPos.x, worldPos.y);
                }

            }

        }

        // *** SubGrid 改动：小格尾部移动方法 ***
        bool AdvanceTailToSubCell(Vector2Int nextSubCell)
        {
            if (_subCellPathQueue.Count == 0)
            {
                Debug.LogWarning("SubCell path queue is empty");
                return false;
            }

            
            // 1) 取出首元素作为新头
            var newHeadSubCell = _subCellPathQueue.First.Value;
            _subCellPathQueue.RemoveFirst();

            // 2) 需要能够访问原身体第六格（索引5）
            if (_subBodyCells.Count < 6)
            {
                Debug.LogWarning("Snake body is too short (need at least 6 sub-cells).");
                return false;
            }

            var sixthBodyCell = GetSubBodyCellAtIndex(_subBodyCells.Count - 1 - 5);
            if (sixthBodyCell == Vector2Int.zero)
            {
                Debug.LogError("Failed to get sixth body cell");
                return false;
            }

            // 3) 计算新头到原第六格的最短中线路径（列表不含终点，正好可取前4步）
            _headCellToBodyPathQueue.Clear();
            EnqueueSubCellPath(newHeadSubCell, sixthBodyCell, _headCellToBodyPathQueue);

            // 4) 原地更新_subBodyCells：
            //    - 前5格：newHead + 路径前4步（路径不足则重复上一步以保持连续）
            //    - 其余格：从尾到索引5逐个赋值为前一格，实现“向前趋近一格”
            // 更新前5格
            var node = _subBodyCells.Last;
            node.Value = newHeadSubCell;

            int filled = 1;
            var p = _headCellToBodyPathQueue.First;

            while (filled < 5 && node != null)
            {
                node = node.Previous;
                if (node == null) break;

                if (p != null)
                {
                    node.Value = p.Value;
                    p = p.Next;
                }
                else
                {
                    // 路径不足4步时，重复前一格，保证连续
                    node.Value = node.Next.Value;
                }
                filled++;
            }

            if (_headCellToBodyPathQueue.Count >= 5)
            {
                var newBody = _headCellToBodyPathQueue.ElementAt(4);

                InsertAtPosition(_subBodyCells, _subBodyCells.Count - 4, newBody);
                _subBodyCells.RemoveFirst();
            }


            // 5) 刷新缓存
            _currentHeadSubCell = _subBodyCells.First.Value;
            _currentTailSubCell = _subBodyCells.Last.Value;
            _currentHeadCell = SubGridHelper.SubCellToBigCell(_currentHeadSubCell);
            _currentTailCell = SubGridHelper.SubCellToBigCell(_currentTailSubCell);
            UpdateCachedSubRectTransformsFromSubBodyCells();


            return true;
        }

        // *** SubGrid 改动：获取Grid配置的公共方法 ***
        public GridConfig GetGrid()
        {
            return _grid;
        }
        void TestSnapSubCellsToGrid()
        {
            //var subcelllist = _subBodyCells[0];
            //
            //subcelllist.Clear();
            //subcelllist.AddLast(new Vector2Int(4, 2));
            //subcelllist.AddLast(new Vector2Int(3, 2));
            //subcelllist.AddLast(new Vector2Int(2, 2));
            //subcelllist.AddLast(new Vector2Int(2, 3));
            //subcelllist.AddLast(new Vector2Int(2, 4));

            /*
             * if(i == 0)
                        {
                            if (j == 0)
                            {
                                rt.rotation = Quaternion.Euler(0, 0, 90);
                            }
                            if (j == 1)
                            {
                                rt.rotation = Quaternion.Euler(0, 0, 67.5f);
                            }
                            if (j == 2)
                            {
                                rt.rotation = Quaternion.Euler(0, 0, 45);
                            }
                            if (j == 3)
                            {
                                rt.rotation = Quaternion.Euler(0, 0, 22.5f);
                            }
                        }
             * */
            SnapSubCellsToGrid();
        }
        // *** SubGrid 改动：小格对齐到网格中心 ***
        void SnapSubCellsToGrid()
        {
            if (_subBodyCells == null)
                return;
            if (_subBodyCells.Count == 0)
                return;


            int i = 0;
            foreach (var cell in _subBodyCells)
            {
                if (i >= _subSegments.Count) break;

                var rt = _subSegments[i].GetComponent<RectTransform>();
                if (rt != null)
                {
                    var worldPos = SubGridHelper.SubCellToWorld(cell, _grid);
                    rt.anchoredPosition = new Vector2(worldPos.x, worldPos.y);

                }

                i++;
            }


            _currentHeadSubCell = _subBodyCells.First.Value;
            _currentTailSubCell = _subBodyCells.Last.Value;
        }


        void HandleInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (_lastMousePos != Input.mousePosition)
                {
                    _lastMousePos = Input.mousePosition;

                    var world = ScreenToWorld(Input.mousePosition);
                    if (TryPickHeadOrTail(world, out _dragFromHead))
                    {
                        _dragging = true;

                        // *** SubGrid 改动：初始化小格拖拽 ***
                        if (EnableSubGridMovement)
                        {
                            _subCellPathQueue.Clear();
                            _lastSampledSubCell = _dragFromHead ? _currentHeadSubCell : _currentTailSubCell;
                            _subCellMoveAccumulator = 0f;
                        }

                        _dragAxis = DragAxis.None;
                    }
                }

            }
            else if (Input.GetMouseButtonUp(0))
            {
                if (_dragging)
                {
                    // 手指松开时，记录最终路径并移动到目标位置
                    //RecordFinalPathOnRelease();

                    //TestSnapSubCellsToGrid();
                    if (_startMove)
                    {
                        // *** SubGrid 改动：小格或大格对齐 ***
                        if (EnableSubGridMovement)
                        {
                            _subCellPathQueue.Clear();
                            SnapSubCellsToGrid();
                        }
                        _startMove = false;
                    }

                }
                _dragging = false;
                _dragAxis = DragAxis.None;
                _lastMousePos = Vector3.zero;
            }
        }


        HoleEntity FindAdjacentHole(Vector2Int from)
        {
            // 简化：全局搜寻场景中的洞实体，找到与from相邻的第一个
            var holes = Object.FindObjectsOfType<HoleEntity>();
            for (int i = 0; i < holes.Length; i++)
            {
                if (holes[i].IsAdjacent(from)) return holes[i];
            }
            return null;
        }

        /// <summary>
        /// 查找目标位置本身或邻近位置的洞
        /// </summary>
        HoleEntity FindHoleAtOrAdjacent(Vector2Int targetCell)
        {
            var holes = Object.FindObjectsOfType<HoleEntity>();
            for (int i = 0; i < holes.Length; i++)
            {
                // 检查目标位置本身是否是洞的位置
                if (holes[i].Cell == targetCell)
                {
                    return holes[i];
                }
                // 检查目标位置是否邻近洞
                if (holes[i].IsAdjacent(targetCell))
                {
                    return holes[i];
                }
            }
            return null;
        }
        public IEnumerator CoConsume(HoleEntity hole, bool fromHead)
        {
            _consuming = true;
            _dragging = false; // 脱离手指控制
            Vector3 holeCenter = _grid.CellToWorld(hole.Cell);

            LinkedList<GameObject> allegments = new LinkedList<GameObject>();

            foreach (var gameObject in _subSegments)
            {
                if (gameObject != null)
                    allegments.AddLast(gameObject);
            }

            // 逐段进入洞并消失，保持身体连续性
            while (_subBodyCells.Count > 0)
            {
                Transform segmentToConsume = null;
                Vector2Int consumedCell;

                if (fromHead)
                {
                    consumedCell = _subBodyCells.First.Value;
                    _subBodyCells.RemoveFirst();
                    if (allegments.Count > 0)
                    {
                        segmentToConsume = allegments.First.Value.transform;
                        allegments.RemoveFirst();
                    }
                }
                else
                {
                    consumedCell = _subBodyCells.Last.Value;
                    _subBodyCells.RemoveLast();
                    int last = allegments.Count - 1;
                    if (last >= 0)
                    {
                        segmentToConsume = allegments.Last.Value.transform;
                        allegments.RemoveLast();
                    }
                }

                // 更新当前头尾缓存，防止空引用
                if (_subBodyCells.Count > 0)
                {
                    _currentHeadSubCell = _subBodyCells.First.Value;
                    _currentTailSubCell = _subBodyCells.Last.Value;
                }

                // 启动消费动画和身体跟随移动
                if (segmentToConsume != null)
                {
                    var consumeCoroutine = StartCoroutine(MoveToHoleAndDestroy(segmentToConsume, holeCenter, hole.ConsumeInterval * 0.8f));
                    var followCoroutine = StartCoroutine(MoveRemainingBodyTowardHole(consumedCell, hole.Cell, hole.ConsumeInterval * 0.8f, fromHead));

                    // 等待消费完成
                    yield return consumeCoroutine;
                }
                else
                {
                    yield return new WaitForSeconds(hole.ConsumeInterval);
                }
            }

            _consuming = false;
            _consumeCoroutine = null;

            // 全部消失后，销毁蛇对象或重生；此处直接销毁
            if (_subBodyCells.Count == 0)
            {
                Destroy(gameObject);
            }
        }
        IEnumerator MoveToHoleAndDestroy(Transform segment, Vector3 holeCenter, float duration)
        {
            Vector3 startPos;
            RectTransform segmentRT = null;

            segmentRT = segment.GetComponent<RectTransform>();
            if (segmentRT != null)
            {
                startPos = new Vector3(segmentRT.anchoredPosition.x, segmentRT.anchoredPosition.y, 0);
            }
            else
            {
                startPos = segment.position;
            }
            // 洞中心也需要转换为UI坐标
            holeCenter = new Vector3(holeCenter.x, holeCenter.y, 0);

            // 计算沿身体路径到洞的移动路径
            List<Vector3> pathToHole = CalculatePathToHole(startPos, holeCenter);

            // 沿路径移动
            float totalDistance = 0f;
            for (int i = 0; i < pathToHole.Count - 1; i++)
            {
                totalDistance += Vector3.Distance(pathToHole[i], pathToHole[i + 1]);
            }

            float moveSpeed = totalDistance / duration;
            int currentSegment = 0;

            while (currentSegment < pathToHole.Count - 1)
            {
                Vector3 segmentStart = pathToHole[currentSegment];
                Vector3 segmentEnd = pathToHole[currentSegment + 1];
                float segmentLength = Vector3.Distance(segmentStart, segmentEnd);

                float segmentTime = segmentLength / moveSpeed;
                float elapsed = 0f;

                while (elapsed < segmentTime)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / segmentTime;
                    Vector3 currentPos = Vector3.Lerp(segmentStart, segmentEnd, t);

                    segmentRT.anchoredPosition = new Vector2(currentPos.x, currentPos.y);
                    yield return null;
                }

                currentSegment++;
            }

            // 确保到达洞中心
            segmentRT.anchoredPosition = new Vector2(holeCenter.x, holeCenter.y);

            // 消失效果（缩小并淡出）
            var img = segment.GetComponent<UnityEngine.UI.Image>();
            if (img != null)
            {
                float fadeTime = duration * 0.3f;
                float fadeElapsed = 0f;
                Color originalColor = img.color;

                while (fadeElapsed < fadeTime)
                {
                    fadeElapsed += Time.deltaTime;
                    float fadeT = fadeElapsed / fadeTime;
                    img.color = new Color(originalColor.r, originalColor.g, originalColor.b, 1f - fadeT);
                    segment.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, fadeT);
                    yield return null;
                }
            }

            Destroy(segment.gameObject);
        }

        /// <summary>
        /// 让剩余的蛇身朝洞方向移动一格，保持身体连续性
        /// </summary>
        IEnumerator MoveRemainingBodyTowardHole(Vector2Int consumedCell, Vector2Int holeCell, float duration, bool fromHead)
        {
            if (_subBodyCells.Count == 0) yield break;

            Vector2Int direction = Vector2Int.zero;

            if (fromHead)
            {
                // 从头部消费：剩余身体朝被消费的头部位置移动
                Vector2Int currentHeadCell = _subBodyCells.First.Value;
                Vector2Int delta = consumedCell - currentHeadCell;

                if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                {
                    direction = new Vector2Int(delta.x > 0 ? 1 : -1, 0);
                }
                else if (delta.y != 0)
                {
                    direction = new Vector2Int(0, delta.y > 0 ? 1 : -1);
                }

                // 如果有有效方向，让整个蛇身朝那个方向移动一格
                if (direction != Vector2Int.zero)
                {
                    Vector2Int newHeadCell = currentHeadCell + direction;

                    // 检查目标位置是否有效且不被阻挡（除了洞）
                    if (_grid.IsInside(newHeadCell) && (newHeadCell == holeCell || !IsPathBlocked(newHeadCell)))
                    {
                        // 将新位置添加到身体前端
                        _subBodyCells.AddFirst(newHeadCell);

                        // 更新头尾缓存
                        _currentHeadSubCell = _subBodyCells.First.Value;
                        _currentTailSubCell = _subBodyCells.Last.Value;

                        // 平滑移动所有身体段到新位置
                        yield return StartCoroutine(SmoothMoveAllSegments(duration));
                    }
                }
            }
            else
            {
                // 从尾部消费：整条蛇朝被消费的尾部位置移动（类似倒车）
                Vector2Int currentTailCell = _subBodyCells.Last.Value;
                Vector2Int delta = consumedCell - currentTailCell;

                if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                {
                    direction = new Vector2Int(delta.x > 0 ? 1 : -1, 0);
                }
                else if (delta.y != 0)
                {
                    direction = new Vector2Int(0, delta.y > 0 ? 1 : -1);
                }

                // 如果有有效方向，让整个蛇身朝那个方向移动一格
                if (direction != Vector2Int.zero)
                {
                    Vector2Int newTailCell = currentTailCell + direction;

                    // 检查目标位置是否有效且不被阻挡（除了洞）
                    if (_grid.IsInside(newTailCell) && (newTailCell == holeCell || !IsPathBlocked(newTailCell)))
                    {
                        // 整条蛇朝尾部方向移动：在尾部添加新位置，移除头部
                        _subBodyCells.AddLast(newTailCell);
                        _subBodyCells.RemoveFirst();

                        // 更新头尾缓存
                        _currentHeadCell = _subBodyCells.First.Value;
                        _currentTailCell = _subBodyCells.Last.Value;

                        // 平滑移动所有身体段到新位置
                        yield return StartCoroutine(SmoothMoveAllSegments(duration));

                    }
                }
            }
        }

        /// <summary>
        /// 平滑移动所有身体段到对应的新格子位置
        /// </summary>
        IEnumerator SmoothMoveAllSegments(float duration)
        {
            if (_subSegments.Count == 0 || _subBodyCells.Count == 0) yield break;

            List<Vector3> startPositions = new List<Vector3>();
            List<Vector3> targetPositions = new List<Vector3>();


            LinkedList<GameObject> allegments = new LinkedList<GameObject>();

            foreach (var gameObject in _subSegments)
            {
                if (gameObject != null)
                    allegments.AddLast(gameObject);
            }

            // 收集所有段的起始和目标位置
            int segmentIndex = 0;
            foreach (var cell in _subBodyCells)
            {
                if (segmentIndex >= allegments.Count) break;

                Vector3 startPos;
                Vector3 targetPos = _grid.CellToWorld(cell);

                var rt = allegments.ElementAt(segmentIndex).GetComponent<RectTransform>();
                if (rt != null)
                {
                    startPos = new Vector3(rt.anchoredPosition.x, rt.anchoredPosition.y, 0);
                }
                else
                {
                    startPos = allegments.ElementAt(segmentIndex).transform.position;
                }

                startPositions.Add(startPos);
                targetPositions.Add(targetPos);
                segmentIndex++;
            }

            // 平滑移动所有段
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                for (int i = 0; i < startPositions.Count && i < allegments.Count; i++)
                {
                    Vector3 currentPos = Vector3.Lerp(startPositions[i], targetPositions[i], t);

                    var rt = allegments.ElementAt(i).GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchoredPosition = new Vector2(currentPos.x, currentPos.y);
                    }
                }

                yield return null;
            }

            // 确保所有段都到达目标位置
            segmentIndex = 0;
            foreach (var cell in _subBodyCells)
            {
                if (segmentIndex >= allegments.Count) break;

                Vector3 finalPos = _grid.CellToWorld(cell);
                var rt = allegments.ElementAt(segmentIndex).GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchoredPosition = new Vector2(finalPos.x, finalPos.y);
                }
                segmentIndex++;
            }
        }


        List<Vector3> CalculatePathToHole(Vector3 startPos, Vector3 holeCenter)
        {
            List<Vector3> path = new List<Vector3>();
            path.Add(startPos);

            Vector2Int startCell = _grid.WorldToCell(startPos);
            Vector2Int holeCell = _grid.WorldToCell(holeCenter);

            // 简单的A*路径寻找，沿着网格路径到洞
            List<Vector2Int> cellPath = FindPathToHole(startCell, holeCell);

            // 转换为世界坐标
            for (int i = 1; i < cellPath.Count; i++)
            {
                path.Add(_grid.CellToWorld(cellPath[i]));
            }

            // 确保最后一点是洞中心
            if (path[path.Count - 1] != holeCenter)
            {
                path.Add(holeCenter);
            }

            return path;
        }

        List<Vector2Int> FindPathToHole(Vector2Int start, Vector2Int target)
        {
            List<Vector2Int> path = new List<Vector2Int>();
            Vector2Int current = start;
            path.Add(current);

            // 简单的曼哈顿距离路径寻找
            while (current != target)
            {
                Vector2Int next = current;

                // 优先朝目标方向移动
                if (current.x != target.x)
                {
                    next.x += current.x < target.x ? 1 : -1;
                }
                else if (current.y != target.y)
                {
                    next.y += current.y < target.y ? 1 : -1;
                }

                // 检查是否可以移动到该位置
                if (_grid.IsInside(next) && !IsPathBlocked(next))
                {
                    current = next;
                    path.Add(current);
                }
                else
                {
                    // 如果被阻挡，尝试其他方向
                    var directions = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
                    bool found = false;

                    foreach (var dir in directions)
                    {
                        Vector2Int alternative = current + dir;
                        if (_grid.IsInside(alternative) && !IsPathBlocked(alternative) && !path.Contains(alternative))
                        {
                            current = alternative;
                            path.Add(current);
                            found = true;
                            break;
                        }
                    }

                    if (!found) break; // 无法找到路径，直接跳出
                }

                // 防止无限循环
                if (path.Count > 50) break;
            }

            return path;
        }

        bool IsPathBlocked(Vector2Int cell)
        {
            // 检查是否被实体阻挡
            if (_entityManager != null)
            {
                var entities = _entityManager.GetAt(cell);
                if (entities != null)
                {
                    foreach (var entity in entities)
                    {
                        if (entity is WallEntity)
                        {
                            return true; // 墙体总是阻挡
                        }
                        else if (entity is HoleEntity holeEntity)
                        {
                            // 洞的阻挡取决于颜色匹配
                            if (holeEntity.IsBlockingCell(cell, this))
                            {
                                return true; // 颜色不匹配，洞算作阻挡物
                            }
                        }
                    }
                }
            }

            // 检查是否被其他蛇阻挡todo
            //if (_snakeManager != null && _snakeManager.IsCellOccupiedByOtherSnakes(cell, this))
            //{
            //    return true;
            //}

            return false;
        }

        /*
        /// <summary>
        /// 获取增强的蛇头视觉位置（无路径点时使用）
        /// </summary>
        Vector3 GetEnhancedHeadVisual(Vector3 fingerPos, Vector2Int currentHeadCell)
        {
            Vector3 currentHeadWorld = _grid.CellToWorld(currentHeadCell);

            // 计算手指与当前头部的距离和方向
            Vector3 direction = fingerPos - currentHeadWorld;
            float distance = direction.magnitude;

            // 如果距离很小，直接返回当前位置
            if (distance < 0.1f)
            {
                return currentHeadWorld;
            }

            // 检查手指所在的网格是否被阻挡
            Vector2Int fingerCell = ClampInside(_grid.WorldToCell(fingerPos));
            bool fingerCellBlocked = IsPathBlocked(fingerCell);

            // 如果手指位置被阻挡，限制视觉位置在当前格子边界内
            if (fingerCellBlocked)
            {
                // 计算当前格子的边界
                float halfCellSize = _grid.CellSize * 0.5f;
                Vector3 clampedPos = currentHeadWorld;

                // 根据手指方向调整位置，但不超出当前格子
                if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
                {
                    // 主要是水平方向
                    clampedPos.x += Mathf.Sign(direction.x) * halfCellSize * 0.8f;
                }
                else
                {
                    // 主要是垂直方向
                    clampedPos.y += Mathf.Sign(direction.y) * halfCellSize * 0.8f;
                }

                return clampedPos;
            }

            // 手指位置不被阻挡，按原有逻辑计算
            // 限制最大距离以防止过度拉伸
            float maxDistance = _grid.CellSize * 0.4f;
            if (distance > maxDistance)
            {
                direction = direction.normalized * maxDistance;
            }

            // 根据拖动轴限制方向
            Vector3 targetPos = currentHeadWorld + direction;
            if (_dragAxis == DragAxis.X)
            {
                targetPos.y = currentHeadWorld.y; // 只允许水平移动
            }
            else if (_dragAxis == DragAxis.Y)
            {
                targetPos.x = currentHeadWorld.x; // 只允许垂直移动
            }

            return targetPos;
        }

        /// <summary>
        /// 计算身体节点跟随目标位置，确保在主方向上不分离
        /// </summary>
        /// <param name="targetPos">要跟随的目标位置</param>
        /// <param name="currentPos">当前身体节点位置</param>
        /// <param name="followRatio">跟随比例</param>
        /// <returns>身体节点应该移动到的位置</returns>
        Vector3 GetBodyFollowPosition(Vector3 targetPos, Vector3 currentPos, float followRatio = 0.8f)
        {
            Vector3 offset = targetPos - currentPos;
            Vector3 bodyTargetPos = currentPos;

            if (_dragAxis == DragAxis.X)
            {
                // 主方向是X轴，身体在X方向上跟随
                bodyTargetPos.x = currentPos.x + offset.x * followRatio;
            }
            else if (_dragAxis == DragAxis.Y)
            {
                // 主方向是Y轴，身体在Y方向上跟随
                bodyTargetPos.y = currentPos.y + offset.y * followRatio;
            }

            return bodyTargetPos;
        }

        /// <summary>
        /// 计算身体节点在当前格子内跟随蛇头移动时的位置（重载方法）
        /// </summary>
        Vector3 GetBodyFollowPosition(Vector3 headVisual, Vector3 bodyCenter, Vector2Int headCell)
        {
            return GetBodyFollowPosition(headVisual, bodyCenter, 0.8f);
        }

        Vector3 ClampWorldToCellBounds(Vector3 world, Vector2Int cell)
        {
            Vector3 c = _grid.CellToWorld(cell);
            float half = _grid.CellSize * 0.5f;
            world.x = Mathf.Clamp(world.x, c.x - half, c.x + half);
            world.y = Mathf.Clamp(world.y, c.y - half, c.y + half);
            world.z = 0f;
            return world;
        }

        void MoveSnakeToHeadCell(Vector2Int desiredHead)
        {
            if (desiredHead == _currentHeadCell) return;
            if (!IsPathValid(_currentHeadCell, desiredHead)) return;
            _currentHeadCell = desiredHead;
            // 更新身体队列：头插入、新尾移除
            _bodyCells.AddFirst(desiredHead);
            _bodyCells.RemoveLast();
        }

        bool IsPathValid(Vector2Int from, Vector2Int to)
        {
            // 仅主轴移动，且逐格检查是否与身体重叠
            Vector2Int step = new Vector2Int(Mathf.Clamp(to.x - from.x, -1, 1), Mathf.Clamp(to.y - from.y, -1, 1));
            if (step.x != 0 && step.y != 0) return false;
            Vector2Int cur = from;

            // 使用缓存的HashSet，避免每次重新创建
            var tailCell = _bodyCells.Last.Value; // 允许移动到尾的格
            while (cur != to)
            {
                cur += step;
                if (IsOccupiedBySelf(cur) && cur != tailCell) return false;
            }
            return true;
        }

        bool TryAdvanceOneCell(Vector2Int step)
        {
            var next = ClampInside(_currentHeadCell + step);
            if (!IsPathValid(_currentHeadCell, next)) return false;
            MoveSnakeToHeadCell(next);
            return true;
        }

        // 将A->B分解为轴对齐的网格路径（先主轴后次轴），使用复用缓冲避免GC
        void EnqueueAxisAlignedPath(Vector2Int from, Vector2Int to)
        {
            _pathBuildBuffer.Clear();
            if (from == to) return;
            int dx = to.x - from.x;
            int dy = to.y - from.y;
            bool horizFirst = Mathf.Abs(dx) >= Mathf.Abs(dy);
            int stepx = dx == 0 ? 0 : (dx > 0 ? 1 : -1);
            int stepy = dy == 0 ? 0 : (dy > 0 ? 1 : -1);
            Vector2Int cur = from;

            // 水平移动
            if (horizFirst)
            {
                for (int i = 0; i < Mathf.Abs(dx); i++)
                {
                    cur = new Vector2Int(cur.x + stepx, cur.y);
                    Vector2Int clampedCell = ClampInside(cur);
                    //if (IsPathBlocked(clampedCell)) break; // 遇到阻挡物停止
                    _pathBuildBuffer.Add(clampedCell);
                }
                for (int i = 0; i < Mathf.Abs(dy); i++)
                {
                    cur = new Vector2Int(cur.x, cur.y + stepy);
                    Vector2Int clampedCell = ClampInside(cur);
                    //if (IsPathBlocked(clampedCell)) break; // 遇到阻挡物停止
                    _pathBuildBuffer.Add(clampedCell);
                }
            }
            else
            {
                for (int i = 0; i < Mathf.Abs(dy); i++)
                {
                    cur = new Vector2Int(cur.x, cur.y + stepy);
                    Vector2Int clampedCell = ClampInside(cur);
                    //if (IsPathBlocked(clampedCell)) break; // 遇到阻挡物停止
                    _pathBuildBuffer.Add(clampedCell);
                }
                for (int i = 0; i < Mathf.Abs(dx); i++)
                {
                    cur = new Vector2Int(cur.x + stepx, cur.y);
                    Vector2Int clampedCell = ClampInside(cur);
                    //if (IsPathBlocked(clampedCell)) break; // 遇到阻挡物停止
                    _pathBuildBuffer.Add(clampedCell);
                }
            }

            int curCount = _pathQueue.Count;
            // 将有效路径点加入队列
            for (int i = 0; i < _pathBuildBuffer.Count; i++)
            {
                if (curCount >= MAX_PATH_QUEUE_SIZE)
                {
                    _pathQueue.Last.Value = (_pathBuildBuffer[i]);
                }
                else
                {
                    if (curCount > 1 && _pathQueue.Last.Value == (_pathBuildBuffer[i]))
                    {
                        break;
                    }
                    else
                    {
                        _pathQueue.AddLast((_pathBuildBuffer[i]));
                    }
                }
            }
        }

        bool AdvanceHeadTo(Vector2Int nextCell)
        {
            // 必须相邻
            if (Manhattan(_currentHeadCell, nextCell) != 1) return false;
            // 检查网格边界
            if (!_grid.IsInside(nextCell)) return false;
            // 使用与IsPathBlocked相同的阻挡检测逻辑，支持颜色匹配
            if (IsPathBlocked(nextCell)) return false;
            // 占用校验：允许进入原尾
            var tailCell = _bodyCells.Last.Value;
            if (IsOccupiedBySelf(nextCell) && nextCell != tailCell) return false;


            // 更新身体
            _bodyCells.AddFirst(nextCell);
            _bodyCells.RemoveLast();
            _currentHeadCell = nextCell;
            _currentTailCell = _bodyCells.Last.Value;

            // 同步HashSet缓存
            SyncBodyCellsSet();

            // 移动完成后，更新身体图片
            if (EnableBodySpriteManagement && _bodySpriteManager != null)
            {
                _bodySpriteManager.OnSnakeMoved();
            }

            return true;
        }

        bool AdvanceTailTo(Vector2Int nextCell)
        {
            // 必须相邻
            if (Manhattan(_currentTailCell, nextCell) != 1) return false;
            // 检查网格边界
            if (!_grid.IsInside(nextCell)) return false;
            // 使用与IsPathBlocked相同的阻挡检测逻辑，支持颜色匹配
            if (IsPathBlocked(nextCell)) return false;
            // 占用校验：允许进入原头
            var headCell = _bodyCells.First.Value;
            if (IsOccupiedBySelf(nextCell) && nextCell != headCell) return false;

            // 更新身体：拖尾时，尾部移动到新位置，头部保持不变
            _bodyCells.AddLast(nextCell);
            _bodyCells.RemoveFirst();
            _currentTailCell = nextCell;
            _currentHeadCell = _bodyCells.First.Value;

            // 同步HashSet缓存
            SyncBodyCellsSet();

            // 移动完成后，更新身体图片
            if (EnableBodySpriteManagement && _bodySpriteManager != null)
            {
                _bodySpriteManager.OnSnakeMoved();
            }

            return true;
        }

        bool TryReverseOneStep()
        {
            // 以尾部为基准，朝着与尾相邻段的反方向后退；若不可行，尝试左右方向
            if (_bodyCells.Last == null || _bodyCells.Last.Previous == null) return false;
            var tail = _bodyCells.Last.Value;
            var prev = _bodyCells.Last.Previous.Value; // 尾部相邻的身体
            Vector2Int dir = tail - prev; // 远离身体方向
            Vector2Int left = new Vector2Int(-dir.y, dir.x);
            Vector2Int right = new Vector2Int(dir.y, -dir.x);
            var candidates = new[] { dir, left, right };
            for (int i = 0; i < candidates.Length; i++)
            {
                var next = tail + candidates[i];
                if (!_grid.IsInside(next.x, next.y)) continue;
                if (_grid.HasBlock(next.x, next.y)) continue;
                if (IsPathBlocked(next)) continue;
                if (!IsCellFree(next)) continue;
                return AdvanceTailTo(next);
            }
            return false;
        }

        bool TryReverseFromTail()
        {
            // 从尾部倒车：以头部为基准，朝着与头相邻段的反方向前进
            if (_bodyCells.First == null || _bodyCells.First.Next == null) return false;
            var head = _bodyCells.First.Value;
            var next = _bodyCells.First.Next.Value; // 头部相邻的身体
            Vector2Int dir = head - next; // 远离身体方向
            Vector2Int left = new Vector2Int(-dir.y, dir.x);
            Vector2Int right = new Vector2Int(dir.y, -dir.x);
            var candidates = new[] { dir, left, right };
            for (int i = 0; i < candidates.Length; i++)
            {
                var nextHead = head + candidates[i];
                if (!_grid.IsInside(nextHead.x, nextHead.y)) continue;
                if (_grid.HasBlock(nextHead.x, nextHead.y)) continue;
                if (IsPathBlocked(nextHead)) continue;
                if (!IsCellFree(nextHead)) continue;
                return AdvanceHeadTo(nextHead);
            }
            return false;
        }



        bool IsCellFree(Vector2Int cell)
        {
            // 不允许进入自身身体占用格（使用优化的HashSet查找）
            if (IsOccupiedBySelf(cell)) return false;

            // 不允许进入其他蛇占用的格子
            if (_snakeManager != null && _snakeManager.IsCellOccupiedByOtherSnakes(cell, this)) return false;

            return true;
        }

        Vector2Int ClampAdjacent(Vector2Int cell, Vector2Int targetNeighbor)
        {
            Vector2Int best = cell;
            int bestDist = Manhattan(cell, targetNeighbor);
            var candidates = new[]
            {
                targetNeighbor + Vector2Int.up,
                targetNeighbor + Vector2Int.down,
                targetNeighbor + Vector2Int.left,
                targetNeighbor + Vector2Int.right
            };
            for (int i = 0; i < candidates.Length; i++)
            {
                var c = ClampInside(candidates[i]);
                int d = Manhattan(c, cell);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = c;
                }
            }
            return best;
        }

        
        */

        bool TryPickHeadOrTail(Vector3 world, out bool onHead)
        {
            onHead = false;
            if(EnableSubGridMovement)
            {
                //小格移动逻辑
                if (_subSegments.Count == 0) return false;

                var curMouseBigCell = _grid.WorldToCell(world);
                if(curMouseBigCell == SubGridHelper.SubCellToBigCell(_currentHeadSubCell))
                {
                    onHead = true;
                    return true;
                }
                if (curMouseBigCell == SubGridHelper.SubCellToBigCell(_currentTailSubCell))
                {

                    onHead = false;
                    return true;
                }
                return false;
            }

            return false;
            
        }
        Vector3 ScreenToWorld(Vector3 screen)
        {
            // UI渲染模式：使用UI坐标转换
            if (_parentCanvas != null)
            {
                var rect = transform.parent as RectTransform; // GridContainer
                if (rect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, _parentCanvas.worldCamera, out Vector2 localPoint))
                {
                    return new Vector3(localPoint.x, localPoint.y, 0f);
                }
            }
            else
            {
                _parentCanvas = GetComponentInParent<Canvas>();
                Debug.LogError("cache _parentCanvas error!!!");
            }

            // 最后的fallback：简单的比例转换
            Vector2 screenSize = new Vector2(Screen.width, Screen.height);
            Vector2 normalizedScreen = new Vector2(screen.x / screenSize.x, screen.y / screenSize.y);

            // 假设网格居中在屏幕中，计算相对位置
            float gridWidth = _grid.Width * _grid.CellSize;
            float gridHeight = _grid.Height * _grid.CellSize;

            float worldX = (normalizedScreen.x - 0.5f) * gridWidth;
            float worldY = (normalizedScreen.y - 0.5f) * gridHeight;

            return new Vector3(worldX, worldY, 0f);

        }

        void OnGUI()
        {
            //if (!ShowDebugStats) return;
            //GUI.color = Color.white;
            //var style = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            //GUILayout.BeginArea(new Rect(10, 10, 400, 200), GUI.skin.box);
            //GUILayout.Label($"Queue: {_pathQueue.Count}", style);
            //GUILayout.Label($"Accumulator: {_moveAccumulator:F2}", style);
            //GUILayout.Label($"Head: {_currentHeadCell} Tail: {_currentTailCell}", style);
            //GUILayout.EndArea();
        }

        void OnDrawGizmosSelected()
        {
            //if (!DrawDebugGizmos) return;
            //if (_grid.Width == 0) return;
            //Gizmos.color = new Color(0f, 1f, 0f, 0.4f);
            //foreach (var c in _bodyCells)
            //{
            //    Gizmos.DrawWireCube(_grid.CellToWorld(c), new Vector3(_grid.CellSize, _grid.CellSize, 0f));
            //}
            //Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
            //Vector3 prev = Vector3.negativeInfinity;
            //foreach (var c in _pathQueue)
            //{
            //    var p = _grid.CellToWorld(c);
            //    Gizmos.DrawSphere(p, 0.05f);
            //    if (prev.x > -10000f) Gizmos.DrawLine(prev, p);
            //    prev = p;
            //}
        }

        /// <summary>
        /// 获取蛇头的格子位置
        /// </summary>
        public Vector2Int GetHeadCell()
        {
            return SubGridHelper.SubCellToBigCell(_currentHeadSubCell);
        }

        /// <summary>
        /// 获取蛇尾的格子位置
        /// </summary>
        public Vector2Int GetTailCell()
        {
            return SubGridHelper.SubCellToBigCell(_currentTailSubCell);
        }

        /// <summary>
        /// 清理缓存的RectTransform组件，防止内存泄漏
        /// </summary>
        void CleanupCachedComponents()
        {
            // 清理已销毁的RectTransform引用
            for (int i = _cachedSubRectTransforms.Count - 1; i >= 0; i--)
            {
                if (_cachedSubRectTransforms[i] == null)
                {
                    _cachedSubRectTransforms.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 获取增强的蛇尾视觉位置（无路径点时使用）
        /// </summary>
        Vector3 GetEnhancedTailVisual(Vector3 fingerPos, Vector2Int currentTailCell)
        {
            Vector3 currentTailWorld = _grid.CellToWorld(currentTailCell);

            // 计算手指与当前尾部的距离和方向
            Vector3 direction = fingerPos - currentTailWorld;
            float distance = direction.magnitude;

            // 如果距离很小，直接返回当前位置
            if (distance < 0.1f)
            {
                return currentTailWorld;
            }

            // 检查手指所在的网格是否被阻挡
            Vector2Int fingerCell = ClampInside(_grid.WorldToCell(fingerPos));
            bool fingerCellBlocked = IsPathBlocked(fingerCell);

            // 如果手指位置被阻挡，限制视觉位置在当前格子边界内
            if (fingerCellBlocked)
            {
                // 计算当前格子的边界
                float halfCellSize = _grid.CellSize * 0.5f;
                Vector3 clampedPos = currentTailWorld;

                // 根据手指方向调整位置，但不超出当前格子
                if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
                {
                    // 主要是水平方向
                    clampedPos.x += Mathf.Sign(direction.x) * halfCellSize * 0.8f;
                }
                else
                {
                    // 主要是垂直方向
                    clampedPos.y += Mathf.Sign(direction.y) * halfCellSize * 0.8f;
                }

                return clampedPos;
            }

            // 手指位置不被阻挡，按原有逻辑计算
            // 限制最大距离以防止过度拉伸
            float maxDistance = _grid.CellSize * 0.4f;
            if (distance > maxDistance)
            {
                direction = direction.normalized * maxDistance;
            }

            // 根据拖动轴限制方向
            Vector3 targetPos = currentTailWorld + direction;
            if (_dragAxis == DragAxis.X)
            {
                targetPos.y = currentTailWorld.y; // 只允许水平移动
            }
            else if (_dragAxis == DragAxis.Y)
            {
                targetPos.x = currentTailWorld.x; // 只允许垂直移动
            }

            return targetPos;
        }



        protected override void OnDestroy()
        {
            base.OnDestroy();
        }
    }
}


