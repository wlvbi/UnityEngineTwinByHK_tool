/*************************************************************************************
 * 工    具：  HKDebug
 
 * 描    述：  HKDebug 对 Unity 内置 Debug 功能的扩展和封装，提供更灵活、强大的日志管理方案。
 *            通过使用 HKDebug，可以轻松控制和优化日志输出，提升调试效率和代码可维护性，主要功能如下：
 *                   
 * 版    本：  V1.1
 * 作    者：  谭潇杰、京产肠饭
 * 
 * 创    建：  2024/01/28  V1.0
 * 
 * 更    新：  2024/11/17  V1.1
 *            1.功能新增：新增 控制日志打印是否包含当前时间的功能。
 *            2.功能新增：新增 HKDebug配置 窗口菜单
 *            3.代码优化：优化 HKDebugTool 代码，完善注释
 * 
 * 链    接：  https://gitee.com/JingChanChangFan/hk_-unity-tools
*************************************************************************************/

using System;

#if UNITY_EDITOR || UNITY_STANDALONE_WIN
using System.IO;
#endif

using UnityEngine;

namespace HKTools
{
    // 定义日志颜色枚举
    public enum LogColor
    {
        White,
        Red,
        Yellow,
        Green,
        Blue,
        Cyan
    }

    // 定义日志样式枚举
    public enum LogStyle
    {
        Normal,
        Bold,
        Italic,
        BoldAndItalic
    }

    public class HKDebug
    {
        // 是否显示日志时间
        static bool isDebugTime = false;

#if UNITY_EDITOR || UNITY_STANDALONE_WIN
        // 是否保存日志到文件
        static bool isSave = false;
        // 是否保存普通日志
        static bool isSaveNormalLog = false;

        // 日志存放目录
        static string logSaveDir = Application.dataPath.Replace("Assets", "") + "HKLog";
        static string logSaveName = "";
#endif

        // 初始化方法，在场景加载前执行
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            // 加载配置文件
            HK_DebugConfig config = Resources.Load<HK_DebugConfig>("HKDebugConfig");

            // 检查配置文件是否存在，如果不存在则保持默认配置
            if (config != null)
            {
                isDebugTime = config.isDebugTime;
            }

#if UNITY_EDITOR || UNITY_STANDALONE_WIN

            // 检查配置文件是否存在，如果不存在则保持默认配置
            if (config != null)
            {
                isSave = config.isSave;
                isSaveNormalLog = config.isSaveNormalLog;
            }

            // 监听Unity的日志消息
            Application.logMessageReceived += (string message, string stackTrace, LogType type) =>
            {
                // 过滤特定类型的日志
                if (type == LogType.Log || type == LogType.Warning || type == LogType.Assert || message.StartsWith("<b>"))
                    return;

                var str = "[Error]" + GetLogTime() + message + "\n" + stackTrace;

                SaveToFile(str);
            };
#endif
        }

        /// <summary>
        /// HKDebug的普通日志方法，支持颜色和样式
        /// </summary>
        public static void Log(string message, LogColor color = LogColor.White, LogStyle style = LogStyle.Normal)
        {
            string prefix = string.Empty;
            string suffix = string.Empty;

            // 根据样式添加前缀
            switch (style)
            {
                case LogStyle.Bold:
                    prefix += "<b>";
                    break;
                case LogStyle.Italic:
                    prefix += "<i>";
                    break;
                case LogStyle.BoldAndItalic:
                    prefix += "<b><i>";
                    break;
            }

            // 根据颜色添加前缀和后缀
            switch (color)
            {
                case LogColor.Yellow:
                    prefix += "<color=#f19927>";
                    suffix += "</color>";
                    break;
                case LogColor.Red:
                    prefix += "<color=#e12828>";
                    suffix += "</color>";
                    break;
                case LogColor.Green:
                    prefix += "<color=green>";
                    suffix += "</color>";
                    break;
                case LogColor.Blue:
                    prefix += "<color=#0089FF>";
                    suffix += "</color>";
                    break;
                case LogColor.Cyan:
                    prefix += "<color=#23d2d2>";
                    suffix += "</color>";
                    break;
            }

            // 根据样式添加后缀
            switch (style)
            {
                case LogStyle.Normal:
                    break;
                case LogStyle.Bold:
                    suffix += "</b>";
                    break;
                case LogStyle.Italic:
                    suffix += "</i>";
                    break;
                case LogStyle.BoldAndItalic:
                    suffix += "</i></b>";
                    break;
            }

            // 使用Unity的Debug.Log输出带格式的日志
            Debug.Log("[Info] " + GetLogTime() + prefix + message + suffix);

            SaveToFile("[Info] " + GetLogTime() + message);
        }

