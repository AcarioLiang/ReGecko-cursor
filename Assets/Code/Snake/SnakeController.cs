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
        [SerializeField] bool DebugShowLeadTarget = false;
        [SerializeField] Color DebugLeadTargetColor = Color.red;
        [SerializeField] float DebugLeadMarkerSize = 10f;

        [SerializeField] bool DebugShowVirtualPath = false;
        [SerializeField] Color DebugPolylineColor = new Color(0f, 1f, 0f, 0.9f);
        [SerializeField] Color DebugPolylineHeadColor = Color.red;
        [SerializeField] Color DebugPolylineTailColor = Color.green;
        [SerializeField] float DebugPolylinePointSize = 10f;

        [SerializeField] bool DebugShowPolyline = false;
        [SerializeField] bool DebugShowBigCellPath = false;

        [Header("SnakeController特有属性")]
        // 拖拽相关
        bool _startMove;
        bool _isReverse;

        [SerializeField] float AStarDirectionBias = 1.0f; // 非前进方向的基础罚分
        [SerializeField] float AStarBackwardPenalty = 3f; // 朝身体反向（与preferredDir相反）额外罚分
        [SerializeField] float AStarTurnLeftPenalty = 1f; // 左转额外罚分
        [SerializeField] float AStarTurnRightPenalty = 1f; // 右转额外罚分

        private Queue<GameObject> _subSegmentPool = new Queue<GameObject>();
        private List<GameObject> _subSegments = new List<GameObject>();
        protected readonly LinkedList<Vector2Int> _subBodyCells = new LinkedList<Vector2Int>(); // 离散身体占用格，头在First
        private readonly List<RectTransform> _cachedSubRectTransforms = new List<RectTransform>();

        private HashSet<HoleEntity> _cacheCoconsumeHoles = new HashSet<HoleEntity>();


        public override LinkedList<Vector2Int> GetBodyCells()
        {
            return _subBodyCells;
        }

        public override List<GameObject> GetSegments()
        {
            return _subSegments;
        }

        // 大格寻路相关
        private LinkedList<Vector2Int> _cellPathQueue = new LinkedList<Vector2Int>(); // 大格路径队列
        private LinkedList<Vector2> _cellPathWithMouse = new LinkedList<Vector2>(); // 修正的路径队列，_activeLeadPos 链接_cellPathQueue尾端链接mouse点


        private Vector2Int _currentHeadCell;
        private Vector2Int _currentTailCell;
        private Vector2Int _currentHeadSubCell;
        private Vector2Int _currentTailSubCell;
        private Vector2Int _lastSampledSubCell;
        


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

        Vector2 _leadTargetPos;          // 正在朝向的目标小格中心（世界坐标）
        Vector2 _leadReversePos;         // 倒车的目标点（世界坐标）
        Vector2Int _leadTargetCell;   // 拖动端当前目标小格（中线）
        Vector2Int _leadLastTargetCell;   // 拖动端上次的目标格子
        Vector2 _leadLastTargetPos;   // 拖动端上次的目标格子

        Vector2 _leadOffsetPos;         // 点击时鼠标在本格内的偏移

        Vector2Int _leadReverseCell;  // 拖动端倒车目标小格（中线）

        bool _switchToSingleCellPath = false;
        bool _lastIsSingleCellPath = false;
        Vector2 _lastSingleCellPathPos;
        Vector3[] _linePositionsCache;

        bool _smoothInited = false;

        float _segmentspacing;           // 每段身体之间固定间距（世界单位）
        float _leadSpeedWorld;           // 拖动端线速度（世界单位/秒）
        const float EPS = 1e-4f;

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

        // 新增：
        List<Vector2> _virtualPathPoints = new List<Vector2>(512);
        List<Vector2> _virtualPathPointsRe = new List<Vector2>(512);
        LinkedList<Vector2Int> _virtualBodyCells = new LinkedList<Vector2Int>();



        public List<Vector2> GetVirtualPathPoints()
        {
            return _virtualPathPoints;
        }

        public List<Vector2> GetVirtualPathPointsRe()
        {
            return _virtualPathPointsRe;
        }
        // 扫描缓存
        Vector2[] _tmpBodyPos;
        float[] _distTargets;
        Vector2Int[] _tmpSubSnap2;


        public override void Initialize(GridConfig grid)
        {
            _grid = grid;
            IsDragging = false;
            DragFromHead = false;


            RecreateSubSegments();
            ClearUnuseSubSegments();
            InitializeSubSegmentPositions(InitialBodyCells);
            InitializeBodySpriteManager();
        }

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
            image.sprite = null;// BodySprite;
            image.color = BodyColor;
            if (EnableBodySpriteManagement)
            {
                image.enabled = false;
            }
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(_grid.CellSize * 0.8f, SubGridHelper.SUB_CELL_SIZE * _grid.CellSize + 5); // 转换为UI单位
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


            Image img = null;
            for (int segmentIndex = 0; segmentIndex < Mathf.Max(1, Length); segmentIndex++)
            {
                var subSegmentList = new List<GameObject>();
                for (int i = 0; i < SubGridHelper.SUB_DIV; i++)
                {
                    var subSegment = GetSubSegmentFromPool();
                    subSegment.name = ($"SubSegment_{segmentIndex}_{i}");
                    subSegmentList.Add(subSegment);
                    img = subSegment.GetComponent<Image>();
                    _subSegments.Add(subSegment);
                }
            }
        }

        void ClearUnuseSubSegments()
        {
            //删除首尾多余cell
            for (int di = 0; di < SubGridHelper.CENTER_INDEX; di++)
            {
                ReturnSubSegmentToPool(_subSegments[di].gameObject);
                ReturnSubSegmentToPool(_subSegments[_subSegments.Count - 1 - di].gameObject);
            }

            for (int di = SubGridHelper.CENTER_INDEX - 1; di >= 0; di--)
            {
                _subSegments.RemoveAt(_subSegments.Count - 1);
            }

            for (int di = SubGridHelper.CENTER_INDEX - 1; di >= 0; di--)
            {
                _subSegments.RemoveAt(0);
            }


            _cachedSubRectTransforms.Clear();
            RectTransform rt = null;
            foreach (var subSegment in _subSegments)
            {
                rt = subSegment.GetComponent<RectTransform>();
                _cachedSubRectTransforms.Add(rt);
            }
        }

        /// <summary>
        /// 初始化所有子段的位置
        /// </summary>
        void InitializeSubSegmentPositions(Vector2Int[] initialbodycells)
        {
            var bodyCells = initialbodycells;
            if (bodyCells == null || bodyCells.Length < 2) return;



            _subBodyCells.Clear();

            // 工具：方向与边的映射
            Vector2Int DirToDelta(int dir)
            {
                switch (dir)
                {
                    // 0:Left 1:Right 2:Down 3:Up
                    case 0: return new Vector2Int(-1, 0);
                    case 1: return new Vector2Int(1, 0);
                    case 2: return new Vector2Int(0, -1);
                    case 3: return new Vector2Int(0, 1);
                    default: return Vector2Int.zero;
                }
            }
            int DeltaToDir(Vector2Int d)
            {
                if (d == new Vector2Int(-1, 0)) return 0;
                if (d == new Vector2Int(1, 0)) return 1;
                if (d == new Vector2Int(0, -1)) return 2;
                if (d == new Vector2Int(0, 1)) return 3;
                return -1;
            }
            int Opposite(int dir)
            {
                if (dir == 0) return 1; // L->R
                if (dir == 1) return 0; // R->L
                if (dir == 2) return 3; // D->U
                if (dir == 3) return 2; // U->D
                return -1;
            }
            Vector2Int SideToEntrySub(Vector2Int bigCell, int side)
            {
                // side: 0-L 1-R 2-D 3-U
                switch (side)
                {
                    case 0: return SubGridHelper.BigCellToLeftSubCell(bigCell);
                    case 1: return SubGridHelper.BigCellToRightSubCell(bigCell);
                    case 2: return SubGridHelper.BigCellToBottomSubCell(bigCell);
                    case 3: return SubGridHelper.BigCellToTopSubCell(bigCell);
                    default: return SubGridHelper.BigCellToCenterSubCell(bigCell);
                }
            }
            Vector2Int[] BuildFiveSubCells(Vector2Int bigCell, int entrySide, int exitSide)
            {
                // 在该大格内，沿中线从 entry 边界走到 exit 边界，必经中心(2,2)，共4步=5点
                var res = new Vector2Int[SubGridHelper.SUB_DIV];
                // 入口点
                res[0] = SideToEntrySub(bigCell, entrySide);

                // 目标“路标”：中心 + 出口边界点
                var center = SubGridHelper.BigCellToCenterSubCell(bigCell);
                var exit = SideToEntrySub(bigCell, exitSide);

                // 在该大格内行走：先到中心，再到出口（每次一步，保证正好填满5个点）
                Vector2Int cur = res[0];

                // 步进到中心（最多2步）
                while (cur != center && (res[1] == default || res[2] == default || res[3] == default || res[4] == default))
                {
                    if (cur.x != center.x)
                    {
                        int step = cur.x < center.x ? 1 : -1;
                        cur = new Vector2Int(cur.x + step, cur.y);
                    }
                    else if (cur.y != center.y)
                    {
                        int step = cur.y < center.y ? 1 : -1;
                        cur = new Vector2Int(cur.x, cur.y + step);
                    }
                    // 写入下一个空位
                    for (int k = 1; k < SubGridHelper.SUB_DIV; k++)
                    {
                        if (res[k] == default)
                        {
                            res[k] = cur;
                            break;
                        }
                    }
                    if (res[SubGridHelper.SUB_DIV - 1] != default) break;
                }

                // 如果还没填满，继续从中心到出口（最多2步）
                while (cur != exit && res[SubGridHelper.SUB_DIV - 1] == default)
                {
                    if (cur.x != exit.x)
                    {
                        int step = cur.x < exit.x ? 1 : -1;
                        cur = new Vector2Int(cur.x + step, cur.y);
                    }
                    else if (cur.y != exit.y)
                    {
                        int step = cur.y < exit.y ? 1 : -1;
                        cur = new Vector2Int(cur.x, cur.y + step);
                    }
                    for (int k = 1; k < SubGridHelper.SUB_DIV; k++)
                    {
                        if (res[k] == default)
                        {
                            res[k] = cur;
                            break;
                        }
                    }
                }

                // 兜底：若因极端情况未填满，剩余点重复最后一个，保证长度为5且连续
                for (int k = 1; k < SubGridHelper.SUB_DIV; k++)
                {
                    if (res[k] == default) res[k] = res[k - 1];
                }
                return res;
            }


            Vector2Int[] BuildHeadSubCells(Vector2Int bigCell, int entrySide, int exitSide)
            {
                var res = new Vector2Int[SubGridHelper.SUB_DIV - 2];
                var fiveres = BuildFiveSubCells(bigCell, entrySide, exitSide);
                for (int i = 2; i < fiveres.Length; i++)
                {
                    res[i - 2] = fiveres[i];
                }
                return res;
            }

            Vector2Int[] BuildLastSubCells(Vector2Int bigCell, int entrySide, int exitSide)
            {
                var res = new Vector2Int[SubGridHelper.SUB_DIV - 2];
                var fiveres = BuildFiveSubCells(bigCell, entrySide, exitSide);
                for (int i = 0; i < fiveres.Length - 2; i++)
                {
                    res[i] = fiveres[i];
                }
                return res;
            }

            // 为每个大格生成5个连续的小格，并写入链表与可视
            int segmentIndex = 0;
            for (int i = 0; i < bodyCells.Length; i++)
            {
                var curBig = bodyCells[i];

                // 计算入/出边
                int entrySide, exitSide;
                if (i == 0)
                {
                    // 头：入口为反向边，出口为指向第二个格的边
                    var dirToNext = DeltaToDir(bodyCells[1] - curBig);
                    exitSide = dirToNext;
                    entrySide = Opposite(exitSide);
                }
                else if (i == bodyCells.Length - 1)
                {
                    // 尾：入口为来自前一格的反向边，出口为其对边（形成封闭端）
                    var dirFromPrev = DeltaToDir(curBig - bodyCells[i - 1]);
                    entrySide = Opposite(dirFromPrev);
                    exitSide = Opposite(entrySide);
                }
                else
                {
                    // 中间：入口=来自前一格的反向边，出口=指向下一格的边
                    var dirFromPrev = DeltaToDir(curBig - bodyCells[i - 1]);
                    var dirToNext = DeltaToDir(bodyCells[i + 1] - curBig);
                    entrySide = Opposite(dirFromPrev);
                    exitSide = dirToNext;
                }

                // 生成该段5个小格（沿中线，必经中心，最多一次转向）
                var subCellPositions = BuildFiveSubCells(curBig, entrySide, exitSide);

                // 同步到链表与显示
                for (int k = 0; k < subCellPositions.Length; k++)
                {
                    _subBodyCells.AddLast(subCellPositions[k]);
                }
                UpdateSubSegmentPositions(segmentIndex, subCellPositions);
                segmentIndex++;
            }

            // 连续性校验：所有小格必须两两相邻（曼哈顿距离=1）
            var node = _subBodyCells.First;
            var idx = 0;
            while (node != null && node.Next != null)
            {
                var a = node.Value;
                var b = node.Next.Value;
                if (SubGridHelper.SubCellManhattan(a, b) != 1)
                {
                    Debug.LogError($"InitializeSubSegmentPositions: 小格不连续 at pair index {idx}->{idx + 1}, a={a}, b={b}");
                    break;
                }
                node = node.Next;
                idx++;
            }

            for (int di = 0; di < SubGridHelper.CENTER_INDEX; di++)
            {
                _subBodyCells.RemoveFirst();
                _subBodyCells.RemoveLast();
            }

            _currentHeadSubCell = _subBodyCells.First.Value;
            _currentTailSubCell = _subBodyCells.Last.Value;
            _currentHeadCell = SubGridHelper.SubCellToBigCell(_currentHeadSubCell);
            _currentTailCell = SubGridHelper.SubCellToBigCell(_currentTailSubCell);

            _lastSampledSubCell = _currentHeadSubCell;
            // 初始放置完成后，更新身体图片
            if (EnableBodySpriteManagement && _bodySpriteManager != null)
            {
                _bodySpriteManager.OnSnakeLengthChanged();
            }
        }


        /// <summary>
        /// 更新子段位置（由SnakeController调用）
        /// </summary>
        /// <param name="segmentIndex">身体节点索引</param>
        /// <param name="subCellPositions">5个子段的小格坐标</param>
        void UpdateSubSegmentPositions(int segmentIndex, Vector2Int[] subCellPositions)
        {
            var grid = GetGrid();

            int offsethead = SubGridHelper.CENTER_INDEX;
            for (int i = 0; i < subCellPositions.Length; i++)
            {
                if(segmentIndex == 0)
                {
                    if(i < SubGridHelper.CENTER_INDEX)
                    {
                        continue;
                    }
                }
                if(segmentIndex == Length -1)
                {
                    if (i > SubGridHelper.CENTER_INDEX)
                    {
                        continue;
                    }
                }

                int curSubIndex = segmentIndex * SubGridHelper.SUB_DIV + i - offsethead;
                if (curSubIndex < _subSegments.Count)
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


        public override void UpdateGridConfig(GridConfig newGrid)
        {
            _grid = newGrid;
            for (int i = 0; i < _subSegments.Count; i++)
            {
                var rt = _subSegments[i].GetComponent<RectTransform>();
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
            if (_subBodyCells.Count == 0 || !IsAlive() || _cachedSubRectTransforms.Count == 0)
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
            if (_subBodyCells == null || _subBodyCells.Count == 0 || _cachedSubRectTransforms.Count == 0) return;


            // 速度/间距（世界单位）
            RefreshKinematicsIfNeeded();

            //寻路开始
            // 1) 确定“活动端”：正向=DragFromHead；倒车时取相反端
            bool activeFromHead = _isReverse ? !DragFromHead : DragFromHead;

            // 2) 初始化活动端的平滑状态（首次或切换端时）
            EnsureActiveLeadInited(activeFromHead);


            // 先把旧格子寻路走完
            if (_leadLastTargetCell != Vector2Int.zero || _leadLastTargetPos != Vector2.zero)
            {
                if (_isReverse)
                {
                    //_leadReversePos = _grid.CellToWorld(_leadLastTargetCell);
                    _leadReversePos = _leadLastTargetPos;
                }
                else
                {
                    //_leadTargetPos = _grid.CellToWorld(_leadLastTargetCell);
                    _leadTargetPos = _leadLastTargetPos;
                }
            }
            else
            {
                // 新寻路
                _switchToSingleCellPath = false;
                _isReverse = false;

                // 采样鼠标 → 期望小格（中线）
                var world = ScreenToWorld(Input.mousePosition);
                Vector2Int targetSubCell = SubGridHelper.WorldToSubCell(world, _grid);


                var leadCurrentSubCell = DragFromHead ? GetHeadSubCell() : GetTailSubCell();


                // 取“一步”的目标格
                _cellPathQueue ??= new LinkedList<Vector2Int>();
                _cellPathQueue.Clear();

                
                EnqueueSubCellPath(leadCurrentSubCell, targetSubCell, _cellPathQueue);
                //GenerateBigCellPathWithMouse();
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
                        if (_leadTargetCell == (DragFromHead ? _subBodyCells.First.Next.Value : _subBodyCells.Last.Previous.Value))
                        {
                            return;
                        }
                    }


                    if (!_isReverse)
                    {

                        var bigcell = SubGridHelper.SubCellToBigCell(_leadTargetCell);
                        // 检查目标点合法性
                        if (!CheckNextCell(bigcell))
                        {
                            //Debug.LogError("CheckNextCell return!");
                            return;
                        }
                    }

                    var curTargetSubCell = _isReverse ? _leadReverseCell : _leadTargetCell;

                    _leadLastTargetCell = curTargetSubCell;

                    if (_isReverse)
                    {
                        _leadReversePos = SubGridHelper.SubCellToWorld(curTargetSubCell, _grid);
                        _leadLastTargetPos = _leadReversePos;
                    }
                    else
                    {
                        _leadTargetPos = SubGridHelper.SubCellToWorld(curTargetSubCell, _grid);
                        _leadLastTargetPos = _leadTargetPos;
                    }


                }
                else
                {
                    return;
                    _isReverse = false;

                    var mousepos = ScreenToWorldCenter(Input.mousePosition);

                    // 倒车判定（你已有）
                    if (CheckIfNeedReverseByPos(mousepos, out _leadReversePos))
                    {
                        _isReverse = true;
                        activeFromHead = _isReverse ? !DragFromHead : DragFromHead;
                        EnsureActiveLeadInited(activeFromHead);
                    }

                    if (!_isReverse)
                    {
                        _leadTargetPos = mousepos;
                        _leadLastTargetPos = _leadTargetPos;
                    }
                    else
                    {
                        _leadLastTargetPos = _leadReversePos;
                    }


                }
            }

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

            if (targetPos == Vector2.zero)
            {
                return;
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
                    //_activeLeadPos = FixActiveLeadPosWithWalkPath(_activeLeadPos);
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


            {
                if (EnableBodySpriteManagement && _bodySpriteManager != null)
                {
                    _bodySpriteManager.UpdateLineFromPolyline(

                        _centerlinePolyline,
                        _cachedSubRectTransforms.Count,
                        _segmentspacing,
                        activeFromHead
                    );
                }
            }




            if (reachedThisFrame)
            {
                _leadLastTargetCell = Vector2Int.zero;
                _leadLastTargetPos = Vector2.zero;
                _switchToSingleCellPath = false;
                UpdateBodyCellsFromCachedRectTransforms();

                // 5) 刷新缓存
                _currentHeadSubCell = _subBodyCells.First.Value;
                _currentTailSubCell = _subBodyCells.Last.Value;
                _currentHeadCell = SubGridHelper.SubCellToBigCell(_currentHeadSubCell);
                _currentTailCell = SubGridHelper.SubCellToBigCell(_currentTailSubCell);

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

        void ReUpdateCenterlinePolyline()
        {
            bool isinit = false;
            if (_activeLeadPath.Count < 5)
                isinit = true;

            if (_activeLeadPath.Count > 0)
            {
                if (DragFromHead)
                {
                    bool find = false;
                    int addcount = 0;
                    float subStep = _segmentspacing * SubGridHelper.SUB_CELL_SIZE;
                    var firstpath = _activeLeadPath[_activeLeadPath.Count - 1];
                    for (int i = _virtualPathPoints.Count - 1; i >= 0; i--)
                    {
                        if (find == false)
                        {
                            if (Vector2.Distance(firstpath, _virtualPathPoints[i]) < subStep)
                            {
                                find = true;

                                continue;
                            }
                        }

                        if (find)
                        {
                            if (addcount >= (isinit ? 7 : 2))
                            {
                                break;
                            }

                            _activeLeadPath.Add(_virtualPathPoints[i]);
                            addcount++;
                        }

                    }

                    _activeLeadPos = _activeLeadPath[_activeLeadPath.Count - 1];
                }
                else
                {
                    bool find = false;
                    int addcount = 0;
                    float subStep = _segmentspacing * SubGridHelper.SUB_CELL_SIZE;
                    var firstpath = _activeLeadPath[_activeLeadPath.Count - 1];
                    for (int i = 0; i < _virtualPathPoints.Count - 1; i++)
                    {
                        if (find == false)
                        {
                            if (Vector2.Distance(firstpath, _virtualPathPoints[i]) < subStep)
                            {
                                find = true;

                                continue;
                            }
                        }

                        if (find)
                        {
                            if (addcount >= (isinit ? 7 : 2))
                            {
                                break;
                            }

                            _activeLeadPath.Add(_virtualPathPoints[i]);
                            addcount++;
                        }

                    }

                    _activeLeadPos = _activeLeadPath[_activeLeadPath.Count - 1];
                }

            }
            //             
            //             if (EnableBodySpriteManagement && _bodySpriteManager != null)
            //             {
            //                 List<Vector2> tmpoldactiveLeadPath = new List<Vector2>(_activeLeadPath);
            //                 _activeLeadPath.Clear();
            // 
            //                 if (_linePositionsCache == null)
            //                 {
            //                     _linePositionsCache = new Vector3[_bodySpriteManager.GetCurLinePositionsCount()];
            //                 }
            // 
            //                 _linePositionsCache = _bodySpriteManager.GetCurLinePositions();
            // 
            // 
            //                 // 获取容器RectTransform用于坐标转换
            //                 var container = transform.parent as RectTransform;
            //                 if (container == null) return;
            // 
            //                 // 将世界坐标转换为蛇的本地坐标，并保存到_activeLeadPath
            //                 for (int i = 0; i < _linePositionsCache.Length; i++)
            //                 {
            //                     // 世界坐标 -> 本地坐标 (GridContainer的本地坐标系)
            //                     Vector3 localPos = container.InverseTransformPoint(_linePositionsCache[i]);
            // 
            //                     // 添加到路径中
            //                     _activeLeadPath.Add(new Vector2(localPos.x, localPos.y));
            //                 }
            // 
            // 
            //                 _activeLeadPos = _activeLeadPath[0];
            // 
            //                 //添加就路点
            //                 var lastvirtual = _activeLeadPath[_activeLeadPath.Count - 1];
            // 
            //                 bool find = false;
            //                 int addcount = 0;
            //                 float subStep = _segmentspacing * SubGridHelper.SUB_CELL_SIZE;
            //                 for (int i = 0; i < tmpoldactiveLeadPath.Count; i++)
            //                 {
            //                     if (find == false)
            //                     {
            //                         if (Vector2.Distance(lastvirtual, tmpoldactiveLeadPath[i]) < subStep)
            //                         {
            //                             find = true;
            //                             //路点过少就不要了
            //                             if (tmpoldactiveLeadPath.Count - i < 5)
            //                             {
            //                                 Debug.Log("GenerateVirtualPathPoints less then break!!");
            //                                 find = false;
            //                                 break;
            //                             }
            // 
            //                             continue;
            //                         }
            //                     }
            // 
            // 
            //                     if (find)
            //                     {
            //                         _activeLeadPath.Add(tmpoldactiveLeadPath[i]);
            //                         addcount++;
            //                     }
            //                 }
            //             }
        }

        void UpdateSingleCellMovement()
        {
            if (EnableBodySpriteManagement && _bodySpriteManager != null)
            {
                var mouseWorld = ScreenToWorld(Input.mousePosition);
                var mouseCell = _grid.WorldToCell(mouseWorld);

                if (!_grid.IsInside(mouseCell))
                    return;
                if (IsPathBlocked(mouseCell))
                    return;
                if (SnakeManager.Instance.IsCellOccupiedByOtherSnakes(mouseCell, this))
                {
                    return;
                }

                if (!GenerateVirtualPathPoints())
                {
                    return;
                }

                if (_virtualPathPoints.Count > 0)
                {
                    _virtualPathPointsRe = new List<Vector2>(_virtualPathPoints);
                    _virtualPathPointsRe.Reverse();
                    var centerpos = ScreenToWorldCenter(Input.mousePosition);
                    var fpos = DragFromHead ? _virtualPathPoints[0] : _virtualPathPoints[_virtualPathPoints.Count - 1];

                    float segLen = 0;
                    float subStep = _segmentspacing * SubGridHelper.SUB_CELL_SIZE;

                    bool findinpath = false;
                    if (DragFromHead)
                    {
                        for (int i = 1; i < _virtualPathPoints.Count; i++)
                        {
                            if (Vector2.Distance(_virtualPathPoints[i - 1], centerpos) < subStep)
                            {
                                findinpath = true;
                                break;
                            }

                            segLen += Vector2.Distance(_virtualPathPoints[i - 1], _virtualPathPoints[i]);
                        }
                    }
                    else
                    {
                        for (int i = _virtualPathPoints.Count - 1; i >= 0; i--)
                        {
                            if (Vector2.Distance(_virtualPathPoints[i], centerpos) < subStep)
                            {
                                findinpath = true;
                                break;
                            }

                            segLen += Vector2.Distance(_virtualPathPoints[i - 1], _virtualPathPoints[i]);

                        }
                    }

                    if (!findinpath)
                    {
                        segLen = Vector2.Distance(DragFromHead ? _virtualPathPoints[0] : _virtualPathPoints[_virtualPathPoints.Count - 1], centerpos);
                    }


                    _bodySpriteManager.UpdateLineOffset(DragFromHead, segLen);
                }
            }
        }
        // —— 工具与缓存 ——
        // —— 相关辅助（本方法内使用） ——


        bool GenerateBigCellPathWithMouse()
        {
            _cellPathWithMouse.Clear();

            // 工具
            Vector2 CenterOf(Vector2Int cell)
            {
                var c = _grid.CellToWorld(cell);
                return new Vector2(c.x, c.y);
            }
            bool OnSameCenterline(Vector2 a, Vector2 b, Vector2 center, float eps = 1e-3f)
            {
                return (Mathf.Abs(a.x - center.x) <= eps && Mathf.Abs(b.x - center.x) <= eps)
                    || (Mathf.Abs(a.y - center.y) <= eps && Mathf.Abs(b.y - center.y) <= eps);
            }
            void AppendViaCenterIfNeeded(Vector2 from, Vector2 to)
            {
                var fromCell = _grid.WorldToCell(new Vector3(from.x, from.y, 0f));
                var fromCenter = CenterOf(fromCell);
                if (!OnSameCenterline(from, to, fromCenter))
                    _cellPathWithMouse.AddLast(fromCenter);
                _cellPathWithMouse.AddLast(to);
            }

            // 1) 起点：_activeLeadPos
            Vector2 cur = _activeLeadPos;
            _cellPathWithMouse.AddLast(cur);

            // 2) 连接 _cellPathQueue 各格中心
            foreach (var big in _cellPathQueue)
            {
                Vector2 next = CenterOf(big);
                AppendViaCenterIfNeeded(cur, next);
                cur = _cellPathWithMouse.Last.Value;
            }

            // 3) 连接到鼠标中线点
            Vector2 mouse = ScreenToWorldCenter(Input.mousePosition);
            if (_cellPathWithMouse.Count == 0)
                _cellPathWithMouse.AddLast(cur);
            AppendViaCenterIfNeeded(cur, mouse);

            return _cellPathWithMouse.Count >= 2;
        }


        Vector2Int FixActiveLeadPosWithWalkPath(Vector2Int pos)
        {
            Vector2 worldPos = new Vector2(pos.x, pos.y);
            Vector2 fixedPos = FixActiveLeadPosWithWalkPath(worldPos);
            return new Vector2Int(Mathf.RoundToInt(fixedPos.x), Mathf.RoundToInt(fixedPos.y));
        }

        Vector2 FixActiveLeadPosWithWalkPath(Vector2 pos)
        {
            if (_cellPathWithMouse == null || _cellPathWithMouse.Count < 2)
                return pos;

            // 寻找距离点pos最近的线段
            float minDist = float.MaxValue;
            Vector2 closestPoint = pos;
            Vector2 prevPoint = _cellPathWithMouse.First.Value;

            foreach (Vector2 point in _cellPathWithMouse.Skip(1))
            {
                // 判断线段是水平还是垂直的
                bool isHorizontal = Mathf.Abs(point.y - prevPoint.y) < 1e-5f;
                bool isVertical = Mathf.Abs(point.x - prevPoint.x) < 1e-5f;

                if (isHorizontal || isVertical)
                {
                    Vector2 projection;
                    float dist;

                    if (isHorizontal)
                    {
                        // 水平线段，投影保持y值不变，x值在线段范围内
                        float y = prevPoint.y;
                        float x = Mathf.Clamp(pos.x, Mathf.Min(prevPoint.x, point.x), Mathf.Max(prevPoint.x, point.x));
                        projection = new Vector2(x, y);
                        dist = Mathf.Abs(pos.y - y); // y轴距离
                    }
                    else // isVertical
                    {
                        // 垂直线段，投影保持x值不变，y值在线段范围内
                        float x = prevPoint.x;
                        float y = Mathf.Clamp(pos.y, Mathf.Min(prevPoint.y, point.y), Mathf.Max(prevPoint.y, point.y));
                        projection = new Vector2(x, y);
                        dist = Mathf.Abs(pos.x - x); // x轴距离
                    }

                    if (dist < minDist)
                    {
                        minDist = dist;
                        closestPoint = projection;
                    }
                }

                prevPoint = point;
            }

            return closestPoint;
        }

        // 计算点到线段的投影点
        private Vector2 ProjectPointOnLineSegment(Vector2 point, Vector2 lineStart, Vector2 lineEnd)
        {
            Vector2 line = lineEnd - lineStart;
            float len = line.magnitude;
            if (len < 1e-5f)
                return lineStart; // 线段退化为点

            Vector2 lineDir = line / len;
            Vector2 pointVector = point - lineStart;
            float dot = Vector2.Dot(pointVector, lineDir);

            // 投影点在线段外
            if (dot <= 0)
                return lineStart;
            if (dot >= len)
                return lineEnd;

            // 投影点在线段上
            return lineStart + lineDir * dot;
        }

        bool GenerateVirtualPathPoints()
        {
            //创建虚拟的可行走路径点，用于预测视觉点刷新
            _virtualPathPoints.Clear();
            _virtualBodyCells = new LinkedList<Vector2Int>(_subBodyCells);

            //插入虚拟路径点的头部
            var nearbig = GetMouseNearestCell();
            if (DragFromHead)
            {
                if (nearbig == GetBodyCellAtIndex(0))
                {
                    var mouseWorld = ScreenToWorld(Input.mousePosition);
                    nearbig = _grid.WorldToCell(mouseWorld);
                }

                if (nearbig == GetBodyCellAtIndex(1))
                {
                    var headcell = GetHeadCell();
                    var candidates = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
                    for (int i = 0; i < candidates.Length; i++)
                    {
                        var newcell = headcell + candidates[i];
                        if (_grid.IsInside(newcell) && newcell != nearbig)
                        {
                            nearbig = newcell;
                            break;
                        }
                    }
                }

                _virtualBodyCells.AddFirst(nearbig);
            }
            else
            {
                if (nearbig == GetBodyCellAtIndex(_subBodyCells.Count - 1))
                {
                    var mouseWorld = ScreenToWorld(Input.mousePosition);
                    nearbig = _grid.WorldToCell(mouseWorld);
                }

                if (nearbig == GetBodyCellAtIndex(_subBodyCells.Count - 2))
                {
                    var tailcell = GetTailCell();
                    var candidates = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
                    for (int i = 0; i < candidates.Length; i++)
                    {
                        var newcell = tailcell + candidates[i];
                        if (_grid.IsInside(newcell) && newcell != nearbig)
                        {
                            nearbig = newcell;
                            break;
                        }
                    }
                }

                _virtualBodyCells.AddLast(nearbig);
            }

            if (Manhattan(nearbig, DragFromHead ? GetHeadCell() : GetTailCell()) != 1)
                return false;

            _GenerateVirtualBodyCells();

            return true;

            //添加尾部
            var lastvirtual = _virtualPathPoints[_virtualPathPoints.Count - 1];

            bool find = false;
            int addcount = 0;
            float subStep = _segmentspacing * SubGridHelper.SUB_CELL_SIZE;
            for (int i = 0; i < _activeLeadPath.Count; i++)
            {
                if (find == false)
                {
                    if (Vector2.Distance(lastvirtual, _activeLeadPath[i]) < subStep)
                    {
                        find = true;
                        //路点过少就不要了
                        if (_activeLeadPath.Count - i < 5)
                        {
                            Debug.Log("GenerateVirtualPathPoints less then break!!");
                            find = false;
                            break;
                        }

                        continue;
                    }
                }


                if (find)
                {
                    _virtualPathPoints.Add(_activeLeadPath[i]);
                    addcount++;
                }
            }

            return true;
            /*
            if(find == false)
            {
                //没有路点就添加预测结尾
                if (DragFromHead)
                {
                    {
                        //添加尾部
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

                            _virtualBodyCells.AddLast(nextBig);
                            break;
                        }
                    }
                }
                else
                {
                    {
                        //添加头部
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

                            _virtualBodyCells.AddFirst(nextHeadBig);
                            break;
                        }
                    }

                }

                _GenerateVirtualBodyCells();
            }



            */
            return true;
        }

        void _GenerateVirtualBodyCells()
        {
            _virtualPathPoints.Clear();

            //离散成路点（每个大格输出5个“中线小格”点 → world）
            // 辅助：方向→边、边→小格入口
            int DeltaToSide(Vector2Int d)
            {
                if (d == new Vector2Int(-1, 0)) return 0; // L
                if (d == new Vector2Int(1, 0)) return 1;  // R
                if (d == new Vector2Int(0, -1)) return 2; // D
                if (d == new Vector2Int(0, 1)) return 3;  // U
                return -1;
            }
            int Opposite(int side)
            {
                if (side == 0) return 1;
                if (side == 1) return 0;
                if (side == 2) return 3;
                if (side == 3) return 2;
                return -1;
            }
            Vector2Int SideToEntrySub(Vector2Int bigCell, int side)
            {
                switch (side)
                {
                    case 0: return SubGridHelper.BigCellToLeftSubCell(bigCell);
                    case 1: return SubGridHelper.BigCellToRightSubCell(bigCell);
                    case 2: return SubGridHelper.BigCellToBottomSubCell(bigCell);
                    case 3: return SubGridHelper.BigCellToTopSubCell(bigCell);
                    default: return SubGridHelper.BigCellToCenterSubCell(bigCell);
                }
            }

            int n = _virtualBodyCells.Count;
            if (n == 0) return;

            var node = _virtualBodyCells.First;
            for (int index = 0; index < n; index++, node = node.Next)
            {
                var cur = node.Value;

                // 计算入口/出口边
                int entrySide, exitSide;
                if (index == 0)
                {
                    // 首：入口为“指向下一格”的反向；出口为指向下一格
                    var toNext = (node.Next != null) ? (node.Next.Value - cur) : Vector2Int.right;
                    int dirToNext = DeltaToSide(new Vector2Int(Mathf.Clamp(toNext.x, -1, 1), Mathf.Clamp(toNext.y, -1, 1)));
                    exitSide = dirToNext;
                    entrySide = Opposite(exitSide);
                }
                else if (index == n - 1)
                {
                    // 末：入口为来自前一格的反向；出口为其对边（封闭端）
                    var fromPrev = (cur - node.Previous.Value);
                    int dirFromPrev = DeltaToSide(new Vector2Int(Mathf.Clamp(fromPrev.x, -1, 1), Mathf.Clamp(fromPrev.y, -1, 1)));
                    entrySide = Opposite(dirFromPrev);
                    exitSide = Opposite(entrySide);
                }
                else
                {
                    // 中间：入口=来自前一格的反向；出口=指向下一格
                    var fromPrev = (cur - node.Previous.Value);
                    var toNext = (node.Next.Value - cur);
                    int dirFromPrev = DeltaToSide(new Vector2Int(Mathf.Clamp(fromPrev.x, -1, 1), Mathf.Clamp(fromPrev.y, -1, 1)));
                    int dirToNext = DeltaToSide(new Vector2Int(Mathf.Clamp(toNext.x, -1, 1), Mathf.Clamp(toNext.y, -1, 1)));
                    entrySide = Opposite(dirFromPrev);
                    exitSide = dirToNext;
                }

                // 在该大格内生成5个中线小格点：入口→中心→出口
                int need = SubGridHelper.SUB_DIV; // 5
                int count = 0;

                Vector2Int centerSub = SubGridHelper.BigCellToCenterSubCell(cur);
                Vector2Int exitSub = SideToEntrySub(cur, exitSide);
                Vector2Int curSub = SideToEntrySub(cur, entrySide);

                void PushSub(Vector2Int sub)
                {
                    if (count >= need) return;
                    var w = SubGridHelper.SubCellToWorld(sub, _grid);
                    _virtualPathPoints.Add(new Vector2(w.x, w.y));
                    count++;
                }

                // 入口
                PushSub(curSub);

                // 到中心（每步±1）
                while (count < need && curSub != centerSub)
                {
                    if (curSub.x != centerSub.x)
                        curSub = new Vector2Int(curSub.x + (curSub.x < centerSub.x ? 1 : -1), curSub.y);
                    else
                        curSub = new Vector2Int(curSub.x, curSub.y + (curSub.y < centerSub.y ? 1 : -1));
                    PushSub(curSub);
                }

                // 到出口
                while (count < need && curSub != exitSub)
                {
                    if (curSub.x != exitSub.x)
                        curSub = new Vector2Int(curSub.x + (curSub.x < exitSub.x ? 1 : -1), curSub.y);
                    else
                        curSub = new Vector2Int(curSub.x, curSub.y + (curSub.y < exitSub.y ? 1 : -1));
                    PushSub(curSub);
                }

                // 兜底填满5个（极端情况下）
                while (count < need)
                    PushSub(curSub);
            }
        }

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
            Vector2Int endCell = fromHead ? GetHeadSubCell() : GetTailSubCell();
            var w = SubGridHelper.SubCellToWorld(endCell, _grid);
            _activeLeadPos = new Vector2(w.x, w.y);

            // 用整条链表构造“活动端路径历史”（末尾靠近活动端）
            int n = _subBodyCells.Count;
            if (_tmpSubSnap2 == null || _tmpSubSnap2.Length < n) _tmpSubSnap2 = new Vector2Int[Mathf.NextPowerOfTwo(n)];
            for (int i = 0; i < n; i++)
                _tmpSubSnap2[i] = GetBodyCellAtIndex(i);

            if (fromHead)
            {
                // 历史应为：靠尾的点在前，靠头的点在后；末尾一个就是“活动端上一格的中心”
                for (int i = n - 1; i >= 1; i--) _activeLeadPath.Add(SubGridHelper.SubCellToWorld(_tmpSubSnap2[i], _grid));
            }
            else
            {
                // 从尾侧开始：历史为靠头的点在前，靠尾的点在后；末尾一个是“活动端上一格的中心”
                for (int i = 0; i < n - 1; i++) _activeLeadPath.Add(SubGridHelper.SubCellToWorld(_tmpSubSnap2[i], _grid));
            }
        }

        void PruneLeadPathHistory(List<Vector2> path)
        {
            int n = _subBodyCells.Count;
            if (n <= 1 || path.Count == 0) return;

            float need = (n - 1) * _segmentspacing + 3f * _segmentspacing;

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
            int n = _subBodyCells.Count;
            if (n == 0) return;

            EnsureDistBuffer(n);
            for (int i = 0; i < n; i++) _distTargets[i] = i * _segmentspacing;


            // 1) 构建严格中线折线（按小格一步一格，恒等间距 = subStep）
            _centerlinePolyline.Clear();

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
                if (visIndex < _cachedSubRectTransforms.Count)
                {
                    var rt = _cachedSubRectTransforms[visIndex];
                    if (rt != null)
                        rt.anchoredPosition = pos;
                }
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
                _segmentspacing = _grid.CellSize * SubGridHelper.SUB_CELL_SIZE;
                _leadSpeedWorld = _cachedSpeedInput * _segmentspacing;
            }
        }
        void EnsureDistBuffer(int n)
        {
            if (_tmpBodyPos == null || _tmpBodyPos.Length < n) _tmpBodyPos = new Vector2[Mathf.NextPowerOfTwo(n)];
            if (_distTargets == null || _distTargets.Length < n) _distTargets = new float[Mathf.NextPowerOfTwo(n)];
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

        bool CheckIfNeedReverseByCell(Vector2Int nextSubCell, out Vector2Int newNextSubCell)
        {
            newNextSubCell = nextSubCell;

            if (nextSubCell == (DragFromHead ? _currentHeadSubCell : _currentTailSubCell))
                return false;

            if (!_subBodyCells.Contains(nextSubCell))
                return false;


            if (DragFromHead)
            {
                var tail = _currentTailCell;
                var tailprevsub = GetBodyCellAtIndex(_subBodyCells.Count - 2);
                var prevSub = GetBodyCellAtIndex(_subBodyCells.Count - 1 - SubGridHelper.SUB_DIV);
                var prev = SubGridHelper.SubCellToBigCell(prevSub);
                Vector2Int dir = _currentTailSubCell - tailprevsub;
                Vector2Int left = new Vector2Int(-dir.y, dir.x);
                Vector2Int right = new Vector2Int(dir.y, -dir.x);
                var candidates = new[] { dir, left, right };
                for (int i = 0; i < candidates.Length; i++)
                {
                    var nextBig = tail + candidates[i];
                    if (!_grid.IsInside(nextBig)) continue;
                    if (IsPathBlocked(nextBig)) continue;
                    if (!CheckOccupiedBySelfReverse(nextBig)) continue;

                    EnqueueSubCellPath(_currentTailSubCell, SubGridHelper.BigCellToCenterSubCell(nextBig), _cellPathQueue);
                    if (_cellPathQueue.Count > 0)
                    {
                        newNextSubCell = _cellPathQueue.First.Value;
                        return true;
                    }
                }
                
                tail = _currentTailSubCell;
                prev = _subBodyCells.Last.Previous.Value;
                dir = tail - prev;
                left = new Vector2Int(-dir.y, dir.x);
                right = new Vector2Int(dir.y, -dir.x);
                candidates = new[] { dir, left, right };
                for (int i = 0; i < candidates.Length; i++)
                {
                    var nextHeadSub = tail + candidates[i];
                    var nextHeadBig = SubGridHelper.SubCellToBigCell(nextHeadSub);
                    if (!_grid.IsInside(nextHeadBig)) continue;
                    if (IsPathBlocked(nextHeadBig)) continue;
                    if (!CheckOccupiedBySelfReverse(nextHeadBig)) continue;
                    if (!_grid.IsInsideSub(nextHeadSub)) continue;

                    EnqueueSubCellPath(_currentTailSubCell, nextHeadSub, _cellPathQueue);
                    if (_cellPathQueue.Count > 0)
                    {
                        newNextSubCell = _cellPathQueue.First.Value;
                        return true;
                    }
                }
                
                return false;
            }
            else
            {
                var head = _currentHeadCell;
                var headprevsub = GetBodyCellAtIndex(1);
                var nextSub = GetBodyCellAtIndex(SubGridHelper.SUB_DIV);
                var next = SubGridHelper.SubCellToBigCell(nextSub); // 头部相邻的身体
                Vector2Int dir = _currentHeadSubCell - headprevsub; // 远离身体方向
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

                    EnqueueSubCellPath(_currentHeadSubCell, SubGridHelper.BigCellToCenterSubCell(nextHeadBig), _cellPathQueue);

                    if (_cellPathQueue.Count > 0)
                    {
                        newNextSubCell = _cellPathQueue.First.Value;
                        return true;

                    }
                }

                
                //再走小格
                head = _currentHeadSubCell;
                next = _subBodyCells.First.Next.Value; // 头部相邻的身体
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
                    if (!CheckOccupiedBySelfReverse(nextHeadBig)) continue;
                    if (!_grid.IsInsideSub(nextHeadSub)) continue;

                    EnqueueSubCellPath(_currentHeadSubCell, nextHeadSub, _cellPathQueue);

                    if (_cellPathQueue.Count > 0)
                    {
                        newNextSubCell = _cellPathQueue.First.Value;
                        return true;

                    }
                }
                
                
                return false;
            }
            return false;
        }

        bool CheckIfNeedReverseByPos(Vector2 nextPos, out Vector2 newNextPos)
        {
            bool isForward = false;
            float moveDistance = 0;
            newNextPos = nextPos;

            var activeleadpos = _activeLeadPos;
            var leadtargetpos = DragFromHead ? _cachedSubRectTransforms[0].anchoredPosition : _cachedSubRectTransforms[_cachedSubRectTransforms.Count - 1].anchoredPosition;
            var leadnextpos = DragFromHead ? _cachedSubRectTransforms[1].anchoredPosition : _cachedSubRectTransforms[_cachedSubRectTransforms.Count - 2].anchoredPosition;
            const float EPSC = 1e-3f;

            if (activeleadpos == Vector2.zero)
            {
                activeleadpos = leadtargetpos;
            }
            if (Vector2.Distance(nextPos, activeleadpos) < 0.01f)
            {
                return false;
            }

            // 工具：取大格中心
            Vector2 GetCellCenter(Vector2 p)
            {
                var c = _grid.WorldToCell(new Vector3(p.x, p.y, 0f));
                var wc = _grid.CellToWorld(c);
                return new Vector2(wc.x, wc.y);
            }

            // 工具：计算两“中线点”间的中心线距离（跨格则走“中心→中心”曼哈顿；同格不同轴则经中心）
            float CenterlineDistance(Vector2 a, Vector2 b)
            {
                var ca = _grid.WorldToCell(new Vector3(a.x, a.y, 0f));
                var cb = _grid.WorldToCell(new Vector3(b.x, b.y, 0f));
                var AC = _grid.CellToWorld(ca); var BC = _grid.CellToWorld(cb);
                Vector2 aC = new Vector2(AC.x, AC.y), bC = new Vector2(BC.x, BC.y);

                bool aVert = Mathf.Abs(a.x - aC.x) <= EPSC;
                bool aHorz = Mathf.Abs(a.y - aC.y) <= EPSC;
                bool bVert = Mathf.Abs(b.x - bC.x) <= EPSC;
                bool bHorz = Mathf.Abs(b.y - bC.y) <= EPSC;

                if (ca == cb)
                {
                    // 同格：同轴直走；异轴经中心
                    if ((aVert && bVert))
                        return Mathf.Abs(a.y - b.y);
                    if ((aHorz && bHorz))
                        return Mathf.Abs(a.x - b.x);
                    return Mathf.Abs(a.x - aC.x) + Mathf.Abs(a.y - aC.y)
                         + Mathf.Abs(b.x - bC.x) + Mathf.Abs(b.y - bC.y);
                }
                else
                {
                    // 跨格：a→中心A + 中心A→中心B(曼哈顿) + 中心B→b
                    float da = Mathf.Abs(a.x - aC.x) + Mathf.Abs(a.y - aC.y);
                    float db = Mathf.Abs(b.x - bC.x) + Mathf.Abs(b.y - bC.y);
                    float centers = Mathf.Abs(bC.x - aC.x) + Mathf.Abs(bC.y - aC.y);
                    return da + centers + db;
                }
            }

            // 1) 入口中线点（由 leadnextpos -> leadtargetpos 的主轴方向判定）
            Vector2 center = GetCellCenter(leadtargetpos);
            float half = 0.5f * _grid.CellSize;

            Vector2 bodyDir = leadtargetpos - leadnextpos;
            Vector2 entry = center; // 入口点在当前格子的“边中点”
            if (Mathf.Abs(bodyDir.x) >= Mathf.Abs(bodyDir.y))
            {
                // 横向进入：dx>0 从左边进入；dx<0 从右边进入
                entry = new Vector2(center.x + (bodyDir.x > 0 ? -half : +half), center.y);
            }
            else
            {
                // 纵向进入：dy>0 从下边进入；dy<0 从上边进入
                entry = new Vector2(center.x, center.y + (bodyDir.y > 0 ? -half : +half));
            }

            // 2) 入口点到目标/到头部的“沿中线距离”
            float nextDis = CenterlineDistance(entry, nextPos);
            float leadDis = CenterlineDistance(entry, activeleadpos);

            // 3) 前进/后退判定：nextDis > leadDis 为前进，否则后退
            isForward = (nextDis > leadDis);

            // 4) 实际移动距离（沿中线）：leadtargetpos -> nextPos
            moveDistance = CenterlineDistance(leadtargetpos, nextPos);


            //计算倒车点
            if (!isForward)
            {
                Vector2Int newNextCell;
                if (DragFromHead)
                {
                    var tail = GetTailCell();
                    var prev = GetBodyCellAtIndex(_subBodyCells.Count - 2);
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

                        EnqueueBigCellPath(GetTailCell(), nextBig, _cellPathQueue);
                        if (_cellPathQueue.Count > 0)
                        {
                            newNextCell = _cellPathQueue.First.Value;
                            //根据nextDis计算倒车精确点
                            newNextPos = ComputeReverseAdvancePoint(tail, prev, newNextCell, _grid.CellSize - nextDis);
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

                        EnqueueBigCellPath(GetHeadCell(), nextHeadBig, _cellPathQueue);

                        if (_cellPathQueue.Count > 0)
                        {
                            newNextCell = _cellPathQueue.First.Value;
                            //根据nextDis计算倒车精确点
                            newNextPos = ComputeReverseAdvancePoint(head, next, newNextCell, _grid.CellSize - nextDis);
                            return true;

                        }
                    }

                    return false;
                }
            }
            return !isForward;
        }

        // 由倒车端计算：从入口点沿“出口方向”推进 nextDis，返回沿中线的位置（世界坐标）
        // 计算倒车点：从出口点(当前tail格的出口/新格入口)出发，沿入口->中心->出口的方向
        // 在 newNextCell 中沿中线前进 nextDis 的位置（结果限定在 newNextCell 内）
        // 在 newNextCell 内，从入口沿入口->中心->出口的方向前进 nextDis，返回世界坐标
        Vector2 ComputeReverseAdvancePoint(Vector2Int tail, Vector2Int prev, Vector2Int newNextCell, float nextDis)
        {
            const float EPS = 1e-4f;
            float cell = _grid.CellSize;
            float half = 0.5f * cell;

            // 出口方向：tail -> newNextCell 的主轴
            Vector2Int d = new Vector2Int(
                Mathf.Clamp(newNextCell.x - tail.x, -1, 1),
                Mathf.Clamp(newNextCell.y - tail.y, -1, 1)
            );
            Vector2Int outDir = (Mathf.Abs(d.x) >= Mathf.Abs(d.y))
                ? new Vector2Int(d.x >= 0 ? 1 : -1, 0)
                : new Vector2Int(0, d.y >= 0 ? 1 : -1);

            // newNextCell 的中心、入口、出口（严格沿 outDir 中线）
            var nextC3 = _grid.CellToWorld(newNextCell);
            Vector2 nextCenter = new Vector2(nextC3.x, nextC3.y);
            Vector2 newEntry = new Vector2(nextCenter.x - outDir.x * half, nextCenter.y - outDir.y * half); // 面向 tail 的边
            Vector2 newExit = new Vector2(nextCenter.x + outDir.x * half, nextCenter.y + outDir.y * half);

            // 从入口起前进 nextDis（经中心），限定在 [0, cell]
            float dClamp = Mathf.Clamp(nextDis, 0f, cell);
            if (dClamp <= EPS)
            {
                // 轻微推进以确保点处于 newNextCell 内
                return new Vector2(newEntry.x + outDir.x * EPS, newEntry.y + outDir.y * EPS);
            }
            if (dClamp <= half + EPS)
            {
                float t = dClamp / half;
                return Vector2.LerpUnclamped(newEntry, nextCenter, t);
            }
            else
            {
                float remain = dClamp - half; // [0, half]
                float t = remain / half;
                return Vector2.LerpUnclamped(nextCenter, newExit, t);
            }
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
            if (_cachedSubRectTransforms.Count == 0 || _subBodyCells.Count == 0)
                return false;

            Vector2Int curCheckCell = GetHeadCell();
            if (!DragFromHead)
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
            if (!CheckOccupiedBySelfForword(nextCell)) return false;


            return true;
        }

        bool EnqueueSubCellPath(Vector2Int from, Vector2Int to, LinkedList<Vector2Int> pathList, int maxPathCount = -1)
        {
            pathList.Clear();
            if (!_grid.IsValid()) return false;

            // 统一为“中线小格”起止点
            //Vector2Int FromAsSub(Vector2Int v)
            //{
            //    // 若已是合法中线小格，直接用；否则视作大格，取中心小格
            //    if (SubGridHelper.IsValidSubCellEx(v, _grid)) return v;
            //    return SubGridHelper.BigCellToCenterSubCell(v);
            //}
            //Vector2Int ToAsSub(Vector2Int v)
            //{
            //    // 允许 to 越界/非中线：夹紧到最近大格后取中心小格
            //    if (SubGridHelper.IsValidSubCellEx(v, _grid)) return v;
            //    var big = _grid.IsInside(v) ? v : ClampInside(v);
            //    return SubGridHelper.BigCellToCenterSubCell(big);
            //}

            Vector2Int fromSub = from;// FromAsSub(from);
            Vector2Int targetSub = to;// ToAsSub(to);

            if (fromSub == targetSub) return false;

            // 计算“前方”方向（与大格一致）
            Vector2Int preferredDir = Vector2Int.zero;
            if (_subBodyCells != null && _subBodyCells.Count >= 2)
            {
                if (DragFromHead)
                {
                    var head = _currentHeadSubCell;
                    var neck = GetBodyCellAtIndex(1);
                    preferredDir = new Vector2Int(Mathf.Clamp(head.x - neck.x, -1, 1), Mathf.Clamp(head.y - neck.y, -1, 1));
                }
                else
                {
                    var tail = _currentTailSubCell;
                    var preTail = GetBodyCellAtIndex(_subBodyCells.Count - 2);
                    preferredDir = new Vector2Int(Mathf.Clamp(tail.x - preTail.x, -1, 1), Mathf.Clamp(tail.y - preTail.y, -1, 1));
                }
            }
            if (preferredDir == Vector2Int.zero)
            {
                // 用“子格到目标”的大方向（轴向符号即可）
                var diff = targetSub - fromSub;
                preferredDir = new Vector2Int(Mathf.Clamp(diff.x, -1, 1), Mathf.Clamp(diff.y, -1, 1));
            }

            // A*（在子格上，但仅允许“中线小格”）
            var open = new List<Vector2Int>(64);
            var openSet = new HashSet<Vector2Int>();
            var closed = new HashSet<Vector2Int>();
            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            var gScore = new Dictionary<Vector2Int, float>();
            var fScore = new Dictionary<Vector2Int, float>();

            int Heuristic(Vector2Int a, Vector2Int b) => SubGridHelper.SubCellManhattan(a, b);

            open.Add(fromSub);
            openSet.Add(fromSub);
            gScore[fromSub] = 0f;
            fScore[fromSub] = Heuristic(fromSub, targetSub);

            // 回退节点：记录离目标最近（h最小）、同等h时g更小的节点
            Vector2Int bestNode = fromSub;
            int bestH = Heuristic(fromSub, targetSub);
            float bestG = 0f;

            // 4邻域（单步子格 ±1）
            Vector2Int[] dirs = new Vector2Int[4]
            {
        new Vector2Int( 1,  0),
        new Vector2Int(-1,  0),
        new Vector2Int( 0,  1),
        new Vector2Int( 0, -1),
            };

            while (open.Count > 0)
            {
                // 取 f 最小
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

                // 更新“最接近目标”的可达节点
                {
                    int hcur = Heuristic(current, targetSub);
                    float gcur = gScore.TryGetValue(current, out var gv) ? gv : float.PositiveInfinity;
                    if (hcur < bestH || (hcur == bestH && gcur < bestG))
                    {
                        bestH = hcur;
                        bestG = gcur;
                        bestNode = current;
                    }
                }

                if (current == targetSub)
                {
                    // 回溯路径（子格路径）
                    var rev = new List<Vector2Int>(64);
                    var c = current;
                    while (!c.Equals(fromSub))
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

                if (!gScore.TryGetValue(current, out var curG)) curG = float.PositiveInfinity;
                closed.Add(current);

                for (int i = 0; i < 4; i++)
                {
                    var step = dirs[i];
                    var nb = new Vector2Int(current.x + step.x, current.y + step.y);

                    // 仅允许中线小格
                    if (!SubGridHelper.IsValidSubCellEx(nb, _grid)) continue;

                    // 映射到大格做边界/阻挡/他蛇占用校验
                    var nbBig = SubGridHelper.SubCellToBigCell(nb);
                    if (!_grid.IsInside(nbBig)) continue;
                    if (IsPathBlocked(nbBig)) continue;
                    //寻路时只允许进入拖拽端后一格
                    if((nb != _subBodyCells.First.Next.Value || nb != _subBodyCells.Last.Previous.Value) &&
                            (nbBig != _currentHeadCell && nbBig != _currentTailCell &&
                            nbBig != GetHeadNextBigCell() && nbBig != GetTailNextBigCell()))
                    {
                        if (SnakeManager.Instance.IsCellOccupiedBySelfSnakes(nbBig, this)) continue;
                    }
                    if (SnakeManager.Instance.IsCellOccupiedByOtherSnakes(nbBig, this)) continue;

                    if (closed.Contains(nb)) continue;

                    // 方向罚分（沿用大格策略）
                    float stepPenalty = 0f;
                    if (preferredDir != Vector2Int.zero)
                    {
                        // 将子格步长映射为轴向方向（±1,0 / 0,±1）
                        Vector2Int stepDir = step;

                        // 非前进基础罚分
                        if (stepDir != preferredDir) stepPenalty += AStarDirectionBias;

                        // 后退
                        if (stepDir.x == -preferredDir.x && stepDir.y == -preferredDir.y)
                        {
                            stepPenalty += AStarBackwardPenalty;
                        }
                        else
                        {
                            // 左/右转（与 preferredDir 正交）
                            int dot = stepDir.x * preferredDir.x + stepDir.y * preferredDir.y;
                            if (dot == 0)
                            {
                                int cross = preferredDir.x * stepDir.y - preferredDir.y * stepDir.x;
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

                        // 强偏好：f = g + h + stepPenalty * 2
                        float h = Heuristic(nb, targetSub);
                        fScore[nb] = tentativeG + h + stepPenalty * 10f;
                    }
                }
            }

            // 无法到达：回退最近可达节点
            if (bestNode != fromSub && cameFrom.ContainsKey(bestNode))
            {
                var rev = new List<Vector2Int>(64);
                var c = bestNode;
                while (!c.Equals(fromSub))
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

        bool EnqueueBigCellPath(Vector2Int from, Vector2Int to, LinkedList<Vector2Int> pathList, int maxPathCount = -1)
        {
            pathList.Clear();
            if (from == to) return false;
            if (!_grid.IsValid()) return false;
            if (!_grid.IsInside(from)) return false;

            // 允许 to 越界，夹紧到边缘
            var target = ClampInside(to);

            // 计算“前方”方向：拖动段后面一格 → 拖动段
            Vector2Int preferredDir = Vector2Int.zero;
            if (_subBodyCells != null && _subBodyCells.Count >= 2)
            {
                if (DragFromHead)
                {
                    // next(后面一格) -> head(拖动段)
                    var head = _currentHeadCell;
                    var neck = GetBodyCellAtIndex(1);
                    preferredDir = new Vector2Int(Mathf.Clamp(head.x - neck.x, -1, 1), Mathf.Clamp(head.y - neck.y, -1, 1));
                }
                else
                {
                    // preTail(后面一格) -> tail(拖动段)
                    var tail = _currentTailCell;
                    var preTail = GetBodyCellAtIndex(_subBodyCells.Count - 2);
                    preferredDir = new Vector2Int(Mathf.Clamp(tail.x - preTail.x, -1, 1), Mathf.Clamp(tail.y - preTail.y, -1, 1));
                }
            }
            // 只有一段时，退化为朝向目标的方向
            if (preferredDir == Vector2Int.zero)
            {
                preferredDir = new Vector2Int(Mathf.Clamp(target.x - from.x, -1, 1), Mathf.Clamp(target.y - from.y, -1, 1));
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
            Vector2Int[] dirs = new Vector2Int[4]
            {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1)
            };

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


        bool AdvanceHeadToCellCoConsume(Vector2Int nextCell)
        {
            if (_subBodyCells == null || _subBodyCells.Count == 0) return true;

            var newHeadCell = nextCell;

            if (_subBodyCells.Count == 1)
            {
                _subBodyCells.First.Value = newHeadCell;
            }
            else
            {
                // 整条蛇朝尾部方向移动：在尾部添加新位置，移除头部
                _subBodyCells.AddFirst(newHeadCell);
                _subBodyCells.RemoveLast();

            }


            // 5) 刷新缓存
            _currentHeadSubCell = _subBodyCells.First.Value;
            _currentTailSubCell = _subBodyCells.Last.Value;
            _currentHeadCell = SubGridHelper.SubCellToBigCell(_currentHeadSubCell);
            _currentTailCell = SubGridHelper.SubCellToBigCell(_currentTailSubCell);
            UpdateCachedRectTransformsFromBodyCells();

            return true;
        }


        private Vector2Int GetBodyCellAtIndex(int index)
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
        /// 根据_bodyCells更新_cachedRectTransforms
        /// </summary>
        private void UpdateCachedRectTransformsFromBodyCells()
        {
            if (_cachedSubRectTransforms.Count == 0 || _subBodyCells.Count == 0)
                return;

            // 遍历身体节点和对应的RectTransform
            var bodycelllist = _subBodyCells.ToList();
            for (int segmentIndex = 0; segmentIndex < _subBodyCells.Count; segmentIndex++)
            {
                var rt = _cachedSubRectTransforms[segmentIndex];
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
            if (_cachedSubRectTransforms.Count == 0 || _subBodyCells.Count == 0)
                return;

            // 遍历身体节点和对应的RectTransform
            _subBodyCells.Clear();
            for (int segmentIndex = 0; segmentIndex < _cachedSubRectTransforms.Count; segmentIndex++)
            {
                var rt = _cachedSubRectTransforms[segmentIndex];
                if (rt != null)
                {
                    _subBodyCells.AddLast(SubGridHelper.WorldToSubCell(rt.anchoredPosition, _grid));
                }

            }

        }


        bool AdvanceTailToCellCoConsume(Vector2Int nextCell)
        {
            if (_subBodyCells == null || _subBodyCells.Count == 0) return true;

            var newHeadCell = nextCell;

            if (_subBodyCells.Count == 1)
            {
                _subBodyCells.First.Value = newHeadCell;
            }
            else
            {
                // 整条蛇朝尾部方向移动：在尾部添加新位置，移除头部
                _subBodyCells.AddLast(newHeadCell);
                _subBodyCells.RemoveFirst();

            }


            // 5) 刷新缓存
            _currentHeadSubCell = _subBodyCells.First.Value;
            _currentTailSubCell = _subBodyCells.Last.Value;
            _currentHeadCell = SubGridHelper.SubCellToBigCell(_currentHeadSubCell);
            _currentTailCell = SubGridHelper.SubCellToBigCell(_currentTailSubCell);
            UpdateCachedRectTransformsFromBodyCells();

            return true;
        }

        public GridConfig GetGrid()
        {
            return _grid;
        }


        public override void SnapCellsToGrid()
        {
            if (_cachedSubRectTransforms == null)
                return;
            if (_cachedSubRectTransforms.Count == 0)
                return;


            _lastIsSingleCellPath = false;
            _switchToSingleCellPath = false;
            _leadLastTargetCell = Vector2Int.zero;
            _leadLastTargetPos = Vector2.zero;
            _leadReversePos = Vector2.zero;
            _leadTargetPos = Vector2.zero;

            _activeLeadPos = Vector2.zero;
            _smoothInited = false;

            Vector2Int[] newInitialBodyCells = new Vector2Int[Length];
            if (DragFromHead)
            {
                int segmentIndex = 0;
                for (int i = 0; i < _subBodyCells.Count;)
                {
                    if (i >= _subBodyCells.Count) break;
                    var bigcell = SubGridHelper.SubCellToBigCell(GetBodyCellAtIndex(i));
                    newInitialBodyCells[segmentIndex] = bigcell;

                    i += SubGridHelper.SUB_DIV;
                    segmentIndex++;
                }
            }
            else
            {
                int segmentIndex = Length - 1;
                for (int i = _subBodyCells.Count - 1; i >= 0;)
                {
                    if (i < 0) break;
                    var bigcell = SubGridHelper.SubCellToBigCell(GetBodyCellAtIndex(i));
                    newInitialBodyCells[segmentIndex] = bigcell;

                    i -= SubGridHelper.SUB_DIV;
                    segmentIndex--;
                }
            }


            InitializeSubSegmentPositions(newInitialBodyCells);
            UpdateCachedRectTransformsFromBodyCells();
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

            foreach (var gameObject in _subSegments)
            {
                if (gameObject != null)
                    allegments.AddLast(gameObject);
            }

            // 1) 确定“活动端”：使用参数 fromHead
            bool activeFromHead = fromHead;

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

                float step = _leadSpeedWorld * 0.5f * Time.deltaTime;
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


                // 3) 直接用中线折线更新LineRenderer
                if (EnableBodySpriteManagement && _bodySpriteManager != null)
                {
                    _bodySpriteManager.UpdateLineFromPolyline(
                        _centerlinePolyline,
                        _cachedSubRectTransforms.Count,
                        _segmentspacing,
                        activeFromHead
                    );
                }

                // 同步渲染折线（身体Sprite）
                if (EnableBodySpriteManagement && _bodySpriteManager != null)
                {
                    _bodySpriteManager.UpdateLineFromPolyline(
                        _centerlinePolyline,
                        _cachedSubRectTransforms.Count,
                        _segmentspacing,
                        activeFromHead
                    );
                }

                yield return null; // 逐帧推进
            }

            // 5) 到达洞中心后，触发吞噬动画（逐帧推进，不阻塞）
            if (EnableBodySpriteManagement && _bodySpriteManager != null)
            {
                float allConsumeTime = hole.ConsumeInterval * Mathf.Max(1, _subBodyCells.Count);
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
            _subBodyCells.Clear();
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

        float ScreenToWorldCellBottom(Vector3 screen, Vector2Int dir, bool isX)
        {
            float offset = 0;
            var mouseWorld = ScreenToWorldCenter(screen);


            // 1) 屏幕 → 世界
            var world = ScreenToWorld(screen);
            var bigCell = SubGridHelper.WorldToBigCell(world, _grid);
            var bigCenter = _grid.CellToWorld(bigCell);
            float half = 0.5f * _grid.CellSize;

            if (isX)
            {
                if (dir.x < 0)
                {
                    offset = mouseWorld.x - bigCenter.x + half;
                }
                else
                {
                    offset = bigCenter.x - mouseWorld.x + half;

                }
            }
            else
            {
                if (dir.y < 0)
                {
                    offset = mouseWorld.y - bigCenter.y + half;
                }
                else
                {
                    offset = bigCenter.y - mouseWorld.y + half;
                }
            }

            return offset;
        }

        Vector3 GetCellCenterOffset(Vector3 world)
        {
            var bigCell = SubGridHelper.WorldToBigCell(world, _grid);
            var bigCenter = _grid.CellToWorld(bigCell);

            Vector3 off = world - bigCenter;
            return off;
        }

        Vector3 ScreenToWorldCenter(Vector3 screen)
        {
            // 1) 屏幕 → 世界
            var world = ScreenToWorld(screen);
            if (_grid.Width == 0 || _grid.Height == 0) return world;

            return WorldToWorldCenter(world);
        }

        Vector3 WorldToWorldCenter(Vector3 world)
        {
            // 2) 世界 → 夹紧到有效大格
            var bigCell = SubGridHelper.WorldToBigCell(world, _grid);
            var bigCenter = _grid.CellToWorld(bigCell);

            // 3) 吸附到该大格的最近“中线”，但沿中线方向不量化到小格中心
            float half = 0.5f * _grid.CellSize;
            Vector3 off = world - bigCenter;

            // 先把偏移限制在当前大格范围内
            float ox = Mathf.Clamp(off.x, -half, half);
            float oy = Mathf.Clamp(off.y, -half, half);

            // 选择更近的中线：竖直中线(x=0)或水平中线(y=0)
            if (Mathf.Abs(ox) <= Mathf.Abs(oy))
            {
                // 竖直中线：x=0，y保持（已夹紧）
                ox = 0f;
            }
            else
            {
                // 水平中线：y=0，x保持（已夹紧）
                oy = 0f;
            }

            return new Vector3(bigCenter.x + ox, bigCenter.y + oy, 0f);
        }

        /// <summary>
        /// 获取鼠标位置所在的格子，如果更接近边缘则返回相邻格子
        /// </summary>
        /// <returns>鼠标所在或更接近的格子坐标（已确保在网格范围内）</returns>
        Vector2Int GetMouseNearestCell()
        {
            // 1. 获取鼠标在网格中的世界坐标
            var mouseWorld = ScreenToWorld(Input.mousePosition);
            if (_grid.Width == 0 || _grid.Height == 0) return Vector2Int.zero;

            // 2. 获取鼠标所在的格子
            var currentCell = _grid.WorldToCell(mouseWorld);

            // 确保在网格范围内
            currentCell.x = Mathf.Clamp(currentCell.x, 0, _grid.Width - 1);
            currentCell.y = Mathf.Clamp(currentCell.y, 0, _grid.Height - 1);

            // 3. 计算鼠标在当前格子内的相对位置（-0.5到0.5范围）
            var cellCenter = _grid.CellToWorld(currentCell);
            float relX = (mouseWorld.x - cellCenter.x) / _grid.CellSize;
            float relY = (mouseWorld.y - cellCenter.y) / _grid.CellSize;

            // 4. 判断是否靠近边缘（阈值设为0.3，可根据需要调整）
            const float threshold = 0f;
            Vector2Int nearestCell = currentCell;

            // 5. 根据相对位置判断更接近哪个边缘
            if (Mathf.Abs(relX) > Mathf.Abs(relY))
            {
                // 更接近左右边缘
                if (relX > threshold)
                {
                    // 更接近右边缘
                    nearestCell.x = Mathf.Min(currentCell.x + 1, _grid.Width - 1);
                }
                else if (relX < -threshold)
                {
                    // 更接近左边缘
                    nearestCell.x = Mathf.Max(currentCell.x - 1, 0);
                }
            }
            else
            {
                // 更接近上下边缘
                if (relY > threshold)
                {
                    // 更接近上边缘
                    nearestCell.y = Mathf.Min(currentCell.y + 1, _grid.Height - 1);
                }
                else if (relY < -threshold)
                {
                    // 更接近下边缘
                    nearestCell.y = Mathf.Max(currentCell.y - 1, 0);
                }
            }

            return nearestCell;
        }

        Vector2Int GetMouseNearestCellReverse()
        {
            // 1. 获取鼠标在网格中的世界坐标
            var mouseWorld = ScreenToWorld(Input.mousePosition);
            if (_grid.Width == 0 || _grid.Height == 0) return Vector2Int.zero;

            // 2. 获取鼠标所在的格子
            var currentCell = _grid.WorldToCell(mouseWorld);

            // 确保在网格范围内
            currentCell.x = Mathf.Clamp(currentCell.x, 0, _grid.Width - 1);
            currentCell.y = Mathf.Clamp(currentCell.y, 0, _grid.Height - 1);

            // 3. 计算鼠标在当前格子内的相对位置（-0.5到0.5范围）
            var cellCenter = _grid.CellToWorld(currentCell);
            float relX = (mouseWorld.x - cellCenter.x) / _grid.CellSize;
            float relY = (mouseWorld.y - cellCenter.y) / _grid.CellSize;

            // 4. 判断是否靠近边缘（阈值设为0.3，可根据需要调整）
            const float threshold = 0f;
            Vector2Int nearestCell = currentCell;

            // 5. 根据相对位置判断更接近哪个边缘
            if (Mathf.Abs(relX) > Mathf.Abs(relY))
            {
                // 更接近左右边缘
                if (relX > threshold)
                {
                    // 更接近右边缘
                    nearestCell.x = Mathf.Max(currentCell.x - 1, 0);
                }
                else if (relX < -threshold)
                {
                    // 更接近左边缘
                    nearestCell.x = Mathf.Min(currentCell.x + 1, _grid.Width - 1);
                }
            }
            else
            {
                // 更接近上下边缘
                if (relY > threshold)
                {
                    // 更接近上边缘
                    nearestCell.y = Mathf.Max(currentCell.y - 1, 0);
                }
                else if (relY < -threshold)
                {
                    // 更接近下边缘
                    nearestCell.y = Mathf.Min(currentCell.y + 1, _grid.Height - 1);
                }
            }

            return nearestCell;
        }
        /// <summary>
        /// 获取鼠标位置所在的格子，如果更接近边缘则返回相邻格子，同时返回相对位置信息
        /// </summary>
        /// <param name="relativePosition">输出参数：鼠标在格子内的相对位置（-0.5到0.5范围）</param>
        /// <returns>鼠标所在或更接近的格子坐标（已确保在网格范围内）</returns>
        Vector2Int GetMouseNearestCell(out Vector2 relativePosition)
        {
            // 1. 获取鼠标在网格中的世界坐标
            var mouseWorld = ScreenToWorld(Input.mousePosition);
            if (_grid.Width == 0 || _grid.Height == 0)
            {
                relativePosition = Vector2.zero;
                return Vector2Int.zero;
            }

            // 2. 获取鼠标所在的格子
            var currentCell = _grid.WorldToCell(mouseWorld);

            // 确保在网格范围内
            currentCell.x = Mathf.Clamp(currentCell.x, 0, _grid.Width - 1);
            currentCell.y = Mathf.Clamp(currentCell.y, 0, _grid.Height - 1);

            // 3. 计算鼠标在当前格子内的相对位置（-0.5到0.5范围）
            var cellCenter = _grid.CellToWorld(currentCell);
            float relX = (mouseWorld.x - cellCenter.x) / _grid.CellSize;
            float relY = (mouseWorld.y - cellCenter.y) / _grid.CellSize;
            relativePosition = new Vector2(relX, relY);

            // 4. 判断是否靠近边缘（阈值设为0.3，可根据需要调整）
            const float threshold = 0.3f;
            Vector2Int nearestCell = currentCell;

            // 5. 根据相对位置判断更接近哪个边缘
            if (Mathf.Abs(relX) > Mathf.Abs(relY))
            {
                // 更接近左右边缘
                if (relX > threshold)
                {
                    // 更接近右边缘
                    nearestCell.x = Mathf.Min(currentCell.x + 1, _grid.Width - 1);
                }
                else if (relX < -threshold)
                {
                    // 更接近左边缘
                    nearestCell.x = Mathf.Max(currentCell.x - 1, 0);
                }
            }
            else
            {
                // 更接近上下边缘
                if (relY > threshold)
                {
                    // 更接近上边缘
                    nearestCell.y = Mathf.Min(currentCell.y + 1, _grid.Height - 1);
                }
                else if (relY < -threshold)
                {
                    // 更接近下边缘
                    nearestCell.y = Mathf.Max(currentCell.y - 1, 0);
                }
            }

            return nearestCell;
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

            // 追加：绘制 _cellPathWithMouse
            if (DebugShowBigCellPath && _cellPathQueue != null && _cellPathQueue.Count > 0)
            {
                Debug.Log(_cellPathQueue.Count);
                var prev = GUI.color;
                // 把折线的“网格局部坐标”转成屏幕坐标后绘制
                int n = _cellPathQueue.Count;
                var cam = GetComponentInParent<Canvas>()?.worldCamera;
                // 先画中间点（统一颜色）
                GUI.color = DebugPolylineColor;
                for (int i = 0; i < n; i++)
                {
                    var pLocal = _cellPathQueue.ElementAt(i);
                    Vector3 wp = container.TransformPoint(new Vector3(pLocal.x, pLocal.y, 0f));
                    Vector2 scr = RectTransformUtility.WorldToScreenPoint(cam, wp);
                    float px = scr.x;
                    float py = Screen.height - scr.y;
                    GUI.DrawTexture(new Rect(px - DebugPolylinePointSize, py - DebugPolylinePointSize,
                        2f * DebugPolylinePointSize, 2f * DebugPolylinePointSize), Texture2D.whiteTexture);
                }

                // 头点（折线开头）
                {
                    var headLocal = _cellPathQueue.First.Value;
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
                    var tailLocal = _cellPathQueue.Last.Value;
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


            // 追加：绘制 _virtualPathPoints
            if (DebugShowVirtualPath && _activeLeadPath != null && _activeLeadPath.Count > 0)
            {
                var prev = GUI.color;
                // 把折线的“网格局部坐标”转成屏幕坐标后绘制
                int n = _activeLeadPath.Count;
                var cam = GetComponentInParent<Canvas>()?.worldCamera;
                // 先画中间点（统一颜色）
                GUI.color = DebugPolylineColor;
                for (int i = 0; i < n; i++)
                {
                    var pLocal = _activeLeadPath[i];
                    Vector3 wp = container.TransformPoint(new Vector3(pLocal.x, pLocal.y, 0f));
                    Vector2 scr = RectTransformUtility.WorldToScreenPoint(cam, wp);
                    float px = scr.x;
                    float py = Screen.height - scr.y;
                    GUI.DrawTexture(new Rect(px - DebugPolylinePointSize, py - DebugPolylinePointSize,
                        2f * DebugPolylinePointSize, 2f * DebugPolylinePointSize), Texture2D.whiteTexture);
                }

                // 头点（折线开头）
                {
                    var headLocal = _activeLeadPath[0];
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
                    var tailLocal = _activeLeadPath[n - 1];
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


            // 追加：绘制 _centerlinePolyline
            if (DebugShowPolyline && _centerlinePolyline != null && _centerlinePolyline.Count > 0)
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

            //绘制倒车点
            //if (_leadReversePos != Vector2.zero)
            //{
            //    var prev = GUI.color;
            //
            //    var cam = GetComponentInParent<Canvas>()?.worldCamera;
            //    var tailLocal = _leadReversePos;
            //    Vector3 wp = container.TransformPoint(new Vector3(tailLocal.x, tailLocal.y, 0f));
            //    Vector2 scr = RectTransformUtility.WorldToScreenPoint(cam, wp);
            //    float px = scr.x;
            //    float py = Screen.height - scr.y;
            //    GUI.color = Color.blue;
            //    GUI.DrawTexture(new Rect(px - DebugPolylinePointSize * 1.5f, py - DebugPolylinePointSize * 1.5f,
            //        3f * DebugPolylinePointSize, 3f * DebugPolylinePointSize), Texture2D.whiteTexture);
            //
            //    GUI.color = prev;
            //}


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
        }

        /// <summary>
        /// 获取蛇尾的格子位置
        /// </summary>
        public Vector2Int GetTailCell()
        {

            return _currentTailCell;
        }


        /// <summary>
        /// 获取蛇头的格子位置
        /// </summary>
        public Vector2Int GetHeadSubCell()
        {
            return _currentHeadSubCell;
        }

        /// <summary>
        /// 获取蛇尾的格子位置
        /// </summary>
        public Vector2Int GetTailSubCell()
        {

            return _currentTailSubCell;
        }

        public Vector2Int GetHeadNextBigCell()
        {
            var nextsub = GetBodyCellAtIndex(SubGridHelper.SUB_DIV);
            return SubGridHelper.SubCellToBigCell(nextsub);
        }
        public Vector2Int GetTailNextBigCell()
        {
            var nextsub = GetBodyCellAtIndex(_subBodyCells.Count - 1 - SubGridHelper.SUB_DIV);
            return SubGridHelper.SubCellToBigCell(nextsub);
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



        protected override void OnDestroy()
        {
            base.OnDestroy();
        }
    }
}


