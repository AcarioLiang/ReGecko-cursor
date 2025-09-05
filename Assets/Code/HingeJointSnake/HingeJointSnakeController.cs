using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using ReGecko.GridSystem;
using ReGecko.Grid.Entities;
using ReGecko.SnakeSystem;
using ReGecko.Game;

namespace ReGecko.HingeJointSnake
{
    /// <summary>
    /// 基于 HingeJoint2D 的蛇控制器
    /// 实现顺滑的拖拽移动，支持蛇头和蛇尾拖拽
    /// </summary>
    public class HingeJointSnakeController : BaseSnake
    {
        [Header("拖拽配置")]
        [SerializeField] private float _dragSmoothness = 10f;      // 拖拽平滑度
        [SerializeField] private float _gridSnapDistance = 0.3f;   // 网格吸附距离
        [SerializeField] private float _minDragDistance = 0.1f;    // 最小拖拽距离
        
        [Header("物理配置")]
        [SerializeField] private float _jointSpring = 1000f;       // 关节弹簧强度
        [SerializeField] private float _jointDamping = 50f;        // 关节阻尼
        [SerializeField] private float _segmentSpacing = 1f;       // 段落间距（网格单位）
        
        [Header("调试")]
        [SerializeField] private bool _showDebugLines = false;
        
        // 蛇段管理
        private List<SnakeSegment> _segments = new List<SnakeSegment>();
        private SnakeSegment _headSegment;
        private SnakeSegment _tailSegment;
        
        // 拖拽状态
        private bool _isDragging = false;
        private SnakeSegment _draggedSegment; // 当前被拖拽的段落
        private Vector2 _dragStartPosition;
        private Vector2 _lastValidPosition;
        private Vector2Int _targetGridCell;
        
        // 移动方向和对齐
        private Vector2 _primaryDirection = Vector2.zero;   // 主要移动方向
        private Vector2 _secondaryDirection = Vector2.zero; // 次要方向（用于对齐）
        
        // 网格对齐相关
        private bool _isAligning = false;
        private Vector2 _alignmentTarget;
        private float _alignmentSpeed = 20f;
        
        // 配置缓存
        private Vector2Int[] _initialBodyCells;
        
        // 公共属性
        public float CellSize => _grid.IsValid() ? _grid.CellSize : 1.0f;

        #region 基类实现

        public override void Initialize(GridConfig grid, GridEntityManager entityManager = null, SnakeManager snakeManager = null)
        {
            _grid = grid;
            _entityManager = entityManager;
            _snakeManager = snakeManager;
            
            CreateSnakeSegments();
            SetupInitialPositions();
            
            Debug.Log($"HingeJointSnake '{SnakeId}' 初始化完成，长度：{Length}");
        }

        public override void UpdateMovement()
        {
            // 处理输入
            HandleInput();
            
            // 更新拖拽和对齐
            if (_isDragging)
            {
                UpdateDragMovement();
            }
            else if (_isAligning)
            {
                UpdateGridAlignment();
            }
            
            // 更新段落位置
            UpdateSegmentPositions();
        }
        
        /// <summary>
        /// 处理输入
        /// </summary>
        private void HandleInput()
        {
            // 如果不可控制或已死亡，不处理输入
            if (!IsControllable || !IsAlive()) return;
            
            // 检测鼠标按下
            if (Input.GetMouseButtonDown(0))
            {
                // 检查是否点击了蛇头或蛇尾
                SnakeSegment clickedSegment = GetClickedSegmentByRaycast();
                if (clickedSegment != null && clickedSegment.CanTriggerDrag)
                {
                    StartDragByMouse(clickedSegment);
                }
            }
            // 检测鼠标拖拽
            else if (Input.GetMouseButton(0) && _isDragging && _draggedSegment != null)
            {
                UpdateDragByMouse();
            }
            // 检测鼠标释放
            else if (Input.GetMouseButtonUp(0) && _isDragging)
            {
                EndDrag();
            }
        }

        #endregion

        #region 蛇段创建和管理

