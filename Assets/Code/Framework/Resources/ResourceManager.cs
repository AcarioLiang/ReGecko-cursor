using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ReGecko.Framework.Resources
{
	public static class ResourceManager
	{
		static readonly Dictionary<string, UnityEngine.Object> _cache = new Dictionary<string, UnityEngine.Object>();

		public static T GetCached<T>(string path) where T : UnityEngine.Object
		{
			if (string.IsNullOrEmpty(path)) return null;
			if (_cache.TryGetValue(path, out var obj)) return obj as T;
			return null;
		}

		public static IEnumerator LoadPrefabAsync(string path, Action<GameObject> onLoaded)
		{
			if (string.IsNullOrEmpty(path))
			{
				onLoaded?.Invoke(null);
				yield break;
			}
			if (_cache.TryGetValue(path, out var cached))
			{
				onLoaded?.Invoke(cached as GameObject);
				yield break;
			}
			UnityEngine.ResourceRequest req = UnityEngine.Resources.LoadAsync<GameObject>(path);
			yield return req;
			var prefab = req.asset as GameObject;
			if (prefab != null) _cache[path] = prefab;
			onLoaded?.Invoke(prefab);
		}
	}
}


