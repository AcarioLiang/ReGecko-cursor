namespace ReGecko.Grid.Entities
{
	public class WallEntity : BaseEntity
	{
		protected override void Awake()
		{
			base.Awake();
			Blocking = true;
		}
	}
}


