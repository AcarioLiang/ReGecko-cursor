using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ReGecko.GridSystem;
using ReGecko.Grid.Entities;
using System.Collections;
using ReGecko.GameCore.Flow;
using System.Linq;
using ReGecko.Game;

namespace ReGecko.SnakeSystem
{
    public class SnakeController : BaseSnake
    {
        // 在 SnakeController 类字段区添加
        [SerializeField] bool DebugShowLeadTarget = true;
        [SerializeField] Color DebugLeadTargetColor = Color.red;
        [SerializeField] float DebugLeadMarkerSize = 8f;

        [Header("SnakeController特有属性")]
        // 拖拽相关
        bool _startMove;
        bool _isReverse;

        // *** SubGrid 改动开始 ***
        [Header("SubGrid 小格移动系统")]
        [Tooltip("是否启用小格移动系统")]
        public bool EnableSubGridMovement = true;
        [Tooltip("小格移动速度（小格/秒）")]
        public float MoveSpeedSubCellsPerSecond = 40f; // 5倍于原来的大格速度

        private List<GameObject> _subSegments = new List<GameObject>();// 每段的5个小段
        private Queue<GameObject> _subSegmentPool = new Queue<GameObject>();

        protected readonly LinkedList<Vector2Int> _subBodyCells = new LinkedList<Vector2Int>(); // 离散身体占用格，头在First
        private readonly List<RectTransform> _cachedSubRectTransforms = new List<RectTransform>();
        private List<Image> _segmentImages = new List<Image>();
        private HashSet<HoleEntity> _cacheCoconsumeHoles = new HashSet<HoleEntity>();


        public override LinkedList<Vector2Int> GetBodyCells()
        {
            return _subBodyCells;
        }

        public override List<GameObject> GetSegments()
        {
            return _subSegments;
        }
        // 小格移动相关
        private Vector2Int _currentHeadCell;
        private Vector2Int _currentTailCell;
        private Vector2Int _currentHeadSubCell;
        private Vector2Int _currentTailSubCell;
        private Vector2Int _lastSampledSubCell; // 上次采样的手指小格
        private LinkedList<Vector2Int> _subCellPathQueue = new LinkedList<Vector2Int>(); // 小格路径队列
        private LinkedList<Vector2Int> _headCellToBodyPathQueue = new LinkedList<Vector2Int>(); // 小格路径队列
        private float _subCellMoveAccumulator; // 小格移动累积器
        // *** SubGrid 改动结束 ***

        //优化缓存
        Vector3 _lastMousePos;
        Canvas _parentCanvas;

        // 拖动优化：减少更新频率
        private float _lastDragUpdateTime = 0f;
        private const float DRAG_UPDATE_INTERVAL = 0.008f; // 约120FPS更新频率

        // 路径队列优化
        private const int MAX_PATH_QUEUE_SIZE = 10; // 路径队列最大长度
        private const int PATH_QUEUE_TRIM_SIZE = 10; // 超出限制时保留的路径数量

        enum DragAxis { None, X, Y }
        DragAxis _dragAxis = DragAxis.None;

        Coroutine _consumeCoroutine;

        // —— 平滑路径模式 开关与缓存 ——
        [SerializeField] bool EnableSmoothPathMode = true;

        List<Vector2> _leadPathPoints = new List<Vector2>(512); // 记录拖动端（头/尾）已经经过的小格中心（世界坐标，按时间顺序）
        Vector2 _leadPos;                // 拖动端当前连续位置（世界坐标）
        Vector2 _leadTargetPos;          // 正在朝向的目标小格中心（世界坐标）
        Vector2 _leadReversePos;         // 倒车的目标点（世界坐标）
        Vector2Int _leadCurrentSubCell;  // 拖动端当前所在小格（中线）
        Vector2Int _leadTargetSubCell;   // 拖动端当前目标小格（中线）

        Vector2Int _leadReverseSubCell;  // 拖动端倒车目标小格（中线）

        bool _smoothInited = false;
        bool _lastDragFromHead = true;   // 记录上次拖动端，切换时重新初始化
        Vector2Int[] _tmpSubSnap;        // 仅用于初始化时从链表拷贝（零分配复用）

        float _segmentSpacing;           // 每段身体之间固定间距（世界单位）
        float _leadSpeedWorld;           // 拖动端线速度（世界单位/秒）
        const float EPS = 1e-3f;
        Vector2Int _lastLeadTargetSubCell = Vector2Int.zero;

        float _cachedSpeedInput;
        float _cachedCellSize;


        public override void Initialize(GridConfig grid, GridEntityManager entityManager = null, SnakeManager snakeManager = null)
        {
            _grid = grid;
            _entityManager = entityManager ?? FindObjectOfType<GridEntityManager>();
            _snakeManager = snakeManager ?? FindObjectOfType<SnakeManager>();
            _parentCanvas = GetComponentInParent<Canvas>();
            IsDragging = false;
            DragFromHead = false;

            // *** SubGrid 改动：确保子段位置正确初始化 ***
            if (EnableSubGridMovement)
            {
                RecreateSubSegments();
                InitializeSubSegmentPositions(InitialBodyCells);

                LoadSpritesFromConfig();

                if (EnableBodySpriteManagement)
                {
                    InitializeBodySpriteManager();
                }
                else
                {
                    UpdateAllSegmentSprites();
                }
            }
        }


        /// <summary>
        /// 从配置文件加载图片
        /// </summary>
        void LoadSpritesFromConfig()
        {
            if (Config == null && GameContext.SnakeBodyConfig == null) return;

            if (Config == null)
                Config = GameContext.SnakeBodyConfig;

            // 从配置文件加载图片
            //if (Config.VerticalHeadSprite != null)
            //    VerticalHeadSprite = Config.VerticalHeadSprite;
            //if (Config.VerticalTailSprite != null)
            //    VerticalTailSprite = Config.VerticalTailSprite;
            //if (Config.VerticalBodySprite != null)
            //    VerticalBodySprite = Config.VerticalBodySprite;

        }

        /// <summary>
        /// 更新所有段的图片
        /// </summary>
        public void UpdateAllSegmentSprites()
        {
            if (_subSegments.Count == 0) return;

            if (_subBodyCells == null || _subBodyCells.Count == 0) return;

            // 更新蛇头
            if (_subSegments.Count > 0 && _subSegments[0] != null)
            {
                UpdateHeadSprite();
            }

            // 更新身体段,蛇尾
            UpdateBodySprite();

        }

        /// <summary>
        /// 更新蛇头图片
        /// </summary>
        void UpdateHeadSprite()
        {
            if (_subBodyCells.Count < 2) return;
            if (VerticalHeadSprite == null) return;

            var headCell = _subBodyCells.First.Value;
            var nextCell = _subBodyCells.First.Next.Value;

            var direction = nextCell - headCell;
            var isHorizontal = Mathf.Abs(direction.x) > Mathf.Abs(direction.y);

            var image = _segmentImages[0];
            var rt = _subSegments[0].GetComponent<RectTransform>();

            if (isHorizontal)
            {
                // 水平方向：使用竖直图片并旋转90度
                image.sprite = VerticalHeadSprite;
                if (headCell.x < nextCell.x)
                {
                    rt.rotation = Quaternion.Euler(0, 0, 360 - RotationAngle);
                }
                else
                {
                    rt.rotation = Quaternion.Euler(0, 0, RotationAngle);
                }
            }
            else
            {
                // 竖直方向：使用竖直图片，不旋转
                image.sprite = VerticalHeadSprite;
                rt.rotation = Quaternion.identity;

                if (headCell.y < nextCell.y)
                {
                    rt.rotation = Quaternion.identity;
                }
                else
                {
                    rt.rotation = Quaternion.Euler(0, 0, 180);
                }
            }
        }


