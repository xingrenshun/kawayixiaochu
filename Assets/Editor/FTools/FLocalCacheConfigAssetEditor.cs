using UnityEditor;
using UnityEngine;
using System.Text;
using FTools.Config;
using FTools.Helpers;
using FUnityCommon;

namespace Editor.FTools
{
    [CustomEditor(typeof(FToolsConfigAsset))]
    public class FLocalCacheConfigAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // 显示默认字段
            DrawDefaultInspector();

            GUILayout.Space(10);

            if (!GUILayout.Button("自动生成 AES Key / IV")) return;
            var config = (FToolsConfigAsset)target;

            config.aesKey = AesHelper.GenerateKey32();
            config.aesIv = AesHelper.GenerateIv16();

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }


    }
}
