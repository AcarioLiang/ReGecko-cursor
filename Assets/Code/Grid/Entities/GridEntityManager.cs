using System.Collections.Generic;
using UnityEngine;
using ReGecko.GridSystem;
using ReGecko.Framework;

namespace ReGecko.Grid.Entities
{
	public class GridEntityManager : BaseManager
    {
        static GridEntityManager _instance;
        public static GridEntityManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("GridEntityManager");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<GridEntityManager>();
                }
                return _instance;
            }
        }

        private GridConfig _grid;
		readonly Dictionary<Vector2Int, List<BaseEntity>> _cellToEntities = new Dictionary<Vector2Int, List<BaseEntity>>();
		private List<BaseEntity> _HoleEntities = new List<BaseEntity>();
        private List<BaseEntity> _WallEntities = new List<BaseEntity>();
        private List<BaseEntity> _ItemEntities = new List<BaseEntity>();
        private List<BaseEntity> _GridEntities = new List<BaseEntity>();


        public List<BaseEntity> HoleEntities => _HoleEntities;
        public List<BaseEntity> WallEntities => _WallEntities;
        public List<BaseEntity> ItemEntities => _ItemEntities;
        public List<BaseEntity> GridEntities => _GridEntities;


        public override void Init()
        {
            base.Init();
        }

        public void SetGridConfig(GridConfig grid)
        {
            _grid = grid;
            ClearAllEntities();
        }


		public void Register(BaseEntity entity)
		{
			if (!_cellToEntities.TryGetValue(entity.Cell, out var list))
			{
				list = new List<BaseEntity>();
				_cellToEntities[entity.Cell] = list;
			}
			list.Add(entity);
            _RegisteredToList(entity);
            entity.OnRegistered(_grid);
		}

		public void Unregister(BaseEntity entity)
		{
			if (_cellToEntities.TryGetValue(entity.Cell, out var list))
			{
				list.Remove(entity);
                _UnregisteredToList(entity);
                if (list.Count == 0) _cellToEntities.Remove(entity.Cell);
			}
			entity.OnUnregistered();
		}

		public List<BaseEntity> GetAt(Vector2Int cell)
		{
			if (_cellToEntities.TryGetValue(cell, out var list)) return list;
			return null;
		}

		public bool IsBlocked(Vector2Int cell)
		{
			if (!_cellToEntities.TryGetValue(cell, out var list)) return false;
			for (int i = 0; i < list.Count; i++) if (list[i].Blocking) return true;
			return false;
		}

		private void _RegisteredToList(BaseEntity entity)
        {
            if (entity is HoleEntity)
            {
                _HoleEntities.Add(entity);
            }
            if (entity is WallEntity)
            {
                _WallEntities.Add(entity);
            }
            if (entity is ItemEntity)
            {
                _ItemEntities.Add(entity);
            }
            if (entity is ItemEntity)
            {
                _GridEntities.Add(entity);
            }

        }
        private void _UnregisteredToList(BaseEntity entity)
        {
            if (entity is HoleEntity)
            {
                _HoleEntities.Remove(entity);
            }
            if (entity is WallEntity)
            {
                _WallEntities.Remove(entity);
            }
            if (entity is ItemEntity)
            {
                _ItemEntities.Remove(entity);
            }
            if (entity is ItemEntity)
            {
                _GridEntities.Remove(entity);
            }
        }

        public void ClearAllEntities()
        {
            foreach (var entity in _GridEntities)
            {
                if (entity != null)
                {
                    Destroy(entity.gameObject);
                }
            }

            foreach (var entity in _HoleEntities)
            {
                if (entity != null)
                {
                    Destroy(entity.gameObject);
                }
            }

            foreach (var entity in _WallEntities)
            {
                if (entity != null)
                {
                    Destroy(entity.gameObject);
                }
            }

            foreach (var entity in _ItemEntities)
            {
                if (entity != null)
                {
                    Destroy(entity.gameObject);
                }
            }

            _GridEntities.Clear();
            _ItemEntities.Clear();
            _WallEntities.Clear();
            _HoleEntities.Clear();
        }

        public void EnqueueBigCellPath(Vector2Int from, Vector2Int to, LinkedList<Vector2Int> pathList, int maxPathCount = 10)
        {

        }

        public void DestroyInstance()
        {
            if (_instance != null)
            {
                DestroyImmediate(_instance.gameObject);
                _instance = null;
            }
        }
    }
}