        /// <summary>
        /// 创建蛇的所有段落
        /// </summary>
        private void CreateSnakeSegments()
        {
            // 清理现有段落
            ClearSegments();
            
            // 获取配置的身体格子
            Vector2Int[] bodyCells = GetInitialBodyCells();
            if (bodyCells == null || bodyCells.Length < 2)
            {
                Debug.LogError("配置的身体格子不足，无法创建蛇");
                return;
            }
            
            // 计算总段落数：每个格子之间需要3个转弯关节
            int totalCellCount = bodyCells.Length;
            int totalJointCount = (totalCellCount - 1) * 3; // 每两个格子之间有3个关节
            int totalSegmentCount = totalCellCount + totalJointCount;
            
            Debug.Log($"创建蛇段落：格子数 {totalCellCount}，关节数 {totalJointCount}，总段落数 {totalSegmentCount}");
            
            // 创建段落
            int segmentIndex = 0;
            
            // 创建蛇头
            SnakeSegment headSegment = CreateSegment(SegmentType.Head, segmentIndex++, false);
            _segments.Add(headSegment);
            _headSegment = headSegment;
            
            // 创建身体和转弯关节
            for (int i = 1; i < bodyCells.Length - 1; i++)
            {
                // 创建前一个格子到当前格子的3个转弯关节
                Vector2Int prevCell = bodyCells[i - 1];
                Vector2Int currentCell = bodyCells[i];
                
                // 创建3个转弯关节
                for (int j = 0; j < 3; j++)
                {
                    SnakeSegment jointSegment = CreateSegment(SegmentType.Joint, segmentIndex++, true);
                    _segments.Add(jointSegment);
                }
                
                // 创建身体段落
                SnakeSegment bodySegment = CreateSegment(SegmentType.Body, segmentIndex++, false);
                _segments.Add(bodySegment);
            }
            
            // 创建最后一组转弯关节（倒数第二个格子到最后一个格子）
            for (int j = 0; j < 3; j++)
            {
                SnakeSegment jointSegment = CreateSegment(SegmentType.Joint, segmentIndex++, true);
                _segments.Add(jointSegment);
            }
            
            // 创建蛇尾
            SnakeSegment tailSegment = CreateSegment(SegmentType.Tail, segmentIndex++, false);
            _segments.Add(tailSegment);
            _tailSegment = tailSegment;
            
            Debug.Log($"创建完成，总段落数：{_segments.Count}");
            
            // 铰链关节连接会在SetupInitialPositions中延迟设置
        }

        /// <summary>
        /// 获取段落类型
        /// </summary>
        private SegmentType GetSegmentType(int index)
        {
            if (index == 0) return SegmentType.Head;
            if (index == Length - 1) return SegmentType.Tail;
            return SegmentType.Body;
        }

        /// <summary>
        /// 创建单个蛇段
        /// </summary>
        private SnakeSegment CreateSegment(SegmentType type, int index, bool isJoint)
        {
            GameObject segmentGO = new GameObject($"Segment_{type}_{index}");
            segmentGO.transform.SetParent(transform, false);
            
            // 添加必要组件
            RectTransform rectTransform = segmentGO.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.one * 0.5f;
            rectTransform.anchorMax = Vector2.one * 0.5f;
            rectTransform.pivot = Vector2.one * 0.5f;
            
            // 大小将在Initialize中设置
            
            // 图片已经是Y轴向上，不需要旋转
            // 默认朝向是垂直向上的
            
            Rigidbody2D rigidbody = segmentGO.AddComponent<Rigidbody2D>();
            rigidbody.gravityScale = 0f; // 完全禁用重力
            rigidbody.freezeRotation = true; // 禁止旋转，确保对齐
            rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rigidbody.constraints = RigidbodyConstraints2D.FreezeRotation; // 冻结旋转
            rigidbody.drag = 10f; // 增加阻力，减少物理影响
            rigidbody.angularDrag = 10f; // 增加角阻力
            
            UnityEngine.UI.Image image = segmentGO.AddComponent<UnityEngine.UI.Image>();
            image.sprite = BodySprite;
            image.color = BodyColor;
            image.raycastTarget = true; // 确保可以接收射线检测
            
            // 添加蛇段组件
            SnakeSegment segment = segmentGO.AddComponent<SnakeSegment>();
            segment.Initialize(this, type, index, BodyColor, isJoint);
            
            // 添加碰撞体
            BoxCollider2D collider = segmentGO.AddComponent<BoxCollider2D>();
            
            // 根据段落类型设置碰撞体大小
            if (isJoint || type == SegmentType.Joint)
            {
                // 转弯关节的碰撞体更小
                collider.size = new Vector2(_grid.CellSize * 0.9f, _grid.CellSize * 0.25f);
            }
            else
            {
                // 蛇头、蛇身、蛇尾的碰撞体略小于视觉大小
                collider.size = new Vector2(_grid.CellSize * 0.9f, _grid.CellSize * 0.9f);
            }
            
            Debug.Log($"创建蛇段：{type} at index {index}, 是否关节：{isJoint}");
            
            return segment;
        }

