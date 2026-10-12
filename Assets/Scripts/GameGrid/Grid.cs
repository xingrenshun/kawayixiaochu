using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Algorithm;
using DG.Tweening;
using Effect;
using FTools.Helpers;
using FTools.Tween;
using FUnityCommon;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using View;

namespace GameGrid
{
    public class Grid
    {
        public GameObject obj;

        private readonly Sprite _icon;
        public Sprite Icon => _icon;
        private int _iconId;
        public int IconId => _iconId;
        private RectTransform _rectTransform;
        public RectTransform Rt => _rectTransform;
        private readonly int _id;
        public int Id => _id;
        public int x, y;

        private const float SPEED = 1400f;


        private FTweenRotation _rotationTween;
        private FTweenScale _scaleTween;

        // 是否正在等待被合并
        private bool _isReceiving;
        private bool _isMoving;
        public bool IsBusy => _isMoving || _isReceiving;
        public bool IsAlive => obj != null && _rectTransform != null;

        private Image _image;
        private Image _renderImage;


        private Button _btn;

        // Dev Only
        private TMP_Text _levelText;

        public Grid(Sprite icon, GameObject obj, int id, int x, int y)
        {
            this.obj = obj;
            this._id = id;
            this.x = x;
            this.y = y;
            _icon = icon;
            _iconId = int.Parse(_icon.name.Split("-")[^1]);
            Setup();
        }

        public override string ToString()
        {
            return $"Grid({x},{y})";
        }


        public void OnClicked()
        {
            if (_isMoving || _isReceiving) return;
            FAudioCore.Instance.PlaySfx("clicked");
            var view = AutoLayoutGameView.Instance;
            if (view.gameIsEnded) return;
            // 检查输入是否被锁定（旋转时禁止操作）
            if (view.IsInputLocked()) return;
            if (view.clickedGridStack.Count == 0)
            {
                OnSelected();
            }
            else
            {
            
                // 寻找最短路径
                var firstItem = view.clickedGridStack.Pop();
                if (firstItem == this)
                {
                    ResetState();
                    return;
                }
                if (!CanMoveTo(firstItem))
                {
                    // 因为类型的问题导致无法移动
                    view.clickedGridStack.Clear();
                    firstItem.ResetState();
                    OnSelected();
                    return;
                }

                var runtime = Runtime.Instance;
                var targetGrid = view.GetGridState(x, y);
                var (success, path) = ShortestPath.FindPath(view.GetGridState(firstItem.x, firstItem.y),targetGrid, view.GridStates);
                if (success)
                {
                    foreach (var p in path)
                    {
                        view.BackgroundGrids[p.x, p.y].FrontImage.color = runtime.pathColor;
                    }
                    view.BackgroundGrids[path[0].x, path[0].y].FrontImage.color = runtime.gridOriginalColor;
                    FAsync.Instance.StartRoutine(firstItem.MoveToTarget(path, this));
                }
                else
                {
                    firstItem.ResetState();
                    FAsync.Instance.InvokeRepeatCount(loopCount =>
                    {
                        foreach (var img in path.Select(p => view.BackgroundGrids[p.x, p.y]))
                        {
                            // var c = img.FrontColor();
                            // img.FrontImage.color = c == runtime.gridOriginalColor ? runtime.errorColor : runtime.gridOriginalColor;
                            img.UpdateFrontImageColor((loopCount + 1)% 2 == 0 ? runtime.gridOriginalColor : runtime.errorColor);
                        }
                    }, 0f, 0.15f, 6);
                    FAudioCore.Instance.PlaySfx("error");
                    FDeviceCore.Vibrate();
                }
                view.clickedGridStack.Clear();
            }
        }

        private void OnSelected()
        {
            _rotationTween.Play();
            _scaleTween.Replay();
            AutoLayoutGameView.Instance.clickedGridStack.Push(this);
        }
        public void ResetState()
        {
            if (obj == null) return;
            _scaleTween.DoSmoothRewind();
            _rotationTween.Pause();
            obj.transform.rotation = Quaternion.identity;
        }

        private bool CanMoveTo(Grid target)
        {
            var isSame =  target._id == _id;
            var targetIsReceiving = target._isReceiving;
            return isSame && !targetIsReceiving;
            // return true;
        }
        public IEnumerator MoveToTarget(List<GridState> path, Grid target, bool moveOnly=false)
        {
            ResetState();
            // 说明是原地打乱
            if (path.Count == 0)
            {
                yield break;
            };
            _isMoving = true;
            if (!moveOnly && target != null)
            {
                target._isReceiving = true;
            }

            path.RemoveAt(0);
            var view = AutoLayoutGameView.Instance;
            // move only 的时候外部已经将状态重置了，这里做判断是为了避免覆盖了其他路径
            if (!moveOnly) view.UpdateGridState(x, y, EGridState.Empty);
            yield return MoveAlong(path, SPEED, moveOnly);
            _isMoving = false;
            if (!moveOnly && target != null)
            {
                Combine(target);
            }
        } 

