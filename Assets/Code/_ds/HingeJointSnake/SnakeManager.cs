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
            // 清除现有的身体部分
            foreach (GameObject part in bodyParts)
            {
                if (part != null) Destroy(part);
            }
            bodyParts.Clear();

            Rigidbody2D previousBody = head.GetComponent<Rigidbody2D>();

            for (int i = 0; i < initialBodyParts; i++)
            {
                // 计算位置 - 沿Y轴向上生成
                Vector2 position = head.transform.position + Vector3.up * spacing * (i + 1);

                // 创建身体部分
                GameObject bodyPart = Instantiate(bodyPartPrefab, position, Quaternion.identity, transform);
                bodyPart.name = $"BodyPart_{i + 1}";

                // 设置组件
                SnakeBodyPart bodyScript = bodyPart.GetComponent<SnakeBodyPart>();
                bodyScript.segmentIndex = i + 1;

                // 连接到前一个身体部分
                bodyScript.ConnectToBody(previousBody);

                // 调整关节锚点以适应Y轴向上的连接
                AdjustJointsForUpwardConnection(bodyPart, previousBody);

                bodyParts.Add(bodyPart);
                previousBody = bodyPart.GetComponent<Rigidbody2D>();
            }
        }

        void AdjustJointsForUpwardConnection(GameObject currentBody, Rigidbody2D previousBody)
        {
            // 调整HingeJoint2D
            HingeJoint2D hingeJoint = currentBody.GetComponent<HingeJoint2D>();
            if (hingeJoint != null && previousBody != null)
            {
                // 设置锚点 - 当前身体的下边缘
                hingeJoint.anchor = new Vector2(0, -0.5f);

                // 设置连接锚点 - 前一个身体的上边缘
                hingeJoint.connectedAnchor = new Vector2(0, 0.5f);

                // 调整关节限制以适应垂直连接
                JointAngleLimits2D limits = hingeJoint.limits;
                limits.min = -10f;
                limits.max = 10f;
                hingeJoint.limits = limits;
            }

            // 调整DistanceJoint2D
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
                // 如果没有身体部分，从头部开始创建
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

                // 连接到最后一个身体部分
                bodyScript.ConnectToBody(lastPart.GetComponent<Rigidbody2D>());
                AdjustJointsForUpwardConnection(newPart, lastPart.GetComponent<Rigidbody2D>());

                bodyParts.Add(newPart);
            }

            
        }

        // 更新所有身体部分的位置，确保它们沿Y轴向上排列
        public void RealignBodyParts()
        {
            if (bodyParts.Count == 0) return;

            // 从头部开始重新排列
            Vector2 currentPosition = head.transform.position;

            foreach (GameObject bodyPart in bodyParts)
            {
                currentPosition += Vector2.up * spacing;
                bodyPart.transform.position = currentPosition;

                // 重置物理状态
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