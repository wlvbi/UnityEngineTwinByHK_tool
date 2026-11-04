using System;
using System.Collections.Generic;
using UnityEngine;

namespace EngineTwin
{
    /// <summary>
    /// 部件位置持久化数据
    /// </summary>
    [Serializable]
    public class PartPositionData
    {
        public string partId;
        public Vector3 customOffset;
        public bool hasCustomPosition;
        public bool isDetached;
    }

    [Serializable]
    public class EngineLayoutData
    {
        public List<PartPositionData> parts = new List<PartPositionData>();
        public string version = "1.0";
    }

    /// <summary>
    /// 部件位置持久化管理器
    /// </summary>
    public static class PartPositionPersistence
    {
        const string SaveFileName = "EngineTwin_PartLayout.json";
        static string SavePath => System.IO.Path.Combine(Application.persistentDataPath, SaveFileName);

        /// <summary>保存所有部件的位置数据</summary>
        public static void Save(EngineAssembly assembly, IReadOnlyList<EnginePart> parts)
        {
            var data = new EngineLayoutData();

            foreach (var part in parts)
            {
                var partData = assembly.GetPartData(part);
                if (partData != null)
                {
                    data.parts.Add(new PartPositionData
                    {
                        partId = part.Id,
                        customOffset = partData.CustomOffset,
                        hasCustomPosition = partData.HasCustomPosition,
                        isDetached = assembly.IsDetached(part)
                    });
                }
            }

            try
            {
                string json = JsonUtility.ToJson(data, true);
                System.IO.File.WriteAllText(SavePath, json);
                Debug.Log($"[持久化] 已保存部件布局到: {SavePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[持久化] 保存失败: {e.Message}");
            }
        }

        /// <summary>加载所有部件的位置数据</summary>
        public static EngineLayoutData Load()
        {
            if (!System.IO.File.Exists(SavePath))
            {
                Debug.Log("[持久化] 未找到保存的布局文件");
                return null;
            }

            try
            {
                string json = System.IO.File.ReadAllText(SavePath);
                var data = JsonUtility.FromJson<EngineLayoutData>(json);
                Debug.Log($"[持久化] 已加载 {data.parts.Count} 个部件的布局数据");
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[持久化] 加载失败: {e.Message}");
                return null;
            }
        }

        /// <summary>应用加载的数据到部件（只恢复自定义拆卸结束位置，不恢复拆卸状态）</summary>
        public static void Apply(EngineLayoutData data, EngineAssembly assembly, IReadOnlyList<EnginePart> parts)
        {
            if (data == null || data.parts == null) return;

            var partDict = new Dictionary<string, PartPositionData>();
            foreach (var partData in data.parts)
            {
                partDict[partData.partId] = partData;
            }

            int restoredCount = 0;
            foreach (var part in parts)
            {
                if (partDict.TryGetValue(part.Id, out var savedData))
                {
                    // 只恢复自定义拆卸结束位置，不恢复拆卸状态
                    if (savedData.hasCustomPosition)
                    {
                        assembly.RestoreCustomPosition(part, savedData.customOffset);
                        restoredCount++;
                    }
                }
            }

            Debug.Log($"[持久化] 已恢复 {restoredCount} 个部件的自定义拆卸位置");
        }

        /// <summary>删除保存的布局数据</summary>
        public static void Clear()
        {
            if (System.IO.File.Exists(SavePath))
            {
                System.IO.File.Delete(SavePath);
                Debug.Log("[持久化] 已清除保存的布局");
            }
        }

        /// <summary>检查是否存在保存的数据</summary>
        public static bool HasSavedData()
        {
            return System.IO.File.Exists(SavePath);
        }
    }
}
