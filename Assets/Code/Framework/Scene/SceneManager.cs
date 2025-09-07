using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using ReGecko.GameCore.Flow;
using UnityEngine.UI;
using ReGecko.Framework.UI;

namespace ReGecko.Framework.Scene
{
    /// <summary>
    /// 场景管理器：实现异步场景加载和无缝切换
    /// </summary>
    public class SceneManager : MonoBehaviour
    {
        private static SceneManager _instance;
        public static SceneManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("SceneManager");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<SceneManager>();
                    _instance.Init();
                }
                return _instance;
            }
        }

        [Header("场景切换设置")]
        public float FadeInDuration = 0.5f;
        public float FadeOutDuration = 0.5f;
        public Color FadeColor = Color.black;

        private Canvas _fadeCanvas;
        private CanvasGroup _fadeCanvasGroup;
        private bool _isTransitioning = false;
        private string _currentSceneName;
        private string _targetSceneName;

        void Init()
        {
            _currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            SetupFadeCanvas();
        }

        void SetupFadeCanvas()
        {
            // 创建淡入淡出Canvas
            var fadeGO = new GameObject("FadeCanvas");
            fadeGO.transform.SetParent(transform, false);
            
            _fadeCanvas = fadeGO.AddComponent<Canvas>();
            _fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _fadeCanvas.sortingOrder = 9999; // 确保在最上层
            
            var scaler = fadeGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            
            // 创建淡入淡出背景
            var bgGO = new GameObject("FadeBackground");
            bgGO.transform.SetParent(fadeGO.transform, false);
            
            var bgImage = bgGO.AddComponent<UnityEngine.UI.Image>();
            bgImage.color = FadeColor;
            
            var bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            
            _fadeCanvasGroup = fadeGO.AddComponent<CanvasGroup>();
            _fadeCanvasGroup.alpha = 0f; // 初始透明
            _fadeCanvasGroup.blocksRaycasts = false;
        }

        /// <summary>
        /// 异步加载场景（无淡入淡出效果）
        /// </summary>
        private void LoadSceneAsync(string sceneName, Action onComplete = null)
        {
            if (_isTransitioning) return;
            
            StartCoroutine(LoadSceneAsyncCoroutine(sceneName, false, onComplete));
        }

        /// <summary>
        /// 异步加载场景（带淡入淡出效果）
        /// </summary>
        private void LoadSceneAsyncWithFade(string sceneName, Action onComplete = null)
        {
            if (_isTransitioning) return;
            
            StartCoroutine(LoadSceneAsyncCoroutine(sceneName, true, onComplete));
        }

        /// <summary>
        /// 加载Loading场景
        /// </summary>
        public void LoadLoadingScene(bool withFade = false)
        {
            if(withFade)
            {
                LoadSceneAsyncWithFade(GameScenes.Loading, () => {
                    UIManager.Instance.Destroy("GameplayHUD");
                    UIManager.Instance.CloseAll();
                });
            }
            else
            {
                LoadSceneAsync(GameScenes.Loading, () => {
                    UIManager.Instance.Destroy("GameplayHUD");
                    UIManager.Instance.CloseAll();
                });
            }
        }

        /// <summary>
        /// 加载Lobby场景
        /// </summary>
        public void LoadLobbyScene(bool withFade = false)
        {
            if(withFade)
                LoadSceneAsyncWithFade(GameScenes.Lobby, null);
            else
                LoadSceneAsync(GameScenes.Lobby, null);
        }

        /// <summary>
        /// 加载Game场景
        /// </summary>
        public void LoadGameScene(bool withFade = false)
        {
            if (withFade)
                LoadSceneAsyncWithFade(GameScenes.Game, null);
            else
                LoadSceneAsync(GameScenes.Game, null);
        }

        /// <summary>
        /// 异步场景加载协程
        /// </summary>
        private IEnumerator LoadSceneAsyncCoroutine(string sceneName, bool useFade, Action onComplete)
        {
            if (_isTransitioning) yield break;
            
            _isTransitioning = true;
            _targetSceneName = sceneName;

            // 1. 淡出当前场景
            if (useFade)
            {
                yield return StartCoroutine(FadeOut());
            }

            // 2. 异步加载目标场景
            var asyncOperation = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName);
            asyncOperation.allowSceneActivation = false; // 不立即激活场景

            // 等待场景加载到90%
            while (asyncOperation.progress < 0.9f)
            {
                yield return null;
            }

            // 3. 激活新场景
            asyncOperation.allowSceneActivation = true;
            yield return asyncOperation;

            // 4. 更新当前场景名称
            _currentSceneName = sceneName;

            // 5. 淡入新场景
            if (useFade)
            {
                yield return StartCoroutine(FadeIn());
            }

            // 6. 完成回调
            onComplete?.Invoke();

            _isTransitioning = false;

        }

        /// <summary>
        /// 淡出效果
        /// </summary>
        private IEnumerator FadeOut()
        {
            _fadeCanvasGroup.blocksRaycasts = true;
            float elapsed = 0f;
            
            while (elapsed < FadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(0f, 1f, elapsed / FadeOutDuration);
                _fadeCanvasGroup.alpha = alpha;
                yield return null;
            }
            
            _fadeCanvasGroup.alpha = 1f;
        }

        /// <summary>
        /// 淡入效果
        /// </summary>
        private IEnumerator FadeIn()
        {
            float elapsed = 0f;
            
            while (elapsed < FadeInDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsed / FadeInDuration);
                _fadeCanvasGroup.alpha = alpha;
                yield return null;
            }
            
            _fadeCanvasGroup.alpha = 0f;
            _fadeCanvasGroup.blocksRaycasts = false;
        }

        /// <summary>
        /// 检查是否正在切换场景
        /// </summary>
        public bool IsTransitioning => _isTransitioning;

        /// <summary>
        /// 获取当前场景名称
        /// </summary>
        public string CurrentSceneName => _currentSceneName;

        /// <summary>
        /// 获取目标场景名称
        /// </summary>
        public string TargetSceneName => _targetSceneName;

        /// <summary>
        /// 预加载场景（在后台加载，不切换）
        /// </summary>
        public void PreloadScene(string sceneName)
        {
            StartCoroutine(PreloadSceneCoroutine(sceneName));
        }

        private IEnumerator PreloadSceneCoroutine(string sceneName)
        {
            var asyncOperation = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName);
            asyncOperation.allowSceneActivation = false;
            
            // 加载到90%后停止
            while (asyncOperation.progress < 0.9f)
            {
                yield return null;
            }
            
            // 预加载完成，但不激活场景
            Debug.Log($"Scene {sceneName} preloaded successfully");
        }

        /// <summary>
        /// 卸载场景
        /// </summary>
        public void UnloadScene(string sceneName)
        {
            StartCoroutine(UnloadSceneCoroutine(sceneName));
        }

        private IEnumerator UnloadSceneCoroutine(string sceneName)
        {
            var asyncOperation = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(sceneName);
            yield return asyncOperation;
        }

        void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
