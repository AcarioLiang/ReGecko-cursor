using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ReGecko.GridSystem;
using ReGecko.Grid.Entities;
using System.Collections;
using ReGecko.GameCore.Flow;

namespace ReGecko.SnakeSystem
{
    public class SnakeController : BaseSnake
    {
        [Header("SnakeController特有属性")]
        // 拖拽相关
        Vector2Int _dragStartCell;
        bool _dragging;
        bool _dragOnHead;
        Vector2Int _lastSampledCell; // 上次采样的手指网格
        float _moveAccumulator; // 基于速度的逐格推进计数器
        float _lastStatsTime;
        int _stepsConsumedThisFrame;
        
        // 性能优化缓存
        private readonly List<RectTransform> _cachedRectTransforms = new List<RectTransform>();
        private readonly List<Vector3> _tempPolylinePoints = new List<Vector3>(); // 复用的折线点列表
        private readonly List<Vector2Int> _tempBodyCellsList = new List<Vector2Int>(); // 复用的身体格子列表
        
        // 拖动优化：减少更新频率
        private float _lastDragUpdateTime = 0f;
        private const float DRAG_UPDATE_INTERVAL = 0.016f; // 约60FPS更新频率
        
        // 路径队列优化
        private const int MAX_PATH_QUEUE_SIZE = 20; // 路径队列最大长度
        private const int PATH_QUEUE_TRIM_SIZE = 10; // 超出限制时保留的路径数量
        
        // 性能分析日志
        private int _updateMovementCallCount = 0;
        private int _enqueuePathCallCount = 0;
        private int _updateVisualsCallCount = 0;
        private float _lastPerformanceLogTime = 0f;
        private const float PERFORMANCE_LOG_INTERVAL = 2.0f; // 每2秒记录一次
        private System.Diagnostics.Stopwatch _updateMovementStopwatch = new System.Diagnostics.Stopwatch();
        private System.Diagnostics.Stopwatch _updateVisualsStopwatch = new System.Diagnostics.Stopwatch();
        private System.Diagnostics.Stopwatch _enqueuePathStopwatch = new System.Diagnostics.Stopwatch();
        private long _totalUpdateMovementTime = 0;
        private long _totalUpdateVisualsTime = 0;
        private long _totalEnqueuePathTime = 0;
        
        // 可视化优化状态
        private bool _polylineNeedsRebuild = true;
        private Vector2Int _lastVisualHeadCell = Vector2Int.zero;
        private int _lastVisualBodyCount = 0;
        
        // 预测性移动状态
        private Vector2Int _lastPredictedTarget = Vector2Int.zero;
        private float _lastPredictionTime = 0f;
        private const float PREDICTION_UPDATE_INTERVAL = 0.1f; // 预测更新间隔
        private const float PREDICTION_DISTANCE_THRESHOLD = 5f; // 预测距离阈值

        enum DragAxis { None, X, Y }
        DragAxis _dragAxis = DragAxis.None;

        Coroutine _consumeCoroutine;

        public override void Initialize(GridConfig grid, GridEntityManager entityManager = null, SnakeManager snakeManager = null)
        {
            _grid = grid;
            _entityManager = entityManager ?? FindObjectOfType<GridEntityManager>();
            _snakeManager = snakeManager ?? FindObjectOfType<SnakeManager>();

            // 初始化身体图片管理器
            InitializeBodySpriteManager();

            BuildSegments();
            PlaceInitial();
           
        }

        public override void UpdateGridConfig(GridConfig newGrid)
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
        }

