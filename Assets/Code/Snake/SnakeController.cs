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
		public float MoveSpeedCellsPerSecond = 8f;
		public float SnapThreshold = 0.05f;

		GridConfig _grid;
		readonly List<Transform> _segments = new List<Transform>();
		readonly LinkedList<Vector2Int> _bodyCells = new LinkedList<Vector2Int>(); // 离散身体占用格，头在First
		readonly Queue<Vector2Int> _pathQueue = new Queue<Vector2Int>(); // 待消费路径（目标格序列）
		Vector2Int _dragStartCell;
		bool _dragging;
		bool _dragOnHead;
		Vector2Int _currentHeadCell;
		Vector2Int _currentTailCell;
		Vector2Int _lastSampledCell; // 上次采样的手指网格
		float _moveAccumulator; // 基于速度的逐格推进计数器

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
				}
			}
			else if (Input.GetMouseButtonUp(0))
			{
				_dragging = false;
				// 结束拖拽：清空未消费路径并吸附（可选）
				_pathQueue.Clear();
				SnapToGrid();
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
					var append = BuildAxisAlignedPath(_lastSampledCell, targetCell, _dragOnHead);
					for (int i = 0; i < append.Count; i++) _pathQueue.Enqueue(append[i]);
					_lastSampledCell = targetCell;
				}
			}

			// 按速度逐格消费路径
			_moveAccumulator += MoveSpeedCellsPerSecond * Time.deltaTime;
			while (_moveAccumulator >= 1f && _pathQueue.Count > 0)
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
			}

			// 将渲染位置强制对齐到当前格中心，避免偏离网格
			int index = 0;
			foreach (var cell in _bodyCells)
			{
				if (index >= _segments.Count) break;
				_segments[index].position = _grid.CellToWorld(cell);
				index++;
			}
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

		// 将A->B分解为轴对齐的网格路径，并在临时队列中模拟校验避免自相交
		List<Vector2Int> BuildAxisAlignedPath(Vector2Int from, Vector2Int to, bool onHead)
		{
			List<Vector2Int> output = new List<Vector2Int>();
			if (from == to) return output;
			// 拆成两段：先走主轴，再走次轴（避免对角）
			int dx = to.x - from.x;
			int dy = to.y - from.y;
			bool horizFirst = Mathf.Abs(dx) >= Mathf.Abs(dy);

			var temp = new LinkedList<Vector2Int>(_bodyCells);
			int stepx = dx == 0 ? 0 : (dx > 0 ? 1 : -1);
			int stepy = dy == 0 ? 0 : (dy > 0 ? 1 : -1);
			Vector2Int cur = from;

			void SimAdvanceHead(Vector2Int next)
			{
				temp.AddFirst(next);
				temp.RemoveLast();
			}
			void SimAdvanceTail(Vector2Int next)
			{
				temp.AddLast(next);
				temp.RemoveFirst();
			}

			bool TryAppend(Vector2Int next)
			{
				// 校验相邻
				if (Manhattan(cur, next) != 1) return false;
				// 占用检查
				var occ = new HashSet<Vector2Int>(temp);
				var allowed = onHead ? temp.Last.Value : temp.First.Value; // 允许进入被释放的一端
				if (occ.Contains(next) && next != allowed) return false;
				output.Add(next);
				if (onHead) SimAdvanceHead(next); else SimAdvanceTail(next);
				cur = next;
				return true;
			}

			// 先主轴
			if (horizFirst)
			{
				for (int i = 0; i < Mathf.Abs(dx); i++)
				{
					var next = ClampInside(new Vector2Int(cur.x + stepx, cur.y));
					if (!TryAppend(next)) break;
				}
				for (int i = 0; i < Mathf.Abs(dy); i++)
				{
					var next = ClampInside(new Vector2Int(cur.x, cur.y + stepy));
					if (!TryAppend(next)) break;
				}
			}
			else
			{
				for (int i = 0; i < Mathf.Abs(dy); i++)
				{
					var next = ClampInside(new Vector2Int(cur.x, cur.y + stepy));
					if (!TryAppend(next)) break;
				}
				for (int i = 0; i < Mathf.Abs(dx); i++)
				{
					var next = ClampInside(new Vector2Int(cur.x + stepx, cur.y));
					if (!TryAppend(next)) break;
				}
			}

			return output;
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
	}
}


