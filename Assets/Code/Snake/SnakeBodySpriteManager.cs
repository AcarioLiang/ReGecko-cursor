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

        SnakeController _snake;
        GridConfig _grid;
        LineRenderer _line;
        readonly List<Vector3> _posBuffer = new List<Vector3>(256);

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

        public void OnSnakeMoved()
        {
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
            _line.numCapVertices = 2;

            _line.widthMultiplier = LineWidth;

            if (BodyLineMaterial != null)
            {
                // 独立材质实例，避免共享材质被改动
                _line.material = new Material(BodyLineMaterial);
                _line.material.color = BodyColor;
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
            foreach(var node in body)
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

            _line.gameObject.SetActive(true);
            _line.positionCount = _posBuffer.Count;
            _line.SetPositions(_posBuffer.ToArray());

            if (EnableTiledTexture && _line.material != null && _line.textureMode == LineTextureMode.Tile)
            {
                float length = ComputePolylineLength(_posBuffer);
                var scale = _line.material.mainTextureScale;
                _line.material.mainTextureScale = new Vector2(Mathf.Max(1f, length / Mathf.Max(0.01f, 25)), scale.y);
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