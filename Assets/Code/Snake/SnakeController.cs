using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ReGecko.GridSystem;
using ReGecko.Grid.Entities;
using System.Collections;
using ReGecko.GameCore.Flow;
using System.Linq;
using ReGecko.Game;
using DG.Tweening;

namespace ReGecko.SnakeSystem
{
    public class SnakeController : BaseSnake
    {
        #region Serialized Settings And Runtime State

        [Header("SnakeController特有属性")]

        [SerializeField] float AStarDirectionBias = 1.0f; // 非前进方向的基础罚分
        [SerializeField] float AStarBackwardPenalty = 3f; // 朝身体反向（与preferredDir相反）额外罚分
        [SerializeField] float AStarTurnLeftPenalty = 1f; // 左转额外罚分
        [SerializeField] float AStarTurnRightPenalty = 1f; // 右转额外罚分

        private Queue<GameObject> _segmentPool = new Queue<GameObject>();
        private List<GameObject> _segments = new List<GameObject>();
        protected readonly LinkedList<Vector2Int> _bodyCells = new LinkedList<Vector2Int>(); // 离散身体占用格，头在First
        protected readonly LinkedList<Vector2Int> _bigBodyCells = new LinkedList<Vector2Int>(); // 离散身体占用格，头在First
        private readonly List<RectTransform> _cachedRectTransforms = new List<RectTransform>();

        public override LinkedList<Vector2Int> GetBodyCells()
        {
            return _bodyCells;
        }

        public override List<GameObject> GetSegments()
        {
            return _segments;
        }

        // 大格寻路相关
        private LinkedList<Vector2Int> _cellPathQueue = new LinkedList<Vector2Int>(); // 格子路径队列
        private LinkedList<Vector2> _cellPathWithMouse = new LinkedList<Vector2>(); // 修正的路径队列，_activeLeadPos 链接_cellPathQueue尾端链接mouse点

        private Vector2Int _currentHeadCell;
        private Vector2Int _currentTailCell;

        // —— 平滑路径模式 开关与缓存 ——
        Vector2[] _lineTargetPositionsCache;

        // 平滑公共
        float _segmentspacing;           // 每段身体之间固定间距（世界单位）
        const float EPS = 1e-4f;

        float _cachedSpeedInput;
        float _cachedCellSize;

        Vector2 _lastActiveLeadPos;

        LinkedList<MoveState> _pendingTargetCellStates = new LinkedList<MoveState>();
        MoveState _moveState;
        MoveState _curMoveState;
        Coroutine _coProduce, _coConsume, _coMove, _coRender;
        Coroutine _consumeCoroutine;
        bool _coroutinesStarted = false;

        bool _hasCurrentMoveTarget = false;
        bool _isCurrentMoveTargetError = false;


        public Ease easeType = Ease.Linear; // 缓动类型

        Vector2 _lastTweenTarget;

        Tweener[] _cellFollowTweeners;

        #endregion

        #region Move State

        struct MoveState
        {
            public MoveState(bool d, Vector2Int st, Vector2Int bt, float s,Vector2 p,bool isBig)
            {
                DragFromHead = d;
                TargetCell = st;
                TargetBigCell = bt;
                DragSpeed = s;
                TargetPos = p;
                IsBigPath = isBig;
            }
            public bool IsValid()
            {
                return TargetCell != SnakeControllerUtil.InvalidCell;
            }
            public void Clear()
            {
                TargetCell = SnakeControllerUtil.InvalidCell;
                TargetBigCell = SnakeControllerUtil.InvalidCell;
            }

            public bool DragFromHead;
            public Vector2Int TargetCell;
            public Vector2Int TargetBigCell;
            public Vector2 TargetPos;
            public float DragSpeed;
            public bool IsBigPath;
        }

        #endregion

        #region Initialization And Segment Setup


        public override void Initialize(GridConfig grid)
        {
            _grid = grid;
            IsDragging = false;
            DragFromHead = false;
            _curMoveState.Clear();
            _moveState.Clear();

            RecreateSegments();
            CacheSegments();
            InitializeSegmentPositions(InitialBodyCells);
            InitializeBodySpriteManager();
        }

        /// <summary>
        /// 创建或获取身体段GameObject
        /// </summary>
        GameObject GetSegmentFromPool()
        {
            if (_segmentPool.Count > 0)
            {
                var obj = _segmentPool.Dequeue();
                obj.SetActive(true);
                return obj;
            }

            // 如果没有预制体，创建一个基本的Image对象
            var go = new GameObject("Segment");
            go.transform.SetParent(transform);

            var image = go.AddComponent<Image>();
            image.sprite = null;// BodySprite;
            image.color = BodyColor;
            if (EnableBodySpriteManagement)
            {
                image.enabled = false;
            }
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(_grid.CellSize * 0.8f, _grid.CellSize * 0.8f);
            return go;
        }

        /// <summary>
        /// 回收身体段到对象池
        /// </summary>
        void ReturnSegmentToPool(GameObject obj)
        {
            obj.SetActive(false);
            _segmentPool.Enqueue(obj);
        }

        /// <summary>
        /// 重新创建所有身体段
        /// </summary>
        void RecreateSegments()
        {
            // 清理现有身体段
            foreach (var segment in _segments)
            {
                ReturnSegmentToPool(segment);
            }
            _segments.Clear();


            for (int segmentIndex = 0; segmentIndex < Mathf.Max(1, Length); segmentIndex++)
            {
                var segment = GetSegmentFromPool();
                segment.name = $"Segment_{segmentIndex}";
                _segments.Add(segment);
            }
        }

        void CacheSegments()
        {
            _cachedRectTransforms.Clear();
            foreach (var segment in _segments)
            {
                _cachedRectTransforms.Add(segment.GetComponent<RectTransform>());
            }
            _cellFollowTweeners = new Tweener[_segments.Count];
        }

