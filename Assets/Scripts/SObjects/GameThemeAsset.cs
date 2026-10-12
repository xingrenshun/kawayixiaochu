using System;
using System.Collections.Generic;
using UnityEngine;

namespace SObjects
{

    [Serializable]
    public class GameThemeItem
    {
        public Color shallowColor;
        public Color deepColor;
        public Sprite cardBack;
    }
    [CreateAssetMenu(fileName = "游戏主题数据", order = 0)]
    public class GameThemeAsset : ScriptableObject
    {
        public List<GameThemeItem> themes;
        
        private static GameThemeAsset _instance;
        public static GameThemeAsset Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = Resources.Load<GameThemeAsset>("ScriptAssets/游戏主题数据");
                return _instance;
            }
        }
    }
}