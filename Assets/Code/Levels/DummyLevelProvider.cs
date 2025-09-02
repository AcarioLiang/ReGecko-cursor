using UnityEngine;
using ReGecko.GridSystem;
using ReGecko.SnakeSystem;

namespace ReGecko.Levels
{
	// 临时关卡提供者：返回固定20x20配置及一条蛇
	public class DummyLevelProvider : MonoBehaviour
	{
		public Sprite GridCellSprite;
		public Sprite SnakeBodySprite;
		public Sprite WallSprite;
		public Sprite HoleSprite;

		public SnakeBodySpriteConfig SnakeBodyConfig;

		public LevelConfig GetLevel()
		{
			var grid = new GridConfig
			{
				Width = 6,
				Height = 10,
				CellSize = 1f
			};
			
			// 配置多条蛇
			var snakes = new SnakeInitConfig[]
			{
				// 第一条蛇 - 玩家控制
				new SnakeInitConfig
				{
					Id = "player_snake",
					Name = "玩家蛇",
					Length = 5,
					HeadCell = new Vector2Int(0, 0),
					Color = Color.green,
					BodySprite = SnakeBodySprite,
					MoveSpeed = 16f,
					IsControllable = true,
					BodyCells = new []
					{
						new Vector2Int(0, 0),
						new Vector2Int(0, 1),
						new Vector2Int(0, 2),
						new Vector2Int(0, 3),
						new Vector2Int(0, 4)
					}
				},
				
				// 第二条蛇 - AI控制（预留）
				new SnakeInitConfig
				{
					Id = "ai_snake",
					Name = "AI蛇",
					Length = 3,
					HeadCell = new Vector2Int(5, 5),
					Color = Color.red,
					BodySprite = SnakeBodySprite,
					MoveSpeed = 12f,
					IsControllable = true,
					EnableAI = false,
					BodyCells = new []
					{
						new Vector2Int(5, 5),
						new Vector2Int(5, 6),
						new Vector2Int(5, 7)
					}
				}
			};

			// 创建一些实体：墙体和洞
			var entities = new[]
			{
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
				new GridEntityConfig
				{
					Type = GridEntityConfig.EntityType.Wall,
					Cell = new Vector2Int(4, 5),
					Sprite = WallSprite,
					Color = Color.white
				},
				new GridEntityConfig
				{
					Type = GridEntityConfig.EntityType.Hole,
					Cell = new Vector2Int(Random.Range(2, 4), Random.Range(2, 8)),
					Sprite = HoleSprite,
					Color = Color.white
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


