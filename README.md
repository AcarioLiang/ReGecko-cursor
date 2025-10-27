# Gecko Out - Unity Game Project

基于《Gecko Out（Slither Rush）》游戏分析的Unity实现项目。

## 核心玩法
- 单指拖动壁虎穿梭在狭窄关卡中
- 躲避动态/静态障碍物，收集金币和增益道具
- 在倒计时或血量归零前到达终点/获得高分

## 项目结构
```
Assets/
├── Scripts/           # 核心脚本
│   ├── Core/         # 核心系统
│   ├── Player/       # 玩家控制
│   ├── Level/        # 关卡系统
│   ├── UI/           # 用户界面
│   └── Utils/        # 工具类
├── Prefabs/          # 预制体
├── Materials/         # 材质
├── Scenes/           # 场景
└── Settings/         # 项目设置
```

## 技术特点
- 平滑的触摸拖动系统
- 动态尾巴跟随系统
- 模块化关卡生成
- 完整的碰撞检测
- 丰富的视觉反馈

## 开发环境
- Unity 2022.3 LTS
- C# 脚本
- 2D游戏架构