using UnityEngine;
using ReGecko.Levels;
using ReGecko.GameCore.Flow;
using ReGecko.Grid.Entities;
using ReGecko.Framework.UI;

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

            // 初始化实体管理器
            var entityManagerGo = new GameObject("EntityManager");
            _entityManager = entityManagerGo.AddComponent<GridEntityManager>();
            _entityManager.Grid = level.Grid;

            // 显示预加载的UI并初始化游戏渲染
            if (GameContext.PreloadedUIPrefab != null)
            {
                var hudInstance = UIManager.Instance.Show("GameplayHUD", GameContext.PreloadedUIPrefab);

                // 查找并初始化UI游戏管理器
                var uiGameManager = hudInstance.GetComponentInChildren<UIGameManager>();
                if (uiGameManager != null)
                {
                    uiGameManager.Initialize(level);
                    uiGameManager.SetCellSprite(LevelProvider.GridCellSprite);
                    uiGameManager.BuildGame();
                }
            }
        }
    }
}


