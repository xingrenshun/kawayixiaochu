using UnityEngine;

public static class RectTransformStretchUtil
{
    public static void SetStretchSize(
        RectTransform rt,
        float? width = null,
        float? height = null,
        bool center = true
    )
    {
        if (rt == null) return;
        if (rt.parent is not RectTransform parent) return;

        
        
        var offsetMin = rt.offsetMin;
        var offsetMax = rt.offsetMax;

        if (width is > 0)
        {
            var parentWidth = parent.rect.width;
            var delta = parentWidth - width.Value;

            if (center)
            {
                float half = delta * 0.5f;
                offsetMin.x = half;
                offsetMax.x = -half;
            }
            else
            {
                offsetMin.x = 0;
                offsetMax.x = -delta;
            }
        }

        if (height is > 0)
        {
            var parentHeight = parent.rect.height;
            var delta = parentHeight - height.Value;

            if (center)
            {
                var half = delta * 0.5f;
                offsetMin.y = half;
                offsetMax.y = -half;
            }
            else
            {
                offsetMin.y = 0;
                offsetMax.y = -delta;
            }
        }

        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }
}