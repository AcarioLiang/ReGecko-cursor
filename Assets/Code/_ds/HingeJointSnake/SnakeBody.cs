// SnakeBody.cs
using UnityEngine;
using System.Collections.Generic;
namespace HingeJointSnake
{

    public class SnakeBodyPart : MonoBehaviour
    {
        public int segmentIndex = 1;
        public float followSmoothness = 8f;
        public float maxDistance = 0.8f;

        private Rigidbody2D rb;
        private HingeJoint2D hingeJoint;
        private DistanceJoint2D distanceJoint;
        private SnakeHeadController head;

        void Start()
        {
            rb = GetComponent<Rigidbody2D>();
            hingeJoint = GetComponent<HingeJoint2D>();
            distanceJoint = GetComponent<DistanceJoint2D>();
            head = FindObjectOfType<SnakeHeadController>();

            ConfigurePhysics();
            ConfigureJoints();
        }

        void FixedUpdate()
        {
            if (head == null) return;

            FollowHead();
            MaintainDistance();
        }

        void FollowHead()
        {
            // 获取头部位置历史
            List<Vector2> history = head.GetPositionHistory();
            int targetIndex = Mathf.Max(0, history.Count - 1 - segmentIndex * 5);

            if (targetIndex < history.Count)
            {
                Vector2 targetPosition = history[targetIndex];

                // 计算移动方向
                Vector2 direction = (targetPosition - rb.position).normalized;
                float distance = Vector2.Distance(rb.position, targetPosition);

                // 应用力来移动身体部分
                if (distance > 0.1f)
                {
                    rb.AddForce(direction * followSmoothness * distance);
                }

                // 对于Y轴向上的排列，添加额外的向上力
                if (segmentIndex > 1)
                {
                    // 获取前一个身体部分的位置
                    SnakeBodyPart[] allParts = FindObjectsOfType<SnakeBodyPart>();
                    SnakeBodyPart prevPart = null;

                    foreach (SnakeBodyPart part in allParts)
                    {
                        if (part.segmentIndex == segmentIndex - 1)
                        {
                            prevPart = part;
                            break;
                        }
                    }

                    if (prevPart != null)
                    {
                        // 计算理想位置（前一个身体部分的正上方）
                        Vector2 idealPosition = (Vector2)prevPart.transform.position + Vector2.up * 0.6f;

                        // 施加力使身体部分保持在理想位置
                        Vector2 positionError = idealPosition - (Vector2)transform.position;
                        rb.AddForce(positionError * 5f);
                    }
                }
            }
        }

        void MaintainDistance()
        {
            if (hingeJoint != null && hingeJoint.connectedBody != null)
            {
                float distance = Vector2.Distance(transform.position, hingeJoint.connectedBody.position);

                // 如果距离太远，使用距离关节拉回
                if (distance > maxDistance && distanceJoint != null)
                {
                    distanceJoint.distance = maxDistance * 0.9f;
                    distanceJoint.enabled = true;
                }
                else if (distanceJoint != null)
                {
                    distanceJoint.enabled = false;
                }
            }
        }

        void ConfigurePhysics()
        {
            if (rb != null)
            {
                // 设置刚体属性
                rb.drag = 3f;
                rb.angularDrag = 4f;
                rb.gravityScale = 0;
                rb.mass = 0.5f; // 身体部分比头部轻
            }
        }

        void ConfigureJoints()
        {
            if (hingeJoint != null)
            {
                // 配置铰链关节
                hingeJoint.autoConfigureConnectedAnchor = false;
                hingeJoint.anchor = Vector2.zero;
                hingeJoint.connectedAnchor = Vector2.zero;

                // 设置关节限制
                JointAngleLimits2D limits = hingeJoint.limits;
                limits.min = -15f;
                limits.max = 15f;
                hingeJoint.limits = limits;
                hingeJoint.useLimits = true;
            }

            if (distanceJoint != null)
            {
                // 配置距离关节
                distanceJoint.autoConfigureDistance = false;
                distanceJoint.distance = maxDistance * 0.9f;
                distanceJoint.maxDistanceOnly = true;
                distanceJoint.enabled = false; // 默认禁用，只在需要时启用
            }
        }

        public void ConnectToBody(Rigidbody2D previousBody)
        {
            if (hingeJoint != null)
            {
                hingeJoint.connectedBody = previousBody;
            }

            if (distanceJoint != null)
            {
                distanceJoint.connectedBody = previousBody;
            }
        }
    }
}