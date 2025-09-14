using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ReGecko.GridSystem;
using ReGecko.Grid.Entities;
using System.Collections;
using ReGecko.GameCore.Flow;
using System.Linq;
using ReGecko.Game;
using System;

namespace ReGecko.SnakeSystem
{
    public class SnakeController : BaseSnake
    {
        // 在 SnakeController 类字段区添加
        [SerializeField] bool DebugShowLeadTarget = true;
        [SerializeField] Color DebugLeadTargetColor = Color.red;
        [SerializeField] float DebugLeadMarkerSize = 8f;

        [SerializeField] bool DebugShowCenterline = true;
        [SerializeField] Color DebugPolylineColor = new Color(0f, 1f, 0f, 0.9f);
        [SerializeField] Color DebugPolylineHeadColor = new Color(0f, 0.9f, 1f, 1f);
        [SerializeField] Color DebugPolylineTailColor = new Color(1f, 0.3f, 1f, 1f);
        [SerializeField] float DebugPolylinePointSize = 10f;

        [Header("SnakeController特有属性")]
        // 拖拽相关
        bool _startMove;
        bool _isReverse;


        private List<GameObject> _segments = new List<GameObject>();
        protected readonly LinkedList<Vector2Int> _bodyCells = new LinkedList<Vector2Int>(); // 离散身体占用格，头在First
        private readonly List<RectTransform> _cachedRectTransforms = new List<RectTransform>();

        private HashSet<HoleEntity> _cacheCoconsumeHoles = new HashSet<HoleEntity>();


        public override LinkedList<Vector2Int> GetBodyCells()
        {
            return _bodyCells;
        }

        public override List<GameObject> GetSegments()
        {
            return _segments;
        }

        // 移动相关
        private LinkedList<Vector2Int> _cellPathQueue = new LinkedList<Vector2Int>(); // 小格路径队列


        private Vector2Int _currentHeadCell;
        private Vector2Int _currentTailCell;

        //优化缓存
        Vector3 _lastMousePos;

        // 拖动优化：减少更新频率
        private float _lastDragUpdateTime = 0f;
        private const float DRAG_UPDATE_INTERVAL = 0.008f; // 约120FPS更新频率


        enum DragAxis { None, X, Y }
        DragAxis _dragAxis = DragAxis.None;

        Coroutine _consumeCoroutine;

        // —— 平滑路径模式 开关与缓存 ——
        [SerializeField] bool EnableSmoothPathMode = true;

        List<Vector2> _leadPathPoints = new List<Vector2>(512); // 记录拖动端（头/尾）已经经过的小格中心（世界坐标，按时间顺序）
        Vector2 _leadPos;                // 拖动端当前连续位置（世界坐标）
        Vector2 _leadTargetPos;          // 正在朝向的目标小格中心（世界坐标）
        Vector2 _leadReversePos;         // 倒车的目标点（世界坐标）
        Vector2Int _leadCurrentSubCell;  // 拖动端当前所在小格（中线）
        Vector2Int _leadTargetCell;   // 拖动端当前目标小格（中线）

        Vector2Int _leadReverseCell;  // 拖动端倒车目标小格（中线）

        bool _smoothInited = false;
        bool _lastDragFromHead = true;   // 记录上次拖动端，切换时重新初始化
        Vector2Int[] _tmpSubSnap;        // 仅用于初始化时从链表拷贝（零分配复用）

        float _segmentspacing;           // 每段身体之间固定间距（世界单位）
        float _leadSpeedWorld;           // 拖动端线速度（世界单位/秒）
        const float EPS = 1e-4f;
        Vector2Int _lastLeadTargetSubCell = Vector2Int.zero;

        float _cachedSpeedInput;
        float _cachedCellSize;

        // 平滑公共
        bool _lastActiveFromHead;
        bool _lastActiveReverse;

        // 活动端（正向=拖动端；倒车时=对端）
        Vector2 _activeLeadPos;
        List<Vector2> _activeLeadPath = new List<Vector2>(512);

        // 新增：中线折线缓存，减少GC
        List<Vector2> _centerlinePolyline = new List<Vector2>(512);

        // 扫描缓存
        Vector2[] _tmpBodyPos;
        float[] _distTargets;
        Vector2Int[] _tmpSubSnap2;


        public override void Initialize(GridConfig grid)
        {
            _grid = grid;
            IsDragging = false;
            DragFromHead = false;


            BuildSegments();
            InitializeSegmentPositions(InitialBodyCells);
            InitializeBodySpriteManager();
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

                if (!EnableBodySpriteManagement)
                {
                    // UI渲染：使用Image组件
                    var image = go.AddComponent<Image>();
                    image.sprite = BodySprite;
                    image.color = BodyColor;
                    image.raycastTarget = false;
                }
                else
                {
                    go.AddComponent<RectTransform>();
                }

                // 设置RectTransform（正确的锚点和轴心）
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(_grid.CellSize, _grid.CellSize);

                _segments.Add(go);
                _cachedRectTransforms.Add(rt); // 缓存RectTransform组件
            }
        }


