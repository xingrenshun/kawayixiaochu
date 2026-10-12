using System;
using DG.Tweening;
using FUnityAttribute.EditorAttributes;
using UnityEngine;

namespace FTools.Tween
{
    public class FTweenShake : FTweenBase
    {
        [FLabel("抖动类型")]
        public ShakeType shakeType = ShakeType.Position; // 自定义枚举

        private bool IsRotationShake => shakeType == ShakeType.Rotation;

        [FLabel("抖动强度"), FHideIf("IsRotationShake")]
        public Vector3 strength = new Vector3(7f, 0, 0f);

        [FLabel("抖动角度"), FShowIf("IsRotationShake")]
        public float rotateAngle = 25f;
        
        [FLabel("振动次数")]
        public int vibrato = 10;

        [FLabel("随机性")]
        [Range(0f, 180f)]
        public float randomness = 10f;

        [FLabel("淡出抖动")]
        public bool fadeOut = true;

        protected override DG.Tweening.Tween CreateTween()
        {

            DG.Tweening.Tween tween = shakeType switch
            {
                ShakeType.Position => transform.DOShakePosition(duration, strength, vibrato, randomness, fadeOut),
                ShakeType.Rotation => transform.DOShakeRotation(duration,
                    IsRotationShake ? new Vector3(0, 0, rotateAngle) : strength, vibrato, randomness, fadeOut),
                _ => throw new ArgumentOutOfRangeException()
            };




            return tween;
        }
    }

    public enum ShakeType
    {
        
        [FLabel("位移抖动")]
        Position,
        [FLabel("旋转抖动")]
        Rotation,
    }
}