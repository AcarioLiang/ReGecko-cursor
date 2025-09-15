using System.Collections.Generic;
using UnityEngine;
using ReGecko.GridSystem;

namespace ReGecko.SnakeSystem
{
    public class SnakeBodySpriteManager : MonoBehaviour
    {
        [Header("Line Settings")]
        public Material BodyLineMaterial;
        public float LineWidth = 1.0f;
        public Color BodyColor = Color.white;
        public bool EnableTiledTexture = true;
        //吞噬动画相关
        float _allConsumeTime = 0f;
        bool _consumeFromHead = false;
        float _consumeSpeed = 0f;              // 路径总长 / 总时长
        Vector3 _consumeAnchor = Vector3.zero; // 锚点（不动不隐藏）
        bool _consumeInited = false;
        float _consumeElapsed = 0f;

        // 路径缓存（严格夹紧于启动瞬间的折线）
        Vector3[] _consumePathCache;   // 顺序：从锚点到末端
        float[] _consumePrefixLen;     // 前缀弧长（长度 = _consumePathCount）
        int _consumePathCount = 0;

        // 原始索引 -> 在缓存路径中的弧长位置 s0
        float[] _consumeInitialS;      // 长度 = _linePositionsCount
        int _consumeAnchorOriginalIndex = 0;

        SnakeController _snake;
        GridConfig _grid;
        LineRenderer _line;
        readonly List<Vector3> _posBuffer = new List<Vector3>(256);
        List<Vector2> _cacheGridLocalPath = new List<Vector2>();
        bool _isInitGridLocalPath = false;

        readonly List<GameObject> _subSegments = new List<GameObject>();// 每段的5个小段
        readonly Queue<GameObject> _subSegmentPool = new Queue<GameObject>();
        readonly LinkedList<Vector2Int> _subBodyCells = new LinkedList<Vector2Int>(); // 离散身体占用格，头在First
        readonly List<RectTransform> _cachedSubRectTransforms = new List<RectTransform>();

        private Vector2Int _currentHeadSubCell;
        private Vector2Int _currentTailSubCell;

        // 新增：折线缓存，避免每帧ToArray分配
        Vector3[] _linePositionsCache;
        int _linePositionsCount;

        // 折线清洗参数
        [SerializeField] float PolylineDedupEps = 1e-4f;       // 同一点容差
        [SerializeField] float PolylineMinLoopEraseFactor = 0.35f; // 局部回环长度阈值系数(乘以 segmentSpacing)
        readonly List<Vector2> _polylineCleanBuffer = new List<Vector2>(512);
        readonly List<long> _polylineKeyBuffer = new List<long>(512);

        void Awake()
        {
            _snake = GetComponentInParent<SnakeController>();
            if (_snake == null) _snake = GetComponent<SnakeController>();
        }

        void Start()
        {
            StartCoroutine(InitializeAfterSnakeBuilt());
        }

        System.Collections.IEnumerator InitializeAfterSnakeBuilt()
        {
            yield return new WaitForEndOfFrame();

            if (_snake == null) yield break;
            _grid = _snake.GetGrid();
            BodyColor = _snake.BodyColor;
            LineWidth = _grid.CellSize * 0.8f;

            EnsureLineCreated();
            RecreateSubSegments();
            UpdateAllLinePositions();
        }


        public void OnSnakeLengthChanged()
        {
            EnsureLineCreated();
            UpdateAllLinePositions();
        }

        public Vector3 GetLineLeadFirstPos(bool fromHead)
        {
            if (_line == null) return Vector3.zero;
            if (_linePositionsCache == null || _linePositionsCache.Length == 0) return Vector3.zero;

            if (fromHead)
            {
                return _linePositionsCache[0];
            }
            else
            {
                return _linePositionsCache[_linePositionsCache.Length - 1];
            }
        }

