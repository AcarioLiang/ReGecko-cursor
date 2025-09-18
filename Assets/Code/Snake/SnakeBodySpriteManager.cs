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
        readonly List<GameObject> _cacheNewBodyList = new List<GameObject>();


        // 新增：折线缓存，避免每帧ToArray分配
        Vector3[] _linePositionsCache;
        int _linePositionsCount;

        // 折线清洗参数
        [SerializeField] float PolylineDedupEps = 1e-4f;       // 同一点容差
        [SerializeField] float PolylineMinLoopEraseFactor = 0.35f; // 局部回环长度阈值系数(乘以 segmentSpacing)
        readonly List<Vector2> _polylineCleanBuffer = new List<Vector2>(512);
        readonly List<long> _polylineKeyBuffer = new List<long>(512);

        public Vector3[] GetCurLinePositions()
        {
            return _linePositionsCache;
        }
        public int GetCurLinePositionsCount()
        {
            return _linePositionsCount;
        }

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



        void UpdateAllLinePositions()
        {
            if (_snake == null || _grid.Width == 0) return;

            var body = _snake.GetSegments();
            if (body == null || body.Count == 0)
            {
                if (_line != null) _line.gameObject.SetActive(false);
                return;
            }

            EnsureLineCreated();

            _posBuffer.Clear();
            _cacheNewBodyList.Clear();

            _cacheNewBodyList.AddRange(body);

            ////头部一个点，身体，尾部一个点
            //EqualizeHeadAndTail(_cacheNewBodyList);

            foreach (var node in _cacheNewBodyList)
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
            return;
            if (_snake == null)
                return;

            UpdateLineFromPolyline(dragfromhead?_snake.GetVirtualPathPoints(): _snake.GetVirtualPathPointsRe(), _snake.Length, _snake.GetGrid().CellSize, dragfromhead, offset);
        }

        // 新增：高效直连更新（输入为网格局部坐标折线）
        public void UpdateLineFromPolyline(List<Vector2> gridLocalPath, int subSegmentCount, float subSegmentSpacing, bool activeFromHead, float offset = 0)
        {
            if (_snake == null || _grid.Width == 0) return;
            if (gridLocalPath == null || gridLocalPath.Count < 2 || subSegmentCount <= 0)
            {
                //if (_line != null) _line.gameObject.SetActive(false);
                return;
            }


            EnsureLineCreated();

            int totalPoints = subSegmentCount;           // 与 _cachedRectTransforms 对齐
            EnsurePositionsCapacity(totalPoints);
            
            var path = gridLocalPath;

            // 预计算折线总长
            float totalLen = 0f;
            for (int i = 1; i < path.Count; i++) totalLen += Vector2.Distance(path[i - 1], path[i]);

            // 扫描光标（从起点向前）
            int polyIdx = 1;
            Vector2 cur = path[0];
            Vector2 nxt = path.Count > 1 ? path[1] : path[0];
            float segLen = Vector2.Distance(cur, nxt);
            float acc = 0f;

            float subStep = subSegmentSpacing;
            var container = _snake.transform.parent as RectTransform;

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

                Vector3 worldPos = container != null
                    ? container.TransformPoint(new Vector3(pos.x, pos.y, 0f))
                    : new Vector3(pos.x, pos.y, 0f);

                _linePositionsCache[writeIndex] = worldPos;
            }

            // forward：按 targetDist 递增调用；reverse：反向写入索引
            if (activeFromHead)
            {
                for (int i = 0; i < subSegmentCount; i++)
                {
                    float baseDist = i * subSegmentSpacing;

                    float target = baseDist;
                    int writeIndex = i;
                    SampleAndWrite(target, writeIndex);
                }
            }
            else
            {
                for (int i = 0; i < subSegmentCount; i++)
                {
                    float baseDist = i * subSegmentSpacing;

                    float target = baseDist;
                    int writeIndex = subSegmentCount - 1 - i;
                    SampleAndWrite(target, writeIndex);
                }
            }

            _line.gameObject.SetActive(true);
            _line.positionCount = subSegmentCount;
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