using UnityEngine;
using UnityEngine.UI;
using ReGecko.GridSystem;
using System.Collections;
using System.Collections.Generic;

namespace ReGecko.Framework.UI
{
    /// <summary>
    /// UI网格渲染器：使用UI Image组件渲染网格
    /// </summary>
    public class UIGridRenderer : MonoBehaviour
    {
        [Header("配置")]
        public GridConfig Config;
        public Sprite CellSprite;

        [Header("样式")]
        public Color DefaultCellColor = Color.white;
        public float CellAlpha = 0.8f;

        readonly List<Image> _cellImages = new List<Image>();
        GameObject _gridContainerGO; // 改为存储GameObject引用而不是Transform
        float _adaptiveCellSize; // 自适应计算的单元格尺寸

        public void BuildGrid()
        {
            ClearGrid();
            if (!Config.IsValid() || CellSprite == null) return;

            // 延迟到下一帧构建，确保UI布局完成
            StartCoroutine(BuildGridDelayed());
        }

        IEnumerator BuildGridDelayed()
        {
            // 等待一帧，让UI布局系统完成计算
            yield return null;

            // 计算自适应的网格尺寸
            CalculateAdaptiveGridSize();

            if (_adaptiveCellSize <= 0)
            {
                Debug.LogError($"Invalid adaptive cell size: {_adaptiveCellSize}, aborting grid construction");
                yield break;
            }

            // 设置容器
            SetupContainer();
            
            if (_gridContainerGO == null)
            {
                Debug.LogError("Failed to create GridContainer, aborting grid construction");
                yield break;
            }

            // 创建网格单元
            for (int y = 0; y < Config.Height; y++)
            {
                for (int x = 0; x < Config.Width; x++)
                {
                    CreateCell(x, y);
                }
            }
        }

        void CalculateAdaptiveGridSize()
        {
            // 获取自身的RectTransform尺寸
            var myRT = GetComponent<RectTransform>();
            if (myRT == null) return;

            Vector2 containerSize = myRT.rect.size;

            // 如果容器尺寸还没有计算出来（可能在布局更新前），使用默认值或等待
            if (containerSize.x <= 0 || containerSize.y <= 0)
            {
                Debug.LogWarning("GridRenderer container size not ready, using fallback size calculation");
                // 尝试强制刷新布局
                Canvas.ForceUpdateCanvases();
                containerSize = myRT.rect.size;

                // 如果还是无效，使用屏幕比例作为临时方案
                if (containerSize.x <= 0 || containerSize.y <= 0)
                {
                    containerSize = new Vector2(Screen.width * 0.8f, Screen.height * 0.6f); // 估算的游戏区域
                }
            }

            // 计算适合容器的单元格尺寸（保持网格比例，选择较小的缩放比例以完全适应）
            float cellSizeX = containerSize.x / Config.Width;
            float cellSizeY = containerSize.y / Config.Height;
            _adaptiveCellSize = Mathf.Min(cellSizeX, cellSizeY);

            // 更新GridConfig的CellSize以保持兼容性
            Config.CellSize = _adaptiveCellSize;


        }

        void SetupContainer()
        {
            if (_gridContainerGO == null)
            {
                _gridContainerGO = new GameObject("GridContainer");
                _gridContainerGO.transform.SetParent(transform, false);

                // 设置容器的RectTransform（居中锚点，Grid坐标以中心为原点）
                var containerRt = _gridContainerGO.AddComponent<RectTransform>();
                containerRt.anchorMin = new Vector2(0.5f, 0.5f);
                containerRt.anchorMax = new Vector2(0.5f, 0.5f);
                containerRt.anchoredPosition = Vector2.zero;
                containerRt.pivot = new Vector2(0.5f, 0.5f); // Center pivot
                
                // 设置容器大小为网格的实际大小
                float gridWidth = Config.Width * _adaptiveCellSize;
                float gridHeight = Config.Height * _adaptiveCellSize;
                containerRt.sizeDelta = new Vector2(gridWidth, gridHeight);
                

            }
        }

        void CreateCell(int x, int y)
        {
            if (_gridContainerGO == null)
            {
                SetupContainer();
                if (_gridContainerGO == null) return;
            }

            var cellGo = new GameObject($"Cell_{x}_{y}");
            cellGo.transform.SetParent(_gridContainerGO.transform, false);

            // 添加RectTransform组件并设置正确的锚点和轴心（居中）
            var rt = cellGo.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(_adaptiveCellSize, _adaptiveCellSize);

            var image = cellGo.AddComponent<Image>();
            image.sprite = CellSprite;
            image.color = new Color(DefaultCellColor.r, DefaultCellColor.g, DefaultCellColor.b, CellAlpha);
            image.raycastTarget = false;

            // 使用Grid坐标系计算位置
            Vector3 worldPos = Config.CellToWorld(new Vector2Int(x, y));
            rt.anchoredPosition = new Vector2(worldPos.x, worldPos.y);

            _cellImages.Add(image);
        }

        public void ClearGrid()
        {
            foreach (var image in _cellImages)
            {
                if (image != null)
                {
                    if (Application.isPlaying) Destroy(image.gameObject);
                    else DestroyImmediate(image.gameObject);
                }
            }
            _cellImages.Clear();
        }

        public void SetCellColor(int x, int y, Color color)
        {
            int index = y * Config.Width + x;
            if (index >= 0 && index < _cellImages.Count && _cellImages[index] != null)
            {
                _cellImages[index].color = color;
            }
        }

        public Transform GetGridContainer()
        {
            if (_gridContainerGO == null)
            {
                SetupContainer();
            }
            return _gridContainerGO?.transform;
        }
        
        public float GetAdaptiveCellSize() => _adaptiveCellSize;

        void OnDestroy()
        {
            ClearGrid();
        }
    }
}