        private void Combine(Grid target)
        {
            var view = AutoLayoutGameView.Instance;
            FDeviceCore.Vibrate();
            FAudioCore.Instance.PlaySfx("finished-02");

            #region Play Particle

            // var prefab = AutoLayoutGameView.Instance.iconCombineBurst;
            // var p = Object.Instantiate(prefab, _rectTransform.parent).GetComponent<ParticleSystem>();
            // p.transform.position = _rectTransform.position;
            // p.transform.localScale = Vector3.one;
            // p.textureSheetAnimation.SetSprite(0, _icon);
            //
            // p.Play();

            #endregion
        
            target._isReceiving = false;
            var seq = DOTween.Sequence();
            seq.SetLink(target.obj); 
            seq.Append(target.obj.transform.DOScale(Vector3.one * 2f, 0.15f));
            seq.Append(target.obj.transform.DOScale(Vector3.one, 0.15f));
            seq.Play();
            view.RemoveGameGrid(this);
            Runtime.Instance.AddOrUpdateCollectionItem(_iconId);
            FAsync.Instance!.StartRoutine(SpawnParticleToScore(target._rectTransform, target._icon));
            
            var tx = target.x;
            var ty = target.y;
            CheckNearIsSame(tx, ty);
            // 动画完成后执行
            seq.OnComplete(() =>
            {
                view.RemoveGameGrid(target, true);
                Runtime.Instance.AddOrUpdateCollectionItem(_iconId); 
                // 检查场景上是否都是单的，如果是，那么直接给他消除掉
                if (view.IsAllSingle())
                {
                    FAsync.Instance.StartRoutine(view.KillAllSingle());
                }
            });
        }

        private IEnumerator SpawnParticleToScore(Transform target, Sprite icon, int count = 10)
        {
            const float radius = 130f;
            var view = AutoLayoutGameView.Instance;
            var prefab = view.multiStepParticlePrefab;
            var targetTransform = view.scoreText.transform;
            var spawnLocalPosition = target.localPosition;
            var textScaleTween = view.scoreText.transform.GetComponent<FTweenScale>();
            var spawnParent = target.parent;
            // var targetPos = new Vector3();


            var perItemScore = Runtime.Instance.perGridItemScore / count;
            var cnt = 0;
            while (cnt < count)
            {
                var currentLoop = Mathf.Min(count - cnt, Random.Range(1, 3));
                for (var i = 0; i < currentLoop; i++)
                {
                    var ins = SpawnParticleItem(prefab, spawnParent, spawnLocalPosition, icon);
                    var comp = ins.GetComponent<MultiStepMoveEffect>();
                    var randomAngle = Random.Range(0, 360);
                    var radians = Mathf.Deg2Rad * randomAngle;
                    var spawnRandomX = Mathf.Cos(radians) * radius;
                    var spawnRandomY = Mathf.Sin(radians) * radius;
                    var originalPos = ins.transform.localPosition;

                    var burstPos = new Vector3(spawnRandomX + originalPos.x, spawnRandomY + originalPos.y, 0);
                    var targetPos = ins.transform.parent.InverseTransformPoint(targetTransform.position);

                    // Calculate Bezier control point for the second segment
                    var ab = targetPos - burstPos;
                    var mid = (burstPos + targetPos) * 0.5f;
                    var dir = Vector3.Cross(ab, Vector3.forward).normalized;
                    // Random offset to make each particle's curve unique
                    var randomOffset = Random.Range(200f, 400f);
                    var randomSide = Random.value > 0.5f ? 1f : -1f; // Random curve direction
                    var control = mid + dir * randomOffset * randomSide;

                    var pos = new List<Vector3> { burstPos, targetPos };
                    var controlPoints = new List<Vector3> { Vector3.zero, control }; // First segment linear, second segment Bezier

                    comp.Setup(pos, new List<float> {0.3f, 0.5f}, controlPoints, true, () =>
                    {
                        if (textScaleTween.IsRunning) textScaleTween.Pause();
                        textScaleTween.Replay();
                        FAudioCore.Instance.PlaySfx("on-score");
                        view.AddScore(perItemScore * Runtime.Instance.combo);
                        Object.Destroy(comp.gameObject);
                    });
                }
                cnt += currentLoop;
                yield return new WaitForSeconds(0.05f);
            }
            // for (var i = 0; i < count; i++)
            // {
            //   
            // }
        }

        private static GameObject SpawnParticleItem(GameObject prefab, Transform parent, Vector3 pos, Sprite icon)
        {
            var ins = Object.Instantiate(prefab, parent);
            ins.transform.localPosition = pos;
            ins.SetActive(true);
            var image = ins.GetComponent<Image>();
            image.sprite = icon;
            Runtime.Instance.levelParticles.Add(ins);
            return ins;
        }


