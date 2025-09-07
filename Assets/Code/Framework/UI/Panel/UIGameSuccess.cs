using ReGecko.Framework.UI;
using ReGecko.GameCore.Flow;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIGameSuccess : MonoBehaviour
{
    private GameObject _UIRoot;

    private Button _Btn_buy_tili;
    private Button _Btn_buy_coin;

    private Button _Btn_reward_ad;
    private Button _Btn_reward_normal;

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
        _UIRoot = UIManager.Instance.FindUI("GameSuccess");

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


            var Btn_reward_ad_Transform = _UIRoot.transform.Find("Panel/BottomArea/Btn_reward_ad");
            if (Btn_reward_ad_Transform != null)
            {
                _Btn_reward_ad = Btn_reward_ad_Transform.GetComponent<Button>();
            }

            var Btn_reward_normal_Transform = _UIRoot.transform.Find("Panel/BottomArea/Btn_reward_coin");
            if (Btn_reward_normal_Transform != null)
            {
                _Btn_reward_normal = Btn_reward_normal_Transform.GetComponent<Button>();
            }
        }


        // 设置按钮事件
        if (_Btn_buy_tili != null)
            _Btn_buy_tili.onClick.AddListener(OnTiliButtonClicked);
        if (_Btn_buy_coin != null)
            _Btn_buy_coin.onClick.AddListener(OnCoinButtonClicked);
        if (_Btn_reward_ad != null)
            _Btn_reward_ad.onClick.AddListener(OnAdButtonClicked);
        if (_Btn_reward_normal != null)
            _Btn_reward_normal.onClick.AddListener(OnNormalButtonClicked);

    }

    void OnTiliButtonClicked()
    {

    }
    void OnCoinButtonClicked()
    {

    }
    void OnAdButtonClicked()
    {
        GameContext.NextLoadIsPlayer = true; // 进入关卡加载
        ReGecko.Framework.Scene.SceneManager.Instance.LoadLoadingScene();
    }
    void OnNormalButtonClicked()
    {
        GameContext.NextLoadIsPlayer = true; // 进入关卡加载
        ReGecko.Framework.Scene.SceneManager.Instance.LoadLoadingScene();
    }
}
