using System.Collections;
using UnityEngine;
using ReGecko.SnakeSystem;
using ReGecko.Game;

namespace ReGecko.Grid.Entities
{
	public class HoleEntity : BaseEntity
	{
		[Header("洞配置")]
		public float ConsumeInterval = 0.1f; // 每段吞噬间隔
		
		[Header("颜色配置")]
		[Tooltip("洞的颜色类型，只有相同颜色的蛇才能进入")]
		public SnakeColorType ColorType = SnakeColorType.Red; // 洞的颜色类型

		protected override void Awake()
		{
			base.Awake();
			Blocking = true; // 默认为阻挡物
		}

		public bool IsAdjacent(Vector2Int other)
		{
			return Mathf.Abs(other.x - Cell.x) + Mathf.Abs(other.y - Cell.y) == 1;
		}

		/// <summary>
		/// 检查蛇是否可以与此洞交互（颜色匹配）
		/// </summary>
		public bool CanInteractWithSnake(BaseSnake snake)
		{
			if (snake == null) return false;
			return snake.ColorType == this.ColorType;
		}

		/// <summary>
		/// 尝试触发吞噬逻辑
		/// </summary>
		public void TryTriggerConsume(SnakeController snake, bool dragOnHead)
		{
			// 检查蛇是否邻近洞
			if (!IsAdjacent(dragOnHead ? snake.GetHeadCell() : snake.GetTailCell())) return;
			
			// 检查颜色是否匹配
			if (!CanInteractWithSnake(snake))
			{
				Debug.Log($"蛇 {snake.SnakeId} ({snake.ColorType.GetDisplayName()}) 无法与洞 ({ColorType.GetDisplayName()}) 交互：颜色不匹配");
				return;
			}
			
			Debug.Log($"蛇 {snake.SnakeId} ({snake.ColorType.GetDisplayName()}) 开始被洞 ({ColorType.GetDisplayName()}) 吞噬");
			
			// 颜色匹配，启动吞噬协程todo
			//snake.StartCoroutine(snake.CoConsume(this, dragOnHead));
		}

		/// <summary>
		/// 检查指定位置是否被此洞阻挡
		/// </summary>
		public bool IsBlockingCell(Vector2Int cell, BaseSnake snake)
		{
			if (cell != Cell) return false; // 不是洞的位置
			
			// 如果颜色匹配，洞不算阻挡物
			if (CanInteractWithSnake(snake))
			{
				return false;
			}
			
			// 颜色不匹配，洞算作阻挡物
			return true;
		}
	}
}
