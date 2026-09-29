using System.Collections;
using System.Collections.Generic;
using ReGecko.Game;
using ReGecko.Grid.Entities;
using ReGecko.GridSystem;
using UnityEngine;
using UnityEngine.UI;

namespace ReGecko.SnakeSystem
{
    public class SnakeController : BaseSnake
    {
        const float SnapDuration = 0.09f;
        const float PositionEpsilon = 0.0001f;

        readonly LinkedList<Vector2Int> _bodyCells = new LinkedList<Vector2Int>();
        readonly List<Vector2Int> _cells = new List<Vector2Int>(32);
        readonly List<Vector2Int> _candidate = new List<Vector2Int>(32);
        readonly List<Vector2> _visualPoints = new List<Vector2>(32);
        readonly List<Vector2> _snapStart = new List<Vector2>(32);
        readonly List<GameObject> _segments = new List<GameObject>(32);
        readonly List<RectTransform> _segmentRects = new List<RectTransform>(32);

        SnakeRibbonGraphic _ribbon;
        Image _headImage;
        Image _tailImage;
        Coroutine _consumeCoroutine;
        Vector2 _dragPoint;
        Vector2 _grabOffset;
        float _snapElapsed;
        float _targetSpeed;
        float _displaySpeed;
        float _crawlPhase;
        bool _snapping;

        public override LinkedList<Vector2Int> GetBodyCells() => _bodyCells;
        public override List<GameObject> GetSegments() => _segments;
        public Vector2Int GetHeadCell() => _cells.Count > 0 ? _cells[0] : Vector2Int.zero;
        public Vector2Int GetTailCell() => _cells.Count > 0 ? _cells[_cells.Count - 1] : Vector2Int.zero;
        public GridConfig GetGrid() => _grid;

        public override void Initialize(GridConfig grid)
        {
            _grid = grid;
            _cells.Clear();
            if (InitialBodyCells != null)
            {
                for (int i = 0; i < InitialBodyCells.Length; i++)
                    _cells.Add(InitialBodyCells[i]);
            }
            if (_cells.Count == 0) _cells.Add(InitialHeadCell);

            Length = _cells.Count;
            SyncBodyCells();
            CreateVisuals();
            SetVisualsToCenters();
            RenderVisuals();
        }

        void CreateVisuals()
        {
            var bodyObject = new GameObject("BodyRibbon", typeof(RectTransform),
                                            typeof(CanvasRenderer), typeof(SnakeRibbonGraphic));
            bodyObject.transform.SetParent(transform, false);
            var bodyRect = bodyObject.GetComponent<RectTransform>();
            bodyRect.anchorMin = bodyRect.anchorMax = new Vector2(0.5f, 0.5f);
            bodyRect.pivot = new Vector2(0.5f, 0.5f);
            bodyRect.anchoredPosition = Vector2.zero;
            bodyRect.sizeDelta = new Vector2(_grid.Width * _grid.CellSize, _grid.Height * _grid.CellSize);
            bodyRect.SetAsFirstSibling();
            _ribbon = bodyObject.GetComponent<SnakeRibbonGraphic>();
            _ribbon.SetBody(ResolveBodySprite(), _grid.CellSize);

            _segments.Clear();
            _segmentRects.Clear();
            for (int i = 0; i < _cells.Count; i++)
            {
                var segment = new GameObject("Segment_" + i, typeof(RectTransform), typeof(Image));
                segment.transform.SetParent(transform, false);
                var rect = segment.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = Vector2.one * _grid.CellSize;
                var image = segment.GetComponent<Image>();
                image.raycastTarget = false;
                image.color = Color.white;
                image.enabled = i == 0 || i == _cells.Count - 1;
                if (i == 0)
                {
                    image.sprite = HeadSprite;
                    _headImage = image;
                }
                else if (i == _cells.Count - 1)
                {
                    image.sprite = TailSprite;
                    _tailImage = image;
                }
                _segments.Add(segment);
                _segmentRects.Add(rect);
            }
        }

        public Vector2 GetEndpointPosition(bool fromHead)
        {
            if (_visualPoints.Count == 0) return Vector2.zero;
            return _visualPoints[fromHead ? 0 : _visualPoints.Count - 1];
        }

        public bool BeginDrag(bool fromHead, Vector2 pointer)
        {
            if (!IsAlive() || _consuming || !IsControllable || _cells.Count == 0) return false;
            _snapping = false;
            DragFromHead = fromHead;
            IsDragging = true;
            _dragPoint = GetEndpointPosition(fromHead);
            _grabOffset = _dragPoint - pointer;
            return true;
        }

