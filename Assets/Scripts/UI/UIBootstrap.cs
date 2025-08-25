using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class UIBootstrap : MonoBehaviour
{
	[Header("Bootstrap Settings")]
	public bool buildOnStart = true;

	void Start()
	{
		if (!buildOnStart) return;
		EnsureEventSystem();
		var canvas = EnsureCanvas();

		var uiManager = FindObjectOfType<UIManager>();
		if (uiManager == null)
		{
			var go = new GameObject("UIManager");
			uiManager = go.AddComponent<UIManager>();
		}

		BuildAndWireUI(canvas, uiManager);
	}

	private void EnsureEventSystem()
	{
		if (FindObjectOfType<EventSystem>() != null) return;
		var es = new GameObject("EventSystem");
		es.AddComponent<EventSystem>();
		es.AddComponent<StandaloneInputModule>();
	}

	private Canvas EnsureCanvas()
	{
		var existing = FindObjectOfType<Canvas>();
		if (existing != null) return existing;

		var go = new GameObject("Canvas");
		var canvas = go.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		go.AddComponent<CanvasScaler>();
		go.AddComponent<GraphicRaycaster>();
		return canvas;
	}

	private GameObject CreatePanel(Transform parent, string name)
	{
		var panel = new GameObject(name);
		panel.transform.SetParent(parent, false);
		var img = panel.AddComponent<Image>();
		img.color = new Color(0f, 0f, 0f, 0.3f);
		var rt = panel.GetComponent<RectTransform>();
		rt.anchorMin = Vector2.zero;
		rt.anchorMax = Vector2.one;
		rt.offsetMin = Vector2.zero;
		rt.offsetMax = Vector2.zero;
		return panel;
	}

	private Button CreateButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax)
	{
		var btnGO = new GameObject(name);
		btnGO.transform.SetParent(parent, false);
		var img = btnGO.AddComponent<Image>();
		img.color = new Color(1f, 1f, 1f, 0.8f);
		var btn = btnGO.AddComponent<Button>();
		var rt = btnGO.GetComponent<RectTransform>();
		rt.anchorMin = anchorMin;
		rt.anchorMax = anchorMax;
		rt.offsetMin = new Vector2(10, 10);
		rt.offsetMax = new Vector2(-10, -10);

		var labelGO = new GameObject("Label");
		labelGO.transform.SetParent(btnGO.transform, false);
		var tmp = labelGO.AddComponent<TextMeshProUGUI>();
		tmp.text = label;
		tmp.alignment = TextAlignmentOptions.Center;
		var lrt = labelGO.GetComponent<RectTransform>();
		lrt.anchorMin = Vector2.zero;
		lrt.anchorMax = Vector2.one;
		lrt.offsetMin = Vector2.zero;
		lrt.offsetMax = Vector2.zero;
		return btn;
	}

	private TextMeshProUGUI CreateText(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax, int fontSize = 36)
	{
		var go = new GameObject(name);
		go.transform.SetParent(parent, false);
		var tmp = go.AddComponent<TextMeshProUGUI>();
		tmp.text = text;
		tmp.fontSize = fontSize;
		tmp.alignment = TextAlignmentOptions.Left;
		var rt = go.GetComponent<RectTransform>();
		rt.anchorMin = anchorMin;
		rt.anchorMax = anchorMax;
		rt.offsetMin = new Vector2(10, 10);
		rt.offsetMax = new Vector2(-10, -10);
		return tmp;
	}

	private Slider CreateSlider(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
	{
		var go = new GameObject(name);
		go.transform.SetParent(parent, false);
		var bg = go.AddComponent<Image>();
		bg.color = new Color(1f, 1f, 1f, 0.2f);
		var slider = go.AddComponent<Slider>();
		var rt = go.GetComponent<RectTransform>();
		rt.anchorMin = anchorMin;
		rt.anchorMax = anchorMax;
		rt.offsetMin = new Vector2(10, 10);
		rt.offsetMax = new Vector2(-10, -10);

		var fillArea = new GameObject("Fill Area");
		fillArea.transform.SetParent(go.transform, false);
		var faRT = fillArea.AddComponent<RectTransform>();
		faRT.anchorMin = new Vector2(0.05f, 0.25f);
		faRT.anchorMax = new Vector2(0.95f, 0.75f);

		var fill = new GameObject("Fill");
		fill.transform.SetParent(fillArea.transform, false);
		var fillImg = fill.AddComponent<Image>();
		fillImg.color = new Color(0.2f, 0.9f, 0.3f, 0.9f);
		slider.fillRect = fillImg.rectTransform;
		slider.targetGraphic = fillImg;
		slider.direction = Slider.Direction.LeftToRight;
		slider.value = 1f;
		return slider;
	}

	private void BuildAndWireUI(Canvas canvas, UIManager ui)
	{
		// Main Menu Panel
		var mainMenu = CreatePanel(canvas.transform, "MainMenuPanel");
		var startBtn = CreateButton(mainMenu.transform, "StartButton", "Start", new Vector2(0.35f, 0.55f), new Vector2(0.65f, 0.75f));
		var settingsBtn = CreateButton(mainMenu.transform, "SettingsButton", "Settings", new Vector2(0.35f, 0.35f), new Vector2(0.65f, 0.55f));
		var quitBtn = CreateButton(mainMenu.transform, "QuitButton", "Quit", new Vector2(0.35f, 0.15f), new Vector2(0.65f, 0.35f));

		// Game Panel (HUD)
		var gamePanel = CreatePanel(canvas.transform, "GamePanel");
		gamePanel.GetComponent<Image>().color = new Color(0, 0, 0, 0); // transparent HUD
		var scoreText = CreateText(gamePanel.transform, "ScoreText", "Score: 0", new Vector2(0f, 0.9f), new Vector2(0.4f, 1f), 28);
		var timeText = CreateText(gamePanel.transform, "TimeText", "00:00", new Vector2(0.4f, 0.9f), new Vector2(0.7f, 1f), 28);
		var healthSlider = CreateSlider(gamePanel.transform, "Health", new Vector2(0.75f, 0.92f), new Vector2(0.98f, 0.98f));
		var pauseBtn = CreateButton(gamePanel.transform, "PauseButton", "II", new Vector2(0.92f, 0.02f), new Vector2(0.98f, 0.1f));

		// Pause Panel
		var pausePanel = CreatePanel(canvas.transform, "PausePanel");
		var resumeBtn = CreateButton(pausePanel.transform, "ResumeButton", "Resume", new Vector2(0.35f, 0.55f), new Vector2(0.65f, 0.75f));
		var restartBtn = CreateButton(pausePanel.transform, "RestartButton", "Restart", new Vector2(0.35f, 0.35f), new Vector2(0.65f, 0.55f));
		var mainMenuBtn = CreateButton(pausePanel.transform, "MainMenuButton", "Main Menu", new Vector2(0.35f, 0.15f), new Vector2(0.65f, 0.35f));

		// Game Over Panel
		var gameOverPanel = CreatePanel(canvas.transform, "GameOverPanel");
		var finalScore = CreateText(gameOverPanel.transform, "FinalScore", "Final Score: 0", new Vector2(0.3f, 0.6f), new Vector2(0.7f, 0.8f), 36);
		var highScore = CreateText(gameOverPanel.transform, "HighScore", "High Score: 0", new Vector2(0.3f, 0.45f), new Vector2(0.7f, 0.6f), 28);
		var retryBtn = CreateButton(gameOverPanel.transform, "RetryButton", "Retry", new Vector2(0.35f, 0.25f), new Vector2(0.65f, 0.4f));
		var goMainMenuBtn = CreateButton(gameOverPanel.transform, "GameOverMainMenuButton", "Main Menu", new Vector2(0.35f, 0.1f), new Vector2(0.65f, 0.25f));

		// Level Complete Panel
		var levelCompletePanel = CreatePanel(canvas.transform, "LevelCompletePanel");
		var levelScore = CreateText(levelCompletePanel.transform, "LevelScore", "Level Score: 0", new Vector2(0.3f, 0.6f), new Vector2(0.7f, 0.8f), 36);
		var levelHigh = CreateText(levelCompletePanel.transform, "LevelHighScore", "High Score: 0", new Vector2(0.3f, 0.45f), new Vector2(0.7f, 0.6f), 28);
		var nextBtn = CreateButton(levelCompletePanel.transform, "NextLevelButton", "Next", new Vector2(0.35f, 0.25f), new Vector2(0.65f, 0.4f));
		var lcMainMenuBtn = CreateButton(levelCompletePanel.transform, "LevelMainMenuButton", "Main Menu", new Vector2(0.35f, 0.1f), new Vector2(0.65f, 0.25f));

		// Settings Panel (minimal)
		var settingsPanel = CreatePanel(canvas.transform, "SettingsPanel");
		var musicSlider = CreateSlider(settingsPanel.transform, "Music", new Vector2(0.2f, 0.6f), new Vector2(0.8f, 0.7f));
		var sfxSlider = CreateSlider(settingsPanel.transform, "SFX", new Vector2(0.2f, 0.45f), new Vector2(0.8f, 0.55f));
		var vibrationToggle = CreateButton(settingsPanel.transform, "VibrationToggle", "Vibration", new Vector2(0.4f, 0.25f), new Vector2(0.6f, 0.35f));

		// Wire to UIManager
		ui.mainMenuPanel = mainMenu;
		ui.gamePanel = gamePanel;
		ui.pausePanel = pausePanel;
		ui.gameOverPanel = gameOverPanel;
		ui.levelCompletePanel = levelCompletePanel;
		ui.settingsPanel = settingsPanel;

		ui.startGameButton = startBtn;
		ui.settingsButton = settingsBtn;
		ui.quitButton = quitBtn;
		ui.pauseButton = pauseBtn;
		ui.resumeButton = resumeBtn;
		ui.restartButton = restartBtn;
		ui.mainMenuButton = mainMenuBtn;
		ui.retryButton = retryBtn;
		ui.mainMenuFromGameOverButton = goMainMenuBtn;
		ui.nextLevelButton = nextBtn;
		ui.mainMenuFromLevelButton = lcMainMenuBtn;

		ui.scoreText = scoreText;
		ui.timeText = timeText;
		ui.healthSlider = healthSlider;

		// Health icons (optional minimal two)
		ui.healthIcons = new Image[3];
		for (int i = 0; i < ui.healthIcons.Length; i++)
		{
			var icon = new GameObject("Heart_" + i);
			icon.transform.SetParent(gamePanel.transform, false);
			var img = icon.AddComponent<Image>();
			img.color = new Color(1f, 0.2f, 0.2f, 0.9f);
			var rt = icon.GetComponent<RectTransform>();
			rt.anchorMin = new Vector2(0.02f + i * 0.04f, 0.85f);
			rt.anchorMax = new Vector2(0.05f + i * 0.04f, 0.88f);
			rt.offsetMin = Vector2.zero;
			rt.offsetMax = Vector2.zero;
			ui.healthIcons[i] = img;
		}

		ui.finalScoreText = finalScore;
		ui.highScoreText = highScore;
		ui.levelScoreText = levelScore;
		ui.levelHighScoreText = levelHigh;

		// Default visibility
		mainMenu.SetActive(true);
		gamePanel.SetActive(false);
		pausePanel.SetActive(false);
		gameOverPanel.SetActive(false);
		levelCompletePanel.SetActive(false);
		settingsPanel.SetActive(false);
	}
}