using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ReGecko.GridSystem;
using ReGecko.Grid.Entities;
using System.Collections;
using ReGecko.GameCore.Flow;

namespace ReGecko.SnakeSystem
{
    public class SnakeController : MonoBehaviour
    {
        public Sprite BodySprite;
        public Color BodyColor = Color.white;
        public int Length = 4;
        public Vector2Int HeadCell;
        public Vector2Int[] InitialBodyCells; // 含头在index 0，可为空
        public float MoveSpeedCellsPerSecond = 16f;
        public float SnapThreshold = 0.05f;
        public int MaxCellsPerFrame = 12;
        [Header("Debug / Profiler")]
        public bool ShowDebugStats = false;
        public bool DrawDebugGizmos = false;

        [Header("Body Sprite Management")]
        public bool EnableBodySpriteManagement = true;

        [Header("Sub-segment Settings")]
        public bool EnableSubSegments = true;
        const int SUB_SEGMENTS_PER_SEGMENT = 5;
        const float TURN_ROTATION_STEP = 22.5f; // 每次旋转角度
        const int TURN_STEPS = 4; // 完成90度转弯需要的步数

        // 转弯状态管理
        private Dictionary<int, Dictionary<int, float>> _subSegmentRotations = new Dictionary<int, Dictionary<int, float>>(); // segmentIndex -> subSegmentIndex -> rotation
        private Dictionary<int, Vector2Int> _turnCenters = new Dictionary<int, Vector2Int>(); // segmentIndex -> turnCenter

        // 25格子虚拟坐标系统
        const int VIRTUAL_GRID_SIZE = 5; // 每个大格子分为5x5=25个小格子
        const float VIRTUAL_CELL_SIZE = 1f / VIRTUAL_GRID_SIZE; // 小格子相对大格子的尺寸比例

        GridConfig _grid;
        GridEntityManager _entityManager;
        readonly List<Transform> _segments = new List<Transform>();
        readonly List<List<Transform>> _subSegments = new List<List<Transform>>(); // 每段的5个小段
        readonly List<List<Queue<Vector2>>> _subSegmentPathQueues = new List<List<Queue<Vector2>>>(); // 每个小段的路径队列

        // 虚拟网格路径规则
        static readonly Vector2[] ValidEntryPoints = { new Vector2(0, 2), new Vector2(2, 0), new Vector2(4, 2), new Vector2(2, 4) };
        static readonly Vector2[] HorizontalLine = { new Vector2(0, 2), new Vector2(1, 2), new Vector2(2, 2), new Vector2(3, 2), new Vector2(4, 2) };
        static readonly Vector2[] VerticalLine = { new Vector2(2, 0), new Vector2(2, 1), new Vector2(2, 2), new Vector2(2, 3), new Vector2(2, 4) };

        readonly LinkedList<Vector2Int> _bodyCells = new LinkedList<Vector2Int>(); // 离散身体占用格，头在First
        readonly Queue<Vector2Int> _pathQueue = new Queue<Vector2Int>(); // 待消费路径（目标格序列）
        readonly List<Vector2Int> _pathBuildBuffer = new List<Vector2Int>(64); // 复用的路径构建缓冲
        Vector2Int _dragStartCell;
        bool _dragging;
        bool _dragOnHead;
        Vector2Int _currentHeadCell;
        Vector2Int _currentTailCell;
        Vector2Int _lastSampledCell; // 上次采样的手指网格
        float _moveAccumulator; // 基于速度的逐格推进计数器
        float _lastStatsTime;
        int _stepsConsumedThisFrame;

        enum DragAxis { None, X, Y }
        DragAxis _dragAxis = DragAxis.None;

        bool _consuming; // 洞吞噬中

        SnakeBodySpriteManager _bodySpriteManager;

        // 公共访问方法
        public Vector2Int GetHeadCell() => _bodyCells.Count > 0 ? _currentHeadCell : Vector2Int.zero;
        public Vector2Int GetTailCell() => _bodyCells.Count > 0 ? _currentTailCell : Vector2Int.zero;

        /// <summary>
        /// 获取身体格子列表（用于图片管理器）
        /// </summary>
        public LinkedList<Vector2Int> GetBodyCells() => _bodyCells;

        public void Initialize(GridConfig grid, GridEntityManager entityManager = null)
        {
            _grid = grid;
            _entityManager = entityManager ?? FindObjectOfType<GridEntityManager>();

            // 初始化身体图片管理器
            if (EnableBodySpriteManagement)
            {
                InitializeBodySpriteManager();
            }

            BuildSegments();
            PlaceInitial();

        }

        public void UpdateGridConfig(GridConfig newGrid)
        {
            _grid = newGrid;
            for (int i = 0; i < _segments.Count; i++)
            {
                var rt = _segments[i].GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.sizeDelta = new Vector2(_grid.CellSize, _grid.CellSize);
                }
            }

            // 更新小段的大小
            if (EnableSubSegments)
            {
                float subSegmentSize = _grid.CellSize / SUB_SEGMENTS_PER_SEGMENT;
                for (int i = 0; i < _subSegments.Count; i++)
                {
                    for (int j = 0; j < _subSegments[i].Count; j++)
                    {
                        var rt = _subSegments[i][j].GetComponent<RectTransform>();
                        if (rt != null)
                        {
                            rt.sizeDelta = new Vector2(_grid.CellSize, subSegmentSize);
                        }
                    }
                }
            }
        }

        void BuildSegments()
        {
            // 清理现有段
            for (int i = 0; i < _segments.Count; i++)
            {
                if (_segments[i] != null) Destroy(_segments[i].gameObject);
            }
            _segments.Clear();

            // 清理现有小段和转弯状态
            for (int i = 0; i < _subSegments.Count; i++)
            {
                for (int j = 0; j < _subSegments[i].Count; j++)
                {
                    if (_subSegments[i][j] != null) Destroy(_subSegments[i][j].gameObject);
                }
            }
            _subSegments.Clear();
            _subSegmentPathQueues.Clear();
            _subSegmentRotations.Clear();
            _turnCenters.Clear();

            for (int i = 0; i < Mathf.Max(1, Length); i++)
            {
                var go = new GameObject(i == 0 ? "Head" : $"Body_{i}");
                // 蛇的段应该直接在蛇对象下，因为蛇对象已经在GridContainer中
                go.transform.SetParent(transform, false);

                // UI渲染：使用Image组件
                var image = go.AddComponent<Image>();
                image.sprite = BodySprite;
                image.color = BodyColor;
                image.raycastTarget = false;

                // 设置RectTransform（正确的锚点和轴心）
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(_grid.CellSize, _grid.CellSize);

                _segments.Add(go.transform);

                // 创建小段
                if (EnableSubSegments)
                {
                    CreateSubSegments(i, go.transform);
                }
            }
        }

        void CreateSubSegments(int segmentIndex, Transform parentSegment)
        {
            List<Transform> subSegmentList = new List<Transform>();
            float subSegmentSize = _grid.CellSize / SUB_SEGMENTS_PER_SEGMENT;

            for (int i = 0; i < SUB_SEGMENTS_PER_SEGMENT; i++)
            {
                var subGO = new GameObject($"SubSegment_{segmentIndex}_{i}");
                // 小段直接作为蛇的子对象，而不是主段的子对象
                subGO.transform.SetParent(transform, false);

                // UI渲染：使用Image组件
                var image = subGO.AddComponent<Image>();
                image.sprite = BodySprite;
                image.color = BodyColor;
                image.raycastTarget = false;
                // 设置图片类型为填充
                image.type = Image.Type.Filled;

                // 设置RectTransform
                var rt = subGO.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(_grid.CellSize, subSegmentSize);

                subSegmentList.Add(subGO.transform);
            }

            _subSegments.Add(subSegmentList);

            // 为每个小段创建路径队列
            var pathQueueList = new List<Queue<Vector2>>();
            for (int i = 0; i < SUB_SEGMENTS_PER_SEGMENT; i++)
            {
                pathQueueList.Add(new Queue<Vector2>());
            }
            _subSegmentPathQueues.Add(pathQueueList);

            // 初始化小段的旋转状态
            var rotationDict = new Dictionary<int, float>();
            for (int i = 0; i < SUB_SEGMENTS_PER_SEGMENT; i++)
            {
                rotationDict[i] = 0f; // 初始旋转为0
            }
            _subSegmentRotations[segmentIndex] = rotationDict;

            // 隐藏主段，显示小段
            parentSegment.GetComponent<Image>().enabled = false;
        }

        Vector3 GetGridDirection(Vector3 direction)
        {
            // 将方向标准化为网格的四个基本方向
            if (direction.magnitude < 0.1f)
                return Vector3.zero;

            // 确定主要方向轴
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            {
                // 水平方向
                return direction.x > 0 ? Vector3.right : Vector3.left;
            }
            else
            {
                // 垂直方向
                return direction.y > 0 ? Vector3.up : Vector3.down;
            }
        }

        void UpdateSubSegmentPositions()
        {
            if (!EnableSubSegments || _subSegments.Count != _segments.Count)
                return;

            for (int segmentIndex = 0; segmentIndex < _segments.Count; segmentIndex++)
            {
                UpdateSubSegmentForSegment(segmentIndex);
            }
        }

        void UpdateSubSegmentForSegment(int segmentIndex)
        {
            if (segmentIndex >= _subSegments.Count || segmentIndex >= _segments.Count)
                return;

            var subSegmentList = _subSegments[segmentIndex];
            var mainSegment = _segments[segmentIndex];
            
            // 获取主段的位置
            var rt = mainSegment.GetComponent<RectTransform>();
            if (rt == null) return;
            
            Vector3 mainPos = new Vector3(rt.anchoredPosition.x, rt.anchoredPosition.y, 0f);
            
            // 检查是否是拖动状态下的拖动部位（头部或尾部）
            bool isDraggedSegment = _dragging && ((_dragOnHead && segmentIndex == 0) || (!_dragOnHead && segmentIndex == _segments.Count - 1));
            
            if (isDraggedSegment)
            {
                // 特殊处理：拖动时的头尾部位使用鼠标位置驱动的小段移动
                ProcessDraggedSegmentSubSegments(subSegmentList, segmentIndex, mainPos);
            }
            else
            {
                // 原有逻辑：计算转弯或直线移动
                Vector3 inDirection = Vector3.zero;
                Vector3 outDirection = Vector3.zero;
                bool isTurn = false;
                
                if (segmentIndex > 0 && segmentIndex < _segments.Count - 1)
                {
                    // 中间段，计算进入和离开方向
                    var prevRT = _segments[segmentIndex - 1].GetComponent<RectTransform>();
                    var nextRT = _segments[segmentIndex + 1].GetComponent<RectTransform>();
                    
                    if (prevRT != null && nextRT != null)
                    {
                        Vector3 prevPos = new Vector3(prevRT.anchoredPosition.x, prevRT.anchoredPosition.y, 0f);
                        Vector3 nextPos = new Vector3(nextRT.anchoredPosition.x, nextRT.anchoredPosition.y, 0f);
                        
                        // 计算方向差值（网格对齐）
                        Vector3 inDiff = mainPos - prevPos;
                        Vector3 outDiff = nextPos - mainPos;
                        
                        // 标准化为网格方向（上下左右）
                        inDirection = GetGridDirection(inDiff);
                        outDirection = GetGridDirection(outDiff);
                        
                        // 检测是否为90度转弯（两个方向垂直）
                        float dot = Vector3.Dot(inDirection, outDirection);
                        isTurn = Mathf.Abs(dot) < 0.1f; // 垂直时点积接近0
                    }
                }
                
                if (isTurn)
                {
                    // 转弯：使用渐进式转弯系统
                    // 记录转弯中心
                    Vector2Int currentCell = _grid.WorldToCell(mainPos);
                    _turnCenters[segmentIndex] = currentCell;
                    
                    ArrangeSubSegmentsWithPositionBasedTurn(subSegmentList, segmentIndex, mainPos, inDirection, outDirection);
                }
                else
                {
                    // 直线：重置旋转状态并排列
                    ResetRotationState(segmentIndex);
                    Vector3 direction = GetMovementDirection(segmentIndex, mainPos);
                    ArrangeSubSegmentsInLineWithRotation(subSegmentList, segmentIndex, mainPos, direction);
                }
            }
        }