        /// <summary>
        /// 更新身体段图片
        /// </summary>
        void UpdateBodySprite()
        {
            if (VerticalBodySprite == null) return;
            if (_subBodyCells.Count < 6) return;

            var bodyCellsList = _subBodyCells.ToList();
            for (int segmentIndex = 1; segmentIndex < _subSegments.Count;)
            {
                if (segmentIndex > _subSegments.Count) break;

                var currentCell = bodyCellsList[segmentIndex];
                bool isSameX = true;
                bool isSameY = true;
                for (int step = 1; step < SubGridHelper.SUB_DIV; step++)
                {
                    if (segmentIndex + step < _subSegments.Count)
                    {
                        var nextCell = bodyCellsList[segmentIndex + step];
                        if (isSameX && currentCell.x == nextCell.x)
                        {
                            isSameX = true;
                        }
                        else
                        {
                            isSameX = false;
                        }

                        if (isSameY && currentCell.y == nextCell.y)
                        {
                            isSameY = true;
                        }
                        else
                        {
                            isSameY = false;
                        }
                    }
                }
                if (!isSameX && !isSameY)
                {
                    // 需要计算各个部位的角度，已22.5度分隔
                    // 拐角
                    // 计算角度：
                    if (segmentIndex < _subSegments.Count - 5)
                    {
                        //全部在大格里

                        var tailOffset = 0f;
                        bool isTail = false;
                        Vector2Int[] fivecells = new Vector2Int[5];
                        for (int step = 0; step < SubGridHelper.SUB_DIV; step++)
                        {
                            fivecells[step] = bodyCellsList[segmentIndex + step];
                        }

                        var startAngle = 0;
                        var offsetAngle = -SubRotationAngle;
                        var dir = GetFiveSubTurnDirection8(fivecells);

                        if (dir == TurnDirection8.RightToUp)
                        {
                            ///*
                            startAngle = 90;
                            offsetAngle = SubRotationAngle;
                            tailOffset = 180f;
                        }
                        else if (dir == TurnDirection8.RightToDown)
                        {
                            ///*
                            startAngle = 90;
                            offsetAngle = -SubRotationAngle;
                            tailOffset = 180f;
                        }
                        else if (dir == TurnDirection8.LeftToUp)
                        {
                            ///*
                            startAngle = 90;
                            offsetAngle = -SubRotationAngle;
                            tailOffset = 0f;
                        }
                        else if (dir == TurnDirection8.LeftToDown)
                        {
                            ///*
                            startAngle = 90;
                            offsetAngle = SubRotationAngle;
                            tailOffset = 0f;
                        }
                        else if (dir == TurnDirection8.UpToRight)
                        {
                            ///*
                            startAngle = 0;
                            offsetAngle = -SubRotationAngle;
                            tailOffset = 0f;
                        }
                        else if (dir == TurnDirection8.UpToLeft)
                        {
                            ///*
                            startAngle = 0;
                            offsetAngle = SubRotationAngle;
                            tailOffset = 0f;
                        }
                        else if (dir == TurnDirection8.DownToRight)
                        {
                            ///*
                            startAngle = 0;
                            offsetAngle = SubRotationAngle;
                            tailOffset = 180f;
                        }
                        else if (dir == TurnDirection8.DownToLeft)
                        {
                            ///*
                            startAngle = 0;
                            offsetAngle = -SubRotationAngle;
                            tailOffset = 180f;
                        }

                        for (int step = 0; step < SubGridHelper.SUB_DIV; step++)
                        {
                            if (segmentIndex + step >= _subSegments.Count) break;

                            var image = _segmentImages[segmentIndex + step];
                            var rt = _subSegments[segmentIndex + step].GetComponent<RectTransform>();

                            if (segmentIndex + step == _subSegments.Count - 1)
                            {
                                //蛇尾
                                image.sprite = VerticalTailSprite;
                                isTail = true;
                            }
                            else
                            {
                                image.sprite = VerticalBodySprite;
                                isTail = false;
                            }


                            rt.rotation = Quaternion.Euler(0, 0, (startAngle + step * offsetAngle) + (isTail ? tailOffset : 0f));

                        }

                    }
                    else
                    {
                        //不够五小段
                        var tailOffset = 0f;
                        var lastSegmentsCount = _subSegments.Count - segmentIndex;
                        Vector2Int[] fivecells = new Vector2Int[lastSegmentsCount];
                        bool isTail = false;
                        for (int step = 0; step < lastSegmentsCount; step++)
                        {
                            fivecells[step] = bodyCellsList[segmentIndex + step];
                        }

                        var startAngle = 0;
                        var offsetAngle = -SubRotationAngle;
                        var dir = GetFiveSubTurnDirection8(fivecells);
         
                        if (dir == TurnDirection8.RightToUp)
                        {
                            ///*
                            startAngle = 90;
                            offsetAngle = SubRotationAngle;
                            tailOffset = 180f;
                        }
                        else if (dir == TurnDirection8.RightToDown)
                        {
                            ///*
                            startAngle = 90;
                            offsetAngle = -SubRotationAngle;
                            tailOffset = 180f;
                        }
                        else if (dir == TurnDirection8.LeftToUp)
                        {
                            ///*
                            startAngle = 90;
                            offsetAngle = -SubRotationAngle;
                            tailOffset = 0f;
                        }
                        else if (dir == TurnDirection8.LeftToDown)
                        {
                            ///*
                            startAngle = 90;
                            offsetAngle = SubRotationAngle;
                            tailOffset = 0f;
                        }
                        else if (dir == TurnDirection8.UpToRight)
                        {
                            ///*
                            startAngle = 0;
                            offsetAngle = -SubRotationAngle;
                            tailOffset = 0f;
                        }
                        else if (dir == TurnDirection8.UpToLeft)
                        {
                            ///*
                            startAngle = 0;
                            offsetAngle = SubRotationAngle;
                            tailOffset = 0f;
                        }
                        else if (dir == TurnDirection8.DownToRight)
                        {
                            ///*
                            startAngle = 0;
                            offsetAngle = SubRotationAngle;
                            tailOffset = 180f;
                        }
                        else if (dir == TurnDirection8.DownToLeft)
                        {
                            ///*
                            startAngle = 0;
                            offsetAngle = -SubRotationAngle;
                            tailOffset = 180f;
                        }


                        for (int step = 0; step < lastSegmentsCount; step++)
                        {
                            if (segmentIndex + step >= _subSegments.Count) break;

                            var image = _segmentImages[segmentIndex + step];
                            var rt = _subSegments[segmentIndex + step].GetComponent<RectTransform>();

                            if (segmentIndex + step == _subSegments.Count - 1)
                            {
                                //蛇尾
                                image.sprite = VerticalTailSprite;
                                isTail = true;
                            }
                            else
                            {
                                image.sprite = VerticalBodySprite;
                                isTail = false;
                            }


                            rt.rotation = Quaternion.Euler(0, 0, (startAngle + step * offsetAngle) + (isTail ? tailOffset : 0f));

                        }

                    }



                }
                else if (isSameX)
                {
                    var tailOffset = 0f;
                    // 五小段均为直线：使用竖直身体图片
                    for (int step = 0; step < SubGridHelper.SUB_DIV; step++)
                    {
                        if (segmentIndex + step >= _subSegments.Count) break;

                        var image = _segmentImages[segmentIndex + step];
                        var rt = _subSegments[segmentIndex + step].GetComponent<RectTransform>();

                        if (segmentIndex + step == _subSegments.Count - 1)
                        {
                            //蛇尾
                            image.sprite = VerticalTailSprite;

                            var nextrt = _subSegments[segmentIndex].GetComponent<RectTransform>();
                            if (nextrt != null && nextrt.position.y > rt.position.y)
                            {
                                tailOffset = 180f;
                            }
                            else
                            {
                                tailOffset = 0f;
                            }
                        }
                        else
                        {
                            image.sprite = VerticalBodySprite;
                            tailOffset = 0f;

                        }

                        // 竖直方向：
                        rt.rotation = Quaternion.Euler(0, 0, tailOffset);
                    }
                }
                else if (isSameY)
                {
                    var tailOffset = 0f;
                    // 五小段均为直线：使用竖直身体图片旋转90度
                    for (int step = 0; step < SubGridHelper.SUB_DIV; step++)
                    {
                        if (segmentIndex + step >= _subSegments.Count) break;

                        var image = _segmentImages[segmentIndex + step];
                        var rt = _subSegments[segmentIndex + step].GetComponent<RectTransform>();

                        if (segmentIndex + step == _subSegments.Count - 1)
                        {
                            //蛇尾
                            image.sprite = VerticalTailSprite;


                            var nextrt = _subSegments[segmentIndex].GetComponent<RectTransform>();
                            if (nextrt != null && nextrt.position.x < rt.position.x)
                            {
                                tailOffset = -180f;
                            }
                            else
                            {
                                tailOffset = 0f;
                            }
                        }
                        else
                        {
                            image.sprite = VerticalBodySprite;
                            tailOffset = 0f;
                        }

                        // 水平方向：旋转90度
                        rt.rotation = Quaternion.Euler(0, 0, RotationAngle + tailOffset);
                    }
                }

                if (!isSameX && !isSameY)
                {
                    segmentIndex += 5;
                }
                else
                {
                    segmentIndex += 1;
                }
            }


        }

        public enum TurnDirection8
        {
            RightToUp,    // → 再 ↑
            RightToDown,  // → 再 ↓
            LeftToUp,     // ← 再 ↑
            LeftToDown,   // ← 再 ↓
            UpToRight,    // ↑ 再 →
            UpToLeft,     // ↑ 再 ←
            DownToRight,  // ↓ 再 →
            DownToLeft    // ↓ 再 ←
        }

        /// <summary>
        /// 计算5小段整体的转弯方向（八方向）
        /// fiveSubCells 为该段5个小格，按身体顺序（头->尾或尾->头）连续排列
        /// 仅在已判定转弯（!isSameX && !isSameY）时调用
        /// </summary>
        public static TurnDirection8 GetFiveSubTurnDirection8(IReadOnlyList<Vector2Int> fiveSubCells)
        {
            Vector2Int dirIn = Vector2Int.zero, dirOut = Vector2Int.zero;

            // 入口方向：从头开始找第一个非零步进
            for (int i = 1; i < fiveSubCells.Count; i++)
            {
                var d = fiveSubCells[i] - fiveSubCells[i - 1];
                if (d.x != 0 || d.y != 0)
                {
                    dirIn = new Vector2Int(Mathf.Clamp(d.x, -1, 1), Mathf.Clamp(d.y, -1, 1));
                    break;
                }
            }

            // 出口方向：从尾开始找第一个非零步进
            for (int i = fiveSubCells.Count - 1; i >= 1; i--)
            {
                var d = fiveSubCells[i] - fiveSubCells[i - 1];
                if (d.x != 0 || d.y != 0)
                {
                    dirOut = new Vector2Int(Mathf.Clamp(d.x, -1, 1), Mathf.Clamp(d.y, -1, 1));
                    break;
                }
            }

            // 兜底
            if (dirIn == Vector2Int.zero || dirOut == Vector2Int.zero)
                return TurnDirection8.RightToUp;

            // 入口水平 + 出口垂直
            if (dirIn.x != 0 && dirOut.y != 0)
            {
                if (dirIn.x > 0 && dirOut.y > 0) return TurnDirection8.RightToUp;
                if (dirIn.x > 0 && dirOut.y < 0) return TurnDirection8.RightToDown;
                if (dirIn.x < 0 && dirOut.y > 0) return TurnDirection8.LeftToUp;
                return TurnDirection8.LeftToDown;
            }

            // 入口垂直 + 出口水平
            if (dirIn.y != 0 && dirOut.x != 0)
            {
                if (dirIn.y > 0 && dirOut.x > 0) return TurnDirection8.UpToRight;
                if (dirIn.y > 0 && dirOut.x < 0) return TurnDirection8.UpToLeft;
                if (dirIn.y < 0 && dirOut.x > 0) return TurnDirection8.DownToRight;
                return TurnDirection8.DownToLeft;
            }

            return TurnDirection8.RightToUp;
        }