        void GenerateBigCellBodys()
        {
            _bigBodyCells.Clear();
            for (var node = _bodyCells.First; node != null; node = node.Next)
            {
                _bigBodyCells.AddLast(node.Value);
            }
        }

        bool CheckMoveBigCellBodysBackward(bool activeFromHead, Vector2 moveToW)
        {
            var moveToBigCell = _grid.WorldToCell(moveToW);
            if (moveToBigCell == (activeFromHead ? _bigBodyCells.First.Next.Value : _bigBodyCells.Last.Previous.Value))
            {
                bool backres = AppendVirtualBigBodyCellAtLast(activeFromHead);
                if (activeFromHead)
                {
                    if(backres)
                    {
                        _bigBodyCells.RemoveFirst();
                    }
                }
                else
                {
                    if (backres)
                    {
                        _bigBodyCells.RemoveLast();
                    }
                }
                return backres;
            }

            return true;
        }

        bool AppendVirtualBigBodyCellAtLast(bool activeFromHead)
        {
            bool isAddNew = false;
            Vector2Int newCell = SnakeControllerUtil.InvalidCell;
            if (activeFromHead)
            {
                var tail = _bigBodyCells.Last.Value;
                var prev = _bigBodyCells.Last.Previous.Value;
                Vector2Int dir = tail - prev;
                var candidates = SnakeControllerUtil.ForwardLeftRight(dir);
                for (int i = 0; i < candidates.Length; i++)
                {
                    var nextBig = tail + candidates[i];
                    if (!_grid.IsInside(nextBig)) continue;
                    if (IsPathBlocked(nextBig)) continue;

                    newCell = nextBig;
                    _bigBodyCells.AddLast(newCell);
                    isAddNew = true;
                    break;
                }

            }
            else
            {
                var head = _bigBodyCells.First.Value;
                var next = _bigBodyCells.First.Next.Value; // 头部相邻的身体
                Vector2Int dir = head - next; // 远离身体方向
                var candidates = SnakeControllerUtil.ForwardLeftRight(dir);

                //优先走大格
                for (int i = 0; i < candidates.Length; i++)
                {
                    var nextHeadBig = head + candidates[i];
                    if (!_grid.IsInside(nextHeadBig)) continue;
                    if (IsPathBlocked(nextHeadBig)) continue;

                    newCell = nextHeadBig;
                    _bigBodyCells.AddFirst(newCell);
                    isAddNew = true;
                    break;
                }

            }

            return isAddNew;
        }
        /// <summary>
        /// 初始化所有身体段的位置
        /// </summary>
        void InitializeSegmentPositions(Vector2Int[] initialbodycells)
        {
            var bodyCells = initialbodycells;
            if (bodyCells == null || bodyCells.Length < 2) return;

            _bodyCells.Clear();

            for (int i = 0; i < bodyCells.Length; i++)
            {
                var cell = ClampInside(bodyCells[i]);
                _bodyCells.AddLast(cell);
                UpdateSegmentPosition(i, cell);
            }

            // 连续性校验：所有格子必须两两相邻（曼哈顿距离=1）
            var node = _bodyCells.First;
            var idx = 0;
            while (node != null && node.Next != null)
            {
                var a = node.Value;
                var b = node.Next.Value;
                if (Manhattan(a, b) != 1)
                {
                    Debug.LogError($"InitializeSegmentPositions: 格子不连续 at pair index {idx}->{idx + 1}, a={a}, b={b}");
                    break;
                }
                node = node.Next;
                idx++;
            }

            _currentHeadCell = _bodyCells.First.Value;
            _currentTailCell = _bodyCells.Last.Value;

            GenerateBigCellBodys();
            // 初始放置完成后，更新身体图片
            if (EnableBodySpriteManagement && _bodySpriteManager != null)
            {
                _bodySpriteManager.OnSnakeLengthChanged();
            }
        }


        /// <summary>
        /// 更新身体段位置（由SnakeController调用）
        /// </summary>
        /// <param name="segmentIndex">身体节点索引</param>
        void UpdateSegmentPosition(int segmentIndex, Vector2Int cell)
        {
            if (segmentIndex < 0 || segmentIndex >= _segments.Count) return;
            var worldPos = _grid.CellToWorld(cell);
            var rt = _segments[segmentIndex].GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition3D = new Vector3(worldPos.x, worldPos.y, 0);
                rt.rotation = Quaternion.Euler(0, 0, 0f);
            }
        }

        #endregion

        #region Public Update Entry Points


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

