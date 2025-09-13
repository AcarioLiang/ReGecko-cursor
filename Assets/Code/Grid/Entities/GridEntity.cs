namespace ReGecko.Grid.Entities
{
    public class GridEntity : BaseEntity
    {
        protected override void Awake()
        {
            base.Awake();
            Blocking = false;
        }
    }
}


