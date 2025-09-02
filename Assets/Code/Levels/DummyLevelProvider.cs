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

		public LevelConfig GetLevel()
		{
			var grid = new GridConfig
			{
				Width = 10,
				Height = 10,
				CellSize = 64f
			};

			// 创建多条蛇，配置不同的颜色类型
			var snakes = new SnakeInitConfig[]
			{
				// 第一条蛇 - 红色玩家控制
				new SnakeInitConfig
				{
					Id = "player_snake",
					Name = "玩家蛇",
					Length = 5,
					HeadCell = new Vector2Int(0, 0),
					Color = Color.green,
					ColorType = SnakeColorType.Green, // 红色类型
					BodySprite = SnakeBodySprite,
					MoveSpeed = 16f,
					IsControllable = true,
					EnableAI = false,
					BodyCells = new []
					{
						new Vector2Int(0, 0),
						new Vector2Int(0, 1),
						new Vector2Int(0, 2),
						new Vector2Int(0, 3),
						new Vector2Int(0, 4)
					}
				},
				
				// 第二条蛇 - 蓝色AI控制
				new SnakeInitConfig
				{
					Id = "ai_snake",
					Name = "AI蛇",
					Length = 3,
					HeadCell = new Vector2Int(5, 5),
					Color = Color.blue,
					ColorType = SnakeColorType.Blue, // 蓝色类型
					BodySprite = SnakeBodySprite,
					MoveSpeed = 12f,
					IsControllable = true, // 暂时也设为可控制，方便测试
					EnableAI = false,
					BodyCells = new []
					{
						new Vector2Int(5, 5),
						new Vector2Int(5, 6),
						new Vector2Int(5, 7)
					}
				}
			};

			// 创建一些实体：墙体和不同颜色的洞
			var entities = new[]
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

			return new LevelConfig
			{
				Grid = grid,
				Snakes = snakes,
				Entities = entities,
				GameTimeLimit = 300f, // 5分钟游戏时间
				EnableTimeLimit = true
			};
		}
	}
}