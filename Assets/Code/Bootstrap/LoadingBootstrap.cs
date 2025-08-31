using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using ReGecko.GameCore.Flow;
using ReGecko.GameCore.Player;
using ReGecko.Levels;
using ReGecko.Framework.Resources;
using ReGecko.Framework.UI;
using ReGecko.SnakeSystem;

namespace ReGecko.Bootstrap
{
    public class LoadingBootstrap : MonoBehaviour
    {
        public Sprite BackgroundSprite;
        public Sprite LogoSprite;
        public Sprite BtnSprite;
        public Sprite ProgressBackgroundSprite;
        public Sprite ProgressFillSprite;
        public float FakeDuration = 3f;

        Slider _progress;
        GameObject _progressBar;
        GameObject _startButton;
        public string UIPrefabPath = "";//"UI/GameplayHUD"; // Resources 下路径

        IEnumerator Start()
        {
            BuildUI();

            // 加载玩家数据
            if (GameContext.NextLoadIsPlayer)
            {
                var data = PlayerService.Get();
            }
            else
            {
                var provider = FindObjectOfType<ReGecko.Levels.DummyLevelProvider>();
                LevelConfig level;
                SnakeBodySpriteConfig bodysprite;
                if (provider != null)
                {
                    level = provider.GetLevel();
                    bodysprite = provider.SnakeBodyConfig;
                }
                else
                {
                    level = new LevelConfig();
                    bodysprite = new SnakeBodySpriteConfig();
                }

                GameContext.CurrentLevelConfig = level;
                GameContext.SnakeBodyConfig = bodysprite;
            }

            // 并行加载资源
            GameObject loadedUIPrefab = null;
            bool uiLoaded = false;
            bool otherResourcesLoaded = false;

            // 启动UI加载
            if (!string.IsNullOrEmpty(UIPrefabPath))
            {
                StartCoroutine(ResourceManager.LoadPrefabAsync(UIPrefabPath, prefab =>
                {
                    loadedUIPrefab = prefab;
                    uiLoaded = true;
                }));
            }
            else
            {
                // 用代码生成的是实例，不需要再Instantiate
                loadedUIPrefab = ReGecko.Framework.UI.GameplayHUDBuilder.BuildPrefabTemplate();
                loadedUIPrefab.SetActive(false); // 先隐藏，等到Game场景再显示
                uiLoaded = true;
            }

            // 启动其他资源加载（预留）
            StartCoroutine(LoadOtherResources(() => otherResourcesLoaded = true));

            // 运行进度条，等待所有资源加载完成
            yield return StartCoroutine(RunFakeProgress(() => uiLoaded && otherResourcesLoaded));

            // 将加载的UI保存到GameContext，并确保不会被场景切换销毁
            if (loadedUIPrefab != null)
            {
                DontDestroyOnLoad(loadedUIPrefab);
            }
            GameContext.PreloadedUIPrefab = loadedUIPrefab;

            // 不再自动切换场景，等待用户点击开始按钮
        }

