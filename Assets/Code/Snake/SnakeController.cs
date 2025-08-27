using System.Collections.Generic;
using UnityEngine;
using ReGecko.GridSystem;
using ReGecko.Grid.Entities;
using System.Collections;

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
		GridEntityManager _entityManager;
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

		bool _consuming; // 洞吞噬中

		// 公共访问方法
		public Vector2Int GetHeadCell() => _bodyCells.Count > 0 ? _currentHeadCell : Vector2Int.zero;
		public Vector2Int GetTailCell() => _bodyCells.Count > 0 ? _currentTailCell : Vector2Int.zero;

	public void Initialize(GridConfig grid)
	{
		_grid = grid;
		_entityManager = FindObjectOfType<GridEntityManager>();
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
			// 如果蛇已被完全消除，停止所有移动更新
			if (_bodyCells.Count == 0) return;
			
			if (_dragging && !_consuming)
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

				// 洞检测：若拖动端临近洞，触发吞噬
				var hole = FindAdjacentHole(_dragOnHead ? _currentHeadCell : _currentTailCell);
				if (hole != null)
				{
					_consumeCoroutine ??= StartCoroutine(CoConsume(hole, _dragOnHead));
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
					// 倒车：若下一步将进入紧邻身体，则改为让尾部后退一步
					var nextBody = _bodyCells.First.Next != null ? _bodyCells.First.Next.Value : _bodyCells.First.Value;
					if (nextCell == nextBody)
					{
						if (!TryReverseOneStep()) break;
					}
					else
					{
						if (!AdvanceHeadTo(nextCell)) break;
					}
				}
				else
				{
					// 尾部倒车：若下一步将进入紧邻身体，则改为让头部前进一步
					var prevBody = _bodyCells.Last.Previous != null ? _bodyCells.Last.Previous.Value : _bodyCells.Last.Value;
					if (nextCell == prevBody)
					{
						if (!TryReverseFromTail()) break;
					}
					else
					{
						if (!AdvanceTailTo(nextCell)) break;
					}
				}
				_moveAccumulator -= 1f;
				stepsThisFrame++;
				_stepsConsumedThisFrame++;
			}

			// 拖动中的可视：使用折线距离定位，严格保持段间距=_grid.CellSize，避免重叠
			if (_dragging && !_consuming)
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

		Coroutine _consumeCoroutine;
		HoleEntity FindAdjacentHole(Vector2Int from)
		{
			// 简化：全局搜寻场景中的洞实体，找到与from相邻的第一个
			var holes = Object.FindObjectsOfType<HoleEntity>();
			for (int i = 0; i < holes.Length; i++)
			{
				if (holes[i].IsAdjacent(from)) return holes[i];
			}
			return null;
		}

		public IEnumerator CoConsume(HoleEntity hole, bool fromHead)
		{
			_consuming = true;
			_dragging = false; // 脱离手指控制
			_pathQueue.Clear();
			_moveAccumulator = 0f;
			Vector3 holeCenter = _grid.CellToWorld(hole.Cell);
			
			// 逐段进入洞并消失
			while (_bodyCells.Count > 0)
			{
				Transform segmentToConsume = null;
				if (fromHead)
				{
					_bodyCells.RemoveFirst();
					if (_segments.Count > 0) 
					{
						segmentToConsume = _segments[0];
						_segments.RemoveAt(0);
					}
				}
				else
				{
					_bodyCells.RemoveLast();
					int last = _segments.Count - 1;
					if (last >= 0) 
					{
						segmentToConsume = _segments[last];
						_segments.RemoveAt(last);
					}
				}
				
				// 更新当前头尾缓存，防止空引用
				if (_bodyCells.Count > 0)
				{
					_currentHeadCell = _bodyCells.First.Value;
					_currentTailCell = _bodyCells.Last.Value;
				}
				
				// 动画：将段移动到洞中心再消失
				if (segmentToConsume != null)
				{
					StartCoroutine(MoveToHoleAndDestroy(segmentToConsume, holeCenter, hole.ConsumeInterval * 0.5f));
				}
				
				yield return new WaitForSeconds(hole.ConsumeInterval);
			}
			_consuming = false;
			_consumeCoroutine = null;
			// 全部消失后，销毁蛇对象或重生；此处直接销毁
			if (_bodyCells.Count == 0)
			{
				Destroy(gameObject);
			}
		}
		
		IEnumerator MoveToHoleAndDestroy(Transform segment, Vector3 holeCenter, float duration)
		{
			Vector3 startPos = segment.position;
			
			// 计算沿身体路径到洞的移动路径
			List<Vector3> pathToHole = CalculatePathToHole(startPos, holeCenter);
			
			// 沿路径移动
			float totalDistance = 0f;
			for (int i = 0; i < pathToHole.Count - 1; i++)
			{
				totalDistance += Vector3.Distance(pathToHole[i], pathToHole[i + 1]);
			}
			
			float moveSpeed = totalDistance / duration;
			float currentDistance = 0f;
			int currentSegment = 0;
			
			while (currentSegment < pathToHole.Count - 1)
			{
				Vector3 segmentStart = pathToHole[currentSegment];
				Vector3 segmentEnd = pathToHole[currentSegment + 1];
				float segmentLength = Vector3.Distance(segmentStart, segmentEnd);
				
				float segmentTime = segmentLength / moveSpeed;
				float elapsed = 0f;
				
				while (elapsed < segmentTime)
				{
					elapsed += Time.deltaTime;
					float t = elapsed / segmentTime;
					segment.position = Vector3.Lerp(segmentStart, segmentEnd, t);
					yield return null;
				}
				
				currentSegment++;
			}
			
			// 确保到达洞中心
			segment.position = holeCenter;
			
			// 消失效果（缩小并淡出）
			var sr = segment.GetComponent<SpriteRenderer>();
			if (sr != null)
			{
				float fadeTime = duration * 0.3f;
				float fadeElapsed = 0f;
				Color originalColor = sr.color;
				
				while (fadeElapsed < fadeTime)
				{
					fadeElapsed += Time.deltaTime;
					float fadeT = fadeElapsed / fadeTime;
					sr.color = new Color(originalColor.r, originalColor.g, originalColor.b, 1f - fadeT);
					segment.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, fadeT);
					yield return null;
				}
			}
			
			Destroy(segment.gameObject);
		}

		List<Vector3> CalculatePathToHole(Vector3 startPos, Vector3 holeCenter)
		{
			List<Vector3> path = new List<Vector3>();
			path.Add(startPos);
			
			Vector2Int startCell = _grid.WorldToCell(startPos);
			Vector2Int holeCell = _grid.WorldToCell(holeCenter);
			
			// 简单的A*路径寻找，沿着网格路径到洞
			List<Vector2Int> cellPath = FindPathToHole(startCell, holeCell);
			
			// 转换为世界坐标
			for (int i = 1; i < cellPath.Count; i++)
			{
				path.Add(_grid.CellToWorld(cellPath[i]));
			}
			
			// 确保最后一点是洞中心
			if (path[path.Count - 1] != holeCenter)
			{
				path.Add(holeCenter);
			}
			
			return path;
		}

		List<Vector2Int> FindPathToHole(Vector2Int start, Vector2Int target)
		{
			List<Vector2Int> path = new List<Vector2Int>();
			Vector2Int current = start;
			path.Add(current);
			
			// 简单的曼哈顿距离路径寻找
			while (current != target)
			{
				Vector2Int next = current;
				
				// 优先朝目标方向移动
				if (current.x != target.x)
				{
					next.x += current.x < target.x ? 1 : -1;
				}
				else if (current.y != target.y)
				{
					next.y += current.y < target.y ? 1 : -1;
				}
				
				// 检查是否可以移动到该位置
				if (_grid.IsInside(next) && !IsPathBlocked(next))
				{
					current = next;
					path.Add(current);
				}
				else
				{
					// 如果被阻挡，尝试其他方向
					var directions = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
					bool found = false;
					
					foreach (var dir in directions)
					{
						Vector2Int alternative = current + dir;
						if (_grid.IsInside(alternative) && !IsPathBlocked(alternative) && !path.Contains(alternative))
						{
							current = alternative;
							path.Add(current);
							found = true;
							break;
						}
					}
					
					if (!found) break; // 无法找到路径，直接跳出
				}
				
				// 防止无限循环
				if (path.Count > 50) break;
			}
			
			return path;
		}

		bool IsPathBlocked(Vector2Int cell)
		{
			// 检查是否被墙体阻挡（洞本身不算阻挡）
			if (_entityManager != null)
			{
				var entities = _entityManager.GetAt(cell);
				if (entities != null)
				{
					foreach (var entity in entities)
					{
						if (entity is WallEntity) return true;
						// 洞不算阻挡，可以通过
					}
				}
			}
			return false;
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
			// 检查网格边界
			if (!_grid.IsInside(nextCell)) return false;
			// 检查实体阻挡
			if (_entityManager != null && _entityManager.IsBlocked(nextCell)) return false;
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
			// 检查网格边界
			if (!_grid.IsInside(nextCell)) return false;
			// 检查实体阻挡
			if (_entityManager != null && _entityManager.IsBlocked(nextCell)) return false;
			// 占用校验：允许进入原头
			var headCell = _bodyCells.First.Value;
			if (_bodyCells.Contains(nextCell) && nextCell != headCell) return false;
			_bodyCells.AddLast(nextCell);
			_bodyCells.RemoveFirst();
			_currentTailCell = nextCell;
			_currentHeadCell = _bodyCells.First.Value;
			return true;
		}

		bool TryReverseOneStep()
		{
			// 以尾部为基准，朝着与尾相邻段的反方向后退；若不可行，尝试左右方向
			if (_bodyCells.Last == null || _bodyCells.Last.Previous == null) return false;
			var tail = _bodyCells.Last.Value;
			var prev = _bodyCells.Last.Previous.Value; // 尾部相邻的身体
			Vector2Int dir = tail - prev; // 远离身体方向
			Vector2Int left = new Vector2Int(-dir.y, dir.x);
			Vector2Int right = new Vector2Int(dir.y, -dir.x);
			var candidates = new [] { dir, left, right };
			for (int i = 0; i < candidates.Length; i++)
			{
				var next = tail + candidates[i];
				if (!_grid.IsInside(next.x, next.y)) continue;
				if (_grid.HasBlock(next.x, next.y)) continue;
				if (_entityManager != null && _entityManager.IsBlocked(next)) continue;
				if (!IsCellFree(next)) continue;
				return AdvanceTailTo(next);
			}
			return false;
		}

		bool TryReverseFromTail()
		{
			// 从尾部倒车：以头部为基准，朝着与头相邻段的反方向前进
			if (_bodyCells.First == null || _bodyCells.First.Next == null) return false;
			var head = _bodyCells.First.Value;
			var next = _bodyCells.First.Next.Value; // 头部相邻的身体
			Vector2Int dir = head - next; // 远离身体方向
			Vector2Int left = new Vector2Int(-dir.y, dir.x);
			Vector2Int right = new Vector2Int(dir.y, -dir.x);
			var candidates = new [] { dir, left, right };
			for (int i = 0; i < candidates.Length; i++)
			{
				var nextHead = head + candidates[i];
				if (!_grid.IsInside(nextHead.x, nextHead.y)) continue;
				if (_grid.HasBlock(nextHead.x, nextHead.y)) continue;
				if (_entityManager != null && _entityManager.IsBlocked(nextHead)) continue;
				if (!IsCellFree(nextHead)) continue;
				return AdvanceHeadTo(nextHead);
			}
			return false;
		}

		bool IsCellFree(Vector2Int cell)
		{
			// 不允许进入身体占用格；允许进入当前头（在反向时不需要，但保持一致性）
			foreach (var c in _bodyCells) { if (c == cell) return false; }
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


