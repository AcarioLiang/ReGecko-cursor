using ReGecko.Framework.Resources;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ReGecko.Framework.UI
{
    public static class GameplayHUDBuilder
    {
        public static GameObject BuildPrefabTemplate()
        {
            var root = new GameObject("GameplayHUD");
            var rt = root.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // 背景（置底）
            var bg = new GameObject("Background");
            bg.transform.SetParent(root.transform, false);
            var bgImg = bg.AddComponent<Image>();
            bgImg.sprite = ResourceManager.LoadPNG(ResourceDefine.Game_BG);
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 0f);
            bgRt.anchorMax = new Vector2(1f, 1f);
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            // 顶部栏
            var top = new GameObject("TopBar");
            top.transform.SetParent(root.transform, false);
            var topRt = top.AddComponent<RectTransform>();
            topRt.anchorMin = new Vector2(0f, 1f);
            topRt.anchorMax = new Vector2(1f, 1f);
            topRt.pivot = new Vector2(0.5f, 1f);
            topRt.sizeDelta = new Vector2(0f, 250);
            topRt.anchoredPosition = new Vector2(0f, 0f);

            // 左组（暂停/重置按钮）
            var left = new GameObject("LeftGroup");
            left.transform.SetParent(top.transform, false);
            var leftRt = left.AddComponent<RectTransform>();
            leftRt.anchorMin = new Vector2(0f, 0f);
            leftRt.anchorMax = new Vector2(0f, 1f);
            leftRt.pivot = new Vector2(0f, 0.5f);
            leftRt.anchoredPosition = new Vector2(20f, 0f);
            leftRt.sizeDelta = new Vector2(201f, 0f); // 98*2 + 5 = 201

            // 暂停按钮
            var pauseBtn = CreateButton(left.transform, "PauseButton", "", 98f, 98f, ResourceDefine.Game_Btn_Pause);
            var pauseRt = pauseBtn.GetComponent<RectTransform>();
            pauseRt.anchorMin = new Vector2(0f, 0.5f);
            pauseRt.anchorMax = new Vector2(0f, 0.5f);
            pauseRt.pivot = new Vector2(0f, 0.5f);
            pauseRt.anchoredPosition = new Vector2(0f, 61f);

            // 重置按钮
            var resetBtn = CreateButton(left.transform, "ResetButton", "", 98f, 98f, ResourceDefine.Game_Btn_Restat);
            var resetRt = resetBtn.GetComponent<RectTransform>();
            resetRt.anchorMin = new Vector2(0f, 0.5f);
            resetRt.anchorMax = new Vector2(0f, 0.5f);
            resetRt.pivot = new Vector2(0f, 0.5f);
            resetRt.anchoredPosition = new Vector2(103f, 61f); // 98 + 5 = 103

            var resetBtnCom = resetBtn.GetComponent<Button>();
            // 添加按钮点击事件
            resetBtnCom.onClick.AddListener(() =>
            {
                SceneManager.LoadScene(GameCore.Flow.GameScenes.Loading);
                UIManager.Instance.Destroy("GameplayHUD");
            });

            // 中间组（文体）
            var middle = new GameObject("MiddleGroup");

            middle.transform.SetParent(top.transform, false);
            var middleRt = middle.AddComponent<RectTransform>();
            middleRt.anchorMin = new Vector2(0.5f, 0f);
            middleRt.anchorMax = new Vector2(0.5f, 1f);
            middleRt.pivot = new Vector2(0.5f, 0.5f);
            middleRt.anchoredPosition = new Vector2(0f, 0f);
            middleRt.sizeDelta = new Vector2(201f, 0f); // 98*2 + 5 = 201

            // 中间区域的三张图片（居中对齐）
            var centerImages = new GameObject("CenterImages");
            centerImages.transform.SetParent(middle.transform, false);
            var centerImagesRt = centerImages.AddComponent<RectTransform>();
            centerImagesRt.anchorMin = new Vector2(0.5f, 0.5f);
            centerImagesRt.anchorMax = new Vector2(0.5f, 0.5f);
            centerImagesRt.pivot = new Vector2(0.5f, 0.5f);
            centerImagesRt.anchoredPosition = Vector2.zero;

            // 第一张图片 (261*87)
            var centerImg11 = CreateImage(centerImages.transform, "CenterImage1", 261f, 87f, ResourceDefine.Game_Img_Lv_Bg);
            var centerImg1Rt = centerImg11.GetComponent<RectTransform>();
            centerImg1Rt.anchorMin = new Vector2(0.5f, 0.5f);
            centerImg1Rt.anchorMax = new Vector2(0.5f, 0.5f);
            centerImg1Rt.pivot = new Vector2(0.5f, 0.5f);
            centerImg1Rt.anchoredPosition = new Vector2(0f, 60f); // 居中，稍微向上
            centerImg1Rt.sizeDelta = new Vector2(261f, 87f); // 98*2 + 5 = 201

            // 第一张图片文字
            var textGo1 = new GameObject("CenterImage1Text");
            textGo1.transform.SetParent(centerImg11.transform, false);
            var text1 = textGo1.AddComponent<Text>();
            text1.text = "LEVEL 1";
            text1.fontSize = 36;
            text1.alignment = TextAnchor.MiddleCenter;
            text1.color = Color.white;
            text1.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text1.raycastTarget = false; // 文字不阻挡点击事件
            var textRt1 = textGo1.GetComponent<RectTransform>();
            textRt1.anchorMin = Vector2.zero;
            textRt1.anchorMax = Vector2.one;
            textRt1.offsetMin = Vector2.zero;
            textRt1.offsetMax = Vector2.zero;

            // 第二张图片 (57*34)
            var centerImg2 = CreateImage(centerImages.transform, "CenterImage2", 57f, 34f, ResourceDefine.Game_Img_Title_hard, true);
            var centerImg2Rt = centerImg2.GetComponent<RectTransform>();
            centerImg2Rt.anchorMin = new Vector2(0.5f, 0.5f);
            centerImg2Rt.anchorMax = new Vector2(0.5f, 0.5f);
            centerImg2Rt.pivot = new Vector2(0.5f, 0.5f);
            centerImg2Rt.anchoredPosition = new Vector2(0f, 16f); // 居中
            centerImg2Rt.sizeDelta = new Vector2(170f, 35f); // 98*2 + 5 = 201

            // 第二张图片文字
            var textGo2 = new GameObject("CenterImage2Text");
            textGo2.transform.SetParent(centerImg2.transform, false);
            var text2 = textGo2.AddComponent<Text>();
            text2.text = "VERY HARD";
            text2.fontSize = 23;
            text2.alignment = TextAnchor.MiddleCenter;
            text2.color = Color.white;
            text2.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text2.raycastTarget = false; // 文字不阻挡点击事件
            var textRt2 = textGo2.GetComponent<RectTransform>();
            textRt2.anchorMin = Vector2.zero;
            textRt2.anchorMax = Vector2.one;
            textRt2.offsetMin = Vector2.zero;
            textRt2.offsetMax = Vector2.zero;


            // 第四张图片 (261*87)
            var centerImg4 = CreateImage(centerImages.transform, "CenterImage4", 261f, 87f, ResourceDefine.Game_Img_Time_bg, true);
            var centerImg4Rt = centerImg4.GetComponent<RectTransform>();
            centerImg4Rt.anchorMin = new Vector2(0.5f, 0.5f);
            centerImg4Rt.anchorMax = new Vector2(0.5f, 0.5f);
            centerImg4Rt.pivot = new Vector2(0.5f, 0.5f);
            centerImg4Rt.anchoredPosition = new Vector2(0f, -52f); // 居中，稍微向下


            // 第四张图片文字
            var textGo4 = new GameObject("CenterImage4Text");
            textGo4.transform.SetParent(centerImg4.transform, false);
            var text4 = textGo4.AddComponent<Text>();
            text4.text = "88:88";
            text4.fontSize = 36;
            text4.alignment = TextAnchor.MiddleCenter;
            text4.color = Color.white;
            text4.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text4.raycastTarget = false; // 文字不阻挡点击事件
            var textRt4 = textGo4.GetComponent<RectTransform>();
            textRt4.anchorMin = Vector2.zero;
            textRt4.anchorMax = Vector2.one;
            textRt4.offsetMin = Vector2.zero;
            textRt4.offsetMax = Vector2.zero;


            // 第三张图片 (90*106)
            var centerImg3 = CreateImage(centerImages.transform, "CenterImage3", 90f, 106f, ResourceDefine.Game_Img_Time_icon);
            var centerImg3Rt = centerImg3.GetComponent<RectTransform>();
            centerImg3Rt.anchorMin = new Vector2(0.5f, 0.5f);
            centerImg3Rt.anchorMax = new Vector2(0.5f, 0.5f);
            centerImg3Rt.pivot = new Vector2(0.5f, 0.5f);
            centerImg3Rt.anchoredPosition = new Vector2(-110f, -52f); // 居中，稍微向下


            // 右组（图片和按钮）
            var right = new GameObject("RightGroup");
            right.transform.SetParent(top.transform, false);
            var rightRt = right.AddComponent<RectTransform>();
            rightRt.anchorMin = new Vector2(1f, 0f);
            rightRt.anchorMax = new Vector2(1f, 1f);
            rightRt.pivot = new Vector2(1f, 0.5f);
            rightRt.anchoredPosition = new Vector2(-20f, 0f);
            rightRt.sizeDelta = new Vector2(320f, 0f);

            // 第二个图片 (109*83) 带文本框
            var img2Container = new GameObject("Image2Container");
            img2Container.transform.SetParent(right.transform, false);
            var img2ContainerRt = img2Container.AddComponent<RectTransform>();
            img2ContainerRt.anchorMin = new Vector2(0f, 0.5f);
            img2ContainerRt.anchorMax = new Vector2(0f, 0.5f);
            img2ContainerRt.pivot = new Vector2(0f, 0.5f);
            img2ContainerRt.anchoredPosition = new Vector2(197f, 0f); // 118 + 69 + 10 = 197
            img2ContainerRt.sizeDelta = new Vector2(109f, 83f);

            // 图片
            var img2 = CreateImage(img2Container.transform, "Image2", 259f, 83f, ResourceDefine.Game_Img_Coin_bg);
            var img2Rt = img2.GetComponent<RectTransform>();
            img2Rt.anchorMin = new Vector2(0.5f, 0.5f);
            img2Rt.anchorMax = new Vector2(0.5f, 0.5f);
            img2Rt.offsetMin = Vector2.zero;
            img2Rt.offsetMax = Vector2.zero;
            img2Rt.anchoredPosition = new Vector2(-53f, 66f); // 118 + 69 + 10 = 197
            img2Rt.sizeDelta = new Vector2(259f, 83f);

            // 文本框
            var img2TextObj = CreateText(img2.transform, "Image2Text", "8888", 24, TextAnchor.MiddleCenter);
            var img2TextRt = img2TextObj.GetComponent<RectTransform>();
            img2TextRt.anchorMin = Vector2.zero;
            img2TextRt.anchorMax = Vector2.one;
            img2TextRt.offsetMin = Vector2.zero;
            img2TextRt.offsetMax = Vector2.zero;
            var img2Text = img2TextObj.GetComponent<Text>();
            img2Text.color = Color.gray;

            // 第一个图片 (108*103)
            var img1 = CreateImage(right.transform, "Image1", 102f, 102f, ResourceDefine.Game_Img_Coin_icon);
            var img1Rt = img1.GetComponent<RectTransform>();
            img1Rt.anchorMin = new Vector2(0f, 0.5f);
            img1Rt.anchorMax = new Vector2(0f, 0.5f);
            img1Rt.pivot = new Vector2(0f, 0.5f);
            img1Rt.anchoredPosition = new Vector2(57f, 70f);

            // 按钮 (69*68)
            var rightBtn = CreateButton(right.transform, "RightButton", "", 55f, 55f, ResourceDefine.Game_Btn_buy);
            var rightBtnRt = rightBtn.GetComponent<RectTransform>();
            rightBtnRt.anchorMin = new Vector2(0f, 0.5f);
            rightBtnRt.anchorMax = new Vector2(0f, 0.5f);
            rightBtnRt.pivot = new Vector2(0f, 0.5f);
            rightBtnRt.anchoredPosition = new Vector2(118f, 39f); // 108 + 10 = 118

            
            // 底部栏（道具）
            var bottom = new GameObject("BottomBar");
            bottom.transform.SetParent(root.transform, false);
            var bottomRt = bottom.AddComponent<RectTransform>();
            bottomRt.anchorMin = new Vector2(0f, 0f);
            bottomRt.anchorMax = new Vector2(1f, 0f);
            bottomRt.pivot = new Vector2(0.5f, 0f);
            bottomRt.sizeDelta = new Vector2(0f, 410f);
            bottomRt.anchoredPosition = new Vector2(0f, 0f);

            // 三个道具按钮（居中对齐，大小204*204）
            var itemBtnBg1 = CreateImage(bottom.transform, "ImageItem1", 206f, 206f, ResourceDefine.Game_Item_bg);
            var imgBtnbg1Rt = itemBtnBg1.GetComponent<RectTransform>();
            imgBtnbg1Rt.anchorMin = new Vector2(0.5f, 0.5f);
            imgBtnbg1Rt.anchorMax = new Vector2(0.5f, 0.5f);
            imgBtnbg1Rt.pivot = new Vector2(0.5f, 0.5f);
            imgBtnbg1Rt.anchoredPosition = new Vector2(-274f, 0f); // 左侧

            var itemBtn1 = CreateButton(itemBtnBg1.transform, "ItemBtn1", "", 204f, 204f, ResourceDefine.Game_Item_icon1);
            var itemBtn1Rt = itemBtn1.GetComponent<RectTransform>();
            itemBtn1Rt.anchorMin = new Vector2(0.5f, 0.5f);
            itemBtn1Rt.anchorMax = new Vector2(0.5f, 0.5f);
            itemBtn1Rt.pivot = new Vector2(0.5f, 0.5f);

            var itemBuyBtn1 = CreateButton(itemBtn1.transform, "ItemButBtn1", "", 55f, 55f, ResourceDefine.Game_Btn_buy);
            var itemBuyBtn1Rt = itemBuyBtn1.GetComponent<RectTransform>();
            itemBuyBtn1Rt.anchorMin = new Vector2(1f, 0f);
            itemBuyBtn1Rt.anchorMax = new Vector2(1f, 0f);
            itemBuyBtn1Rt.pivot = new Vector2(1f, 0f);
            itemBuyBtn1Rt.anchoredPosition = new Vector2(0f, 0f);


            var itemBtnBg2 = CreateImage(bottom.transform, "ImageItem2", 206f, 206f, ResourceDefine.Game_Item_bg);
            var imgBtnbg2Rt = itemBtnBg2.GetComponent<RectTransform>();
            imgBtnbg2Rt.anchorMin = new Vector2(0.5f, 0.5f);
            imgBtnbg2Rt.anchorMax = new Vector2(0.5f, 0.5f);
            imgBtnbg2Rt.pivot = new Vector2(0.5f, 0.5f);
            imgBtnbg2Rt.anchoredPosition = new Vector2(0f, 0f);

            var itemBtn2 = CreateButton(itemBtnBg2.transform, "ItemBtn2", "", 204f, 204f, ResourceDefine.Game_Item_icon2);
            var itemBtn2Rt = itemBtn2.GetComponent<RectTransform>();
            itemBtn2Rt.anchorMin = new Vector2(0.5f, 0.5f);
            itemBtn2Rt.anchorMax = new Vector2(0.5f, 0.5f);
            itemBtn2Rt.pivot = new Vector2(0.5f, 0.5f);

            var itemBuyBtn2 = CreateButton(itemBtn2.transform, "ItemButBtn2", "", 55f, 55f, ResourceDefine.Game_Btn_buy);
            var itemBuyBtn2Rt = itemBuyBtn2.GetComponent<RectTransform>();
            itemBuyBtn2Rt.anchorMin = new Vector2(1f, 0f);
            itemBuyBtn2Rt.anchorMax = new Vector2(1f, 0f);
            itemBuyBtn2Rt.pivot = new Vector2(1f, 0f);
            itemBuyBtn2Rt.anchoredPosition = new Vector2(0f, 0f);


            var itemBtnBg3 = CreateImage(bottom.transform, "ImageItem3", 206f, 206f, ResourceDefine.Game_Item_bg);
            var imgBtnbg3Rt = itemBtnBg3.GetComponent<RectTransform>();
            imgBtnbg3Rt.anchorMin = new Vector2(0.5f, 0.5f);
            imgBtnbg3Rt.anchorMax = new Vector2(0.5f, 0.5f);
            imgBtnbg3Rt.pivot = new Vector2(0.5f, 0.5f);
            imgBtnbg3Rt.anchoredPosition = new Vector2(274f, 0f);

            var itemBtn3 = CreateButton(itemBtnBg3.transform, "ItemBtn3", "", 204f, 204f, ResourceDefine.Game_Item_icon3);
            var itemBtn3Rt = itemBtn3.GetComponent<RectTransform>();
            itemBtn3Rt.anchorMin = new Vector2(0.5f, 0.5f);
            itemBtn3Rt.anchorMax = new Vector2(0.5f, 0.5f);
            itemBtn3Rt.pivot = new Vector2(0.5f, 0.5f);
            itemBtn3Rt.anchoredPosition = new Vector2(0f, 0f); // 右侧

            var itemBuyBtn3 = CreateButton(itemBtn3.transform, "ItemButBtn3", "", 55f, 55f, ResourceDefine.Game_Btn_buy);
            var itemBuyBtn3Rt = itemBuyBtn3.GetComponent<RectTransform>();
            itemBuyBtn3Rt.anchorMin = new Vector2(1f, 0f);
            itemBuyBtn3Rt.anchorMax = new Vector2(1f, 0f);
            itemBuyBtn3Rt.pivot = new Vector2(1f, 0f);
            itemBuyBtn3Rt.anchoredPosition = new Vector2(0f, 0f);

            // 中间区域（游戏渲染区域）
            var gamemiddle = new GameObject("GameRenderArea");
            gamemiddle.transform.SetParent(root.transform, false);
            var gamemmiddleRt = gamemiddle.AddComponent<RectTransform>();
            gamemmiddleRt.anchorMin = new Vector2(0f, 0f);
            gamemmiddleRt.anchorMax = new Vector2(1f, 1f);
            gamemmiddleRt.offsetMin = new Vector2(0f, 410f); // BottomBar高度
            gamemmiddleRt.offsetMax = new Vector2(0f, -250f); // TopBar高度




            // UI游戏管理器
            var gameManager = gamemiddle.AddComponent<UIGameManager>();

            return root;
        }

        static GameObject CreateButton(Transform parent, string name, string label, float w = 140f, float h = 80f, string sprite = "")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            if(!string.IsNullOrEmpty(sprite))
            {
                img.sprite = ResourceManager.LoadPNG(sprite);
            }
            else
            {
                img.color = new Color(1f, 1f, 1f, 0.15f);
            }
            var btn = go.AddComponent<Button>();
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);
            var txt = CreateText(go.transform, "Text", label, 28, TextAnchor.MiddleCenter);
            return go;
        }

        static Text CreateText(Transform parent, string name, string content, int size, TextAnchor anchor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var txt = go.AddComponent<Text>();
            txt.text = content;
            txt.fontSize = size;
            txt.alignment = anchor;
            txt.color = Color.white;
            txt.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return txt;
        }

        static GameObject CreateImage(Transform parent, string name, float width, float height, string sprite = "", bool isTiled = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            if (!string.IsNullOrEmpty(sprite))
            {
                img.sprite = ResourceManager.LoadPNG(sprite);
            }
            else
            {
                img.color = new Color(1f, 1f, 1f, 0.8f); // 半透明白色
            }
            if(isTiled)
            {
                img.type = Image.Type.Tiled;
            }

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, height);
            return go;
        }
    }
}


