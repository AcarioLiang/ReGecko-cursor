using UnityEngine;

namespace ReGecko.Grid.Entities
{
	public class ItemEntity : GridEntity
	{
		protected override void Awake()
		{
			base.Awake();
			Blocking = false;
		}
	}
}


