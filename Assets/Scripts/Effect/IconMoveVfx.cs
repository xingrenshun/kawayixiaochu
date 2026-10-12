using System;
using System.Collections;
using System.Collections.Generic;
using FTools.Helpers;
using FUnityCommon;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Effect
{
    public static class IconMoveVfx
    {
        public static IEnumerator StartIconCircleMove(GameObject prefab, Transform target, int count=10,
            Action preCompleteCallback = null, Action completeCallback = null, Transform itemSpawnParent = null)
        {
            var spawnLocalPosition = prefab.transform.localPosition;
            var spawnParent = itemSpawnParent ?? prefab.transform.parent;
            var cnt = 0;
            const float radius = 130f;
            while (cnt < count)
            {
                var currentLoop = Mathf.Min(count - cnt, Random.Range(1, 3));
                for (var i = 0; i < currentLoop; i++)
                {
                    var ins = SpawnParticleItem(prefab, spawnParent, spawnLocalPosition);
                    ins.transform.localPosition = Vector3.zero;
                    ins.GetComponentInChildren<TMP_Text>().gameObject.SetActive(false);
                    ins.transform.localScale = Vector3.one;
                    ins.GetComponent<LayoutElement>().ignoreLayout = true;
                    var comp = ins.GetComponent<MultiStepMoveEffect>();
                    var randomAngle = Random.Range(0, 360);
                    var radians = Mathf.Deg2Rad * randomAngle;
                    var spawnRandomX = Mathf.Cos(radians) * radius;
                    var spawnRandomY = Mathf.Sin(radians) * radius;
                    var originalPos = ins.transform.localPosition;
                    var pos = new List<Vector3>
                    {
                        new(spawnRandomX + originalPos.x, spawnRandomY + originalPos.y, 0),
                        ins.transform.parent.InverseTransformPoint(target.position)
                    };
                    comp.Setup(pos, new List<float> { 0.3f, 0.5f }, null, true, () =>
                    {
                        UnityEngine.Object.Destroy(comp.gameObject);
                        preCompleteCallback?.Invoke();
                    });
                }

                cnt += currentLoop;
                yield return new WaitForSeconds(0.05f);
            }

            FAsync.Instance.InvokeDelay(() => { completeCallback?.Invoke(); }, 0.8f);
        }

        private static GameObject SpawnParticleItem(GameObject prefab, Transform parent, Vector3 pos)
        {
            var ins = UnityEngine.Object.Instantiate(prefab, parent);
            ins.transform.localPosition = pos;
            ins.SetActive(true);
            return ins;
        }
    }
}