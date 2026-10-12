using System.Collections.Generic;
using FTools.Cache;
using GameGrid;
using UnityEngine;

public class BoomSpawner
{
    private readonly GameObject _boomPrefab;
    private readonly Transform _renderRoot;
    private readonly List<GameObject> _booms;

    private int _currentLevel;
    
    
    
    private const float POSSIBILITIES = 1f;

    private int _maxBoomCount;

    public BoomSpawner(GameObject boomPrefab, Transform renderRoot, int maxBoomCount = 1)
    {
        _boomPrefab = boomPrefab;
        _maxBoomCount = maxBoomCount;
        _renderRoot = renderRoot;
        _booms = new List<GameObject>();
    }

    public void Reset()
    {
        _booms.ForEach(i =>
        {
            if (i == null) return;
            Object.Destroy(i);
        });
        _booms.Clear();
        var cache = FCacheManager.GetCache(ECacheType.Local);
        _currentLevel = cache.Load("GAME_LEVEL", 1);
    }

    public void SpawnBoom(int x, int y)
    {
        if (_booms.Count >= _maxBoomCount || Random.value > POSSIBILITIES) return;
        var boomInstance = Object.Instantiate(_boomPrefab, _renderRoot);
        boomInstance.SetActive(true);
        boomInstance.GetComponent<BoomGrid>().Setup(x, y);
        _booms.Add(boomInstance);
    }
}