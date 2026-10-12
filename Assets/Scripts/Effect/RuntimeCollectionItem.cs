using System;
using FTools.Tween;
using FUnityUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Effect
{
    public class RuntimeCollectionItem : MonoBehaviour
    {
        public TMP_Text amountLabel;
        public Image icon;
        [HideInInspector] public int index;
        private int _id;
        public int Id => _id;

        public const float BASE_OFFSET = 60f;

        public int amount;
        private float _width;
        public float Width => _width;
        private FTweenPosition _tweenPosition;
        private FTweenScale _tweenScale;
        private FTweenShake _tweenShake;
        private float _rootCanvasWidth;
        private RectTransform _rectTransform;
        private float Spacing => index == 0 ? 0 : index * 20f;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _tweenPosition = GetComponent<FTweenPosition>();
            _tweenScale = GetComponent<FTweenScale>();
            _tweenShake = GetComponent<FTweenShake>();
            _width = (transform as RectTransform)!.rect.width;
            _rootCanvasWidth = FViewManager.Instance.RootCanvasWidth;
        }

        public void Setup(int id, int idx, Sprite sprite, Action onComplete=null)
        {
            _id = id;
            index = idx;
            icon.sprite = sprite;
            amountLabel.text = "1";
            gameObject.SetActive(true);
            StartTween(true, onComplete);
            amount = 1;
        }

        public void StartTween(bool firstSetup=false, Action onComplete=null)
        {
            _tweenPosition.fromPosition = firstSetup ? new Vector2(transform.position.x + _rootCanvasWidth, 0)
                : _rectTransform.anchoredPosition;
            _tweenPosition.targetPosition = new Vector2(index * _width + _width / 2 + BASE_OFFSET + Spacing , 0);
            if (_tweenPosition.IsRunning)
            {
                _tweenPosition.Pause();
            }
            _tweenPosition.UpdateTween();
            _tweenPosition.Play().onComplete += () =>
            {
                onComplete?.Invoke();
            };
        }
        
        public void AddAmount()
        {
            amount++;
            amountLabel.text = amount.ToString();
            if (_tweenShake.IsRunning) _tweenShake.Pause();
            _tweenShake.Replay();
            if (_tweenScale.IsRunning) _tweenScale.Pause();
            _tweenScale.Replay();
        }
    }
}