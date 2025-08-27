using UnityEngine;
using ReGecko.GridSystem;

namespace ReGecko.Levels
{
	// 临时关卡提供者：返回固定20x20配置及一条蛇
	public class DummyLevelProvider : MonoBehaviour
	{
		public Sprite GridCellSprite;
		public Sprite SnakeBodySprite;
		public Sprite WallSprite;
		public Sprite HoleSprite;

		public LevelConfig GetLevel()
		{
			var grid = new GridConfig
			{
				Width = 10,
				Height = 10,
				CellSize = 1f
			};
			var snake = new SnakeInitConfig
			{
				Length = 5,
				HeadCell = new Vector2Int(0, 0),
				Color = Color.green,
				BodySprite = SnakeBodySprite,
				BodyCells = new []
				{
					new Vector2Int(0, 0),
					new Vector2Int(0, 1),
					new Vector2Int(0, 2),
					new Vector2Int(0, 3),
					new Vector2Int(0, 4)
				}
			};

			// 创建一些实体：墙体和洞
			var entities = new[]
			{
				new GridEntityConfig
				{
					Type = GridEntityConfig.EntityType.Wall,
					Cell = new Vector2Int(5, 5),
					Sprite = WallSprite,
					Color = Color.gray
				},
				new GridEntityConfig
				{
					Type = GridEntityConfig.EntityType.Wall,
					Cell = new Vector2Int(6, 5),
					Sprite = WallSprite,
					Color = Color.gray
				},
				new GridEntityConfig
				{
					Type = GridEntityConfig.EntityType.Wall,
					Cell = new Vector2Int(7, 5),
					Sprite = WallSprite,
					Color = Color.gray
				},
				new GridEntityConfig
				{
					Type = GridEntityConfig.EntityType.Hole,
					Cell = new Vector2Int(Random.Range(2, 8), Random.Range(2, 8)),
					Sprite = HoleSprite,
					Color = Color.black
				}
			};

			return new LevelConfig
			{
				Grid = grid,
				Snakes = new[] { snake },
				Entities = entities
			};
		}
	}
}


