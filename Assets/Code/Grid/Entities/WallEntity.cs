namespace ReGecko.Grid.Entities
{
	public class WallEntity : GridEntity
	{
		protected override void Awake()
		{
			base.Awake();
			Blocking = true;
		}
	}
}


