using System.Collections.Generic;
using UnityEngine;
using ReGecko.GridSystem;

namespace ReGecko.SnakeSystem
{
	public class SnakeController : MonoBehaviour
	{
		public Sprite BodySprite;
		public Color BodyColor = Color.green;
		public int Length = 4;
		public Vector2Int HeadCell;
		public Vector2Int[] InitialBodyCells; // 含头在index 0，可为空
		public float MoveSpeedCellsPerSecond = 16f;
		public float SnapThreshold = 0.05f;
		public int MaxCellsPerFrame = 12;
		[Header("Debug / Profiler")]
		public bool ShowDebugStats = false;
		public bool DrawDebugGizmos = false;

		GridConfig _grid;
		readonly List<Transform> _segments = new List<Transform>();
		readonly LinkedList<Vector2Int> _bodyCells = new LinkedList<Vector2Int>(); // 离散身体占用格，头在First
		readonly Queue<Vector2Int> _pathQueue = new Queue<Vector2Int>(); // 待消费路径（目标格序列）
		readonly List<Vector2Int> _pathBuildBuffer = new List<Vector2Int>(64); // 复用的路径构建缓冲
		Vector2Int _dragStartCell;
		bool _dragging;
		bool _dragOnHead;
		Vector2Int _currentHeadCell;
		Vector2Int _currentTailCell;
		Vector2Int _lastSampledCell; // 上次采样的手指网格
		float _moveAccumulator; // 基于速度的逐格推进计数器
		float _lastStatsTime;
		int _stepsConsumedThisFrame;

		enum DragAxis { None, X, Y }
		DragAxis _dragAxis = DragAxis.None;

		public void Initialize(GridConfig grid)
		{
			_grid = grid;
			BuildSegments();
			PlaceInitial();
		}

		void BuildSegments()
		{
			for (int i = 0; i < _segments.Count; i++)
			{
				if (_segments[i] != null) Destroy(_segments[i].gameObject);
			}
			_segments.Clear();
			for (int i = 0; i < Mathf.Max(1, Length); i++)
			{
				var go = new GameObject(i == 0 ? "Head" : $"Body_{i}");
				go.transform.SetParent(transform, false);
				var sr = go.AddComponent<SpriteRenderer>();
				sr.sprite = BodySprite;
				sr.color = BodyColor;
				sr.sortingOrder = 0 + (i == 0 ? 1 : 0);
				_segments.Add(go.transform);
			}
		}

		void PlaceInitial()
		{
			// 构建初始身体格（优先使用配置）
			List<Vector2Int> cells = new List<Vector2Int>();
			if (InitialBodyCells != null && InitialBodyCells.Length > 0)
			{
				for (int i = 0; i < InitialBodyCells.Length && i < Length; i++)
				{
					var c = ClampInside(InitialBodyCells[i]);
					if (cells.Count == 0 || Manhattan(cells[cells.Count - 1], c) == 1)
					{
						cells.Add(c);
					}
					else
					{
						break; // 非相邻则停止使用后续，避免断裂
					}
				}
			}
			if (cells.Count == 0)
			{
				var head = ClampInside(HeadCell);
				cells.Add(head);
				for (int i = 1; i < Length; i++)
				{
					var c = new Vector2Int(head.x, Mathf.Clamp(head.y + i, 0, _grid.Height - 1));
					cells.Add(c);
				}
			}
			// 去重防重叠
			var set = new HashSet<Vector2Int>();
			for (int i = 0; i < cells.Count; i++)
			{
				if (set.Contains(cells[i]))
				{
					// 发现重叠，回退到简单直线
					cells.Clear();
					var head = ClampInside(HeadCell);
					cells.Add(head);
					for (int k = 1; k < Length; k++)
					{
						cells.Add(new Vector2Int(head.x, Mathf.Clamp(head.y + k, 0, _grid.Height - 1)));
					}
					break;
				}
				set.Add(cells[i]);
			}
			// 同步到链表与可视
			_bodyCells.Clear();
			for (int i = 0; i < Mathf.Min(cells.Count, _segments.Count); i++)
			{
				_bodyCells.AddLast(cells[i]);
				_segments[i].position = _grid.CellToWorld(cells[i]);
			}
			_currentHeadCell = _bodyCells.First.Value;
			_currentTailCell = _bodyCells.Last.Value;
		}

		Vector2Int ClampInside(Vector2Int cell)
		{
			cell.x = Mathf.Clamp(cell.x, 0, _grid.Width - 1);
			cell.y = Mathf.Clamp(cell.y, 0, _grid.Height - 1);
			return cell;
		}

		void Update()
		{
			HandleInput();
			UpdateMovement();
		}