        /// <summary>
        /// 设置铰链关节连接
        /// </summary>
        private void SetupHingeJoints()
        {
            Debug.Log($"设置铰链关节，段落数：{_segments.Count}");
            
            // 确保物理引擎已经更新了所有段落的位置
            Physics2D.SyncTransforms();
            
            for (int i = 1; i < _segments.Count; i++)
            {
                SnakeSegment currentSegment = _segments[i];
                SnakeSegment previousSegment = _segments[i - 1];
                
                if (currentSegment == null || previousSegment == null)
                {
                    Debug.LogError($"段落 {i} 或 {i-1} 为空，无法设置铰链关节");
                    continue;
                }
                
                // 计算连接方向
                Vector2 currentPos = currentSegment.GetPosition();
                Vector2 previousPos = previousSegment.GetPosition();
                Vector2 direction = currentPos - previousPos;
                
                // 将世界坐标方向转换为网格方向
                Vector2Int gridDirection = new Vector2Int(
                    Mathf.RoundToInt(direction.normalized.x),
                    Mathf.RoundToInt(direction.normalized.y)
                );
                
                // 设置铰链关节，传入连接方向信息
                currentSegment.SetupHingeJoint(previousSegment.Rigidbody, gridDirection);
                
                Debug.Log($"连接段落 {i-1}({previousSegment.Type}) -> {i}({currentSegment.Type}), 方向：{gridDirection}");
            }
            
            // 再次同步物理引擎
            Physics2D.SyncTransforms();
        }

        /// <summary>
        /// 清理所有段落
        /// </summary>
        private void ClearSegments()
        {
            // 创建一个临时列表来存储段落，避免在遍历时修改集合
            var segmentsToDestroy = new List<SnakeSegment>(_segments);
            
            foreach (var segment in segmentsToDestroy)
            {
                if (segment != null && segment.gameObject != null)
                {
                    // 使用Destroy而不是DestroyImmediate，避免多次销毁问题
                    Destroy(segment.gameObject);
                }
            }
            
            _segments.Clear();
            _headSegment = null;
            _tailSegment = null;
        }

        #endregion

        #region 初始位置设置

