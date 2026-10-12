//using System;
//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;

//public class Btn_Ad : MonoBehaviour
//{
//    // 可配置：体力奖励与上限
//    [SerializeField] private int rewardFlashAmount = 20;
//    [SerializeField] private int maxFlash = 100;

//    // 缓存引用，避免频繁查找
//    private ManagerMecanique mecCache;
//    private ManagerAds adsCache;

//    private void Awake()
//    {
//        // 尝试缓存，若场景加载顺序导致未找到，点击时会再次查找
//        mecCache = FindObjectOfType<ManagerMecanique>();
//        adsCache = FindObjectOfType<ManagerAds>();
//    }

//    private ManagerMecanique GetMecanique()
//    {
//        if (mecCache == null)
//        {
//            mecCache = FindObjectOfType<ManagerMecanique>();
//        }
//        return mecCache;
//    }

//    private ManagerAds GetManagerAds()
//    {
//        if (adsCache == null)
//        {
//            adsCache = FindObjectOfType<ManagerAds>();
//        }
//        return adsCache;
//    }

//    public void Btn_AdFlashOnClick()
//    {

//        AdManager.Instance.ShowRewardedVideoAd(onSuccess: () =>
//        {
//            Debug.Log("广告调用成功 ");
//            var mec = GetMecanique();
//            if (mec != null)
//            {
//                mec.FlashInt += rewardFlashAmount;
//                // 上限控制
//                if (mec.FlashInt > maxFlash) mec.FlashInt = maxFlash;
//                PlayerPrefs.SetInt("flash", mec.FlashInt);
//                // 立即刷新显示
//                if (mec.Flash != null)
//                {
//                    mec.Flash.text = mec.FlashInt + "/" + maxFlash;

//                }
//            }


//        }, onFail: (error) =>
//        {
//            Debug.LogError($"广告播放失败，无法使用技能: {error}");
//        });

       
//    }

   
//}
