using UnityEngine;

namespace HKTools
{
    public class HK_DebugConfig : ScriptableObject
    {
        [Header("是否打印时间：")]
        public bool isDebugTime = false;

        [Header("是否存储日志：")]
        public bool isSave = false;

        [Header("是否存储普通Log：")]
        public bool isSaveNormalLog = false;
    }
}