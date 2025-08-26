using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using ReGecko.GameCore.Flow;
using ReGecko.GameCore.Player;
using ReGecko.Levels;

namespace ReGecko.Bootstrap
{
	public class LoadingBootstrap : MonoBehaviour
	{
		IEnumerator Start()
		{
			yield return null;
			if (GameContext.NextLoadIsPlayer)
			{
				// 加载玩家数据
				var data = PlayerService.Get();
				// 可做远端同步/校验；此处省略
				GameContext.NextLoadIsPlayer = false;
				SceneManager.LoadScene(GameScenes.Lobby);
			}
			else
			{
				// 加载关卡：根据玩家等级构建关卡配置（此处用Dummy）
				var provider = FindObjectOfType<ReGecko.Levels.DummyLevelProvider>();
				LevelConfig level;
				if (provider != null) level = provider.GetLevel(); else level = new LevelConfig();
				GameContext.CurrentLevelConfig = level;
				SceneManager.LoadScene(GameScenes.Game);
			}
		}
	}
}