		void HandleInput()
		{
			if (Input.GetMouseButtonDown(0))
			{
				var world = ScreenToWorld(Input.mousePosition);
				if (TryPickHeadOrTail(world, out _dragOnHead))
				{
					_dragging = true;
					_dragStartCell = _grid.WorldToCell(world);
					_pathQueue.Clear();
					_lastSampledCell = _dragOnHead ? _currentHeadCell : _currentTailCell;
					_moveAccumulator = 0f;
					_stepsConsumedThisFrame = 0;
					_dragAxis = DragAxis.None;
				}
			}
			else if (Input.GetMouseButtonUp(0))
			{
				_dragging = false;
				// 结束拖拽：清空未消费路径并吸附（可选）
				_pathQueue.Clear();
				SnapToGrid();
				_dragAxis = DragAxis.None;
			}
		}

		void UpdateMovement()
		{
			if (_dragging)
			{
				// 采样当前手指所在格，扩充路径队列（仅四向路径）
				var world = ScreenToWorld(Input.mousePosition);
				var targetCell = ClampInside(_grid.WorldToCell(world));
				if (targetCell != _lastSampledCell)
				{
					// 更新主方向：按更大位移轴确定
					var delta = targetCell - (_dragOnHead ? _currentHeadCell : _currentTailCell);
					_dragAxis = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) ? DragAxis.X : DragAxis.Y;
					EnqueueAxisAlignedPath(_lastSampledCell, targetCell);
					_lastSampledCell = targetCell;
				}
			}

			// 按速度逐格消费路径
			_stepsConsumedThisFrame = 0;
			_moveAccumulator += MoveSpeedCellsPerSecond * Time.deltaTime;
			int stepsThisFrame = 0;
			while (_moveAccumulator >= 1f && _pathQueue.Count > 0 && stepsThisFrame < MaxCellsPerFrame)
			{
				var nextCell = _pathQueue.Dequeue();
				if (_dragOnHead)
				{
					if (!AdvanceHeadTo(nextCell)) break;
				}
				else
				{
					if (!AdvanceTailTo(nextCell)) break;
				}
				_moveAccumulator -= 1f;
				stepsThisFrame++;
				_stepsConsumedThisFrame++;
			}

			// 拖动中的可视：使用折线距离定位，严格保持段间距=_grid.CellSize，避免重叠
			if (_dragging)
			{
				UpdateVisualsSmoothDragging();
			}
			else
			{
				// 未拖动时：保持每段对齐到自身格中心
				int idx = 0;
				foreach (var cell in _bodyCells)
				{
					if (idx >= _segments.Count) break;
					_segments[idx].position = _grid.CellToWorld(cell);
					idx++;
				}
			}
		}

		void UpdateVisualsSmoothDragging()
		{
			float frac = Mathf.Clamp01(_moveAccumulator);
			Vector3 finger = ScreenToWorld(Input.mousePosition);
			if (_dragOnHead)
			{
				Vector3 headA = _grid.CellToWorld(_currentHeadCell);
				Vector3 headVisual;
				if (_pathQueue.Count > 0)
				{
					Vector3 headB = _grid.CellToWorld(_pathQueue.Peek());
					headVisual = Vector3.Lerp(headA, headB, frac);
				}
				else
				{
					// 单格内自由拖动：限制在当前格AABB内，且仅沿主方向自由
					headVisual = ClampWorldToCellBounds(finger, _currentHeadCell);
					var center = _grid.CellToWorld(_currentHeadCell);
					if (_dragAxis == DragAxis.X) headVisual.y = center.y; else if (_dragAxis == DragAxis.Y) headVisual.x = center.x;
				}
				// 构建折线：headVisual -> (body First.Next ... Last)
				List<Vector3> pts = new List<Vector3>(_segments.Count + 2);
				pts.Add(headVisual);
				var it = _bodyCells.First;
				if (it != null) it = it.Next; // skip head cell
				while (it != null)
				{
					pts.Add(_grid.CellToWorld(it.Value));
					it = it.Next;
				}
				float spacing = _grid.CellSize;
				for (int i = 0; i < _segments.Count; i++)
				{
					Vector3 p = GetPointAlongPolyline(pts, i * spacing);
					_segments[i].position = p;
				}
			}
			else
			{
				// 拖尾：构建折线： (head ... before tail) -> tailVisual
				Vector3 tailA = _grid.CellToWorld(_currentTailCell);
				Vector3 tailVisual;
				if (_pathQueue.Count > 0)
				{
					Vector3 tailB = _grid.CellToWorld(_pathQueue.Peek());
					tailVisual = Vector3.Lerp(tailA, tailB, frac);
				}
				else
				{
					// 单格内自由拖动：限制在当前格AABB内，且仅沿主方向自由
					tailVisual = ClampWorldToCellBounds(finger, _currentTailCell);
					var center = _grid.CellToWorld(_currentTailCell);
					if (_dragAxis == DragAxis.X) tailVisual.y = center.y; else if (_dragAxis == DragAxis.Y) tailVisual.x = center.x;
				}
				List<Vector3> pts = new List<Vector3>(_segments.Count + 2);
				// head to last-1 body cells
				var it = _bodyCells.First;
				while (it != null)
				{
					// skip last, we will add tailVisual instead
					if (it.Next == null) break;
					pts.Add(_grid.CellToWorld(it.Value));
					it = it.Next;
				}
				pts.Add(tailVisual);
				float spacing = _grid.CellSize;
				for (int i = 0; i < _segments.Count; i++)
				{
					Vector3 p = GetPointAlongPolyline(pts, i * spacing);
					_segments[i].position = p;
				}
			}
		}