            UpdateSmoothPathPointsMovement();
        }

        #endregion

        #region Movement Coroutine Pipeline


        void UpdateSmoothPathPointsMovement()
        {
            // 基础校验
            if (_bodyCells == null || _bodyCells.Count == 0 || _cachedRectTransforms.Count == 0) return;

            // 只负责确保协程已启动；寻路与位移转入协程处理
            if (!_coroutinesStarted)
            {
                _coProduce = StartCoroutine(_ProduceTargetsLoop());
                _coConsume = StartCoroutine(_ConsumeTargetsLoop());
                _coMove = StartCoroutine(_ConsumeMoveLoop());
                _coRender = StartCoroutine(_ConsumeRenderLoop());
                _coroutinesStarted = true;
            }

            // 协程负责路径与位移，避免每帧重复寻路。
            // 否则，这里不再做寻路与位移，避免停顿
        }

        bool ShouldRunMovementCoroutines()
        {
            return IsDragging && enabled && !_consuming;
        }

        IEnumerator _ProduceTargetsLoop()
        {
            while (enabled)
            {
                if (!ShouldRunMovementCoroutines())
                {
                    yield return null;
                    continue;
                }

                // 速度/间距（世界单位）
                RefreshKinematicsIfNeeded();


                // 采样鼠标 → 目标格子
                var canvas = SnakeManager.Instance.SnakeCanvas;
                var camera = canvas != null ? canvas.worldCamera : null;
                var world = SnakeControllerUtil.ScreenToGridWorld(Input.mousePosition, transform.parent as RectTransform, camera, _grid);
                world = _grid.ClampWorld(world);

                Vector2Int targetCell = WorldToCellClamped(world);

                //优先大格寻路
                Vector2Int fromCell;
                bool fromHead = DragFromHead;

                if (_pendingTargetCellStates.Count > 0)
                {
                    fromCell = _pendingTargetCellStates.Last.Value.TargetCell;
                    fromHead = _pendingTargetCellStates.Last.Value.DragFromHead;
                }
                else
                {
                    fromHead = DragFromHead;
                    fromCell = DragFromHead ? GetHeadCell() : GetTailCell();
                }

                // 寻路（大格）
                _cellPathQueue ??= new LinkedList<Vector2Int>();
                _cellPathQueue.Clear();
                EnqueueBigCellPath(fromHead, fromCell, targetCell, _cellPathQueue);
                var speed = Time.deltaTime * 1.5f;

                for (var n = _cellPathQueue.First; n != null; n = n.Next)
                {
                    var cell = n.Value;
                    _pendingTargetCellStates.AddLast(new MoveState(fromHead, cell, cell, speed, _grid.CellToWorld(cell), true));
                }

                _moveState = new MoveState(fromHead, targetCell, targetCell, speed, _grid.CellToWorld(targetCell), false);
                yield return null; // 每帧产出一次
            }
        }

        IEnumerator _ConsumeTargetsLoop()
        {
            while (enabled)
            {
                // 取目标
                if (!_hasCurrentMoveTarget)
                {
                    //选寻路大格
                    if (_pendingTargetCellStates.Count > 0)
                    {

                        _curMoveState = _pendingTargetCellStates.First.Value;
                  
                        var bigcell = _curMoveState.TargetBigCell;
                        if(bigcell == (_curMoveState.DragFromHead ? GetHeadCell() : GetTailCell()))
                        {
                            if (_pendingTargetCellStates.Count > 0)
                            {
                                _pendingTargetCellStates.RemoveFirst();
                            }
                            continue;
                        }
                        // 检查目标点合法性
                        if (!CheckNextBigCell(_curMoveState.DragFromHead, _curMoveState.TargetBigCell))
                        {
                            if (_pendingTargetCellStates.Count > 0)
                            {
                                _pendingTargetCellStates.RemoveFirst();
                            }
                            continue;
                        }

                        _curMoveState.IsBigPath = true;
                        _hasCurrentMoveTarget = true;

                    }
                    else
                    {
                        _curMoveState.Clear();

                        if (!_moveState.IsValid())
                        {
                            yield return null;
                            continue;
                        }


                        Vector2Int targetBigCell = WorldToCellClamped(_moveState.TargetPos);
                        if (!CheckNextBigCell(_moveState.DragFromHead, targetBigCell))
                        {
                            _moveState.Clear();
                            yield return null;
                            continue;
                        }

                        bool activeFromHead = _moveState.DragFromHead;
                        RectTransform leadTransform = activeFromHead ? _cachedRectTransforms[0] : _cachedRectTransforms[_cachedRectTransforms.Count - 1];
                        Vector2 target = _moveState.IsBigPath ? _grid.CellToWorld(_moveState.TargetBigCell) : _moveState.TargetPos;
                        if (Vector2.Distance(leadTransform.anchoredPosition, target) <= EPS)
                        {
                            _moveState.Clear();
                            yield return null;
                            continue;
                        }


                        _curMoveState = _moveState;
                        _curMoveState.IsBigPath = false;
                        _hasCurrentMoveTarget = true;
                        _moveState.Clear();
                    }
                }

                yield return null;
            }
        }


        IEnumerator _ConsumeMoveLoop()
        {
            while (enabled)
            {
                // 没目标就等下帧
                if (!_hasCurrentMoveTarget && !_curMoveState.IsValid())
                {
                    if (!IsDragging)
                    {
                        if (NeedSnapCellsToGrid)
                        {
                            NeedSnapCellsToGrid = false;
                            UpdateBodyCellsFromCachedRectTransforms();
                            SnapCellsToGrid();
                            _hasCurrentMoveTarget = false;
                            foreach (var tw in _cellFollowTweeners)
                            {
                                tw?.Kill();
                            }
                            if (_curMoveState.IsBigPath)
                            {
                                if (_pendingTargetCellStates.Count > 0 )
                                {
                                    _pendingTargetCellStates.RemoveFirst();
                                }
                            }
                            _curMoveState.Clear();
                        }
                    }
                    yield return null;
                    continue;
                }

                if(!_curMoveState.IsValid())
                {
                    _hasCurrentMoveTarget = false;
                    yield return null;
                    continue;
                }

                bool activeFromHead = _curMoveState.DragFromHead;
                RectTransform leadTransform = activeFromHead ? _cachedRectTransforms[0] : _cachedRectTransforms[_cachedRectTransforms.Count - 1];
                bool changetarget = false;
                Vector2 target;
                //修复跨一格时闪烁的问题
                if (_curMoveState.IsBigPath )
                {
                    if(_pendingTargetCellStates.Count == 1)
                    {
                        target = _curMoveState.TargetPos;
                    }else
                    {
                        target = _grid.CellToWorld(_curMoveState.TargetCell);
                    }

                }
                else
                {

                    target = _curMoveState.TargetPos;
                }
                    
                if(Vector2.Distance(_lastTweenTarget,target) > EPS)
                {
                    _lastTweenTarget = target;
                    changetarget = true;
                    _isCurrentMoveTargetError = false;
                }

                if(!changetarget)
                {
                    bool isfinish = true;
                    foreach (var tw in _cellFollowTweeners)
                    {
                        if (tw != null && tw.IsActive() && (!tw.IsComplete() || tw.IsPlaying()))
                        {
                            isfinish = false;
                            break;
                        }
                    }

                    if (Vector2.Distance(leadTransform.anchoredPosition,target) <= EPS || _isCurrentMoveTargetError || isfinish)
                    {
                        UpdateBodyCellsFromCachedRectTransforms();
                        _hasCurrentMoveTarget = false;

                        if (_curMoveState.IsBigPath)
                        {
                            if (_pendingTargetCellStates.Count > 0)
                            {
                                _pendingTargetCellStates.RemoveFirst();
                            }
                        }
                        _curMoveState.Clear();
                    }

                    yield return null;
                    continue;
                }

                var curcheckbigcell = WorldToCellClamped(target);
                if(!CheckNextBigCell(_curMoveState.DragFromHead, curcheckbigcell))
                {
                    _isCurrentMoveTargetError = true;
                    _curMoveState.Clear();
                    _hasCurrentMoveTarget = false;

                    yield return null;
                    continue;
                }
                //检查倒车
                if (!CheckMoveBigCellBodysBackward(activeFromHead, target))
                {
                    _isCurrentMoveTargetError = true;
                    _curMoveState.Clear();
                    _hasCurrentMoveTarget = false;

                    yield return null;
                    continue;
                }

                bool isplaying = false;
                foreach(var tw in _cellFollowTweeners)
                {
                    if(tw != null && tw.IsActive() && (tw.IsPlaying() || !tw.IsComplete()))
                    {
                        isplaying = true;
                        break;
                    }
                }

                if(isplaying)
                {
                    yield return null;
                    continue;
                }

                GenerateBigCellPathWithMouse(activeFromHead, target);
                ApplySmoothVisualsTargetPath(activeFromHead);
                // 计算所需时间
                float duration = Mathf.Min(0.1f, _curMoveState.DragSpeed);

                for (int i = 0; i < _cachedRectTransforms.Count; i++)
                {
                    MoveNextFollowTweeners(activeFromHead, i, duration);
                }

                var activeLeadPos = leadTransform.anchoredPosition;
                if (Vector2.Distance(_lastActiveLeadPos, activeLeadPos) > 0.05f * _segmentspacing)
                {
                    _lastActiveLeadPos = activeLeadPos;
                    UpdateBodyCellsFromCachedRectTransforms();
                }

                yield return null;

            }
        }


        IEnumerator _ConsumeRenderLoop()
        {
            while (enabled)
            {
                if (EnableBodySpriteManagement && _bodySpriteManager != null)
                {
                    _bodySpriteManager.OnSnakeLengthChanged();
                }
                yield return null;
            }
            yield break;
        }

        #endregion

        #region Smooth Path Rendering


        bool GenerateBigCellPathWithMouse(bool activeFromHead,Vector2 moveToW)
        {
            _cellPathWithMouse.Clear();
            var moveToBigCell = _grid.WorldToCell(moveToW);

            var centerleadpos = WorldToWorldCenter(moveToW);
            // 工具
            Vector2 CenterOf(Vector2Int cell)
            {
                var c = _grid.CellToWorld(cell);
                return new Vector2(c.x, c.y);
            }
            bool NeedAppendCenterPoint(Vector2 a, Vector2 b, Vector2 center, float eps = 1e-3f)
            {
                // 垂直同线且经过 center（center 在 a.y 与 b.y 之间）
                if (Mathf.Abs(a.x - b.x) <= eps && Mathf.Abs(a.x - center.x) <= eps)
                {
                    return (Vector2.Distance(a, b) > _grid.CellSize);
 
                }
                // 水平同线且经过 center（center 在 a.x 与 b.x 之间）
                if (Mathf.Abs(a.y - b.y) <= eps && Mathf.Abs(a.y - center.y) <= eps)
                {
                    return (Vector2.Distance(a, b) > _grid.CellSize);
                }
                // 不是同轴或不经过 center：需要插中心
                return true;
            }
            bool AppendViaCenterIfNeeded(Vector2 fromW, Vector2 toW)
            {
                bool appendCenter = false;
                var fromCell = _grid.WorldToCell(new Vector3(fromW.x, fromW.y, 0f));
                var fromCenter = CenterOf(fromCell);
                if (NeedAppendCenterPoint(fromW, toW, fromCenter))
                {
                    _cellPathWithMouse.AddLast(fromCenter);
                    appendCenter = true;
                }
                _cellPathWithMouse.AddLast(toW);
                return appendCenter;
            }

            // 1) 起点：leadpos
            Vector2 curW = centerleadpos;
            _cellPathWithMouse.AddLast(curW);

            int startindex = 0;
            if (moveToBigCell == (activeFromHead ? _bigBodyCells.First.Value : _bigBodyCells.Last.Value)) 
            {
                startindex = 1;
            }

            // 2) 需要判断是否是后退，后退要删除_activeLeadBigCellPath的第一个点，尾部要添加一个预测点
            bool isBack = false;
            bool checkBack = false;

            // 2) 连接 _bigBodyCells 各格中心
            if (activeFromHead)
            {
                for (int i = startindex; i < _bigBodyCells.Count; i++)
                {
                    var bigCell = SnakeControllerUtil.GetCellAt(_bigBodyCells, i, Vector2Int.zero);

                    Vector2 nextW = CenterOf(bigCell);
                    var appendres = AppendViaCenterIfNeeded(curW, nextW);
                    if (!checkBack)
                    {
                        checkBack = true;
                        isBack = !appendres;
                    }
                    curW = _cellPathWithMouse.Last.Value;
                }
                
            }
            else
            {

                for (int i = _bigBodyCells.Count - 1 - startindex; i >= 0; i--)
                {

                    var bigCell = SnakeControllerUtil.GetCellAt(_bigBodyCells, i, Vector2Int.zero);

                    Vector2 nextW = CenterOf(bigCell);
                    var appendres = AppendViaCenterIfNeeded(curW, nextW);
                    if (!checkBack)
                    {
                        checkBack = true;
                        isBack = !appendres;
                    }
                    curW = _cellPathWithMouse.Last.Value;
                }
            }
            

            //添加预测点
            if (isBack)
            {
                bool isAddNew = false;
                    Vector2Int newCell = SnakeControllerUtil.InvalidCell;
                if (activeFromHead)
                {
                    var tail = _bigBodyCells.Last.Value;
                    var prev = _bigBodyCells.Last.Previous.Value;
                    Vector2Int dir = tail - prev;
                    var candidates = SnakeControllerUtil.ForwardLeftRight(dir);
                    for (int i = 0; i < candidates.Length; i++)
                    {
                        var nextBig = tail + candidates[i];
                        if (!_grid.IsInside(nextBig)) continue;
                        if (IsPathBlocked(nextBig)) continue;

                        newCell = nextBig;
                        isAddNew = true;
                        break;
                    }

                }
                else
                {
                    var head = _bigBodyCells.First.Value;
                    var next = _bigBodyCells.First.Next.Value; // 头部相邻的身体
                    Vector2Int dir = head - next; // 远离身体方向
                    var candidates = SnakeControllerUtil.ForwardLeftRight(dir);

                    //优先走大格
                    for (int i = 0; i < candidates.Length; i++)
                    {
                        var nextHeadBig = head + candidates[i];
                        if (!_grid.IsInside(nextHeadBig)) continue;
                        if (IsPathBlocked(nextHeadBig)) continue;

                        newCell = nextHeadBig;
                        isAddNew = true;
                        break;
                    }

                }

                if(isAddNew && newCell != SnakeControllerUtil.InvalidCell)
                {
                    _cellPathWithMouse.AddLast(_grid.CellToWorld(newCell));
                }
            }
            
            return _cellPathWithMouse.Count >= 2;
        }

        void ApplySmoothVisualsTargetPath(bool activeFromHead)
        {
            int n = _bodyCells.Count;
            if (n == 0) return;


            if (_lineTargetPositionsCache == null || _lineTargetPositionsCache.Length < n)
                _lineTargetPositionsCache = new Vector2[Mathf.NextPowerOfTwo(n)];

            var path = _cellPathWithMouse.ToList();

            // 预计算折线总长
            float totalLen = 0f;
            for (int i = 1; i < path.Count; i++) totalLen += Vector2.Distance(path[i - 1], path[i]);

            // 扫描光标（从起点向前）
            int polyIdx = 1;
            Vector2 cur = path[0];
            Vector2 nxt = path.Count > 1 ? path[1] : path[0];
            float segLen = Vector2.Distance(cur, nxt);
            float acc = 0f;
            float offset = 0f;
            int segmentCount = n;

            float step = _segmentspacing;

            void SampleAndWrite(float targetDist, int writeIndex)
            {
                // 优先尝试偏移后的采样
                float desired = targetDist + offset;
                bool useOffset = desired >= 0f && desired <= totalLen;
                float s = useOffset ? desired : targetDist;

                // 推进光标至 s
                while (acc + segLen + 1e-4f < s && polyIdx < path.Count - 1)
                {
                    acc += segLen;
                    cur = nxt;
                    polyIdx++;
                    nxt = path[polyIdx];
                    segLen = Vector2.Distance(cur, nxt);
                }

                Vector2 pos;
                if (segLen <= 1e-6f) pos = cur;
                else
                {
                    float need = Mathf.Clamp(s - acc, 0f, segLen);
                    float t = need / Mathf.Max(segLen, 1e-6f);
                    pos = Vector2.LerpUnclamped(cur, nxt, t);
                }

                _lineTargetPositionsCache[writeIndex] = pos;
            }

            for (int i = 0; i < segmentCount; i++)
            {
                float baseDist = i * step;

                float target = baseDist;
                int writeIndex = i;
                SampleAndWrite(target, writeIndex);
            }

        }



        void MoveNextFollowTweeners(bool activeFromHead, int tweenerindex, float duration)
        {
            // 平滑跟随模式 - 使用DOTween
            RectTransform curTransform = _cachedRectTransforms[tweenerindex];
            Tweener curFollowTweener = _cellFollowTweeners[tweenerindex];


            Vector2 target;
            if (activeFromHead)
            {
                target = _lineTargetPositionsCache[tweenerindex];
            }
            else
            {
                target = _lineTargetPositionsCache[_cachedRectTransforms.Count - 1 - tweenerindex];
            }

            if (curFollowTweener == null || !curFollowTweener.IsPlaying())
            {
                _cellFollowTweeners[tweenerindex]?.Kill();
                curFollowTweener = curTransform.DOAnchorPos(target, duration)
                    .SetEase(easeType)
                    .SetAutoKill(false);

                _cellFollowTweeners[tweenerindex] = curFollowTweener;
            }
            else
            {
                // 更新Tweener的目标位置
                curFollowTweener.ChangeEndValue(target, true).Restart();
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
            }
        }

        #endregion

        #region Big Cell Occupancy And Pathfinding

        bool CheckOccupiedBySelfForword(bool activeFromHead, Vector2Int bigcell)
        {
            var exclude = activeFromHead ? GetHeadCell() : GetTailCell();
            if (bigcell == exclude)
            {
                return true;
            }
            var exclude2 = activeFromHead ? _bigBodyCells.First.Next.Value : _bigBodyCells.Last.Previous.Value;
            if (bigcell == exclude2)
            {
                return true;
            }

            var cellset = SnakeManager.Instance.GetSnakeOccupiedCells(this);
            if (cellset != null)
                return !cellset.Contains(bigcell);

            return false;
        }

        bool CheckNextBigCell(bool activeFromHead, Vector2Int nextCell)
        {
            if (_cachedRectTransforms.Count == 0 || _bodyCells.Count == 0)
                return false;

            Vector2Int curCheckCell = GetHeadCell();
            if (!activeFromHead)
            {
                curCheckCell = GetTailCell();
            }
            if (nextCell == curCheckCell)
                return true;

            // 必须相邻
            if (Manhattan(curCheckCell, nextCell) != 1) return false;
            // 检查网格边界
            if (!_grid.IsInside(nextCell)) return false;
            // 使用与IsPathBlocked相同的阻挡检测逻辑，支持颜色匹配
            if (IsPathBlocked(nextCell)) return false;
            if (!CheckOccupiedBySelfForword(activeFromHead,nextCell)) return false;


            return true;
        }

        bool EnqueueBigCellPath(bool activeFromHead, Vector2Int from, Vector2Int to, LinkedList<Vector2Int> pathList, int maxPathCount = -1)
        {
            pathList.Clear();
            if (from == to) return false;
            if (!_grid.IsValid()) return false;
            if (!_grid.IsInside(from)) return false;

            // 允许 to 越界，夹紧到边缘
            var target = ClampInside(to);

            // 计算“前方”方向：拖动段后面一格 → 拖动段
            Vector2Int preferredDir = Vector2Int.zero;
            if (_bodyCells != null && _bodyCells.Count >= 2)
            {
                if (activeFromHead)
                {
                    // next(后面一格) -> head(拖动段)
                    var head = _currentHeadCell;
                    var neck = SnakeControllerUtil.GetCellAt(_bodyCells, 1, Vector2Int.zero);
                    preferredDir = SnakeControllerUtil.DirectionBetween(head, neck);
                }
                else
                {
                    // preTail(后面一格) -> tail(拖动段)
                    var tail = _currentTailCell;
                    var preTail = SnakeControllerUtil.GetCellAt(_bodyCells, _bodyCells.Count - 2, Vector2Int.zero);
                    preferredDir = SnakeControllerUtil.DirectionBetween(tail, preTail);
                }
            }
            // 只有一段时，退化为朝向目标的方向
            if (preferredDir == Vector2Int.zero)
            {
                preferredDir = SnakeControllerUtil.DirectionBetween(target, from);
            }

            // A*（浮点代价）
            var open = new List<Vector2Int>(64);
            var openSet = new HashSet<Vector2Int>();
            var closed = new HashSet<Vector2Int>();
            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            var gScore = new Dictionary<Vector2Int, float>();
            var fScore = new Dictionary<Vector2Int, float>();

            int Heuristic(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

            open.Add(from);
            openSet.Add(from);
            gScore[from] = 0f;
            fScore[from] = Heuristic(from, target);

            // 回退用：记录“离目标最近”的已可达节点
            Vector2Int bestNode = from;
            int bestH = Heuristic(from, target);
            float bestG = 0f;

            // 4邻域
            Vector2Int[] dirs = SnakeControllerUtil.CardinalDirections;

            while (open.Count > 0)
            {
                // 取 f 最小（加强方向偏好的 fScore 也会影响这里的选择顺序）
                int bestIdx = 0;
                float bestF = float.PositiveInfinity;
                for (int i = 0; i < open.Count; i++)
                {
                    var n = open[i];
                    float f = fScore.TryGetValue(n, out var fv) ? fv : float.PositiveInfinity;
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
                    // 回溯路径
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
                float curG = gScore.TryGetValue(current, out var cg) ? cg : float.PositiveInfinity;
                int curH = Heuristic(current, target);
                if (curG < float.PositiveInfinity && (curH < bestH || (curH == bestH && curG < bestG)))
                {
                    bestH = curH;
                    bestG = curG;
                    bestNode = current;
                }

                for (int i = 0; i < 4; i++)
                {
                    var step = dirs[i];
                    var nb = new Vector2Int(current.x + step.x, current.y + step.y);

                    // 边界与阻挡
                    if (!_grid.IsInside(nb)) continue;
                    if (IsPathBlocked(nb)) continue;
                    if (SnakeManager.Instance.IsCellOccupiedByOtherSnakes(nb, this))
                    {
                        continue;
                    }
                    if (closed.Contains(nb)) continue;

                    // 本步方向罚分：非前进、后退、左右转
                    float stepPenalty = 0f;
                    if (preferredDir != Vector2Int.zero)
                    {
                        // 非前进基础罚分
                        if (step != preferredDir) stepPenalty += AStarDirectionBias;

                        // 后退（远离“拖动段后一格”）
                        if (step.x == -preferredDir.x && step.y == -preferredDir.y)
                        {
                            stepPenalty += AStarBackwardPenalty;
                        }
                        else
                        {
                            // 左/右转（与 preferredDir 正交）
                            int dot = step.x * preferredDir.x + step.y * preferredDir.y; // -1,0,1
                            if (dot == 0)
                            {
                                // 叉积：>0 左转，<0 右转（坐标：x右 y上）
                                int cross = preferredDir.x * step.y - preferredDir.y * step.x;
                                if (cross > 0) stepPenalty += AStarTurnLeftPenalty;
                                else if (cross < 0) stepPenalty += AStarTurnRightPenalty;
                            }
                        }
                    }

                    // g 代价：基础1 + 方向罚分
                    float tentativeG = (curG < float.PositiveInfinity) ? (curG + 1f + stepPenalty) : float.PositiveInfinity;
                    if (!(tentativeG < float.PositiveInfinity)) continue;

                    bool isBetter = false;
                    if (!openSet.Contains(nb))
                    {
                        open.Add(nb);
                        openSet.Add(nb);
                        isBetter = true;
                    }
                    else
                    {
                        float oldG = gScore.TryGetValue(nb, out var og) ? og : float.PositiveInfinity;
                        if (tentativeG < oldG) isBetter = true;
                    }

                    if (isBetter)
                    {
                        cameFrom[nb] = current;
                        gScore[nb] = tentativeG;

                        // f = g + h +（再加一次“方向偏好”用于强偏好）
                        // 这样在选择开放表最小 f 时，也会倾向于“前进/侧转”而不是后退
                        float h = Heuristic(nb, target);
                        fScore[nb] = tentativeG + h + stepPenalty * 2;
                    }
                }
            }

            // 无法到达，回退到最近可达节点
            if (bestNode != from && cameFrom.ContainsKey(bestNode))
            {
                var rev = new List<Vector2Int>(64);
                var c = bestNode;
                while (!c.Equals(from))
                {
                    rev.Add(c);
                    if (!cameFrom.TryGetValue(c, out c)) break;
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

            return false;
        }

        #endregion

        #region Body Cell Synchronization

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
                    _bodyCells.AddLast(WorldToCellClamped(rt.anchoredPosition));
                }

            }

            GenerateBigCellBodys();

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
                }
            }
            {
                var hole = FindHoleAtOrAdjacentWithColor(_currentTailCell, ColorType);
                if (hole != null)
                {
                    _consumeCoroutine ??= StartCoroutine(CoConsume(hole, false));
                }
            }
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
                int segmentIndex = 0;
                for (int i = 0; i < _bodyCells.Count && segmentIndex < newInitialBodyCells.Length; i++)
                {
                    newInitialBodyCells[segmentIndex] = SnakeControllerUtil.GetCellAt(_bodyCells, i, Vector2Int.zero);
                    segmentIndex++;
                }
            }
            else
            {
                int segmentIndex = Length - 1;
                for (int i = _bodyCells.Count - 1; i >= 0 && segmentIndex >= 0; i--)
                {
                    newInitialBodyCells[segmentIndex] = SnakeControllerUtil.GetCellAt(_bodyCells, i, Vector2Int.zero);
                    segmentIndex--;
                }
            }


            InitializeSegmentPositions(newInitialBodyCells);
            UpdateCachedRectTransformsFromBodyCells();
        }

        #endregion

        #region Consume Flow


        public IEnumerator CoConsume(HoleEntity hole, bool fromHead)
        {
            hole.OnTirggerStart();
            _consuming = true;
            IsDragging = false; // 脱离手指控制

            _pendingTargetCellStates.Clear();
            _moveState.Clear();
            _curMoveState.Clear();
            _hasCurrentMoveTarget = false;
            foreach (var tw in _cellFollowTweeners)
            {
                tw?.Kill();
            }


            Vector3 world = _grid.CellToWorld(hole.Cell);


            // 1) 确定“活动端”：使用参数 fromHead
            world = _grid.ClampWorld(world);

            Vector2Int targetCell = WorldToCellClamped(world);
            Vector2Int fromCell;

            if (_pendingTargetCellStates.Count > 0)
            {
                fromCell = _pendingTargetCellStates.Last.Value.TargetCell;
                fromHead = _pendingTargetCellStates.Last.Value.DragFromHead;
            }
            else
            {
                fromCell = fromHead ? GetHeadCell() : GetTailCell();
            }

            // 寻路（大格）
            _cellPathQueue ??= new LinkedList<Vector2Int>();
            _cellPathQueue.Clear();
            EnqueueBigCellPath(fromHead, fromCell, targetCell, _cellPathQueue);
            var speed = Time.deltaTime;

            for (var n = _cellPathQueue.First; n != null; n = n.Next)
            {
                var cell = n.Value;
                var cellWorld = _grid.CellToWorld(cell);
                _pendingTargetCellStates.AddLast(new MoveState(fromHead, cell, cell, speed, cellWorld, true));
            }

            _moveState = new MoveState(fromHead, targetCell, targetCell, speed, world, false);

            RectTransform leadTransform = fromHead ? _cachedRectTransforms[0] : _cachedRectTransforms[_cachedRectTransforms.Count - 1];

            while (true)
            {
                var activeLeadPos = leadTransform.anchoredPosition;
                if (Vector2.Distance(world, activeLeadPos) <= EPS)
                {
                    break;
                }
                else
                {
                    if(!_moveState.IsValid())
                    {
                        if (_pendingTargetCellStates.Count > 0)
                        {
                            fromCell = _pendingTargetCellStates.Last.Value.TargetCell;
                            fromHead = _pendingTargetCellStates.Last.Value.DragFromHead;
                        }
                        else
                        {
                            fromCell = fromHead ? GetHeadCell() : GetTailCell();
                        }

                        _cellPathQueue.Clear();
                        EnqueueBigCellPath(fromHead, fromCell, targetCell, _cellPathQueue);
                        speed = Time.deltaTime;

                        for (var n = _cellPathQueue.First; n != null; n = n.Next)
                        {
                            var cell = n.Value;
                            var cellWorld = _grid.CellToWorld(cell);
                            _pendingTargetCellStates.AddLast(new MoveState(fromHead, cell, cell, speed, cellWorld, true));
                        }

                        _moveState = new MoveState(fromHead, targetCell, targetCell, speed, world, false);
                    }
                }

                yield return null; // 逐帧推进
            }

            // 5) 到达洞中心后，触发吞噬动画（逐帧推进，不阻塞）
            float allConsumeTime = hole.ConsumeInterval * Mathf.Max(1, _bodyCells.Count);
            float setpDis = _segmentspacing;
            var targetPos = world;

            int count = _cachedRectTransforms.Count;
            if (count <= 0) yield break;

            // 1) 快照每段的初始世界坐标（仅 XY 用于 DOAnchorPos 的“身体路径”）
            Vector2[] initXY = new Vector2[count];
            for (int i = 0; i < count; i++) initXY[i] = _cachedRectTransforms[i].anchoredPosition;

            // fromHead 决定顺序：顺序 0 是拖拽端（最终最深）
            int GetIndexInOrder(int o) => fromHead ? o : (count - 1 - o);

            // 2) 为每段构建“仅 XY”的 DOAnchorPos 路线：依次经过“前一段的初始坐标”，最终到达 targetPos
            float[] reachTargetTimes = new float[count]; // 每段抵达 targetPos(XY) 的时刻
            float[] segPathLen = new float[count];       // 每段 XY 路径总长
            Vector2[][] hopTo = new Vector2[count][];    // 每段的 XY 跳点

            for (int o = 0; o < count; o++)
            {
                int idx = GetIndexInOrder(o);

                // o 段的“前一段们”是顺序 o-1 → 0（沿着身体向拖拽端推进）
                List<Vector2> hops = new List<Vector2>();
                for (int k = o - 1; k >= 0; k--)
                {
                    int prevIdx = GetIndexInOrder(k);
                    hops.Add(initXY[prevIdx]);
                }
                hops.Add(targetPos); // 最终到达洞中心 XY

                hopTo[o] = hops.ToArray();

                // 计算总路长
                float total = 0f;
                Vector2 cur = initXY[idx];
                for (int h = 0; h < hopTo[o].Length; h++)
                {
                    total += Vector2.Distance(cur, hopTo[o][h]);
                    cur = hopTo[o][h];
                }
                segPathLen[o] = total;
            }

            // 统一速度：最长路径用满 allConsumeTime，其他段抵达后“待机”到结束
            float maxLen = 0f;
            for (int o = 0; o < count; o++) if (segPathLen[o] > maxLen) maxLen = segPathLen[o];
            float v = maxLen <= 1e-6f ? 0f : (maxLen / Mathf.Max(allConsumeTime, 1e-6f));

            // 3) 为每段创建 DOAnchorPos 串行动画（仅 XY），并记录抵达 targetPos 的时间
            for (int o = 0; o < count; o++)
            {
                int idx = GetIndexInOrder(o);
                RectTransform rt = _cachedRectTransforms[idx];

                float accT = 0f;
                Vector2 from = initXY[idx];

                Sequence seq = DOTween.Sequence().SetAutoKill(false).SetUpdate(true).SetEase(Ease.Linear);

                for (int h = 0; h < hopTo[o].Length; h++)
                {
                    Vector2 to = hopTo[o][h];
                    float d = Vector2.Distance(from, to);
                    float dur = v <= 1e-6f ? 0f : d / v;
                    accT += dur;
                    seq.Append(rt.DOAnchorPos(to, dur).SetEase(Ease.Linear));
                    from = to;
                }
                reachTargetTimes[o] = accT;

                // 若提前到达，填满剩余时间，XY 保持不动
                if (accT < allConsumeTime)
                    seq.AppendInterval(allConsumeTime - accT);

                seq.Play();
            }

            // 4) 运行期：仅在某段完成 XY 抵达 targetPos 之后，才开始对其进行 Z 负向台阶下沉
            float elapsed = 0f;

            while (elapsed < allConsumeTime)
            {
                // 统计“按顺序”已抵达 targetPos(XY) 的段数（顺序 0..reached-1）
                int reached = 0;
                for (int o = 0; o < count; o++)
                {
                    if (elapsed + 1e-6f >= reachTargetTimes[o]) reached++;
                    else break; // reachTargetTimes 随 o 单调不降（路径更短，越靠后越早到达）
                }

                // 仅对“已抵达 XY 的段”进行 Z 台阶式下沉；未抵达的一律保持原 Z
                for (int o = 0; o < count; o++)
                {
                    int idx = GetIndexInOrder(o);
                    var p3 = _cachedRectTransforms[idx].anchoredPosition3D;

                    if (o < reached)
                    {
                        // 台阶深度 = 已达数量 - 自己的顺序号（fromHead 时顺序 0 为拖拽端）
                        int steps = Mathf.Clamp(reached - o, 0, count);
                        p3.z = -setpDis * steps;
                    }
                    else
                    {
                        // 还未抵达 targetPos(XY) → 不允许提前下沉
                        p3.z = 0f;
                    }

                    _cachedRectTransforms[idx].anchoredPosition3D = p3;
                }

                if (_bodySpriteManager != null)
                {
                    _bodySpriteManager.OnSnakeLengthCoConsume(reached);
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // 5) 完成：所有段 XY = targetPos；Z 终态（拖拽端最深，依次上浮）
            for (int o = 0; o < count; o++)
            {
                int idx = GetIndexInOrder(o);
                _cachedRectTransforms[idx].anchoredPosition = targetPos;
                var p3 = _cachedRectTransforms[idx].anchoredPosition3D;
                p3.z = -setpDis * (count - o);
                _cachedRectTransforms[idx].anchoredPosition3D = p3;
            }
            _bodySpriteManager?.OnSnakeLengthCoConsume(count);

            // 全部消失后，销毁蛇对象或重生；此处直接销毁（保留原有行为）
            _bodyCells.Clear();
            Destroy(gameObject);
            SnakeManager.Instance.TryClearSnakes();
            hole.OnTirggered();

            _consuming = false;
            _consumeCoroutine = null;
            yield break;
        }

        #endregion

        #region Grid Entity Queries

        /// <summary>
        /// 查找目标位置本身或邻近位置的洞
        /// </summary>
        HoleEntity FindHoleAtOrAdjacentWithColor(Vector2Int targetCell, SnakeColorType color)
        {
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

        #endregion

        #region Coordinate And Cell Access

        Vector3 WorldToWorldCenter(Vector3 world)
        {
            // 2) 世界 → 夹紧到有效大格
            var bigCell = WorldToCellClamped(world);
            var bigCenter = _grid.CellToWorld(bigCell);

            return bigCenter;
        }

        Vector2Int WorldToCellClamped(Vector3 world)
        {
            return ClampInside(_grid.WorldToCell(world));
        }

        Vector2Int WorldToCellClamped(Vector2 world)
        {
            return WorldToCellClamped(new Vector3(world.x, world.y, 0f));
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

        #endregion

        #region Cleanup And Lifecycle

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
        public override void Destroy()
        {
            if (_consumeCoroutine != null)
            {
                StopCoroutine(_consumeCoroutine);
                _consumeCoroutine = null;
            }
            if (_coProduce != null)
            {
                StopCoroutine(_coProduce);
                _coProduce = null;
            }
            if (_coConsume != null)
            {
                StopCoroutine(_coConsume);
                _coConsume = null;
            }
            if (_coMove != null)
            {
                StopCoroutine(_coMove);
                _coMove = null;
            }
            if (_coRender != null)
            {
                StopCoroutine(_coRender);
                _coRender = null;
            }

        }


        protected override void OnDestroy()
        {
            base.OnDestroy();
        }

        #endregion
    }
}

