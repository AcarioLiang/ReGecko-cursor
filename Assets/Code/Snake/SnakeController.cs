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
        Vector2Int _dragStartCell;
        bool _startMove;
        bool _dragging;
        bool _dragFromHead;
        bool _isReversing; // 标记是否正在倒车
        Vector2Int _lastSampledCell; // 上次采样的手指网格
        float _moveAccumulator; // 基于速度的逐格推进计数器
        float _lastStatsTime;

        //优化缓存
        Vector3 _lastMousePos;
        Canvas _parentCanvas;
        RectTransform _headRt;
        RectTransform _tailRt;


        // 性能优化缓存
        private readonly List<RectTransform> _cachedRectTransforms = new List<RectTransform>();
        private readonly List<Vector3> _tempPolylinePoints = new List<Vector3>(); // 复用的折线点列表
        private readonly List<Vector2Int> _tempBodyCellsList = new List<Vector2Int>(); // 复用的身体格子列表

        // 拖动优化：减少更新频率
        private float _lastDragUpdateTime = 0f;
        private const float DRAG_UPDATE_INTERVAL = 0.016f; // 约60FPS更新频率

        // 路径队列优化
        private const int MAX_PATH_QUEUE_SIZE = 10; // 路径队列最大长度
        private const int PATH_QUEUE_TRIM_SIZE = 10; // 超出限制时保留的路径数量

        // 松开后快速移动相关
        private bool _isReleaseMovement = false; // 是否处于松开后的快速移动状态
        private float _releaseSpeedMultiplier = 1f; // 松开后的速度倍数
        private const float MIN_RELEASE_SPEED_MULTIPLIER = 1f; // 最小松开速度倍数
        private const float MAX_RELEASE_SPEED_MULTIPLIER = 5f; // 最大松开速度倍数
        private const int RELEASE_SPEED_PATH_THRESHOLD = 3; // 开始加速的路径长度阈值



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
            _parentCanvas = GetComponentInParent<Canvas>();

            // 初始化身体图片管理器
            InitializeBodySpriteManager();

            BuildSegments();
            PlaceInitial();

            //更新缓存
            if (_segments.Count > 0)
            {
                _headRt = _segments[0].GetComponent<RectTransform>();
                _tailRt = _segments[_segments.Count - 1].GetComponent<RectTransform>();
            }

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
        }

        public override void UpdateMovement()
        {
            // 清理已销毁的组件
            CleanupCachedComponents();

            // 如果蛇已被完全消除或组件被销毁，停止所有移动更新
            if (_bodyCells.Count == 0 || !IsAlive() || _cachedRectTransforms.Count == 0)
            {
                return;
            }

            if (!_dragging)
                return;

            if (_consuming)
                return;


            // 采样当前手指所在格，扩充路径队列（仅四向路径）
            var world = ScreenToWorld(Input.mousePosition);
            var targetCell = ClampInside(_grid.WorldToCell(world));
            if (targetCell != _lastSampledCell)
            {
                // 如果正在倒车，不执行正向拖动路径生成
                if (_isReversing)
                {
                    Debug.Log($"[蛇{SnakeId}] 倒车进行中，忽略正向拖动路径生成");
                    return;
                }

                // 更新主方向：按更大位移轴确定
                var delta = targetCell - (_dragFromHead ? _currentHeadCell : _currentTailCell);
                _dragAxis = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) ? DragAxis.X : DragAxis.Y;
                EnqueueOptimizedPath(_lastSampledCell, targetCell);

                _lastSampledCell = targetCell;
            }

            // 洞检测：若拖动端在洞位置或临近洞，且颜色匹配，触发吞噬
            var hole = FindHoleAtOrAdjacent(_dragFromHead ? _currentHeadCell : _currentTailCell);
            if (hole != null && hole.CanInteractWithSnake(this))
            {
                _consumeCoroutine ??= StartCoroutine(CoConsume(hole, _dragFromHead));
                return;
            }


            if (_pathQueue.Count > 0)
            {
                // 动态移动速度：根据路径队列长度自动调整
                float dynamicSpeed = CalculateDynamicMoveSpeed();
                _moveAccumulator += dynamicSpeed * Time.deltaTime; // 累积而不是重置

                var nextCell = _pathQueue.First.Value;

                // 检查目标点合法性
                if (!CheckNextPoint(nextCell))
                {
                    _pathQueue.RemoveFirst();
                    return;
                }

                // 当移动累积器达到1.0时，表示应该移动到下一个格子
                if (_moveAccumulator >= 1.0f)
                {
                    _moveAccumulator -= 1.0f; // 减去1.0，保留余数
                    _pathQueue.RemoveFirst();

                    // 更新蛇的逻辑位置
                    if (_dragFromHead)
                    {
                        // 倒车：若下一步将进入紧邻身体，则改为让尾部后退一步
                        var nextBody = _bodyCells.First.Next != null ? _bodyCells.First.Next.Value : _bodyCells.First.Value;
                        if (nextCell == nextBody)
                        {
                            TryReverseOneStep();
                        }
                        else
                        {
                            AdvanceHeadTo(nextCell);
                        }
                    }
                    else
                    {
                        // 尾部倒车：若下一步将进入紧邻身体，则改为让头部前进一步
                        var prevBody = _bodyCells.Last.Previous != null ? _bodyCells.Last.Previous.Value : _bodyCells.Last.Value;
                        if (nextCell == prevBody)
                        {
                            TryReverseFromTail();
                        }
                        else
                        {
                            AdvanceTailTo(nextCell);
                        }
                    }
                }
                else
                {
                    // 还没到达目标格子，进行平滑视觉更新
                    UpdateVisualsSmoothDragging();
                }
            }
            else
            {
                //拖拽在当前格子里
                UpdateVisualsSmoothDragging();
            }

            // 检查是否需要重置松开移动状态
            if (_isReleaseMovement && _pathQueue.Count == 0)
            {
                _isReleaseMovement = false;
                _releaseSpeedMultiplier = 1f;
                Debug.Log($"[蛇{SnakeId}] 松开后移动完成，重置速度倍数");
            }

            // 检查是否需要重置倒车状态
            if (_isReversing && _pathQueue.Count == 0)
            {
                _isReversing = false;
                // 更新最后采样位置，准备恢复正向拖动
                _lastSampledCell = _dragFromHead ? _currentHeadCell : _currentTailCell;
                Debug.Log($"[蛇{SnakeId}] 倒车完成，清空倒车状态，恢复正向拖动准备");
            }

        }

        bool CheckNextPoint(Vector2Int nextCell)
        {
            if (_dragFromHead)
            {
                // 必须相邻
                if (Manhattan(_currentHeadCell, nextCell) != 1) return false;
                // 检查网格边界
                if (!_grid.IsInside(nextCell)) return false;

                // 使用与IsPathBlocked相同的阻挡检测逻辑，支持颜色匹配
                if (IsPathBlocked(nextCell)) return false;
                // 占用校验：允许进入原尾
                var bodyCell = _bodyCells.First.Next.Value;
                if (IsOccupiedBySelf(nextCell) && nextCell != bodyCell) return false;

                return true;
            }
            else
            {
                // 必须相邻
                if (Manhattan(_currentTailCell, nextCell) != 1) return false;
                // 检查网格边界
                if (!_grid.IsInside(nextCell)) return false;

                // 使用与IsPathBlocked相同的阻挡检测逻辑，支持颜色匹配
                if (IsPathBlocked(nextCell)) return false;

                // 占用校验：允许进入原头
                var bodyCell = _bodyCells.Last.Previous.Value;
                if (IsOccupiedBySelf(nextCell) && nextCell != bodyCell) return false;

                return true;
            }
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
                        _dragStartCell = _grid.WorldToCell(world);
                        _pathQueue.Clear();
                        _lastSampledCell = _dragFromHead ? _currentHeadCell : _currentTailCell;
                        _moveAccumulator = 0f;
                        _dragAxis = DragAxis.None;

                        // 开始新的拖拽时清除倒车状态
                        _isReversing = false;
                    }
                }

            }
            else if (Input.GetMouseButtonUp(0))
            {
                if (_dragging)
                {
                    // 手指松开时，记录最终路径并移动到目标位置
                    //RecordFinalPathOnRelease();

                    if(_startMove)
                    {
                        _pathQueue.Clear();
                        SnapToGrid();
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
            // 智能更新频率控制
            if (!ShouldUpdateDragVisuals())
            {
                return;
            }

            _startMove = true;
            float frac = Mathf.Clamp01(_moveAccumulator); // 确保在0-1之间

            if (_dragFromHead)
            {
                if (_pathQueue.Count > 0)
                {
                    //跨格子移动
                    Vector3 headVisual;
                    Vector3 finger = ScreenToWorld(Input.mousePosition);
                    // 跨格移动：按速度限制线性移动
                    Vector3 currentHeadPos = _grid.CellToWorld(_currentHeadCell);
                    Vector3 targetHeadPos = _grid.CellToWorld(_pathQueue.First.Value);
                    headVisual = Vector3.Lerp(currentHeadPos, targetHeadPos, frac);

                    // 更新蛇头视觉位置
                    var headRt = _cachedRectTransforms[0];
                    if (headRt != null)
                    {
                        headRt.anchoredPosition = new Vector2(headVisual.x, headVisual.y);
                    }
                    // 更新身体部分：每一节都跟随前一节移动
                    // 先计算所有身体节点的目标位置
                    Vector3[] bodyTargetPositions = new Vector3[_bodyCells.Count];
                    bodyTargetPositions[0] = headVisual; // 蛇头的视觉位置
                                                         // 计算每个身体节点的目标位置
                    for (int i = 1; i < _bodyCells.Count; i++)
                    {
                        // 跨格移动时：每个身体节都朝着前一节的当前逻辑位置移动
                        if (i == 1)
                        {
                            // 第一个身体节跟随蛇头的当前逻辑位置
                            bodyTargetPositions[i] = _grid.CellToWorld(_currentHeadCell);
                        }
                        else
                        {
                            // 其他身体节跟随前一节的逻辑位置
                            bodyTargetPositions[i] = _grid.CellToWorld(_bodyCells.ElementAt(i - 1));
                        }
                    }

                    // 应用插值并更新视觉位置
                    for (int i = 1; i < _bodyCells.Count && i < _cachedRectTransforms.Count; i++)
                    {
                        Vector3 currentBodyPos = _grid.CellToWorld(_bodyCells.ElementAt(i));
                        Vector3 targetBodyPos = bodyTargetPositions[i];
                        Vector3 bodyVisual = Vector3.Lerp(currentBodyPos, targetBodyPos, frac);

                        var bodyRt = _cachedRectTransforms[i];
                        if (bodyRt != null)
                        {
                            bodyRt.anchoredPosition = new Vector2(bodyVisual.x, bodyVisual.y);
                        }
                    }

                }
                else
                {
                    //本格子里移动

                    // 当前格子内自由移动
                    Vector3 headVisual;
                    Vector3 finger = ScreenToWorld(Input.mousePosition);
                    // 当前格子内自由移动
                    headVisual = GetEnhancedHeadVisual(finger, _currentHeadCell);
                    // 更新蛇头视觉位置
                    var headRt = _cachedRectTransforms[0];
                    if (headRt != null)
                    {
                        headRt.anchoredPosition = new Vector2(headVisual.x, headVisual.y);
                    }

                    // 更新身体部分：每一节都跟随前一节移动
                    // 先计算所有身体节点的目标位置
                    Vector3[] bodyTargetPositions = new Vector3[_bodyCells.Count];
                    bodyTargetPositions[0] = headVisual; // 蛇头的视觉位置

                    // 计算每个身体节点的目标位置
                    for (int i = 1; i < _bodyCells.Count; i++)
                    {
                        // 当前格子内移动时：身体跟随蛇头的视觉移动，确保在主方向上不分离
                        if (i == 1)
                        {
                            // 第一个身体节在主方向上跟随蛇头的视觉位置
                            bodyTargetPositions[i] = GetBodyFollowPosition(headVisual, _grid.CellToWorld(_bodyCells.ElementAt(i)), _currentHeadCell);
                        }
                        else
                        {
                            // 其他身体节跟随前一节，确保链式连接
                            bodyTargetPositions[i] = GetBodyFollowPosition(bodyTargetPositions[i - 1], _grid.CellToWorld(_bodyCells.ElementAt(i)), 0.6f);
                        }
                    }
                    // 应用插值并更新视觉位置
                    for (int i = 1; i < _bodyCells.Count && i < _cachedRectTransforms.Count; i++)
                    {
                        Vector3 currentBodyPos = _grid.CellToWorld(_bodyCells.ElementAt(i));
                        Vector3 targetBodyPos = bodyTargetPositions[i];
                        Vector3 bodyVisual = Vector3.Lerp(currentBodyPos, targetBodyPos, frac);

                        var bodyRt = _cachedRectTransforms[i];
                        if (bodyRt != null)
                        {
                            bodyRt.anchoredPosition = new Vector2(bodyVisual.x, bodyVisual.y);
                        }
                    }
                }

            }
            else
            {
                // 拖尾逻辑：计算蛇尾的视觉位置
                Vector3 tailVisual;
                Vector3 finger = ScreenToWorld(Input.mousePosition);

                if (_pathQueue.Count > 0)
                {
                    // 跨格移动：按速度限制线性移动
                    Vector3 currentTailPos = _grid.CellToWorld(_currentTailCell);
                    Vector3 targetTailPos = _grid.CellToWorld(_pathQueue.First.Value);
                    tailVisual = Vector3.Lerp(currentTailPos, targetTailPos, frac);
                }
                else
                {
                    // 当前格子内自由移动
                    tailVisual = GetEnhancedTailVisual(finger, _currentTailCell);
                }

                // 更新蛇尾视觉位置（最后一个RectTransform）
                int tailIndex = _bodyCells.Count - 1;
                if (tailIndex >= 0 && tailIndex < _cachedRectTransforms.Count)
                {
                    var tailRt = _cachedRectTransforms[tailIndex];
                    if (tailRt != null)
                    {
                        tailRt.anchoredPosition = new Vector2(tailVisual.x, tailVisual.y);
                    }
                }

                // 更新身体部分：每一节都朝着后一节的位置移动（拖尾模式）
                for (int i = _bodyCells.Count - 2; i >= 0; i--)
                {
                    // 确保索引在有效范围内
                    if (i >= _cachedRectTransforms.Count) continue;

                    Vector3 currentBodyPos = _grid.CellToWorld(_bodyCells.ElementAt(i));
                    Vector3 targetBodyPos;

                    if (i == _bodyCells.Count - 2)
                    {
                        // 倒数第二个身体节跟随蛇尾的当前逻辑位置
                        targetBodyPos = _grid.CellToWorld(_currentTailCell);
                    }
                    else
                    {
                        // 其他身体节跟随后一节的当前逻辑位置
                        int nextIndex = i + 1;
                        if (nextIndex < _bodyCells.Count)
                        {
                            targetBodyPos = _grid.CellToWorld(_bodyCells.ElementAt(nextIndex));
                        }
                        else
                        {
                            continue; // 跳过无效的索引
                        }
                    }

                    Vector3 bodyVisual = Vector3.Lerp(currentBodyPos, targetBodyPos, frac);

                    var bodyRt = _cachedRectTransforms[i];
                    if (bodyRt != null)
                    {
                        bodyRt.anchoredPosition = new Vector2(bodyVisual.x, bodyVisual.y);
                    }
                }

            }
        }

        /// <summary>
        /// 优化的折线距离计算方法
        /// </summary>
        Vector3 GetPointAlongPolyline(List<Vector3> pts, float distance)
        {
            if (pts.Count == 0) return Vector3.zero;
            if (pts.Count == 1) return pts[0];
            if (distance <= 0) return pts[0];

            float remaining = distance;

            // 优化：预计算线段长度以减少重复计算
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector3 a = pts[i];
                Vector3 b = pts[i + 1];

                // 使用平方距离比较来优化性能（避免开方）
                Vector3 diff = b - a;
                float segLenSq = diff.sqrMagnitude;
                float segLen = Mathf.Sqrt(segLenSq);

                if (remaining <= segLen)
                {
                    if (segLen <= 0.0001f) return a;
                    float t = remaining / segLen;
                    return Vector3.LerpUnclamped(a, b, t);
                }
                remaining -= segLen;
            }

            // 超出折线长度时的处理
            if (pts.Count >= 2)
            {
                Vector3 lastA = pts[pts.Count - 2];
                Vector3 lastB = pts[pts.Count - 1];
                Vector3 direction = lastB - lastA;

                // 避免归一化计算，直接使用比例
                if (direction.sqrMagnitude > 0.0001f)
                {
                    float dirLen = Mathf.Sqrt(direction.sqrMagnitude);
                    direction = direction / dirLen;
                    return lastB + direction * remaining;
                }
            }

            return pts[pts.Count - 1];
        }

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
            //// 必须相邻
            //if (Manhattan(_currentHeadCell, nextCell) != 1) return false;
            //// 检查网格边界
            //if (!_grid.IsInside(nextCell)) return false;
            //// 使用与IsPathBlocked相同的阻挡检测逻辑，支持颜色匹配
            //if (IsPathBlocked(nextCell)) return false;
            //// 占用校验：允许进入原尾
            //var tailCell = _bodyCells.Last.Value;
            //if (IsOccupiedBySelf(nextCell) && nextCell != tailCell) return false;


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

        /// <summary>
        /// 生成智能倒车路径，支持转向避障
        /// </summary>
        /// <param name="startPos">起始位置</param>
        /// <param name="candidates">候选方向</param>
        /// <param name="isTailReverse">是否为拖尾倒车</param>
        /// <returns>倒车路径点列表</returns>
        List<Vector2Int> GenerateSmartReversePath(Vector2Int startPos, Vector2Int[] candidates, bool isTailReverse)
        {
            var path = new List<Vector2Int>();

            // 尝试每个候选方向，只生成一步倒车路径
            for (int dirIndex = 0; dirIndex < candidates.Length; dirIndex++)
            {
                var currentDir = candidates[dirIndex];
                var nextPos = startPos + currentDir;

                // 检查是否可以移动到该位置
                if (!_grid.IsInside(nextPos.x, nextPos.y)) continue;
                if (IsPathBlocked(nextPos)) continue;
                if (!IsCellFree(nextPos)) continue;

                // 找到第一个可用的倒车位置就返回
                path.Add(nextPos);
                return path;
            }

            return path; // 返回空路径表示无法倒车
        }

        /// <summary>
        /// 将倒车路径步骤加入路径队列
        /// </summary>
        /// <param name="reversePath">倒车路径</param>
        /// <param name="isTailReverse">是否为拖尾倒车</param>
        void EnqueueReversePathSteps(List<Vector2Int> reversePath, bool isTailReverse)
        {
            // 如果不是倒车状态，清空队列开始倒车
            if (!_isReversing)
            {
                _pathQueue.Clear();
                Debug.Log($"[蛇{SnakeId}] 开始倒车序列");
            }

            // 添加倒车步骤到路径队列（追加到现有倒车路径）
            foreach (var step in reversePath)
            {
                _pathQueue.AddLast(step);
            }

            // 设置拖拽状态以匹配倒车类型
            _dragFromHead = !isTailReverse; // 拖尾倒车时设为false，拖头倒车时设为true

            // 设置倒车状态
            _isReversing = true;

            Debug.Log($"[蛇{SnakeId}] 添加倒车步骤，当前倒车队列长度：{_pathQueue.Count}，拖拽类型：{(_dragFromHead ? "拖头" : "拖尾")}");
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

            head = new Vector3(_headRt.anchoredPosition.x, _headRt.anchoredPosition.y, 0f);
            tail = new Vector3(_tailRt.anchoredPosition.x, _tailRt.anchoredPosition.y, 0f);

            float headDist = Vector3.Distance(world, head);
            float tailDist = Vector3.Distance(world, tail);
            if (Mathf.Min(headDist, tailDist) > _grid.CellSize * 0.8f) return false;
            onHead = headDist <= tailDist;
            return true;
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
            if (!ShowDebugStats) return;
            GUI.color = Color.white;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            GUILayout.BeginArea(new Rect(10, 10, 400, 200), GUI.skin.box);
            GUILayout.Label($"Queue: {_pathQueue.Count}", style);
            GUILayout.Label($"Accumulator: {_moveAccumulator:F2}", style);
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

        /// <summary>
        /// 优化的蛇头折线更新
        /// </summary>
        void UpdatePolylineForHead(Vector3 headVisual, bool forceRebuild)
        {
            if (forceRebuild || _tempPolylinePoints.Count == 0)
            {
                // 完全重建折线
                _tempPolylinePoints.Clear();
                _tempPolylinePoints.Add(headVisual);

                var it = _bodyCells.First;
                if (it != null) it = it.Next; // 跳过头部
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
        }

        /// <summary>
        /// 智能判断是否需要更新拖动视觉效果
        /// </summary>
        bool ShouldUpdateDragVisuals()
        {
            float currentTime = Time.time;
            float timeSinceLastUpdate = currentTime - _lastDragUpdateTime;

            // 基本频率限制
            if (timeSinceLastUpdate < DRAG_UPDATE_INTERVAL)
            {
                return false;
            }

            // 如果路径队列很少或为空，提高更新频率以改善平滑度
            if (_pathQueue.Count <= 2)
            {
                float enhancedInterval = DRAG_UPDATE_INTERVAL * 0.5f; // 双倍频率
                if (timeSinceLastUpdate >= enhancedInterval)
                {
                    _lastDragUpdateTime = currentTime;
                    return true;
                }
                return false;
            }

            // 正常情况下的更新
            _lastDragUpdateTime = currentTime;
            return true;
        }

        /// <summary>
        /// 优化的蛇尾折线更新
        /// </summary>
        void UpdatePolylineForTail(Vector3 tailVisual)
        {
            // 构建从尾部开始的折线
            _tempPolylinePoints.Clear();
            _tempPolylinePoints.Add(tailVisual);

            // 添加身体段位置（从尾部向头部）
            var it = _bodyCells.Last;
            if (it != null) it = it.Previous; // 跳过尾部
            while (it != null)
            {
                _tempPolylinePoints.Add(_grid.CellToWorld(it.Value));
                it = it.Previous;
            }
        }

        /// <summary>
        /// 计算松开后的速度倍数
        /// </summary>
        void CalculateReleaseSpeedMultiplier()
        {
            int pathLength = _pathQueue.Count;

            if (pathLength <= RELEASE_SPEED_PATH_THRESHOLD)
            {
                // 路径太短，使用正常速度
                _releaseSpeedMultiplier = MIN_RELEASE_SPEED_MULTIPLIER;
                _isReleaseMovement = false;
            }
            else
            {
                // 根据路径长度计算速度倍数
                // 使用平方根函数让长路径的速度增益递减
                float normalizedLength = Mathf.Sqrt(pathLength - RELEASE_SPEED_PATH_THRESHOLD) / Mathf.Sqrt(20f); // 基于20个路径点的正则化
                normalizedLength = Mathf.Clamp01(normalizedLength);

                _releaseSpeedMultiplier = Mathf.Lerp(MIN_RELEASE_SPEED_MULTIPLIER, MAX_RELEASE_SPEED_MULTIPLIER, normalizedLength);
                _isReleaseMovement = true;

                Debug.Log($"[蛇{SnakeId}] 松开后加速: 路径长度={pathLength}, 速度倍数={_releaseSpeedMultiplier:F2}x");
            }
        }

        /// <summary>
        /// 检查手指松开后的洞吞噬逻辑
        /// </summary>
        void CheckHoleConsumptionOnRelease(Vector2Int targetCell)
        {
            // 检查目标位置本身或邻近位置是否有洞
            var hole = FindHoleAtOrAdjacent(targetCell);
            if (hole != null && hole.CanInteractWithSnake(this))
            {
                Debug.Log($"[蛇{SnakeId}] 手指松开后检测到目标位置或邻近的可交互洞，准备触发吞噬");

                // 延迟触发吞噬，等待蛇移动到目标位置附近
                StartCoroutine(DelayedHoleConsumption(hole));
            }
        }

        /// <summary>
        /// 延迟的洞吞噬检查
        /// </summary>
        IEnumerator DelayedHoleConsumption(HoleEntity hole)
        {
            // 等待蛇移动到目标位置附近
            while (_pathQueue.Count > 0)
            {
                yield return null;
            }

            // 再次检查是否可以与洞交互
            var currentDragEnd = _dragFromHead ? _currentHeadCell : _currentTailCell;

            // 检查是否在洞的位置或邻近位置，并且颜色匹配
            bool canInteract = false;
            if (hole.Cell == currentDragEnd)
            {
                // 蛇头/尾在洞的位置上
                canInteract = hole.CanInteractWithSnake(this);
                Debug.Log($"[蛇{SnakeId}] 蛇头/尾在洞的位置上，可交互: {canInteract}");
            }
            else if (hole.IsAdjacent(currentDragEnd))
            {
                // 蛇头/尾邻近洞
                canInteract = hole.CanInteractWithSnake(this);
                Debug.Log($"[蛇{SnakeId}] 蛇头/尾邻近洞，可交互: {canInteract}");
            }

            if (canInteract)
            {
                Debug.Log($"[蛇{SnakeId}] 延迟检查确认，开始吞噬进程");
                _consumeCoroutine ??= StartCoroutine(CoConsume(hole, _dragFromHead));
            }
            else
            {
                Debug.Log($"[蛇{SnakeId}] 延迟检查失败，无法与洞交互");
            }
        }

        /// <summary>
        /// 手指松开时记录最终路径
        /// </summary>
        void RecordFinalPathOnRelease()
        {
            var world = ScreenToWorld(Input.mousePosition);
            var finalTargetCell = ClampInside(_grid.WorldToCell(world));
            var currentDragCell = _dragFromHead ? _currentHeadCell : _currentTailCell;

            // 如果手指松开位置与当前拖动端不同，生成最终路径
            if (finalTargetCell != currentDragCell)
            {
                Debug.Log($"[蛇{SnakeId}] 手指松开：从({currentDragCell.x},{currentDragCell.y})到({finalTargetCell.x},{finalTargetCell.y})");

                // 清空当前路径队列
                _pathQueue.Clear();

                // 生成最终路径
                EnqueueOptimizedPath(currentDragCell, finalTargetCell);

                // 重置移动累积器以确保平滑移动
                _moveAccumulator = 0f;

                // 计算并设置松开后的速度倍数
                CalculateReleaseSpeedMultiplier();

                // 检查最终目标位置是否邻近洞，如果是则触发吞噬
                CheckHoleConsumptionOnRelease(finalTargetCell);

                Debug.Log($"[蛇{SnakeId}] 最终路径生成完成，路径长度: {_pathQueue.Count}");
            }
            else
            {
                // 手指松开位置就是当前位置，清空路径并对齐网格
                _pathQueue.Clear();
                SnapToGrid();
            }
        }

        /// <summary>
        /// 预测拖动目标位置
        /// </summary>
        Vector2Int PredictDragTarget(Vector2Int currentTarget)
        {
            Vector2Int currentPos = _dragFromHead ? _currentHeadCell : _currentTailCell;
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


            EnqueueAxisAlignedPath(from, to);
        }

        /// <summary>
        /// 计算动态移动速度
        /// </summary>
        float CalculateDynamicMoveSpeed()
        {
            float baseSpeed = MoveSpeedCellsPerSecond;

            // 如果处于松开后的快速移动状态，优先使用松开速度倍数
            if (_isReleaseMovement)
            {
                return baseSpeed * _releaseSpeedMultiplier;
            }

            // 只在拖动时启用动态速度
            if (!_dragging) return baseSpeed;


            return baseSpeed; // 正常速度
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


