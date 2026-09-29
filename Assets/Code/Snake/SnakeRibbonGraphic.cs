using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace ReGecko.SnakeSystem
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SnakeRibbonGraphic : MaskableGraphic
    {
        readonly List<Vector2> _centers = new List<Vector2>(32);
        readonly List<Vector2> _rounded = new List<Vector2>(96);
        readonly List<Vector2> _samples = new List<Vector2>(256);
        readonly List<float> _sampleDistances = new List<float>(256);

        Sprite _bodySprite;
        float _cellSize = 1f;
        float _speedFactor;
        float _crawlPhase;

        public override Texture mainTexture => _bodySprite != null ? _bodySprite.texture : s_WhiteTexture;

        public void SetBody(Sprite sprite, float cellSize)
        {
            _bodySprite = sprite;
            _cellSize = Mathf.Max(0.01f, cellSize);
            raycastTarget = false;
            SetMaterialDirty();
            SetVerticesDirty();
        }

        public void SetShape(IReadOnlyList<Vector2> centers, float speedFactor, float crawlPhase)
        {
            _centers.Clear();
            for (int i = 0; i < centers.Count; i++) _centers.Add(centers[i]);
            _speedFactor = Mathf.Clamp01(speedFactor);
            _crawlPhase = crawlPhase;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_bodySprite == null || _centers.Count < 2) return;

            BuildRoundedPath();
            BuildSamples();
            if (_samples.Count < 2) return;

            Vector4 uv = DataUtility.GetOuterUV(_bodySprite);
            float halfWidth = _cellSize * 0.43f;
            AddFins(vh, uv, halfWidth);
            for (int i = 0; i < _samples.Count - 1; i++)
            {
                Vector2 a = _samples[i];
                Vector2 b = _samples[i + 1];
                Vector2 tangentA = TangentAt(i);
                Vector2 tangentB = TangentAt(i + 1);
                Vector2 normalA = new Vector2(-tangentA.y, tangentA.x);
                Vector2 normalB = new Vector2(-tangentB.y, tangentB.x);
                float widthA = WidthAt(_sampleDistances[i], halfWidth);
                float widthB = WidthAt(_sampleDistances[i + 1], halfWidth);
                float uA = Mathf.Repeat(_sampleDistances[i] / _cellSize, 1f);
                float uB = Mathf.Repeat(_sampleDistances[i + 1] / _cellSize, 1f);
                if (uB < uA || Mathf.Abs(uB) < 0.0001f) uB = 1f;

                int first = vh.currentVertCount;
                AddVert(vh, a + normalA * widthA, uv, uA, 1f);
                AddVert(vh, a - normalA * widthA, uv, uA, 0f);
                AddVert(vh, b + normalB * widthB, uv, uB, 1f);
                AddVert(vh, b - normalB * widthB, uv, uB, 0f);
                vh.AddTriangle(first, first + 2, first + 1);
                vh.AddTriangle(first + 2, first + 3, first + 1);
            }
        }

        void BuildRoundedPath()
        {
            _rounded.Clear();
            _rounded.Add(_centers[0]);
            for (int i = 1; i < _centers.Count - 1; i++)
            {
                Vector2 previous = _centers[i - 1];
                Vector2 corner = _centers[i];
                Vector2 next = _centers[i + 1];
                Vector2 incoming = corner - previous;
                Vector2 outgoing = next - corner;
                float inLength = incoming.magnitude;
                float outLength = outgoing.magnitude;
                if (inLength < 0.001f || outLength < 0.001f ||
                    Vector2.Dot(incoming / inLength, outgoing / outLength) > 0.99f)
                {
                    _rounded.Add(corner);
                    continue;
                }

                float radius = Mathf.Min(_cellSize * 0.24f, inLength * 0.35f, outLength * 0.35f);
                Vector2 entry = corner - incoming / inLength * radius;
                Vector2 exit = corner + outgoing / outLength * radius;
                _rounded.Add(entry);
                for (int step = 1; step <= 4; step++)
                {
                    float t = step / 4f;
                    _rounded.Add((1f - t) * (1f - t) * entry +
                                 2f * (1f - t) * t * corner + t * t * exit);
                }
            }
            _rounded.Add(_centers[_centers.Count - 1]);
        }

        void BuildSamples()
        {
            _samples.Clear();
            _sampleDistances.Clear();
            _samples.Add(_rounded[0]);
            _sampleDistances.Add(0f);

            float distance = 0f;
            for (int i = 1; i < _rounded.Count; i++)
            {
                Vector2 from = _rounded[i - 1];
                Vector2 to = _rounded[i];
                float length = Vector2.Distance(from, to);
                if (length < 0.001f) continue;

                float traveled = 0f;
                while (traveled < length - 0.0001f)
                {
                    float toTile = _cellSize - Mathf.Repeat(distance, _cellSize);
                    if (toTile < 0.0001f) toTile = _cellSize;
                    float step = Mathf.Min(length - traveled, _cellSize / 7f, toTile);
                    traveled += step;
                    distance += step;
                    _samples.Add(Vector2.Lerp(from, to, traveled / length));
                    _sampleDistances.Add(distance);
                }
            }
        }

        Vector2 TangentAt(int index)
        {
            Vector2 from = _samples[Mathf.Max(0, index - 1)];
            Vector2 to = _samples[Mathf.Min(_samples.Count - 1, index + 1)];
            Vector2 tangent = to - from;
            return tangent.sqrMagnitude > 0.000001f ? tangent.normalized : Vector2.right;
        }

        float WidthAt(float distance, float halfWidth)
        {
            float wave = Mathf.Sin(_crawlPhase - distance / _cellSize * 3.5f);
            return halfWidth * (1f + 0.035f * _speedFactor * wave);
        }

        void AddFins(VertexHelper vh, Vector4 uv, float halfWidth)
        {
            float length = _sampleDistances[_sampleDistances.Count - 1];
            for (float distance = _cellSize * 0.8f;
                 distance < length - _cellSize * 0.45f; distance += _cellSize * 1.4f)
            {
                int sample = 0;
                while (sample < _sampleDistances.Count - 1 &&
                       _sampleDistances[sample] < distance) sample++;
                Vector2 at = _samples[sample];
                Vector2 tangent = TangentAt(sample);
                Vector2 normal = new Vector2(-tangent.y, tangent.x);
                float sway = Mathf.Sin(_crawlPhase - distance / _cellSize * 4f) *
                             (0.025f + 0.08f * _speedFactor) * _cellSize;
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector2 outward = normal * side;
                    Vector2 root = at + outward * halfWidth * 0.7f;
                    Vector2 tip = at + outward * (halfWidth + _cellSize * 0.11f) +
                                  tangent * sway;
                    int first = vh.currentVertCount;
                    AddVert(vh, root - tangent * _cellSize * 0.1f, uv, 0.1f, 0.5f);
                    AddVert(vh, root + tangent * _cellSize * 0.1f, uv, 0.1f, 0.5f);
                    AddVert(vh, tip, uv, 0.1f, 0.5f);
                    vh.AddTriangle(first, first + 1, first + 2);
                }
            }
        }

        void AddVert(VertexHelper vh, Vector2 position, Vector4 uv, float u, float v)
        {
            vh.AddVert(position, color, new Vector2(Mathf.Lerp(uv.x, uv.z, u), Mathf.Lerp(uv.y, uv.w, v)));
        }
    }
}
