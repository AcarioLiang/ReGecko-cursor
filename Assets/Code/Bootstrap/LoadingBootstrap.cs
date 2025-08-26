using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ReGecko.GameCore.Flow;
using ReGecko.GameCore.Player;
using ReGecko.Levels;

namespace ReGecko.Bootstrap
{
	public class LoadingBootstrap : MonoBehaviour
	{
		public Sprite BackgroundSprite;
		public Sprite ProgressBackgroundSprite;
		public Sprite ProgressFillSprite;
		public float FakeDuration = 3f;

		Slider _progress;

		IEnumerator Start()
		{
			BuildUI();
			yield return StartCoroutine(RunFakeProgress());
			// 完成后再执行原逻辑
			if (GameContext.NextLoadIsPlayer)
			{
				var data = PlayerService.Get();
				GameContext.NextLoadIsPlayer = false;
				SceneManager.LoadScene(GameScenes.Lobby);
			}
			else
			{
				var provider = FindObjectOfType<ReGecko.Levels.DummyLevelProvider>();
				LevelConfig level;
				if (provider != null) level = provider.GetLevel(); else level = new LevelConfig();
				GameContext.CurrentLevelConfig = level;
				SceneManager.LoadScene(GameScenes.Game);
			}
		}

		void BuildUI()
		{
			var canvasGo = new GameObject("Canvas");
			var canvas = canvasGo.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			var scaler = canvasGo.AddComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = new Vector2(1080, 1920);
			canvasGo.AddComponent<GraphicRaycaster>();

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

			// 进度条容器
			var barGo = new GameObject("ProgressBar");
			barGo.transform.SetParent(canvasGo.transform, false);
			var barBg = barGo.AddComponent<Image>();
			barBg.sprite = ProgressBackgroundSprite;
			var barRt = barGo.GetComponent<RectTransform>();
			barRt.anchorMin = new Vector2(0.5f, 0f);
			barRt.anchorMax = new Vector2(0.5f, 0f);
			barRt.pivot = new Vector2(0.5f, 0.5f);
			barRt.sizeDelta = new Vector2(600, 40);
			barRt.anchoredPosition = new Vector2(0, 80);

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
			fillAreaRt.anchorMin = new Vector2(0f, 0f);
			fillAreaRt.anchorMax = new Vector2(1f, 1f);
			fillAreaRt.offsetMin = new Vector2(5, 5);
			fillAreaRt.offsetMax = new Vector2(-5, -5);

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
		}

		IEnumerator RunFakeProgress()
		{
			float t = 0f;
			while (t < FakeDuration)
			{
				t += Time.deltaTime;
				if (_progress != null) _progress.value = Mathf.Clamp01(t / FakeDuration);
				yield return null;
			}
			if (_progress != null) _progress.value = 1f;
		}
	}
}


