using UnityEngine;
using UnityEditor;
using TMPro;
using HKTools;

namespace EngineTwin.EditorTools
{
    /// <summary>
    /// 修复 Tip 标签文字框自适应问题：让文字框根据内容自动调整大小
    /// </summary>
    public class TipTextAutoSizeFixer : EditorWindow
    {
        [MenuItem("EngineTwin/Tools/修复 Tip 文字框自适应")]
        static void ShowWindow()
        {
            GetWindow<TipTextAutoSizeFixer>("Tip 文字框修复");
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox("修复 Tip 标签的文字框，让边框自动适应文字内容", MessageType.Info);

            EditorGUILayout.Space(10);

            if (GUILayout.Button("🔧 修复场景中所有 Tip", GUILayout.Height(40)))
            {
                FixSceneTips();
            }

            EditorGUILayout.Space(5);

            if (GUILayout.Button("🔧 修复预制体", GUILayout.Height(40)))
            {
                FixPrefab();
            }
        }

        void FixSceneTips()
        {
            var tips = FindObjectsOfType<HK_TipsCtrl>();
            int fixedCount = 0;

            foreach (var tip in tips)
            {
                if (FixTipTextAutoSize(tip))
                {
                    fixedCount++;
                }
            }

            Debug.Log($"[TipTextAutoSizeFixer] 修复了 {fixedCount}/{tips.Length} 个 Tip 标签");
            EditorUtility.DisplayDialog("完成", $"已修复 {fixedCount} 个 Tip 标签的文字框自适应", "确定");
        }

        void FixPrefab()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab EngineTip");
            if (guids.Length == 0)
            {
                EditorUtility.DisplayDialog("未找到", "未找到 EngineTip 预制体", "确定");
                return;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null)
            {
                EditorUtility.DisplayDialog("错误", "无法加载预制体", "确定");
                return;
            }

            var tip = prefab.GetComponent<HK_TipsCtrl>();
            if (tip == null)
            {
                EditorUtility.DisplayDialog("错误", "预制体没有 HK_TipsCtrl 组件", "确定");
                return;
            }

            if (FixTipTextAutoSize(tip))
            {
                EditorUtility.SetDirty(prefab);
                AssetDatabase.SaveAssets();
                Debug.Log($"[TipTextAutoSizeFixer] 已修复预制体: {path}");
                EditorUtility.DisplayDialog("完成", "已修复 EngineTip 预制体", "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("无需修复", "预制体的文字框已经是自适应的", "确定");
            }
        }

        bool FixTipTextAutoSize(HK_TipsCtrl tip)
        {
            bool modified = false;

            // 查找文字组件
            var textComponents = tip.GetComponentsInChildren<TMP_Text>(true);

            foreach (var text in textComponents)
            {
                // 跳过不是标签内容的文字（可能是其他 UI）
                if (!text.name.Contains("Text") && !text.name.Contains("Label") && !text.name.Contains("Title"))
                    continue;

                // 启用自动调整大小
                if (!text.enableAutoSizing)
                {
                    text.enableAutoSizing = true;
                    text.fontSizeMin = 12;
                    text.fontSizeMax = 18;
                    modified = true;
                    Debug.Log($"[TipTextAutoSizeFixer] 启用自动调整: {tip.name}/{text.name}");
                }

                // 设置文字溢出模式为省略号
                if (text.overflowMode != TextOverflowModes.Ellipsis)
                {
                    text.overflowMode = TextOverflowModes.Ellipsis;
                    modified = true;
                }

                // 启用自动换行
                if (!text.enableWordWrapping)
                {
                    text.enableWordWrapping = true;
                    modified = true;
                }

                // 调整 RectTransform 以适应内容
                var rectTransform = text.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    // 设置为自适应内容大小
                    var contentSizeFitter = text.GetComponent<UnityEngine.UI.ContentSizeFitter>();
                    if (contentSizeFitter == null)
                    {
                        contentSizeFitter = text.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
                        contentSizeFitter.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
                        contentSizeFitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
                        modified = true;
                        Debug.Log($"[TipTextAutoSizeFixer] 添加 ContentSizeFitter: {tip.name}/{text.name}");
                    }
                }
            }

            // 查找背景框（Image 组件）
            var images = tip.GetComponentsInChildren<UnityEngine.UI.Image>(true);
            foreach (var image in images)
            {
                if (image.name.Contains("BG") || image.name.Contains("Background") || image.name.Contains("Frame"))
                {
                    // 为背景框添加 ContentSizeFitter
                    var contentSizeFitter = image.GetComponent<UnityEngine.UI.ContentSizeFitter>();
                    if (contentSizeFitter == null)
                    {
                        contentSizeFitter = image.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
                        contentSizeFitter.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
                        contentSizeFitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
                        modified = true;
                        Debug.Log($"[TipTextAutoSizeFixer] 背景框添加 ContentSizeFitter: {tip.name}/{image.name}");
                    }

                    // 添加 HorizontalLayoutGroup 或 VerticalLayoutGroup
                    var layoutGroup = image.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
                    if (layoutGroup == null)
                    {
                        layoutGroup = image.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
                        layoutGroup.childAlignment = TextAnchor.MiddleCenter;
                        layoutGroup.childControlWidth = true;
                        layoutGroup.childControlHeight = true;
                        layoutGroup.childForceExpandWidth = false;
                        layoutGroup.childForceExpandHeight = false;
                        layoutGroup.padding = new RectOffset(10, 10, 5, 5);
                        modified = true;
                        Debug.Log($"[TipTextAutoSizeFixer] 背景框添加 HorizontalLayoutGroup: {tip.name}/{image.name}");
                    }
                }
            }

            return modified;
        }
    }
}
