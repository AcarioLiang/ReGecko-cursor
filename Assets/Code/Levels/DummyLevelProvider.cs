using UnityEngine;
using ReGecko.GridSystem;
using ReGecko.Game;
using ReGecko.SnakeSystem;

namespace ReGecko.Levels
{
	public class DummyLevelProvider : MonoBehaviour
	{
		[Header("资源引用")]
		public Sprite SnakeBodySprite;
		public Sprite WallSprite;
		public Sprite HoleSprite;
		
		[Header("蛇身体配置")]
		public SnakeBodySpriteConfig SnakeBodyConfig;

		public LevelConfig GetLevel(int lv = 0)
		{
            //////////////////////////////////////////////////GridConfig////////////////////////////////////////////////////////
            var gridCfg = new GridConfig
			{
				Width = 10,
				Height = 10,
				CellSize = 64f
			};

            var gridCfg1= new GridConfig
            {
                Width = 4,
                Height = 6,
                CellSize = 64f
            };

            var gridCfg2 = new GridConfig
            {
                Width = 6,
                Height = 5,
                CellSize = 64f
            };

            var gridCfg3 = new GridConfig
            {
                Width = 5,
                Height = 6,
                CellSize = 64f
            };

            //////////////////////////////////////////////////GridConfig end////////////////////////////////////////////////////////
            ///
            //////////////////////////////////////////////////SnakeInitConfig////////////////////////////////////////////////////////
            

            // 创建多条蛇，配置不同的颜色类型
            var snakesCfg = new SnakeInitConfig[]
			{
				// 第一条蛇 - 红色玩家控制
				new SnakeInitConfig
				{
					Id = "snake_3",
					Name = "玩家蛇3",
					Length = 3,
					HeadCell = new Vector2Int(2, 3),
					Color = SnakeColorType.Purple.ToUnityColor(),
					ColorType = SnakeColorType.Purple, // 红色类型
					BodySprite = SnakeBodySprite,
					MoveSpeed = 16f,
					IsControllable = true,
					BodyCells = new []
					{
						new Vector2Int(2, 3),
						new Vector2Int(2, 2),
						new Vector2Int(2, 1)
					}
				},
				
				// 第二条蛇 - 蓝色AI控制
				new SnakeInitConfig
				{
					Id = "snake_4",
					Name = "玩家蛇4",
					Length = 5,
					HeadCell = new Vector2Int(3, 3),
					Color = SnakeColorType.Green.ToUnityColor(),
					ColorType = SnakeColorType.Green, // 蓝色类型
					BodySprite = SnakeBodySprite,
					MoveSpeed = 12f,
					IsControllable = true, // 暂时也设为可控制，方便测试
					BodyCells = new []
					{
						new Vector2Int(3, 3),
						new Vector2Int(3, 2),
						new Vector2Int(3, 1),
                        new Vector2Int(3, 0),
                        new Vector2Int(2, 0)
                    }
				}
			};
            var snakesCfg1 = new SnakeInitConfig[]
            {
				// 第一条蛇 - 红色玩家控制
				new SnakeInitConfig
                {
                    Id = "snake_1",
                    Name = "玩家蛇1",
                    Length = 5,
                    HeadCell = new Vector2Int(0, 3),
                    Color = SnakeColorType.Blue.ToUnityColor(),
                    ColorType = SnakeColorType.Blue,
                    BodySprite = SnakeBodySprite,
                    MoveSpeed = 16f,
                    IsControllable = true,
                    BodyCells = new []
                    {
                        new Vector2Int(0, 3),
                        new Vector2Int(0, 2),
                        new Vector2Int(0, 1),
                        new Vector2Int(0, 0),
                        new Vector2Int(1, 0)
                    }
                },
				
				// 第二条蛇 - 蓝色AI控制
				new SnakeInitConfig
                {
                    Id = "snake_2",
                    Name = "玩家蛇2",
                    Length = 3,
                    HeadCell = new Vector2Int(1, 3),
                    Color = SnakeColorType.Orange.ToUnityColor(),
                    ColorType = SnakeColorType.Orange, // 蓝色类型
					BodySprite = SnakeBodySprite,
                    MoveSpeed = 12f,
                    IsControllable = true, // 暂时也设为可控制，方便测试
					BodyCells = new []
                    {
                        new Vector2Int(1, 3),
                        new Vector2Int(1, 2),
                        new Vector2Int(1, 1)
                    }
                },
                new SnakeInitConfig
                {
                    Id = "snake_3",
                    Name = "玩家蛇3",
                    Length = 3,
                    HeadCell = new Vector2Int(2, 3),
                    Color = SnakeColorType.Purple.ToUnityColor(),
                    ColorType = SnakeColorType.Purple, // 蓝色类型
					BodySprite = SnakeBodySprite,
                    MoveSpeed = 12f,
                    IsControllable = true, // 暂时也设为可控制，方便测试
					BodyCells = new []
                    {
                        new Vector2Int(2, 3),
                        new Vector2Int(2, 2),
                        new Vector2Int(2, 1)
                    }
                },
                new SnakeInitConfig
                {
                    Id = "snake_4",
                    Name = "玩家蛇4",
                    Length = 5,
                    HeadCell = new Vector2Int(3, 3),
                    Color = SnakeColorType.Green.ToUnityColor(),
                    ColorType = SnakeColorType.Green, // 蓝色类型
					BodySprite = SnakeBodySprite,
                    MoveSpeed = 12f,
                    IsControllable = true, // 暂时也设为可控制，方便测试
					BodyCells = new []
                    {
                        new Vector2Int(3, 3),
                        new Vector2Int(3, 2),
                        new Vector2Int(3, 1),
                        new Vector2Int(3, 0),
                        new Vector2Int(2, 0)
                    }
                }
            };

            var snakesCfg2 = new SnakeInitConfig[]
            {
				// 第一条蛇 - 红色玩家控制
				new SnakeInitConfig
                {
                    Id = "snake_1",
                    Name = "玩家蛇1",
                    Length = 3,
                    HeadCell = new Vector2Int(0, 0),
                    Color = SnakeColorType.Green.ToUnityColor(),
                    ColorType = SnakeColorType.Green,
                    BodySprite = SnakeBodySprite,
                    MoveSpeed = 16f,
                    IsControllable = true,
                    BodyCells = new []
                    {
                        new Vector2Int(0, 0),
                        new Vector2Int(0, 1),
                        new Vector2Int(0, 2)
                    }
                },
				
				// 第二条蛇 - 蓝色AI控制
				new SnakeInitConfig
                {
                    Id = "snake_2",
                    Name = "玩家蛇2",
                    Length = 3,
                    HeadCell = new Vector2Int(2, 0),
                    Color = SnakeColorType.Red.ToUnityColor(),
                    ColorType = SnakeColorType.Red, // 蓝色类型
					BodySprite = SnakeBodySprite,
                    MoveSpeed = 12f,
                    IsControllable = true, // 暂时也设为可控制，方便测试
					BodyCells = new []
                    {
                        new Vector2Int(2, 0),
                        new Vector2Int(2, 1),
                        new Vector2Int(2, 2)
                    }
                },
                new SnakeInitConfig
                {
                    Id = "snake_3",
                    Name = "玩家蛇3",
                    Length = 3,
                    HeadCell = new Vector2Int(3, 0),
                    Color = SnakeColorType.Yellow.ToUnityColor(),
                    ColorType = SnakeColorType.Yellow, // 蓝色类型
					BodySprite = SnakeBodySprite,
                    MoveSpeed = 12f,
                    IsControllable = true, // 暂时也设为可控制，方便测试
					BodyCells = new []
                    {
                        new Vector2Int(3, 0),
                        new Vector2Int(3, 1),
                        new Vector2Int(3, 2)
                    }
                },
                new SnakeInitConfig
                {
                    Id = "snake_4",
                    Name = "玩家蛇4",
                    Length = 3,
                    HeadCell = new Vector2Int(5, 0),
                    Color = SnakeColorType.Blue.ToUnityColor(),
                    ColorType = SnakeColorType.Blue, // 蓝色类型
					BodySprite = SnakeBodySprite,
                    MoveSpeed = 12f,
                    IsControllable = true, // 暂时也设为可控制，方便测试
					BodyCells = new []
                    {
                        new Vector2Int(5, 0),
                        new Vector2Int(5, 1),
                        new Vector2Int(5, 2)
                    }
                }
            };

            var snakesCfg3 = new SnakeInitConfig[]
            {
				// 第一条蛇 - 红色玩家控制
				new SnakeInitConfig
                {
                    Id = "snake_1",
                    Name = "玩家蛇1",
                    Length = 7,
                    HeadCell = new Vector2Int(4, 0),
                    Color = SnakeColorType.Blue.ToUnityColor(),
                    ColorType = SnakeColorType.Blue,
                    BodySprite = SnakeBodySprite,
                    MoveSpeed = 16f,
                    IsControllable = true,
                    BodyCells = new []
                    {
                        new Vector2Int(4, 0),
                        new Vector2Int(3, 0),
                        new Vector2Int(2, 0),
                        new Vector2Int(1, 0),
                        new Vector2Int(0, 0),
                        new Vector2Int(0, 1),
                        new Vector2Int(0, 2)
                    }
                },
				
				// 第二条蛇 - 蓝色AI控制
				new SnakeInitConfig
                {
                    Id = "snake_2",
                    Name = "玩家蛇2",
                    Length = 3,
                    HeadCell = new Vector2Int(1, 3),
                    Color = SnakeColorType.Yellow.ToUnityColor(),
                    ColorType = SnakeColorType.Yellow, // 蓝色类型
					BodySprite = SnakeBodySprite,
                    MoveSpeed = 12f,
                    IsControllable = true, // 暂时也设为可控制，方便测试
					BodyCells = new []
                    {
                        new Vector2Int(1, 3),
                        new Vector2Int(1, 2),
                        new Vector2Int(1, 1)
                    }
                },
                new SnakeInitConfig
                {
                    Id = "snake_3",
                    Name = "玩家蛇3",
                    Length = 3,
                    HeadCell = new Vector2Int(2, 1),
                    Color = SnakeColorType.Red.ToUnityColor(),
                    ColorType = SnakeColorType.Red, // 蓝色类型
					BodySprite = SnakeBodySprite,
                    MoveSpeed = 12f,
                    IsControllable = true, // 暂时也设为可控制，方便测试
					BodyCells = new []
                    {
                        new Vector2Int(2, 1),
                        new Vector2Int(2, 2),
                        new Vector2Int(2, 3)
                    }
                },
                new SnakeInitConfig
                {
                    Id = "snake_4",
                    Name = "玩家蛇4",
                    Length = 3,
                    HeadCell = new Vector2Int(3,3),
                    Color = SnakeColorType.Green.ToUnityColor(),
                    ColorType = SnakeColorType.Green, // 蓝色类型
					BodySprite = SnakeBodySprite,
                    MoveSpeed = 12f,
                    IsControllable = true, // 暂时也设为可控制，方便测试
					BodyCells = new []
                    {
                        new Vector2Int(3, 3),
                        new Vector2Int(3, 2),
                        new Vector2Int(3, 1)
                    }
                },
                new SnakeInitConfig
                {
                    Id = "snake_5",
                    Name = "玩家蛇5",
                    Length = 3,
                    HeadCell = new Vector2Int(4, 1),
                    Color = SnakeColorType.Orange.ToUnityColor(),
                    ColorType = SnakeColorType.Orange, // 蓝色类型
					BodySprite = SnakeBodySprite,
                    MoveSpeed = 12f,
                    IsControllable = true, // 暂时也设为可控制，方便测试
					BodyCells = new []
                    {
                        new Vector2Int(4, 1),
                        new Vector2Int(4, 2),
                        new Vector2Int(4, 3)
                    }
                },
            };
            //////////////////////////////////////////////////SnakeInitConfig end////////////////////////////////////////////////////////
            ///
            //////////////////////////////////////////////////GridEntityConfig////////////////////////////////////////////////////////
            // 创建一些实体：墙体和不同颜色的洞
            var entitiesCfg = new[]
            {
				// 墙体
				new GridEntityConfig
                {
                    Type = GridEntityConfig.EntityType.Wall,
                    Cell = new Vector2Int(2, 5),
                    Sprite = WallSprite,
                    Color = Color.white
                },
                new GridEntityConfig
                {
                    Type = GridEntityConfig.EntityType.Wall,
                    Cell = new Vector2Int(3, 5),
                    Sprite = WallSprite,
                    Color = Color.white
                },
				
				// 红色洞 - 只有红色蛇可以进入
				new GridEntityConfig
                {
                    Type = GridEntityConfig.EntityType.Hole,
                    Cell = new Vector2Int(8, 2),
                    Sprite = HoleSprite,
                    Color = SnakeColorType.Red.ToUnityColor(), // 使用扩展方法获取颜色
					ColorType = SnakeColorType.Red
                },
				
				// 蓝色洞 - 只有蓝色蛇可以进入
				new GridEntityConfig
                {
                    Type = GridEntityConfig.EntityType.Hole,
                    Cell = new Vector2Int(8, 7),
                    Sprite = HoleSprite,
                    Color = SnakeColorType.Blue.ToUnityColor(), // 使用扩展方法获取颜色
					ColorType = SnakeColorType.Blue
                },
				
				// 绿色洞 - 没有对应颜色的蛇，所以对所有蛇都是阻挡物
				new GridEntityConfig
                {
                    Type = GridEntityConfig.EntityType.Hole,
                    Cell = new Vector2Int(1, 8),
                    Sprite = HoleSprite,
                    Color = SnakeColorType.Green.ToUnityColor(),
                    ColorType = SnakeColorType.Green
                }
            };

            // 创建一些实体：墙体和不同颜色的洞
            var entitiesCfg1 = new[]
			{
				// 墙体
				// 红色洞 - 只有红色蛇可以进入
				new GridEntityConfig
				{
					Type = GridEntityConfig.EntityType.Hole,
					Cell = new Vector2Int(0, 5),
					Sprite = HoleSprite,
					Color = SnakeColorType.Blue.ToUnityColor(), // 使用扩展方法获取颜色
					ColorType = SnakeColorType.Blue
                },
				
				// 蓝色洞 - 只有蓝色蛇可以进入
				new GridEntityConfig
				{
					Type = GridEntityConfig.EntityType.Hole,
					Cell = new Vector2Int(1, 5),
					Sprite = HoleSprite,
					Color = SnakeColorType.Orange.ToUnityColor(), // 使用扩展方法获取颜色
					ColorType = SnakeColorType.Orange
				},

				// 蓝色洞 - 只有蓝色蛇可以进入
				new GridEntityConfig
                {
                    Type = GridEntityConfig.EntityType.Hole,
                    Cell = new Vector2Int(2, 5),
                    Sprite = HoleSprite,
                    Color = SnakeColorType.Purple.ToUnityColor(), // 使用扩展方法获取颜色
					ColorType = SnakeColorType.Purple
                },
				
				
				// 绿色洞 - 没有对应颜色的蛇，所以对所有蛇都是阻挡物
				new GridEntityConfig
				{
					Type = GridEntityConfig.EntityType.Hole,
					Cell = new Vector2Int(3, 5),
					Sprite = HoleSprite,
					Color = SnakeColorType.Green.ToUnityColor(),
					ColorType = SnakeColorType.Green
				}
                
			};

            // 创建一些实体：墙体和不同颜色的洞
            var entitiesCfg2 = new[]
            {
				// 墙体
				new GridEntityConfig
                {
                    Type = GridEntityConfig.EntityType.Wall,
                    Cell = new Vector2Int(0, 4),
                    Sprite = WallSprite,
                    Color = Color.white
                },
                new GridEntityConfig
                {
                    Type = GridEntityConfig.EntityType.Wall,
                    Cell = new Vector2Int(1, 0),
                    Sprite = WallSprite,
                    Color = Color.white
                },
                new GridEntityConfig
                {
                    Type = GridEntityConfig.EntityType.Wall,
                    Cell = new Vector2Int(1, 1),
                    Sprite = WallSprite,
                    Color = Color.white
                },
                new GridEntityConfig
                {
                    Type = GridEntityConfig.EntityType.Wall,
                    Cell = new Vector2Int(1, 2),
                    Sprite = WallSprite,
                    Color = Color.white
                },
                new GridEntityConfig
                {
                    Type = GridEntityConfig.EntityType.Wall,
                    Cell = new Vector2Int(4, 0),
                    Sprite = WallSprite,
                    Color = Color.white
                },
                new GridEntityConfig
                {
                    Type = GridEntityConfig.EntityType.Wall,
                    Cell = new Vector2Int(4, 1),
                    Sprite = WallSprite,
                    Color = Color.white
                },
                new GridEntityConfig
                {
                    Type = GridEntityConfig.EntityType.Wall,
                    Cell = new Vector2Int(4, 2),
                    Sprite = WallSprite,
                    Color = Color.white
                },

                new GridEntityConfig
                {
                    Type = GridEntityConfig.EntityType.Wall,
                    Cell = new Vector2Int(5, 4),
                    Sprite = WallSprite,
                    Color = Color.white
                },
				
				// 红色洞 - 只有红色蛇可以进入
				new GridEntityConfig
                {
                    Type = GridEntityConfig.EntityType.Hole,
                    Cell = new Vector2Int(1, 4),
                    Sprite = HoleSprite,
                    Color = SnakeColorType.Green.ToUnityColor(), // 使用扩展方法获取颜色
					ColorType = SnakeColorType.Green
                },
				
				// 蓝色洞 - 只有蓝色蛇可以进入
				new GridEntityConfig
                {
                    Type = GridEntityConfig.EntityType.Hole,
                    Cell = new Vector2Int(2, 4),
                    Sprite = HoleSprite,
                    Color = SnakeColorType.Yellow.ToUnityColor(), // 使用扩展方法获取颜色
					ColorType = SnakeColorType.Yellow
                },
				
				// 绿色洞 - 没有对应颜色的蛇，所以对所有蛇都是阻挡物
				new GridEntityConfig
                {
                    Type = GridEntityConfig.EntityType.Hole,
                    Cell = new Vector2Int(3, 4),
                    Sprite = HoleSprite,
                    Color = SnakeColorType.Red.ToUnityColor(),
                    ColorType = SnakeColorType.Red
                },
				
				// 绿色洞 - 没有对应颜色的蛇，所以对所有蛇都是阻挡物
				new GridEntityConfig
                {
                    Type = GridEntityConfig.EntityType.Hole,
                    Cell = new Vector2Int(4, 4),
                    Sprite = HoleSprite,
                    Color = SnakeColorType.Blue.ToUnityColor(),
                    ColorType = SnakeColorType.Blue
                }
            };
            // 创建一些实体：墙体和不同颜色的洞
            var entitiesCfg3 = new[]
            {
				// 墙体
				new GridEntityConfig
                {
                    Type = GridEntityConfig.EntityType.Wall,
                    Cell = new Vector2Int(0, 3),
                    Sprite = WallSprite,
                    Color = Color.white
                },
                new GridEntityConfig
                {
                    Type = GridEntityConfig.EntityType.Wall,
                    Cell = new Vector2Int(0, 5),
                    Sprite = WallSprite,
                    Color = Color.white
                },
				
				// 红色洞 - 只有红色蛇可以进入
				new GridEntityConfig
                {
                    Type = GridEntityConfig.EntityType.Hole,
                    Cell = new Vector2Int(0, 4),
                    Sprite = HoleSprite,
                    Color = SnakeColorType.Blue.ToUnityColor(), // 使用扩展方法获取颜色
					ColorType = SnakeColorType.Blue
                },
				
				// 蓝色洞 - 只有蓝色蛇可以进入
				new GridEntityConfig
                {
                    Type = GridEntityConfig.EntityType.Hole,
                    Cell = new Vector2Int(1, 5),
                    Sprite = HoleSprite,
                    Color = SnakeColorType.Green.ToUnityColor(), // 使用扩展方法获取颜色
					ColorType = SnakeColorType.Green
                },
				
				// 绿色洞 - 没有对应颜色的蛇，所以对所有蛇都是阻挡物
				new GridEntityConfig
                {
                    Type = GridEntityConfig.EntityType.Hole,
                    Cell = new Vector2Int(2, 5),
                    Sprite = HoleSprite,
                    Color = SnakeColorType.Yellow.ToUnityColor(),
                    ColorType = SnakeColorType.Yellow
                },
                new GridEntityConfig
                {
                    Type = GridEntityConfig.EntityType.Hole,
                    Cell = new Vector2Int(3, 5),
                    Sprite = HoleSprite,
                    Color = SnakeColorType.Orange.ToUnityColor(), // 使用扩展方法获取颜色
					ColorType = SnakeColorType.Orange
                },
                new GridEntityConfig
                {
                    Type = GridEntityConfig.EntityType.Hole,
                    Cell = new Vector2Int(4, 5),
                    Sprite = HoleSprite,
                    Color = SnakeColorType.Red.ToUnityColor(), // 使用扩展方法获取颜色
					ColorType = SnakeColorType.Red
                }
            };


            //////////////////////////////////////////////////GridEntityConfig end////////////////////////////////////////////////////////
            switch (lv)
            {
                case 1:
                    {
                        return new LevelConfig
                        {
                            LV = 1,
                            Grid = gridCfg1,
                            Snakes = snakesCfg1,
                            Entities = entitiesCfg1,
                            GameTimeLimit = 180f,
                            EnableTimeLimit = true
                        };
                    }
                    break;
                case 2:
                    {
                        return new LevelConfig
                        {
                            LV = 2,
                            Grid = gridCfg2,
                            Snakes = snakesCfg2,
                            Entities = entitiesCfg2,
                            GameTimeLimit = 90f,
                            EnableTimeLimit = true
                        };
                    }
                    break;
                case 3:
                    {
                        return new LevelConfig
                        {
                            LV = 3,
                            Grid = gridCfg3,
                            Snakes = snakesCfg3,
                            Entities = entitiesCfg3,
                            GameTimeLimit = 90f,
                            EnableTimeLimit = true
                        };
                    }
                    break;
				default:
                    {
                        return new LevelConfig
                        {
                            LV = 0,
                            Grid = gridCfg,
                            Snakes = snakesCfg,
                            Entities = entitiesCfg,
                            GameTimeLimit = 300f, // 5分钟游戏时间
                            EnableTimeLimit = true
                        };
                    }
					break;
            }

		}
	}
}