        /// <summary>
        /// 设置初始位置
        /// </summary>
        private void SetupInitialPositions()
        {
            // 从配置获取初始位置，或使用默认位置
            Vector2Int[] bodyCells = GetInitialBodyCells();
            
            if (bodyCells == null || bodyCells.Length < 2 || _segments.Count == 0)
            {
                Debug.LogError("配置的格子不足或段落为空，无法设置初始位置");
                return;
            }
            
            Debug.Log($"设置蛇初始位置，段落数：{_segments.Count}，配置格子数：{bodyCells.Length}");
            
            // 计算各段落位置
            int segmentIndex = 0;
            
            // 设置蛇头位置
            Vector3 headPos = _grid.CellToWorld(bodyCells[0]);
            _segments[segmentIndex++].SetPosition(headPos);
            _currentHeadCell = bodyCells[0];
            
            // 设置身体和转弯关节位置
            for (int i = 1; i < bodyCells.Length; i++)
            {
                Vector2Int prevCell = bodyCells[i - 1];
                Vector2Int currentCell = bodyCells[i];
                Vector3 prevWorldPos = _grid.CellToWorld(prevCell);
                Vector3 currentWorldPos = _grid.CellToWorld(currentCell);
                
                // 计算方向和距离
                Vector3 worldDirection = (currentWorldPos - prevWorldPos).normalized;
                float distance = Vector3.Distance(prevWorldPos, currentWorldPos);
                
                // 计算两个格子之间的方向向量
                Vector2Int gridDirection = currentCell - prevCell;
                
                // 如果是最后一个格子，只有3个转弯关节和蛇尾
                if (i == bodyCells.Length - 1)
                {
                    // 设置3个转弯关节位置 - 严格对齐到网格
                    for (int j = 0; j < 3; j++)
                    {
                        // 计算关节在网格上的精确位置
                        Vector2Int jointCell = prevCell + new Vector2Int(
                            Mathf.RoundToInt(gridDirection.x * ((j + 1) / 4.0f)),
                            Mathf.RoundToInt(gridDirection.y * ((j + 1) / 4.0f))
                        );
                        Vector3 jointPos = _grid.CellToWorld(jointCell);
                        
                        // 设置关节位置
                        _segments[segmentIndex].SetPosition(jointPos);
                        
                        // 设置关节旋转 - 使其垂直于移动方向
                        float angle = 0;
                        if (gridDirection.x != 0) // 水平移动
                        {
                            angle = 90; // 垂直放置关节
                        }
                        _segments[segmentIndex].SetRotation(angle);
                        
                        segmentIndex++;
                    }
                    
                    // 设置蛇尾位置 - 严格对齐到网格中心
                    _segments[segmentIndex++].SetPosition(currentWorldPos);
                    _currentTailCell = currentCell;
                }
                else
                {
                    // 设置3个转弯关节位置 - 严格对齐到网格
                    for (int j = 0; j < 3; j++)
                    {
                        // 计算关节在网格上的精确位置
                        Vector2Int jointCell = prevCell + new Vector2Int(
                            Mathf.RoundToInt(gridDirection.x * ((j + 1) / 4.0f)),
                            Mathf.RoundToInt(gridDirection.y * ((j + 1) / 4.0f))
                        );
                        Vector3 jointPos = _grid.CellToWorld(jointCell);
                        
                        // 设置关节位置
                        _segments[segmentIndex].SetPosition(jointPos);
                        
                        // 设置关节旋转 - 使其垂直于移动方向
                        float angle = 0;
                        if (gridDirection.x != 0) // 水平移动
                        {
                            angle = 90; // 垂直放置关节
                        }
                        _segments[segmentIndex].SetRotation(angle);
                        
                        segmentIndex++;
                    }
                    
                    // 设置身体段落位置 - 严格对齐到网格中心
                    _segments[segmentIndex++].SetPosition(currentWorldPos);
                }
            }
            
            Debug.Log($"设置完成，总段落数：{segmentIndex}，实际段落数：{_segments.Count}");
            
            // 确保所有段落都设置了位置
            if (segmentIndex != _segments.Count)
            {
                Debug.LogWarning($"段落数量不匹配：预期 {_segments.Count}，实际设置了 {segmentIndex}");
            }
            
            // 延迟一帧设置铰链关节，确保所有段落位置已经正确设置
            StartCoroutine(DelayedSetupJoints(bodyCells));
        }
        
        /// <summary>
        /// 延迟设置铰链关节
        /// </summary>
        private System.Collections.IEnumerator DelayedSetupJoints(Vector2Int[] bodyCells)
        {
            // 等待一帧
            yield return null;
            
            // 设置铰链关节
            SetupHingeJoints();
        }

        /// <summary>
        /// 获取初始身体格子位置
        /// </summary>
        private Vector2Int[] GetInitialBodyCells()
        {
            // 尝试从SnakeManager获取初始配置
            Vector2Int[] configCells = GetConfiguredBodyCells();
            if (configCells != null && configCells.Length > 0)
            {
                return configCells;
            }
            
            // 使用默认生成
            Vector2Int[] cells = new Vector2Int[Length];
            Vector2Int startCell = new Vector2Int(2, 2); // 默认起始位置，避免边缘
            
            for (int i = 0; i < Length; i++)
            {
                cells[i] = startCell + Vector2Int.down * i; // 垂直排列
            }
            
            return cells;
        }