        void BuildSegments()
        {
            for (int i = 0; i < _segments.Count; i++)
            {
                if (_segments[i] != null) Destroy(_segments[i].gameObject);
            }
            _segments.Clear();
            _cachedRectTransforms.Clear(); // 清理缓存
            
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
                _cachedRectTransforms.Add(rt); // 缓存RectTransform组件
            }
        }

        protected override void InitializeBodySpriteManager()
        {
            base.InitializeBodySpriteManager();
            
            if (_bodySpriteManager != null)
            {
                _bodySpriteManager.Config = GameContext.SnakeBodyConfig;
            }
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
                
                // 使用缓存的RectTransform（添加null检查）
                if (i < _cachedRectTransforms.Count)
                {
                    var rt = _cachedRectTransforms[i];
                    if (rt != null)
                    {
                        var worldPos = _grid.CellToWorld(cells[i]);
                        rt.anchoredPosition = new Vector2(worldPos.x, worldPos.y);
                    }
                }


            }
            _currentHeadCell = _bodyCells.First.Value;
            _currentTailCell = _bodyCells.Last.Value;
            
            // 同步HashSet缓存
            SyncBodyCellsSet();
            
            // 初始放置完成后，更新身体图片
            if (EnableBodySpriteManagement && _bodySpriteManager != null)
            {
                _bodySpriteManager.UpdateAllSegmentSprites();
            }
        }



        void Update()
        {
            if (IsControllable)
            {
                HandleInput();
            }
            // UpdateMovement由SnakeManager统一调用，避免重复
        }

        public override void UpdateMovement()
        {
            _updateMovementStopwatch.Restart();
            _updateMovementCallCount++;
            
            // 清理已销毁的组件
            CleanupCachedComponents();
            
            // 如果蛇已被完全消除或组件被销毁，停止所有移动更新
            if (_bodyCells.Count == 0 || !IsAlive() || _cachedRectTransforms.Count == 0) 
            {
                _updateMovementStopwatch.Stop();
                return;
            }

            if (_dragging && !_consuming)
            {
                // 采样当前手指所在格，扩充路径队列（仅四向路径）
                var world = ScreenToWorld(Input.mousePosition);
                var targetCell = ClampInside(_grid.WorldToCell(world));
                if (targetCell != _lastSampledCell)
                {
                    // 预测性移动检查
                    Vector2Int predictedTarget = PredictDragTarget(targetCell);
                    bool shouldUsePrediction = ShouldUsePredictiveMovement(targetCell, predictedTarget);
                    
                    if (shouldUsePrediction)
                    {
                        Debug.Log($"[蛇{SnakeId}] 使用预测性移动: 当前({targetCell.x},{targetCell.y}) -> 预测({predictedTarget.x},{predictedTarget.y})");
                        targetCell = predictedTarget;
                        _pathQueue.Clear(); // 清空当前队列以使用预测路径
                    }
                    
                    // 更新主方向：按更大位移轴确定
                    var delta = targetCell - (_dragOnHead ? _currentHeadCell : _currentTailCell);
                    _dragAxis = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) ? DragAxis.X : DragAxis.Y;
                    _enqueuePathStopwatch.Restart();
                    _enqueuePathCallCount++;
                    int pathCountBefore = _pathQueue.Count;
                    EnqueueOptimizedPath(_lastSampledCell, targetCell);
                    int pathCountAfter = _pathQueue.Count;
                    int pathsAdded = pathCountAfter - pathCountBefore;
                    _enqueuePathStopwatch.Stop();
                    _totalEnqueuePathTime += _enqueuePathStopwatch.ElapsedTicks;
                    
                    // 记录路径添加情况
                    if (pathsAdded > 5) // 只记录较大的路径添加
                    {
                        Debug.Log($"[蛇{SnakeId}] 快速拖动检测: 从({_lastSampledCell.x},{_lastSampledCell.y})到({targetCell.x},{targetCell.y}), 添加{pathsAdded}个路径点, 队列长度: {pathCountAfter}");
                    }
                    
                    // 路径队列长度限制
                    TrimPathQueueIfNeeded();
                    _lastSampledCell = targetCell;
                }

                // 洞检测：若拖动端临近洞，且颜色匹配，触发吞噬
                var hole = FindAdjacentHole(_dragOnHead ? _currentHeadCell : _currentTailCell);
                if (hole != null && hole.CanInteractWithSnake(this))
                {
                    _consumeCoroutine ??= StartCoroutine(CoConsume(hole, _dragOnHead));
                }
            }

            // 按动态速度逐格消费路径
            _stepsConsumedThisFrame = 0;
            
            // 动态移动速度：根据路径队列长度自动调整
            float dynamicSpeed = CalculateDynamicMoveSpeed();
            _moveAccumulator += dynamicSpeed * Time.deltaTime;
            
            int stepsThisFrame = 0;
            int pathQueueSizeBefore = _pathQueue.Count;
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
                        _polylineNeedsRebuild = true; // 标记需要重建折线
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
                        _polylineNeedsRebuild = true; // 标记需要重建折线
                    }
                }
                _moveAccumulator -= 1f;
                stepsThisFrame++;
                _stepsConsumedThisFrame++;
            }
            
            // 记录路径消费情况
            int pathQueueSizeAfter = _pathQueue.Count;
            int pathsConsumed = pathQueueSizeBefore - pathQueueSizeAfter;
            if (_dragging && (pathsConsumed > 0 || pathQueueSizeAfter > 10))
            {
                Debug.Log($"[蛇{SnakeId}] 路径消费: 消费{pathsConsumed}个, 剩余{pathQueueSizeAfter}个, 移动累积器: {_moveAccumulator:F2}, 移动速度: {MoveSpeedCellsPerSecond}");
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
                    if (idx >= _cachedRectTransforms.Count) break;

                    // UI渲染：使用缓存的RectTransform（添加null检查）
                    var rt = _cachedRectTransforms[idx];
                    if (rt != null)
                    {
                        var worldPos = _grid.CellToWorld(cell);
                        rt.anchoredPosition = new Vector2(worldPos.x, worldPos.y);
                    }
                    
                    idx++;
                }
            }
            
            // 移动完成后，更新身体图片
            if (EnableBodySpriteManagement && _bodySpriteManager != null && stepsThisFrame > 0)
            {
                _bodySpriteManager.OnSnakeMoved();
            }
            
            // 性能统计
            _updateMovementStopwatch.Stop();
            _totalUpdateMovementTime += _updateMovementStopwatch.ElapsedTicks;
            
            // 定期输出性能日志
            LogPerformanceStats();
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
            
            // 检查是否被其他蛇阻挡
            if (_snakeManager != null && _snakeManager.IsCellOccupiedByOtherSnakes(cell, this))
            {
                return true;
            }
            
            return false;
        }

        void UpdateVisualsSmoothDragging()
        {
            _updateVisualsStopwatch.Restart();
            _updateVisualsCallCount++;
            
            // 限制更新频率以提高性能
            float currentTime = Time.time;
            if (currentTime - _lastDragUpdateTime < DRAG_UPDATE_INTERVAL)
            {
                _updateVisualsStopwatch.Stop();
                return; // 跳过这帧的更新
            }
            _lastDragUpdateTime = currentTime;
            
            // 检查是否需要重建折线
            bool needsRebuild = ShouldRebuildPolyline();
            
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
                // 只在需要时重建折线
                if (needsRebuild)
                {
                    // 构建折线：headVisual -> (body First.Next ... Last)
                    // 使用复用的列表减少GC分配
                    _tempPolylinePoints.Clear();
                    _tempPolylinePoints.Add(headVisual);
                    var it = _bodyCells.First;
                    if (it != null) it = it.Next; // skip head cell
                    while (it != null)
                    {
                        _tempPolylinePoints.Add(_grid.CellToWorld(it.Value));
                        it = it.Next;
                    }
                    _polylineNeedsRebuild = false;
                }
                else
                {
                    // 只更新头部位置
                    if (_tempPolylinePoints.Count > 0)
                    {
                        _tempPolylinePoints[0] = headVisual;
                    }
                }
                float spacing = _grid.CellSize;
                for (int i = 0; i < _segments.Count && i < _cachedRectTransforms.Count; i++)
                {
                    Vector3 p = GetPointAlongPolyline(_tempPolylinePoints, i * spacing);
                    var rt = _cachedRectTransforms[i];
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
                // 使用复用的列表减少GC分配
                _tempPolylinePoints.Clear();
                _tempPolylinePoints.Add(tailVisual); // 尾部拖动位置
                
                // 添加身体段位置（从尾部向头部）
                var it = _bodyCells.Last;
                if (it != null) it = it.Previous; // 跳过尾部cell（已经用tailVisual代替）
                while (it != null)
                {
                    _tempPolylinePoints.Add(_grid.CellToWorld(it.Value));
                    it = it.Previous;
                }
                
                float spacing = _grid.CellSize;
                // 从尾部开始分布，索引0对应尾部段
                for (int i = 0; i < _segments.Count && i < _cachedRectTransforms.Count; i++)
                {
                    int segmentIndex = _segments.Count - 1 - i; // 倒序：尾部段在前
                    if (segmentIndex < _cachedRectTransforms.Count)
                    {
                        Vector3 p = GetPointAlongPolyline(_tempPolylinePoints, i * spacing);
                        var rt = _cachedRectTransforms[segmentIndex];
                        if (rt != null)
                        {
                            rt.anchoredPosition = new Vector2(p.x, p.y);
                        }
                    }
                }
            }
            
            // 性能统计
            _updateVisualsStopwatch.Stop();
            _totalUpdateVisualsTime += _updateVisualsStopwatch.ElapsedTicks;
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
            // 检查是否被其他蛇阻挡
            if (_snakeManager != null && _snakeManager.IsCellOccupiedByOtherSnakes(nextCell, this)) return false;
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
            // 检查实体阻挡
            if (_entityManager != null && _entityManager.IsBlocked(nextCell)) return false;
            // 检查是否被其他蛇阻挡
            if (_snakeManager != null && _snakeManager.IsCellOccupiedByOtherSnakes(nextCell, this)) return false;
            // 占用校验：允许进入原头
            var headCell = _bodyCells.First.Value;
            if (IsOccupiedBySelf(nextCell) && nextCell != headCell) return false;
            
            // 更新身体
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
            // 不允许进入自身身体占用格（使用优化的HashSet查找）
            if (IsOccupiedBySelf(cell)) return false;
            
            // 不允许进入其他蛇占用的格子
            if (_snakeManager != null && _snakeManager.IsCellOccupiedByOtherSnakes(cell, this)) return false;
            
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

        /// <summary>
        /// 获取蛇头的格子位置
        /// </summary>
        public Vector2Int GetHeadCell()
        {
            return _currentHeadCell;
        }

        /// <summary>
        /// 获取蛇尾的格子位置
        /// </summary>
        public Vector2Int GetTailCell()
        {
            return _currentTailCell;
        }

        /// <summary>
        /// 清理缓存的RectTransform组件，防止内存泄漏
        /// </summary>
        void CleanupCachedComponents()
        {
            // 清理已销毁的RectTransform引用
            for (int i = _cachedRectTransforms.Count - 1; i >= 0; i--)
            {
                if (_cachedRectTransforms[i] == null)
                {
                    _cachedRectTransforms.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 预测拖动目标位置
        /// </summary>
        Vector2Int PredictDragTarget(Vector2Int currentTarget)
        {
            Vector2Int currentPos = _dragOnHead ? _currentHeadCell : _currentTailCell;
            Vector2Int direction = currentTarget - currentPos;
            
            // 简单的线性预测：沿着当前方向延伸
            if (direction.magnitude > 0)
            {
                Vector2Int normalizedDir = new Vector2Int(
                    direction.x != 0 ? (direction.x > 0 ? 1 : -1) : 0,
                    direction.y != 0 ? (direction.y > 0 ? 1 : -1) : 0
                );
                
                // 预测未来位置（延伸2-4个格子）
                int predictionDistance = Mathf.Clamp(Mathf.RoundToInt(direction.magnitude * 0.5f), 2, 4);
                Vector2Int predicted = currentTarget + normalizedDir * predictionDistance;
                
                return ClampInside(predicted);
            }
            
            return currentTarget;
        }
        
        /// <summary>
        /// 判断是否应该使用预测性移动
        /// </summary>
        bool ShouldUsePredictiveMovement(Vector2Int currentTarget, Vector2Int predictedTarget)
        {
            float currentTime = Time.time;
            
            // 限制预测更新频率
            if (currentTime - _lastPredictionTime < PREDICTION_UPDATE_INTERVAL)
            {
                return false;
            }
            
            // 检查距离阈值
            float distance = Vector2Int.Distance(currentTarget, predictedTarget);
            if (distance < PREDICTION_DISTANCE_THRESHOLD)
            {
                return false;
            }
            
            // 检查路径队列是否过长（只在队列较长时使用预测）
            if (_pathQueue.Count < 8)
            {
                return false;
            }
            
            _lastPredictionTime = currentTime;
            _lastPredictedTarget = predictedTarget;
            return true;
        }
        
        /// <summary>
        /// 判断是否需要重建折线
        /// </summary>
        bool ShouldRebuildPolyline()
        {
            // 强制重建标志
            if (_polylineNeedsRebuild) return true;
            
            // 检查关键状态变化
            bool headCellChanged = _currentHeadCell != _lastVisualHeadCell;
            bool bodySizeChanged = _bodyCells.Count != _lastVisualBodyCount;
            
            if (headCellChanged || bodySizeChanged)
            {
                _lastVisualHeadCell = _currentHeadCell;
                _lastVisualBodyCount = _bodyCells.Count;
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// 优化的路径生成方法，根据距离选择不同策略
        /// </summary>
        void EnqueueOptimizedPath(Vector2Int from, Vector2Int to)
        {
            if (from == to) return;
            
            float distance = Vector2Int.Distance(from, to);
            int dx = Mathf.Abs(to.x - from.x);
            int dy = Mathf.Abs(to.y - from.y);
            int totalSteps = dx + dy;
            
            // 根据距离选择不同的路径生成策略
            if (totalSteps <= 8)
            {
                // 短距离：使用原始精确算法
                EnqueueAxisAlignedPath(from, to);
            }
            else if (totalSteps <= 20)
            {
                // 中距离：使用适度采样
                EnqueueSampledPath(from, to, Mathf.Min(12, totalSteps / 2));
            }
            else
            {
                // 长距离：使用粗采样 + 直接跳跃
                EnqueueSampledPath(from, to, 8);
            }
        }
        
        /// <summary>
        /// 采样路径生成，减少中间点数量
        /// </summary>
        void EnqueueSampledPath(Vector2Int from, Vector2Int to, int maxSamples)
        {
            _pathBuildBuffer.Clear();
            
            for (int i = 1; i <= maxSamples; i++)
            {
                float t = (float)i / maxSamples;
                Vector2Int sample = new Vector2Int(
                    Mathf.RoundToInt(Mathf.Lerp(from.x, to.x, t)),
                    Mathf.RoundToInt(Mathf.Lerp(from.y, to.y, t))
                );
                
                // 避免重复点
                if (_pathBuildBuffer.Count == 0 || _pathBuildBuffer[_pathBuildBuffer.Count - 1] != sample)
                {
                    _pathBuildBuffer.Add(ClampInside(sample));
                }
            }
            
            // 添加到队列
            for (int i = 0; i < _pathBuildBuffer.Count; i++)
            {
                //if (!IsPathValid(_pathBuildBuffer[i])) continue;
                _pathQueue.Enqueue(_pathBuildBuffer[i]);
            }
        }
        
        /// <summary>
        /// 修剪路径队列以防止过度积压
        /// </summary>
        void TrimPathQueueIfNeeded()
        {
            if (_pathQueue.Count > MAX_PATH_QUEUE_SIZE)
            {
                Debug.Log($"[蛇{SnakeId}] 路径队列过长({_pathQueue.Count}), 修剪到{PATH_QUEUE_TRIM_SIZE}个路径点");
                
                // 保留最近的路径点
                var trimmedQueue = new Queue<Vector2Int>();
                int keepCount = PATH_QUEUE_TRIM_SIZE;
                int skipCount = _pathQueue.Count - keepCount;
                
                // 跳过早期的路径点
                for (int i = 0; i < skipCount; i++)
                {
                    if (_pathQueue.Count > 0) _pathQueue.Dequeue();
                }
                
                Debug.Log($"[蛇{SnakeId}] 路径队列修剪完成，剩余{_pathQueue.Count}个路径点");
            }
        }
        
        /// <summary>
        /// 计算动态移动速度
        /// </summary>
        float CalculateDynamicMoveSpeed()
        {
            float baseSpeed = MoveSpeedCellsPerSecond;
            
            // 只在拖动时启用动态速度
            if (!_dragging) return baseSpeed;
            
            int queueSize = _pathQueue.Count;
            
            // 动态速度调整策略
            if (queueSize <= 3)
            {
                return baseSpeed; // 正常速度
            }
            else if (queueSize <= 8)
            {
                return baseSpeed * 1.5f; // 中度加速
            }
            else if (queueSize <= 15)
            {
                return baseSpeed * 2.5f; // 高速模式
            }
            else
            {
                return baseSpeed * 4.0f; // 极速模式，快速清空队列
            }
        }
        
        /// <summary>
        /// 输出性能统计日志
        /// </summary>
        void LogPerformanceStats()
        {
            float currentTime = Time.time;
            if (currentTime - _lastPerformanceLogTime >= PERFORMANCE_LOG_INTERVAL)
            {
                _lastPerformanceLogTime = currentTime;
                
                // 计算平均耗时（毫秒）
                double avgUpdateMovementMs = _updateMovementCallCount > 0 ? 
                    (_totalUpdateMovementTime * 1000.0 / System.Diagnostics.Stopwatch.Frequency) / _updateMovementCallCount : 0;
                double avgUpdateVisualsMs = _updateVisualsCallCount > 0 ? 
                    (_totalUpdateVisualsTime * 1000.0 / System.Diagnostics.Stopwatch.Frequency) / _updateVisualsCallCount : 0;
                double avgEnqueuePathMs = _enqueuePathCallCount > 0 ? 
                    (_totalEnqueuePathTime * 1000.0 / System.Diagnostics.Stopwatch.Frequency) / _enqueuePathCallCount : 0;
                
                float currentDynamicSpeed = CalculateDynamicMoveSpeed();
                float speedMultiplier = currentDynamicSpeed / MoveSpeedCellsPerSecond;
                
                Debug.Log($"[蛇{SnakeId}] 性能统计 - 过去{PERFORMANCE_LOG_INTERVAL}秒:\n" +
                    $"UpdateMovement: {_updateMovementCallCount}次调用, 平均{avgUpdateMovementMs:F3}ms\n" +
                    $"UpdateVisuals: {_updateVisualsCallCount}次调用, 平均{avgUpdateVisualsMs:F3}ms\n" +
                    $"EnqueuePath: {_enqueuePathCallCount}次调用, 平均{avgEnqueuePathMs:F3}ms\n" +
                    $"路径队列长度: {_pathQueue.Count}, 动态速度倍数: {speedMultiplier:F1}x\n" +
                    $"身体段数: {_bodyCells.Count}, 拖动中: {_dragging}, 移动累积器: {_moveAccumulator:F2}");
                
                // 重置计数器
                _updateMovementCallCount = 0;
                _updateVisualsCallCount = 0;
                _enqueuePathCallCount = 0;
                _totalUpdateMovementTime = 0;
                _totalUpdateVisualsTime = 0;
                _totalEnqueuePathTime = 0;
            }
        }

        protected override void OnDestroy()
        {
            // 清理缓存
            _cachedRectTransforms.Clear();
            _tempPolylinePoints.Clear();
            _tempBodyCellsList.Clear();
            
            base.OnDestroy();
        }
    }
}


