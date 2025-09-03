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

    private Button _Btn_reward_ad;


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


            var Btn_reward_ad_Transform = _UIRoot.transform.Find("Panel/MiddleArea/Btn_reward_ad");
            if (Btn_reward_ad_Transform != null)
            {
                _Btn_reward_ad = Btn_reward_ad_Transform.GetComponent<Button>();
            }

        }


        // 设置按钮事件
        if (_Btn_buy_tili != null)
            _Btn_buy_tili.onClick.AddListener(OnTiliButtonClicked);
        if (_Btn_buy_coin != null)
            _Btn_buy_coin.onClick.AddListener(OnCoinButtonClicked);
        if (_Btn_reward_ad != null)
            _Btn_reward_ad.onClick.AddListener(OnAdButtonClicked);

        // UI游戏管理器
        var hudInstance = UIManager.Instance.FindUI("GameplayHUD");
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
            var hudInstance = UIManager.Instance.FindUI("GameplayHUD");
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
