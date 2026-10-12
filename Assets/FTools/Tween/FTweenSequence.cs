using DG.Tweening;
using System.Collections.Generic;
using FUnityAttribute.EditorAttributes;

namespace FTools.Tween
{
    public class FTweenSequence : FTweenBase
    {
        [FLabel("动画列表（依次执行）")]
        public List<FTweenBase> tweenList = new();

        [FLabel("每个动画之间的间隔")]
        public float interval = 0.0f;

        protected override DG.Tweening.Tween CreateTween()
        {
            // 创建 Sequence
            var sequence = DOTween.Sequence();

            foreach (var t in tweenList)
            {
                if (t == null) continue;

                // 确保内部 Tween 被正确初始化
                var subTween = t.GetOrCreateTween();
                if (subTween == null) continue;

                // 将子 Tween 添加进序列
                sequence.Append(subTween);

                // 如果设置了间隔，就插入间隔
                if (interval > 0)
                    sequence.AppendInterval(interval);
            }

            return sequence;
        }

        protected override void BeforeTweenStart()
        {
            // 禁止子 tween 自动播放，交由 sequence 控制
            foreach (var t in tweenList)
            {
                if (t != null)
                    t.Pause();
            }
        }
    }
}