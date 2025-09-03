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

        GridEntityManager _entityManager;

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

            // 初始化实体管理器
            if (FindObjectOfType<GridEntityManager>() == null)
            {
                var entityManagerGo = new GameObject("EntityManager");
                _entityManager = entityManagerGo.AddComponent<GridEntityManager>();
                _entityManager.Grid = level.Grid;
            }
                

            // 显示预加载的UI并初始化游戏渲染
            if (GameContext.PreloadedUIPrefab_GameMain != null)
            {
                var hudInstance = UIManager.Instance.Show("GameplayHUD", GameContext.PreloadedUIPrefab_GameMain);

                // 查找并初始化UI游戏管理器
                var uiGameManager = hudInstance.GetComponentInChildren<UIGameManager>();
                if (uiGameManager != null)
                {
                    uiGameManager.Initialize(level);
                    uiGameManager.BuildGame();
                }
            }

            if (FindObjectOfType<GameStateUI>() == null)
            {
                var gameStateUIGo = new GameObject("GameStateUI");
                gameStateUIGo.AddComponent<GameStateUI>();
            }
        }
    }
}


