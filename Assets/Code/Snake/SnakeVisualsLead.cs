using DG.Tweening;
using ReGecko.GridSystem;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ReGecko.SnakeSystem
{
    public class SnakeVisualsLead : MonoBehaviour
    {
        const float EPS = 1e-4f;

        public GameObject BindObjectLead;
        public GameObject BindObjectLeadNext;
        bool IsHead;

        public Color BodyColor = Color.white;
        public Sprite BodySprite;
        public SpriteRenderer spriteRenderer;

        private GridConfig _grid;
        Tweener curFollowTweener; 
        RotateMode rotateMode = RotateMode.Fast; // 旋转模式
        float rotateDuration = 0.05f; // 旋转模式

        RectTransform targetRT;

        // Update is called once per frame
        void Update()
        {
        }

        public void Init(bool ishead,GameObject start, GameObject end, Sprite sp, GridConfig g)
        {
            IsHead = ishead;
            BindObjectLead = start;
            BindObjectLeadNext = end;
            BodySprite = sp;
            _grid = g;

            if(spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            if(spriteRenderer != null)
            {
                spriteRenderer.sprite = BodySprite;
                spriteRenderer.color = BodyColor;

     

                spriteRenderer.sortingLayerName = "Default"; // 或你的 UI Sorting Layer
                spriteRenderer.sortingOrder = 105;

                float size = _grid.CellSize * 1f;
                SetPixelSize(size, size);
            }

            if(BindObjectLead != null)
            {
                transform.position = BindObjectLead.transform.position;
                targetRT = BindObjectLead.GetComponent<RectTransform>();
            }
            UpdateRotation();
        }

        public GridConfig GetGrid()
        {
            return _grid;
        }

        public void UpdateRotation()
        {
            if (targetRT != null && targetRT.anchoredPosition3D.z < 0)
            {
                gameObject.SetActive(false);
                return;
            }

            if (BindObjectLead != null && BindObjectLeadNext != null)
            {
                
                transform.position = BindObjectLead.transform.position;

                Vector3 pLead = BindObjectLead.transform.position;
                Vector3 pNext = BindObjectLeadNext.transform.position;

                Vector3 direction = pNext - pLead;

                // 如果方向向量太小，不进行旋转
                if (direction.sqrMagnitude < EPS)
                    return;

                // 计算旋转角度（以Z轴为旋转轴）
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;


                // 获取当前欧拉角度
                Vector3 currentEuler = transform.eulerAngles;
                // 创建目标角度（只改变Y轴，保持X和Z不变）
                Vector3 targetEuler = new Vector3(currentEuler.x, currentEuler.y, angle);


                // 设置旋转（绕Z轴旋转）
                var newRot = Quaternion.AngleAxis(angle, Vector3.forward);


                if(!transform.rotation.Equals(newRot))
                {
                    transform.rotation = newRot;
                    //if (curFollowTweener == null || !curFollowTweener.IsPlaying())
                    //{
                    //    curFollowTweener?.Kill();
                    //    curFollowTweener = transform.DORotate(targetEuler, rotateDuration, rotateMode)
                    //                        .SetEase(Ease.OutCubic);
                    //}
                    //else
                    //{
                    //    // 更新Tweener的目标位置
                    //    curFollowTweener.ChangeEndValue(targetEuler, true).Restart();
                    //    //Debug.Log($"ChangeEndValue Restart target:{target}");
                    //}

                }

            }
        }

        void SetPixelSize(float width, float height)
        {
            if (spriteRenderer == null || spriteRenderer.sprite == null)
            {
                Debug.LogError("SpriteRenderer or Sprite is missing!");
                return;
            }

            Sprite sprite = spriteRenderer.sprite;

            float ppu = spriteRenderer.sprite.pixelsPerUnit;

            // 获取Sprite的实际像素尺寸（考虑裁剪）
            float originalWidth = sprite.rect.width;
            float originalHeight = sprite.rect.height;

            // 计算Scale
            Vector3 newScale = transform.localScale;
            newScale.x = width / originalWidth * ppu;
            newScale.y = height / originalHeight * ppu;

            transform.localScale = newScale;
        }
    }
}
