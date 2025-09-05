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

            // ��ʼ��������Ϊ����
            currentDirection = Vector2.up;
            targetDirection = Vector2.up;

            // ��ʼ���뵽��������
            SnapToGridCenter();
            lastGridPosition = transform.position;

            // ���÷���ָʾ��
            if (directionIndicator != null)
            {
                indicatorRenderer = directionIndicator.GetComponent<SpriteRenderer>();
                UpdateDirectionIndicator();
            }

            // ������������
            ConfigurePhysics();

            // ��ʼ��λ����ʷ
            InitializePositionHistory();
        }

        void Update()
        {
            HandleInput();
            UpdateDirectionIndicator();

            // ��������ٶ�
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
            // ���/��������
            if (Input.GetMouseButtonDown(0))
            {
                Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                Collider2D hit = Physics2D.OverlapPoint(mousePos);

                if (hit != null && hit.gameObject == gameObject)
                {
                    StartDrag(mousePos);
                }
            }

            // �϶���
            if (isDragging && Input.GetMouseButton(0))
            {
                Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                ContinueDrag(mousePos);
            }

            // ���/�����ͷ�
            if (Input.GetMouseButtonUp(0) && isDragging)
            {
                EndDrag();
            }
        }

        void StartDrag(Vector2 position)
        {
            isDragging = true;
            dragStartPosition = position;

            // �л��������ƶ���ɫ
            if (indicatorRenderer != null)
                indicatorRenderer.color = freeMoveColor;
        }

        void ContinueDrag(Vector2 position)
        {
            // �����϶�ƫ����
            Vector2 offset = (position - dragStartPosition) * dragSensitivity;

            // ȷ����Ҫ�ƶ�����
            if (Mathf.Abs(offset.y) > Mathf.Abs(offset.x))
            {
                // ��ֱ�ƶ�Ϊ������
                targetDirection = offset.y > 0 ? Vector2.up : Vector2.down;

                // ���������������ƶ�
                Vector2 movement = new Vector2(0, offset.y);
                rb.AddForce(movement);

                // �ڴη�����ʩ�Ӷ�����
                if (Mathf.Abs(offset.x) > 0.1f)
                {
                    float alignForceX = (GridSystem.Instance.GetNearestGridCenter(transform.position).x - transform.position.x) * alignmentForce;
                    rb.AddForce(new Vector2(alignForceX, 0));
                }
            }
            else
            {
                // ˮƽ�ƶ�Ϊ������
                targetDirection = offset.x > 0 ? Vector2.right : Vector2.left;

                // ���������������ƶ�
                Vector2 movement = new Vector2(offset.x, 0);
                rb.AddForce(movement);

                // �ڴη�����ʩ�Ӷ�����
                if (Mathf.Abs(offset.y) > 0.1f)
                {
                    float alignForceY = (GridSystem.Instance.GetNearestGridCenter(transform.position).y - transform.position.y) * alignmentForce;
                    rb.AddForce(new Vector2(0, alignForceY));
                }
            }

            // ����Ƿ���Ҫ���µ�ǰ����
            if (targetDirection != currentDirection && CanChangeDirection())
            {
                currentDirection = targetDirection;
            }
        }

        void EndDrag()
        {
            isDragging = false;

            // ȷ�����뵽����
            if (GridSystem.Instance.IsOnGridCenter(transform.position, gridAlignmentThreshold))
            {
                SnapToGridCenter();
            }

            // �л���������ɫ
            if (indicatorRenderer != null)
                indicatorRenderer.color = alignedColor;
        }

        void ApplyGridAlignment()
        {
            // ����������϶�״̬��ȷ�����뵽����
            if (!isDragging && GridSystem.Instance.IsOnGridCenter(transform.position, gridAlignmentThreshold))
            {
                SnapToGridCenter();
                isAlignedToGrid = true;
            }
            else if (!isDragging)
            {
                // ʩ����ʹ����뵽����
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
            // ֻ�����������ĵ���ܸı䷽��
            return GridSystem.Instance.IsOnGridCenter(transform.position, gridAlignmentThreshold);
        }

        void UpdateDirectionIndicator()
        {
            if (directionIndicator != null)
            {
                // ����ָʾ��λ�ú���ת
                directionIndicator.transform.localPosition = currentDirection * 0.5f;

                float angle = Mathf.Atan2(currentDirection.y, currentDirection.x) * Mathf.Rad2Deg;
                directionIndicator.transform.rotation = Quaternion.Euler(0, 0, angle);

                // ���ݶ���״̬������ɫ
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
            // ���ø�������
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
            // ���Ƶ�ǰ����
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, transform.position + (Vector3)currentDirection * 0.8f);

            // �����������״̬
            if (isAlignedToGrid)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(transform.position, 0.2f);
            }
            else
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position, 0.15f);

                // ���ƶ���Ŀ��
                Vector2 gridCenter = GridSystem.Instance.GetNearestGridCenter(transform.position);
                Gizmos.DrawLine(transform.position, gridCenter);
                Gizmos.DrawWireSphere(gridCenter, 0.1f);
            }
        }
    }
}