        /// <summary>
        /// HKDebug的警告日志方法，支持颜色和样式
        /// </summary>
        public static void LogWarning(string message)
        {
            string prefix = string.Empty;
            string suffix = string.Empty;

            prefix += "<b><color=#FFD400>";
            suffix += "</color></b>";

            Debug.LogWarning(prefix + "[Warn] " + GetLogTime() + message + suffix);

            SaveToFile("[Warn] " + GetLogTime() + message, true);
        }

        /// <summary>
        /// HKDebug的错误日志方法，支持颜色和样式
        /// </summary>
        public static void LogError(string message)
        {
            string prefix = string.Empty;
            string suffix = string.Empty;

            prefix += "<b><color=red>";
            suffix += "</color></b>";

            Debug.LogError(prefix + "[Error] " + GetLogTime() + message + suffix);

            SaveToFile("[Error] " + GetLogTime() + message, true);
        }

        /// <summary>
        /// 获取日志时间字符串
        /// </summary>
        static string GetLogTime()
        {
            if (!isDebugTime)
                return "";

            return $"[{DateTime.Now.ToString("HH:mm:ss.fff")}]: ";
        }

#if UNITY_EDITOR || UNITY_STANDALONE_WIN
        // StreamWriter用于写入日志文件  
        static StreamWriter streamWriter = null;
#endif


        /// <summary>
        /// 将日志写入文件
        /// </summary>
        static void SaveToFile(string message, bool EnableStack = false)
        {
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
            if (!isSave)
                return;

            if (message.StartsWith("[Info]") && !isSaveNormalLog)
                return;

            if (streamWriter == null)
            {
                // 设置日志文件名和路径
                logSaveName = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss") + ".log";
                string path = Path.Combine(logSaveDir, logSaveName);

                try
                {
                    // 创建日志目录
                    if (!Directory.Exists(logSaveDir))
                    {
                        Directory.CreateDirectory(logSaveDir);
                    }

                    // 初始化StreamWriter
                    streamWriter = File.AppendText(path);
                    streamWriter.AutoFlush = true;

                    // 写入系统和设备信息
                    streamWriter.WriteLine("*********************************************************************************************************");
                    streamWriter.WriteLine("By " + SystemInfo.deviceName);
                    DateTime now = DateTime.Now;
                    streamWriter.WriteLine(string.Concat(new object[] { now.Year.ToString(), "年", now.Month.ToString(), "月", now.Day, "日  ", now.Hour.ToString(), ":", now.Minute.ToString(), ":", now.Second.ToString() }));
                    streamWriter.WriteLine();
                    streamWriter.WriteLine("操作系统:  " + SystemInfo.operatingSystem);
                    streamWriter.WriteLine("系统内存大小:  " + SystemInfo.systemMemorySize);
                    streamWriter.WriteLine("设备模型:  " + SystemInfo.deviceModel);
                    streamWriter.WriteLine("处理器类型:  " + SystemInfo.processorType);
                    streamWriter.WriteLine("显卡名称:  " + SystemInfo.graphicsDeviceName);
                    streamWriter.WriteLine("显存大小:  " + SystemInfo.graphicsMemorySize);
                    streamWriter.WriteLine("*********************************************************************************************************");
                    streamWriter.WriteLine();
                }
                catch
                {
                    // 如果出现异常，重置streamWriter
                    streamWriter = null;
                    return;
                }
            }

            if (streamWriter != null)
            {
                try
                {
                    // 写入日志消息
                    streamWriter.WriteLine(message);

                    if (EnableStack)
                    {
                        // 提取堆栈信息并写入
                        var st = StackTraceUtility.ExtractStackTrace();
#if UNITY_EDITOR
                        for (int i = 0; i < 3; i++)
#else
                        for (int i = 0; i < 2; i++)
#endif
                        {
                            st = st.Remove(0, st.IndexOf('\n') + 1);
                        }
                        streamWriter.WriteLine(st);
                    }
                }
                catch (Exception)
                {
                    // 忽略写入异常
                }
            }
#endif
        }


#if UNITY_EDITOR || UNITY_STANDALONE_WIN
        // 确保StreamWriter在应用退出时正确关闭  
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void OnApplicationQuit()
        {
            if (streamWriter != null)
            {
                streamWriter.Close();
                streamWriter = null;
            }
        }
#endif
    }
}