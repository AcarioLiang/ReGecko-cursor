# HingeJoint2D 蛇系统使用说明

## 概述
基于 HingeJoint2D 的蛇类实现，支持顺滑的拖拽移动和物理连接。

## 主要特性

### 1. 蛇段管理
- **蛇头 (Head)**: 可拖拽，控制整条蛇的移动
- **蛇身 (Body)**: 通过 HingeJoint2D 连接，自然跟随
- **蛇尾 (Tail)**: 可拖拽，支持反向控制

### 2. 拖拽系统
- **主方向移动**: 在拖拽的主要方向上连续移动
- **次方向对齐**: 自动对齐到网格中心
- **网格吸附**: 松开时自动对齐到最近的网格边缘

### 3. 物理特性
- 使用 HingeJoint2D 实现段落间的自然连接
- 支持角度限制，防止过度弯曲
- 可配置的弹簧和阻尼参数

## 使用方法

### 1. 配置蛇的初始状态
```csharp
var snakeConfig = new SnakeInitConfig
{
    Id = "player_snake",
    Name = "玩家蛇",
    Length = 5,
    HeadCell = new Vector2Int(2, 2),
    ColorType = SnakeColorType.Green,
    BodyCells = new []
    {
        new Vector2Int(2, 2), // 蛇头
        new Vector2Int(2, 3), // 身体
        new Vector2Int(2, 4), // 身体
        new Vector2Int(2, 5), // 身体
        new Vector2Int(2, 6)  // 蛇尾
    },
    IsControllable = true
};
```

### 2. 拖拽操作
- 点击蛇头或蛇尾开始拖拽
- 主方向（水平/垂直）连续移动
- 次方向自动对齐网格
- 松开鼠标自动吸附到网格

### 3. 参数调整
在 HingeJointSnakeController 中可调整：
- `_dragSmoothness`: 拖拽平滑度
- `_gridSnapDistance`: 网格吸附距离
- `_jointSpring`: 关节弹簧强度
- `_jointDamping`: 关节阻尼

## 兼容性
- 完全兼容现有的 BaseSnake 接口
- 支持 SnakeManager 的统一管理
- 保持原有的碰撞检测和状态管理

## 文件结构
- `SnakeSegment.cs`: 蛇段组件
- `HingeJointSnakeController.cs`: 主控制器
- `SnakeDragHandler.cs`: 拖拽事件处理器
