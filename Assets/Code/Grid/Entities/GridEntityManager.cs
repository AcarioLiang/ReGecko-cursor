using System.Collections.Generic;
using UnityEngine;
using ReGecko.GridSystem;

namespace ReGecko.Grid.Entities
{
	public class GridEntityManager : MonoBehaviour
	{
		public GridConfig Grid;
		readonly Dictionary<Vector2Int, List<GridEntity>> _cellToEntities = new Dictionary<Vector2Int, List<GridEntity>>();
		private List<GridEntity> _HoleEntities = new List<GridEntity>();
        private List<GridEntity> _WallEntities = new List<GridEntity>();
        private List<GridEntity> _ItemEntities = new List<GridEntity>();


        public List<GridEntity> HoleEntities => _HoleEntities;
        public List<GridEntity> WallEntities => _WallEntities;
        public List<GridEntity> ItemEntities => _ItemEntities;

        private void Start()
        {
			ClearAllEntities();
		}

		public void ClearAllEntities()
        {
            _HoleEntities.Clear();
			_WallEntities.Clear();
			_ItemEntities.Clear();
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
            entity.OnRegistered(Grid);
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
        }
    }
}


