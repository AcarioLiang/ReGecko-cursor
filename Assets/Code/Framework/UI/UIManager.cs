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

            GameObject inst;

            if (_spawned.TryGetValue(key, out var go) && go != null)
            {
                // UI已存在，直接激活
                inst = go;
                inst.SetActive(true);
            }
            else
            {
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
            }

            // 确保UI显示在最前面：移动到Canvas的最后一个子对象位置
            inst.transform.SetAsLastSibling();

            return inst;
        }

        public GameObject FindUI(string key)
        {
            if (_spawned.TryGetValue(key, out var go) && go != null)
            {
                return go;
            }
            return null;
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

        public void CloseAll()
        {
            foreach(var _go in _spawned)
            {
                if(_go.Value != null)
                    _go.Value.SetActive(false);
            }

        }
    }
}


