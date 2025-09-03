using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ReGecko.GameCore.Flow;
using UnityEngine.EventSystems;
using ReGecko.Framework.UI;

namespace ReGecko.Bootstrap
{
	public class LobbyBootstrap : MonoBehaviour
	{
		public Sprite StartButtonSprite;

		void Start()
		{
			BuildUI();
		}

		void BuildUI()
		{
			EnsureEventSystem();

			UIManager.Instance.Show("GameLobby", GameContext.PreloadedUIPrefab_Lobby);
		}

		void EnsureEventSystem()
		{
			if (EventSystem.current != null) return;
			var es = new GameObject("EventSystem");
			es.AddComponent<EventSystem>();
			es.AddComponent<StandaloneInputModule>();
		}

	}
}


