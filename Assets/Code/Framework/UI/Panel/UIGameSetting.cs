using ReGecko.Framework.UI;
using ReGecko.Game;
using ReGecko.GameCore.Flow;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIGameSetting : MonoBehaviour
{
    private GameObject _UIRoot;

    private Button _Btn_buy_tili;
    private Button _Btn_buy_coin;

    private Button _Btn_sound;
    private Button _Btn_zhendong;

    private Button _Btn_privacy;
    private Button _Btn_language;
    private Button _Btn_contact;
    private Button _Btn_removeads;
    private Button _Txt_version;


    GameStateController _gameStateController;

    // Start is called before the first frame update
    void Start()
    {
        InitializeUI();

    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void InitializeUI()
    {
        _UIRoot = UIManager.Instance.FindUI("GameSetting");

        // 注册组件
        if (_UIRoot != null)
        {
            // 查找CenterImage4Text组件
            var Btn_Buy_Tili_Transform = _UIRoot.transform.Find("Panel/TopArea/ui_item_tili/Btn_Buy_Tili");
            if (Btn_Buy_Tili_Transform != null)
            {
                _Btn_buy_tili = Btn_Buy_Tili_Transform.GetComponent<Button>();
            }

            var Btn_Buy_Coin_Transform = _UIRoot.transform.Find("Panel/TopArea/ui_item_coin/Btn_Buy_coin");
            if (Btn_Buy_Coin_Transform != null)
            {
                _Btn_buy_coin = Btn_Buy_Coin_Transform.GetComponent<Button>();
            }


            var Btn_zhendong_Transform = _UIRoot.transform.Find("Panel/MiddleArea/Btn_zhendong");
            if (Btn_zhendong_Transform != null)
            {
                _Btn_zhendong = Btn_zhendong_Transform.GetComponent<Button>();
            }
            var Btn_sound_Transform = _UIRoot.transform.Find("Panel/MiddleArea/Btn_jingyin");
            if (Btn_sound_Transform != null)
            {
                _Btn_sound = Btn_sound_Transform.GetComponent<Button>();
            }

            var Btn_privacy_Transform = _UIRoot.transform.Find("Panel/MiddleArea/Btn_privacy");
            if (Btn_privacy_Transform != null)
            {
                _Btn_privacy = Btn_privacy_Transform.GetComponent<Button>();
            }

            var Btn_language_Transform = _UIRoot.transform.Find("Panel/MiddleArea/Btn_language");
            if (Btn_language_Transform != null)
            {
                _Btn_language = Btn_language_Transform.GetComponent<Button>();
            }
            var Btn_contact_Transform = _UIRoot.transform.Find("Panel/MiddleArea/Btn_contact");
            if (Btn_contact_Transform != null)
            {
                _Btn_contact = Btn_language_Transform.GetComponent<Button>();
            }
            var Btn_removeads_Transform = _UIRoot.transform.Find("Panel/MiddleArea/Btn_removeads");
            if (Btn_removeads_Transform != null)
            {
                _Btn_removeads = Btn_language_Transform.GetComponent<Button>();
            }
        }


        // 设置按钮事件
        if (_Btn_buy_tili != null)
            _Btn_buy_tili.onClick.AddListener(OnTiliButtonClicked);
        if (_Btn_buy_coin != null)
            _Btn_buy_coin.onClick.AddListener(OnCoinButtonClicked);
        if (_Btn_sound != null)
            _Btn_sound.onClick.AddListener(OnAdButtonClicked);
        if (_Btn_zhendong != null)
            _Btn_zhendong.onClick.AddListener(OnAdButtonClicked);
        if (_Btn_privacy != null)
            _Btn_privacy.onClick.AddListener(OnAdButtonClicked);
        if (_Btn_language != null)
            _Btn_language.onClick.AddListener(OnAdButtonClicked);
        if (_Btn_contact != null)
            _Btn_contact.onClick.AddListener(OnAdButtonClicked);
        if (_Btn_removeads != null)
            _Btn_removeads.onClick.AddListener(OnAdButtonClicked);

        // UI游戏管理器
        var hudInstance = UIManager.Instance.FindUI("GameMain");
        if(hudInstance != null)
        {
            var gameManager = hudInstance.GetComponentInChildren<UIGameManager>();
            if (gameManager != null)
            {
                _gameStateController = gameManager.GetGameStateController();
            }
        }

        if(_gameStateController == null)
        {

            Debug.Log(" not  _gameStateController is null!!!");
        }
        // 查找并初始化UI游戏管理器


    }

    void OnTiliButtonClicked()
    {

    }
    void OnCoinButtonClicked()
    {

    }
    private GameStateController GetGameStateController()
    {
        if (_gameStateController == null)
        {
            var hudInstance = UIManager.Instance.FindUI("GameMain");
            if (hudInstance != null)
            {
                var gameManager = hudInstance.GetComponentInChildren<UIGameManager>();
                if (gameManager != null)
                {
                    _gameStateController = gameManager.GetGameStateController();
                }
            }
        }
        return _gameStateController;
    }

    void OnAdButtonClicked()
    {
        UIManager.Instance.Hide("GameSetting");
        var controller = GetGameStateController();
        if (controller != null)
        {
            controller.ResumeGame();
        }
        else
        {
            Debug.LogError("无法获取GameStateController!");
        }
    }
}
