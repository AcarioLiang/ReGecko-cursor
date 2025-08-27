using UnityEngine;
using ReGecko.GridSystem;

namespace ReGecko.Grid.Entities
{
	[DisallowMultipleComponent]
	public abstract class GridEntity : MonoBehaviour
	{
		public Vector2Int Cell;
		public bool Blocking;
		public Sprite Sprite;

		SpriteRenderer _renderer;

		protected virtual void Awake()
		{
			_renderer = GetComponent<SpriteRenderer>();
			if (_renderer == null) _renderer = gameObject.AddComponent<SpriteRenderer>();
			if (Sprite != null) _renderer.sprite = Sprite;
		}

		public virtual void OnRegistered(GridConfig grid)
		{
			if (_renderer != null && Sprite != null) _renderer.sprite = Sprite;
			transform.position = grid.CellToWorld(Cell);
		}

		public virtual void OnUnregistered() { }
	}
}


