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
        readonly List<GameObject> _cacheNewBodyList = new List<GameObject>();

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
            _line.numCapVertices = 0;

            _line.widthMultiplier = LineWidth;

            if (BodyLineMaterial != null)
            {
                // 独立材质实例，避免共享材质被改动
                _line.material = new Material(BodyLineMaterial);
                _line.material.color = BodyColor;

                // _Borders = (左边框, 右边框, 头部高度, 尾部高度)
                Vector4 borders = new Vector4(5,5,108,107);
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

            //头部一个点，身体，尾部一个点
            EqualizeHeadAndTail(_cacheNewBodyList);

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

    }
}