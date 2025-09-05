// SnakeHead.cs
using UnityEngine;
using System.Collections.Generic;
namespace HingeJointSnake
{

    public class SnakeHeadController : MonoBehaviour
    {
        [Header("Movement Settings")]
        public float moveSpeed = 5f;
        public float dragSensitivity = 1f;
        public float alignmentForce = 10f;
        public float maxVelocity = 3f;

        [Header("Grid Settings")]
        public float gridAlignmentThreshold = 0.2f;

        [Header("Visual Feedback")]
        public GameObject directionIndicator;
        public Color freeMoveColor = Color.yellow;
        public Color alignedColor = Color.green;

        private Rigidbody2D rb;
        private Vector2 currentDirection = Vector2.right;
        private Vector2 targetDirection = Vector2.right;
        private bool isDragging = false;
        private Vector2 dragStartPosition;
        private Vector2 lastGridPosition;
        private bool isAlignedToGrid = true;

        private List<Vector2> positionHistory = new List<Vector2>();
        private int historyLength = 100;
        private SpriteRenderer indicatorRenderer;

        void Start()
        {
            rb = GetComponent<Rigidbody2D>();

            // 初始方向设置为向上
            currentDirection = Vector2.up;
            targetDirection = Vector2.up;

            // 初始对齐到网格中心
            SnapToGridCenter();
            lastGridPosition = transform.position;

            // 设置方向指示器
            if (directionIndicator != null)
            {
                indicatorRenderer = directionIndicator.GetComponent<SpriteRenderer>();
                UpdateDirectionIndicator();
            }

            // 配置物理属性
            ConfigurePhysics();

            // 初始化位置历史
            InitializePositionHistory();
        }

        void Update()
        {
            HandleInput();
            UpdateDirectionIndicator();

            // 限制最大速度
            if (rb.velocity.magnitude > maxVelocity)
            {
                rb.velocity = rb.velocity.normalized * maxVelocity;
            }
        }

        void FixedUpdate()
        {
            ApplyGridAlignment();
            UpdatePositionHistory();
        }

        void HandleInput()
        {
            // 鼠标/触摸按下
            if (Input.GetMouseButtonDown(0))
            {
                Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                Collider2D hit = Physics2D.OverlapPoint(mousePos);

                if (hit != null && hit.gameObject == gameObject)
                {
                    StartDrag(mousePos);
                }
            }

            // 拖动中
            if (isDragging && Input.GetMouseButton(0))
            {
                Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                ContinueDrag(mousePos);
            }

            // 鼠标/触摸释放
            if (Input.GetMouseButtonUp(0) && isDragging)
            {
                EndDrag();
            }
        }

        void StartDrag(Vector2 position)
        {
            isDragging = true;
            dragStartPosition = position;

            // 切换到自由移动颜色
            if (indicatorRenderer != null)
                indicatorRenderer.color = freeMoveColor;
        }

        void ContinueDrag(Vector2 position)
        {
            // 计算拖动偏移量
            Vector2 offset = (position - dragStartPosition) * dragSensitivity;

            // 确定主要移动方向
            if (Mathf.Abs(offset.y) > Mathf.Abs(offset.x))
            {
                // 垂直移动为主方向
                targetDirection = offset.y > 0 ? Vector2.up : Vector2.down;

                // 在主方向上自由移动
                Vector2 movement = new Vector2(0, offset.y);
                rb.AddForce(movement);

                // 在次方向上施加对齐力
                if (Mathf.Abs(offset.x) > 0.1f)
                {
                    float alignForceX = (GridSystem.Instance.GetNearestGridCenter(transform.position).x - transform.position.x) * alignmentForce;
                    rb.AddForce(new Vector2(alignForceX, 0));
                }
            }
            else
            {
                // 水平移动为主方向
                targetDirection = offset.x > 0 ? Vector2.right : Vector2.left;

                // 在主方向上自由移动
                Vector2 movement = new Vector2(offset.x, 0);
                rb.AddForce(movement);

                // 在次方向上施加对齐力
                if (Mathf.Abs(offset.y) > 0.1f)
                {
                    float alignForceY = (GridSystem.Instance.GetNearestGridCenter(transform.position).y - transform.position.y) * alignmentForce;
                    rb.AddForce(new Vector2(0, alignForceY));
                }
            }

            // 检查是否需要更新当前方向
            if (targetDirection != currentDirection && CanChangeDirection())
            {
                currentDirection = targetDirection;
            }
        }

