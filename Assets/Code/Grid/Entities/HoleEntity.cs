using System.Collections;
using UnityEngine;
using ReGecko.SnakeSystem;
using ReGecko.Game;
using ReGecko.GridSystem;
using DG.Tweening;
using UnityEngine.UI;

namespace ReGecko.Grid.Entities
{
	public class HoleEntity : BaseEntity
	{
		[Header("洞配置")]
		public float ConsumeInterval = 0.03f; // 每段吞噬间隔
		
		[Header("颜色配置")]
		[Tooltip("洞的颜色类型，只有相同颜色的蛇才能进入")]
		public SnakeColorType ColorType = SnakeColorType.Red; // 洞的颜色类型

		bool _isTriggering = false;

		protected override void Awake()
		{
			base.Awake();
			Blocking = true; // 默认为阻挡物
		}

        protected override void Update()
        {
            base.Update();
        }

        public override void OnRegistered(GridConfig grid)
        {
            base.OnRegistered(grid);

			if(Cell.x <= grid.Width / 2)
            {
				FlipHorizontal(false);
			}
            else
            {
                FlipHorizontal(true);
            }
        }

        // 水平翻转
        void FlipHorizontal(bool flip)
        {
            Vector3 scale = transform.localScale;
            scale.x = flip ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
            transform.localScale = scale;
        }
        void FlipVertical(bool flip)
        {
            Vector3 scale = transform.localScale;
            scale.y = flip ? -Mathf.Abs(scale.y) : Mathf.Abs(scale.y);
            transform.localScale = scale;
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

        public override void OnTirggerStart()
        {
            base.OnTirggerStart();

			_isTriggering = true;

			Vector3 locals = gameObject.transform.localScale;
			gameObject.transform.DOScale(locals * 1.1f, 0.2f);
		}
        public override void OnTirggered()
        {
            base.OnTirggered();
            _isTriggering = false;

            Vector3 locals = gameObject.transform.localScale;
            gameObject.transform.DOScale(locals * 0.9f, 0.2f);


            // 创建动画序列
            Sequence sequence = DOTween.Sequence();

			// 同时执行旋转和渐隐
			sequence.Join(transform.DORotate(new Vector3(0, 0, 1080f), 1f, RotateMode.FastBeyond360)); // 旋转3圈（360*3=1080）
			if(_renderer != null)
				sequence.Join(_renderer.DOFade(0f, 1f)); // 渐隐到完全透明
            sequence.Join(transform.DOScale(0f, 1.2f));
            

            // 设置缓动函数
            sequence.SetEase(Ease.InOutSine);

            // 动画完成后的回调
            sequence.OnComplete(() =>
            {
                GridEntityManager.Instance.Unregister(this);
                Destroy(this.gameObject);
            });

		}
    }
}