		Vector3 GetPointAlongPolyline(List<Vector3> pts, float distance)
		{
			if (pts.Count == 0) return Vector3.zero;
			if (pts.Count == 1) return pts[0];
			float remaining = distance;
			for (int i = 0; i < pts.Count - 1; i++)
			{
				Vector3 a = pts[i];
				Vector3 b = pts[i + 1];
				float segLen = Vector3.Distance(a, b);
				if (remaining <= segLen)
				{
					float t = segLen <= 0.0001f ? 0f : (remaining / segLen);
					return Vector3.Lerp(a, b, t);
				}
				remaining -= segLen;
			}
			// 超出则返回最后一点
			return pts[pts.Count - 1];
		}

		Vector3 ClampWorldToCellBounds(Vector3 world, Vector2Int cell)
		{
			Vector3 c = _grid.CellToWorld(cell);
			float half = _grid.CellSize * 0.5f;
			world.x = Mathf.Clamp(world.x, c.x - half, c.x + half);
			world.y = Mathf.Clamp(world.y, c.y - half, c.y + half);
			world.z = 0f;
			return world;
		}

		void MoveSnakeToHeadCell(Vector2Int desiredHead)
		{
			if (desiredHead == _currentHeadCell) return;
			if (!IsPathValid(_currentHeadCell, desiredHead)) return;
			_currentHeadCell = desiredHead;
			// 更新身体队列：头插入、新尾移除
			_bodyCells.AddFirst(desiredHead);
			_bodyCells.RemoveLast();
		}

		bool IsPathValid(Vector2Int from, Vector2Int to)
		{
			// 仅主轴移动，且逐格检查是否与身体重叠
			Vector2Int step = new Vector2Int(Mathf.Clamp(to.x - from.x, -1, 1), Mathf.Clamp(to.y - from.y, -1, 1));
			if (step.x != 0 && step.y != 0) return false;
			Vector2Int cur = from;
			// 构建当前身体占据的格集合（基于离散cells）
			var occupied = new HashSet<Vector2Int>(_bodyCells);
			var tailCell = _bodyCells.Last.Value; // 允许移动到尾的格
			while (cur != to)
			{
				cur += step;
				if (occupied.Contains(cur) && cur != tailCell) return false;
			}
			return true;
		}

		bool TryAdvanceOneCell(Vector2Int step)
		{
			var next = ClampInside(_currentHeadCell + step);
			if (!IsPathValid(_currentHeadCell, next)) return false;
			MoveSnakeToHeadCell(next);
			return true;
		}

		// 将A->B分解为轴对齐的网格路径（先主轴后次轴），使用复用缓冲避免GC
		void EnqueueAxisAlignedPath(Vector2Int from, Vector2Int to)
		{
			_pathBuildBuffer.Clear();
			if (from == to) return;
			int dx = to.x - from.x;
			int dy = to.y - from.y;
			bool horizFirst = Mathf.Abs(dx) >= Mathf.Abs(dy);
			int stepx = dx == 0 ? 0 : (dx > 0 ? 1 : -1);
			int stepy = dy == 0 ? 0 : (dy > 0 ? 1 : -1);
			Vector2Int cur = from;
			if (horizFirst)
			{
				for (int i = 0; i < Mathf.Abs(dx); i++) { cur = new Vector2Int(cur.x + stepx, cur.y); _pathBuildBuffer.Add(ClampInside(cur)); }
				for (int i = 0; i < Mathf.Abs(dy); i++) { cur = new Vector2Int(cur.x, cur.y + stepy); _pathBuildBuffer.Add(ClampInside(cur)); }
			}
			else
			{
				for (int i = 0; i < Mathf.Abs(dy); i++) { cur = new Vector2Int(cur.x, cur.y + stepy); _pathBuildBuffer.Add(ClampInside(cur)); }
				for (int i = 0; i < Mathf.Abs(dx); i++) { cur = new Vector2Int(cur.x + stepx, cur.y); _pathBuildBuffer.Add(ClampInside(cur)); }
			}
			for (int i = 0; i < _pathBuildBuffer.Count; i++)
			{
				_pathQueue.Enqueue(_pathBuildBuffer[i]);
			}
		}

