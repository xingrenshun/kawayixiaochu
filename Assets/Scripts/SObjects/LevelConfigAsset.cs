using System;
using System.Collections.Generic;
using FUnityAttribute.EditorAttributes;
using UnityEngine;

namespace SObjects
{

    [Serializable]
    public class LevelConfigItem
    {
        [FLabel("面板宽度")]public float panelWidth;
        [FLabel("面板高度")]public float panelHeight;
        [FLabel("最小障碍物生成系数")] public float minObstacle = 0.03f;
        [FLabel("最大障碍物生成系数")] public float maxObstacle = 0.08f;
        [FLabel("空白格子组")] public int blankGridGroupAmount = 2;
    }
    [CreateAssetMenu(fileName = "关卡配置", order = 0)]
    public class LevelConfigAsset : ScriptableObject
    {
        public List<LevelConfigItem> levels;

        private static LevelConfigAsset _instance;
        public static LevelConfigAsset Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = Resources.Load<LevelConfigAsset>("ScriptAssets/关卡配置");
                return _instance;
            }
        }

        public LevelConfigItem GetLevelConfig(int level)
        {
            var idx  = Mathf.Clamp(level, 1, levels.Count);
            if (levels.Count == 0) return null;
            return idx > levels.Count ? levels[^1] : levels[idx - 1];
        }
    }
}