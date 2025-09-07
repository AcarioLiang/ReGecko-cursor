using ReGecko.Framework.UI;
using ReGecko.GameCore.Flow;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIGameLobby : MonoBehaviour
{
    private GameObject _UIRoot;

    private Button _Btn_buy_tili;
    private Button _Btn_buy_coin;

    private Button _Btn_play;
    private Button _Btn_shop;
    private Button _Btn_map;
    private Button _Btn_setting;

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
        _UIRoot = UIManager.Instance.FindUI("GameLobby");

        // 注册组件
        if (_UIRoot != null)
        {
            // 查找CenterImage4Text组件
            var Btn_Play_Transform = _UIRoot.transform.Find("Panel/MiddleArea/Btn_level");
            if (Btn_Play_Transform != null)
            {
                _Btn_play = Btn_Play_Transform.GetComponent<Button>();
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


        // 设置按钮事件
        if (_Btn_buy_tili != null)
            _Btn_buy_tili.onClick.AddListener(OnTiliButtonClicked);
        if (_Btn_buy_coin != null)
            _Btn_buy_coin.onClick.AddListener(OnCoinButtonClicked);
        if (_Btn_play != null)
            _Btn_play.onClick.AddListener(OnPlayButtonClicked);
        if (_Btn_shop != null)
            _Btn_shop.onClick.AddListener(OnShopButtonClicked);
        if (_Btn_map != null)
            _Btn_map.onClick.AddListener(OnMapButtonClicked);
        if (_Btn_setting != null)
            _Btn_setting.onClick.AddListener(OnSettingButtonClicked);

    }

    void OnTiliButtonClicked()
    {

    }
    void OnCoinButtonClicked()
    {

    }
    void OnPlayButtonClicked()
    {
        GameContext.NextLoadIsPlayer = false; // 进入关卡加载
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
