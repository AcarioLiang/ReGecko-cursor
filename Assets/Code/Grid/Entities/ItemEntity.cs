using UnityEngine;

namespace ReGecko.Grid.Entities
{
	public class ItemEntity : BaseEntity
	{
		protected override void Awake()
		{
			base.Awake();
			Blocking = false;
		}
	}
}