        void EnsureLineCreated()
        {
            if (_line != null) return;

            var go = new GameObject("BodyLine");
            go.transform.SetParent(transform, false);

            _line = go.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.alignment = LineAlignment.View;
            _line.textureMode = LineTextureMode.Stretch;
            _line.numCornerVertices = 0;
            _line.numCapVertices = 0;

            _line.widthMultiplier = LineWidth;

            if (BodyLineMaterial != null)
            {
                // 独立材质实例，避免共享材质被改动
                _line.material = new Material(BodyLineMaterial);
                _line.material.color = BodyColor;

                // _Borders = (左边框, 右边框, 头部高度, 尾部高度)
                Vector4 borders = new Vector4(5, 5, 108, 107);
                // 设置九宫格边框参数（像素单位）
                // 假设纹理是 64x64，边框各为 16 像素
                _line.material.SetVector("_Borders", borders);

                // 临时启用调试模式来验证分区
                //_line.material.SetFloat("_DebugMode", 2f);


                // 调试输出
                Debug.Log($"Texture size: {_line.material.mainTexture.width}x{_line.material.mainTexture.height}");
                Debug.Log($"Borders set to: {borders}");
                Debug.Log($"TexelSize: {_line.material.GetVector("_MainTex_TexelSize")}");

                Debug.Log($"LineRenderer textureMode: {_line.textureMode}");
                Debug.Log($"LineRenderer material: {_line.material.name}");
            }

            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(BodyColor, 0f), new GradientColorKey(BodyColor, 1f) },
                new[] { new GradientAlphaKey(BodyColor.a, 0f), new GradientAlphaKey(BodyColor.a, 1f) }
            );
            //_line.colorGradient = grad;

            var r = _line.GetComponent<Renderer>();
            r.sortingLayerName = "Default"; // 或你的 UI Sorting Layer
            r.sortingOrder = 100;

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
            go.transform.SetParent(_snake.transform);

            var rt = go.AddComponent<RectTransform>();
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


            _cachedSubRectTransforms.Clear();
            RectTransform rt = null;
            for (int segmentIndex = 0; segmentIndex < Mathf.Max(1, _snake.GetSegments().Count); segmentIndex++)
            {
                var subSegmentList = new List<GameObject>();
                for (int i = 0; i < SubGridHelper.SUB_DIV; i++)
                {
                    var subSegment = GetSubSegmentFromPool();
                    subSegment.name = ($"SubSegment_{segmentIndex}_{i}");
                    subSegmentList.Add(subSegment);
                    rt = subSegment.GetComponent<RectTransform>();
                    _cachedSubRectTransforms.Add(rt);
                    _subSegments.Add(subSegment);
                }
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

            _currentHeadSubCell = _subBodyCells.First.Value;
            _currentTailSubCell = _subBodyCells.Last.Value;
        }


