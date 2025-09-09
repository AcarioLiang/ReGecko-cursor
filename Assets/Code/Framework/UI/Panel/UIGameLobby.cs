using ReGecko.Framework.UI;
using ReGecko.GameCore.Flow;
using ReGecko.GameCore.Player;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIGameLobby : MonoBehaviour
{
    private GameObject _UIRoot;

    private Button _Btn_buy_tili;
    private Button _Btn_buy_coin;

    private Button _Btn_play1;
    private Button _Btn_play2;
    private Button _Btn_play3;

    private Button _Btn_shop;
    private Button _Btn_map;
    private Button _Btn_setting;


    PlayerData _playerData;

    // Start is called before the first frame update
    void Start()
    {
        _playerData = PlayerService.Get();
        InitializeUI();
        InitData();
        SubscribeToEvents();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    void InitializeUI()
    {
        _UIRoot = UIManager.Instance.FindUI("GameLobby");

        // ע�����
        if (_UIRoot != null)
        {
            // ����CenterImage4Text���
            var Btn_Play_Transform1 = _UIRoot.transform.Find("Panel/MiddleArea/Btn_level1");
            if (Btn_Play_Transform1 != null)
            {
                _Btn_play1 = Btn_Play_Transform1.GetComponent<Button>();
            }
            var Btn_Play_Transform2 = _UIRoot.transform.Find("Panel/MiddleArea/Btn_level2");
            if (Btn_Play_Transform2 != null)
            {
                _Btn_play2 = Btn_Play_Transform2.GetComponent<Button>();
            }
            var Btn_Play_Transform3 = _UIRoot.transform.Find("Panel/MiddleArea/Btn_level3");
            if (Btn_Play_Transform3 != null)
            {
                _Btn_play3 = Btn_Play_Transform3.GetComponent<Button>();
            }

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


            var Btn_shop_Transform = _UIRoot.transform.Find("Panel/BottomArea/Btn_shop");
            if (Btn_shop_Transform != null)
            {
                _Btn_shop = Btn_shop_Transform.GetComponent<Button>();
            }


            var Btn_map_Transform = _UIRoot.transform.Find("Panel/BottomArea/Btn__map");
            if (Btn_map_Transform != null)
            {
                _Btn_map = Btn_map_Transform.GetComponent<Button>();
            }

            var Btn_setting_Transform = _UIRoot.transform.Find("Panel/BottomArea/Btn_setting");
            if (Btn_setting_Transform != null)
            {
                _Btn_setting = Btn_setting_Transform.GetComponent<Button>();
            }
        }


        // ���ð�ť�¼�
        if (_Btn_buy_tili != null)
            _Btn_buy_tili.onClick.AddListener(OnTiliButtonClicked);
        if (_Btn_buy_coin != null)
            _Btn_buy_coin.onClick.AddListener(OnCoinButtonClicked);
        if (_Btn_play1 != null)
            _Btn_play1.onClick.AddListener(OnPlayButtonClicked);
        if (_Btn_play2 != null)
            _Btn_play2.onClick.AddListener(OnPlayButtonClicked);
        if (_Btn_play3 != null)
            _Btn_play3.onClick.AddListener(OnPlayButtonClicked);
        if (_Btn_shop != null)
            _Btn_shop.onClick.AddListener(OnShopButtonClicked);
        if (_Btn_map != null)
            _Btn_map.onClick.AddListener(OnMapButtonClicked);
        if (_Btn_setting != null)
            _Btn_setting.onClick.AddListener(OnSettingButtonClicked);

    }

    void InitData()
    {
        List<Button> levelBtns = new List<Button>();
        levelBtns.Add(_Btn_play1);
        levelBtns.Add(_Btn_play2);
        levelBtns.Add(_Btn_play3);

        for (int i = 0; i < levelBtns.Count; i++)
        {
            if(i == _playerData.Level - 1)
            {
                levelBtns[i].enabled = true;
                levelBtns[i].transform.Find("lock").gameObject.SetActive(false);
            }
            else if(i < _playerData.Level - 1)
            {
                levelBtns[i].enabled = false;
                levelBtns[i].transform.Find("lock").gameObject.SetActive(false);

            }
            else if (i > _playerData.Level - 1)
            {
                levelBtns[i].enabled = false;
                levelBtns[i].transform.Find("lock").gameObject.SetActive(true);
            }
        }

    }


    /// <summary>
    /// �����¼�
    /// </summary>
    void SubscribeToEvents()
    {
        PlayerService.OnPlayerDataChanged += OnPlayerDataChanged;
    }

    /// <summary>
    /// ȡ�������¼�
    /// </summary>
    void UnsubscribeFromEvents()
    {
        PlayerService.OnPlayerDataChanged -= OnPlayerDataChanged;
    }




    /// <summary>
    /// ��Ϸ״̬�仯�¼�����
    /// </summary>
    void OnPlayerDataChanged(object sender, EventArgs e)
    {
        InitData();
    }


    void OnTiliButtonClicked()
    {

    }
    void OnCoinButtonClicked()
    {

    }
    void OnPlayButtonClicked()
    {
        GameContext.NextLoadIsPlayer = false; // ����ؿ�����
        ReGecko.Framework.Scene.SceneManager.Instance.LoadLoadingScene();
    }
    void OnShopButtonClicked()
    {
    }
    void OnMapButtonClicked()
    {
    }
    void OnSettingButtonClicked()
    {
    }

}
