// GridBoundary.cs
using UnityEngine;

namespace HingeJointSnake
{

    public class GridBoundary : MonoBehaviour
    {
        public int gridSize = 10;
        public float cellSize = 1f;

        void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Snake"))
            {
                // 如果蛇离开网格，将其传送到对面
                Vector3 position = other.transform.position;

                if (position.x < 0) position.x = gridSize * cellSize;
                else if (position.x > gridSize * cellSize) position.x = 0;

                if (position.y < 0) position.y = gridSize * cellSize;
                else if (position.y > gridSize * cellSize) position.y = 0;

                other.transform.position = position;

                // 通知蛇头对齐到网格
                SnakeHeadController head = other.GetComponent<SnakeHeadController>();
                if (head != null)
                {
                    head.SnapToGridCenter();
                }
            }
        }
    }
}