        public void DragTo(Vector2 pointer)
        {
            if (!IsDragging || _consuming) return;
            Vector2 desired = _grid.ClampWorld(pointer + _grabOffset);
            Vector2 previous = _dragPoint;
            SweepTo(desired);
            float dt = Mathf.Max(Time.unscaledDeltaTime, 0.001f);
            _targetSpeed = Mathf.Clamp01(Vector2.Distance(previous, _dragPoint) /
                                         (_grid.CellSize * dt * 8f));
            UpdateDragVisuals();
        }

        public void EndDrag()
        {
            if (!IsDragging) return;
            IsDragging = false;
            _targetSpeed = 0f;
            _snapping = true;
            _snapElapsed = 0f;
            _snapStart.Clear();
            _snapStart.AddRange(_visualPoints);
        }

        // Walk every crossed grid boundary, including crossings skipped by a fast pointer frame.
        void SweepTo(Vector2 desired)
        {
            Vector2 start = _dragPoint;
            Vector2 delta = desired - start;
            if (delta.sqrMagnitude < PositionEpsilon * PositionEpsilon) return;

            Vector2Int cursor = _grid.WorldToCell(start);
            Vector2Int destination = _grid.WorldToCell(desired);
            int stepX = delta.x > 0f ? 1 : -1;
            int stepY = delta.y > 0f ? 1 : -1;
            float half = _grid.CellSize * 0.5f;
            Vector2 center = _grid.CellToWorld(cursor);
            float crossX = Mathf.Abs(delta.x) < PositionEpsilon
                ? float.PositiveInfinity
                : (center.x + stepX * half - start.x) / delta.x;
            float crossY = Mathf.Abs(delta.y) < PositionEpsilon
                ? float.PositiveInfinity
                : (center.y + stepY * half - start.y) / delta.y;
            float incrementX = Mathf.Abs(delta.x) < PositionEpsilon
                ? float.PositiveInfinity : _grid.CellSize / Mathf.Abs(delta.x);
            float incrementY = Mathf.Abs(delta.y) < PositionEpsilon
                ? float.PositiveInfinity : _grid.CellSize / Mathf.Abs(delta.y);

            int limit = _grid.Width + _grid.Height + 4;
            while (cursor != destination && limit-- > 0)
            {
                bool alongX = crossX < crossY ||
                              (Mathf.Abs(crossX - crossY) < 0.0001f &&
                               Mathf.Abs(delta.x) >= Mathf.Abs(delta.y));
                float crossing = alongX ? crossX : crossY;
                Vector2Int next = cursor + (alongX
                    ? new Vector2Int(stepX, 0) : new Vector2Int(0, stepY));
                if (!TryBuildStep(next, _candidate))
                {
                    _dragPoint = Vector2.Lerp(start, desired, Mathf.Clamp01(crossing - 0.0001f));
                    return;
                }

                CommitStep(_candidate);
                if (_consuming)
                {
                    _dragPoint = _grid.CellToWorld(next);
                    return;
                }
                cursor = next;
                if (alongX) crossX += incrementX;
                else crossY += incrementY;
            }
            _dragPoint = desired;
        }

        bool TryBuildStep(Vector2Int next, List<Vector2Int> result)
        {
            result.Clear();
            if (_cells.Count == 0 || !_grid.IsInside(next)) return false;
            int last = _cells.Count - 1;
            Vector2Int active = DragFromHead ? _cells[0] : _cells[last];
            if (Mathf.Abs(active.x - next.x) + Mathf.Abs(active.y - next.y) != 1) return false;
            if (IsPathBlocked(next)) return false;

            if (_cells.Count == 1)
            {
                result.Add(next);
                return true;
            }

            bool retract = next == (DragFromHead ? _cells[1] : _cells[last - 1]);
            if (retract)
            {
                Vector2Int far = DragFromHead ? _cells[last] : _cells[0];
                Vector2Int beforeFar = DragFromHead ? _cells[last - 1] : _cells[1];
                Vector2Int extension = far + (far - beforeFar);
                if (!_grid.IsInside(extension) || IsPathBlocked(extension) ||
                    OccupiedBySelf(extension, DragFromHead ? 0 : last)) return false;
                if (DragFromHead)
                {
                    for (int i = 1; i <= last; i++) result.Add(_cells[i]);
                    result.Add(extension);
                }
                else
                {
                    result.Add(extension);
                    for (int i = 0; i < last; i++) result.Add(_cells[i]);
                }
                return true;
            }

            if (OccupiedBySelf(next, DragFromHead ? last : 0)) return false;
            if (DragFromHead)
            {
                result.Add(next);
                for (int i = 0; i < last; i++) result.Add(_cells[i]);
            }
            else
            {
                for (int i = 1; i <= last; i++) result.Add(_cells[i]);
                result.Add(next);
            }
            return true;
        }

