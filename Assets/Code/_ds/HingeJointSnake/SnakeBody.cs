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
            // ��ȡͷ��λ����ʷ
            List<Vector2> history = head.GetPositionHistory();
            int targetIndex = Mathf.Max(0, history.Count - 1 - segmentIndex * 5);

            if (targetIndex < history.Count)
            {
                Vector2 targetPosition = history[targetIndex];

                // �����ƶ�����
                Vector2 direction = (targetPosition - rb.position).normalized;
                float distance = Vector2.Distance(rb.position, targetPosition);

                // Ӧ�������ƶ����岿��
                if (distance > 0.1f)
                {
                    rb.AddForce(direction * followSmoothness * distance);
                }

                // ����Y�����ϵ����У���Ӷ����������
                if (segmentIndex > 1)
                {
                    // ��ȡǰһ�����岿�ֵ�λ��
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
                        // ��������λ�ã�ǰһ�����岿�ֵ����Ϸ���
                        Vector2 idealPosition = (Vector2)prevPart.transform.position + Vector2.up * 0.6f;

                        // ʩ����ʹ���岿�ֱ���������λ��
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

                // �������̫Զ��ʹ�þ���ؽ�����
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
                // ���ø�������
                rb.drag = 3f;
                rb.angularDrag = 4f;
                rb.gravityScale = 0;
                rb.mass = 0.5f; // ���岿�ֱ�ͷ����
            }
        }

        void ConfigureJoints()
        {
            if (hingeJoint != null)
            {
                // ���ý����ؽ�
                hingeJoint.autoConfigureConnectedAnchor = false;
                hingeJoint.anchor = Vector2.zero;
                hingeJoint.connectedAnchor = Vector2.zero;

                // ���ùؽ�����
                JointAngleLimits2D limits = hingeJoint.limits;
                limits.min = -15f;
                limits.max = 15f;
                hingeJoint.limits = limits;
                hingeJoint.useLimits = true;
            }

            if (distanceJoint != null)
            {
                // ���þ���ؽ�
                distanceJoint.autoConfigureDistance = false;
                distanceJoint.distance = maxDistance * 0.9f;
                distanceJoint.maxDistanceOnly = true;
                distanceJoint.enabled = false; // Ĭ�Ͻ��ã�ֻ����Ҫʱ����
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