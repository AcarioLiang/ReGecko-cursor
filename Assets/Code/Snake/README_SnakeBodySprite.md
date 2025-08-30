# 蛇身体图片管理系统

## 概述

蛇身体图片管理系统是一个动态更新蛇身体各部位图片的系统，能够根据蛇的移动方向和身体形状自动选择合适的图片并应用正确的旋转。

## 功能特性

### 🎯 智能图片选择
- **蛇头**: 根据与下一节身体的方向关系，自动选择水平或竖直图片
- **身体段**: 检测拐角，使用L形状图片；直线段使用竖直身体图片
- **蛇尾**: 根据与前一节身体的方向关系，自动选择水平或竖直图片

### 🔄 自动旋转
- 水平移动时自动旋转90度
- L形状图片根据拐角方向自动计算旋转角度
- 支持自定义旋转角度

### ⚙️ 配置管理
- 支持ScriptableObject配置文件
- Inspector中实时预览和调整
- 运行时动态更新

## 使用方法

### 1. 基本设置

在SnakeController上启用身体图片管理：

```csharp
[Header("Body Sprite Management")]
public bool EnableBodySpriteManagement = true;
```

### 2. 图片资源设置

#### 方法1: 直接在Inspector中设置
在SnakeBodySpriteManager组件中直接拖拽图片资源：
- `VerticalHeadSprite`: 竖直方向的蛇头图片
- `VerticalTailSprite`: 竖直方向的蛇尾图片  
- `VerticalBodySprite`: 竖直方向的身体图片
- `LTurnBodySprite`: L方向转弯的身体图片

#### 方法2: 使用配置文件
1. 创建配置文件：右键 → Create → ReGecko → Snake Body Sprite Config
2. 在配置文件中设置图片资源
3. 将配置文件拖拽到SnakeBodySpriteManager的Config字段

### 3. 图片要求

#### 蛇头图片
- 默认方向：竖直向上
- 水平移动时自动旋转90度

#### 蛇尾图片
- 默认方向：竖直向上
- 水平移动时自动旋转90度

#### 身体图片
- 默认方向：竖直向上
- 水平移动时自动旋转90度

#### L转弯图片
- 默认方向：向右上角弯曲
- 根据实际拐角方向自动计算旋转角度

## 技术实现

### 核心组件

#### SnakeBodySpriteManager
- 管理所有身体段的图片
- 实时计算移动方向和拐角
- 自动应用正确的图片和旋转

#### SnakeController集成
- 在蛇移动后自动调用图片更新
- 在蛇长度改变后重新收集段并更新图片
- 支持启用/禁用图片管理功能

### 更新时机

图片更新在以下时机自动触发：
1. **蛇移动后**: `OnSnakeMoved()`
2. **蛇长度改变后**: `OnSnakeLengthChanged()`
3. **强制刷新**: `ForceRefreshAllSprites()`
4. **配置改变后**: 自动调用更新

### 方向检测算法

#### 蛇头/蛇尾方向检测
```csharp
var direction = nextCell - currentCell;
var isHorizontal = Mathf.Abs(direction.x) > Mathf.Abs(direction.y);
```

#### 身体拐角检测
```csharp
bool isCorner = prevDirection != nextDirection;
```

#### L图片旋转计算
根据前后方向向量计算8种可能的拐角情况，自动应用正确的旋转角度。

## 性能优化

- 只在必要时更新图片（移动后、长度改变后）
- 延迟初始化，等待SnakeController构建完成
- 缓存段引用，避免重复查找

## 调试和故障排除

### 常见问题

1. **图片不显示**
   - 检查图片资源是否正确设置
   - 确认SnakeController已启用图片管理
   - 查看Console中的错误信息

2. **旋转不正确**
   - 检查图片的默认方向
   - 调整RotationAngle设置
   - 确认L图片的默认弯曲方向

3. **性能问题**
   - 减少不必要的图片更新调用
   - 检查图片资源大小和格式

### 调试功能

- 在Inspector中实时调整设置
- 使用`ForceRefreshAllSprites()`强制刷新
- 查看Console中的更新日志

## 扩展功能

### 自定义图片类型
可以通过修改`UpdateBodySprite`方法添加更多图片类型：
- 特殊拐角图片
- 不同长度的身体段图片
- 动画序列图片

### 动态配置
支持运行时动态切换配置文件，实现不同主题的蛇外观。

## 示例代码

```csharp
// 手动更新所有段的图片
var spriteManager = GetComponent<SnakeBodySpriteManager>();
spriteManager.UpdateAllSegmentSprites();

// 强制刷新
spriteManager.ForceRefreshAllSprites();

// 响应蛇移动
spriteManager.OnSnakeMoved();

// 响应蛇长度改变
spriteManager.OnSnakeLengthChanged();
```

## 注意事项

1. 确保所有图片资源都已正确设置
2. 图片的默认方向要与系统期望一致
3. 在修改配置后，系统会自动更新
4. 支持运行时启用/禁用图片管理功能
