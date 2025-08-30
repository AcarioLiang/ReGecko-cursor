using System.Collections;
using UnityEngine;
using ReGecko.SnakeSystem;

namespace ReGecko.Grid.Entities
{
	public class HoleEntity : GridEntity
	{
		public float ConsumeInterval = 0.05f; // 每段吞噬间隔

		protected override void Awake()
		{
			base.Awake();
			Blocking = true;
		}

		public bool IsAdjacent(Vector2Int other)
		{
			return Mathf.Abs(other.x - Cell.x) + Mathf.Abs(other.y - Cell.y) == 1;
		}

		public void TryTriggerConsume(SnakeController snake, bool dragOnHead)
		{
			if (!IsAdjacent(dragOnHead ? snake.GetHeadCell() : snake.GetTailCell())) return;
			// 直接启动吞噬协程
			snake.StartCoroutine(snake.CoConsume(this, dragOnHead));
		}
	}
}