        void InitializeSegmentPositions(Vector2Int[] initialbodycells)
        {
            // 构建初始身体格（优先使用配置）
            List<Vector2Int> cells = new List<Vector2Int>();
            if (initialbodycells != null && initialbodycells.Length > 0)
            {
                for (int i = 0; i < initialbodycells.Length && i < Length; i++)
                {
                    var c = ClampInside(initialbodycells[i]);
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
                Debug.LogError("蛇配置信息未找到，创建默认蛇");
                var head = ClampInside(InitialHeadCell);
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
                    var head = ClampInside(InitialHeadCell);
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

            // 初始放置完成后，更新身体图片
            if (EnableBodySpriteManagement && _bodySpriteManager != null)
            {
                _bodySpriteManager.OnSnakeLengthChanged();
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


        void Update()
        {
        }

        public override void UpdateMovement()
        {


            // 如果蛇已被完全消除或组件被销毁，停止所有移动更新
            if (_bodyCells.Count == 0 || !IsAlive() || _cachedRectTransforms.Count == 0)
            {
                return;
            }


            if (_consuming)
            {
                // 清理已销毁的组件
                CleanupCachedComponents();
                return;
            }

            if (EnableSmoothPathMode)
            {
                UpdateSmoothPathPointsMovement();
            }
        }


        // *** SubGrid 改动：恒定速度沿路径移动 ***
        void UpdateSmoothPathPointsMovement()
        {
            // 基础校验
            if (_bodyCells == null || _bodyCells.Count == 0 || _cachedRectTransforms.Count == 0) return;

            _isReverse = false;

            // 速度/间距（世界单位）
            RefreshKinematicsIfNeeded();
            // 采样鼠标 → 期望小格（中线）
            var world = ScreenToWorld(Input.mousePosition);
            Vector2Int targetCell = _grid.WorldToCell(world);


            var leadCurrentCell = DragFromHead ? GetHeadCell() : GetTailCell();


            // 取“一步”的目标格
            _cellPathQueue ??= new LinkedList<Vector2Int>();
            _cellPathQueue.Clear();
            EnqueueBigCellPath(leadCurrentCell, targetCell, _cellPathQueue, 1);
            if (_cellPathQueue.Count > 0)
            {
                //跨格子移动
                _leadTargetCell = _cellPathQueue.First.Value;

                // 倒车判定（你已有）
                if (CheckIfNeedReverseByCell(_leadTargetCell, out _leadReverseCell))
                {
                    _isReverse = true;
                }
                else
                {
                    if (_leadTargetCell == (DragFromHead ? _bodyCells.First.Next.Value : _bodyCells.Last.Previous.Value))
                    {
                        return;
                    }
                }

                if (!_isReverse)
                {
                    // 检查目标点合法性
                    if (!CheckNextCell(_leadTargetCell))
                    {
                        //Debug.LogError("CheckNextCell return!");
                        return;
                    }
                }


                if (_isReverse)
                {
                    _leadReversePos = _grid.CellToWorld(_leadReverseCell);
                }
                else
                {
                    _leadTargetPos = _grid.CellToWorld(_leadTargetCell);
                }
            }
            else
            {
                //小格子移动
                return;
            }


            // 1) 确定“活动端”：正向=DragFromHead；倒车时取相反端
            bool activeFromHead = _isReverse ? !DragFromHead : DragFromHead;

            // 2) 初始化活动端的平滑状态（首次或切换端时）
            EnsureActiveLeadInited(activeFromHead);

            // 3) 目标点（连续世界坐标）
            Vector2 targetPos;
            if (_isReverse)
            {
                // 倒车：活动端朝 _leadReversePos
                targetPos = _leadReversePos;
            }
            else
            {
                // 正向：活动端朝“鼠标投影到最近中线”的连续世界坐标
                targetPos = _leadTargetPos;
            }


            // 4) 活动端沿目标插值
            bool reachedThisFrame = false;
            Vector2 dir = targetPos - _activeLeadPos;
            float dist = dir.magnitude;
            if (dist > EPS)
            {
                float step = _leadSpeedWorld * Time.deltaTime;
                if (step + 1e-3f >= dist)
                {
                    _activeLeadPos = targetPos;
                    reachedThisFrame = true;
                }
                else
                {
                    _activeLeadPos += dir * (step / dist);
                }
            }
            else
            {
                reachedThisFrame = true;
            }


            // 5) 路径历史：位移达阈值才采样，降低抖动
            if (_activeLeadPath.Count == 0)
            {
                _activeLeadPath.Add(_activeLeadPos);
            }
            else
            {
                var last = _activeLeadPath[_activeLeadPath.Count - 1];
                if (Vector2.Distance(last, _activeLeadPos) >= 0.10f * _segmentspacing)
                    _activeLeadPath.Add(_activeLeadPos);
            }

            // 6) 裁剪历史：仅保留覆盖全身需要的长度
            PruneLeadPathHistory(_activeLeadPath);

            // 7) 由“活动端当前位置+历史”生成整条蛇的连续位置并写入可视（单调扫描 O(n)）
            ApplySmoothVisualsByPath(activeFromHead);

            if (reachedThisFrame)
            {
                UpdateBodyCellsFromCachedRectTransforms();

                // 5) 刷新缓存
                _currentHeadCell = _bodyCells.First.Value;
                _currentTailCell = _bodyCells.Last.Value;

                //刷新碰撞缓存
                SnakeManager.Instance.InvalidateOccupiedCellsCache();

                // 洞检测：若拖动端在洞位置或临近洞，且颜色匹配，触发吞噬
                {
                    var hole = FindHoleAtOrAdjacentWithColor(_currentHeadCell, ColorType);
                    if (hole != null)
                    {
                        _consumeCoroutine ??= StartCoroutine(CoConsume(hole, true));
                        return;
                    }
                }
                {
                    var hole = FindHoleAtOrAdjacentWithColor(_currentTailCell, ColorType);
                    if (hole != null)
                    {
                        _consumeCoroutine ??= StartCoroutine(CoConsume(hole, false));
                        return;
                    }
                }
            }
        }

        // —— 工具与缓存 ——
        // —— 相关辅助（本方法内使用） ——

        void EnsureActiveLeadInited(bool fromHead)
        {
            // 若首次或端发生切换，则重建历史并设置活动端初始位置
            if (!_smoothInited || _lastActiveFromHead != fromHead || _lastActiveReverse != _isReverse)
            {
                InitializeSmoothPathFromEnd(fromHead);
                _lastActiveFromHead = fromHead;
                _lastActiveReverse = _isReverse;
                _smoothInited = true;
            }
        }

        void InitializeSmoothPathFromEnd(bool fromHead)
        {
            _activeLeadPath.Clear();

            // 活动端当前位置（用链表端的小格中心初始化）
            Vector2Int endCell = fromHead ? GetHeadCell() : GetTailCell();
            var w = _grid.CellToWorld(endCell);
            _activeLeadPos = new Vector2(w.x, w.y);

            // 用整条链表构造“活动端路径历史”（末尾靠近活动端）
            int n = _bodyCells.Count;
            if (_tmpSubSnap2 == null || _tmpSubSnap2.Length < n) _tmpSubSnap2 = new Vector2Int[Mathf.NextPowerOfTwo(n)];
            for (int i = 0; i < n; i++)
                _tmpSubSnap2[i] = GetBodyCellAtIndex(i);

            if (fromHead)
            {
                // 历史应为：靠尾的点在前，靠头的点在后；末尾一个就是“活动端上一格的中心”
                for (int i = n - 1; i >= 1; i--) _activeLeadPath.Add(_grid.CellToWorld(_tmpSubSnap2[i]));
            }
            else
            {
                // 从尾侧开始：历史为靠头的点在前，靠尾的点在后；末尾一个是“活动端上一格的中心”
                for (int i = 0; i < n - 1; i++) _activeLeadPath.Add(_grid.CellToWorld(_tmpSubSnap2[i]));
            }
        }

        void PruneLeadPathHistory(List<Vector2> path)
        {
            int n = _bodyCells.Count;
            if (n <= 1 || path.Count == 0) return;

            float need = (n - 1) * _segmentspacing + 2f * _segmentspacing;

            // 估算总长度：活动端当前位置 → history[末] → ... → history[0]
            float total = 0f;
            Vector2 p = _activeLeadPos;
            for (int i = path.Count - 1; i >= 0; i--)
            {
                total += Vector2.Distance(p, path[i]);
                p = path[i];
            }

            float remove = total - need;
            while (path.Count > 1 && remove > 0f)
            {
                var a = path[0];
                var b = path[1];
                float seg = Vector2.Distance(a, b);
                if (seg <= remove)
                {
                    path.RemoveAt(0);
                    remove -= seg;
                }
                else break;
            }
        }
        void ApplySmoothVisualsByPath(bool activeFromHead)
        {
            int n = _bodyCells.Count;
            if (n == 0) return;

            EnsureDistBuffer(n);
            for (int i = 0; i < n; i++) _distTargets[i] = i * _segmentspacing;

            Vector2 SnapToCenterline(Vector2 p)
            {
                var sub = SubGridHelper.WorldToSubCell(new Vector3(p.x, p.y, 0f), _grid);
                var w = SubGridHelper.SubCellToWorld(sub, _grid);
                return new Vector2(w.x, w.y);
            }

            // 1) 构建严格中线折线（按小格一步一格，恒等间距 = subStep）
            _centerlinePolyline.Clear();

            float subStep = _segmentspacing * SubGridHelper.SUB_CELL_SIZE; // = CellSize/5

            // 从活动端当前位置（已夹紧在中线的小格中心）开始
            var startSub = SubGridHelper.WorldToSubCell(new Vector3(_activeLeadPos.x, _activeLeadPos.y, 0f), _grid);
            var startW = SubGridHelper.SubCellToWorld(startSub, _grid);
            _centerlinePolyline.Add(new Vector2(startW.x, startW.y));

            // 工具：从一个小格走到另一个小格，沿中线每步±1，逐步写世界坐标
            void AppendSubgridSteps(Vector2Int fromSub, Vector2Int toSub)
            {
                if (fromSub == toSub) return;

                Vector2Int cur = fromSub;
                // 每次只能在一个轴上走一步，直到到达 toSub
                while (cur != toSub)
                {
                    if (cur.x != toSub.x)
                    {
                        int sx = (toSub.x > cur.x) ? 1 : -1;
                        cur = new Vector2Int(cur.x + sx, cur.y);
                    }
                    else if (cur.y != toSub.y)
                    {
                        int sy = (toSub.y > cur.y) ? 1 : -1;
                        cur = new Vector2Int(cur.x, cur.y + sy);
                    }
                    var w = SubGridHelper.SubCellToWorld(cur, _grid);
                    Vector2 wp = new Vector2(w.x, w.y);
                    // 去重：避免重复点
                    if (_centerlinePolyline.Count == 0 ||
                        Vector2.Distance(_centerlinePolyline[_centerlinePolyline.Count - 1], wp) > 1e-6f)
                    {
                        _centerlinePolyline.Add(wp);
                    }
                }
            }

            // 依次把“活动端路径历史”投影到中线并拼接（使用中线曼哈顿路径，展开到每个小步）
            var prevSub = startSub;
            for (int i = _activeLeadPath.Count - 1; i >= 0; i--)
            {
                var nextSub = SubGridHelper.WorldToSubCell(new Vector3(_activeLeadPath[i].x, _activeLeadPath[i].y, 0f), _grid);
                if (nextSub == prevSub) continue;

                var nodes = SubGridHelper.GenerateCenterLinePath(prevSub, nextSub); // 2~4个拐点
                for (int k = 1; k < nodes.Length; k++)
                {
                    AppendSubgridSteps(nodes[k - 1], nodes[k]); // 展开为“每步±1”的序列
                }
                prevSub = nextSub;
            }

            // 注意：此时 _centerlinePolyline 相邻点的实际距离即 subStep（横/竖每步恰好一小格）。
            // 后续 2) 等距采样 与 3) LineRenderer 同原逻辑
            int idx = 1;
            Vector2 cur2 = _centerlinePolyline[0];
            Vector2 nxt2 = (idx < _centerlinePolyline.Count) ? _centerlinePolyline[idx] : _centerlinePolyline[0];
            float seg = Vector2.Distance(cur2, nxt2);
            float acc = 0f;

            for (int i = 0; i < n; i++)
            {
                float target = _distTargets[i];

                while (acc + seg + EPS < target && idx < _centerlinePolyline.Count - 1)
                {
                    acc += seg;
                    cur2 = nxt2;
                    idx++;
                    nxt2 = _centerlinePolyline[idx];
                    seg = Vector2.Distance(cur2, nxt2);
                }

                Vector2 pos;
                if (seg <= EPS) pos = cur2;
                else
                {
                    float need = Mathf.Clamp(target - acc, 0f, seg);
                    float t = need / seg;
                    pos = Vector2.LerpUnclamped(cur2, nxt2, t);
                }

                int visIndex = activeFromHead ? i : (n - 1 - i);
                if (visIndex < _cachedRectTransforms.Count)
                {
                    var rt = _cachedRectTransforms[visIndex];
                    if (rt != null)
                        rt.anchoredPosition = pos;
                }
            }

            // 3) 直接用中线折线更新LineRenderer
            if (EnableBodySpriteManagement && _bodySpriteManager != null)
            {
                _bodySpriteManager.UpdateLineFromPolyline(
                    _centerlinePolyline,
                    _cachedRectTransforms.Count,
                    _segmentspacing,
                    activeFromHead
                );
            }
        }

        // 速度/间距缓存
        void RefreshKinematicsIfNeeded()
        {
            if (!Mathf.Approximately(_cachedSpeedInput, MoveSpeedCellsPerSecond) ||
                !Mathf.Approximately(_cachedCellSize, _grid.CellSize))
            {
                _cachedSpeedInput = MoveSpeedCellsPerSecond;
                _cachedCellSize = _grid.CellSize;
                _segmentspacing = _grid.CellSize;
                _leadSpeedWorld = _cachedSpeedInput * _segmentspacing;
            }
        }
        void EnsureDistBuffer(int n)
        {
            if (_tmpBodyPos == null || _tmpBodyPos.Length < n) _tmpBodyPos = new Vector2[Mathf.NextPowerOfTwo(n)];
            if (_distTargets == null || _distTargets.Length < n) _distTargets = new float[Mathf.NextPowerOfTwo(n)];
        }

        //每一小段身体移动完成之后
        void AfterSmoothVisualsByPath()
        {
            //UpdateSubBodyCellsFromCachedSubRectTransforms();

            // 5) 刷新缓存
            _currentHeadCell = _bodyCells.First.Value;
            _currentTailCell = _bodyCells.Last.Value;

            //刷新碰撞缓存
            SnakeManager.Instance.InvalidateOccupiedCellsCache();

            // 洞检测：若拖动端在洞位置或临近洞，且颜色匹配，触发吞噬
            {
                var hole = FindHoleAtOrAdjacentWithColor(_currentHeadCell, ColorType);
                if (hole != null)
                {
                    _consumeCoroutine ??= StartCoroutine(CoConsume(hole, true));
                    return;
                }
            }
            {
                var hole = FindHoleAtOrAdjacentWithColor(_currentTailCell, ColorType);
                if (hole != null)
                {
                    _consumeCoroutine ??= StartCoroutine(CoConsume(hole, false));
                    return;
                }
            }

            //更新身体图片
            if (EnableBodySpriteManagement)
            {
                _bodySpriteManager?.OnSnakeMoved();
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

            // 正常情况下的更新
            _lastDragUpdateTime = currentTime;
            return true;
        }

        bool CheckOccupiedBySelfForword(Vector2Int bigcell)
        {
            var exclude = DragFromHead ? GetHeadCell() : GetTailCell();
            if (bigcell == exclude)
            {
                return true;
            }

            var cellset = SnakeManager.Instance.GetSnakeOccupiedCells(this);
            if (cellset != null)
                return !cellset.Contains(bigcell);

            return false;
        }


        bool CheckOccupiedBySelfReverse(Vector2Int bigcell)
        {
            if (SnakeManager.Instance == null)
            {
                return false;
            }

            var exclude = DragFromHead ? GetTailCell() : GetHeadCell();

            if (bigcell == exclude)
            {
                return true;
            }

            var cellset = SnakeManager.Instance.GetSnakeOccupiedCells(this);
            if (cellset != null)
                return !cellset.Contains(bigcell);

            return false;
        }

        bool CheckIfNeedReverseByCell(Vector2Int nextCell, out Vector2Int newNextCell)
        {
            newNextCell = nextCell;
            var leadtargetcell = DragFromHead ? GetHeadCell() : GetTailCell();


            if (nextCell == leadtargetcell)
                return false;

            if (!_bodyCells.Contains(nextCell))
                return false;


            if (DragFromHead)
            {
                var tail = GetTailCell();
                var prev = GetBodyCellAtIndex(_bodyCells.Count - 2);
                Vector2Int dir = tail - prev;
                Vector2Int left = new Vector2Int(-dir.y, dir.x);
                Vector2Int right = new Vector2Int(dir.y, -dir.x);
                var candidates = new[] { dir, left, right };
                for (int i = 0; i < candidates.Length; i++)
                {
                    var nextBig = tail + candidates[i];
                    if (!_grid.IsInside(nextBig)) continue;
                    if (IsPathBlocked(nextBig)) continue;
                    if (!CheckOccupiedBySelfReverse(nextBig)) continue;

                    EnqueueBigCellPath(GetTailCell(), nextBig, _cellPathQueue, 1);
                    if (_cellPathQueue.Count > 0)
                    {
                        newNextCell = _cellPathQueue.First.Value;
                        return true;
                    }
                }

                return false;
            }
            else
            {
                var head = GetHeadCell();
                var next = GetBodyCellAtIndex(1); // 头部相邻的身体
                Vector2Int dir = head - next; // 远离身体方向
                Vector2Int left = new Vector2Int(-dir.y, dir.x);
                Vector2Int right = new Vector2Int(dir.y, -dir.x);
                var candidates = new[] { dir, left, right };

                //优先走大格
                for (int i = 0; i < candidates.Length; i++)
                {
                    var nextHeadBig = head + candidates[i];
                    if (!_grid.IsInside(nextHeadBig)) continue;
                    if (IsPathBlocked(nextHeadBig)) continue;
                    if (!CheckOccupiedBySelfReverse(nextHeadBig)) continue;

                    EnqueueBigCellPath(GetHeadCell(), nextHeadBig, _cellPathQueue, 1);

                    if (_cellPathQueue.Count > 0)
                    {
                        newNextCell = _cellPathQueue.First.Value;
                        return true;

                    }
                }

                return false;
            }

            return false;
        }
        /*
        bool CheckIfNeedReverse(Vector2 targetPos, out Vector2 reversePos)
        {
            reversePos = Vector2.zero;

            //先检查是否达成倒车条件--检查是否是头尾两格范围内的点
            var targetSubCell = SubGridHelper.WorldToSubCell(targetPos, _grid);
            if (DragFromHead)
            {
                var headsubcell = GetBodyCellAtIndex(0);
                if (targetSubCell == headsubcell)
                {
                    return true;
                }

                var headsubcell2 = GetBodyCellAtIndex(1);
                if (targetSubCell != headsubcell2)
                    return false;
            }
            else
            {
                var tailsubcell = GetBodyCellAtIndex(_bodyCells.Count - 1);
                if (targetSubCell == tailsubcell)
                {
                    return true;
                }
                var tailsubcell2 = GetBodyCellAtIndex(_bodyCells.Count - 1 - 1);
                if (targetSubCell != tailsubcell2)
                    return false;
            }

            //再检查倒车点
            if (DragFromHead)
            {
                //优先走大格
                var tail = GetTailCell();
                var prevSub = GetBodyCellAtIndex(_bodyCells.Count - 1 - 5);
                var prev = SubGridHelper.SubCellToBigCell(prevSub);
                Vector2Int dir = tail - prev;
                Vector2Int left = new Vector2Int(-dir.y, dir.x);
                Vector2Int right = new Vector2Int(dir.y, -dir.x);
                var candidates = new[] { dir, left, right };
                for (int i = 0; i < candidates.Length; i++)
                {
                    var nextBig = tail + candidates[i];
                    if (!_grid.IsInside(nextBig)) continue;
                    if (IsPathBlocked(nextBig)) continue;
                    if (!CheckOccupiedBySelfReverse(nextBig)) continue;

                    EnqueueBigCellPath(GetTailCell(), nextBig, _cellPathQueue, 1);
                    if (_cellPathQueue.Count > 0)
                    {
                        reversePos = SubGridHelper.SubCellToWorld(_cellPathQueue.First.Value, _grid);
                        return true;
                    }
                }

                //再走小格
                tail = GetTailCell();
                prev = GetBodyCellAtIndex(_bodyCells.Count - 1 - 1);
                dir = tail - prev;
                left = new Vector2Int(-dir.y, dir.x);
                right = new Vector2Int(dir.y, -dir.x);
                candidates = new[] { dir, left, right };
                for (int i = 0; i < candidates.Length; i++)
                {
                    var nextHeadSub = tail + candidates[i];
                    var nextHeadBig = SubGridHelper.SubCellToBigCell(nextHeadSub);
                    if (!_grid.IsInside(nextHeadBig)) continue;
                    if (!_grid.IsInsideSub(nextHeadSub)) continue;
                    if (IsPathBlocked(nextHeadBig)) continue;
                    if (!CheckOccupiedBySelfReverse(nextHeadBig)) continue;

                    EnqueueSubCellPath(_currentTailCell, nextHeadSub, _cellPathQueue, 1);
                    if (_cellPathQueue.Count > 0)
                    {
                        reversePos = SubGridHelper.SubCellToWorld(_cellPathQueue.First.Value, _grid);
                        return true;
                    }
                }

                return false;
            }
            else
            {
                var head = GetHeadCell();
                var nextSub = GetBodyCellAtIndex(5);
                var next = SubGridHelper.SubCellToBigCell(nextSub); // 头部相邻的身体
                Vector2Int dir = head - next; // 远离身体方向
                Vector2Int left = new Vector2Int(-dir.y, dir.x);
                Vector2Int right = new Vector2Int(dir.y, -dir.x);
                var candidates = new[] { dir, left, right };

                //优先走大格
                for (int i = 0; i < candidates.Length; i++)
                {
                    var nextHeadBig = head + candidates[i];
                    if (!_grid.IsInside(nextHeadBig)) continue;
                    if (IsPathBlocked(nextHeadBig)) continue;
                    if (!CheckOccupiedBySelfReverse(nextHeadBig)) continue;

                    EnqueueBigCellPath(GetHeadCell(), nextHeadBig, _cellPathQueue, 1);

                    if (_cellPathQueue.Count > 0)
                    {
                        reversePos = SubGridHelper.SubCellToWorld(_cellPathQueue.First.Value, _grid);
                        return true;

                    }
                }

                //再走小格
                head = GetHeadCell();
                next = GetBodyCellAtIndex(1); // 头部相邻的身体
                dir = head - next; // 远离身体方向
                left = new Vector2Int(-dir.y, dir.x);
                right = new Vector2Int(dir.y, -dir.x);
                candidates = new[] { dir, left, right };
                for (int i = 0; i < candidates.Length; i++)
                {
                    var nextHeadSub = head + candidates[i];
                    var nextHeadBig = SubGridHelper.SubCellToBigCell(nextHeadSub);
                    if (!_grid.IsInside(nextHeadBig)) continue;
                    if (IsPathBlocked(nextHeadBig)) continue;
                    if (!_grid.IsInsideSub(nextHeadSub)) continue;
                    if (!CheckOccupiedBySelfReverse(nextHeadBig)) continue;

                    EnqueueSubCellPath(GetHeadCell(), nextHeadSub, _cellPathQueue, 1);

                    if (_cellPathQueue.Count > 0)
                    {
                        reversePos = SubGridHelper.SubCellToWorld(_cellPathQueue.First.Value, _grid);
                        return true;

                    }
                }

                return false;
            }

            return false;
        }
        */
        bool CheckNextCell(Vector2Int nextCell)
        {
            if (_cachedRectTransforms.Count == 0 || _bodyCells.Count == 0)
                return false;

            Vector2Int curCheckCell = GetHeadCell();
            if (!DragFromHead)
            {
                curCheckCell = GetTailCell();
            }

            // 必须相邻
            if (Manhattan(curCheckCell, nextCell) != 1) return false;
            // 检查网格边界
            if (!_grid.IsInside(nextCell)) return false;
            // 使用与IsPathBlocked相同的阻挡检测逻辑，支持颜色匹配
            if (IsPathBlocked(nextCell)) return false;
            if (!CheckOccupiedBySelfForword(nextCell)) return false;


            return true;
        }

        bool EnqueueBigCellPath(Vector2Int from, Vector2Int to, LinkedList<Vector2Int> pathList, int maxPathCount = -1)
        {
            pathList.Clear();
            if (from == to) return false;
            if (!_grid.IsValid()) return false;
            if (!_grid.IsInside(from)) return false;

            // 允许 to 越界，夹紧到边缘
            var target = ClampInside(to);

            // A* 数据结构
            var open = new List<Vector2Int>(64);
            var openSet = new HashSet<Vector2Int>();
            var closed = new HashSet<Vector2Int>();
            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            var gScore = new Dictionary<Vector2Int, int>();
            var fScore = new Dictionary<Vector2Int, int>();

            int Heuristic(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

            open.Add(from);
            openSet.Add(from);
            gScore[from] = 0;
            fScore[from] = Heuristic(from, target);

            // 回退用：记录“离目标最近”的已可达节点
            Vector2Int bestNode = from;
            int bestH = Heuristic(from, target);
            int bestG = 0;

            // 4邻域
            Vector2Int[] dirs = new Vector2Int[4]
            {
                new Vector2Int(1, 0),
                new Vector2Int(-1, 0),
                new Vector2Int(0, 1),
                new Vector2Int(0, -1)
            };

            while (open.Count > 0)
            {
                // 取 f 最小
                int bestIdx = 0;
                int bestF = int.MaxValue;
                for (int i = 0; i < open.Count; i++)
                {
                    var n = open[i];
                    int f = fScore.TryGetValue(n, out var fv) ? fv : int.MaxValue;
                    if (f < bestF)
                    {
                        bestF = f;
                        bestIdx = i;
                    }
                }

                var current = open[bestIdx];
                open.RemoveAt(bestIdx);
                openSet.Remove(current);

                if (current == target)
                {
                    // 命中目标：回溯路径
                    var rev = new List<Vector2Int>(64);
                    var c = current;
                    while (!c.Equals(from))
                    {
                        rev.Add(c);
                        c = cameFrom[c];
                    }
                    rev.Reverse();

                    if (maxPathCount > 0)
                    {
                        for (int i = 0; i < rev.Count && i < maxPathCount; i++)
                            pathList.AddLast(rev[i]);
                    }
                    else
                    {
                        for (int i = 0; i < rev.Count; i++)
                            pathList.AddLast(rev[i]);
                    }
                    return pathList.Count > 0;
                }

                closed.Add(current);

                // 更新“最佳可达节点”
                int curG = gScore.TryGetValue(current, out var cg) ? cg : int.MaxValue;
                int curH = Heuristic(current, target);
                if (curG != int.MaxValue && (curH < bestH || (curH == bestH && curG < bestG)))
                {
                    bestH = curH;
                    bestG = curG;
                    bestNode = current;
                }

                for (int i = 0; i < 4; i++)
                {
                    var nb = new Vector2Int(current.x + dirs[i].x, current.y + dirs[i].y);

                    // 边界与阻挡校验
                    if (!_grid.IsInside(nb)) continue;
                    if (IsPathBlocked(nb)) continue; // 若需要允许站上被阻挡的目标格：if (nb != target && IsPathBlocked(nb)) continue;

                    if (closed.Contains(nb)) continue;

                    int tentativeG = curG == int.MaxValue ? int.MaxValue : (curG + 1);
                    if (tentativeG == int.MaxValue) continue;

                    bool isBetter = false;
                    if (!openSet.Contains(nb))
                    {
                        open.Add(nb);
                        openSet.Add(nb);
                        isBetter = true;
                    }
                    else
                    {
                        int oldG = gScore.TryGetValue(nb, out var og) ? og : int.MaxValue;
                        if (tentativeG < oldG) isBetter = true;
                    }

                    if (isBetter)
                    {
                        cameFrom[nb] = current;
                        gScore[nb] = tentativeG;
                        fScore[nb] = tentativeG + Heuristic(nb, target);
                    }
                }
            }

            // 未能到达目标：回退到“离目标最近的可达点”并回溯路径
            if (bestNode != from && cameFrom.ContainsKey(bestNode))
            {
                var rev = new List<Vector2Int>(64);
                var c = bestNode;
                while (!c.Equals(from))
                {
                    rev.Add(c);
                    // 安全回溯（理论上都会存在）
                    if (!cameFrom.TryGetValue(c, out c))
                        break;
                }
                rev.Reverse();

                if (maxPathCount > 0)
                {
                    for (int i = 0; i < rev.Count && i < maxPathCount; i++)
                        pathList.AddLast(rev[i]);
                }
                else
                {
                    for (int i = 0; i < rev.Count; i++)
                        pathList.AddLast(rev[i]);
                }
                return pathList.Count > 0;
            }

            // 完全不可走（from四周全阻挡等）
            return false;
        }


        bool AdvanceHeadToCellCoConsume(Vector2Int nextCell)
        {
            if (_bodyCells == null || _bodyCells.Count == 0) return true;

            var newHeadCell = nextCell;

            if (_bodyCells.Count == 1)
            {
                _bodyCells.First.Value = newHeadCell;
            }
            else
            {
                // 整条蛇朝尾部方向移动：在尾部添加新位置，移除头部
                _bodyCells.AddFirst(newHeadCell);
                _bodyCells.RemoveLast();

            }


            // 5) 刷新缓存
            _currentHeadCell = _bodyCells.First.Value;
            _currentTailCell = _bodyCells.Last.Value;
            UpdateCachedRectTransformsFromBodyCells();

            return true;
        }


        private Vector2Int GetBodyCellAtIndex(int index)
        {
            if (index >= _bodyCells.Count || index < 0)
                return Vector2Int.zero;

            var node = _bodyCells.First;
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
        /// 根据_bodyCells更新_cachedRectTransforms
        /// </summary>
        private void UpdateCachedRectTransformsFromBodyCells()
        {
            if (_cachedRectTransforms.Count == 0 || _bodyCells.Count == 0)
                return;

            // 遍历身体节点和对应的RectTransform
            var bodycelllist = _bodyCells.ToList();
            for (int segmentIndex = 0; segmentIndex < _bodyCells.Count; segmentIndex++)
            {
                var rt = _cachedRectTransforms[segmentIndex];
                if (rt != null)
                {
                    var worldPos = _grid.CellToWorld(bodycelllist[segmentIndex]);
                    rt.anchoredPosition = new Vector2(worldPos.x, worldPos.y);
                }

            }

        }


        /// <summary>
        /// 根据_bodyCells更新_cachedRectTransforms
        /// </summary>
        private void UpdateBodyCellsFromCachedRectTransforms()
        {
            if (_cachedRectTransforms.Count == 0 || _bodyCells.Count == 0)
                return;

            // 遍历身体节点和对应的RectTransform
            _bodyCells.Clear();
            for (int segmentIndex = 0; segmentIndex < _cachedRectTransforms.Count; segmentIndex++)
            {
                var rt = _cachedRectTransforms[segmentIndex];
                if (rt != null)
                {
                    _bodyCells.AddLast(_grid.WorldToCell(rt.anchoredPosition));
                }

            }

        }


        bool AdvanceTailToCellCoConsume(Vector2Int nextCell)
        {
            if (_bodyCells == null || _bodyCells.Count == 0) return true;

            var newHeadCell = nextCell;

            if (_bodyCells.Count == 1)
            {
                _bodyCells.First.Value = newHeadCell;
            }
            else
            {
                // 整条蛇朝尾部方向移动：在尾部添加新位置，移除头部
                _bodyCells.AddLast(newHeadCell);
                _bodyCells.RemoveFirst();

            }


            // 5) 刷新缓存
            _currentHeadCell = _bodyCells.First.Value;
            _currentTailCell = _bodyCells.Last.Value;
            UpdateCachedRectTransformsFromBodyCells();

            return true;
        }

        public GridConfig GetGrid()
        {
            return _grid;
        }

        public override void SnapCellsToGrid()
        {
            if (_cachedRectTransforms == null)
                return;
            if (_cachedRectTransforms.Count == 0)
                return;


            Vector2Int[] newInitialBodyCells = new Vector2Int[Length];
            if (DragFromHead)
            {
                for (int i = 0; i < _bodyCells.Count; i++)
                {
                    if (i >= _segments.Count) break;
                    var rt = _segments[i].GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        var bigcell = _grid.WorldToCell(rt.anchoredPosition);
                        newInitialBodyCells[i] = bigcell;
                    }
                }
            }
            else
            {
                for (int i = _bodyCells.Count - 1; i >= 0; i--)
                {
                    if (i < 0) break;
                    var rt = _segments[i].GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        var bigcell = _grid.WorldToCell(rt.anchoredPosition);
                        newInitialBodyCells[i] = bigcell;
                    }

                }
            }


            InitializeSegmentPositions(newInitialBodyCells);
            UpdateCachedRectTransformsFromBodyCells();

            if (EnableBodySpriteManagement)
            {
                _bodySpriteManager?.OnSnakeMoved();
            }
        }
        public IEnumerator CoConsume(HoleEntity hole, bool fromHead)
        {
            _consuming = true;
            IsDragging = false; // 脱离手指控制
            _isReverse = false;
            Vector3 holeCenter = _grid.CellToWorld(hole.Cell);
            var holeCenterCell = hole.Cell;

            LinkedList<GameObject> allegments = new LinkedList<GameObject>();
            Transform segmentToConsume = null;

            foreach (var gameObject in _segments)
            {
                if (gameObject != null)
                    allegments.AddLast(gameObject);
            }

            // 1) 确定“活动端”：使用参数 fromHead
            bool activeFromHead = fromHead;
            var leadCurrentCell = activeFromHead ? GetHeadCell() : GetTailCell();

            // 2) 初始化活动端的平滑状态（首次或切换端时）
            EnsureActiveLeadInited(activeFromHead);

            // 3) 目标点（连续世界坐标）
            Vector2 targetPos = holeCenter;

            // 4) 活动端沿目标插值（逐帧推进，不阻塞）
            while (true)
            {
                Vector2 dir = targetPos - _activeLeadPos;
                float dist = dir.magnitude;
                if (dist <= EPS) break;

                float step = _leadSpeedWorld * Time.deltaTime;
                if (step + 1e-3f >= dist)
                {
                    _activeLeadPos = targetPos;
                }
                else
                {
                    _activeLeadPos += dir * (step / Mathf.Max(dist, 1e-6f));
                }

                // 路径历史：位移达阈值才采样
                if (_activeLeadPath.Count == 0)
                {
                    _activeLeadPath.Add(_activeLeadPos);
                }
                else
                {
                    var last = _activeLeadPath[_activeLeadPath.Count - 1];
                    if (Vector2.Distance(last, _activeLeadPos) >= 0.10f * _segmentspacing)
                        _activeLeadPath.Add(_activeLeadPos);
                }

                // 裁剪历史
                PruneLeadPathHistory(_activeLeadPath);

                // 生成整条蛇的连续位置并写入可视
                ApplySmoothVisualsByPath(activeFromHead);

                // 同步渲染折线（身体Sprite）
                if (EnableBodySpriteManagement && _bodySpriteManager != null)
                {
                    _bodySpriteManager.UpdateLineFromPolyline(
                        _centerlinePolyline,
                        _cachedRectTransforms.Count,
                        _segmentspacing,
                        activeFromHead
                    );
                }

                yield return null; // 逐帧推进
            }

            // 5) 到达洞中心后，触发吞噬动画（逐帧推进，不阻塞）
            if (EnableBodySpriteManagement && _bodySpriteManager != null)
            {
                float allConsumeTime = hole.ConsumeInterval * Mathf.Max(1, _bodyCells.Count);
                _bodySpriteManager.StartSnakeCoconsume(allConsumeTime, activeFromHead);

                float elapsed = 0f;
                while (elapsed < allConsumeTime)
                {
                    _bodySpriteManager.OnSnakeCoconsumeUpdate();
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }

            _consuming = false;
            _consumeCoroutine = null;

            // 全部消失后，销毁蛇对象或重生；此处直接销毁（保留原有行为）
            _bodyCells.Clear();
            Destroy(gameObject);
            SnakeManager.Instance.TryClearSnakes();
            yield break;
        }

        /// <summary>
        /// 查找目标位置本身或邻近位置的洞
        /// </summary>
        HoleEntity FindHoleAtOrAdjacentWithColor(Vector2Int targetCell, SnakeColorType color)
        {
            _cacheCoconsumeHoles.Clear();
            HoleEntity hole;
            // 检查是否被实体阻挡
            if (GridEntityManager.Instance != null)
            {
                var entities = GridEntityManager.Instance.HoleEntities;
                if (entities != null)
                {
                    foreach (var entity in entities)
                    {
                        hole = (HoleEntity)entity;
                        // 检查目标位置本身是否是洞的位置
                        if (hole.Cell == targetCell && hole.ColorType == color)
                        {
                            _cacheCoconsumeHoles.Add(hole);
                            return hole;
                        }
                        // 检查目标位置是否邻近洞
                        if (hole.IsAdjacent(targetCell) && hole.ColorType == color)
                        {
                            return hole;
                        }
                    }
                }
            }

            return null;
        }

        bool IsPathBlocked(Vector2Int bigCell)
        {
            // 检查是否被实体阻挡GridEntityManager.Instance
            var entities = GridEntityManager.Instance.GetAt(bigCell);
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
                        if (holeEntity.IsBlockingCell(bigCell, this))
                        {
                            return true; // 颜色不匹配，洞算作阻挡物
                        }
                    }
                }
            }

            // 检查是否被其他蛇阻挡
            if (SnakeManager.Instance.IsCellOccupiedByOtherSnakes(bigCell, this))
            {
                return true;
            }

            return false;
        }

        Vector3 ScreenToWorld(Vector3 screen)
        {
            // UI渲染模式：使用UI坐标转换
            if (SnakeManager.Instance.SnakeCanvas != null)
            {
                var rect = transform.parent as RectTransform; // GridContainer
                if (rect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, SnakeManager.Instance.SnakeCanvas.worldCamera, out Vector2 localPoint))
                {
                    return new Vector3(localPoint.x, localPoint.y, 0f);
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
            if (_grid.Width == 0 || _grid.Height == 0) return;

            // 取容器 RectTransform（与 ScreenToWorld 中一致的父容器）
            var container = transform.parent as RectTransform;
            if (container == null) return;

            if (DebugShowLeadTarget)
            {
                // 将“网格世界坐标系”的 _leadTargetPos 映射到屏幕坐标
                // 先把局部(anchored)坐标转换为世界坐标，再转屏幕坐标
                Vector3 worldPoint = container.TransformPoint(new Vector3(_leadTargetPos.x, _leadTargetPos.y, 0f));
                var cam = GetComponentInParent<Canvas>()?.worldCamera;
                Vector2 sp = RectTransformUtility.WorldToScreenPoint(cam, worldPoint);

                // OnGUI 的 y 轴从顶向下，需要翻转
                float sx = sp.x;
                float sy = Screen.height - sp.y;

                // 画一个十字与坐标文本
                var prev = GUI.color;
                GUI.color = DebugLeadTargetColor;

                float s = DebugLeadMarkerSize;
                GUI.DrawTexture(new Rect(sx - 1f, sy - s, 2f, 2f * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(sx - s, sy - 1f, 2f * s, 2f), Texture2D.whiteTexture);

                GUI.Label(new Rect(sx + s + 4f, sy - 12f, 260f, 24f),
                    $"leadTarget ({_leadTargetPos.x:F2},{_leadTargetPos.y:F2})");

                GUI.color = prev;
            }

            // 追加：绘制 _centerlinePolyline
            if (DebugShowCenterline && _centerlinePolyline != null && _centerlinePolyline.Count > 0)
            {
                var prev = GUI.color;
                // 把折线的“网格局部坐标”转成屏幕坐标后绘制
                int n = _centerlinePolyline.Count;
                var cam = GetComponentInParent<Canvas>()?.worldCamera;
                // 先画中间点（统一颜色）
                GUI.color = DebugPolylineColor;
                for (int i = 0; i < n; i++)
                {
                    var pLocal = _centerlinePolyline[i];
                    Vector3 wp = container.TransformPoint(new Vector3(pLocal.x, pLocal.y, 0f));
                    Vector2 scr = RectTransformUtility.WorldToScreenPoint(cam, wp);
                    float px = scr.x;
                    float py = Screen.height - scr.y;
                    GUI.DrawTexture(new Rect(px - DebugPolylinePointSize, py - DebugPolylinePointSize,
                        2f * DebugPolylinePointSize, 2f * DebugPolylinePointSize), Texture2D.whiteTexture);
                }

                // 头点（折线开头）
                {
                    var headLocal = _centerlinePolyline[0];
                    Vector3 wp = container.TransformPoint(new Vector3(headLocal.x, headLocal.y, 0f));
                    Vector2 scr = RectTransformUtility.WorldToScreenPoint(cam, wp);
                    float px = scr.x;
                    float py = Screen.height - scr.y;
                    GUI.color = DebugPolylineHeadColor;
                    GUI.DrawTexture(new Rect(px - DebugPolylinePointSize * 1.5f, py - DebugPolylinePointSize * 1.5f,
                        3f * DebugPolylinePointSize, 3f * DebugPolylinePointSize), Texture2D.whiteTexture);
                }

                // 尾点（折线末尾）
                {
                    var tailLocal = _centerlinePolyline[n - 1];
                    Vector3 wp = container.TransformPoint(new Vector3(tailLocal.x, tailLocal.y, 0f));
                    Vector2 scr = RectTransformUtility.WorldToScreenPoint(cam, wp);
                    float px = scr.x;
                    float py = Screen.height - scr.y;
                    GUI.color = DebugPolylineTailColor;
                    GUI.DrawTexture(new Rect(px - DebugPolylinePointSize * 1.5f, py - DebugPolylinePointSize * 1.5f,
                        3f * DebugPolylinePointSize, 3f * DebugPolylinePointSize), Texture2D.whiteTexture);
                }
                GUI.color = prev;
            }



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
            return _currentHeadCell;
            //if (_cachedRectTransforms != null && _cachedRectTransforms.Count > 0)
            //{
            //    return SubGridHelper.WorldToBigCell(_cachedRectTransforms[0].anchoredPosition, _grid);
            //}
            //return Vector2Int.zero;
        }

        /// <summary>
        /// 获取蛇尾的格子位置
        /// </summary>
        public Vector2Int GetTailCell()
        {

            return _currentTailCell;
            //if (_cachedRectTransforms != null && _cachedRectTransforms.Count > 0)
            //{
            //    return SubGridHelper.WorldToBigCell(_cachedRectTransforms[_cachedRectTransforms.Count - 1].anchoredPosition, _grid);
            //}
            //return Vector2Int.zero;
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



        protected override void OnDestroy()
        {
            base.OnDestroy();
        }
    }
}