        void BuildUI()
        {
            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            canvasGo.AddComponent<GraphicRaycaster>();

            // 确保有EventSystem来处理UI事件
            if (FindObjectOfType<EventSystem>() == null)
            {
                var eventSystemGo = new GameObject("EventSystem");
                eventSystemGo.AddComponent<EventSystem>();
                eventSystemGo.AddComponent<StandaloneInputModule>();
            }

            // 背景图（自适应）
            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.sprite = BackgroundSprite;
            bgImg.preserveAspect = true;
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            // Logo图片 (926*651，位置160*160)
            var logoGo = new GameObject("Logo");
            logoGo.transform.SetParent(canvasGo.transform, false);
            var logoImg = logoGo.AddComponent<Image>();
            logoImg.sprite = LogoSprite;
            logoImg.preserveAspect = true;
            var logoRt = logoGo.GetComponent<RectTransform>();
            logoRt.anchorMin = new Vector2(0.5f, 1f);
            logoRt.anchorMax = new Vector2(0.5f, 1f);
            logoRt.pivot = new Vector2(0.5f, 0.5f);
            logoRt.sizeDelta = new Vector2(926, 651);
            logoRt.anchoredPosition = new Vector2(0, -450);

            // 进度条容器（位置调整到高480）
            var barGo = new GameObject("ProgressBar");
            barGo.transform.SetParent(canvasGo.transform, false);
            var barBg = barGo.AddComponent<Image>();
            barBg.sprite = ProgressBackgroundSprite;
            barBg.type = Image.Type.Tiled;
            var barRt = barGo.GetComponent<RectTransform>();
            barRt.anchorMin = new Vector2(0.5f, 0f);
            barRt.anchorMax = new Vector2(0.5f, 0f);
            barRt.pivot = new Vector2(0.5f, 0.5f);
            barRt.sizeDelta = new Vector2(750, 90);
            barRt.anchoredPosition = new Vector2(0, 480);

            // Slider
            var slider = barGo.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            slider.transition = Selectable.Transition.None;

            // Fill Area
            var fillArea = new GameObject("FillArea");
            fillArea.transform.SetParent(barGo.transform, false);
            var fillAreaRt = fillArea.AddComponent<RectTransform>();
            fillAreaRt.anchorMin = new Vector2(0f, 1f);
            fillAreaRt.anchorMax = new Vector2(0f, 1f);
            fillAreaRt.sizeDelta = new Vector2(701f, 49f);
            fillAreaRt.anchoredPosition = new Vector2(373f, -35f);

            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fillImg = fill.AddComponent<Image>();
            fillImg.sprite = ProgressFillSprite;
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            var fillRt = fill.GetComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(1f, 1f);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;

            slider.fillRect = fillRt;
            slider.targetGraphic = fillImg;
            _progress = slider;
            _progressBar = barGo;

            // 开始按钮（位置高480，居中对齐，初始隐藏）
            var buttonGo = new GameObject("StartButton");
            buttonGo.transform.SetParent(canvasGo.transform, false);
            var buttonImg = buttonGo.AddComponent<Image>();
            buttonImg.sprite = BtnSprite;
            buttonImg.raycastTarget = true; // 确保可以接收点击事件
            var button = buttonGo.AddComponent<Button>();
            var buttonRt = buttonGo.GetComponent<RectTransform>();
            buttonRt.anchorMin = new Vector2(0.5f, 0f);
            buttonRt.anchorMax = new Vector2(0.5f, 0f);
            buttonRt.pivot = new Vector2(0.5f, 0.5f);
            buttonRt.sizeDelta = new Vector2(632, 234);
            buttonRt.anchoredPosition = new Vector2(0, 480);

            // 按钮文字
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(buttonGo.transform, false);
            var text = textGo.AddComponent<Text>();
            text.text = "开始游戏";
            text.fontSize = 36;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.raycastTarget = false; // 文字不阻挡点击事件
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            // 设置Button的targetGraphic
            button.targetGraphic = buttonImg;
            
            // 添加按钮点击事件
            button.onClick.AddListener(() =>
            {
                ReGecko.Framework.Scene.SceneManager.Instance.LoadLobbyScene();
            });

            _startButton = buttonGo;
            _startButton.SetActive(false); // 初始隐藏
        }

        IEnumerator LoadOtherResources(System.Action onComplete)
        {
            // 预留其他资源加载时间（例如音效、特效等）
            yield return new WaitForSeconds(0.5f);
            onComplete?.Invoke();
        }

        IEnumerator RunFakeProgress(System.Func<bool> extraDone = null)
        {
            float t = 0f;
            while (t < FakeDuration || (extraDone != null && !extraDone()))
            {
                t += Time.deltaTime;
                // 进度条结合时间和资源加载状态
                float timeProgress = Mathf.Clamp01(t / FakeDuration);
                float resourceProgress = (extraDone != null && extraDone()) ? 1f : 0f;
                float finalProgress = Mathf.Max(timeProgress, resourceProgress);

                if (_progress != null) _progress.value = finalProgress;
                yield return null;
            }

            // 进度达到100%，隐藏进度条，显示开始按钮
            if (_progress != null) _progress.value = 1f;

            // 等待一帧确保进度条更新完成
            yield return null;

            if (GameContext.NextLoadIsPlayer)
            {
                // 进度达到100%时，总是隐藏进度条，显示开始按钮
                if (_progressBar != null) _progressBar.SetActive(false);
                if (_startButton != null) _startButton.SetActive(true);
            }
            else
            {
                ReGecko.Framework.Scene.SceneManager.Instance.LoadGameScene();
            }

                
        }
    }
}