        public override void UpdateGridConfig(GridConfig newGrid)
        {
            _grid = newGrid;

            // 更新小段的大小
            if (EnableSubGridMovement)
            {
                float subSegmentSize = _grid.CellSize / SubGridHelper.SUB_DIV;
                for (int i = 0; i < _subSegments.Count; i++)
                {
                    var rt = _subSegments[i].GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.sizeDelta = new Vector2(_grid.CellSize, subSegmentSize);
                    }
                }
            }
        }

        // *** SubGrid 改动：子段管理方法开始 ***

        /// <summary>
        /// 创建或获取子段GameObject
        /// </summary>
        GameObject GetSubSegmentFromPool()
        {
            if (_subSegmentPool.Count > 0)
            {
                var obj = _subSegmentPool.Dequeue();
                obj.SetActive(true);
                return obj;
            }

            // 如果没有预制体，创建一个基本的Image对象
            var go = new GameObject("SubSegment");
            go.transform.SetParent(transform);

            var image = go.AddComponent<Image>();
            image.sprite = null;// BodySprite;
            image.color = BodyColor;
            if (EnableBodySpriteManagement)
            {
                image.enabled = false;
            }
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(_grid.CellSize * 0.8f, SubGridHelper.SUB_CELL_SIZE * _grid.CellSize + 5); // 转换为UI单位
            return go;
        }

        /// <summary>
        /// 回收子段到对象池
        /// </summary>
        void ReturnSubSegmentToPool(GameObject obj)
        {
            obj.SetActive(false);
            _subSegmentPool.Enqueue(obj);
        }

        /// <summary>
        /// 重新创建所有子段
        /// </summary>
        void RecreateSubSegments()
        {
            // 清理现有子段
            foreach (var subSeg in _subSegments)
            {
                ReturnSubSegmentToPool(subSeg);
            }
            _subSegments.Clear();


            _cachedSubRectTransforms.Clear();
            RectTransform rt = null;
            Image img = null;
            for (int segmentIndex = 0; segmentIndex < Mathf.Max(1, Length); segmentIndex++)
            {
                var subSegmentList = new List<GameObject>();
                for (int i = 0; i < SubGridHelper.SUB_DIV; i++)
                {
                    var subSegment = GetSubSegmentFromPool();
                    subSegment.name = ($"SubSegment_{segmentIndex}_{i}");
                    subSegmentList.Add(subSegment);
                    rt = subSegment.GetComponent<RectTransform>();
                    img = subSegment.GetComponent<Image>();
                    _cachedSubRectTransforms.Add(rt);
                    _segmentImages.Add(img);
                    _subSegments.Add(subSegment);
                }
            }
        }

        /// <summary>
        /// 初始化所有子段的位置
        /// </summary>
        void InitializeSubSegmentPositions(Vector2Int[] initialbodycells)
        {
            if (!EnableSubGridMovement)
                return;

            var bodyCells = initialbodycells;
            if (bodyCells == null || bodyCells.Length < 2) return;

            _subBodyCells.Clear();

            // 工具：方向与边的映射
            Vector2Int DirToDelta(int dir)
            {
                switch (dir)
                {
                    // 0:Left 1:Right 2:Down 3:Up
                    case 0: return new Vector2Int(-1, 0);
                    case 1: return new Vector2Int(1, 0);
                    case 2: return new Vector2Int(0, -1);
                    case 3: return new Vector2Int(0, 1);
                    default: return Vector2Int.zero;
                }
            }
            int DeltaToDir(Vector2Int d)
            {
                if (d == new Vector2Int(-1, 0)) return 0;
                if (d == new Vector2Int(1, 0)) return 1;
                if (d == new Vector2Int(0, -1)) return 2;
                if (d == new Vector2Int(0, 1)) return 3;
                return -1;
            }
            int Opposite(int dir)
            {
                if (dir == 0) return 1; // L->R
                if (dir == 1) return 0; // R->L
                if (dir == 2) return 3; // D->U
                if (dir == 3) return 2; // U->D
                return -1;
            }
            Vector2Int SideToEntrySub(Vector2Int bigCell, int side)
            {
                // side: 0-L 1-R 2-D 3-U
                switch (side)
                {
                    case 0: return SubGridHelper.BigCellToLeftSubCell(bigCell);
                    case 1: return SubGridHelper.BigCellToRightSubCell(bigCell);
                    case 2: return SubGridHelper.BigCellToBottomSubCell(bigCell);
                    case 3: return SubGridHelper.BigCellToTopSubCell(bigCell);
                    default: return SubGridHelper.BigCellToCenterSubCell(bigCell);
                }
            }
            Vector2Int[] BuildFiveSubCells(Vector2Int bigCell, int entrySide, int exitSide)
            {
                // 在该大格内，沿中线从 entry 边界走到 exit 边界，必经中心(2,2)，共4步=5点
                var res = new Vector2Int[SubGridHelper.SUB_DIV];
                // 入口点
                res[0] = SideToEntrySub(bigCell, entrySide);

                // 目标“路标”：中心 + 出口边界点
                var center = SubGridHelper.BigCellToCenterSubCell(bigCell);
                var exit = SideToEntrySub(bigCell, exitSide);

                // 在该大格内行走：先到中心，再到出口（每次一步，保证正好填满5个点）
                Vector2Int cur = res[0];

                // 步进到中心（最多2步）
                while (cur != center && (res[1] == default || res[2] == default || res[3] == default || res[4] == default))
                {
                    if (cur.x != center.x)
                    {
                        int step = cur.x < center.x ? 1 : -1;
                        cur = new Vector2Int(cur.x + step, cur.y);
                    }
                    else if (cur.y != center.y)
                    {
                        int step = cur.y < center.y ? 1 : -1;
                        cur = new Vector2Int(cur.x, cur.y + step);
                    }
                    // 写入下一个空位
                    for (int k = 1; k < SubGridHelper.SUB_DIV; k++)
                    {
                        if (res[k] == default)
                        {
                            res[k] = cur;
                            break;
                        }
                    }
                    if (res[SubGridHelper.SUB_DIV - 1] != default) break;
                }

                // 如果还没填满，继续从中心到出口（最多2步）
                while (cur != exit && res[SubGridHelper.SUB_DIV - 1] == default)
                {
                    if (cur.x != exit.x)
                    {
                        int step = cur.x < exit.x ? 1 : -1;
                        cur = new Vector2Int(cur.x + step, cur.y);
                    }
                    else if (cur.y != exit.y)
                    {
                        int step = cur.y < exit.y ? 1 : -1;
                        cur = new Vector2Int(cur.x, cur.y + step);
                    }
                    for (int k = 1; k < SubGridHelper.SUB_DIV; k++)
                    {
                        if (res[k] == default)
                        {
                            res[k] = cur;
                            break;
                        }
                    }
                }

                // 兜底：若因极端情况未填满，剩余点重复最后一个，保证长度为5且连续
                for (int k = 1; k < SubGridHelper.SUB_DIV; k++)
                {
                    if (res[k] == default) res[k] = res[k - 1];
                }
                return res;
            }


            Vector2Int[] BuildHeadSubCells(Vector2Int bigCell, int entrySide, int exitSide)
            {
                var res = new Vector2Int[SubGridHelper.SUB_DIV - 2];
                var fiveres = BuildFiveSubCells(bigCell, entrySide, exitSide);
                for (int i = 2; i < fiveres.Length; i++) 
                {
                    res[i - 2] = fiveres[i];
                }
                return res;
            }

            Vector2Int[] BuildLastSubCells(Vector2Int bigCell, int entrySide, int exitSide)
            {
                var res = new Vector2Int[SubGridHelper.SUB_DIV - 2];
                var fiveres = BuildFiveSubCells(bigCell, entrySide, exitSide);
                for (int i = 0; i < fiveres.Length - 2; i++)
                {
                    res[i] = fiveres[i];
                }
                return res;
            }

            // 为每个大格生成5个连续的小格，并写入链表与可视
            int segmentIndex = 0;
            for (int i = 0; i < bodyCells.Length; i++)
            {
                var curBig = bodyCells[i];

                // 计算入/出边
                int entrySide, exitSide;
                if (i == 0)
                {
                    // 头：入口为反向边，出口为指向第二个格的边
                    var dirToNext = DeltaToDir(bodyCells[1] - curBig);
                    exitSide = dirToNext;
                    entrySide = Opposite(exitSide);
                }
                else if (i == bodyCells.Length - 1)
                {
                    // 尾：入口为来自前一格的反向边，出口为其对边（形成封闭端）
                    var dirFromPrev = DeltaToDir(curBig - bodyCells[i - 1]);
                    entrySide = Opposite(dirFromPrev);
                    exitSide = Opposite(entrySide);
                }
                else
                {
                    // 中间：入口=来自前一格的反向边，出口=指向下一格的边
                    var dirFromPrev = DeltaToDir(curBig - bodyCells[i - 1]);
                    var dirToNext = DeltaToDir(bodyCells[i + 1] - curBig);
                    entrySide = Opposite(dirFromPrev);
                    exitSide = dirToNext;
                }

                // 生成该段5个小格（沿中线，必经中心，最多一次转向）
                var subCellPositions = BuildFiveSubCells(curBig, entrySide, exitSide);

                // 同步到链表与显示
                for (int k = 0; k < subCellPositions.Length; k++)
                {
                    _subBodyCells.AddLast(subCellPositions[k]);
                }
                UpdateSubSegmentPositions(segmentIndex, subCellPositions);
                segmentIndex++;
            }

            // 连续性校验：所有小格必须两两相邻（曼哈顿距离=1）
            var node = _subBodyCells.First;
            var idx = 0;
            while (node != null && node.Next != null)
            {
                var a = node.Value;
                var b = node.Next.Value;
                if (SubGridHelper.SubCellManhattan(a, b) != 1)
                {
                    Debug.LogError($"InitializeSubSegmentPositions: 小格不连续 at pair index {idx}->{idx + 1}, a={a}, b={b}");
                    break;
                }
                node = node.Next;
                idx++;
            }

            _currentHeadSubCell = _subBodyCells.First.Value;
            _currentTailSubCell = _subBodyCells.Last.Value;
            _lastSampledSubCell = _currentHeadSubCell;

            _currentHeadCell = SubGridHelper.SubCellToBigCell(_currentHeadSubCell);
            _currentTailCell = SubGridHelper.SubCellToBigCell(_currentTailSubCell);
        }


