using UnityEngine;
using QFramework;

namespace HKTools
{
    public class HKTips_BaseController : MonoBehaviour, IController
    {
        public IArchitecture GetArchitecture()
        {
            return HK_ArchitectureProvider.Architecture;
        }
    }
}