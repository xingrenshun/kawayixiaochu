using System;
using System.Collections.Generic;
using DG.Tweening;
using FUnityCommon;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Effect
{
    public class MultiStepMoveEffect : MonoBehaviour
    {
        private List<Vector3> _pos;
        private List<float> _durations;
        private List<Vector3> _controlPoints;

        private Action _callback;

        private bool _isRunning;
        public void Setup(List<Vector3> pos, List<float> durations=null, List<Vector3> controlPoints=null, bool immediate=true, Action finishCallback=null)
        {
            _callback = finishCallback;
            _pos = pos;
            _durations = durations;
            _controlPoints = controlPoints;

            if (immediate)
            {
                Startup();
            }
        }
        public void Startup()
        {
            if (_isRunning) return;
            _isRunning = true;
            Startup(_pos, 0);
        }


        private void Startup(List<Vector3> targetPos, int index)
        {
            if (targetPos.Count == 0)
            {
                transform.DOScale(0, 0.3f).SetLink(gameObject);
                _callback?.Invoke();
                return;
            };
            var pos = targetPos[0];
            targetPos.RemoveAt(0);

            var duration = GetDuration(index);

            // Check if we have a control point for this segment
            if (_controlPoints != null && index < _controlPoints.Count && _controlPoints[index] != Vector3.zero)
            {
                // Use Bezier curve movement
                var startPos = transform.localPosition;
                var endPos = pos;
                var control = _controlPoints[index];

                DOVirtual.Float(0, 1, duration, t =>
                {
                    var bezierPos = FMath.Bezier(startPos, control, endPos, t);
                    transform.localPosition = bezierPos;
                }).SetLink(gameObject).onComplete = () =>
                {
                    Startup(targetPos, index + 1);
                };
            }
            else
            {
                // Use linear movement (backward compatible)
                transform.DOLocalMove(pos, duration).SetLink(gameObject).onComplete = () =>
                {
                    Startup(targetPos, index + 1);
                };
            }
        }

        private float GetDuration(int index)
        {
            if (_durations == null)
            {
                return 0.2f;
            }

            if (index >= _durations.Count)
            {
                return _durations[0];
            }
            return _durations[index];
        }
        
    }
}