using HKTools;
using QFramework;
using UnityEngine;

namespace EngineTwin
{
    /// <summary>
    /// 发动机数字孪生的项目架构（QFramework Architecture）。
    /// HKTools 的 HK_ArchitectureProvider 已指向本架构，因此框架里的 HK_Tips 等模块
    /// 与本项目共享同一套 System / Model / Command / Event。
    /// </summary>
    public class EngineTwinArchitecture : Architecture<EngineTwinArchitecture>
    {
        protected override void Init()
        {
            // ---------- HKTools 框架系统 ----------
            // 悬浮提示标签系统（部件提示）
            this.RegisterSystem<IHK_TipSystem>(new HK_TipSystem());

            // ---------- 本项目 ----------
            this.RegisterModel<IEngineTelemetryModel>(new EngineTelemetryModel());
            this.RegisterModel<IEnginePartModel>(new EnginePartModel());
            this.RegisterSystem<IEngineSimulationSystem>(new EngineSimulationSystem());
        }

        // 关闭「进入播放模式时重新加载域」时，静态单例会跨次运行残留（持有已销毁的 Tip 等），这里每次运行前清空
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticState()
        {
            mArchitecture = null;
        }
    }

    /// <summary>本项目所有 MonoBehaviour 控制器的基类：接入 EngineTwinArchitecture</summary>
    public abstract class EngineTwinController : MonoBehaviour, IController
    {
        public IArchitecture GetArchitecture() => EngineTwinArchitecture.Interface;
    }
}
