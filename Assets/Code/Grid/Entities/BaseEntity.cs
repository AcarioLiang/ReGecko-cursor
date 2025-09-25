using UnityEngine;
using ReGecko.GridSystem;
using UnityEngine.UI;

namespace ReGecko.Grid.Entities
{
	[DisallowMultipleComponent]
	public abstract class BaseEntity : MonoBehaviour
	{
		public GameObject GameObj;
		public Vector2Int Cell;
		public bool Blocking;
		Sprite Sprite;
        Color SpriteColor;

        protected Image _renderer;

		protected virtual void Awake()
		{
			_renderer = GetComponent<Image>();
			if (_renderer == null) _renderer = gameObject.AddComponent<Image>();
		}

        protected virtual void Update()
        {

        }

		public void SetSprite(Sprite s, Color c)
        {
			Sprite = s;
			SpriteColor = c;
			if (_renderer != null)
            {
				_renderer.sprite = Sprite;
                _renderer.color = Color.white;
            }
        }

        public virtual void OnRegistered(GridConfig grid)
		{
			if (_renderer != null && Sprite != null) _renderer.sprite = Sprite;
		}

		public virtual void OnUnregistered() { }

        public virtual void OnTirggerStart() { }
        public virtual void OnTirggered() { }

    }
}