        private IEnumerator MoveAlong(List<GridState> points, float speed, bool moveOnly)
        {
            var rect = _rectTransform;
            var view = AutoLayoutGameView.Instance;
            var runtime = Runtime.Instance;
            if (points.Count == 0) yield break;

            // float threshold = 0.1f;

            foreach (var g in points)
            {
                var start = rect.anchoredPosition;
                var target = new Vector2(
                    view.GetXPosition(g.x),
                    view.GetYPosition(g.y)
                );
                var distance = Vector2.Distance(start, target);
                var duration = Mathf.Max(distance / speed - distance * 0.5f, 0.05f);
 
                var t = 0f;

                // var runtime = Runtime.Instance;
                const float shadowCooldown = 0.25f;
                var shadowTick = 0f;
                while (t < duration)
                {
                    shadowTick -= Time.deltaTime;
                    t += Time.deltaTime;
                    var ratio = t / duration;
                    rect.anchoredPosition = Vector2.Lerp(start, target, ratio);

                    if (shadowTick <= 0f )
                    {
                        view.imageShadowFade.AddImageToFadeList(obj.transform.parent, rect.anchoredPosition, _icon);
                        shadowTick = shadowCooldown;
                    }
                    yield return null;
                }

                // view.BackgroundGrids[g.x, g.y].FrontImage.color = runtime.gridOriginalColor;
                view.BackgroundGrids[g.x, g.y].UpdateFrontImageColor(runtime.gridOriginalColor);

                rect.anchoredPosition = target;
            }
        }


        private void CheckNearIsSame(int checkX, int checkY)
        {
            var view = AutoLayoutGameView.Instance;
            
            // 定义四个方向：上、下、左、右
            int[] dx = { 0, 0, -1, 1 };
            int[] dy = { 1, -1, 0, 0 };


            
            for (var i = 0; i < 4; i++)
            {
                var nx = checkX + dx[i];
                var ny = checkY + dy[i];

                if (!view.IsValidCoordinate(nx, ny)) continue;

                var neighborGrid = view.GetGridInstance(nx, ny);

                if (neighborGrid == null) continue;
                if (neighborGrid.obj == null || !neighborGrid.obj.activeSelf) continue;

                if (neighborGrid._iconId == _iconId && 
                    !neighborGrid._isReceiving && 
                    !neighborGrid._isMoving)
                {
                    neighborGrid.ChainEliminate();
                }
            }
        }

        public void DoKill()
        {
            _rectTransform.DOScale(2f, 0.35f).OnComplete(() =>
            {
                FAsync.Instance.StartRoutine(SpawnParticleToScore(_rectTransform, _icon, 6));
                _rectTransform.DOScale(0f, 0.25f).OnComplete(() =>
                {
                    AutoLayoutGameView.Instance.RemoveGameGrid(this, true);
                    Runtime.Instance.AddOrUpdateCollectionItem(_iconId);
                });
            });
        }
        private void ChainEliminate()
        {
            if (_isReceiving) return;
            if (obj == null) return;
    
            _isReceiving = true;

            var view = AutoLayoutGameView.Instance;


            view.UpdateGridState(x, y, EGridState.Empty); 
    
            FAudioCore.Instance.PlaySfx("finished-02");
            Runtime.Instance.combo++;
            FAsync.Instance.StartRoutine(SpawnParticleToScore(_rectTransform, _icon, 6));

            var seq = DOTween.Sequence();
            seq.SetLink(obj); 
    
            seq.Append(obj.transform.DOScale(Vector3.one * 1.3f, 0.1f));
            seq.Append(obj.transform.DOScale(Vector3.zero, 0.15f));
            var currentX = x;
            var currentY = y;
            CheckNearIsSame(currentX, currentY);
            seq.OnComplete(() =>
            {
                view.RemoveGameGrid(this, true); 
                Runtime.Instance.AddOrUpdateCollectionItem(_iconId);
                if (view.IsAllSingle())
                {
                    FAsync.Instance.StartRoutine(view.KillAllSingle());
                }
            });
        }


        private void Setup()
        {
            _image = obj.GetComponent<Image>();
            _renderImage = obj.transform.GetChild(0).GetComponent<Image>();
            _renderImage.sprite = _icon;
            obj.SetActive(true);
            _rectTransform = obj.GetComponent<RectTransform>();
            _btn = obj.GetComponent<Button>();
            _btn.onClick.AddListener(OnClicked);
            _rotationTween = obj.GetComponent<FTweenRotation>();
            _scaleTween = obj.GetComponent<FTweenScale>();

            obj.transform.DOScale(1.3f, Random.Range(0.15f, 0.35f)).SetEase(Ease.InCirc).OnComplete(() =>
            {
                obj.transform.DOScale(1f, 0.2f).SetEase(Ease.InCirc);
            });
            
            // _image.color = Runtime.Instance.colors[this.id];
        }

        public void SetPosition(Vector2 position)
        {
            if (!IsAlive) return;
            _rectTransform.anchoredPosition = position;
        }
        public void SetGirdSize(Vector2 size)
        {
            if (!IsAlive) return;
            _rectTransform.sizeDelta = size;
        }

        public void Highlight()
        {
            _rotationTween.Play();
            _scaleTween.Replay();
        }
    }
}