        bool OccupiedBySelf(Vector2Int cell, int vacatedIndex)
        {
            for (int i = 0; i < _cells.Count; i++)
                if (i != vacatedIndex && _cells[i] == cell) return true;
            return false;
        }

        void CommitStep(List<Vector2Int> next)
        {
            _cells.Clear();
            _cells.AddRange(next);
            SyncBodyCells();
            SnakeManager.Instance.InvalidateOccupiedCellsCache();

            HoleEntity hole = FindMatchingHole(GetHeadCell());
            if (hole != null) _consumeCoroutine = StartCoroutine(CoConsume(hole, true));
            if (_consuming) return;
            hole = FindMatchingHole(GetTailCell());
            if (hole != null) _consumeCoroutine = StartCoroutine(CoConsume(hole, false));
        }

        void SyncBodyCells()
        {
            _bodyCells.Clear();
            for (int i = 0; i < _cells.Count; i++) _bodyCells.AddLast(_cells[i]);
        }

        void SetVisualsToCenters()
        {
            _visualPoints.Clear();
            for (int i = 0; i < _cells.Count; i++) _visualPoints.Add(_grid.CellToWorld(_cells[i]));
        }

        void UpdateDragVisuals()
        {
            SetVisualsToCenters();
            int activeIndex = DragFromHead ? 0 : _cells.Count - 1;
            Vector2 center = _grid.CellToWorld(_cells[activeIndex]);
            Vector2 offset = _dragPoint - center;
            ApplyPartialStep(new Vector2Int(offset.x >= 0f ? 1 : -1, 0),
                             Mathf.Abs(offset.x) / _grid.CellSize);
            ApplyPartialStep(new Vector2Int(0, offset.y >= 0f ? 1 : -1),
                             Mathf.Abs(offset.y) / _grid.CellSize);
            _visualPoints[activeIndex] = _dragPoint;
        }

        void ApplyPartialStep(Vector2Int direction, float fraction)
        {
            if (fraction < 0.0001f) return;
            Vector2Int active = DragFromHead ? _cells[0] : _cells[_cells.Count - 1];
            if (!TryBuildStep(active + direction, _candidate)) return;
            float amount = Mathf.Clamp01(fraction);
            for (int i = 0; i < _cells.Count; i++)
            {
                Vector2 original = _grid.CellToWorld(_cells[i]);
                Vector2 next = _grid.CellToWorld(_candidate[i]);
                _visualPoints[i] += (next - original) * amount;
            }
        }

        void LateUpdate()
        {
            if (_cells.Count == 0 || _ribbon == null) return;
            if (_snapping)
            {
                _snapElapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(_snapElapsed / SnapDuration);
                t = 1f - (1f - t) * (1f - t) * (1f - t);
                for (int i = 0; i < _visualPoints.Count; i++)
                    _visualPoints[i] = Vector2.Lerp(_snapStart[i], _grid.CellToWorld(_cells[i]), t);
                if (t >= 1f) _snapping = false;
            }

            _displaySpeed = Mathf.MoveTowards(_displaySpeed, _targetSpeed,
                                              Time.unscaledDeltaTime * 7f);
            _crawlPhase += Time.unscaledDeltaTime * (0.7f + 13f * _displaySpeed);
            RenderVisuals();
        }

        void RenderVisuals()
        {
            if (_ribbon == null || _visualPoints.Count == 0) return;
            _ribbon.SetShape(_visualPoints, _displaySpeed, _crawlPhase);
            for (int i = 0; i < _segmentRects.Count && i < _visualPoints.Count; i++)
                _segmentRects[i].anchoredPosition = _visualPoints[i];

            if (_visualPoints.Count > 1)
            {
                PointCap(_headImage, _visualPoints[0], _visualPoints[1]);
                int last = _visualPoints.Count - 1;
                PointCap(_tailImage, _visualPoints[last], _visualPoints[last - 1]);
            }
            if (_headImage != null)
            {
                float pulse = 1f + 0.018f * _displaySpeed * Mathf.Sin(_crawlPhase);
                _headImage.rectTransform.localScale = new Vector3(pulse, 1f / pulse, 1f);
            }
        }

