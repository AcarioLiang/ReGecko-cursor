using System.Collections.Generic;
using UnityEngine;
using ReGecko.GridSystem;

namespace ReGecko.SnakeSystem
{
    public class SnakeVisualsLeadSpriteManager : MonoBehaviour
    {
        [Header("Line Settings")]
        public Material BodyLineMaterial;
        public float LineWidth = 1.0f;
        public Color BodyColor = Color.white;
        public Sprite BodySprite;
        public bool EnableTiledTexture = true;

        SnakeVisualsLead _snake;
        private GridConfig _grid;
        LineRenderer _line;
        readonly List<Vector3> _posBuffer = new List<Vector3>(256);
        readonly List<GameObject> _cacheNewBodyList = new List<GameObject>();


        // 新增：折线缓存，避免每帧ToArray分配
        Vector3[] _linePositionsCache;
        int _linePositionsCount;

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
            _snake = GetComponentInParent<SnakeVisualsLead>();
            if (_snake == null) _snake = GetComponent<SnakeVisualsLead>();
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
            //bodyTW = 94  => _grid.CellSize * 0.9f 

            //head TW = 108
            //Tail TW = 94
            LineWidth = _grid.CellSize * 0.9f;

            EnsureLineCreated();
            UpdateAllLinePositions();
        }


        public void OnSnakeLengthChanged()
        {
            EnsureLineCreated();
            UpdateAllLinePositions();
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

                Vector4 borders = new Vector4(BodySprite.texture.width, BodySprite.texture.width, 0, 0);
                _line.material.SetVector("_Borders", borders);

                //临时启用调试模式来验证分区
                //_line.material.SetFloat("_DebugMode", 2f);
                _line.material.SetFloat("_SegmentCount", 1f);
            }

            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(BodyColor, 0f), new GradientColorKey(BodyColor, 1f) },
                new[] { new GradientAlphaKey(BodyColor.a, 0f), new GradientAlphaKey(BodyColor.a, 1f) }
            );
            //_line.colorGradient = grad;

            var r = _line.GetComponent<Renderer>();
            r.sortingLayerName = "Default"; // 或你的 UI Sorting Layer
            r.sortingOrder = 101;

        }



        void UpdateAllLinePositions()
        {
            if (_snake == null) return;

            if (_snake.BindObjectLead == null || _snake.BindObjectLeadNext == null)
            {
                if (_line != null) _line.gameObject.SetActive(false);
                return;
            }

            EnsureLineCreated();

            _posBuffer.Clear();
            foreach(var p in _snake.LinePositions)
            {
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
                //_line.material.mainTextureScale = new Vector2(Mathf.Max(1f, length / Mathf.Max(0.01f, 25)), scale.y);
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