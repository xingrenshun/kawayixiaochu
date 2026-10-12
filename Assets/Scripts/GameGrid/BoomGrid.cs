using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using View;

namespace GameGrid
{
    public class BoomGrid : MonoBehaviour
    {
        public int x, y;

        public RectTransform boomIcon;
        public RectTransform boomBg;

        public RectTransform rectTransform;

        

        // 爆炸时间
        private float _timeToBoom;
        // 消除的次数
        private int _cancelCombo;
        private void Awake()
        {
        }

        public void Setup(int inX, int inY)
        {
            x = inX;
            y = inY;

            var view = AutoLayoutGameView.Instance;
            rectTransform.anchoredPosition = new Vector2(view.GetXPosition(x), view.GetYPosition(y));
            rectTransform.sizeDelta = view.GetCellSize();
            
            boomBg.localScale *= 3f;
            
            CalculateBoomTimeAndCombo(AutoLayoutGameView.Instance.GameGrids);
            
        }

        private void CalculateBoomTimeAndCombo(List<Grid> gameGrids)
        {
            var grouped = gameGrids.GroupBy(i => i.Id).ToDictionary(i => i.Key, i => Mathf.FloorToInt(i.Count() / 2f));
            Debug.Log(grouped.Count);
            // 找出count大于2的
            var enableGrids = grouped.Where(i => i.Value > 0);
            var keyValuePairs = enableGrids as KeyValuePair<int, int>[] ?? enableGrids.ToArray();
            var maxGroup = keyValuePairs.Max(i => i.Value);
            var minGroup = keyValuePairs.Min(i => i.Value);
            Debug.Log("最大组数: " + maxGroup);
            Debug.Log("最小组数: " + minGroup);
            
        }
    }
}