        /// <summary>
        /// 处理拖动时的头尾部位小段移动（特殊逻辑）
        /// </summary>
        void ProcessDraggedSegmentSubSegments(List<Transform> subSegmentList, int segmentIndex, Vector3 mainPos)
        {
            Vector2Int dragCell;
            
            if (_dragOnHead)
            {
                dragCell = GetHeadCell();
            }
            else
            {
                dragCell = GetTailCell();
            }
            
            // 获取鼠标在格子中的象限
            int quadrant = GetMouseQuadrantInCell(dragCell);
            
            // 获取鼠标的世界坐标
            Vector3 mouseWorldPos = ScreenToWorld(Input.mousePosition);
            Vector3 cellCenterWorld = _grid.CellToWorld(dragCell);
            
            // 计算主方向（蛇的移动方向）
            Vector3 mainDirection = GetSegmentMainDirection(segmentIndex);
            
            // 处理进入当前格子的小段身体
            ProcessSubSegmentsInDragCell(subSegmentList, segmentIndex, mouseWorldPos, cellCenterWorld, mainDirection, quadrant);
        }

        /// <summary>
        /// 获取段的主要移动方向（用于拖拽时的小段排列）
        /// </summary>
        Vector3 GetSegmentMainDirection(int segmentIndex)
        {
            if (_dragOnHead && segmentIndex == 0)
            {
                // 头部拖拽：从第二段指向头部的方向
                if (_segments.Count > 1)
                {
                    var headPos = _segments[0].GetComponent<RectTransform>().anchoredPosition;
                    var secondPos = _segments[1].GetComponent<RectTransform>().anchoredPosition;
                    Vector3 direction = (headPos - secondPos).normalized;
                    return GetGridDirection(direction); // 标准化为网格方向
                }
            }
            else if (!_dragOnHead && segmentIndex == _segments.Count - 1)
            {
                // 尾部拖拽：从倒数第二段指向尾部的方向
                if (segmentIndex > 0)
                {
                    var tailPos = _segments[segmentIndex].GetComponent<RectTransform>().anchoredPosition;
                    var prevPos = _segments[segmentIndex - 1].GetComponent<RectTransform>().anchoredPosition;
                    Vector3 direction = (tailPos - prevPos).normalized;
                    return GetGridDirection(direction); // 标准化为网格方向
                }
            }

            // 默认水平方向
            return Vector3.right;
        }

        /// <summary>
        /// 处理进入拖拽格子的小段身体位置
        /// </summary>
        void ProcessSubSegmentsInDragCell(List<Transform> subSegmentList, int segmentIndex, Vector3 mouseWorldPos, Vector3 cellCenterWorld, Vector3 mainDirection, int quadrant)
        {
            if (subSegmentList == null || subSegmentList.Count == 0)
                return;

            // 确定哪些小段进入了当前格子
            List<int> subSegmentsInCell = GetSubSegmentsInCell(subSegmentList, cellCenterWorld);

            if (subSegmentsInCell.Count == 0)
            {
                // 如果没有小段在格子内，使用原有的直线排列逻辑
                ArrangeSubSegmentsInLine(subSegmentList, cellCenterWorld, mainDirection);
                return;
            }

            // 新的连续性处理逻辑
            ProcessSubSegmentsWithContinuity(subSegmentList, subSegmentsInCell, segmentIndex, mouseWorldPos, cellCenterWorld, mainDirection);
        }

        /// <summary>
        /// 处理小段的连续性拖拽逻辑，考虑后退和倒车
        /// </summary>
        void ProcessSubSegmentsWithContinuity(List<Transform> subSegmentList, List<int> subSegmentsInCell, int segmentIndex, Vector3 mouseWorldPos, Vector3 cellCenterWorld, Vector3 mainDirection)
        {
            if (subSegmentList == null || subSegmentList.Count == 0 || subSegmentsInCell.Count == 0)
                return;

            // 计算小段间距
            float subSegmentSpacing = _grid.CellSize / SUB_SEGMENTS_PER_SEGMENT;
            
            // 第一个进入格子的小段跟随鼠标位置
            int firstInCellIndex = subSegmentsInCell[0];
            var firstInCellRT = subSegmentList[firstInCellIndex].GetComponent<RectTransform>();
            if (firstInCellRT == null) return;
            
            // 限制鼠标位置在格子范围内
            Vector3 clampedMousePos = ClampPositionToCell(mouseWorldPos, cellCenterWorld);
            firstInCellRT.anchoredPosition = new Vector2(clampedMousePos.x, clampedMousePos.y);
            
            // 从第一个在格子内的小段开始，向后连续排列所有小段
            Vector3 anchorPos = new Vector3(clampedMousePos.x, clampedMousePos.y, 0f);
            Vector3 backwardDirection = -mainDirection; // 向后的方向
            
            // 先处理在格子内的其他小段（在firstInCellIndex之后的）
            for (int i = 1; i < subSegmentsInCell.Count; i++)
            {
                int subSegmentIndex = subSegmentsInCell[i];
                var rt = subSegmentList[subSegmentIndex].GetComponent<RectTransform>();
                if (rt == null) continue;
                
                // 计算在格子内的连续位置
                Vector3 targetPos = anchorPos + backwardDirection * (subSegmentSpacing * i);
                
                // 在垂直方向上做轻微的平滑插值
                Vector3 cellCenterProjection = ProjectPointOntoLine(targetPos, cellCenterWorld, mainDirection);
                float lerpFactor = Mathf.Clamp01((float)i / subSegmentsInCell.Count) * 0.3f;
                Vector3 smoothedPos = Vector3.Lerp(targetPos, cellCenterProjection, lerpFactor);
                
                // 确保位置在格子范围内
                smoothedPos = ClampPositionToCell(smoothedPos, cellCenterWorld);
                rt.anchoredPosition = new Vector2(smoothedPos.x, smoothedPos.y);
            }
            
            // 再处理不在格子内的小段
            ArrangeSubSegmentsOutsideCell(subSegmentList, subSegmentsInCell, segmentIndex, anchorPos, backwardDirection, subSegmentSpacing);
            
            Debug.Log($"Processed segment {segmentIndex} with continuity: {subSegmentsInCell.Count} in cell (first at index {firstInCellIndex}), {subSegmentList.Count - subSegmentsInCell.Count} outside cell");
        }
        
        /// <summary>
        /// 判断位置是否在指定格子内
        /// </summary>
        bool IsPositionInCell(Vector3 position, Vector3 cellCenter)
        {
            float cellHalfSize = _grid.CellSize * 0.5f;
            Vector3 offset = position - cellCenter;
            return Mathf.Abs(offset.x) <= cellHalfSize && Mathf.Abs(offset.y) <= cellHalfSize;
        }
        
        /// <summary>
        /// 处理超出格子的小段，考虑倒车或连接
        /// </summary>
        void HandleSubSegmentOutOfCell(List<Transform> subSegmentList, int subSegmentIndex, Vector3 targetPos, int segmentIndex, Vector3 mainDirection)
        {
            var rt = subSegmentList[subSegmentIndex].GetComponent<RectTransform>();
            if (rt == null) return;
            
            // 首先尝试连接到相邻的大段
            Vector3 connectedPos = TryConnectToAdjacentSegment(segmentIndex, targetPos, mainDirection);
            
            if (connectedPos != Vector3.zero)
            {
                // 成功连接到相邻段
                rt.anchoredPosition = new Vector2(connectedPos.x, connectedPos.y);
            }
            else
            {
                // 无法连接，需要考虑倒车
                RequestSnakeReverse(segmentIndex, subSegmentIndex, targetPos, mainDirection);
                // 临时使用目标位置
                rt.anchoredPosition = new Vector2(targetPos.x, targetPos.y);
            }
        }
        
        /// <summary>
        /// 尝试连接到相邻的大段
        /// </summary>
        Vector3 TryConnectToAdjacentSegment(int currentSegmentIndex, Vector3 targetPos, Vector3 direction)
        {
            int adjacentSegmentIndex = _dragOnHead ? currentSegmentIndex + 1 : currentSegmentIndex - 1;
            
            if (adjacentSegmentIndex >= 0 && adjacentSegmentIndex < _segments.Count)
            {
                var adjacentRT = _segments[adjacentSegmentIndex].GetComponent<RectTransform>();
                if (adjacentRT != null)
                {
                    Vector3 adjacentPos = new Vector3(adjacentRT.anchoredPosition.x, adjacentRT.anchoredPosition.y, 0f);
                    
                    // 计算从目标位置到相邻段的距离
                    float distanceToAdjacent = Vector3.Distance(targetPos, adjacentPos);
                    
                    // 如果距离合理，尝试连接
                    if (distanceToAdjacent <= _grid.CellSize * 1.5f)
                    {
                        // 计算两点之间的中间位置，偏向目标位置
                        float t = 0.7f; // 偏向目标位置
                        return Vector3.Lerp(adjacentPos, targetPos, t);
                    }
                }
            }
            
            return Vector3.zero; // 返回零向量表示无法连接
        }
        
        /// <summary>
        /// 请求蛇倒车以提供更多空间
        /// </summary>
        void RequestSnakeReverse(int segmentIndex, int subSegmentIndex, Vector3 targetPos, Vector3 mainDirection)
        {
            // 计算需要的倒车距离
            float reverseDistance = _grid.CellSize * 0.2f; // 小幅度倒车
            
            // 向整个蛇发送倒车信号（这里可以实现更复杂的倒车逻辑）
            Debug.Log($"Requesting snake reverse for segment {segmentIndex}, sub-segment {subSegmentIndex}, distance: {reverseDistance}");
            
            // TODO: 实现实际的倒车逻辑
        }

        /// <summary>
        /// 处理不在格子内的小段，保持连续性
        /// </summary>
        void ArrangeSubSegmentsOutsideCell(List<Transform> subSegmentList, List<int> subSegmentsInCell, int segmentIndex, Vector3 anchorPos, Vector3 backwardDirection, float subSegmentSpacing)
        {
            int continuousIndex = subSegmentsInCell.Count; // 从格子内的最后一个小段后继续
            
            for (int i = 0; i < subSegmentList.Count; i++)
            {
                if (subSegmentsInCell.Contains(i)) continue; // 跳过已经处理的在格子内的小段
                
                var rt = subSegmentList[i].GetComponent<RectTransform>();
                if (rt == null) continue;
                
                // 计算连续位置
                Vector3 targetPos = anchorPos + backwardDirection * (subSegmentSpacing * continuousIndex);
                
                // 尝试连接到相邻段或使用目标位置
                Vector3 finalPos = TryConnectToAdjacentSegment(segmentIndex, targetPos, backwardDirection);
                if (finalPos == Vector3.zero)
                {
                    finalPos = targetPos; // 如果无法连接，使用计算的位置
                }
                
                rt.anchoredPosition = new Vector2(finalPos.x, finalPos.y);
                continuousIndex++;
            }
        }

        /// <summary>
        /// 处理不在拖拽格子内的小段，使用原有的移动逻辑（备用方法）
        /// </summary>
        void ArrangeNonDragSubSegments(List<Transform> subSegmentList, List<int> subSegmentsInCell, int segmentIndex, Vector3 cellCenterWorld, Vector3 mainDirection)
        {
            if (subSegmentList == null || subSegmentList.Count == 0)
                return;
                
            int nonDragCount = 0;
            // 对于不在格子内的小段，使用原有的直线排列逻辑
            for (int i = 0; i < subSegmentList.Count; i++)
            {
                if (!subSegmentsInCell.Contains(i))
                {
                    var rt = subSegmentList[i].GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        // 使用原有的直线排列逻辑，不进行特殊处理
                        float t = (float)i / (SUB_SEGMENTS_PER_SEGMENT - 1);
                        float offset = (t - 0.5f) * _grid.CellSize * 0.8f;
                        Vector3 pos = cellCenterWorld + mainDirection * offset;
                        rt.anchoredPosition = new Vector2(pos.x, pos.y);
                        nonDragCount++;
                    }
                }
            }
            