        /// <summary>
        /// 更新子段位置（由SnakeController调用）
        /// </summary>
        /// <param name="segmentIndex">身体节点索引</param>
        /// <param name="subCellPositions">5个子段的小格坐标</param>
        public void UpdateSubSegmentPositions(int segmentIndex, Vector2Int[] subCellPositions)
        {

            var grid = _snake.GetGrid();

            for (int i = 0; i < subCellPositions.Length; i++)
            {
                int curSubIndex = segmentIndex * SubGridHelper.SUB_DIV + i;
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

        void UpdateAllLinePositions()
        {
            if (_snake == null || _grid.Width == 0) return;

            var bodys = _snake.GetBodyCells();
            if (bodys == null || bodys.Count == 0)
            {
                if (_line != null) _line.gameObject.SetActive(false);
                return;
            }

            EnsureLineCreated();
            _posBuffer.Clear();

            Vector2Int[] bodyArray = new Vector2Int[bodys.Count];
            bodys.CopyTo(bodyArray, 0);

            InitializeSubSegmentPositions(bodyArray);

            //头部一个点，身体，尾部一个点
            EqualizeHeadAndTail(_subSegments);

            foreach (var node in _subSegments)
            {
                var p = node.transform.position;
                p.z = 0f;
                _posBuffer.Add(p);
            }

            if (_posBuffer.Count < 2)
            {
                _line.gameObject.SetActive(false);
                return;
            }

            _linePositionsCount = _posBuffer.Count;
            _linePositionsCache = _posBuffer.ToArray();
            _line.gameObject.SetActive(true);
            _line.positionCount = _linePositionsCount;
            _line.SetPositions(_linePositionsCache);

            if (EnableTiledTexture && _line.material != null && _line.textureMode == LineTextureMode.Tile)
            {
                float length = ComputePolylineLength(_posBuffer);
                var scale = _line.material.mainTextureScale;
                _line.material.mainTextureScale = new Vector2(Mathf.Max(1f, length / Mathf.Max(0.01f, 25)), scale.y);
            }
        }

        public void UpdateLineOffset(bool dragfromhead ,float offset)
        {
            Debug.Log(dragfromhead +" : "+ offset);
            if (_snake == null)
                return;
            var container = _snake.transform.parent as RectTransform;
            if (container == null)
                return;

            if(!_isInitGridLocalPath)
            {
                _cacheGridLocalPath.Clear();
                if (dragfromhead)
                {
                    for (int i = 0; i < _linePositionsCache.Length; i++)
                    {
                        Vector3 leadLocal = container.InverseTransformPoint(_linePositionsCache[i]);
                        _cacheGridLocalPath.Add(new Vector2(leadLocal.x, leadLocal.y));
                    }

                }
                else
                {
                    for (int i = _linePositionsCache.Length - 1; i >= 0; i--)
                    {
                        Vector3 leadLocal = container.InverseTransformPoint(_linePositionsCache[i]);
                        _cacheGridLocalPath.Add(new Vector2(leadLocal.x, leadLocal.y));
                    }
                }
            }

            UpdateLineFromPolyline(_cacheGridLocalPath, _snake.Length, _snake.GetGrid().CellSize, dragfromhead, offset);
        }

        // 新增：高效直连更新（输入为网格局部坐标折线）
        public void UpdateLineFromPolyline(List<Vector2> gridLocalPath, int segmentCount, float segmentSpacing, bool activeFromHead, float offset = 0)
        {
            if (_snake == null || _grid.Width == 0) return;
            if (gridLocalPath == null || gridLocalPath.Count < 2 || segmentCount <= 0)
            {
                if (_line != null) _line.gameObject.SetActive(false);
                return;
            }

            if(offset == 0)
            {
                _cacheGridLocalPath = gridLocalPath;
                _isInitGridLocalPath = true;
            }

            EnsureLineCreated();

            int subDiv = SubGridHelper.SUB_DIV;                // 5
            int totalPoints = segmentCount * subDiv;           // 与 _cachedRectTransforms 1:5 对齐
            EnsurePositionsCapacity(totalPoints);
            
            var path = gridLocalPath;
            // 折线扫描状态：统一从折线起点（index 0）向前扫描
            int polyIdx = 1;
            Vector2 cur = path[0];
            Vector2 nxt = path.Count > 1 ? path[1] : path[0];
            float segLen = Vector2.Distance(cur, nxt);
            float acc = 0f;

            // 每个小段间距 = segmentSpacing * 0.2
            float subStep = segmentSpacing * SubGridHelper.SUB_CELL_SIZE;

            var container = _snake.transform.parent as RectTransform;

            void SampleAndWrite(float targetDist, int writeIndex)
            {
                while (acc + segLen + 1e-4f < targetDist && polyIdx < path.Count - 1)
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
                    float need = Mathf.Clamp(targetDist - acc, 0f, segLen);
                    float t = need / Mathf.Max(segLen, 1e-6f);
                    pos = Vector2.LerpUnclamped(cur, nxt, t);
                }

                Vector3 worldPos = container != null
                    ? container.TransformPoint(new Vector3(pos.x, pos.y, 0f))
                    : new Vector3(pos.x, pos.y, 0f);

                _linePositionsCache[writeIndex] = worldPos;
            }

            if (activeFromHead)
            {
                for (int i = 0; i < segmentCount; i++)
                {
                    float baseDist = i * segmentSpacing;
                    for (int j = 0; j < subDiv; j++)
                    {
                        float target = baseDist + j * subStep;
                        int writeIndex = i * subDiv + j; // 5*i + j
                        SampleAndWrite(target, writeIndex);
                    }
                }
            }
            else
            {
                for (int i = 0; i < segmentCount; i++)
                {
                    float baseDist = i * segmentSpacing;
                    for (int j = 0; j < subDiv; j++)
                    {
                        float target = baseDist + j * subStep;
                        int forwardIndex = i * subDiv + j;
                        int writeIndex = totalPoints - 1 - forwardIndex; // 反向写入，使 [last] 对应 path[0]
                        SampleAndWrite(target, writeIndex);
                    }
                }
            }

            _line.gameObject.SetActive(true);
            _line.positionCount = totalPoints;
            _line.SetPositions(_linePositionsCache);

            if (EnableTiledTexture && _line.material != null && _line.textureMode == LineTextureMode.Tile)
            {
                float length = ComputePolylineLength(_linePositionsCache, totalPoints);
                var scale = _line.material.mainTextureScale;
                _line.material.mainTextureScale = new Vector2(Mathf.Max(1f, length / Mathf.Max(0.01f, 25f)), scale.y);
            }
        }

        void EnsurePositionsCapacity(int n)
        {
            if (_linePositionsCache == null || _linePositionsCache.Length < n)
                _linePositionsCache = new Vector3[Mathf.NextPowerOfTwo(n)];
        }

        float ComputePolylineLength(Vector3[] pts, int count)
        {
            if (pts == null || count < 2) return 0f;
            float len = 0f;
            for (int i = 1; i < count; i++) len += Vector3.Distance(pts[i - 1], pts[i]);
            return len;
        }


        // 就地修改 bodyList：
        // - 头部：用 [0] 到 [5] 的连线四等分，写入 [1..4]
        // - 尾部：用 [Count-6] 到 [Count-1] 的连线四等分，写入 [Count-5..Count-2]
        public void EqualizeHeadAndTail(List<GameObject> bodyList)
        {
            if (bodyList == null || bodyList.Count < 6) return;

            // 头部 0→5，填充 1..4
            {
                Vector3 a = bodyList[0].transform.position;
                Vector3 b = bodyList[5].transform.position;
                for (int i = 1; i <= 4; i++)
                {
                    float t = i / 5f;                 // 1/5, 2/5, 3/5, 4/5
                    bodyList[i].transform.position = Vector3.Lerp(a, b, t);
                }
            }

            // 尾部 (Count-6)→(Count-1)，填充 (Count-5..Count-2)
            {
                int n = bodyList.Count;
                Vector3 a = bodyList[n - 6].transform.position;
                Vector3 b = bodyList[n - 1].transform.position;
                for (int i = 1; i <= 4; i++)
                {
                    float t = i / 5f;                 // 1/5, 2/5, 3/5, 4/5
                    bodyList[n - 6 + i].transform.position = Vector3.Lerp(a, b, t); // → n-5, n-4, n-3, n-2
                }
            }
        }

        float ComputePolylineLength(List<Vector3> pts)
        {
            if (pts == null || pts.Count < 2) return 0f;
            float len = 0f;
            for (int i = 1; i < pts.Count; i++) len += Vector3.Distance(pts[i - 1], pts[i]);
            return len;
        }
        public void StartSnakeCoconsume(float time, bool activeFromHead)
        {
            _allConsumeTime = time;
            _consumeFromHead = activeFromHead;

            _consumeSpeed = 0f;
            _consumeElapsed = 0f;
            _consumeInited = false;

            if (_line == null || _linePositionsCache == null) return;

            _linePositionsCount = Mathf.Max(_linePositionsCount, _line.positionCount);
            if (_linePositionsCount <= 1 || _allConsumeTime <= 1e-6f) return;

            int last = _linePositionsCount - 1;
            _consumeAnchorOriginalIndex = _consumeFromHead ? 0 : last;
            _consumeAnchor = _linePositionsCache[_consumeAnchorOriginalIndex];

            // 1) 构建“从锚点到末端”的路径缓存
            _consumePathCount = _linePositionsCount;
            if (_consumePathCache == null || _consumePathCache.Length < _consumePathCount)
                _consumePathCache = new Vector3[_consumePathCount];
            if (_consumePrefixLen == null || _consumePrefixLen.Length < _consumePathCount)
                _consumePrefixLen = new float[_consumePathCount];

            if (_consumeFromHead)
            {
                // 原顺序：0..last
                for (int i = 0; i < _consumePathCount; i++)
                    _consumePathCache[i] = _linePositionsCache[i];
            }
            else
            {
                // 反向：last..0（把锚点搬到0位）
                for (int i = 0; i < _consumePathCount; i++)
                    _consumePathCache[i] = _linePositionsCache[last - i];
            }

            // 2) 前缀弧长
            _consumePrefixLen[0] = 0f;
            for (int i = 1; i < _consumePathCount; i++)
            {
                _consumePrefixLen[i] = _consumePrefixLen[i - 1] +
                    Vector3.Distance(_consumePathCache[i - 1], _consumePathCache[i]);
            }
            float totalLen = _consumePrefixLen[_consumePathCount - 1];
            if (totalLen <= 1e-6f) return;

            // 3) 原始索引的初始弧长 s0
            if (_consumeInitialS == null || _consumeInitialS.Length < _linePositionsCount)
                _consumeInitialS = new float[_linePositionsCount];

            if (_consumeFromHead)
            {
                // 原索引 i 对应缓存路径的 i 位置
                for (int i = 0; i < _linePositionsCount; i++)
                    _consumeInitialS[i] = _consumePrefixLen[Mathf.Clamp(i, 0, _consumePathCount - 1)];
            }
            else
            {
                // 原索引 i 对应缓存路径的 (last - i) 位置
                for (int i = 0; i < _linePositionsCount; i++)
                    _consumeInitialS[i] = _consumePrefixLen[Mathf.Clamp(last - i, 0, _consumePathCount - 1)];
            }

            // 4) 速度（总长 / 时间）
            _consumeSpeed = totalLen / _allConsumeTime;
            _consumeInited = true;
        }

        public void OnSnakeCoconsumeUpdate()
        {
            if (_line == null || _linePositionsCache == null) return;
            if (!_consumeInited || _linePositionsCount <= 0 || _consumePathCount <= 1) return;
            if (_allConsumeTime <= 1e-6f || _consumeSpeed <= 0f) return;

            _consumeElapsed += Time.deltaTime;
            float travel = _consumeSpeed * _consumeElapsed; // 本帧应减少的弧长

            // s(t) = max(0, s0 - travel)，并严格落在缓存路径上
            for (int i = 0; i < _linePositionsCount; i++)
            {
                float s0 = _consumeInitialS[i];
                float s = s0 - travel;
                if (s <= 0f)
                {
                    // 到达锚点：位置贴锚点，且非锚点隐藏
                    _linePositionsCache[i] = _consumeAnchor;
                    if (i != _consumeAnchorOriginalIndex)
                    {
                        var hide = _consumeAnchor;
                        hide.z = _consumeAnchor.z - 1000f;
                        _linePositionsCache[i] = hide;
                    }
                    continue;
                }
                if (s >= _consumePrefixLen[_consumePathCount - 1])
                {
                    // 超出末端（正常不发生），夹紧到末端
                    _linePositionsCache[i] = _consumePathCache[_consumePathCount - 1];
                    continue;
                }

                // 二分查找 s 所在段：prefix[j] <= s < prefix[j+1]
                int lo = 0, hi = _consumePathCount - 1;
                while (lo + 1 < hi)
                {
                    int mid = (lo + hi) >> 1;
                    if (_consumePrefixLen[mid] <= s) lo = mid; else hi = mid;
                }
                float segStart = _consumePrefixLen[lo];
                float segLen = Mathf.Max(1e-6f, _consumePrefixLen[lo + 1] - segStart);
                float t = (s - segStart) / segLen;

                Vector3 a = _consumePathCache[lo];
                Vector3 b = _consumePathCache[lo + 1];
                _linePositionsCache[i] = Vector3.LerpUnclamped(a, b, t);
            }

            // 写回 LineRenderer
            _line.positionCount = _linePositionsCount;
            _line.SetPositions(_linePositionsCache);
        }

    }
}