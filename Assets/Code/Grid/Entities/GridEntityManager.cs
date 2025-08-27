using System.Collections.Generic;
using UnityEngine;
using ReGecko.GridSystem;

namespace ReGecko.Grid.Entities
{
	public class GridEntityManager : MonoBehaviour
	{
		public GridConfig Grid;
		readonly Dictionary<Vector2Int, List<GridEntity>> _cellToEntities = new Dictionary<Vector2Int, List<GridEntity>>();

		public void Register(GridEntity entity)
		{
			if (!_cellToEntities.TryGetValue(entity.Cell, out var list))
			{
				list = new List<GridEntity>();
				_cellToEntities[entity.Cell] = list;
			}
			list.Add(entity);
			entity.OnRegistered(Grid);
		}

		public void Unregister(GridEntity entity)
		{
			if (_cellToEntities.TryGetValue(entity.Cell, out var list))
			{
				list.Remove(entity);
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
	}
}


