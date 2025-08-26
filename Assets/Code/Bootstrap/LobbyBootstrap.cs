using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ReGecko.GameCore.Flow;
using UnityEngine.EventSystems;

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
			var canvasGo = new GameObject("Canvas");
			var canvas = canvasGo.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvasGo.AddComponent<CanvasScaler>();
			canvasGo.AddComponent<GraphicRaycaster>();

			var btnGo = new GameObject("StartButton");
			btnGo.transform.SetParent(canvasGo.transform, false);
			var img = btnGo.AddComponent<Image>();
			img.sprite = StartButtonSprite;
			var btn = btnGo.AddComponent<Button>();
			btn.onClick.AddListener(OnClickStart);

			// 文字：开始游戏
			var textGo = new GameObject("Text");
			textGo.transform.SetParent(btnGo.transform, false);
			var text = textGo.AddComponent<Text>();
			text.text = "开始游戏";
			text.alignment = TextAnchor.MiddleCenter;
			text.color = Color.black;
			text.fontSize = 32;
			text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			var textRt = textGo.GetComponent<RectTransform>();
			textRt.anchorMin = Vector2.zero;
			textRt.anchorMax = Vector2.one;
			textRt.offsetMin = Vector2.zero;
			textRt.offsetMax = Vector2.zero;

			var rt = btnGo.GetComponent<RectTransform>();
			rt.sizeDelta = new Vector2(200, 80);
			rt.anchorMin = new Vector2(0.5f, 0.5f);
			rt.anchorMax = new Vector2(0.5f, 0.5f);
			rt.pivot = new Vector2(0.5f, 0.5f);
			rt.anchoredPosition = Vector2.zero;
		}

		void EnsureEventSystem()
		{
			if (EventSystem.current != null) return;
			var es = new GameObject("EventSystem");
			es.AddComponent<EventSystem>();
			es.AddComponent<StandaloneInputModule>();
		}

		void OnClickStart()
		{
			GameContext.NextLoadIsPlayer = false; // 进入关卡加载
			SceneManager.LoadScene(GameScenes.Loading);
		}
	}
}


