using UnityEngine;
using UnityEngine.UI;

namespace ReGecko.HingeJointSnake
{
    /// <summary>
    /// 蛇段类型枚举
    /// </summary>
    public enum SegmentType
    {
        Head,       // 蛇头
        Body,       // 蛇身
        Tail,       // 蛇尾
        Joint       // 转弯关节
    }

    /// <summary>
    /// 蛇段组件 - 管理蛇的单个段落，包含物理和视觉组件
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Image))]
    public class SnakeSegment : MonoBehaviour
    {
        [Header("段落配置")]
        [SerializeField] private SegmentType _segmentType = SegmentType.Body;
        [SerializeField] private int _segmentIndex = 0; // 在蛇中的索引位置
        
        [Header("视觉配置")]
        [SerializeField] private Sprite _headSprite;
        [SerializeField] private Sprite _bodySprite;
        [SerializeField] private Sprite _tailSprite;
        [SerializeField] private Sprite _jointSprite;
        
        [Header("尺寸配置")]
        [SerializeField] private bool _isJoint = false; // 是否为转弯关节
        
        // 组件引用
        private RectTransform _rectTransform;
        private Rigidbody2D _rigidbody;
        private Image _image;
        private HingeJoint2D _hingeJoint;
        
        // 父蛇引用
        private HingeJointSnakeController _parentSnake;
        
        // 属性
        public SegmentType Type => _segmentType;
        public int Index => _segmentIndex;
        public RectTransform RectTransform => _rectTransform;
        public Rigidbody2D Rigidbody => _rigidbody;
        public Image Image => _image;
        public HingeJoint2D HingeJoint => _hingeJoint;
        public HingeJointSnakeController ParentSnake => _parentSnake;
        
        /// <summary>
        /// 是否为蛇头
        /// </summary>
        public bool IsHead => _segmentType == SegmentType.Head;
        
        /// <summary>
        /// 是否为蛇尾
        /// </summary>
        public bool IsTail => _segmentType == SegmentType.Tail;
        
        /// <summary>
        /// 是否可以触发拖拽（蛇头或蛇尾）
        /// </summary>
        public bool CanTriggerDrag => IsHead || IsTail;

        void Awake()
        {
            InitializeComponents();
        }

        /// <summary>
        /// 初始化组件
        /// </summary>
        private void InitializeComponents()
        {
            _rectTransform = GetComponent<RectTransform>();
            _rigidbody = GetComponent<Rigidbody2D>();
            _image = GetComponent<Image>();
            
            // 设置默认物理属性
            _rigidbody.gravityScale = 0f; // 2D UI不需要重力
            _rigidbody.angularDrag = 5f;  // 适当的角度阻尼
            _rigidbody.drag = 1f;         // 适当的线性阻尼
        }

        /// <summary>
        /// 初始化蛇段
        /// </summary>
        public void Initialize(HingeJointSnakeController parentSnake, SegmentType type, int index, Color color, bool isJoint = false)
        {
            _parentSnake = parentSnake;
            _segmentType = type;
            _segmentIndex = index;
            _isJoint = isJoint;
            
            // 设置名称
            gameObject.name = $"SnakeSegment_{type}_{index}";
            
            // 设置视觉
            UpdateVisual(color);
            
            // 设置物理属性
            SetupPhysics();
            
            // 设置尺寸
            UpdateSize();
        }
        
        /// <summary>
        /// 更新段落尺寸
        /// </summary>
        private void UpdateSize()
        {
            if (_rectTransform == null) return;
            
            float cellSize = _parentSnake != null ? _parentSnake.CellSize : 1.0f;
            
            if (_isJoint || _segmentType == SegmentType.Joint)
            {
                // 转弯关节的尺寸：宽度对齐网格，高度为格子大小的三分之一
                _rectTransform.sizeDelta = new Vector2(cellSize, cellSize / 3f);
            }
            else
            {
                // 蛇头、蛇身、蛇尾的尺寸为一个格子大小
                _rectTransform.sizeDelta = new Vector2(cellSize, cellSize);
            }
        }

        /// <summary>
        /// 更新视觉表现
        /// </summary>
        public void UpdateVisual(Color color)
        {
            if (_image == null) return;
            
            // 根据段落类型设置精灵
            Sprite targetSprite = _segmentType switch
            {
                SegmentType.Head => _headSprite,
                SegmentType.Body => _bodySprite,
                SegmentType.Tail => _tailSprite,
                SegmentType.Joint => _jointSprite,
                _ => _bodySprite
            };
            
            _image.sprite = targetSprite;
            _image.color = color;
            
            // 如果是转弯关节，使用半透明颜色
            if (_isJoint || _segmentType == SegmentType.Joint)
            {
                Color jointColor = color;
                jointColor.a = 0.7f; // 设置透明度
                _image.color = jointColor;
            }
        }

        /// <summary>
        /// 设置物理属性
        /// </summary>
        private void SetupPhysics()
        {
            if (_rigidbody == null) return;
            
            // 根据段落类型设置不同的物理属性
            switch (_segmentType)
            {
                case SegmentType.Head:
                    _rigidbody.mass = 1.2f; // 蛇头稍重
                    break;
                case SegmentType.Body:
                    _rigidbody.mass = 1.0f; // 标准质量
                    break;
                case SegmentType.Tail:
                    _rigidbody.mass = 0.8f; // 蛇尾稍轻
                    break;
                case SegmentType.Joint:
                    _rigidbody.mass = 0.5f; // 转弯关节更轻
                    break;
            }
            
            // 如果是转弯关节，设置更小的阻力
            if (_isJoint || _segmentType == SegmentType.Joint)
            {
                _rigidbody.drag = 0.5f;
                _rigidbody.angularDrag = 2.0f;
            }
        }

        /// <summary>
        /// 设置铰链关节连接到前一个段落
        /// </summary>
        public void SetupHingeJoint(Rigidbody2D connectedBody, Vector2Int direction = default)
        {
            if (connectedBody == null) return;
            
            // 如果已有关节则先移除
            if (_hingeJoint != null)
            {
                DestroyImmediate(_hingeJoint);
            }
            
            // 添加铰链关节
            _hingeJoint = gameObject.AddComponent<HingeJoint2D>();
            _hingeJoint.connectedBody = connectedBody;
            _hingeJoint.autoConfigureConnectedAnchor = false;
            
            // 计算连接锚点 - 图片默认朝向Y轴向上
            Vector2 anchorOffset = Vector2.zero;
            Vector2 connectedAnchorOffset = Vector2.zero;
            
            // 根据连接方向设置锚点
            if (direction.y > 0) // 当前段落在前一个段落上方
            {
                // 当前段落的底部连接到前一个段落的顶部
                anchorOffset = new Vector2(0, -0.5f);
                connectedAnchorOffset = new Vector2(0, 0.5f);
            }
            else if (direction.y < 0) // 当前段落在前一个段落下方
            {
                // 当前段落的顶部连接到前一个段落的底部
                anchorOffset = new Vector2(0, 0.5f);
                connectedAnchorOffset = new Vector2(0, -0.5f);
            }
            else if (direction.x > 0) // 当前段落在前一个段落右侧
            {
                // 当前段落的左侧连接到前一个段落的右侧
                anchorOffset = new Vector2(-0.5f, 0);
                connectedAnchorOffset = new Vector2(0.5f, 0);
            }
            else if (direction.x < 0) // 当前段落在前一个段落左侧
            {
                // 当前段落的右侧连接到前一个段落的左侧
                anchorOffset = new Vector2(0.5f, 0);
                connectedAnchorOffset = new Vector2(-0.5f, 0);
            }
            
            _hingeJoint.anchor = anchorOffset;
            _hingeJoint.connectedAnchor = connectedAnchorOffset;
            
            // 设置关节限制（几乎不允许摆动，保持严格对齐）
            _hingeJoint.useLimits = true;
            _hingeJoint.limits = new JointAngleLimits2D
            {
                min = -1f,  // 最小角度
                max = 1f    // 最大角度
            };
            
            // 添加固定关节，确保段落之间严格保持位置关系
            FixedJoint2D fixedJoint = gameObject.AddComponent<FixedJoint2D>();
            fixedJoint.connectedBody = connectedBody;
            fixedJoint.autoConfigureConnectedAnchor = false;
            fixedJoint.breakForce = float.PositiveInfinity; // 永不断开
            
            // 设置关节距离限制，保持段落间距
            // 使用额外的距离关节来控制段落间距
            DistanceJoint2D distanceJoint = gameObject.AddComponent<DistanceJoint2D>();
            distanceJoint.connectedBody = connectedBody;
            distanceJoint.autoConfigureDistance = false;
            distanceJoint.distance = 1.0f; // 设置为1.0，与网格大小相匹配
            distanceJoint.maxDistanceOnly = false; // 同时限制最小和最大距离
            distanceJoint.breakForce = float.PositiveInfinity; // 永不断开
            
            // 启用关节马达以保持连接稳定
            _hingeJoint.useMotor = false; // 默认不使用马达，需要时可启用
            
            Debug.Log($"设置铰链关节，方向：{direction}，锚点：{anchorOffset}，连接锚点：{connectedAnchorOffset}");
        }

        /// <summary>
        /// 移除铰链关节
        /// </summary>
        public void RemoveHingeJoint()
        {
            if (_hingeJoint != null)
            {
                // 使用Destroy而不是DestroyImmediate，避免多次销毁问题
                Destroy(_hingeJoint);
                _hingeJoint = null;
            }
            
            // 同时移除距离关节
            DistanceJoint2D distanceJoint = GetComponent<DistanceJoint2D>();
            if (distanceJoint != null)
            {
                Destroy(distanceJoint);
            }
            
            // 移除固定关节
            FixedJoint2D fixedJoint = GetComponent<FixedJoint2D>();
            if (fixedJoint != null)
            {
                Destroy(fixedJoint);
            }
        }

        /// <summary>
        /// 设置段落位置
        /// </summary>
        public void SetPosition(Vector3 position)
        {
            if (_rectTransform != null)
            {
                _rectTransform.anchoredPosition = position;
            }
        }

        /// <summary>
        /// 获取段落位置
        /// </summary>
        public Vector3 GetPosition()
        {
            return _rectTransform != null ? _rectTransform.anchoredPosition : Vector3.zero;
        }

        /// <summary>
        /// 设置段落旋转
        /// </summary>
        public void SetRotation(float angle)
        {
            if (_rectTransform != null)
            {
                _rectTransform.rotation = Quaternion.Euler(0, 0, angle);
            }
        }

        /// <summary>
        /// 获取段落旋转角度
        /// </summary>
        public float GetRotationAngle()
        {
            return _rectTransform != null ? _rectTransform.eulerAngles.z : 0f;
        }
        
        // 使用OnDisable而不是OnDestroy，确保在销毁前清理
        void OnDisable()
        {
            // 只清理组件，不销毁
            if (_hingeJoint != null)
            {
                _hingeJoint.connectedBody = null;
                _hingeJoint.enabled = false;
            }
            
            DistanceJoint2D distanceJoint = GetComponent<DistanceJoint2D>();
            if (distanceJoint != null)
            {
                distanceJoint.connectedBody = null;
                distanceJoint.enabled = false;
            }
            
            FixedJoint2D fixedJoint = GetComponent<FixedJoint2D>();
            if (fixedJoint != null)
            {
                fixedJoint.connectedBody = null;
                fixedJoint.enabled = false;
            }
        }
    }
}
