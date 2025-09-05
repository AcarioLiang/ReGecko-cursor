using UnityEngine;
using ReGecko.GridSystem;
using ReGecko.SnakeSystem;
using ReGecko.Grid.Entities;
using System.Collections.Generic;

namespace ReGecko.HingeJointSnake
{
    /// <summary>
    /// 蛇测试脚本 - 用于在场景中测试蛇的功能
    /// </summary>
    public class SnakeTestScript : MonoBehaviour
    {
        [Header("网格配置")]
        public int gridWidth = 10;
        public int gridHeight = 10;
        public float cellSize = 1.0f;
        
        [Header("蛇配置")]
        public Sprite headSprite;
        public Sprite bodySprite;
        public Sprite tailSprite;
        public Sprite jointSprite;
        public Color snakeColor = Color.green;
        
        [Header("测试配置")]
        public Vector2Int[] testBodyCells;
        
        private HingeJointSnakeController _testSnake;
        private GridConfig _gridConfig;
        
        void Start()
        {
            // 创建网格配置
            _gridConfig = new GridConfig
            {
                Width = gridWidth,
                Height = gridHeight,
                CellSize = cellSize
            };
            
            // 创建测试蛇
            CreateTestSnake();
        }
        
        /// <summary>
        /// 创建测试蛇
        /// </summary>
        [ContextMenu("创建测试蛇")]
        public void CreateTestSnake()
        {
            // 清理现有测试蛇
            if (_testSnake != null)
            {
                Destroy(_testSnake.gameObject);
                _testSnake = null;
            }
            
            // 创建蛇对象
            GameObject snakeGO = new GameObject("TestSnake");
            snakeGO.transform.SetParent(transform, false);
            
            // 添加RectTransform组件
            RectTransform rectTransform = snakeGO.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(gridWidth * cellSize, gridHeight * cellSize);
            rectTransform.anchorMin = Vector2.one * 0.5f;
            rectTransform.anchorMax = Vector2.one * 0.5f;
            rectTransform.pivot = Vector2.one * 0.5f;
            
            // 添加蛇控制器
            _testSnake = snakeGO.AddComponent<HingeJointSnakeController>();
            
            // 设置蛇的属性
            _testSnake.SnakeId = "test_snake";
            _testSnake.Name = "测试蛇";
            _testSnake.BodySprite = bodySprite;
            _testSnake.BodyColor = snakeColor;
            _testSnake.Length = testBodyCells != null ? testBodyCells.Length : 5;
            _testSnake.IsControllable = true;
            
            // 设置初始身体格子
            if (testBodyCells == null || testBodyCells.Length < 2)
            {
                // 创建默认格子配置
                testBodyCells = new Vector2Int[]
                {
                    new Vector2Int(2, 2),  // 蛇头
                    new Vector2Int(3, 2),  // 身体
                    new Vector2Int(4, 2),  // 身体
                    new Vector2Int(5, 2),  // 身体
                    new Vector2Int(6, 2)   // 蛇尾
                };
            }
            
            _testSnake.SetInitialBodyCells(testBodyCells);
            
            // 初始化蛇
            _testSnake.Initialize(_gridConfig);
            
            Debug.Log($"测试蛇创建完成，格子数：{testBodyCells.Length}");
        }
        
        /// <summary>
        /// 销毁测试蛇
        /// </summary>
        [ContextMenu("销毁测试蛇")]
        public void DestroyTestSnake()
        {
            if (_testSnake != null)
            {
                Destroy(_testSnake.gameObject);
                _testSnake = null;
                Debug.Log("测试蛇已销毁");
            }
        }
        
        /// <summary>
        /// 绘制网格辅助线
        /// </summary>
        void OnDrawGizmos()
        {
            if (!_gridConfig.IsValid())
            {
                _gridConfig = new GridConfig
                {
                    Width = gridWidth,
                    Height = gridHeight,
                    CellSize = cellSize
                };
            }
            
            // 绘制网格
            Gizmos.color = Color.gray;
            Vector3 center = transform.position;
            
            // 计算网格边界
            float gridWidthWorld = _gridConfig.Width * _gridConfig.CellSize;
            float gridHeightWorld = _gridConfig.Height * _gridConfig.CellSize;
            float startX = center.x - gridWidthWorld / 2;
            float startY = center.y - gridHeightWorld / 2;
            
            // 绘制水平线
            for (int y = 0; y <= _gridConfig.Height; y++)
            {
                float yPos = startY + y * _gridConfig.CellSize;
                Vector3 start = new Vector3(startX, yPos, 0);
                Vector3 end = new Vector3(startX + gridWidthWorld, yPos, 0);
                Gizmos.DrawLine(start, end);
            }
            
            // 绘制垂直线
            for (int x = 0; x <= _gridConfig.Width; x++)
            {
                float xPos = startX + x * _gridConfig.CellSize;
                Vector3 start = new Vector3(xPos, startY, 0);
                Vector3 end = new Vector3(xPos, startY + gridHeightWorld, 0);
                Gizmos.DrawLine(start, end);
            }
            
            // 绘制格子索引
            if (testBodyCells != null)
            {
                Gizmos.color = Color.yellow;
                for (int i = 0; i < testBodyCells.Length; i++)
                {
                    Vector2Int cell = testBodyCells[i];
                    Vector3 pos = _gridConfig.CellToWorld(cell);
                    Gizmos.DrawWireSphere(pos, _gridConfig.CellSize * 0.2f);
                    
                    // 绘制索引
                    UnityEditor.Handles.Label(pos, $"{i}");
                }
            }
        }
    }
}
