// GridSystem.cs
using UnityEngine;
namespace HingeJointSnake
{

    public class GridSystem : MonoBehaviour
    {
        public static GridSystem Instance;

        public int gridSize = 10;
        public float cellSize = 1f;
        public LayerMask gridLayer;

        private Vector2[,] gridCenters;

        void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);

            InitializeGrid();
        }

        void InitializeGrid()
        {
            gridCenters = new Vector2[gridSize, gridSize];

            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    // 调整网格中心点计算，确保从底部开始
                    gridCenters[x, y] = new Vector2(
                        x * cellSize + cellSize / 2,
                        y * cellSize + cellSize / 2
                    );
                }
            }
        }

        public Vector2 GetNearestGridCenter(Vector2 position)
        {
            int gridX = Mathf.Clamp(Mathf.RoundToInt((position.x - cellSize / 2) / cellSize), 0, gridSize - 1);
            int gridY = Mathf.Clamp(Mathf.RoundToInt((position.y - cellSize / 2) / cellSize), 0, gridSize - 1);

            return gridCenters[gridX, gridY];
        }

        public Vector2 GetGridPosition(Vector2Int gridCoords)
        {
            if (gridCoords.x < 0 || gridCoords.x >= gridSize || gridCoords.y < 0 || gridCoords.y >= gridSize)
                return Vector2.zero;

            return gridCenters[gridCoords.x, gridCoords.y];
        }

        public Vector2Int GetGridCoordinates(Vector2 position)
        {
            int gridX = Mathf.Clamp(Mathf.FloorToInt(position.x / cellSize), 0, gridSize - 1);
            int gridY = Mathf.Clamp(Mathf.FloorToInt(position.y / cellSize), 0, gridSize - 1);

            return new Vector2Int(gridX, gridY);
        }

        public bool IsOnGridCenter(Vector2 position, float threshold = 0.1f)
        {
            Vector2 nearestCenter = GetNearestGridCenter(position);
            return Vector2.Distance(position, nearestCenter) < threshold;
        }

        void OnDrawGizmos()
        {
            if (gridCenters == null) return;

            Gizmos.color = Color.gray;

            // 绘制网格线
            for (int i = 0; i <= gridSize; i++)
            {
                float pos = i * cellSize;
                Gizmos.DrawLine(new Vector3(0, pos, 0), new Vector3(gridSize * cellSize, pos, 0));
                Gizmos.DrawLine(new Vector3(pos, 0, 0), new Vector3(pos, gridSize * cellSize, 0));
            }

            // 绘制网格中心点
            Gizmos.color = Color.white;
            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    Gizmos.DrawWireSphere(gridCenters[x, y], 0.05f);
                }
            }

            // 绘制Y轴向上方向指示
            Gizmos.color = Color.cyan;
            for (int y = 0; y < gridSize; y++)
            {
                Vector2 center = new Vector2(gridSize * cellSize / 2, y * cellSize + cellSize / 2);
                Gizmos.DrawLine(center, center + Vector2.up * 0.3f);
            }
        }
    }

}