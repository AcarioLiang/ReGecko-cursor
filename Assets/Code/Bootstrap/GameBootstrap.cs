using UnityEngine;
using ReGecko.Levels;
using ReGecko.GameCore.Flow;
using ReGecko.Grid.Entities;
using ReGecko.Framework.UI;
using UnityEngine.EventSystems;

namespace ReGecko.Bootstrap
{
    public class GameBootstrap : MonoBehaviour
    {
        public DummyLevelProvider LevelProvider;


        void Awake()
        {
            if (LevelProvider == null) LevelProvider = FindObjectOfType<DummyLevelProvider>();
        }

        void Start()
        {
            var level = GameContext.CurrentLevelConfig != null ? GameContext.CurrentLevelConfig : LevelProvider != null ? LevelProvider.GetLevel() : new LevelConfig();

            // 确保有EventSystem来处理UI事件
            if (FindObjectOfType<EventSystem>() == null)
            {
                var eventSystemGo = new GameObject("EventSystem");
                eventSystemGo.AddComponent<EventSystem>();
                eventSystemGo.AddComponent<StandaloneInputModule>();
            }

            // 显示预加载的UI并初始化游戏渲染
            if (GameContext.PreloadedUIPrefab_GameMain != null)
            {
                var hudInstance = UIManager.Instance.Show("GameMain", GameContext.PreloadedUIPrefab_GameMain);

                // 查找并初始化UI游戏管理器
                UIManager.Instance.GameManager = hudInstance.GetComponentInChildren<UIGameManager>();
                if (UIManager.Instance.GameManager != null)
                {
                    UIManager.Instance.GameManager.Initialize(level);
                    UIManager.Instance.GameManager.BuildGame();
                }
            }
        }
    }
}


