using UnityEngine;
using ReGecko.GridSystem;

namespace ReGecko.Levels
{
	// 临时关卡提供者：返回固定20x20配置及一条蛇
	public class DummyLevelProvider : MonoBehaviour
	{
		public Sprite GridCellSprite;
		public Sprite SnakeBodySprite;

		public LevelConfig GetLevel()
		{
			var grid = new GridConfig
			{
				Width = 20,
				Height = 20,
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
			return new LevelConfig
			{
				Grid = grid,
				Snakes = new[] { snake }
			};
		}
	}
}


