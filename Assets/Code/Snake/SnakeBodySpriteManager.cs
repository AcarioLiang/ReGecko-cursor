using System.Collections.Generic;
using UnityEngine;
using ReGecko.GridSystem;

namespace ReGecko.SnakeSystem
{
    public class SnakeBodySpriteManager : MonoBehaviour
    {
        const float EPS = 1e-4f;
        [Header("Line Settings")]
        public Material BodyLineMaterial;
        public float LineWidth = 1.0f;
        public Color BodyColor = Color.white;
        public Sprite BodySprite;
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
            BodySprite = _snake.BodySprite;
            LineWidth = _grid.CellSize * 0.9f;

            EnsureLineCreated();
            UpdateAllLinePositions();
        }


        public void OnSnakeLengthChanged()
        {
            EnsureLineCreated();
            UpdateAllLinePositions();
            _snake.UpdateVisualsHead();
        }
        public void OnSnakeLengthCoConsume(int index)
        {
            EnsureLineCreated();
            UpdateAllLinePositions(index);
            _snake.UpdateVisualsHead();
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
                _line.material.color = Color.white;
                _line.material.mainTexture = BodySprite.texture;

                //Vector4 borders = new Vector4(BodySprite.texture.width, BodySprite.texture.width, 0, 0);
                //_line.material.SetVector("_Borders", borders);

                //临时启用调试模式来验证分区
                //_line.material.SetFloat("_DebugMode", 2f);
                _line.material.SetFloat("_SegmentCount", (float)_snake.Length);

                // 调试输出
                //Debug.Log($"Texture size: {_line.material.mainTexture.width}x{_line.material.mainTexture.height}");
                //Debug.Log($"Borders set to: {borders}");
                //Debug.Log($"TexelSize: {_line.material.GetVector("_MainTex_TexelSize")}");
                //
                //Debug.Log($"LineRenderer textureMode: {_line.textureMode}");
                //Debug.Log($"LineRenderer material: {_line.material.name}");
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



        void UpdateAllLinePositions(int coConsumeCnt = -1)
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
            float unit = SubGridHelper.SUB_CELL_SIZE * _grid.CellSize;
            int index = 0;
            foreach (var node in _cacheNewBodyList)
            {
                var p = node.transform.position;
                if(coConsumeCnt >= 0)
                {
                    p.z = node.transform.position.z;
                }
                else
                {
                    p.z = 0;
                }
                    
                _posBuffer.Add(p);
                index++;
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

                // 获取纹理的宽度（像素）
                float textureWidth = _line.material.mainTexture.width;

                // 计算平铺次数：总长度（世界单位） * （每单位像素数）/ 纹理宽度
                // 即：总长度（世界单位） * pixelsPerUnit / 纹理宽度
                float tilingX = length  / textureWidth;

                _line.material.mainTextureScale = new Vector2(tilingX, scale.y);
            }
        }


        float ComputePolylineLength(List<Vector3> pts)
        {
            if (pts == null || pts.Count < 2) return 0f;
            float len = 0f;
            for (int i = 1; i < pts.Count; i++) len += Vector3.Distance(pts[i - 1], pts[i]);
            return len;
        }

    }
}