        static void PointCap(Image cap, Vector2 at, Vector2 towardBody)
        {
            if (cap == null) return;
            Vector2 direction = towardBody - at;
            if (direction.sqrMagnitude > 0.0001f)
                cap.rectTransform.localRotation = Quaternion.Euler(
                    0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }

        public override void UpdateMovement()
        {
            if (IsDragging) UpdateDragVisuals();
        }

        public override void SnapCellsToGrid()
        {
            _snapping = false;
            SetVisualsToCenters();
            RenderVisuals();
        }

        public override void UpdateGridConfig(GridConfig newGrid)
        {
            _grid = newGrid;
            if (_ribbon != null)
            {
                _ribbon.rectTransform.sizeDelta =
                    new Vector2(_grid.Width * _grid.CellSize, _grid.Height * _grid.CellSize);
                _ribbon.SetBody(ResolveBodySprite(), _grid.CellSize);
            }
            foreach (var rect in _segmentRects) rect.sizeDelta = Vector2.one * _grid.CellSize;
            SnapCellsToGrid();
        }

        Sprite ResolveBodySprite()
        {
            var repeatingBody = Resources.Load<Sprite>(
                "UI/Snake/Game_SnakeBody" + (int)ColorType);
            return repeatingBody != null ? repeatingBody : BodySprite;
        }

        bool IsPathBlocked(Vector2Int cell)
        {
            if (!_grid.IsInside(cell)) return true;
            if (GridEntityManager.Instance != null)
            {
                var entities = GridEntityManager.Instance.GetAt(cell);
                if (entities != null)
                {
                    foreach (var entity in entities)
                    {
                        if (entity is WallEntity) return true;
                        if (entity is HoleEntity hole && hole.IsBlockingCell(cell, this)) return true;
                    }
                }
            }
            return SnakeManager.Instance.IsCellOccupiedByOtherSnakes(cell, this);
        }

        HoleEntity FindMatchingHole(Vector2Int cell)
        {
            if (GridEntityManager.Instance == null) return null;
            foreach (var entity in GridEntityManager.Instance.HoleEntities)
            {
                var hole = entity as HoleEntity;
                if (hole != null && hole.ColorType == ColorType &&
                    (hole.Cell == cell || hole.IsAdjacent(cell))) return hole;
            }
            return null;
        }

        public IEnumerator CoConsume(HoleEntity hole, bool fromHead)
        {
            _consuming = true;
            IsDragging = false;
            _snapping = false;
            _targetSpeed = 0f;
            hole.OnTirggerStart();

            int count = _cells.Count;
            var path = new List<Vector2>(count + 1) { _grid.CellToWorld(hole.Cell) };
            for (int i = 0; i < count; i++)
            {
                int index = fromHead ? i : count - 1 - i;
                path.Add(_visualPoints[index]);
            }
            var lengths = new float[path.Count];
            for (int i = 1; i < path.Count; i++)
                lengths[i] = lengths[i - 1] + Vector2.Distance(path[i - 1], path[i]);

            float interval = Mathf.Max(0.01f, hole.ConsumeInterval);
            float duration = interval * count;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float travel = elapsed / duration * lengths[count];
                for (int i = 0; i < count; i++)
                {
                    int index = fromHead ? i : count - 1 - i;
                    float distance = Mathf.Max(0f, lengths[i + 1] - travel);
                    _visualPoints[index] = SamplePath(path, lengths, distance);
                    var image = index == 0 ? _headImage : index == count - 1 ? _tailImage : null;
                    if (image != null)
                    {
                        var tint = image.color;
                        tint.a = distance < _grid.CellSize * 0.15f ? 0f : 1f;
                        image.color = tint;
                    }
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            _cells.Clear();
            _bodyCells.Clear();
            SnakeManager.Instance.InvalidateOccupiedCellsCache();
            hole.OnTirggered();
            Destroy(gameObject);
            SnakeManager.Instance.TryClearSnakes();
        }

        static Vector2 SamplePath(List<Vector2> path, float[] lengths, float distance)
        {
            for (int i = 1; i < path.Count; i++)
            {
                if (distance <= lengths[i])
                {
                    float span = lengths[i] - lengths[i - 1];
                    float t = span < PositionEpsilon ? 1f :
                              (distance - lengths[i - 1]) / span;
                    return Vector2.Lerp(path[i - 1], path[i], t);
                }
            }
            return path[path.Count - 1];
        }

        public override void Destroy()
        {
            if (_consumeCoroutine != null)
            {
                StopCoroutine(_consumeCoroutine);
                _consumeCoroutine = null;
            }
            IsDragging = false;
        }
    }
}
