using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace ReGecko.Framework.UI
{
	public class UIManager : MonoBehaviour
	{
		static UIManager _instance;
		public static UIManager Instance
		{
			get
			{
				if (_instance == null)
				{
					var go = new GameObject("UIManager");
					DontDestroyOnLoad(go);
					_instance = go.AddComponent<UIManager>();
					_instance.Init();
				}
				return _instance;
			}
		}

		Canvas _canvas;
		GraphicRaycaster _raycaster;
		readonly Dictionary<string, GameObject> _spawned = new Dictionary<string, GameObject>();

	void Init()
	{
		_canvas = gameObject.AddComponent<Canvas>();
		_canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		
		var scaler = gameObject.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1080, 1920);
		scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
		scaler.matchWidthOrHeight = 0.5f;
		
		_raycaster = gameObject.AddComponent<GraphicRaycaster>();
	}

		public GameObject Show(string key, GameObject prefabOrInstance)
		{
			if (prefabOrInstance == null) return null;
			if (_spawned.TryGetValue(key, out var go) && go != null)
			{
				go.SetActive(true);
				return go;
			}
			
			GameObject inst;
			// 判断是否已经是实例（在场景中的GameObject）
			if (prefabOrInstance.scene.IsValid())
			{
				// 已经是实例，直接使用
				inst = prefabOrInstance;
				inst.transform.SetParent(_canvas.transform, false);
			}
			else
			{
				// 是prefab模板，需要实例化
				inst = Instantiate(prefabOrInstance, _canvas.transform, false);
			}
			
			inst.name = key;
			inst.SetActive(true);
			_spawned[key] = inst;
			return inst;
		}

		public void Hide(string key)
		{
			if (_spawned.TryGetValue(key, out var go) && go != null)
			{
				go.SetActive(false);
			}
		}

        public void Destroy(string key)
		{
			if (_spawned.TryGetValue(key, out var go) && go != null)
			{
				go.SetActive(false);
				DestroyImmediate(go);
			}
		}
    }
}


