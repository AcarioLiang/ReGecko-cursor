using UnityEngine;
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
            bgImg.color = new Color(0.08f, 0.09f, 0.1f, 1f); // 深色背景
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
            topRt.sizeDelta = new Vector2(0f, 120f);
            topRt.anchoredPosition = new Vector2(0f, 0f);
            var topLayout = top.AddComponent<HorizontalLayoutGroup>();
            topLayout.childForceExpandWidth = true;
            topLayout.childForceExpandHeight = true;
            topLayout.childAlignment = TextAnchor.MiddleCenter;
            topLayout.padding = new RectOffset(24, 24, 12, 12);
            topLayout.spacing = 16f;

            // 左组（暂停/重置）
            var left = new GameObject("LeftGroup");
            left.transform.SetParent(top.transform, false);
            var leftRt = left.AddComponent<RectTransform>();
            var leftLayout = left.AddComponent<HorizontalLayoutGroup>();
            leftLayout.childForceExpandWidth = false;
            leftLayout.childForceExpandHeight = true;
            leftLayout.spacing = 12f;
            var leftLE = left.AddComponent<LayoutElement>();
            leftLE.minWidth = 280f;

            CreateButton(left.transform, "PauseButton", "暂停");
            CreateButton(left.transform, "ResetButton", "重置");

            // 中间（关卡信息）
            var center = new GameObject("CenterInfo");
            center.transform.SetParent(top.transform, false);
            var centerRt = center.AddComponent<RectTransform>();
            var centerLE = center.AddComponent<LayoutElement>();
            centerLE.flexibleWidth = 1f;
            var centerText = center.AddComponent<Text>();
            centerText.text = "关卡 1-1";
            centerText.alignment = TextAnchor.MiddleCenter;
            centerText.fontSize = 36;
            centerText.color = Color.white;
            centerText.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 右组（金币）
            var right = new GameObject("RightGroup");
            right.transform.SetParent(top.transform, false);
            var rightRt = right.AddComponent<RectTransform>();
            var rightLE = right.AddComponent<LayoutElement>();
            rightLE.minWidth = 240f;
            var rightLayout = right.AddComponent<HorizontalLayoutGroup>();
            rightLayout.childForceExpandWidth = false;
            rightLayout.childForceExpandHeight = true;
            rightLayout.childAlignment = TextAnchor.MiddleRight;
            rightLayout.spacing = 8f;
            var coinText = CreateText(right.transform, "CoinsText", "金币: 0", 32, TextAnchor.MiddleRight);

            // 底部栏（道具）
            var bottom = new GameObject("BottomBar");
            bottom.transform.SetParent(root.transform, false);
            var bottomRt = bottom.AddComponent<RectTransform>();
            bottomRt.anchorMin = new Vector2(0f, 0f);
            bottomRt.anchorMax = new Vector2(1f, 0f);
            bottomRt.pivot = new Vector2(0.5f, 0f);
            bottomRt.sizeDelta = new Vector2(0f, 160f);
            bottomRt.anchoredPosition = new Vector2(0f, 0f);
            var bottomLayout = bottom.AddComponent<HorizontalLayoutGroup>();
            bottomLayout.childAlignment = TextAnchor.MiddleCenter;
            bottomLayout.childForceExpandWidth = false;
            bottomLayout.childForceExpandHeight = false;
            bottomLayout.padding = new RectOffset(0, 0, 24, 24);
            bottomLayout.spacing = 24f;

            CreateButton(bottom.transform, "ItemBtn1", "道具1", 220, 80);
            CreateButton(bottom.transform, "ItemBtn2", "道具2", 220, 80);
            CreateButton(bottom.transform, "ItemBtn3", "道具3", 220, 80);

            // 中间区域（游戏渲染区域）
            var middle = new GameObject("GameRenderArea");
            middle.transform.SetParent(root.transform, false);
            var middleRt = middle.AddComponent<RectTransform>();
            middleRt.anchorMin = new Vector2(0f, 0f);
            middleRt.anchorMax = new Vector2(1f, 1f);
            middleRt.offsetMin = new Vector2(0f, 160f);
            middleRt.offsetMax = new Vector2(0f, -120f);

            // UI游戏管理器
            var gameManager = middle.AddComponent<UIGameManager>();

            // 渲染背景（深色，让游戏内容更突出）
            //var renderBg = middle.AddComponent<Image>();
            //renderBg.color = new Color(0.05f, 0.05f, 0.08f, 1f);
            //renderBg.raycastTarget = false;

            return root;
        }

        static GameObject CreateButton(Transform parent, string name, string label, float w = 140f, float h = 80f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.15f);
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
    }
}