        void EndDrag()
        {
            isDragging = false;

            // 确保对齐到网格
            if (GridSystem.Instance.IsOnGridCenter(transform.position, gridAlignmentThreshold))
            {
                SnapToGridCenter();
            }

            // 切换到对齐颜色
            if (indicatorRenderer != null)
                indicatorRenderer.color = alignedColor;
        }

        void ApplyGridAlignment()
        {
            // 如果不处于拖动状态，确保对齐到网格
            if (!isDragging && GridSystem.Instance.IsOnGridCenter(transform.position, gridAlignmentThreshold))
            {
                SnapToGridCenter();
                isAlignedToGrid = true;
            }
            else if (!isDragging)
            {
                // 施加力使其对齐到网格
                Vector2 gridCenter = GridSystem.Instance.GetNearestGridCenter(transform.position);
                Vector2 alignForce = (gridCenter - (Vector2)transform.position) * alignmentForce;
                rb.AddForce(alignForce);
                isAlignedToGrid = false;
            }
        }

        public void SnapToGridCenter()
        {
            transform.position = GridSystem.Instance.GetNearestGridCenter(transform.position);
            rb.velocity = Vector2.zero;
            lastGridPosition = transform.position;
        }

        bool CanChangeDirection()
        {
            // 只有在网格中心点才能改变方向
            return GridSystem.Instance.IsOnGridCenter(transform.position, gridAlignmentThreshold);
        }

        void UpdateDirectionIndicator()
        {
            if (directionIndicator != null)
            {
                // 更新指示器位置和旋转
                directionIndicator.transform.localPosition = currentDirection * 0.5f;

                float angle = Mathf.Atan2(currentDirection.y, currentDirection.x) * Mathf.Rad2Deg;
                directionIndicator.transform.rotation = Quaternion.Euler(0, 0, angle);

                // 根据对齐状态更新颜色
                if (isAlignedToGrid)
                {
                    indicatorRenderer.color = alignedColor;
                }
                else
                {
                    indicatorRenderer.color = freeMoveColor;
                }
            }
        }

        void ConfigurePhysics()
        {
            // 设置刚体属性
            rb.drag = 2f;
            rb.angularDrag = 5f;
            rb.gravityScale = 0;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        void InitializePositionHistory()
        {
            for (int i = 0; i < historyLength; i++)
            {
                positionHistory.Add(transform.position);
            }
        }

        void UpdatePositionHistory()
        {
            positionHistory.Add(transform.position);
            if (positionHistory.Count > historyLength)
            {
                positionHistory.RemoveAt(0);
            }
        }

        public List<Vector2> GetPositionHistory()
        {
            return new List<Vector2>(positionHistory);
        }

        public Vector2 GetCurrentDirection()
        {
            return currentDirection;
        }

        public bool IsAlignedToGrid()
        {
            return isAlignedToGrid;
        }

        void OnDrawGizmos()
        {
            // 绘制当前方向
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, transform.position + (Vector3)currentDirection * 0.8f);

            // 绘制网格对齐状态
            if (isAlignedToGrid)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(transform.position, 0.2f);
            }
            else
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position, 0.15f);

                // 绘制对齐目标
                Vector2 gridCenter = GridSystem.Instance.GetNearestGridCenter(transform.position);
                Gizmos.DrawLine(transform.position, gridCenter);
                Gizmos.DrawWireSphere(gridCenter, 0.1f);
            }
        }
    }
}