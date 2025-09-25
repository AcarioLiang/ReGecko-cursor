using ReGecko.GridSystem;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

        private GridConfig _grid;
        protected SnakeVisualsLeadSpriteManager _bodySpriteManager;
        public Vector3[] LinePositions;

        Vector3 _lastPosition;
        // Update is called once per frame
        void Update()
        {
            if(BindObjectLead != null && BindObjectLeadNext != null)
            {
                if(Vector3.Distance(_lastPosition, BindObjectLead.transform.position) > EPS)
                {
                    _lastPosition = BindObjectLead.transform.position;

                    transform.position = BindObjectLead.transform.position;
                    UpdateLines();
                    if (_bodySpriteManager != null)
                    {
                        _bodySpriteManager.OnSnakeLengthChanged();
                    }
                }

            }
        }

        public void Init(bool ishead,GameObject start, GameObject end, Sprite sp, GridConfig g)
        {
            IsHead = ishead;
            BindObjectLead = start;
            BindObjectLeadNext = end;
            BodySprite = sp;
            _grid = g;

            UpdateLines();
            InitializeBodySpriteManager();
        }

        public GridConfig GetGrid()
        {
            return _grid;
        }

        protected void UpdateLines()
        {
            if (BindObjectLead != null && BindObjectLeadNext != null)
            {
                if (LinePositions == null)
                    LinePositions = new Vector3[3];

                Vector3 pLead = BindObjectLead.transform.position;
                Vector3 pNext = BindObjectLeadNext.transform.position;

                Vector3 d = pLead - pNext;
                Vector3 virPos = d.sqrMagnitude < EPS ? pLead : pNext + d.normalized * _grid.CellSize;



                LinePositions[0] = virPos;
                LinePositions[1] = pLead;
                LinePositions[2] = pNext;
            }

        }
        protected void InitializeBodySpriteManager()
        {
            var bodySpriteGo = new GameObject("BodySpriteManager");
            bodySpriteGo.transform.SetParent(transform, false);
            _bodySpriteManager = bodySpriteGo.AddComponent<SnakeVisualsLeadSpriteManager>();

            Material newMaterial = Resources.Load<Material>("SnakeBody");
            _bodySpriteManager.BodyLineMaterial = newMaterial;

        }
    }
}