            // 调试信息
            if (nonDragCount > 0)
            {
                Debug.Log($"Segment {segmentIndex}: {subSegmentsInCell.Count} sub-segments in drag cell, {nonDragCount} using original logic");
            }
        }

        /// <summary>
        /// 排列不在拖拽格子内的小段，确保与下一大段连续连接（备用方法）
        /// </summary>
        void ArrangeOutOfCellSubSegments(List<Transform> subSegmentList, List<int> subSegmentsInCell, int segmentIndex, Vector3 cellCenterWorld, Vector3 mainDirection)
        {
            if (subSegmentList == null || subSegmentList.Count == 0)
                return;
                
            // 获取下一大段的位置作为连接目标
            Vector3 nextSegmentPos = GetNextSegmentPosition(segmentIndex, mainDirection);
            
            // 计算连接路径的起点和终点
            Vector3 connectionStart = cellCenterWorld;
            Vector3 connectionEnd = nextSegmentPos;
            
            // 如果有小段在格子内，找到最后一个在格子内的小段作为起点
            if (subSegmentsInCell.Count > 0)
            {
                int lastInCellIndex = subSegmentsInCell[subSegmentsInCell.Count - 1];
                var lastInCellRT = subSegmentList[lastInCellIndex].GetComponent<RectTransform>();
                if (lastInCellRT != null)
                {
                    connectionStart = new Vector3(lastInCellRT.anchoredPosition.x, lastInCellRT.anchoredPosition.y, 0f);
                }
            }
            
            // 收集不在格子内的小段，按索引顺序排列
            List<int> outOfCellSegments = new List<int>();
            for (int i = 0; i < subSegmentList.Count; i++)
            {
                if (!subSegmentsInCell.Contains(i))
                {
                    outOfCellSegments.Add(i);
                }
            }
            
            if (outOfCellSegments.Count == 0)
                return;
                
            // 按照小段在列表中的相对位置排列，保持连续性
            for (int i = 0; i < outOfCellSegments.Count; i++)
            {
                int subSegmentIndex = outOfCellSegments[i];
                var rt = subSegmentList[subSegmentIndex].GetComponent<RectTransform>();
                if (rt == null) continue;
                
                // 计算在连接路径上的位置
                // 使用更细致的插值，确保不会完全到达终点
                float t;
                if (outOfCellSegments.Count == 1)
                {
                    t = 0.5f; // 如果只有一个小段，放在中间
                }
                else
                {
                    t = (float)(i + 1) / (outOfCellSegments.Count + 1);
                }
                
                Vector3 pos = Vector3.Lerp(connectionStart, connectionEnd, t);
                rt.anchoredPosition = new Vector2(pos.x, pos.y);
            }
            
            // 调试信息
            Debug.Log($"Arranged {outOfCellSegments.Count} out-of-cell sub-segments for segment {segmentIndex}, connecting from {connectionStart} to {connectionEnd}");
        }
        
        /// <summary>
        /// 获取下一大段的位置（用于连接计算）
        /// </summary>
        Vector3 GetNextSegmentPosition(int currentSegmentIndex, Vector3 mainDirection)
        {
            int nextSegmentIndex = -1;
            
            if (_dragOnHead)
            {
                // 拖拽头部时，下一段是索引+1的段
                nextSegmentIndex = currentSegmentIndex + 1;
            }
            else
            {
                // 拖拽尾部时，下一段是索引-1的段
                nextSegmentIndex = currentSegmentIndex - 1;
            }
            
            // 检查索引有效性
            if (nextSegmentIndex >= 0 && nextSegmentIndex < _segments.Count)
            {
                var nextSegmentRT = _segments[nextSegmentIndex].GetComponent<RectTransform>();
                if (nextSegmentRT != null)
                {
                    return new Vector3(nextSegmentRT.anchoredPosition.x, nextSegmentRT.anchoredPosition.y, 0f);
                }
            }
            
            // 如果没有下一段，返回当前段位置加上主方向的偏移
            Vector3 currentSegmentPos = _segments[currentSegmentIndex].GetComponent<RectTransform>().anchoredPosition;
            float offset = _dragOnHead ? _grid.CellSize : -_grid.CellSize;
            return new Vector3(currentSegmentPos.x, currentSegmentPos.y, 0f) + mainDirection * offset;
        }

        /// <summary>
        /// 获取进入指定格子的小段索引列表
        /// </summary>
        List<int> GetSubSegmentsInCell(List<Transform> subSegmentList, Vector3 cellCenterWorld)
        {
            List<int> result = new List<int>();
            float cellHalfSize = _grid.CellSize * 0.5f;

            for (int i = 0; i < subSegmentList.Count; i++)
            {
                var rt = subSegmentList[i].GetComponent<RectTransform>();
                if (rt == null) continue;

                Vector3 subSegmentPos = new Vector3(rt.anchoredPosition.x, rt.anchoredPosition.y, 0f);
                Vector3 offset = subSegmentPos - cellCenterWorld;

                // 检查是否在格子范围内
                if (Mathf.Abs(offset.x) <= cellHalfSize && Mathf.Abs(offset.y) <= cellHalfSize)
                {
                    result.Add(i);
                }
            }

            return result;
        }

        /// <summary>
        /// 将位置限制在格子范围内
        /// </summary>
        Vector3 ClampPositionToCell(Vector3 position, Vector3 cellCenter)
        {
            float cellHalfSize = _grid.CellSize * 0.4f; // 留一些边距
            Vector3 offset = position - cellCenter;

            offset.x = Mathf.Clamp(offset.x, -cellHalfSize, cellHalfSize);
            offset.y = Mathf.Clamp(offset.y, -cellHalfSize, cellHalfSize);

            return cellCenter + offset;
        }

        /// <summary>
        /// 将点投影到通过指定点和方向的直线上
        /// </summary>
        Vector3 ProjectPointOntoLine(Vector3 point, Vector3 linePoint, Vector3 lineDirection)
        {
            Vector3 pointToLinePoint = point - linePoint;
            float projectionLength = Vector3.Dot(pointToLinePoint, lineDirection.normalized);
            return linePoint + lineDirection.normalized * projectionLength;
        }

        Vector3 GetMovementDirection(int segmentIndex, Vector3 mainPos)
        {
            Vector3 direction = Vector3.right; // 默认方向

            if (segmentIndex == 0) // 头部
            {
                if (_segments.Count > 1)
                {
                    var nextRT = _segments[1].GetComponent<RectTransform>();
                    if (nextRT != null)
                    {
                        Vector3 nextPos = new Vector3(nextRT.anchoredPosition.x, nextRT.anchoredPosition.y, 0f);
                        Vector3 diff = mainPos - nextPos;
                        direction = GetGridDirection(diff);
                    }
                }
            }
            else if (segmentIndex == _segments.Count - 1) // 尾部
            {
                var prevRT = _segments[segmentIndex - 1].GetComponent<RectTransform>();
                if (prevRT != null)
                {
                    Vector3 prevPos = new Vector3(prevRT.anchoredPosition.x, prevRT.anchoredPosition.y, 0f);
                    Vector3 diff = mainPos - prevPos;
                    direction = GetGridDirection(diff);
                }
            }
            else
            {
                // 中间段但不转弯，使用与前一段的方向
                var prevRT = _segments[segmentIndex - 1].GetComponent<RectTransform>();
                if (prevRT != null)
                {
                    Vector3 prevPos = new Vector3(prevRT.anchoredPosition.x, prevRT.anchoredPosition.y, 0f);
                    Vector3 diff = mainPos - prevPos;
                    direction = GetGridDirection(diff);
                }
            }

            return direction;
        }

        void ArrangeSubSegmentsInLine(List<Transform> subSegments, Vector3 centerPos, Vector3 direction)
        {
            float subSegmentSize = _grid.CellSize / SUB_SEGMENTS_PER_SEGMENT;

            // 5个小段均分主段的空间，确保不超出主段边界
            // 主段的有效范围应该是 ±(_grid.CellSize * 0.4) 来留出一些边距
            float maxOffset = _grid.CellSize * 0.4f;

            for (int i = 0; i < subSegments.Count; i++)
            {
                // 计算每个小段在主段中的相对位置
                // i=0时在最前面，i=4时在最后面
                float t = (float)i / (SUB_SEGMENTS_PER_SEGMENT - 1); // 0, 0.25, 0.5, 0.75, 1
                float offset = (t - 0.5f) * maxOffset * 2f; // 在 ±maxOffset 范围内分布

                Vector3 pos = centerPos + direction * offset;

                var rt = subSegments[i].GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchoredPosition = new Vector2(pos.x, pos.y);
                    // 移除旋转，保持原始方向
                    rt.rotation = Quaternion.identity;
                }
            }
        }

        void ArrangeSubSegmentsInLShape(List<Transform> subSegments, Vector3 centerPos, Vector3 inDirection, Vector3 outDirection)
        {
            float subSegmentSize = _grid.CellSize / SUB_SEGMENTS_PER_SEGMENT;

            // 计算90度L形转弯的关键点，确保小段不超出主段范围
            // 使用更小的偏移量来保持在合理范围内
            float armLength = _grid.CellSize * 0.3f; // 每个方向的最大延伸距离
            float turnRadius = _grid.CellSize * 0.15f; // 转弯半径

            // 转弯内角点：在两个方向的交汇处，稍微向内偏移
            Vector3 innerCorner = centerPos + (inDirection + outDirection).normalized * turnRadius;

            for (int i = 0; i < subSegments.Count; i++)
            {
                Vector3 pos;

                if (i == 0)
                {
                    // 第1个小段：沿进入方向，距离中心最远
                    pos = centerPos - inDirection * armLength;
                }
                else if (i == 1)
                {
                    // 第2个小段：沿进入方向，距离中心较近
                    pos = centerPos - inDirection * (armLength * 0.5f);
                }
                else if (i == 2)
                {
                    // 第3个小段：在转弯内角，创造90度效果
                    pos = innerCorner;
                }
                else if (i == 3)
                {
                    // 第4个小段：沿离开方向，距离中心较近
                    pos = centerPos + outDirection * (armLength * 0.5f);
                }
                else // i == 4
                {
                    // 第5个小段：沿离开方向，距离中心最远
                    pos = centerPos + outDirection * armLength;
                }

                var rt = subSegments[i].GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchoredPosition = new Vector2(pos.x, pos.y);
                    // 移除旋转，保持原始方向
                    rt.rotation = Quaternion.identity;
                }
            }
        }

        void InitializeBodySpriteManager()
        {
            // 检查是否已经有身体图片管理器
            _bodySpriteManager = GetComponent<SnakeBodySpriteManager>();
            if (_bodySpriteManager == null)
            {
                _bodySpriteManager = gameObject.AddComponent<SnakeBodySpriteManager>();
            }
            _bodySpriteManager.Config = GameContext.SnakeBodyConfig;
        }

        void PlaceInitial()
        {
            // 构建初始身体格（优先使用配置）
            List<Vector2Int> cells = new List<Vector2Int>();
            if (InitialBodyCells != null && InitialBodyCells.Length > 0)
            {
                for (int i = 0; i < InitialBodyCells.Length && i < Length; i++)
                {
                    var c = ClampInside(InitialBodyCells[i]);
                    if (cells.Count == 0 || Manhattan(cells[cells.Count - 1], c) == 1)
                    {
                        cells.Add(c);
                    }
                    else
                    {
                        break; // 非相邻则停止使用后续，避免断裂
                    }
                }
            }
            if (cells.Count == 0)
            {
                var head = ClampInside(HeadCell);
                cells.Add(head);
                for (int i = 1; i < Length; i++)
                {
                    var c = new Vector2Int(head.x, Mathf.Clamp(head.y + i, 0, _grid.Height - 1));
                    cells.Add(c);
                }
            }
            // 去重防重叠
            var set = new HashSet<Vector2Int>();
            for (int i = 0; i < cells.Count; i++)
            {
                if (set.Contains(cells[i]))
                {
                    // 发现重叠，回退到简单直线
                    cells.Clear();
                    var head = ClampInside(HeadCell);
                    cells.Add(head);
                    for (int k = 1; k < Length; k++)
                    {
                        cells.Add(new Vector2Int(head.x, Mathf.Clamp(head.y + k, 0, _grid.Height - 1)));
                    }
                    break;
                }
                set.Add(cells[i]);
            }
            // 同步到链表与可视
            _bodyCells.Clear();
            for (int i = 0; i < Mathf.Min(cells.Count, _segments.Count); i++)
            {
                _bodyCells.AddLast(cells[i]);

                var rt = _segments[i].GetComponent<RectTransform>();
                if (rt != null)
                {
                    var worldPos = _grid.CellToWorld(cells[i]);
                    rt.anchoredPosition = new Vector2(worldPos.x, worldPos.y);
                }


            }
            _currentHeadCell = _bodyCells.First.Value;
            _currentTailCell = _bodyCells.Last.Value;

            // 初始放置完成后，更新身体图片
            if (EnableBodySpriteManagement && _bodySpriteManager != null)
            {
                _bodySpriteManager.UpdateAllSegmentSprites();
            }

            // 更新小段位置
            if (EnableSubSegments)
            {
                UpdateSubSegmentPositions();
            }
        }

        Vector2Int ClampInside(Vector2Int cell)
        {
            cell.x = Mathf.Clamp(cell.x, 0, _grid.Width - 1);
            cell.y = Mathf.Clamp(cell.y, 0, _grid.Height - 1);
            return cell;
        }

        void Update()
        {
            HandleInput();
            UpdateMovement();
        }

        void HandleInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                var world = ScreenToWorld(Input.mousePosition);
                if (TryPickHeadOrTail(world, out _dragOnHead))
                {
                    _dragging = true;
                    _dragStartCell = _grid.WorldToCell(world);
                    _pathQueue.Clear();
                    _lastSampledCell = _dragOnHead ? _currentHeadCell : _currentTailCell;
                    _moveAccumulator = 0f;
                    _stepsConsumedThisFrame = 0;
                    _dragAxis = DragAxis.None;
                }
            }
            else if (_dragging && Input.GetMouseButton(0))
            {
                // 拖拽过程中，持续更新小段路径
                var world = ScreenToWorld(Input.mousePosition);
                UpdateSubSegmentPaths(world);
            }
            else if (Input.GetMouseButtonUp(0))
            {
                _dragging = false;
                // 结束拖拽：清空未消费路径并吸附（可选）
                _pathQueue.Clear();
                SnapToGrid();
                _dragAxis = DragAxis.None;
            }
        }

        void UpdateMovement()
        {
            // 如果蛇已被完全消除，停止所有移动更新
            if (_bodyCells.Count == 0) return;

            if (_dragging && !_consuming)
            {
                // 采样当前手指所在格，扩充路径队列（仅四向路径）
                var world = ScreenToWorld(Input.mousePosition);
                var targetCell = ClampInside(_grid.WorldToCell(world));
                if (targetCell != _lastSampledCell)
                {
                    // 更新主方向：按更大位移轴确定
                    var delta = targetCell - (_dragOnHead ? _currentHeadCell : _currentTailCell);
                    _dragAxis = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) ? DragAxis.X : DragAxis.Y;
                    EnqueueAxisAlignedPath(_lastSampledCell, targetCell);
                    _lastSampledCell = targetCell;
                }

                // 消费小段路径
                //ConsumeSubSegmentPaths();

                // 洞检测：若拖动端临近洞，触发吞噬
                var hole = FindAdjacentHole(_dragOnHead ? _currentHeadCell : _currentTailCell);
                if (hole != null)
                {
                    _consumeCoroutine ??= StartCoroutine(CoConsume(hole, _dragOnHead));
                }
            }

            // 按速度逐格消费路径
            _stepsConsumedThisFrame = 0;
            _moveAccumulator += MoveSpeedCellsPerSecond * Time.deltaTime;
            int stepsThisFrame = 0;
            while (_moveAccumulator >= 1f && _pathQueue.Count > 0 && stepsThisFrame < MaxCellsPerFrame)
            {
                var nextCell = _pathQueue.Dequeue();
                if (_dragOnHead)
                {
                    // 倒车：若下一步将进入紧邻身体，则改为让尾部后退一步
                    var nextBody = _bodyCells.First.Next != null ? _bodyCells.First.Next.Value : _bodyCells.First.Value;
                    if (nextCell == nextBody)
                    {
                        if (!TryReverseOneStep()) break;
                    }
                    else
                    {
                        if (!AdvanceHeadTo(nextCell)) break;
                    }
                }
                else
                {
                    // 尾部倒车：若下一步将进入紧邻身体，则改为让头部前进一步
                    var prevBody = _bodyCells.Last.Previous != null ? _bodyCells.Last.Previous.Value : _bodyCells.Last.Value;
                    if (nextCell == prevBody)
                    {
                        if (!TryReverseFromTail()) break;
                    }
                    else
                    {
                        if (!AdvanceTailTo(nextCell)) break;
                    }
                }
                _moveAccumulator -= 1f;
                stepsThisFrame++;
                _stepsConsumedThisFrame++;
            }

            // 拖动中的可视：使用折线距离定位，严格保持段间距=_grid.CellSize，避免重叠
            if (_dragging && !_consuming)
            {
                UpdateVisualsSmoothDragging();
            }
            else
            {
                // 未拖动时：保持每段对齐到自身格中心
                int idx = 0;
                foreach (var cell in _bodyCells)
                {
                    if (idx >= _segments.Count) break;

                    // UI渲染：使用RectTransform的anchoredPosition
                    var rt = _segments[idx].GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        var worldPos = _grid.CellToWorld(cell);
                        rt.anchoredPosition = new Vector2(worldPos.x, worldPos.y);
                    }
                    idx++;
                }

                // 更新小段位置
                if (EnableSubSegments)
                {
                    UpdateSubSegmentPositions();
                }
            }

            // 移动完成后，更新身体图片
            if (EnableBodySpriteManagement && _bodySpriteManager != null && stepsThisFrame > 0)
            {
                _bodySpriteManager.OnSnakeMoved();
            }
        }

        Coroutine _consumeCoroutine;
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

        public IEnumerator CoConsume(HoleEntity hole, bool fromHead)
        {
            _consuming = true;
            _dragging = false; // 脱离手指控制
            _pathQueue.Clear();
            _moveAccumulator = 0f;
            Vector3 holeCenter = _grid.CellToWorld(hole.Cell);

            // 逐段进入洞并消失，保持身体连续性
            while (_bodyCells.Count > 0)
            {
                Transform segmentToConsume = null;
                Vector2Int consumedCell;

                if (fromHead)
                {
                    consumedCell = _bodyCells.First.Value;
                    _bodyCells.RemoveFirst();
                    if (_segments.Count > 0)
                    {
                        segmentToConsume = _segments[0];
                        _segments.RemoveAt(0);

                        // 同时移除对应的小段
                        if (EnableSubSegments && _subSegments.Count > 0)
                        {
                            _subSegments.RemoveAt(0);
                        }
                    }
                }
                else
                {
                    consumedCell = _bodyCells.Last.Value;
                    _bodyCells.RemoveLast();
                    int last = _segments.Count - 1;
                    if (last >= 0)
                    {
                        segmentToConsume = _segments[last];
                        _segments.RemoveAt(last);

                        // 同时移除对应的小段
                        if (EnableSubSegments && _subSegments.Count > last)
                        {
                            _subSegments.RemoveAt(last);
                        }
                    }
                }

                // 更新当前头尾缓存，防止空引用
                if (_bodyCells.Count > 0)
                {
                    _currentHeadCell = _bodyCells.First.Value;
                    _currentTailCell = _bodyCells.Last.Value;
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
            if (_bodyCells.Count == 0)
            {
                Destroy(gameObject);
            }
        }

        IEnumerator MoveToHoleAndDestroy(Transform segment, Vector3 holeCenter, float duration)
        {
            // 找到对应的小段列表
            List<Transform> subSegmentsToMove = null;
            int segmentIndex = _segments.IndexOf(segment);

            if (EnableSubSegments && segmentIndex >= 0 && segmentIndex < _subSegments.Count)
            {
                subSegmentsToMove = _subSegments[segmentIndex];
            }

            if (subSegmentsToMove != null && subSegmentsToMove.Count > 0)
            {
                // 小段系统：逐个消费小段
                yield return StartCoroutine(ConsumeSubSegmentsSequentially(subSegmentsToMove, holeCenter, duration));
            }
            else
            {
                // 主段系统：消费主段（向后兼容）
                yield return StartCoroutine(ConsumeMainSegment(segment, holeCenter, duration));
            }

            // 销毁主段对象
            Destroy(segment.gameObject);
        }

        IEnumerator ConsumeSubSegmentsSequentially(List<Transform> subSegments, Vector3 holeCenter, float totalDuration)
        {
            // 计算每个小段的消费时间
            float timePerSubSegment = totalDuration / SUB_SEGMENTS_PER_SEGMENT;

            for (int i = 0; i < subSegments.Count; i++)
            {
                if (subSegments[i] != null)
                {
                    // 启动单个小段的消费动画（并行执行）
                    StartCoroutine(ConsumeSingleSubSegment(subSegments[i], holeCenter, timePerSubSegment * 1.5f, i));

                    // 等待一小段时间再开始下一个小段，创造流动效果
                    yield return new WaitForSeconds(timePerSubSegment * 0.6f);
                }
            }

            // 等待最后一个小段完成消费
            yield return new WaitForSeconds(timePerSubSegment * 1.5f);
        }

        IEnumerator ConsumeSingleSubSegment(Transform subSegment, Vector3 holeCenter, float duration, int subSegmentIndex)
        {
            var subRT = subSegment.GetComponent<RectTransform>();
            if (subRT == null) yield break;

            Vector3 startPos = new Vector3(subRT.anchoredPosition.x, subRT.anchoredPosition.y, 0);
            holeCenter = new Vector3(holeCenter.x, holeCenter.y, 0);

            // 移动阶段（70%时间）
            float moveTime = duration * 0.7f;
            float elapsed = 0f;

            while (elapsed < moveTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / moveTime;

                // 使用缓动函数让移动更自然
                float easedT = Mathf.SmoothStep(0f, 1f, t);
                Vector3 currentPos = Vector3.Lerp(startPos, holeCenter, easedT);

                subRT.anchoredPosition = new Vector2(currentPos.x, currentPos.y);
                yield return null;
            }

            // 确保到达洞中心
            subRT.anchoredPosition = new Vector2(holeCenter.x, holeCenter.y);

            // 消失阶段（30%时间）
            float fadeTime = duration * 0.3f;
            var img = subSegment.GetComponent<Image>();
            Color originalColor = img != null ? img.color : Color.white;

            elapsed = 0f;
            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                float fadeT = elapsed / fadeTime;

                // 缩放和淡出
                subSegment.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, fadeT);
                if (img != null)
                {
                    img.color = new Color(originalColor.r, originalColor.g, originalColor.b, 1f - fadeT);
                }

                yield return null;
            }

            // 销毁小段
            Destroy(subSegment.gameObject);
        }

        IEnumerator ConsumeMainSegment(Transform segment, Vector3 holeCenter, float duration)
        {
            Vector3 startPos;
            RectTransform segmentRT = segment.GetComponent<RectTransform>();
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
            float fadeTime = duration * 0.3f;
            float fadeElapsed = 0f;
            var img = segment.GetComponent<Image>();
            Color originalColor = img != null ? img.color : Color.white;

            while (fadeElapsed < fadeTime)
            {
                fadeElapsed += Time.deltaTime;
                float fadeT = fadeElapsed / fadeTime;

                if (img != null)
                {
                    img.color = new Color(originalColor.r, originalColor.g, originalColor.b, 1f - fadeT);
                }
                segment.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, fadeT);

                yield return null;
            }
        }

        /// <summary>
        /// 让剩余的蛇身朝洞方向移动一格，保持身体连续性
        /// </summary>
        IEnumerator MoveRemainingBodyTowardHole(Vector2Int consumedCell, Vector2Int holeCell, float duration, bool fromHead)
        {
            if (_bodyCells.Count == 0) yield break;

            Vector2Int direction = Vector2Int.zero;

            if (fromHead)
            {
                // 从头部消费：剩余身体朝被消费的头部位置移动
                Vector2Int currentHeadCell = _bodyCells.First.Value;
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
                        _bodyCells.AddFirst(newHeadCell);

                        // 更新头尾缓存
                        _currentHeadCell = _bodyCells.First.Value;
                        _currentTailCell = _bodyCells.Last.Value;

                        // 平滑移动所有身体段到新位置
                        yield return StartCoroutine(SmoothMoveAllSegments(duration));
                    }
                }
            }
            else
            {
                // 从尾部消费：整条蛇朝被消费的尾部位置移动（类似倒车）
                Vector2Int currentTailCell = _bodyCells.Last.Value;
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
                        _bodyCells.AddLast(newTailCell);
                        _bodyCells.RemoveFirst();

                        // 更新头尾缓存
                        _currentHeadCell = _bodyCells.First.Value;
                        _currentTailCell = _bodyCells.Last.Value;

                        // 平滑移动所有身体段到新位置
                        yield return StartCoroutine(SmoothMoveAllSegments(duration));

                        // 长度改变后，更新身体图片
                        if (EnableBodySpriteManagement && _bodySpriteManager != null)
                        {
                            _bodySpriteManager.OnSnakeLengthChanged();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 平滑移动所有身体段到对应的新格子位置
        /// </summary>
        IEnumerator SmoothMoveAllSegments(float duration)
        {
            if (_segments.Count == 0 || _bodyCells.Count == 0) yield break;

            List<Vector3> startPositions = new List<Vector3>();
            List<Vector3> targetPositions = new List<Vector3>();

            // 收集所有段的起始和目标位置
            int segmentIndex = 0;
            foreach (var cell in _bodyCells)
            {
                if (segmentIndex >= _segments.Count) break;

                Vector3 startPos;
                Vector3 targetPos = _grid.CellToWorld(cell);

                var rt = _segments[segmentIndex].GetComponent<RectTransform>();
                if (rt != null)
                {
                    startPos = new Vector3(rt.anchoredPosition.x, rt.anchoredPosition.y, 0);
                }
                else
                {
                    startPos = _segments[segmentIndex].position;
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

                for (int i = 0; i < startPositions.Count && i < _segments.Count; i++)
                {
                    Vector3 currentPos = Vector3.Lerp(startPositions[i], targetPositions[i], t);

                    var rt = _segments[i].GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchoredPosition = new Vector2(currentPos.x, currentPos.y);
                    }
                }

                yield return null;
            }

            // 确保所有段都到达目标位置
            segmentIndex = 0;
            foreach (var cell in _bodyCells)
            {
                if (segmentIndex >= _segments.Count) break;

                Vector3 finalPos = _grid.CellToWorld(cell);
                var rt = _segments[segmentIndex].GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchoredPosition = new Vector2(finalPos.x, finalPos.y);
                }
                segmentIndex++;
            }

            // 更新小段位置
            if (EnableSubSegments)
            {
                UpdateSubSegmentPositions();
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
            // 检查是否被墙体阻挡（洞本身不算阻挡）
            if (_entityManager != null)
            {
                var entities = _entityManager.GetAt(cell);
                if (entities != null)
                {
                    foreach (var entity in entities)
                    {
                        if (entity is WallEntity) return true;
                        // 洞不算阻挡，可以通过
                    }
                }
            }
            return false;
        }

        void UpdateVisualsSmoothDragging()
        {
            float frac = Mathf.Clamp01(_moveAccumulator);
            Vector3 finger = ScreenToWorld(Input.mousePosition);
            if (_dragOnHead)
            {
                Vector3 headA = _grid.CellToWorld(_currentHeadCell);
                Vector3 headVisual;
                if (_pathQueue.Count > 0)
                {
                    Vector3 headB = _grid.CellToWorld(_pathQueue.Peek());
                    headVisual = Vector3.Lerp(headA, headB, frac);
                }
                else
                {
                    // 单格内自由拖动：限制在当前格AABB内，且仅沿主方向自由
                    headVisual = ClampWorldToCellBounds(finger, _currentHeadCell);
                    var center = _grid.CellToWorld(_currentHeadCell);
                    if (_dragAxis == DragAxis.X) headVisual.y = center.y; else if (_dragAxis == DragAxis.Y) headVisual.x = center.x;
                }
                // 构建折线：headVisual -> (body First.Next ... Last)
                List<Vector3> pts = new List<Vector3>(_segments.Count + 2);
                pts.Add(headVisual);
                var it = _bodyCells.First;
                if (it != null) it = it.Next; // skip head cell
                while (it != null)
                {
                    pts.Add(_grid.CellToWorld(it.Value));
                    it = it.Next;
                }
                float spacing = _grid.CellSize;
                for (int i = 0; i < _segments.Count; i++)
                {
                    Vector3 p = GetPointAlongPolyline(pts, i * spacing);

                    var rt = _segments[i].GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchoredPosition = new Vector2(p.x, p.y);
                    }
                }
            }
            else
            {
                // 拖尾：构建折线： tailVisual -> (body Last.Previous ... First)
                Vector3 tailA = _grid.CellToWorld(_currentTailCell);
                Vector3 tailVisual;
                if (_pathQueue.Count > 0)
                {
                    Vector3 tailB = _grid.CellToWorld(_pathQueue.Peek());
                    tailVisual = Vector3.Lerp(tailA, tailB, frac);
                }
                else
                {
                    // 单格内自由拖动：限制在当前格AABB内，且仅沿主方向自由
                    tailVisual = ClampWorldToCellBounds(finger, _currentTailCell);
                    var center = _grid.CellToWorld(_currentTailCell);
                    if (_dragAxis == DragAxis.X) tailVisual.y = center.y; else if (_dragAxis == DragAxis.Y) tailVisual.x = center.x;
                }

                // 构建折线：从尾部拖动位置开始，向头部方向延伸
                List<Vector3> pts = new List<Vector3>(_segments.Count);
                pts.Add(tailVisual); // 尾部拖动位置

                // 添加身体段位置（从尾部向头部）
                var it = _bodyCells.Last;
                if (it != null) it = it.Previous; // 跳过尾部cell（已经用tailVisual代替）
                while (it != null)
                {
                    pts.Add(_grid.CellToWorld(it.Value));
                    it = it.Previous;
                }

                float spacing = _grid.CellSize;
                // 从尾部开始分布，索引0对应尾部段
                for (int i = 0; i < _segments.Count; i++)
                {
                    int segmentIndex = _segments.Count - 1 - i; // 倒序：尾部段在前
                    Vector3 p = GetPointAlongPolyline(pts, i * spacing);

                    var rt = _segments[segmentIndex].GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchoredPosition = new Vector2(p.x, p.y);
                    }
                }
            }

            // 更新小段位置
            if (EnableSubSegments)
            {
                UpdateSubSegmentPositions();
            }
        }

        Vector3 GetPointAlongPolyline(List<Vector3> pts, float distance)
        {
            if (pts.Count == 0) return Vector3.zero;
            if (pts.Count == 1) return pts[0];

            float remaining = distance;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector3 a = pts[i];
                Vector3 b = pts[i + 1];
                float segLen = Vector3.Distance(a, b);
                if (remaining <= segLen)
                {
                    float t = segLen <= 0.0001f ? 0f : (remaining / segLen);
                    return Vector3.Lerp(a, b, t);
                }
                remaining -= segLen;
            }

            // 超出折线长度时，沿着最后一段的方向继续延伸
            if (pts.Count >= 2)
            {
                Vector3 lastA = pts[pts.Count - 2];
                Vector3 lastB = pts[pts.Count - 1];
                Vector3 direction = (lastB - lastA).normalized;

                // remaining 现在是超出的距离
                return lastB + direction * remaining;
            }

            // Fallback: 返回最后一点
            return pts[pts.Count - 1];
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
            // 构建当前身体占据的格集合（基于离散cells）
            var occupied = new HashSet<Vector2Int>(_bodyCells);
            var tailCell = _bodyCells.Last.Value; // 允许移动到尾的格
            while (cur != to)
            {
                cur += step;
                if (occupied.Contains(cur) && cur != tailCell) return false;
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
            if (horizFirst)
            {
                for (int i = 0; i < Mathf.Abs(dx); i++) { cur = new Vector2Int(cur.x + stepx, cur.y); _pathBuildBuffer.Add(ClampInside(cur)); }
                for (int i = 0; i < Mathf.Abs(dy); i++) { cur = new Vector2Int(cur.x, cur.y + stepy); _pathBuildBuffer.Add(ClampInside(cur)); }
            }
            else
            {
                for (int i = 0; i < Mathf.Abs(dy); i++) { cur = new Vector2Int(cur.x, cur.y + stepy); _pathBuildBuffer.Add(ClampInside(cur)); }
                for (int i = 0; i < Mathf.Abs(dx); i++) { cur = new Vector2Int(cur.x + stepx, cur.y); _pathBuildBuffer.Add(ClampInside(cur)); }
            }
            for (int i = 0; i < _pathBuildBuffer.Count; i++)
            {
                _pathQueue.Enqueue(_pathBuildBuffer[i]);
            }
        }

        bool AdvanceHeadTo(Vector2Int nextCell)
        {
            // 必须相邻
            if (Manhattan(_currentHeadCell, nextCell) != 1) return false;
            // 检查网格边界
            if (!_grid.IsInside(nextCell)) return false;
            // 检查实体阻挡
            if (_entityManager != null && _entityManager.IsBlocked(nextCell)) return false;
            // 占用校验：允许进入原尾
            var tailCell = _bodyCells.Last.Value;
            if (_bodyCells.Contains(nextCell) && nextCell != tailCell) return false;
            _bodyCells.AddFirst(nextCell);
            _bodyCells.RemoveLast();
            _currentHeadCell = nextCell;
            _currentTailCell = _bodyCells.Last.Value;

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
            // 检查实体阻挡
            if (_entityManager != null && _entityManager.IsBlocked(nextCell)) return false;
            // 占用校验：允许进入原头
            var headCell = _bodyCells.First.Value;
            if (_bodyCells.Contains(nextCell) && nextCell != headCell) return false;
            _bodyCells.AddLast(nextCell);
            _bodyCells.RemoveFirst();
            _currentTailCell = nextCell;
            _currentHeadCell = _bodyCells.First.Value;

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
                if (_entityManager != null && _entityManager.IsBlocked(next)) continue;
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
                if (_entityManager != null && _entityManager.IsBlocked(nextHead)) continue;
                if (!IsCellFree(nextHead)) continue;
                return AdvanceHeadTo(nextHead);
            }
            return false;
        }

        bool IsCellFree(Vector2Int cell)
        {
            // 不允许进入身体占用格；允许进入当前头（在反向时不需要，但保持一致性）
            foreach (var c in _bodyCells) { if (c == cell) return false; }
            return true;
        }

        void SnapToGrid()
        {
            if (_bodyCells == null)
                return;
            if (_bodyCells.Count == 0)
                return;


            // 基于离散cells统一吸附
            int index = 0;
            foreach (var cell in _bodyCells)
            {
                if (index >= _segments.Count) break;

                var rt = _segments[index].GetComponent<RectTransform>();
                if (rt != null)
                {
                    var worldPos = _grid.CellToWorld(cell);
                    rt.anchoredPosition = new Vector2(worldPos.x, worldPos.y);
                }

                index++;
            }
            _currentHeadCell = _bodyCells.First.Value;
            _currentTailCell = _bodyCells.Last.Value;
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

        int Manhattan(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        bool TryPickHeadOrTail(Vector3 world, out bool onHead)
        {
            onHead = false;
            if (_segments.Count == 0) return false;

            Vector3 head, tail;

            var headRT = _segments[0].GetComponent<RectTransform>();
            var tailRT = _segments[_segments.Count - 1].GetComponent<RectTransform>();
            head = new Vector3(headRT.anchoredPosition.x, headRT.anchoredPosition.y, 0f);
            tail = new Vector3(tailRT.anchoredPosition.x, tailRT.anchoredPosition.y, 0f);

            float headDist = Vector3.Distance(world, head);
            float tailDist = Vector3.Distance(world, tail);
            if (Mathf.Min(headDist, tailDist) > _grid.CellSize * 0.8f) return false;
            onHead = headDist <= tailDist;
            return true;
        }

        Vector3 ScreenToWorld(Vector3 screen)
        {
            // UI渲染模式：使用UI坐标转换
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                var rect = transform.parent as RectTransform; // GridContainer
                if (rect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, canvas.worldCamera, out Vector2 localPoint))
                {
                    return new Vector3(localPoint.x, localPoint.y, 0f);
                }
            }

            // Fallback: 改进的屏幕到UI坐标转换
            var gridContainer = transform.parent as RectTransform;
            if (gridContainer != null)
            {
                var canvasRT = gridContainer.GetComponentInParent<Canvas>().GetComponent<RectTransform>();
                if (canvasRT != null)
                {
                    // 将屏幕坐标转换为Canvas坐标
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, screen, null, out Vector2 canvasPoint))
                    {
                        // 再转换为GridContainer内的本地坐标
                        Vector2 localPoint = canvasPoint - (Vector2)gridContainer.anchoredPosition;
                        return new Vector3(localPoint.x, localPoint.y, 0f);
                    }
                }
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
            if (!ShowDebugStats) return;
            GUI.color = Color.white;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            GUILayout.BeginArea(new Rect(10, 10, 400, 200), GUI.skin.box);
            GUILayout.Label($"Queue: {_pathQueue.Count}", style);
            GUILayout.Label($"Accumulator: {_moveAccumulator:F2}", style);
            GUILayout.Label($"Steps/frame: {_stepsConsumedThisFrame}", style);
            GUILayout.Label($"Head: {_currentHeadCell} Tail: {_currentTailCell}", style);
            GUILayout.EndArea();
        }

        /// <summary>
        /// 简化的小段系统：小段跟随大段位置，只在转弯时计算旋转
        /// </summary>
        void ArrangeSubSegmentsWithPositionBasedTurn(List<Transform> subSegments, int segmentIndex, Vector3 centerPos, Vector3 inDirection, Vector3 outDirection)
        {
            // 计算小段在大段内的分布
            float maxOffset = _grid.CellSize * 0.4f;

            for (int i = 0; i < subSegments.Count; i++)
            {
                var rt = subSegments[i].GetComponent<RectTransform>();
                if (rt == null) continue;

                // 1. 小段跟随大段位置分布（简单的线性分布）
                float t = (float)i / (SUB_SEGMENTS_PER_SEGMENT - 1); // 0, 0.25, 0.5, 0.75, 1
                float offset = (t - 0.5f) * maxOffset * 2f; // 在 ±maxOffset 范围内分布

                // 根据转弯方向调整小段位置（保持在大段范围内）
                Vector3 baseDirection = GetGridDirection(inDirection); // 标准化方向
                Vector3 subSegmentPos = centerPos + baseDirection * offset;

                // 设置小段位置
                rt.anchoredPosition = new Vector2(subSegmentPos.x, subSegmentPos.y);

                // 2. 只计算旋转角度（基于小段在转弯路径中的相对位置）
                float rotation = CalculateSimpleRotationForSubSegment(i, inDirection, outDirection);
                rt.rotation = Quaternion.Euler(0, 0, rotation);
            }
        }

        /// <summary>
        /// 简化的小段旋转计算（基于小段在转弯中的索引位置）
        /// </summary>
        float CalculateSimpleRotationForSubSegment(int subSegmentIndex, Vector3 inDirection, Vector3 outDirection)
        {
            // 检查是否为90度转弯
            bool isTurning = Vector3.Dot(inDirection, outDirection) < 0.1f;
            if (!isTurning)
            {
                return 0f; // 直线移动时不旋转
            }

            // 根据小段索引计算渐进式旋转
            // subSegmentIndex: 0=头部侧, 4=尾部侧
            // 尾部侧的小段先开始旋转，头部侧的小段后旋转

            switch (subSegmentIndex)
            {
                case 0: return 0f;   // 头部侧，不旋转
                case 1: return 0f;   // 接近头部，不旋转
                case 2: return 0f;   // 中间，不旋转（或轻微旋转）
                case 3: return 30f;  // 接近尾部，开始旋转
                case 4: return 60f;  // 尾部侧，旋转更多
                default: return 0f;
            }
        }

        /// <summary>
        /// 根据小段的实际位置单独计算旋转角度
        /// </summary>
        float CalculateIndividualRotationFromPosition(Vector2 virtualPos, Vector3 inDirection, Vector3 outDirection)
        {
            // 确保坐标对齐到整数（小格子中心）
            float alignedX = Mathf.Round(virtualPos.x);
            float alignedY = Mathf.Round(virtualPos.y);

            // 判断是否在大格子的中心区域 (1,1) 到 (3,3)
            bool isInCenterRegion = (alignedX >= 1f && alignedX <= 3f && alignedY >= 1f && alignedY <= 3f);

            // 只有当小段确实经过大格子中心点(2,2)之后，才开始计算转弯
            if (!HasPassedCenterPoint(alignedX, alignedY, inDirection, outDirection))
            {
                return 0f; // 还没经过中心点，不旋转
            }

            // 确定转弯类型
            bool isVerticalToHorizontal = (Mathf.Abs(inDirection.y) > 0.5f && Mathf.Abs(outDirection.x) > 0.5f);
            bool isHorizontalToVertical = (Mathf.Abs(inDirection.x) > 0.5f && Mathf.Abs(outDirection.y) > 0.5f);

            if (isVerticalToHorizontal)
            {
                return CalculateVerticalToHorizontalRotation(new Vector2(alignedX, alignedY), inDirection, outDirection);
            }
            else if (isHorizontalToVertical)
            {
                return CalculateHorizontalToVerticalRotation(new Vector2(alignedX, alignedY), inDirection, outDirection);
            }

            return 0f; // 直线移动，无旋转
        }

        /// <summary>
        /// 检查小段是否已经通过大格子的中心点
        /// </summary>
        bool HasPassedCenterPoint(float virtualX, float virtualY, Vector3 inDirection, Vector3 outDirection)
        {
            // 中心点是 (2,2)
            Vector2 centerPoint = new Vector2(2f, 2f);

            // 检查小段是否在离开方向上已经越过中心点
            if (Mathf.Abs(inDirection.y) > 0.5f) // 竖直进入
            {
                if (inDirection.y > 0) // 从上进入
                {
                    // 只有当Y坐标 <= 2 时，才算通过了中心点
                    return virtualY <= centerPoint.y;
                }
                else // 从下进入
                {
                    // 只有当Y坐标 >= 2 时，才算通过了中心点
                    return virtualY >= centerPoint.y;
                }
            }
            else if (Mathf.Abs(inDirection.x) > 0.5f) // 水平进入
            {
                if (inDirection.x > 0) // 从右进入
                {
                    // 只有当X坐标 <= 2 时，才算通过了中心点
                    return virtualX <= centerPoint.x;
                }
                else // 从左进入
                {
                    // 只有当X坐标 >= 2 时，才算通过了中心点
                    return virtualX >= centerPoint.x;
                }
            }

            return false; // 默认不旋转
        }

        /// <summary>
        /// 计算小段在25格子坐标系统中的虚拟位置（统一从头部到尾部方向）
        /// </summary>
        Vector2 CalculateSubSegmentVirtualPosition(int subSegmentIndex, Vector3 inDirection, Vector3 outDirection)
        {
            // 统一方向：subSegmentIndex = 0 代表最接近头部的小段，4 代表最接近尾部的小段
            // 在转弯时，头部方向是 inDirection 的反方向，尾部方向是 outDirection

            // 根据进入和离开方向确定路径
            bool isVerticalToHorizontal = (Mathf.Abs(inDirection.y) > 0.5f && Mathf.Abs(outDirection.x) > 0.5f);
            bool isHorizontalToVertical = (Mathf.Abs(inDirection.x) > 0.5f && Mathf.Abs(outDirection.y) > 0.5f);

            if (isVerticalToHorizontal)
            {
                // 竖直到水平转弯
                if (inDirection.y > 0 && outDirection.x > 0) // 从上到右
                {
                    // 头部→尾部路径：(2,4) → (2,3) → (2,2) → (3,2) → (4,2)
                    // subSegmentIndex: 0=头部侧, 4=尾部侧
                    switch (subSegmentIndex)
                    {
                        case 0: return new Vector2(2f, 4f); // 最接近头部（进入侧）
                        case 1: return new Vector2(2f, 3f); // 接近中心
                        case 2: return new Vector2(2f, 2f); // 转弯中心点
                        case 3: return new Vector2(3f, 2f); // 朝向尾部
                        case 4: return new Vector2(4f, 2f); // 最接近尾部（离开侧）
                    }
                }
                else if (inDirection.y > 0 && outDirection.x < 0) // 从上到左
                {
                    // 头部→尾部路径：(2,4) → (2,3) → (2,2) → (1,2) → (0,2)
                    switch (subSegmentIndex)
                    {
                        case 0: return new Vector2(2f, 4f); // 最接近头部
                        case 1: return new Vector2(2f, 3f); // 接近中心
                        case 2: return new Vector2(2f, 2f); // 中心点
                        case 3: return new Vector2(1f, 2f); // 朝向尾部
                        case 4: return new Vector2(0f, 2f); // 最接近尾部
                    }
                }
                else if (inDirection.y < 0 && outDirection.x > 0) // 从下到右
                {
                    // 头部→尾部路径：(2,0) → (2,1) → (2,2) → (3,2) → (4,2)
                    switch (subSegmentIndex)
                    {
                        case 0: return new Vector2(2f, 0f); // 最接近头部
                        case 1: return new Vector2(2f, 1f); // 接近中心
                        case 2: return new Vector2(2f, 2f); // 中心点
                        case 3: return new Vector2(3f, 2f); // 朝向尾部
                        case 4: return new Vector2(4f, 2f); // 最接近尾部
                    }
                }
                else if (inDirection.y < 0 && outDirection.x < 0) // 从下到左
                {
                    // 头部→尾部路径：(2,0) → (2,1) → (2,2) → (1,2) → (0,2)
                    switch (subSegmentIndex)
                    {
                        case 0: return new Vector2(2f, 0f); // 最接近头部
                        case 1: return new Vector2(2f, 1f); // 接近中心
                        case 2: return new Vector2(2f, 2f); // 中心点
                        case 3: return new Vector2(1f, 2f); // 朝向尾部
                        case 4: return new Vector2(0f, 2f); // 最接近尾部
                    }
                }
            }
            else if (isHorizontalToVertical)
            {
                // 水平到竖直转弯
                if (inDirection.x > 0 && outDirection.y > 0) // 从右到上
                {
                    // 头部→尾部路径：(4,2) → (3,2) → (2,2) → (2,3) → (2,4)
                    switch (subSegmentIndex)
                    {
                        case 0: return new Vector2(4f, 2f); // 最接近头部
                        case 1: return new Vector2(3f, 2f); // 接近中心
                        case 2: return new Vector2(2f, 2f); // 中心点
                        case 3: return new Vector2(2f, 3f); // 朝向尾部
                        case 4: return new Vector2(2f, 4f); // 最接近尾部
                    }
                }
                else if (inDirection.x > 0 && outDirection.y < 0) // 从右到下
                {
                    // 头部→尾部路径：(4,2) → (3,2) → (2,2) → (2,1) → (2,0)
                    switch (subSegmentIndex)
                    {
                        case 0: return new Vector2(4f, 2f); // 最接近头部
                        case 1: return new Vector2(3f, 2f); // 接近中心
                        case 2: return new Vector2(2f, 2f); // 中心点
                        case 3: return new Vector2(2f, 1f); // 朝向尾部
                        case 4: return new Vector2(2f, 0f); // 最接近尾部
                    }
                }
                else if (inDirection.x < 0 && outDirection.y > 0) // 从左到上
                {
                    // 头部→尾部路径：(0,2) → (1,2) → (2,2) → (2,3) → (2,4)
                    switch (subSegmentIndex)
                    {
                        case 0: return new Vector2(0f, 2f); // 最接近头部
                        case 1: return new Vector2(1f, 2f); // 接近中心
                        case 2: return new Vector2(2f, 2f); // 中心点
                        case 3: return new Vector2(2f, 3f); // 朝向尾部
                        case 4: return new Vector2(2f, 4f); // 最接近尾部
                    }
                }
                else if (inDirection.x < 0 && outDirection.y < 0) // 从左到下
                {
                    // 头部→尾部路径：(0,2) → (1,2) → (2,2) → (2,1) → (2,0)
                    switch (subSegmentIndex)
                    {
                        case 0: return new Vector2(0f, 2f); // 最接近头部
                        case 1: return new Vector2(1f, 2f); // 接近中心
                        case 2: return new Vector2(2f, 2f); // 中心点
                        case 3: return new Vector2(2f, 1f); // 朝向尾部
                        case 4: return new Vector2(2f, 0f); // 最接近尾部
                    }
                }
            }

            // 默认直线排列（沿水平线，头部→尾部方向）
            // subSegmentIndex: 0=头部侧, 4=尾部侧
            float step = 1f;
            float startOffset = -2f; // 从左侧开始
            return new Vector2(2f + startOffset + subSegmentIndex * step, 2f); // (0,2) → (1,2) → (2,2) → (3,2) → (4,2)
        }

        /// <summary>
        /// 根据进入方向确定进入点
        /// </summary>
        Vector2 GetVirtualEntryPoint(Vector3 inDirection)
        {
            if (Mathf.Abs(inDirection.x) > 0.5f)
            {
                // 水平进入
                return inDirection.x > 0 ? new Vector2(0, 2) : new Vector2(4, 2); // 从左进入或从右进入
            }
            else
            {
                // 垂直进入
                return inDirection.y > 0 ? new Vector2(2, 0) : new Vector2(2, 4); // 从下进入或从上进入
            }
        }

        /// <summary>
        /// 计算虚拟网格内的最短路径（只能沿横竖线移动）
        /// </summary>
        List<Vector2> CalculateVirtualGridPath(Vector2 entryPoint, Vector2 targetVirtualPos)
        {
            var path = new List<Vector2>();
            path.Add(entryPoint);

            // 当前位置
            Vector2 current = entryPoint;
            Vector2 target = new Vector2(Mathf.Round(targetVirtualPos.x), Mathf.Round(targetVirtualPos.y));

            // 限制目标点到有效路径上
            target = ConstrainToValidPath(target);

            // 如果已经在目标位置，直接返回
            if (Vector2.Distance(current, target) < 0.1f)
                return path;

            // 路径规划：先到中心点(2,2)，再到目标点
            Vector2 center = new Vector2(2, 2);

            // 第一段：从进入点到中心点
            if (Vector2.Distance(current, center) > 0.1f)
            {
                var pathToCenter = GetPathBetweenPoints(current, center);
                path.AddRange(pathToCenter);
                current = center;
            }

            // 第二段：从中心点到目标点
            if (Vector2.Distance(current, target) > 0.1f)
            {
                var pathToTarget = GetPathBetweenPoints(current, target);
                path.AddRange(pathToTarget);
            }

            return path;
        }

        /// <summary>
        /// 将目标点限制到有效路径上（横线或竖线）
        /// </summary>
        Vector2 ConstrainToValidPath(Vector2 target)
        {
            // 检查是否在横线上
            if (Mathf.Abs(target.y - 2f) < 0.1f)
            {
                // 在横线上，限制x坐标
                float x = Mathf.Clamp(target.x, 0f, 4f);
                return new Vector2(Mathf.Round(x), 2f);
            }
            // 检查是否在竖线上
            else if (Mathf.Abs(target.x - 2f) < 0.1f)
            {
                // 在竖线上，限制y坐标
                float y = Mathf.Clamp(target.y, 0f, 4f);
                return new Vector2(2f, Mathf.Round(y));
            }
            else
            {
                // 不在有效路径上，选择最近的有效点
                float distToHorizontal = Mathf.Abs(target.y - 2f);
                float distToVertical = Mathf.Abs(target.x - 2f);

                if (distToHorizontal < distToVertical)
                {
                    // 投影到横线
                    float x = Mathf.Clamp(target.x, 0f, 4f);
                    return new Vector2(Mathf.Round(x), 2f);
                }
                else
                {
                    // 投影到竖线
                    float y = Mathf.Clamp(target.y, 0f, 4f);
                    return new Vector2(2f, Mathf.Round(y));
                }
            }
        }

        /// <summary>
        /// 获取两点之间的路径（只能沿横竖线）
        /// </summary>
        List<Vector2> GetPathBetweenPoints(Vector2 from, Vector2 to)
        {
            var path = new List<Vector2>();

            // 如果在同一条线上，直接连接
            if (Mathf.Abs(from.y - to.y) < 0.1f)
            {
                // 在同一水平线上
                float startX = Mathf.Min(from.x, to.x);
                float endX = Mathf.Max(from.x, to.x);
                for (float x = startX + 1; x <= endX; x++)
                {
                    path.Add(new Vector2(x, from.y));
                }
            }
            else if (Mathf.Abs(from.x - to.x) < 0.1f)
            {
                // 在同一竖直线上
                float startY = Mathf.Min(from.y, to.y);
                float endY = Mathf.Max(from.y, to.y);
                for (float y = startY + 1; y <= endY; y++)
                {
                    path.Add(new Vector2(from.x, y));
                }
            }

            return path;
        }

        /// <summary>
        /// 更新小段路径队列（在拖拽过程中调用）
        /// </summary>
        void UpdateSubSegmentPaths(Vector3 dragPosition)
        {
            if (!EnableSubSegments || _subSegmentPathQueues.Count == 0)
                return;

            // 获取拖拽的大段索引
            int dragSegmentIndex = _dragOnHead ? 0 : _segments.Count - 1;

            if (dragSegmentIndex >= _subSegmentPathQueues.Count)
                return;

            // 计算大段的中心位置
            Vector3 segmentCenter = _segments[dragSegmentIndex].GetComponent<RectTransform>().anchoredPosition;

            // 将拖拽位置转换为虚拟网格坐标
            Vector2 targetVirtualPos = WorldToVirtualGrid(dragPosition, segmentCenter);

            // 确定进入方向（基于前一段的方向）
            Vector3 inDirection = GetSegmentInDirection(dragSegmentIndex);

            // 确定进入点
            Vector2 entryPoint = GetVirtualEntryPoint(inDirection);

            // 计算虚拟网格路径
            var virtualPath = CalculateVirtualGridPath(entryPoint, targetVirtualPos);

            // 为每个小段分配路径点
            DistributePathToSubSegments(dragSegmentIndex, virtualPath);
        }

        /// <summary>
        /// 获取段的进入方向
        /// </summary>
        Vector3 GetSegmentInDirection(int segmentIndex)
        {
            if (_dragOnHead && segmentIndex < _segments.Count - 1)
            {
                // 头部拖拽，使用下一段的方向
                var currentPos = _segments[segmentIndex].GetComponent<RectTransform>().anchoredPosition;
                var nextPos = _segments[segmentIndex + 1].GetComponent<RectTransform>().anchoredPosition;
                return (currentPos - nextPos).normalized;
            }
            else if (!_dragOnHead && segmentIndex > 0)
            {
                // 尾部拖拽，使用前一段的方向
                var currentPos = _segments[segmentIndex].GetComponent<RectTransform>().anchoredPosition;
                var prevPos = _segments[segmentIndex - 1].GetComponent<RectTransform>().anchoredPosition;
                return (currentPos - prevPos).normalized;
            }

            // 默认水平方向
            return Vector3.right;
        }

        /// <summary>
        /// 将虚拟路径分配给小段
        /// </summary>
        void DistributePathToSubSegments(int segmentIndex, List<Vector2> virtualPath)
        {
            if (segmentIndex >= _subSegmentPathQueues.Count)
                return;

            var pathQueues = _subSegmentPathQueues[segmentIndex];

            // 清空现有路径
            for (int i = 0; i < pathQueues.Count; i++)
            {
                pathQueues[i].Clear();
            }

            // 如果路径太短，直接返回
            if (virtualPath.Count < SUB_SEGMENTS_PER_SEGMENT)
                return;

            // 将路径均匀分配给5个小段
            for (int i = 0; i < SUB_SEGMENTS_PER_SEGMENT; i++)
            {
                float t = (float)i / (SUB_SEGMENTS_PER_SEGMENT - 1);
                int pathIndex = Mathf.RoundToInt(t * (virtualPath.Count - 1));
                pathIndex = Mathf.Clamp(pathIndex, 0, virtualPath.Count - 1);

                // 从该点开始，为小段添加路径
                for (int j = pathIndex; j < virtualPath.Count; j++)
                {
                    pathQueues[i].Enqueue(virtualPath[j]);
                }
            }
        }

        /// <summary>
        /// 消费小段路径队列（类似大段的路径消费逻辑）
        /// </summary>
        void ConsumeSubSegmentPaths()
        {
            if (!EnableSubSegments || _subSegmentPathQueues.Count == 0)
                return;

            // 只处理被拖拽的段
            int dragSegmentIndex = _dragOnHead ? 0 : _segments.Count - 1;

            if (dragSegmentIndex >= _subSegmentPathQueues.Count || dragSegmentIndex >= _subSegments.Count)
                return;

            var pathQueues = _subSegmentPathQueues[dragSegmentIndex];
            var subSegmentList = _subSegments[dragSegmentIndex];
            var mainSegmentCenter = _segments[dragSegmentIndex].GetComponent<RectTransform>().anchoredPosition;

            // 为每个小段消费一个路径点
            for (int i = 0; i < pathQueues.Count && i < subSegmentList.Count; i++)
            {
                if (pathQueues[i].Count > 0)
                {
                    var nextVirtualPos = pathQueues[i].Dequeue();
                    var worldPos = VirtualGridToWorld(nextVirtualPos, mainSegmentCenter);

                    var rt = subSegmentList[i].GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchoredPosition = new Vector2(worldPos.x, worldPos.y);
                    }
                }
            }
        }



        /// <summary>
        /// 简化的直线排列：小段跟随大段位置，无旋转
        /// </summary>
        void ArrangeSubSegmentsInLineWithRotation(List<Transform> subSegments, int segmentIndex, Vector3 centerPos, Vector3 direction)
        {
            float maxOffset = _grid.CellSize * 0.4f;

            for (int i = 0; i < subSegments.Count; i++)
            {
                // 小段沿大段方向线性分布
                float t = (float)i / (SUB_SEGMENTS_PER_SEGMENT - 1); // 0, 0.25, 0.5, 0.75, 1
                float offset = (t - 0.5f) * maxOffset * 2f; // 在 ±maxOffset 范围内分布
                Vector3 pos = centerPos + direction * offset;

                var rt = subSegments[i].GetComponent<RectTransform>();
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
        /// 重置小段的旋转状态（用于直线移动）
        /// </summary>
        void ResetRotationState(int segmentIndex)
        {
            if (!_subSegmentRotations.ContainsKey(segmentIndex))
                return;

            var rotations = _subSegmentRotations[segmentIndex];
            for (int i = 0; i < SUB_SEGMENTS_PER_SEGMENT; i++)
            {
                rotations[i] = 0f; // 重置为0度
            }

            // 移除转弯中心记录
            if (_turnCenters.ContainsKey(segmentIndex))
            {
                _turnCenters.Remove(segmentIndex);
            }
        }

        /// <summary>
        /// 将世界坐标转换为25格子虚拟坐标系统
        /// </summary>
        Vector2 WorldToVirtualGrid(Vector3 worldPos, Vector3 gridCenter)
        {
            // 计算相对于格子中心的偏移
            Vector3 offset = worldPos - gridCenter;

            // 转换为虚拟格子坐标 (0-4, 0-4)
            float virtualX = (offset.x / _grid.CellSize + 0.5f) * VIRTUAL_GRID_SIZE;
            float virtualY = (offset.y / _grid.CellSize + 0.5f) * VIRTUAL_GRID_SIZE;

            return new Vector2(virtualX, virtualY);
        }

        /// <summary>
        /// 将25格子虚拟坐标转换为世界坐标（确保对齐到小格子中心点）
        /// </summary>
        Vector3 VirtualGridToWorld(Vector2 virtualPos, Vector3 gridCenter)
        {
            // 确保虚拟坐标对齐到整数（小格子中心点）
            float alignedX = Mathf.Round(virtualPos.x);
            float alignedY = Mathf.Round(virtualPos.y);

            // 转换为相对偏移 (-0.5 到 +0.5)
            float offsetX = (alignedX / VIRTUAL_GRID_SIZE - 0.5f) * _grid.CellSize;
            float offsetY = (alignedY / VIRTUAL_GRID_SIZE - 0.5f) * _grid.CellSize;

            return gridCenter + new Vector3(offsetX, offsetY, 0f);
        }

        /// <summary>
        /// 根据25格子坐标计算旋转角度
        /// </summary>
        float CalculateRotationFromVirtualCoordinate(Vector2 virtualPos, Vector3 inDirection, Vector3 outDirection)
        {
            // 确定移动方向类型
            bool isVerticalToHorizontal = (Mathf.Abs(inDirection.y) > 0.5f && Mathf.Abs(outDirection.x) > 0.5f);
            bool isHorizontalToVertical = (Mathf.Abs(inDirection.x) > 0.5f && Mathf.Abs(outDirection.y) > 0.5f);

            if (isVerticalToHorizontal)
            {
                // 从竖直方向转向水平方向 (例如：从上向右转)
                return CalculateVerticalToHorizontalRotation(virtualPos, inDirection, outDirection);
            }
            else if (isHorizontalToVertical)
            {
                // 从水平方向转向竖直方向 (例如：从右向下转)
                return CalculateHorizontalToVerticalRotation(virtualPos, inDirection, outDirection);
            }

            return 0f; // 直线移动，无旋转
        }

        /// <summary>
        /// 计算竖直到水平转弯的旋转角度
        /// </summary>
        float CalculateVerticalToHorizontalRotation(Vector2 virtualPos, Vector3 inDirection, Vector3 outDirection)
        {
            // 检查是从上到右，还是从下到右等情况
            bool fromTopToRight = (inDirection.y > 0 && outDirection.x > 0);
            bool fromTopToLeft = (inDirection.y > 0 && outDirection.x < 0);
            bool fromBottomToRight = (inDirection.y < 0 && outDirection.x > 0);
            bool fromBottomToLeft = (inDirection.y < 0 && outDirection.x < 0);

            if (fromTopToRight)
            {
                // 从上向右转：根据虚拟坐标精确计算旋转
                if (Mathf.Approximately(virtualPos.x, 2f) && virtualPos.y > 2f)
                {
                    return 0f; // 在竖线路径上 (2,4) → (2,3) → (2,2)
                }
                else if (Mathf.Approximately(virtualPos.x, 2f) && Mathf.Approximately(virtualPos.y, 2f))
                {
                    return 0f; // 在中心点 (2,2)
                }
                else if (Mathf.Approximately(virtualPos.x, 3f) && Mathf.Approximately(virtualPos.y, 2f))
                {
                    return 30f; // 第一个转弯点 (3,2)
                }
                else if (Mathf.Approximately(virtualPos.x, 4f) && Mathf.Approximately(virtualPos.y, 2f))
                {
                    return 60f; // 第二个转弯点 (4,2)
                }
                else if (virtualPos.x >= 4f)
                {
                    return 90f; // 超出边缘，完全转弯
                }
            }
            else if (fromTopToLeft)
            {
                // 从上向左转：(2,4) → (2,3) → (2,2) → (1,2) → (0,2)
                if (Mathf.Approximately(virtualPos.x, 2f) && virtualPos.y > 2f)
                {
                    return 0f; // 在竖线路径上
                }
                else if (Mathf.Approximately(virtualPos.x, 2f) && Mathf.Approximately(virtualPos.y, 2f))
                {
                    return 0f; // 在中心点
                }
                else if (Mathf.Approximately(virtualPos.x, 1f) && Mathf.Approximately(virtualPos.y, 2f))
                {
                    return -30f; // 向左转30度（负角度）
                }
                else if (Mathf.Approximately(virtualPos.x, 0f) && Mathf.Approximately(virtualPos.y, 2f))
                {
                    return -60f; // 向左转60度
                }
                else if (virtualPos.x <= 0f)
                {
                    return -90f; // 完全向左转弯
                }
            }
            else if (fromBottomToRight)
            {
                // 从下向右转：(2,0) → (2,1) → (2,2) → (3,2) → (4,2)
                if (Mathf.Approximately(virtualPos.x, 2f) && virtualPos.y < 2f)
                {
                    return 0f; // 在竖线路径上
                }
                else if (Mathf.Approximately(virtualPos.x, 2f) && Mathf.Approximately(virtualPos.y, 2f))
                {
                    return 0f; // 在中心点
                }
                else if (Mathf.Approximately(virtualPos.x, 3f) && Mathf.Approximately(virtualPos.y, 2f))
                {
                    return -30f; // 向右上转30度
                }
                else if (Mathf.Approximately(virtualPos.x, 4f) && Mathf.Approximately(virtualPos.y, 2f))
                {
                    return -60f; // 向右上转60度
                }
                else if (virtualPos.x >= 4f)
                {
                    return -90f; // 完全转弯
                }
            }
            else if (fromBottomToLeft)
            {
                // 从下向左转：(2,0) → (2,1) → (2,2) → (1,2) → (0,2)
                if (Mathf.Approximately(virtualPos.x, 2f) && virtualPos.y < 2f)
                {
                    return 0f; // 在竖线路径上
                }
                else if (Mathf.Approximately(virtualPos.x, 2f) && Mathf.Approximately(virtualPos.y, 2f))
                {
                    return 0f; // 在中心点
                }
                else if (Mathf.Approximately(virtualPos.x, 1f) && Mathf.Approximately(virtualPos.y, 2f))
                {
                    return 30f; // 向左上转30度
                }
                else if (Mathf.Approximately(virtualPos.x, 0f) && Mathf.Approximately(virtualPos.y, 2f))
                {
                    return 60f; // 向左上转60度
                }
                else if (virtualPos.x <= 0f)
                {
                    return 90f; // 完全转弯
                }
            }

            return 0f;
        }

        /// <summary>
        /// 计算水平到竖直转弯的旋转角度
        /// </summary>
        float CalculateHorizontalToVerticalRotation(Vector2 virtualPos, Vector3 inDirection, Vector3 outDirection)
        {
            // 检查具体的转弯方向
            bool fromRightToUp = (inDirection.x > 0 && outDirection.y > 0);
            bool fromRightToDown = (inDirection.x > 0 && outDirection.y < 0);
            bool fromLeftToUp = (inDirection.x < 0 && outDirection.y > 0);
            bool fromLeftToDown = (inDirection.x < 0 && outDirection.y < 0);

            if (fromRightToUp)
            {
                // 从右向上转：(4,2) → (3,2) → (2,2) → (2,3) → (2,4)
                if (Mathf.Approximately(virtualPos.y, 2f) && virtualPos.x > 2f)
                {
                    return 0f; // 在水平线路径上
                }
                else if (Mathf.Approximately(virtualPos.x, 2f) && Mathf.Approximately(virtualPos.y, 2f))
                {
                    return 0f; // 在中心点
                }
                else if (Mathf.Approximately(virtualPos.x, 2f) && Mathf.Approximately(virtualPos.y, 3f))
                {
                    return 30f; // 第一个转弯点
                }
                else if (Mathf.Approximately(virtualPos.x, 2f) && Mathf.Approximately(virtualPos.y, 4f))
                {
                    return 60f; // 第二个转弯点
                }
                else if (virtualPos.y >= 4f)
                {
                    return 90f; // 完全转弯
                }
            }
            else if (fromRightToDown)
            {
                // 从右向下转：(4,2) → (3,2) → (2,2) → (2,1) → (2,0)
                if (Mathf.Approximately(virtualPos.y, 2f) && virtualPos.x > 2f)
                {
                    return 0f; // 在水平线路径上
                }
                else if (Mathf.Approximately(virtualPos.x, 2f) && Mathf.Approximately(virtualPos.y, 2f))
                {
                    return 0f; // 在中心点
                }
                else if (Mathf.Approximately(virtualPos.x, 2f) && Mathf.Approximately(virtualPos.y, 1f))
                {
                    return -30f; // 向下转30度
                }
                else if (Mathf.Approximately(virtualPos.x, 2f) && Mathf.Approximately(virtualPos.y, 0f))
                {
                    return -60f; // 向下转60度
                }
                else if (virtualPos.y <= 0f)
                {
                    return -90f; // 完全转弯
                }
            }
            else if (fromLeftToUp)
            {
                // 从左向上转：(0,2) → (1,2) → (2,2) → (2,3) → (2,4)
                if (Mathf.Approximately(virtualPos.y, 2f) && virtualPos.x < 2f)
                {
                    return 0f; // 在水平线路径上
                }
                else if (Mathf.Approximately(virtualPos.x, 2f) && Mathf.Approximately(virtualPos.y, 2f))
                {
                    return 0f; // 在中心点
                }
                else if (Mathf.Approximately(virtualPos.x, 2f) && Mathf.Approximately(virtualPos.y, 3f))
                {
                    return -30f; // 向上左转30度
                }
                else if (Mathf.Approximately(virtualPos.x, 2f) && Mathf.Approximately(virtualPos.y, 4f))
                {
                    return -60f; // 向上左转60度
                }
                else if (virtualPos.y >= 4f)
                {
                    return -90f; // 完全转弯
                }
            }
            else if (fromLeftToDown)
            {
                // 从左向下转：(0,2) → (1,2) → (2,2) → (2,1) → (2,0)
                if (Mathf.Approximately(virtualPos.y, 2f) && virtualPos.x < 2f)
                {
                    return 0f; // 在水平线路径上
                }
                else if (Mathf.Approximately(virtualPos.x, 2f) && Mathf.Approximately(virtualPos.y, 2f))
                {
                    return 0f; // 在中心点
                }
                else if (Mathf.Approximately(virtualPos.x, 2f) && Mathf.Approximately(virtualPos.y, 1f))
                {
                    return 30f; // 向下右转30度
                }
                else if (Mathf.Approximately(virtualPos.x, 2f) && Mathf.Approximately(virtualPos.y, 0f))
                {
                    return 60f; // 向下右转60度
                }
                else if (virtualPos.y <= 0f)
                {
                    return 90f; // 完全转弯
                }
            }

            return 0f;
        }

        void OnDrawGizmosSelected()
        {
            if (!DrawDebugGizmos) return;
            if (_grid.Width == 0) return;
            Gizmos.color = new Color(0f, 1f, 0f, 0.4f);
            foreach (var c in _bodyCells)
            {
                Gizmos.DrawWireCube(_grid.CellToWorld(c), new Vector3(_grid.CellSize, _grid.CellSize, 0f));
            }
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
            Vector3 prev = Vector3.negativeInfinity;
            foreach (var c in _pathQueue)
            {
                var p = _grid.CellToWorld(c);
                Gizmos.DrawSphere(p, 0.05f);
                if (prev.x > -10000f) Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }

        /// <summary>
        /// 获取鼠标在当前格子中的象限位置（以格子中心为分界线）
        /// </summary>
        /// <param name="mouseScreenPosition">鼠标屏幕坐标</param>
        /// <param name="gridCell">目标格子坐标</param>
        /// <returns>象限编号：1=右上，2=左上，3=左下，4=右下</returns>
        int GetMouseQuadrantInCell(Vector3 mouseScreenPosition, Vector2Int gridCell)
        {
            // 将鼠标屏幕坐标转换为世界坐标
            Vector3 mouseWorldPos = ScreenToWorld(mouseScreenPosition);

            // 获取格子中心的世界坐标
            Vector3 cellCenterWorld = _grid.CellToWorld(gridCell);

            // 计算鼠标相对于格子中心的偏移
            Vector3 offset = mouseWorldPos - cellCenterWorld;

            // 根据偏移量确定象限
            // 象限定义：1=右上(+x,+y)，2=左上(-x,+y)，3=左下(-x,-y)，4=右下(+x,-y)
            if (offset.x >= 0 && offset.y >= 0)
            {
                return 1; // 右上象限
            }
            else if (offset.x < 0 && offset.y >= 0)
            {
                return 2; // 左上象限
            }
            else if (offset.x < 0 && offset.y < 0)
            {
                return 3; // 左下象限
            }
            else // (offset.x >= 0 && offset.y < 0)
            {
                return 4; // 右下象限
            }
        }

        /// <summary>
        /// 获取鼠标在当前格子中的象限位置（重载方法，直接使用当前鼠标位置）
        /// </summary>
        /// <param name="gridCell">目标格子坐标</param>
        /// <returns>象限编号：1=右上，2=左上，3=左下，4=右下</returns>
        int GetMouseQuadrantInCell(Vector2Int gridCell)
        {
            return GetMouseQuadrantInCell(Input.mousePosition, gridCell);
        }

        /// <summary>
        /// 获取鼠标在当前格子中的象限位置（返回详细信息）
        /// </summary>
        /// <param name="mouseScreenPosition">鼠标屏幕坐标</param>
        /// <param name="gridCell">目标格子坐标</param>
        /// <returns>包含象限编号和偏移量的详细信息</returns>
        (int quadrant, Vector2 offset, string description) GetMouseQuadrantInCellDetailed(Vector3 mouseScreenPosition, Vector2Int gridCell)
        {
            // 将鼠标屏幕坐标转换为世界坐标
            Vector3 mouseWorldPos = ScreenToWorld(mouseScreenPosition);

            // 获取格子中心的世界坐标
            Vector3 cellCenterWorld = _grid.CellToWorld(gridCell);

            // 计算鼠标相对于格子中心的偏移
            Vector3 offset3D = mouseWorldPos - cellCenterWorld;
            Vector2 offset = new Vector2(offset3D.x, offset3D.y);

            int quadrant;
            string description;

            // 根据偏移量确定象限
            if (offset.x >= 0 && offset.y >= 0)
            {
                quadrant = 1;
                description = "右上象限";
            }
            else if (offset.x < 0 && offset.y >= 0)
            {
                quadrant = 2;
                description = "左上象限";
            }
            else if (offset.x < 0 && offset.y < 0)
            {
                quadrant = 3;
                description = "左下象限";
            }
            else // (offset.x >= 0 && offset.y < 0)
            {
                quadrant = 4;
                description = "右下象限";
            }

            return (quadrant, offset, description);
        }
    }
}


