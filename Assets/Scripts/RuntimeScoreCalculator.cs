using UnityEngine;

public class RuntimeScoreCalculator
{
    private int _baseScore;
    private int _gameGirdCount;

    private const int BASE_COL = 7;
    private const int BASE_ROW = 10;
    private const int REFERENCE_AREA = BASE_COL * BASE_ROW;
    
    // 当前实际的格子总数
    private int _currentGameGridCount;
    
    // 难度/面积倍率系数
    private float _areaRatio;
    public RuntimeScoreCalculator()
    {
        
    }
    /// <summary>
    /// 初始化计算器
    /// </summary>
    /// <param name="gameGridCount">实际有效的游戏格子总数</param>
    public void Setup(int gameGridCount)
    {
        _currentGameGridCount = gameGridCount;

        // 计算面积比率
        // 比率 > 1.0 表示当前屏幕比设计标准大（格子多，目标分应更高）
        // 比率 < 1.0 表示当前屏幕比设计标准小（格子少，目标分应更低）
        _areaRatio = (float)_currentGameGridCount / REFERENCE_AREA;
        Debug.Log($"[ScoreCalculator] 标准格子:{REFERENCE_AREA}, 当前格子:{_currentGameGridCount}, 目标倍率:{_areaRatio:F2}");
    }
    /// <summary>
    /// 获取当前尺寸下的【目标分数】
    /// </summary>
    /// <param name="standardTargetScore">策划配置的标准目标分（比如标准屏下1000分通关）</param>
    /// <returns>动态调整后的目标分</returns>
    public int GetDynamicTargetScore(int standardTargetScore)
    {
        // 底分不变，目标分随格子数量同比例缩放
        return Mathf.CeilToInt(standardTargetScore * _areaRatio);
    }
       
    /// <summary>
    /// 计算当前进度的百分比
    /// </summary>
    /// <param name="currentScore">当前实际得分</param>
    /// <param name="standardTargetScore">标准目标分</param>
    /// <returns>0.0 ~ 1.0 的进度</returns>
    public float GetProgress(int currentScore, int standardTargetScore)
    {
        int realTarget = GetDynamicTargetScore(standardTargetScore);
        if (realTarget == 0) return 1f;
        return Mathf.Clamp01((float)currentScore / realTarget);
    }
}