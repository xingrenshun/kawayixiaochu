using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace Effect
{
    public class ImageShadowFade
    {
        #region Pool

        private readonly ObjectPool<Image> _fadeImagePool;
        private readonly List<Image> _waitingFadeImages;
        #endregion

        public ImageShadowFade(Image prefabImage, int defaultCapacity=100, int maxCapacity=500)
        {
            _fadeImagePool = new ObjectPool<Image>(
                () => Object.Instantiate(prefabImage),   // Create
                obj => obj.gameObject.SetActive(true),  // On Get
                obj => obj.gameObject.SetActive(false), // On Release
                Object.Destroy,         // On Destroy
                false, defaultCapacity, maxCapacity
            );
            _waitingFadeImages = new List<Image>();
        }
        public void AddImageToFadeList(Transform parent, Vector3 pos, Sprite icon)
        {
            var instance = _fadeImagePool.Get();
            instance.sprite = icon;
            instance.color = Color.white;
            instance.transform.SetParent(parent);
            (instance.transform as RectTransform)!.anchoredPosition = pos;
            instance.transform.localScale = Vector3.one;
            _waitingFadeImages.Add(instance);
            instance.transform.SetAsLastSibling();
        }
        public void Update()
        {
            foreach (var image in _waitingFadeImages)
            {
                var oldColor = image.color;
                oldColor.a -= 0.05f;
                image.color = oldColor;
                if (image.color.a <= 0.01f)
                {
                    _fadeImagePool.Release(image);
                }
            }
            _waitingFadeImages.RemoveAll(x => !x.gameObject.activeSelf);
        }
    }
}