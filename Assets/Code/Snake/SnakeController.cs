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

        GridConfig _grid;
        GridEntityManager _entityManager;
        readonly List<Transform> _segments = new List<Transform>();
        readonly List<List<Transform>> _subSegments = new List<List<Transform>>(); // 每段的5个小段
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
            
            // 清理现有小段
            for (int i = 0; i < _subSegments.Count; i++)
            {
                for (int j = 0; j < _subSegments[i].Count; j++)
                {
                    if (_subSegments[i][j] != null) Destroy(_subSegments[i][j].gameObject);
                }
            }
            _subSegments.Clear();
            
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
            
            // 计算90度转弯
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
                // 转弯：排列成L形
                ArrangeSubSegmentsInLShape(subSegmentList, mainPos, inDirection, outDirection);
            }
            else
            {
                // 直线：沿着网格对齐的方向排列
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
                
                ArrangeSubSegmentsInLine(subSegmentList, mainPos, direction);
            }
        }
        
        void ArrangeSubSegmentsInLine(List<Transform> subSegments, Vector3 centerPos, Vector3 direction)
        {
            float subSegmentSize = _grid.CellSize / SUB_SEGMENTS_PER_SEGMENT;
            
            // 5个小段均分主段的空间，每个小段占据1/5的空间
            for (int i = 0; i < subSegments.Count; i++)
            {
                // 计算每个小段在主段中的相对位置
                // i=0时在最前面，i=4时在最后面
                float t = (float)i / (SUB_SEGMENTS_PER_SEGMENT - 1); // 0, 0.25, 0.5, 0.75, 1
                float offset = (t - 0.5f) * _grid.CellSize; // -0.5, -0.25, 0, 0.25, 0.5 * CellSize
                
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
            
            // 计算90度L形转弯的关键点
            // 使用网格单元的1/4作为转弯半径，创造更贴合网格的效果
            float turnOffset = _grid.CellSize * 0.25f;
            
            // 转弯内角点：稍微向内偏移
            Vector3 innerCorner = centerPos + (inDirection + outDirection).normalized * turnOffset * 0.5f;
            
            for (int i = 0; i < subSegments.Count; i++)
            {
                Vector3 pos;
                
                if (i == 0)
                {
                    // 第1个小段：沿进入方向，距离转弯点最远
                    pos = centerPos - inDirection * subSegmentSize * 2f;
                }
                else if (i == 1)
                {
                    // 第2个小段：沿进入方向，靠近转弯点
                    pos = centerPos - inDirection * subSegmentSize;
                }
                else if (i == 2)
                {
                    // 第3个小段：在转弯内角，创造90度效果
                    pos = innerCorner;
                }
                else if (i == 3)
                {
                    // 第4个小段：沿离开方向，靠近转弯点
                    pos = centerPos + outDirection * subSegmentSize;
                }
                else // i == 4
                {
                    // 第5个小段：沿离开方向，距离转弯点最远
                    pos = centerPos + outDirection * subSegmentSize * 2f;
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
    }
}


