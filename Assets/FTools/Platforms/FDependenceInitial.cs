using System;
using UnityEngine;

namespace FTools.Platforms
{
    public class FDependenceInitial : MonoBehaviour
    {
        private void Awake()
        {
            FPlatformCore.Instance.InitSdk();
        }
    }
}