		bool AdvanceHeadTo(Vector2Int nextCell)
		{
			// 必须相邻
			if (Manhattan(_currentHeadCell, nextCell) != 1) return false;
			// 占用校验：允许进入原尾
			var tailCell = _bodyCells.Last.Value;
			if (_bodyCells.Contains(nextCell) && nextCell != tailCell) return false;
			_bodyCells.AddFirst(nextCell);
			_bodyCells.RemoveLast();
			_currentHeadCell = nextCell;
			_currentTailCell = _bodyCells.Last.Value;
			return true;
		}

		bool AdvanceTailTo(Vector2Int nextCell)
		{
			// 必须相邻
			if (Manhattan(_currentTailCell, nextCell) != 1) return false;
			// 占用校验：允许进入原头
			var headCell = _bodyCells.First.Value;
			if (_bodyCells.Contains(nextCell) && nextCell != headCell) return false;
			_bodyCells.AddLast(nextCell);
			_bodyCells.RemoveFirst();
			_currentTailCell = nextCell;
			_currentHeadCell = _bodyCells.First.Value;
			return true;
		}

		void SnapToGrid()
		{
			// 基于离散cells统一吸附
			int index = 0;
			foreach (var cell in _bodyCells)
			{
				if (index >= _segments.Count) break;
				_segments[index].position = _grid.CellToWorld(cell);
				index++;
			}
			_currentHeadCell = _bodyCells.First.Value;
			_currentTailCell = _bodyCells.Last.Value;
		}

		Vector2Int ClampAdjacent(Vector2Int cell, Vector2Int targetNeighbor)
		{
			Vector2Int best = cell;
			int bestDist = Manhattan(cell, targetNeighbor);
			var candidates = new []
			{
				targetNeighbor + Vector2Int.up,
				targetNeighbor + Vector2Int.down,
				targetNeighbor + Vector2Int.left,
				targetNeighbor + Vector2Int.right
			};
			for (int i = 0; i < candidates.Length; i++)
			{
				var c = ClampInside(candidates[i]);
				int d = Manhattan(c, cell);
				if (d < bestDist)
				{
					bestDist = d;
					best = c;
				}
			}
			return best;
		}

		int Manhattan(Vector2Int a, Vector2Int b)
		{
			return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
		}

		bool TryPickHeadOrTail(Vector3 world, out bool onHead)
		{
			onHead = false;
			if (_segments.Count == 0) return false;
			var head = _segments[0].position;
			var tail = _segments[_segments.Count - 1].position;
			float headDist = Vector3.Distance(world, head);
			float tailDist = Vector3.Distance(world, tail);
			if (Mathf.Min(headDist, tailDist) > _grid.CellSize * 0.8f) return false;
			onHead = headDist <= tailDist;
			return true;
		}

		Vector3 ScreenToWorld(Vector3 screen)
		{
			var cam = Camera.main;
			if (cam == null) return Vector3.zero;
			var w = cam.ScreenToWorldPoint(screen);
			w.z = 0f;
			return w;
		}

		void OnGUI()
		{
			if (!ShowDebugStats) return;
			GUI.color = Color.white;
			var style = new GUIStyle(GUI.skin.label) { fontSize = 14 };
			GUILayout.BeginArea(new Rect(10, 10, 400, 200), GUI.skin.box);
			GUILayout.Label($"Queue: {_pathQueue.Count}", style);
			GUILayout.Label($"Accumulator: {_moveAccumulator:F2}", style);
			GUILayout.Label($"Steps/frame: {_stepsConsumedThisFrame}", style);
			GUILayout.Label($"Head: {_currentHeadCell} Tail: {_currentTailCell}", style);
			GUILayout.EndArea();
		}

		void OnDrawGizmosSelected()
		{
			if (!DrawDebugGizmos) return;
			if (_grid.Width == 0) return;
			Gizmos.color = new Color(0f, 1f, 0f, 0.4f);
			foreach (var c in _bodyCells)
			{
				Gizmos.DrawWireCube(_grid.CellToWorld(c), new Vector3(_grid.CellSize, _grid.CellSize, 0f));
			}
			Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
			Vector3 prev = Vector3.negativeInfinity;
			foreach (var c in _pathQueue)
			{
				var p = _grid.CellToWorld(c);
				Gizmos.DrawSphere(p, 0.05f);
				if (prev.x > -10000f) Gizmos.DrawLine(prev, p);
				prev = p;
			}
		}
	}
}


