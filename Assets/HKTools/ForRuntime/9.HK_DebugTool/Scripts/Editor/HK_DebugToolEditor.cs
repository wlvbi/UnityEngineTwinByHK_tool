using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;

// [EngineTwin 兼容补丁] Unity 6000.5 起 Object.GetInstanceID() 为编译错误（CS0619）。
// 本类只是「双击 HKDebug 日志跳转到调用处」的编辑器便利功能，在 6.5+ 上禁用，不影响 HKDebug 日志本身。
#if !UNITY_6000_5_OR_NEWER
namespace HKTools
{
    public class HK_DebugToolEditor
    {
        static HK_DebugToolEditor Instance;
        public static HK_DebugToolEditor GetInstacne()
        {
            if (Instance == null)
            {
                Instance = new HK_DebugToolEditor();
            }
            return Instance;
        }

        const string HKDebugPath = "Assets/HKTools/ForRuntime/9.HK_DebugTool/Scripts/HK_DebugTool.cs";

        int hkDebugInstanceId;
        Type consoleWindowType = null;

        FieldInfo activeTextInfo;
        FieldInfo consoleWindowFileInfo;

        HK_DebugToolEditor()
        {
            UnityEngine.Object debuggerFile = AssetDatabase.LoadAssetAtPath(HKDebugPath, typeof(UnityEngine.Object));
            hkDebugInstanceId = debuggerFile.GetInstanceID();
            consoleWindowType = Type.GetType("UnityEditor.ConsoleWindow,UnityEditor");
            activeTextInfo = consoleWindowType.GetField("m_ActiveText", BindingFlags.Instance | BindingFlags.NonPublic);
            consoleWindowFileInfo = consoleWindowType.GetField("ms_ConsoleWindow", BindingFlags.Static | BindingFlags.NonPublic);
        }

        [UnityEditor.Callbacks.OnOpenAsset(-1)]
        static bool OnOpenAsset(int instanceID, int line)
        {
            if (instanceID == GetInstacne().hkDebugInstanceId)
            {
                return GetInstacne().FindCode();
            }
            return false;
        }

        bool FindCode()
        {
            var windowInstance = consoleWindowFileInfo.GetValue(null);
            var activeText = activeTextInfo.GetValue(windowInstance);
            string[] contentStrings = activeText.ToString().Split('\n');
            List<string> filePath = new List<string>();

            for (int i = 0; i < contentStrings.Length; i++)
            {
                if (contentStrings[i].Contains("at"))
                {
                    filePath.Add(contentStrings[i]);
                }
            }

            bool success = PingAndOpen(filePath[1]);
            return success;
        }

        bool PingAndOpen(string fileContext)
        {
            string regexRule = @"at ([\w\W]*):(\d+)\)";
            Match match = Regex.Match(fileContext, regexRule);

            if (match.Groups.Count > 1)
            {
                string path = match.Groups[1].Value;
                string line = match.Groups[2].Value;
                UnityEngine.Object codeObject = AssetDatabase.LoadAssetAtPath(path, typeof(UnityEngine.Object));
                if (codeObject == null)
                {
                    return false;
                }
                EditorGUIUtility.PingObject(codeObject);
                AssetDatabase.OpenAsset(codeObject, int.Parse(line));
                return true;
            }
            return false;
        }
    }
}
#endif