        /// <summary>
        /// 更新子段位置（由SnakeController调用）
        /// </summary>
        /// <param name="segmentIndex">身体节点索引</param>
        /// <param name="subCellPositions">5个子段的小格坐标</param>
        public void UpdateSubSegmentPositions(int segmentIndex, Vector2Int[] subCellPositions)
        {
            if (!EnableSubGridMovement)
                return;

            var grid = GetGrid();

            for (int i = 0; i < subCellPositions.Length; i++)
            {
                int curSubIndex = segmentIndex * SubGridHelper.SUB_DIV + i;
                if (curSubIndex < _subSegments.Count)
                {
                    var cell = subCellPositions[i];
                    var worldPos = SubGridHelper.SubCellToWorld(cell, _grid);

                    var rt = _subSegments[curSubIndex].GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchoredPosition = new Vector2(worldPos.x, worldPos.y);
                        rt.rotation = Quaternion.Euler(0, 0, 0f);
                    }
                }

            }

        }


        void Update()
        {
        }

        public override void UpdateMovement()
        {
            if (!IsDragging)
                return;

            // 如果蛇已被完全消除或组件被销毁，停止所有移动更新
            if (_subBodyCells.Count == 0 || !IsAlive() || _cachedSubRectTransforms.Count == 0)
            {
                return;
            }


            if (_consuming)
            {
                // 清理已销毁的组件
                CleanupCachedComponents();
                return;
            }

            if (EnableSmoothPathMode)
            {
                UpdateSubSmoothPathPointsMovement();
            }
        }

        // *** SubGrid 改动：恒定速度沿路径移动 ***
        void UpdateSubSmoothPathPointsMovement()
        {
            // 基础校验
            if (_subBodyCells == null || _subBodyCells.Count == 0 || _cachedSubRectTransforms.Count == 0) return;

            // 初始化/切换端
            if (!_smoothInited || _lastDragFromHead != DragFromHead)
            {
                InitializeSmoothPathFromCurrentState();
                _lastDragFromHead = DragFromHead;
            }

            _isReverse = false;

            // 速度/间距（世界单位）
            RefreshKinematicsIfNeeded();

            // 1) 鼠标投影到最近中线（连续世界坐标，不量化到小格中心）
            _leadTargetPos = ScreenToWorldCenter(Input.mousePosition);
            var targetSubCell = SubGridHelper.WorldToSubCell(_leadTargetPos, _grid);

            // 取“一步”的目标格
            _subCellPathQueue ??= new LinkedList<Vector2Int>();
            _subCellPathQueue.Clear();

            // 计算移动方向
            var mouseSubCell = SubGridHelper.WorldToSubCell(_leadTargetPos, _grid);
            EnqueueSubCellPath(DragFromHead ? GetHeadSubCell() : GetTailSubCell(), mouseSubCell, _subCellPathQueue, 1) ;

            //手指方向大一一小格距离需要修正方向
            if(_subCellPathQueue.Count > 0)
            {
                _leadTargetPos = SubGridHelper.SubCellToWorld(_subCellPathQueue.First.Value, _grid);
                targetSubCell = SubGridHelper.WorldToSubCell(_leadTargetPos, _grid);
            }

            // 倒车判定
            if (CheckIfNeedReverse(_leadTargetPos, out _leadReversePos))
            {
                if(_leadReversePos == Vector2.zero)
                {
                    return;
                }
                _isReverse = true;
            }

            if(_isReverse)
            {
                //执行倒车逻辑
                return;
            }

            // 检查目标点合法性
            if (!CheckNextCell(targetSubCell))
            {
                //Debug.LogError("CheckNextCell return!");
                return;
            }

            // 2) 拖动端沿目标插值
            Vector2 dir = _leadTargetPos - _leadPos;
            float dist = dir.magnitude;
            if (dist > 1e-4f)
            {
                float step = _leadSpeedWorld * Time.deltaTime;
                if (step + 1e-3f >= dist) 
                    _leadPos = _leadTargetPos;
                else 
                    _leadPos += dir * (step / dist);
            }

            // 3) 路径历史：每移动到达一定阈值才采样，降低抖动
            if (_leadPathPoints.Count == 0)
            {
                _leadPathPoints.Add(_leadPos);
            }
            else
            {
                var last = _leadPathPoints[_leadPathPoints.Count - 1];
                if (Vector2.Distance(last, _leadPos) >= 0.25f * _segmentSpacing)
                    _leadPathPoints.Add(_leadPos);
            }


            // 4) 裁剪历史：仅保留覆盖全身需要的长度
            {
                int n = _subBodyCells.Count;
                if (n > 1)
                {
                    float need = (n - 1) * _segmentSpacing + 2f * _segmentSpacing;
                    // 估算总长
                    float total = 0f;
                    Vector2 p = _leadPos;
                    for (int i = _leadPathPoints.Count - 1; i >= 0; i--)
                    {
                        total += Vector2.Distance(p, _leadPathPoints[i]);
                        p = _leadPathPoints[i];
                    }
                    float remove = total - need;
                    while (_leadPathPoints.Count > 1 && remove > 0f)
                    {
                        var a = _leadPathPoints[0];
                        var b = _leadPathPoints[1];
                        float seg = Vector2.Distance(a, b);
                        if (seg <= remove)
                        {
                            _leadPathPoints.RemoveAt(0);
                            remove -= seg;
                        }
                        else break;
                    }
                }
            }

            // 5) 生成整条蛇的连续位置（单调扫描，O(n)）
            {
                int n = _subBodyCells.Count;

                // 单调扫描游标：从 leadPos → lastHistory → ... → firstHistory
                int idx = _leadPathPoints.Count - 1; // 指向当前段的“历史终点”
                Vector2 cur = _leadPos;              // 段起点
                Vector2 nxt = (idx >= 0) ? _leadPathPoints[idx] : _leadPos;
                float seg = Vector2.Distance(cur, nxt);
                float acc = 0f;                      // 已累计的路径长度（从 leadPos 起）

                for (int i = 0; i < n; i++)
                {
                    float target = i * _segmentSpacing;

                    // 推进游标直到覆盖 target
                    while (acc + seg + 1e-6f < target && idx > 0)
                    {
                        // 前进到下一个历史段
                        acc += seg;
                        cur = nxt;
                        idx--;
                        nxt = _leadPathPoints[idx];
                        seg = Vector2.Distance(cur, nxt);
                    }

                    Vector2 pos;
                    if (seg <= 1e-6f)
                    {
                        // 没有可用段或段长为0：落在当前点
                        pos = cur;
                    }
                    else
                    {
                        float need = Mathf.Clamp(target - acc, 0f, seg);
                        float t = need / seg;
                        pos = Vector2.LerpUnclamped(cur, nxt, t);
                    }

                    // 写入可视（拖头/拖尾映射）
                    int visIndex = DragFromHead ? i : (n - 1 - i);
                    if (visIndex < _cachedSubRectTransforms.Count)
                    {
                        var rt = _cachedSubRectTransforms[visIndex];
                        rt.anchoredPosition = pos;
                    }


                    //更新身体图片
                    if (EnableBodySpriteManagement)
                    {
                        _bodySpriteManager?.OnSnakeMoved();
                    }
                    else
                    {
                        UpdateAllSegmentSprites();
                    }
                }
            }

        }

        // —— 工具与缓存 ——

        // 速度/间距缓存
        void RefreshKinematicsIfNeeded()
        {
            if (!Mathf.Approximately(_cachedSpeedInput, MoveSpeedSubCellsPerSecond) ||
                !Mathf.Approximately(_cachedCellSize, _grid.CellSize))
            {
                _cachedSpeedInput = MoveSpeedSubCellsPerSecond;
                _cachedCellSize = _grid.CellSize;
                _segmentSpacing = SubGridHelper.SUB_CELL_SIZE * _grid.CellSize;
                _leadSpeedWorld = _cachedSpeedInput * _segmentSpacing;
            }
        }

        void InitializeSmoothPathFromCurrentState()
        {
            _leadPathPoints.Clear();

            // 当前拖动端的“当前小格”和“连续位置”设定为现有的头/尾中心
            if (DragFromHead)
            {
                _leadCurrentSubCell = _currentHeadSubCell;
                _leadPos = ToWorldCenter(_leadCurrentSubCell);
            }
            else
            {
                _leadCurrentSubCell = _currentTailSubCell;
                _leadPos = ToWorldCenter(_leadCurrentSubCell);
            }

            // 用当前整条蛇的小格队列，按“拖动端→另一端”的方向，构造历史（越靠近拖动端的点排在末尾）
            int n = _subBodyCells.Count;
            if (_tmpSubSnap == null || _tmpSubSnap.Length < n) _tmpSubSnap = new Vector2Int[Mathf.NextPowerOfTwo(n)];

            var node = _subBodyCells.First;
            for (int i = 0; i < n; i++, node = node.Next) _tmpSubSnap[i] = node.Value;

            if (DragFromHead)
            {
                // 历史应为：靠尾的点在前，靠头的点在后；末尾一个就是“拖动端上一格的中心”
                for (int i = n - 1; i >= 1; i--) _leadPathPoints.Add(ToWorldCenter(_tmpSubSnap[i]));
            }
            else
            {
                // 从尾拖：历史为靠头的点在前，靠尾的点在后；末尾一个是“拖动端上一格的中心”
                for (int i = 0; i < n - 1; i++) _leadPathPoints.Add(ToWorldCenter(_tmpSubSnap[i]));
            }

            _smoothInited = true;
        }

        //每一小段身体移动完成之后
        void AfterSmoothVisualsByPath()
        {
            //UpdateSubBodyCellsFromCachedSubRectTransforms();

            // 5) 刷新缓存
            _currentHeadSubCell = _subBodyCells.First.Value;
            _currentTailSubCell = _subBodyCells.Last.Value;
            _currentHeadCell = SubGridHelper.SubCellToBigCell(_currentHeadSubCell);
            _currentTailCell = SubGridHelper.SubCellToBigCell(_currentTailSubCell);

            //刷新碰撞缓存
            _snakeManager.InvalidateOccupiedCellsCache();

            // 洞检测：若拖动端在洞位置或临近洞，且颜色匹配，触发吞噬
            {
                var hole = FindHoleAtOrAdjacentWithColor(_currentHeadCell, ColorType);
                if (hole != null)
                {
                    _consumeCoroutine ??= StartCoroutine(CoConsume(hole, true));
                    return;
                }
            }
            {
                var hole = FindHoleAtOrAdjacentWithColor(_currentTailCell, ColorType);
                if (hole != null)
                {
                    _consumeCoroutine ??= StartCoroutine(CoConsume(hole, false));
                    return;
                }
            }

            //更新身体图片
            if (EnableBodySpriteManagement)
            {
                _bodySpriteManager?.OnSnakeMoved();
            }
            else
            {
                UpdateAllSegmentSprites();
            }
        }

        Vector2 ToWorldCenter(Vector2Int subCell)
        {
            var w = SubGridHelper.SubCellToWorld(subCell, _grid);
            return new Vector2(w.x, w.y);
        }


        /// <summary>
        /// 智能判断是否需要更新拖动视觉效果
        /// </summary>
        bool ShouldUpdateDragVisuals()
        {
            float currentTime = Time.time;
            float timeSinceLastUpdate = currentTime - _lastDragUpdateTime;

            // 基本频率限制
            if (timeSinceLastUpdate < DRAG_UPDATE_INTERVAL)
            {
                return false;
            }

            // 正常情况下的更新
            _lastDragUpdateTime = currentTime;
            return true;
        }

        bool CheckOccupiedBySelfForword(Vector2Int bigcell)
        {
            if (_snakeManager == null)
            {
                return false;
            }

            var exclude = DragFromHead ? _currentHeadCell : _currentTailCell;
            var exclude2 = GetBodyCellsNextBigCell(DragFromHead);

            if (bigcell == exclude || bigcell == exclude2)
            {
                return true;
            }

            var cellset = _snakeManager.GetSnakeOccupiedCells(this);
            return !cellset.Contains(bigcell);
        }


        bool CheckOccupiedBySelfReverse(Vector2Int bigcell)
        {
            if (_snakeManager == null)
            {
                return false;
            }

            var exclude = DragFromHead ? GetTailBigCell() : GetHeadBigCell();
            var exclude2 = GetBodyCellsNextBigCell(!DragFromHead);

            if (bigcell == exclude || bigcell == exclude2)
            {
                return true;
            }

            var cellset = _snakeManager.GetSnakeOccupiedCells(this);
            return !cellset.Contains(bigcell);
        }

        Vector2Int GetBodyCellsNextBigCell(bool fromHead)
        {
            if (_subBodyCells == null || _subBodyCells.Count == 0)
                return Vector2Int.zero;

            if (fromHead)
            {
                var nextcellsub = GetSubBodyCellAtIndex(5);
                return SubGridHelper.SubCellToBigCell(nextcellsub);
            }
            else
            {
                var nextcellsub = GetSubBodyCellAtIndex(_subBodyCells.Count - 1 - 5);
                return SubGridHelper.SubCellToBigCell(nextcellsub);
            }

            return Vector2Int.zero;
        }

        bool CheckIfNeedReverse(Vector2 targetPos, out Vector2 reversePos)
        {
            reversePos = Vector2.zero;

            //先检查是否达成倒车条件--检查是否是头尾两格范围内的点
            var targetSubCell = SubGridHelper.WorldToSubCell(_leadTargetPos, _grid);
            if (DragFromHead)
            {
                var headsubcell = GetSubBodyCellAtIndex(0);
                if (targetSubCell == headsubcell)
                {
                    return true;
                }

                var headsubcell2 = GetSubBodyCellAtIndex(1);
                if (targetSubCell != headsubcell2)
                    return false;
            }
            else
            {
                var tailsubcell = GetSubBodyCellAtIndex(_subBodyCells.Count - 1);
                if (targetSubCell == tailsubcell)
                {
                    return true;
                }
                var tailsubcell2 = GetSubBodyCellAtIndex(_subBodyCells.Count - 1 - 1);
                if (targetSubCell != tailsubcell2)
                    return false;
            }

            //再检查倒车点
            if (DragFromHead)
            {
                //优先走大格
                var tail = GetTailBigCell();
                var prevSub = GetSubBodyCellAtIndex(_subBodyCells.Count - 1 - 5);
                var prev = SubGridHelper.SubCellToBigCell(prevSub);
                Vector2Int dir = tail - prev;
                Vector2Int left = new Vector2Int(-dir.y, dir.x);
                Vector2Int right = new Vector2Int(dir.y, -dir.x);
                var candidates = new[] { dir, left, right };
                for (int i = 0; i < candidates.Length; i++)
                {
                    var nextBig = tail + candidates[i];
                    if (!_grid.IsInside(nextBig)) continue;
                    if (IsPathBlocked(nextBig)) continue;
                    //if (!CheckOccupiedBySelfReverse(nextBig)) continue;//todo

                    EnqueueBigCellPath(GetTailSubCell(), nextBig, _subCellPathQueue, 1);
                    if (_subCellPathQueue.Count > 0)
                    {
                        reversePos = SubGridHelper.SubCellToWorld(_subCellPathQueue.First.Value, _grid);
                        return true;
                    }
                }

                //再走小格
                tail = GetTailSubCell();
                prev = GetSubBodyCellAtIndex(_subBodyCells.Count - 1 - 1);
                dir = tail - prev;
                left = new Vector2Int(-dir.y, dir.x);
                right = new Vector2Int(dir.y, -dir.x);
                candidates = new[] { dir, left, right };
                for (int i = 0; i < candidates.Length; i++)
                {
                    var nextHeadSub = tail + candidates[i];
                    var nextHeadBig = SubGridHelper.SubCellToBigCell(nextHeadSub);
                    if (!_grid.IsInside(nextHeadBig)) continue;
                    if (!_grid.IsInsideSub(nextHeadSub)) continue;
                    if (IsPathBlocked(nextHeadBig)) continue;
                    //if (!CheckOccupiedBySelfReverse(nextHeadBig)) continue;

                    EnqueueSubCellPath(_currentTailSubCell, nextHeadSub, _subCellPathQueue, 1);
                    if (_subCellPathQueue.Count > 0)
                    {
                        reversePos = SubGridHelper.SubCellToWorld(_subCellPathQueue.First.Value, _grid);
                        return true;
                    }
                }

                return false;
            }
            else
            {
                var head = GetHeadBigCell();
                var nextSub = GetSubBodyCellAtIndex(5);
                var next = SubGridHelper.SubCellToBigCell(nextSub); // 头部相邻的身体
                Vector2Int dir = head - next; // 远离身体方向
                Vector2Int left = new Vector2Int(-dir.y, dir.x);
                Vector2Int right = new Vector2Int(dir.y, -dir.x);
                var candidates = new[] { dir, left, right };

                //优先走大格
                for (int i = 0; i < candidates.Length; i++)
                {
                    var nextHeadBig = head + candidates[i];
                    if (!_grid.IsInside(nextHeadBig)) continue;
                    if (IsPathBlocked(nextHeadBig)) continue;
                    //if (!CheckOccupiedBySelfReverse(nextHeadBig)) continue;

                    EnqueueBigCellPath(GetHeadSubCell(), nextHeadBig, _subCellPathQueue, 1);

                    if (_subCellPathQueue.Count > 0)
                    {
                        reversePos = SubGridHelper.SubCellToWorld(_subCellPathQueue.First.Value, _grid);
                        return true;

                    }
                }

                //再走小格
                head = GetHeadSubCell();
                next = GetSubBodyCellAtIndex(1); // 头部相邻的身体
                dir = head - next; // 远离身体方向
                left = new Vector2Int(-dir.y, dir.x);
                right = new Vector2Int(dir.y, -dir.x);
                candidates = new[] { dir, left, right };
                for (int i = 0; i < candidates.Length; i++)
                {
                    var nextHeadSub = head + candidates[i];
                    var nextHeadBig = SubGridHelper.SubCellToBigCell(nextHeadSub);
                    if (!_grid.IsInside(nextHeadBig)) continue;
                    if (IsPathBlocked(nextHeadBig)) continue;
                    if (!_grid.IsInsideSub(nextHeadSub)) continue;
                    //if (!CheckOccupiedBySelfReverse(nextHeadBig)) continue;

                    EnqueueSubCellPath(GetHeadSubCell(), nextHeadSub, _subCellPathQueue, 1);

                    if (_subCellPathQueue.Count > 0)
                    {
                        reversePos = SubGridHelper.SubCellToWorld(_subCellPathQueue.First.Value, _grid);
                        return true;

                    }
                }

                return false;
            }

            return false;
        }

        bool CheckNextCell(Vector2Int nextSubCell)
        {

            if (!EnableSubGridMovement || _cachedSubRectTransforms.Count == 0 || _subBodyCells.Count == 0)
                return false;

            Vector2Int curCheckSubCell = GetHeadSubCell();
            if (!DragFromHead)
            {
                curCheckSubCell = GetTailSubCell();
            }


            Vector2Int nextBigCell = SubGridHelper.SubCellToBigCell(nextSubCell);
            var curHeadBigCell = SubGridHelper.SubCellToBigCell(curCheckSubCell);
            if (nextBigCell == curHeadBigCell)
            {
                //当前大格默认是可行走
                return true;
            }

            // 必须相邻
            if (Manhattan(curCheckSubCell, nextSubCell) != 1) return false;
            // 检查网格边界
            if (!_grid.IsInside(nextBigCell)) return false;
            // 使用与IsPathBlocked相同的阻挡检测逻辑，支持颜色匹配
            if (IsPathBlocked(nextBigCell)) return false;
            //if (!CheckOccupiedBySelfForword(nextBigCell)) return false; todo


            return true;
        }
        void EnqueueSubCellPath(Vector2Int fromSubCell, Vector2Int toSubCell, LinkedList<Vector2Int> pathList, int maxPathCount = 10)
        {
            if (pathList == null) return;
            pathList.Clear();
            if (fromSubCell == toSubCell) return;

            // 1) 参数校验：必须在中线上
            bool FromOnCenter = SubGridHelper.IsValidSubCell(fromSubCell);
            bool ToOnCenter = SubGridHelper.IsValidSubCell(toSubCell);
            if (!FromOnCenter || !ToOnCenter)
            {
                Debug.LogError($"EnqueueSubCellPath: 参数不在中线 from={fromSubCell}, to={toSubCell}");
                return;
            }

            // 2) 最短中线路径（仅在 x%5==2 的竖线和 y%5==2 的横线行走）
            Vector2Int cur = fromSubCell;

            // 使用非负取模的局部坐标判定在横/竖中线
            var fromLocal = SubGridHelper.GetSubCellLocalPos(fromSubCell);
            var toLocal = SubGridHelper.GetSubCellLocalPos(toSubCell);
            bool fromOnVertical = (fromLocal.x == SubGridHelper.CENTER_INDEX);
            bool fromOnHorizontal = (fromLocal.y == SubGridHelper.CENTER_INDEX);
            bool toOnVertical = (toLocal.x == SubGridHelper.CENTER_INDEX);
            bool toOnHorizontal = (toLocal.y == SubGridHelper.CENTER_INDEX);

            // 局部函数：按轴追加（包含终点），逐格入队，保证连续性与合法性
            void AppendVertical(int yTarget)
            {
                int step = yTarget > cur.y ? 1 : -1;
                while (cur.y != yTarget)
                {
                    var next = new Vector2Int(cur.x, cur.y + step);
                    if (!SubGridHelper.IsValidSubCell(next) || !SubGridHelper.IsSubCellInside(next, _grid))
                    {
                        Debug.LogError($"EnqueueSubCellPath: 竖向越界/非法 next={next}");
                        break;
                    }
                    pathList.AddLast(next);
                    if (pathList.Count > maxPathCount) return;
                    cur = next;
                }
            }
            void AppendHorizontal(int xTarget)
            {
                int step = xTarget > cur.x ? 1 : -1;
                while (cur.x != xTarget)
                {
                    var next = new Vector2Int(cur.x + step, cur.y);
                    if (!SubGridHelper.IsValidSubCell(next) || !SubGridHelper.IsSubCellInside(next, _grid))
                    {
                        Debug.LogError($"EnqueueSubCellPath: 横向越界/非法 next={next}");
                        break;
                    }
                    pathList.AddLast(next);
                    if (pathList.Count > maxPathCount) return;
                    cur = next;
                }
            }

            int HorCenter(int y) => (y / SubGridHelper.SUB_DIV) * SubGridHelper.SUB_DIV + SubGridHelper.CENTER_INDEX;
            int VerCenter(int x) => (x / SubGridHelper.SUB_DIV) * SubGridHelper.SUB_DIV + SubGridHelper.CENTER_INDEX;

            // 同线直走（无需拐弯）
            if (fromSubCell.x == toSubCell.x && fromOnVertical && toOnVertical)
            {
                AppendVertical(toSubCell.y);
            }
            else if (fromSubCell.y == toSubCell.y && fromOnHorizontal && toOnHorizontal)
            {
                AppendHorizontal(toSubCell.x);
            }
            else
            {
                // 需要一次转向
                if (fromOnVertical && toOnHorizontal)
                {
                    // 一竖一横：交点( from.x, to.y )
                    AppendVertical(toSubCell.y);
                    if (pathList.Count > maxPathCount) return;
                    AppendHorizontal(toSubCell.x);
                }
                else if (fromOnHorizontal && toOnVertical)
                {
                    // 一横一竖：交点( to.x, from.y )
                    AppendHorizontal(toSubCell.x);
                    if (pathList.Count > maxPathCount) return;
                    AppendVertical(toSubCell.y);
                }
                else if (fromOnVertical && toOnVertical)
                {
                    // 同为竖线：选择更优的水平中线作为转折（最短）
                    int yA = HorCenter(fromSubCell.y);
                    int yB = HorCenter(toSubCell.y);
                    int distA = Mathf.Abs(fromSubCell.y - yA) + Mathf.Abs(toSubCell.y - yA);
                    int distB = Mathf.Abs(fromSubCell.y - yB) + Mathf.Abs(toSubCell.y - yB);
                    int yPivot = distA <= distB ? yA : yB;

                    AppendVertical(yPivot);
                    if (pathList.Count > maxPathCount) return;
                    AppendHorizontal(toSubCell.x);
                    if (pathList.Count > maxPathCount) return;
                    if (cur != toSubCell) AppendVertical(toSubCell.y);
                }
                else if (fromOnHorizontal && toOnHorizontal)
                {
                    // 同为横线：选择更优的竖直中线作为转折（最短）
                    int xA = VerCenter(fromSubCell.x);
                    int xB = VerCenter(toSubCell.x);
                    int distA = Mathf.Abs(fromSubCell.x - xA) + Mathf.Abs(toSubCell.x - xA);
                    int distB = Mathf.Abs(fromSubCell.x - xB) + Mathf.Abs(toSubCell.x - xB);
                    int xPivot = distA <= distB ? xA : xB;

                    AppendHorizontal(xPivot);
                    if (pathList.Count > maxPathCount) return;
                    AppendVertical(toSubCell.y);
                    if (pathList.Count > maxPathCount) return;
                    if (cur != toSubCell) AppendHorizontal(toSubCell.x);
                }
                else
                {
                    Debug.LogError($"EnqueueSubCellPath: 起点不在中线 from={fromSubCell}");
                }
            }

            // 返回列表可包含终点；若有需要可去掉末尾 toSubCell
            // if (pathList.Count > 0 && pathList.Last.Value == toSubCell) pathList.RemoveLast();
        }

        void EnqueueBigCellPath(Vector2Int fromSubCell, Vector2Int toBigCell, LinkedList<Vector2Int> pathList, int maxPathCount = 10)
        {
            var toSubCell = SubGridHelper.BigCellToCenterSubCell(toBigCell);

            EnqueueSubCellPath(fromSubCell, toSubCell, pathList, maxPathCount);
        }


        bool AdvanceHeadToSubCellCoConsume(Vector2Int nextSubCell)
        {
            if (_subBodyCells == null || _subBodyCells.Count == 0) return true;

            var newHeadSubCell = nextSubCell;

            if (_subBodyCells.Count == 1)
            {
                _subBodyCells.First.Value = newHeadSubCell;
            }
            else
            {
                // 整条蛇朝尾部方向移动：在尾部添加新位置，移除头部
                _subBodyCells.AddFirst(newHeadSubCell);
                _subBodyCells.RemoveLast();

            }


            // 5) 刷新缓存
            _currentHeadSubCell = _subBodyCells.First.Value;
            _currentTailSubCell = _subBodyCells.Last.Value;
            _currentHeadCell = SubGridHelper.SubCellToBigCell(_currentHeadSubCell);
            _currentTailCell = SubGridHelper.SubCellToBigCell(_currentTailSubCell);
            UpdateCachedSubRectTransformsFromSubBodyCells();

            return true;
        }


        /// <summary>
        /// 获取指定索引的身体格子
        /// </summary>
        private Vector2Int GetSubBodyCellAtIndex(int index)
        {
            if (index >= _cachedSubRectTransforms.Count || index < 0)
                return Vector2Int.zero;

            var rt = _cachedSubRectTransforms[index];
            if(rt != null)
            {
                return SubGridHelper.WorldToSubCell(rt.anchoredPosition, _grid);
            }

            return Vector2Int.zero;
        }

        private Vector2Int GetBigBodyCellAtIndex(int index)
        {
            if (index >= _cachedSubRectTransforms.Count || index < 0)
                return Vector2Int.zero;

            var rt = _cachedSubRectTransforms[index];
            if (rt != null)
            {
                return SubGridHelper.WorldToBigCell(rt.anchoredPosition, _grid);
            }

            return Vector2Int.zero;
        }

        // 在指定位置插入新元素的方法
        public static void InsertAtPosition(LinkedList<Vector2Int> list, int position, Vector2Int newValue)
        {
            if (list == null)
                throw new System.ArgumentNullException(nameof(list));

            if (position < 1 || position > list.Count + 1)
                throw new System.ArgumentOutOfRangeException(nameof(position), "位置超出范围");

            // 如果插入位置是开头
            if (position == 1)
            {
                list.AddFirst(newValue);
                return;
            }

            // 如果插入位置是末尾
            if (position == list.Count + 1)
            {
                list.AddLast(newValue);
                return;
            }

            // 找到位置前的节点（第 position-1 个节点）
            LinkedListNode<Vector2Int> currentNode = list.First;
            for (int i = 1; i < position - 1; i++)
            {
                currentNode = currentNode.Next;
            }

            // 在当前节点后插入新元素
            list.AddAfter(currentNode, newValue);

        }


        /// <summary>
        /// 根据_subBodyCells更新_cachedSubRectTransforms
        /// </summary>
        private void UpdateCachedSubRectTransformsFromSubBodyCells()
        {
            if (!EnableSubGridMovement || _cachedSubRectTransforms.Count == 0 || _subBodyCells.Count == 0)
                return;

            // 遍历身体节点和对应的RectTransform
            var bodycelllist = _subBodyCells.ToList();
            for (int segmentIndex = 0; segmentIndex < _subBodyCells.Count; segmentIndex++)
            {
                var rt = _cachedSubRectTransforms[segmentIndex];
                if (rt != null)
                {
                    var subCell = bodycelllist[segmentIndex];
                    var worldPos = SubGridHelper.SubCellToWorld(subCell, _grid);

                    rt.anchoredPosition = new Vector2(worldPos.x, worldPos.y);
                }

            }

        }


        /// <summary>
        /// 根据_subBodyCells更新_cachedSubRectTransforms
        /// </summary>
        private void UpdateSubBodyCellsFromCachedSubRectTransforms()
        {
            if (!EnableSubGridMovement || _cachedSubRectTransforms.Count == 0 || _subBodyCells.Count == 0)
                return;

            // 遍历身体节点和对应的RectTransform
            _subBodyCells.Clear();
            for (int segmentIndex = 0; segmentIndex < _cachedSubRectTransforms.Count; segmentIndex++)
            {
                var rt = _cachedSubRectTransforms[segmentIndex];
                if (rt != null)
                {
                    var subCell = SubGridHelper.WorldToSubCell(rt.anchoredPosition, _grid);
                    _subBodyCells.AddLast(subCell);
                }

            }

        }


        bool AdvanceTailToSubCellCoConsume(Vector2Int nextSubCell)
        {
            if (_subBodyCells == null || _subBodyCells.Count == 0) return true;

            var newHeadSubCell = nextSubCell;

            if (_subBodyCells.Count == 1)
            {
                _subBodyCells.First.Value = newHeadSubCell;
            }
            else
            {
                // 整条蛇朝尾部方向移动：在尾部添加新位置，移除头部
                _subBodyCells.AddLast(newHeadSubCell);
                _subBodyCells.RemoveFirst();

            }


            // 5) 刷新缓存
            _currentHeadSubCell = _subBodyCells.First.Value;
            _currentTailSubCell = _subBodyCells.Last.Value;
            _currentHeadCell = SubGridHelper.SubCellToBigCell(_currentHeadSubCell);
            _currentTailCell = SubGridHelper.SubCellToBigCell(_currentTailSubCell);
            UpdateCachedSubRectTransformsFromSubBodyCells();

            return true;
        }

        public GridConfig GetGrid()
        {
            return _grid;
        }

        public override void SnapCellsToGrid()
        {
            if (_cachedSubRectTransforms == null)
                return;
            if (_cachedSubRectTransforms.Count == 0)
                return;


            Vector2Int[] newInitialBodyCells = new Vector2Int[Length];
            if(DragFromHead)
            {
                int segmentIndex = 0;
                for (int i = 0; i < _subBodyCells.Count;)
                {
                    if (i >= _subSegments.Count) break;
                    var rt = _subSegments[i].GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        var bigcell = SubGridHelper.WorldToBigCell(rt.anchoredPosition, _grid);
                        newInitialBodyCells[segmentIndex] = bigcell;
                    }

                    i += 5;
                    segmentIndex++;
                }
            }
            else
            {
                int segmentIndex = Length - 1;
                for (int i = _subBodyCells.Count - 1; i > 0;)
                {
                    if (i < 0) break;
                    var rt = _subSegments[i].GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        var bigcell = SubGridHelper.WorldToBigCell(rt.anchoredPosition, _grid);
                        newInitialBodyCells[segmentIndex] = bigcell;
                    }

                    i -= 5;
                    segmentIndex--;
                }
            }
            

            InitializeSubSegmentPositions(newInitialBodyCells);

            if (EnableBodySpriteManagement)
            {
                _bodySpriteManager?.OnSnakeMoved();
            }
        }

        public IEnumerator CoConsume(HoleEntity hole, bool fromHead)
        {
            _consuming = true;
            IsDragging = false; // 脱离手指控制
            _isReverse = false;
            Vector3 holeCenter = _grid.CellToWorld(hole.Cell);
            var holeCenterSubCell = SubGridHelper.BigCellToCenterSubCell(hole.Cell);

            LinkedList<GameObject> allegments = new LinkedList<GameObject>();
            Transform segmentToConsume = null;

            foreach (var gameObject in _subSegments)
            {
                if (gameObject != null)
                    allegments.AddLast(gameObject);
            }

            LinkedList<Vector2Int> pathList = new LinkedList<Vector2Int>();
            // 逐段进入洞并消失，保持身体连续性
            while (_subBodyCells.Count > 0)
            {
                if (fromHead)
                {
                    while (_subBodyCells.Count > 0 && Manhattan(_currentTailSubCell, holeCenterSubCell) != 1)
                    {
                        pathList.Clear();
                        EnqueueSubCellPath(_currentHeadSubCell, holeCenterSubCell, pathList, 1);

                        if (pathList.Count > 0)
                        {
                            AdvanceHeadToSubCellCoConsume(pathList.First.Value);
                            pathList.Clear();

                            //更新身体图片
                            if (EnableBodySpriteManagement)
                            {
                                _bodySpriteManager?.OnSnakeMoved();
                            }
                            else
                            {
                                UpdateAllSegmentSprites();
                            }

                            yield return new WaitForSeconds(hole.ConsumeInterval);
                        }
                        else if (_currentHeadSubCell == holeCenterSubCell)
                        {
                            AdvanceHeadToSubCellCoConsume(holeCenterSubCell);
                            break;
                        }
                        else
                        {
                            _consuming = false;
                            _consumeCoroutine = null;
                            Debug.LogError("CoConsume error! can not find path to hole!");
                            yield break;
                        }

                    }

                    _subBodyCells.RemoveLast();
                    _subSegments.RemoveAt(_subSegments.Count - 1);

                    if (allegments.Count > 0)
                    {
                        segmentToConsume = allegments.Last.Value.transform;
                        allegments.RemoveLast();

                        Destroy(segmentToConsume.gameObject);
                    }

                    // 更新当前头尾缓存，防止空引用
                    if (_subBodyCells.Count > 0)
                    {
                        _currentHeadSubCell = _subBodyCells.First.Value;
                        _currentTailSubCell = _subBodyCells.Last.Value;
                    }

                    //更新身体图片
                    if (EnableBodySpriteManagement)
                    {
                        _bodySpriteManager?.OnSnakeMoved();
                    }
                    else
                    {
                        UpdateAllSegmentSprites();
                    }

                    if (_subBodyCells.Count == 0)
                        break;

                    yield return new WaitForSeconds(hole.ConsumeInterval);
                }
                else
                {
                    while (Manhattan(_currentTailSubCell, holeCenterSubCell) != 1)
                    {
                        pathList.Clear();
                        EnqueueSubCellPath(_currentTailSubCell, holeCenterSubCell, pathList, 1);

                        if (pathList.Count > 0)
                        {
                            AdvanceTailToSubCellCoConsume(pathList.First.Value);
                            pathList.Clear();
                        }
                        else
                        {
                            _consuming = false;
                            _consumeCoroutine = null;
                            Debug.LogError("CoConsume error! can not find path to hole!");
                            yield break;
                        }

                        //更新身体图片
                        if (EnableBodySpriteManagement)
                        {
                            _bodySpriteManager?.OnSnakeMoved();
                        }
                        else
                        {
                            UpdateAllSegmentSprites();
                        }

                        yield return new WaitForSeconds(hole.ConsumeInterval);
                    }

                    _subBodyCells.RemoveLast();
                    _subSegments.RemoveAt(_subSegments.Count - 1);

                    if (allegments.Count > 0)
                    {
                        segmentToConsume = allegments.Last.Value.transform;
                        allegments.RemoveLast();

                        Destroy(segmentToConsume.gameObject);
                    }

                    // 更新当前头尾缓存，防止空引用
                    if (_subBodyCells.Count > 0)
                    {
                        _currentHeadSubCell = _subBodyCells.First.Value;
                        _currentTailSubCell = _subBodyCells.Last.Value;
                    }
                }

            }

            _consuming = false;
            _consumeCoroutine = null;

            // 全部消失后，销毁蛇对象或重生；此处直接销毁
            if (_subBodyCells.Count == 0)
            {
                Destroy(gameObject);
                _snakeManager.TryClearSnakes();
            }
        }

        /// <summary>
        /// 查找目标位置本身或邻近位置的洞
        /// </summary>
        HoleEntity FindHoleAtOrAdjacentWithColor(Vector2Int targetCell, SnakeColorType color)
        {
            _cacheCoconsumeHoles.Clear();
            HoleEntity hole;
            // 检查是否被实体阻挡
            if (_entityManager != null)
            {
                var entities = _entityManager.HoleEntities;
                if (entities != null)
                {
                    foreach (var entity in entities)
                    {
                        hole = (HoleEntity)entity;
                        // 检查目标位置本身是否是洞的位置
                        if (hole.Cell == targetCell && hole.ColorType == color)
                        {
                            _cacheCoconsumeHoles.Add(hole);
                            return hole;
                        }
                        // 检查目标位置是否邻近洞
                        if (hole.IsAdjacent(targetCell) && hole.ColorType == color)
                        {
                            return hole;
                        }
                    }
                }
            }

            return null;
        }

        bool IsPathBlocked(Vector2Int bigCell)
        {
            // 检查是否被实体阻挡
            if (_entityManager != null)
            {
                var entities = _entityManager.GetAt(bigCell);
                if (entities != null)
                {
                    foreach (var entity in entities)
                    {
                        if (entity is WallEntity)
                        {
                            return true; // 墙体总是阻挡
                        }
                        else if (entity is HoleEntity holeEntity)
                        {
                            // 洞的阻挡取决于颜色匹配
                            if (holeEntity.IsBlockingCell(bigCell, this))
                            {
                                return true; // 颜色不匹配，洞算作阻挡物
                            }
                        }
                    }
                }
            }

            // 检查是否被其他蛇阻挡
            if (_snakeManager != null && _snakeManager.IsCellOccupiedByOtherSnakes(bigCell, this))
            {
                return true;
            }

            return false;
        }

        Vector3 ScreenToWorld(Vector3 screen)
        {
            // UI渲染模式：使用UI坐标转换
            if (_parentCanvas != null)
            {
                var rect = transform.parent as RectTransform; // GridContainer
                if (rect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, _parentCanvas.worldCamera, out Vector2 localPoint))
                {
                    return new Vector3(localPoint.x, localPoint.y, 0f);
                }
            }
            else
            {
                _parentCanvas = GetComponentInParent<Canvas>();
                Debug.LogError("cache _parentCanvas error!!!");
            }

            // 最后的fallback：简单的比例转换
            Vector2 screenSize = new Vector2(Screen.width, Screen.height);
            Vector2 normalizedScreen = new Vector2(screen.x / screenSize.x, screen.y / screenSize.y);

            // 假设网格居中在屏幕中，计算相对位置
            float gridWidth = _grid.Width * _grid.CellSize;
            float gridHeight = _grid.Height * _grid.CellSize;

            float worldX = (normalizedScreen.x - 0.5f) * gridWidth;
            float worldY = (normalizedScreen.y - 0.5f) * gridHeight;

            return new Vector3(worldX, worldY, 0f);

        }

        Vector3 ScreenToWorldCenter(Vector3 screen)
        {
            // 1) 屏幕 → 世界
            var world = ScreenToWorld(screen);
            if (_grid.Width == 0 || _grid.Height == 0) return world;

            // 2) 世界 → 夹紧到有效大格
            var bigCell = SubGridHelper.WorldToBigCell(world, _grid);
            var bigCenter = _grid.CellToWorld(bigCell);

            // 3) 吸附到该大格的最近“中线”，但沿中线方向不量化到小格中心
            float half = 0.5f * _grid.CellSize;
            Vector3 off = world - bigCenter;

            // 先把偏移限制在当前大格范围内
            float ox = Mathf.Clamp(off.x, -half, half);
            float oy = Mathf.Clamp(off.y, -half, half);

            // 选择更近的中线：竖直中线(x=0)或水平中线(y=0)
            if (Mathf.Abs(ox) <= Mathf.Abs(oy))
            {
                // 竖直中线：x=0，y保持（已夹紧）
                ox = 0f;
            }
            else
            {
                // 水平中线：y=0，x保持（已夹紧）
                oy = 0f;
            }

            return new Vector3(bigCenter.x + ox, bigCenter.y + oy, 0f);
        }

        void OnGUI()
        {
            if (!DebugShowLeadTarget) return;
            if (_grid.Width == 0 || _grid.Height == 0) return;

            // 取容器 RectTransform（与 ScreenToWorld 中一致的父容器）
            var container = transform.parent as RectTransform;
            if (container == null) return;

            // 将“网格世界坐标系”的 _leadTargetPos 映射到屏幕坐标
            // 先把局部(anchored)坐标转换为世界坐标，再转屏幕坐标
            Vector3 worldPoint = container.TransformPoint(new Vector3(_leadTargetPos.x, _leadTargetPos.y, 0f));
            var cam = GetComponentInParent<Canvas>()?.worldCamera;
            Vector2 sp = RectTransformUtility.WorldToScreenPoint(cam, worldPoint);

            // OnGUI 的 y 轴从顶向下，需要翻转
            float sx = sp.x;
            float sy = Screen.height - sp.y;

            // 画一个十字与坐标文本
            var prev = GUI.color;
            GUI.color = DebugLeadTargetColor;

            float s = DebugLeadMarkerSize;
            GUI.DrawTexture(new Rect(sx - 1f, sy - s, 2f, 2f * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(sx - s, sy - 1f, 2f * s, 2f), Texture2D.whiteTexture);

            GUI.Label(new Rect(sx + s + 4f, sy - 12f, 260f, 24f),
                $"leadTarget ({_leadTargetPos.x:F2},{_leadTargetPos.y:F2})");

            GUI.color = prev;
        }

        void OnDrawGizmosSelected()
        {
            //if (!DrawDebugGizmos) return;
            //if (_grid.Width == 0) return;
            //Gizmos.color = new Color(0f, 1f, 0f, 0.4f);
            //foreach (var c in _bodyCells)
            //{
            //    Gizmos.DrawWireCube(_grid.CellToWorld(c), new Vector3(_grid.CellSize, _grid.CellSize, 0f));
            //}
            //Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
            //Vector3 prev = Vector3.negativeInfinity;
            //foreach (var c in _pathQueue)
            //{
            //    var p = _grid.CellToWorld(c);
            //    Gizmos.DrawSphere(p, 0.05f);
            //    if (prev.x > -10000f) Gizmos.DrawLine(prev, p);
            //    prev = p;
            //}
        }

        /// <summary>
        /// 获取蛇头的格子位置
        /// </summary>
        public Vector2Int GetHeadBigCell()
        {
            if (_cachedSubRectTransforms != null && _cachedSubRectTransforms.Count > 0)
            {
                return SubGridHelper.WorldToBigCell(_cachedSubRectTransforms[0].anchoredPosition, _grid);
            }
            return Vector2Int.zero;
        }

        /// <summary>
        /// 获取蛇尾的格子位置
        /// </summary>
        public Vector2Int GetTailBigCell()
        {
            if (_cachedSubRectTransforms != null && _cachedSubRectTransforms.Count > 0)
            {
                return SubGridHelper.WorldToBigCell(_cachedSubRectTransforms[_cachedSubRectTransforms.Count - 1].anchoredPosition, _grid);
            }
            return Vector2Int.zero;
        }

        /// <summary>
        /// 获取蛇头的格子位置
        /// </summary>
        public Vector2Int GetHeadSubCell()
        {
            if (_cachedSubRectTransforms != null && _cachedSubRectTransforms.Count > 0)
            {
                return SubGridHelper.WorldToSubCell(_cachedSubRectTransforms[0].anchoredPosition, _grid);
            }
            return Vector2Int.zero;
        }

        /// <summary>
        /// 获取蛇尾的格子位置
        /// </summary>
        public Vector2Int GetTailSubCell()
        {
            if (_cachedSubRectTransforms != null && _cachedSubRectTransforms.Count > 0)
            {
                return SubGridHelper.WorldToSubCell(_cachedSubRectTransforms[_cachedSubRectTransforms.Count-1].anchoredPosition, _grid);
            }
            return Vector2Int.zero;
        }

        /// <summary>
        /// 清理缓存的RectTransform组件，防止内存泄漏
        /// </summary>
        void CleanupCachedComponents()
        {
            // 清理已销毁的RectTransform引用
            for (int i = _cachedSubRectTransforms.Count - 1; i >= 0; i--)
            {
                if (_cachedSubRectTransforms[i] == null)
                {
                    _cachedSubRectTransforms.RemoveAt(i);
                }
            }
        }



        protected override void OnDestroy()
        {
            base.OnDestroy();
        }
    }
}


