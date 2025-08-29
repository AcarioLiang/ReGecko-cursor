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

		/// <summary>
		/// 同步加载PNG图片
		/// </summary>
		/// <param name="path">相对于Resources文件夹的路径，不包含扩展名</param>
		/// <returns>加载的Sprite，如果失败返回null</returns>
		public static Sprite LoadPNG(string path)
		{
			if (string.IsNullOrEmpty(path)) return null;
			
			// 检查缓存
			if (_cache.TryGetValue(path, out var cached))
			{
				return cached as Sprite;
			}
			
			// 从Resources文件夹加载
			var sprite = UnityEngine.Resources.Load<Sprite>(path);
			if (sprite != null)
			{
				_cache[path] = sprite;
			}
			
			return sprite;
		}

		/// <summary>
		/// 异步加载PNG图片
		/// </summary>
		/// <param name="path">相对于Resources文件夹的路径，不包含扩展名</param>
		/// <param name="onLoaded">加载完成回调</param>
		/// <returns>协程</returns>
		public static IEnumerator LoadPNGAsync(string path, Action<Sprite> onLoaded)
		{
			if (string.IsNullOrEmpty(path))
			{
				onLoaded?.Invoke(null);
				yield break;
			}
			
			// 检查缓存
			if (_cache.TryGetValue(path, out var cached))
			{
				onLoaded?.Invoke(cached as Sprite);
				yield break;
			}
			
			// 异步加载
			UnityEngine.ResourceRequest req = UnityEngine.Resources.LoadAsync<Sprite>(path);
			yield return req;
			
			var sprite = req.asset as Sprite;
			if (sprite != null)
			{
				_cache[path] = sprite;
			}
			
			onLoaded?.Invoke(sprite);
		}

		/// <summary>
		/// 加载Texture2D（如果需要原始纹理数据）
		/// </summary>
		/// <param name="path">相对于Resources文件夹的路径，不包含扩展名</param>
		/// <returns>加载的Texture2D，如果失败返回null</returns>
		public static Texture2D LoadTexture(string path)
		{
			if (string.IsNullOrEmpty(path)) return null;
			
			// 检查缓存
			if (_cache.TryGetValue(path, out var cached))
			{
				return cached as Texture2D;
			}
			
			// 从Resources文件夹加载
			var texture = UnityEngine.Resources.Load<Texture2D>(path);
			if (texture != null)
			{
				_cache[path] = texture;
			}
			
			return texture;
		}

		/// <summary>
		/// 异步加载Texture2D
		/// </summary>
		/// <param name="path">相对于Resources文件夹的路径，不包含扩展名</param>
		/// <param name="onLoaded">加载完成回调</param>
		/// <returns>协程</returns>
		public static IEnumerator LoadTextureAsync(string path, Action<Texture2D> onLoaded)
		{
			if (string.IsNullOrEmpty(path))
			{
				onLoaded?.Invoke(null);
				yield break;
			}
			
			// 检查缓存
			if (_cache.TryGetValue(path, out var cached))
			{
				onLoaded?.Invoke(cached as Texture2D);
				yield break;
			}
			
			// 异步加载
			UnityEngine.ResourceRequest req = UnityEngine.Resources.LoadAsync<Texture2D>(path);
			yield return req;
			
			var texture = req.asset as Texture2D;
			if (texture != null)
			{
				_cache[path] = texture;
			}
			
			onLoaded?.Invoke(texture);
		}
	}
}


