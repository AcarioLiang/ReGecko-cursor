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
		readonly Dictionary<Vector2Int, List<GridEntity>> _cellToEntities = new Dictionary<Vector2Int, List<GridEntity>>();
		private List<GridEntity> _HoleEntities = new List<GridEntity>();
        private List<GridEntity> _WallEntities = new List<GridEntity>();
        private List<GridEntity> _ItemEntities = new List<GridEntity>();


        public List<GridEntity> HoleEntities => _HoleEntities;
        public List<GridEntity> WallEntities => _WallEntities;
        public List<GridEntity> ItemEntities => _ItemEntities;

        readonly List<GridEntity> _entities = new List<GridEntity>();


        public void Init(GridConfig grid)
        {
            _grid = grid;
            ClearAllEntities();
        }


		public void Register(GridEntity entity)
		{
			if (!_cellToEntities.TryGetValue(entity.Cell, out var list))
			{
				list = new List<GridEntity>();
				_cellToEntities[entity.Cell] = list;
			}
			list.Add(entity);
            _RegisteredToList(entity);
            entity.OnRegistered(_grid);
		}

		public void Unregister(GridEntity entity)
		{
			if (_cellToEntities.TryGetValue(entity.Cell, out var list))
			{
				list.Remove(entity);
                _UnregisteredToList(entity);
                if (list.Count == 0) _cellToEntities.Remove(entity.Cell);
			}
			entity.OnUnregistered();
		}

		public List<GridEntity> GetAt(Vector2Int cell)
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

		private void _RegisteredToList(GridEntity entity)
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

            _entities.Add(entity);
        }
        private void _UnregisteredToList(GridEntity entity)
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
            _entities.Remove(entity);
        }

        public void ClearAllEntities()
        {
            foreach (var entity in _entities)
            {
                if (entity != null)
                {
                    Destroy(entity.gameObject);
                }
            }

            _entities.Clear();
            _ItemEntities.Clear();
            _WallEntities.Clear();
            _HoleEntities.Clear();
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