        /// <summary>
        /// 从配置获取身体格子位置
        /// </summary>
        private Vector2Int[] GetConfiguredBodyCells()
        {
            return _initialBodyCells;
        }

        /// <summary>
        /// 设置初始身体格子配置（由SnakeManager调用）
        /// </summary>
        public void SetInitialBodyCells(Vector2Int[] bodyCells)
        {
            _initialBodyCells = bodyCells;
        }


        #endregion

        #region 拖拽处理

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!IsControllable || !IsAlive()) return;
            
            // 检测点击的是否为可拖拽的段落（蛇头或蛇尾）
            SnakeSegment clickedSegment = GetClickedSegment(eventData);
            if (clickedSegment == null || !clickedSegment.CanTriggerDrag) return;
            
            StartDrag(clickedSegment, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging || _draggedSegment == null) return;
            
            UpdateDrag(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_isDragging)
            {
                EndDrag();
            }
        }

        /// <summary>
        /// 通过射线检测获取点击的段落
        /// </summary>
        private SnakeSegment GetClickedSegmentByRaycast()
        {
            Vector2 mousePosition = Input.mousePosition;
            Debug.Log($"检测点击段落，鼠标位置：{mousePosition}");
            
            // 将屏幕坐标转换为世界坐标
            Vector2 worldPoint = Camera.main.ScreenToWorldPoint(mousePosition);
            Debug.Log($"世界坐标：{worldPoint}");
            
            // 使用OverlapPoint检测点击
            Collider2D hit = Physics2D.OverlapPoint(worldPoint);
            
            if (hit != null)
            {
                // 检查是否是蛇段
                SnakeSegment segment = hit.GetComponent<SnakeSegment>();
                if (segment != null && segment.CanTriggerDrag)
                {
                    Debug.Log($"点击到段落：{segment.Type} at {segment.GetPosition()}");
                    return segment;
                }
            }
            
            // 如果没有直接点击到，尝试使用较小范围的圆形检测
            Collider2D[] nearbyColliders = Physics2D.OverlapCircleAll(worldPoint, 0.5f);
            foreach (var collider in nearbyColliders)
            {
                SnakeSegment segment = collider.GetComponent<SnakeSegment>();
                if (segment != null && segment.CanTriggerDrag)
                {
                    Debug.Log($"附近找到段落：{segment.Type} at {segment.GetPosition()}");
                    return segment;
                }
            }
            
            Debug.Log("未点击到可拖拽的段落");
            return null;
        }
        
        /// <summary>
        /// 通过UI事件获取点击的段落（用于EventSystem）
        /// </summary>
        private SnakeSegment GetClickedSegment(PointerEventData eventData)
        {
            Debug.Log($"检测点击段落，事件位置：{eventData.position}");
            
            // 检查每个可拖拽的段落
            foreach (var segment in _segments)
            {
                if (!segment.CanTriggerDrag) continue;
                
                // 直接检查点击是否在段落的RectTransform范围内
                RectTransform segmentRect = segment.RectTransform;
                Vector2 localPoint;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    segmentRect, eventData.position, eventData.pressEventCamera, out localPoint))
                {
                    // 检查点击点是否在段落范围内
                    Rect rect = segmentRect.rect;
                    if (rect.Contains(localPoint))
                    {
                        Debug.Log($"点击到段落：{segment.Type} at {segment.GetPosition()}");
                        return segment;
                    }
                }
            }
            
            Debug.Log("未点击到可拖拽的段落");
            return null;
        }

        /// <summary>
        /// 通过鼠标开始拖拽
        /// </summary>
        private void StartDragByMouse(SnakeSegment segment)
        {
            _isDragging = true;
            _draggedSegment = segment;
            _isAligning = false;
            
            // 获取鼠标在世界坐标中的位置
            Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            
            _dragStartPosition = mouseWorldPos;
            _lastValidPosition = segment.GetPosition();
            
            // 重置方向
            _primaryDirection = Vector2.zero;
            _secondaryDirection = Vector2.zero;
            
            Debug.Log($"开始拖拽 {segment.Type} 段落，鼠标位置：{mouseWorldPos}");
        }
        
        /// <summary>
        /// 通过UI事件开始拖拽（用于EventSystem）
        /// </summary>
        private void StartDrag(SnakeSegment segment, PointerEventData eventData)
        {
            _isDragging = true;
            _draggedSegment = segment;
            _isAligning = false;
            
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                transform as RectTransform, eventData.position, eventData.pressEventCamera, out localPoint);
            
            _dragStartPosition = localPoint;
            _lastValidPosition = segment.GetPosition();
            
            // 重置方向
            _primaryDirection = Vector2.zero;
            _secondaryDirection = Vector2.zero;
            
            Debug.Log($"开始拖拽 {segment.Type} 段落，事件位置：{eventData.position}");
        }

        /// <summary>
        /// 通过鼠标更新拖拽
        /// </summary>
        private void UpdateDragByMouse()
        {
            // 获取鼠标在世界坐标中的位置
            Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            
            Vector2 dragDelta = mouseWorldPos - _dragStartPosition;
            
            // 检查是否达到最小拖拽距离
            if (dragDelta.magnitude < _minDragDistance) return;
            
            // 确定主要移动方向
            UpdateMovementDirection(dragDelta);
            
            // 计算目标位置
            Vector2 targetPosition = CalculateDragTargetPosition(dragDelta);
            
            // 应用拖拽移动
            ApplyDragMovement(targetPosition);
        }
        
        /// <summary>
        /// 通过UI事件更新拖拽（用于EventSystem）
        /// </summary>
        private void UpdateDrag(PointerEventData eventData)
        {
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                transform as RectTransform, eventData.position, eventData.pressEventCamera, out localPoint);
            
            Vector2 dragDelta = localPoint - _dragStartPosition;
            
            // 检查是否达到最小拖拽距离
            if (dragDelta.magnitude < _minDragDistance) return;
            
            // 确定主要移动方向
            UpdateMovementDirection(dragDelta);
            
            // 计算目标位置
            Vector2 targetPosition = CalculateDragTargetPosition(dragDelta);
            
            // 应用拖拽移动
            ApplyDragMovement(targetPosition);
        }

        /// <summary>
        /// 更新移动方向
        /// </summary>
        private void UpdateMovementDirection(Vector2 dragDelta)
        {
            // 确定主要方向（水平或垂直）
            if (Mathf.Abs(dragDelta.x) > Mathf.Abs(dragDelta.y))
            {
                _primaryDirection = dragDelta.x > 0 ? Vector2.right : Vector2.left;
                _secondaryDirection = Vector2.up;
            }
            else
            {
                _primaryDirection = dragDelta.y > 0 ? Vector2.up : Vector2.down;
                _secondaryDirection = Vector2.right;
            }
        }

        /// <summary>
        /// 计算拖拽目标位置
        /// </summary>
        private Vector2 CalculateDragTargetPosition(Vector2 dragDelta)
        {
            Vector2 currentPos = _draggedSegment.GetPosition();
            
            // 主方向移动：允许连续移动
            float primaryMovement = Vector2.Dot(dragDelta, _primaryDirection);
            Vector2 primaryOffset = _primaryDirection * primaryMovement;
            
            // 计算当前所在的网格单元
            Vector2Int currentCell = _grid.WorldToCell(currentPos);
            Vector2 gridCenterPos = _grid.CellToWorld(currentCell);
            
            // 计算主方向上移动的网格单元数
            int cellsToMove = Mathf.RoundToInt(primaryMovement / _grid.CellSize);
            
            // 计算目标网格单元
            Vector2Int targetCell = currentCell;
            if (_primaryDirection.x != 0)
            {
                targetCell.x += cellsToMove;
            }
            else if (_primaryDirection.y != 0)
            {
                targetCell.y += cellsToMove;
            }
            
            // 确保目标单元在网格范围内
            targetCell.x = Mathf.Clamp(targetCell.x, 0, _grid.Width - 1);
            targetCell.y = Mathf.Clamp(targetCell.y, 0, _grid.Height - 1);
            
            // 获取目标单元的世界坐标（严格对齐网格中心）
            Vector2 targetPos = _grid.CellToWorld(targetCell);
            
            return targetPos;
        }

        /// <summary>
        /// 应用拖拽移动
        /// </summary>
        private void ApplyDragMovement(Vector2 targetPosition)
        {
            // 计算目标网格位置
            Vector2Int targetCell = _grid.WorldToCell(targetPosition);
            
            // 确保目标单元在网格范围内
            targetCell.x = Mathf.Clamp(targetCell.x, 0, _grid.Width - 1);
            targetCell.y = Mathf.Clamp(targetCell.y, 0, _grid.Height - 1);
            
            // 获取严格对齐到网格的世界坐标
            Vector2 alignedPos = _grid.CellToWorld(targetCell);
            
            // 平滑移动到目标位置
            Vector2 currentPos = _draggedSegment.GetPosition();
            Vector2 newPos = Vector2.Lerp(currentPos, alignedPos, Time.deltaTime * _dragSmoothness);
            
            // 设置位置
            _draggedSegment.SetPosition(newPos);
            
            // 更新网格位置记录
            if (_draggedSegment.IsHead)
            {
                _currentHeadCell = targetCell;
            }
            else if (_draggedSegment.IsTail)
            {
                _currentTailCell = targetCell;
            }
        }

        /// <summary>
        /// 结束拖拽
        /// </summary>
        private void EndDrag()
        {
            _isDragging = false;
            
            if (_draggedSegment != null)
            {
                // 当前位置应该已经对齐到网格中心，但为了确保，再次计算
                Vector2 currentPos = _draggedSegment.GetPosition();
                Vector2Int nearestCell = _grid.WorldToCell(currentPos);
                Vector2 targetPos = _grid.CellToWorld(nearestCell);
                
                // 立即设置到网格中心，不需要平滑过渡
                _draggedSegment.SetPosition(targetPos);
                
                // 更新网格位置记录
                if (_draggedSegment.IsHead)
                {
                    _currentHeadCell = nearestCell;
                }
                else if (_draggedSegment.IsTail)
                {
                    _currentTailCell = nearestCell;
                }
                
                Debug.Log($"结束拖拽，对齐到网格 {nearestCell}");
            }
            
            _draggedSegment = null;
        }

        #endregion

        #region 网格对齐

        /// <summary>
        /// 开始网格对齐
        /// </summary>
        private void StartGridAlignment(Vector2 targetPosition)
        {
            _isAligning = true;
            _alignmentTarget = targetPosition;
        }

        /// <summary>
        /// 更新网格对齐
        /// </summary>
        private void UpdateGridAlignment()
        {
            if (_headSegment == null) return;
            
            Vector2 currentPos = _headSegment.GetPosition();
            Vector2 newPos = Vector2.MoveTowards(currentPos, _alignmentTarget, Time.deltaTime * _alignmentSpeed);
            
            _headSegment.SetPosition(newPos);
            
            // 检查是否完成对齐
            if (Vector2.Distance(newPos, _alignmentTarget) < 0.01f)
            {
                _headSegment.SetPosition(_alignmentTarget);
                _isAligning = false;
                
                // 更新网格位置记录
                UpdateGridPositionRecord();
            }
        }

        #endregion

        #region 段落位置更新

        /// <summary>
        /// 更新所有段落位置
        /// </summary>
        private void UpdateSegmentPositions()
        {
            // 物理系统会自动处理段落间的连接
            // 这里主要处理一些额外的约束和修正
            
            for (int i = 1; i < _segments.Count; i++)
            {
                SnakeSegment current = _segments[i];
                SnakeSegment previous = _segments[i - 1];
                
                // 确保段落间距不会过大
                Vector2 distance = current.GetPosition() - previous.GetPosition();
                float maxDistance = _segmentSpacing * _grid.CellSize * 1.5f;
                
                if (distance.magnitude > maxDistance)
                {
                    Vector2 direction = distance.normalized;
                    Vector2 previousPos = new Vector2(previous.GetPosition().x, previous.GetPosition().y);
                    Vector2 correctedPos = previousPos + direction * maxDistance;
                    current.SetPosition(correctedPos);
                }
            }
        }

        /// <summary>
        /// 更新拖拽移动逻辑
        /// </summary>
        private void UpdateDragMovement()
        {
            // 拖拽过程中的额外处理
            // 例如：检查碰撞、边界限制等
            
            if (_draggedSegment != null)
            {
                Vector2 currentPos = _draggedSegment.GetPosition();
                Vector2Int currentCell = _grid.WorldToCell(currentPos);
                
                // 检查边界
                if (!_grid.IsInside(currentCell))
                {
                    // 限制在网格范围内
                    Vector2Int clampedCell = new Vector2Int(
                        Mathf.Clamp(currentCell.x, 0, _grid.Width - 1),
                        Mathf.Clamp(currentCell.y, 0, _grid.Height - 1)
                    );
                    Vector2 clampedPos = _grid.CellToWorld(clampedCell);
                    _draggedSegment.SetPosition(clampedPos);
                }
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 更新网格位置记录
        /// </summary>
        private void UpdateGridPositionRecord()
        {
            if (_headSegment != null)
            {
                _currentHeadCell = _grid.WorldToCell(_headSegment.GetPosition());
            }
            
            if (_tailSegment != null)
            {
                _currentTailCell = _grid.WorldToCell(_tailSegment.GetPosition());
            }
        }

        /// <summary>
        /// 获取所有段落的网格位置
        /// </summary>
        public List<Vector2Int> GetAllSegmentCells()
        {
            List<Vector2Int> cells = new List<Vector2Int>();
            foreach (var segment in _segments)
            {
                Vector2Int cell = _grid.WorldToCell(segment.GetPosition());
                cells.Add(cell);
            }
            return cells;
        }

        /// <summary>
        /// 检查指定网格位置是否被蛇占用
        /// </summary>
        public bool IsOccupyingCell(Vector2Int cell)
        {
            foreach (var segment in _segments)
            {
                Vector2Int segmentCell = _grid.WorldToCell(segment.GetPosition());
                if (segmentCell == cell)
                    return true;
            }
            return false;
        }

        #endregion

        #region 调试绘制

        void OnDrawGizmos()
        {
            if (!_showDebugLines || _segments == null) return;
            
            // 绘制段落连接线
            Gizmos.color = Color.yellow;
            for (int i = 1; i < _segments.Count; i++)
            {
                if (_segments[i] != null && _segments[i-1] != null)
                {
                    Vector3 start = _segments[i-1].GetPosition();
                    Vector3 end = _segments[i].GetPosition();
                    Gizmos.DrawLine(start, end);
                }
            }
            
            // 绘制拖拽目标
            if (_isAligning)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(_alignmentTarget, _grid.CellSize * 0.2f);
            }
        }

        #endregion

        #region 清理

        protected override void OnDestroy()
        {
            // 在销毁前断开所有连接，避免多次销毁问题
            foreach (var segment in _segments)
            {
                if (segment != null)
                {
                    // 断开连接而不是销毁
                    var joints = segment.GetComponents<Joint2D>();
                    foreach (var joint in joints)
                    {
                        if (joint != null)
                        {
                            joint.connectedBody = null;
                            joint.enabled = false;
                        }
                    }
                }
            }
            
            base.OnDestroy();
            
            // 使用延迟清理，避免在OnDestroy中直接销毁对象
            if (Application.isPlaying)
            {
                StartCoroutine(DelayedClear());
            }
        }
        
        /// <summary>
        /// 延迟清理段落
        /// </summary>
        private System.Collections.IEnumerator DelayedClear()
        {
            // 等待一帧
            yield return null;
            
            // 清理段落
            ClearSegments();
        }

        #endregion
    }
}
