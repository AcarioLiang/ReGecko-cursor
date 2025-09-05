// SnakeManager.cs
using UnityEngine;
using System.Collections.Generic;
namespace HingeJointSnake
{

    public class SnakeBodyManager : MonoBehaviour
    {
        public GameObject bodyPartPrefab;
        public int initialBodyParts = 3;
        public float spacing = 0.6f;

        private List<GameObject> bodyParts = new List<GameObject>();
        private SnakeHeadController head;

        void Start()
        {
            head = FindObjectOfType<SnakeHeadController>();
            GenerateBody();
        }

        void GenerateBody()
        {
            // ������е����岿��
            foreach (GameObject part in bodyParts)
            {
                if (part != null) Destroy(part);
            }
            bodyParts.Clear();

            Rigidbody2D previousBody = head.GetComponent<Rigidbody2D>();

            for (int i = 0; i < initialBodyParts; i++)
            {
                // ����λ�� - ��Y����������
                Vector2 position = head.transform.position + Vector3.up * spacing * (i + 1);

                // �������岿��
                GameObject bodyPart = Instantiate(bodyPartPrefab, position, Quaternion.identity, transform);
                bodyPart.name = $"BodyPart_{i + 1}";

                // �������
                SnakeBodyPart bodyScript = bodyPart.GetComponent<SnakeBodyPart>();
                bodyScript.segmentIndex = i + 1;

                // ���ӵ�ǰһ�����岿��
                bodyScript.ConnectToBody(previousBody);

                // �����ؽ�ê������ӦY�����ϵ�����
                AdjustJointsForUpwardConnection(bodyPart, previousBody);

                bodyParts.Add(bodyPart);
                previousBody = bodyPart.GetComponent<Rigidbody2D>();
            }
        }

        void AdjustJointsForUpwardConnection(GameObject currentBody, Rigidbody2D previousBody)
        {
            // ����HingeJoint2D
            HingeJoint2D hingeJoint = currentBody.GetComponent<HingeJoint2D>();
            if (hingeJoint != null && previousBody != null)
            {
                // ����ê�� - ��ǰ������±�Ե
                hingeJoint.anchor = new Vector2(0, -0.5f);

                // ��������ê�� - ǰһ��������ϱ�Ե
                hingeJoint.connectedAnchor = new Vector2(0, 0.5f);

                // �����ؽ���������Ӧ��ֱ����
                JointAngleLimits2D limits = hingeJoint.limits;
                limits.min = -10f;
                limits.max = 10f;
                hingeJoint.limits = limits;
            }

            // ����DistanceJoint2D
            DistanceJoint2D distanceJoint = currentBody.GetComponent<DistanceJoint2D>();
            if (distanceJoint != null)
            {
                distanceJoint.distance = spacing * 0.9f;
            }
        }

        public void AddBodyPart()
        {
            if (bodyParts.Count == 0)
            {
                // ���û�����岿�֣���ͷ����ʼ����
                Rigidbody2D previousBody = head.GetComponent<Rigidbody2D>();
                Vector2 position1 = head.transform.position + Vector3.up * spacing;

                GameObject newPart = Instantiate(bodyPartPrefab, position1, Quaternion.identity, transform);
                newPart.name = "BodyPart_1";

                SnakeBodyPart bodyScript = newPart.GetComponent<SnakeBodyPart>();
                bodyScript.segmentIndex = 1;
                bodyScript.ConnectToBody(previousBody);
                AdjustJointsForUpwardConnection(newPart, previousBody);

                bodyParts.Add(newPart);
                return;
            }
            else
            {
                GameObject lastPart = bodyParts[bodyParts.Count - 1];
                Vector2 position = lastPart.transform.position + Vector3.up * spacing;

                GameObject newPart = Instantiate(bodyPartPrefab, position, Quaternion.identity, transform);
                newPart.name = $"BodyPart_{bodyParts.Count + 1}";

                SnakeBodyPart bodyScript = newPart.GetComponent<SnakeBodyPart>();
                bodyScript.segmentIndex = bodyParts.Count + 1;

                // ���ӵ����һ�����岿��
                bodyScript.ConnectToBody(lastPart.GetComponent<Rigidbody2D>());
                AdjustJointsForUpwardConnection(newPart, lastPart.GetComponent<Rigidbody2D>());

                bodyParts.Add(newPart);
            }

            
        }

        // �����������岿�ֵ�λ�ã�ȷ��������Y����������
        public void RealignBodyParts()
        {
            if (bodyParts.Count == 0) return;

            // ��ͷ����ʼ��������
            Vector2 currentPosition = head.transform.position;

            foreach (GameObject bodyPart in bodyParts)
            {
                currentPosition += Vector2.up * spacing;
                bodyPart.transform.position = currentPosition;

                // ��������״̬
                Rigidbody2D rb = bodyPart.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.velocity = Vector2.zero;
                    rb.angularVelocity = 0;
                }
            }
        }

        [ContextMenu("Regenerate Body")]
        void RegenerateBody()
        {
            GenerateBody();
        }

        [ContextMenu("Add Body Part")]
        void AddBodyPartContext()
        {
            AddBodyPart();
        }

        [ContextMenu("Realign Body Parts")]
        void RealignBodyPartsContext()
        {
            RealignBodyParts();
        }
    }
}