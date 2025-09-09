using ReGecko.Framework.UI;
using ReGecko.Game;
using ReGecko.GameCore.Flow;
using ReGecko.GameCore.Player;
using ReGecko.Grid.Entities;
using ReGecko.SnakeSystem;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIGameGameMain : MonoBehaviour
{
    private GameObject _UIRoot;

    private Button _Btn_Pause;
    private Button _Btn_Reset;

    private Text _Txt_Lv;
    private GameObject _Panel_Hard;
    private Text _Txt_Time;
    private Button _Btn_buy_coin;
    private Text _Txt_coin;

    private Button _Btn_item1;
    private Button _Btn_item2;
    private Button _Btn_item3;

    private Button _Btn_buy_item1;
    private Button _Btn_buy_item2;
    private Button _Btn_buy_item3;



    // UI��Ϸ������
    private UIGameManager _gameManager;
    private PlayerData _playerData;
    private GameStateController _gameStateController;

    // Start is called before the first frame update
    void Start()
    {
        _playerData = PlayerService.Get();
        InitializeUI();
        InitUIData();
        SubscribeToEvents();


        // ��ʼ״̬
        UpdateUIForState(GameState.Playing);
    }

    private void Update()
    {
        UpdateGameInfo();
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
        if (_gameStateController != null)
        {
            _gameStateController.RestartGame();
        }
    }

    #region UI��ʼ��

    void InitializeUI()
    {
        _UIRoot = UIManager.Instance.FindUI("GameMain");

        // ע�����
        if (_UIRoot != null)
        {
            // ����CenterImage4Text���
            var Btn_Pause = _UIRoot.transform.Find("Panel/TopArea/Btn_Pause");
            if (Btn_Pause != null)
            {
                _Btn_Pause = Btn_Pause.GetComponent<Button>();
            }
            var Btn_Reset = _UIRoot.transform.Find("Panel/TopArea/Btn_Reset");
            if (Btn_Reset != null)
            {
                _Btn_Reset = Btn_Reset.GetComponent<Button>();
            }

            var Txt_Lv_Transform = _UIRoot.transform.Find("Panel/TopArea/lvbg/lv");
            if (Txt_Lv_Transform != null)
            {
                _Txt_Lv = Txt_Lv_Transform.GetComponent<Text>();
            }

            var Panel_Hard_Transform = _UIRoot.transform.Find("Panel/TopArea/hardbg");
            if (Panel_Hard_Transform != null)
            {
                _Panel_Hard = Panel_Hard_Transform.gameObject;
            }


            var Txt_Time_Transform = _UIRoot.transform.Find("Panel/TopArea/timebg/time");
            if (Txt_Time_Transform != null)
            {
                _Txt_Time = Txt_Time_Transform.GetComponent<Text>();
            }


            var Btn_item1_Transform = _UIRoot.transform.Find("Panel/BottomArea/ImageItem1/Btn_Item1");
            if (Btn_item1_Transform != null)
            {
                _Btn_item1 = Btn_item1_Transform.GetComponent<Button>();
            }
            var Btn_buy1_Transform = _UIRoot.transform.Find("Panel/BottomArea/ImageItem1/Btn_buy1");
            if (Btn_buy1_Transform != null)
            {
                _Btn_buy_item1 = Btn_buy1_Transform.GetComponent<Button>();
            }

            var Btn_item2_Transform = _UIRoot.transform.Find("Panel/BottomArea/ImageItem1/Btn_Item2");
            if (Btn_item2_Transform != null)
            {
                _Btn_item2 = Btn_item2_Transform.GetComponent<Button>();
            }
            var Btn_buy2_Transform = _UIRoot.transform.Find("Panel/BottomArea/ImageItem1/Btn_buy2");
            if (Btn_buy2_Transform != null)
            {
                _Btn_buy_item2 = Btn_buy2_Transform.GetComponent<Button>();
            }

            var Btn_item3_Transform = _UIRoot.transform.Find("Panel/BottomArea/ImageItem1/Btn_Item3");
            if (Btn_item3_Transform != null)
            {
                _Btn_item3 = Btn_item3_Transform.GetComponent<Button>();
            }
            var Btn_buy3_Transform = _UIRoot.transform.Find("Panel/BottomArea/ImageItem1/Btn_buy3");
            if (Btn_buy3_Transform != null)
            {
                _Btn_buy_item3 = Btn_buy3_Transform.GetComponent<Button>();
            }
        }


        // ���ð�ť�¼�
        if (_Btn_Pause != null)
            _Btn_Pause.onClick.AddListener(OnButtonClicked_Pause);
        if (_Btn_Reset != null)
            _Btn_Reset.onClick.AddListener(OnButtonClicked_Reset);
        if (_Btn_buy_coin != null)
            _Btn_buy_coin.onClick.AddListener(OnButtonClicked_buy_coin);
        if (_Btn_item1 != null)
            _Btn_item1.onClick.AddListener(OnButtonClicked_Item1);
        if (_Btn_buy_item1 != null)
            _Btn_buy_item1.onClick.AddListener(OnButtonClicked_buy_Item1);
        if (_Btn_item2 != null)
            _Btn_item2.onClick.AddListener(OnButtonClicked_Item2);
        if (_Btn_buy_item2 != null)
            _Btn_buy_item2.onClick.AddListener(OnButtonClicked_buy_Item2);
        if (_Btn_item3 != null)
            _Btn_item3.onClick.AddListener(OnButtonClicked_Item3);
        if (_Btn_buy_item3 != null)
            _Btn_buy_item3.onClick.AddListener(OnButtonClicked_buy_Item3);

    }


    void InitUIData()
    {
        if (_UIRoot != null)
        {
            var MiddleArea = _UIRoot.transform.Find("Panel/MiddleArea");
            if (MiddleArea != null)
            {
                _gameManager = MiddleArea.GetComponent<UIGameManager>();
                if (_gameManager == null)
                    _gameManager = _UIRoot.AddComponent<UIGameManager>();
            }

            if (_gameManager != null)
                _gameStateController = _gameManager.GetGameStateController();
        }

        if(_Txt_Lv != null && _playerData != null)
        {
            _Txt_Lv.text = "LEVEL " + _playerData.Level;
        }

    }


    #endregion

    /// <summary>
    /// �����¼�
    /// </summary>
    void SubscribeToEvents()
    {
        GameStateController.OnGameStateChanged += OnGameStateChanged;
    }

    /// <summary>
    /// ȡ�������¼�
    /// </summary>
    void UnsubscribeFromEvents()
    {
        GameStateController.OnGameStateChanged -= OnGameStateChanged;
    }




    /// <summary>
    /// ��Ϸ״̬�仯�¼�����
    /// </summary>
    void OnGameStateChanged(object sender, GameStateChangedEventArgs e)
    {
        UpdateUIForState(e.NewState);
    }

    /// <summary>
    /// ������Ϸ״̬����UI
    /// </summary>
    void UpdateUIForState(GameState state)
    {
        // �����������
        //UIManager.Instance.CloseAll();

        // ����״̬��ʾ��Ӧ���
        switch (state)
        {
            case GameState.Initializing:
                break;
            case GameState.Playing:
                // ��Ϸ��״̬����ʾ��ͣ��ť
                UIManager.Instance.Show("GameMain", GameContext.PreloadedUIPrefab_GameMain);
                break;
            case GameState.Paused:
                UIManager.Instance.Show("GameSetting", GameContext.PreloadedUIPrefab_GameSetting);
                break;
            case GameState.GameOver:
                if (_gameStateController && _gameStateController.RemainingTime > 0)
                {
                    _playerData.Level = _playerData.Level + 1;
                    if(_playerData.Level > GameContext.PlayerMaxLevel)
                    {
                        _playerData.Level = 1;
                    }
                    PlayerService.ChangePlayerData(_playerData);
                    UIManager.Instance.Show("GameSuccess", GameContext.PreloadedUIPrefab_GameSuccess);
                }
                else
                {
                    UIManager.Instance.Show("GameFaild", GameContext.PreloadedUIPrefab_GameFaild);
                }
                break;
        }

    }

    /// <summary>
    /// ������Ϸ��Ϣ��ʾ
    /// </summary>
    void UpdateGameInfo()
    {
        if (_gameStateController == null) return;

        // ����ʣ��ʱ��
        if (_Txt_Time != null)
        {
            float remainingTime = _gameStateController.RemainingTime;
            _Txt_Time.text = $"{FormatTime(remainingTime)}";
        }

    }


    #region �����¼�

    void OnButtonClicked_Pause()
    {
        if (_gameStateController != null)
        {
            _gameStateController.PauseGame();
        }
    }
    void OnButtonClicked_Reset()
    {
        if (_gameManager != null)
        {
            GameContext.NextLoadIsPlayer = false;
            ReGecko.Framework.Scene.SceneManager.Instance.LoadLoadingScene();
            var _gameStateController = _gameManager.GetGameStateController();
            if (_gameStateController != null)
            {
                _gameStateController.RestartGame();
            }
        }
    }

    void OnButtonClicked_buy_coin()
    {
    }
    void OnButtonClicked_Item1()
    {
    }
    void OnButtonClicked_buy_Item1()
    {
    }
    void OnButtonClicked_Item2()
    {
    }
    void OnButtonClicked_buy_Item2()
    {
    }
    void OnButtonClicked_Item3()
    {
    }
    void OnButtonClicked_buy_Item3()
    {
    }

    #endregion

    #region ��������

    /// <summary>
    /// ��ʽ��ʱ����ʾ
    /// </summary>
    string FormatTime(float timeInSeconds)
    {
        int minutes = Mathf.FloorToInt(timeInSeconds / 60f);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60f);
        return $"{minutes:00}:{seconds:00}";
    }

    /// <summary>
    /// ��ȡ״̬��ʾ�ı�
    /// </summary>
    string GetStateDisplayText(GameState state)
    {
        switch (state)
        {
            case GameState.Initializing:
                return "��ʼ����...";
            case GameState.Playing:
                return "��Ϸ��";
            case GameState.Paused:
                return "����ͣ";
            case GameState.GameOver:
                return "��Ϸ����";
            default:
                return "δ֪״̬";
        }
    }
    #endregion
}
