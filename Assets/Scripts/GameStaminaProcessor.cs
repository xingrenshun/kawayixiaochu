using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using FUnityCommon;
using UnityEngine;

public class GameStaminaProcessor
{
    private readonly RectTransform _renderRoot;
    private readonly GameObject _staminaPrefab;
    private int _stamina;

    private List<GameObject> _staminaObjs = new();

    public int CurrentStamina => _stamina;

    public GameStaminaProcessor(RectTransform renderRoot, GameObject staminaPrefab)
    {
        _renderRoot = renderRoot;
        _staminaPrefab = staminaPrefab;
    }

    public void InitStamina(int stamina)
    {
        _stamina = stamina;
        _staminaObjs.ForEach(i => Object.Destroy(i.gameObject));
        _staminaObjs.Clear();
        FAsync.Instance.StartRoutine(GenerateStaminaObj());
    }

    public int Consume(int amount = 1)
    {
        if (amount <= 0) return _stamina;
        var consume = Mathf.Min(amount, _stamina);
        _stamina -= consume;
        for (var i = 0; i < consume; i++)
        {
            if (_staminaObjs.Count == 0) break;
            var lastIndex = _staminaObjs.Count - 1;
            var obj = _staminaObjs[lastIndex];
            _staminaObjs.RemoveAt(lastIndex);
            if (obj != null) PlayConsumeEffect(obj, i * 0.06f);
        }

        return _stamina;
    }

    private void PlayConsumeEffect(GameObject obj, float delay)
    {
        var rt = obj.GetComponent<RectTransform>();
        if (rt == null)
        {
            Object.Destroy(obj);
            return;
        }

        var canvasGroup = obj.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = obj.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;

        var seq = DOTween.Sequence();
        seq.SetLink(obj);
        if (delay > 0f) seq.AppendInterval(delay);
        seq.Append(rt.DOScale(1.15f, 0.08f).SetEase(Ease.OutBack));
        seq.Join(rt.DOShakeAnchorPos(0.12f, 8f, 10, 90f, false, false));
        seq.Append(rt.DOScale(0f, 0.18f).SetEase(Ease.InBack));
        seq.Join(canvasGroup.DOFade(0f, 0.18f));
        seq.OnComplete(() =>
        {
            if (obj != null) Object.Destroy(obj);
        });
    }

    private const float SPACE = 15f;
    private IEnumerator GenerateStaminaObj()
    {
        // var rect = _renderRoot.rect;
        // 容器宽度
        // var width = rect.width;

        var prefabWidth = _staminaPrefab.GetComponent<RectTransform>().rect.width;
        for (var i = 0; i < _stamina; i++)
        {
            var newObj = Object.Instantiate(_staminaPrefab, _renderRoot);
            _staminaObjs.Add(newObj);
            // 计算中心的X坐标
            var startPosX = _stamina * (prefabWidth + SPACE) / 2;
            var x = prefabWidth / 2 + (prefabWidth + SPACE) * i - startPosX;
            newObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, 0);
            newObj.SetActive(true);
            yield return new WaitForSeconds(0.15f);
        }
    }
}
