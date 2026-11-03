using QFramework;

namespace HKTools
{
    /// <summary>
    /// HKTools 框架提供工具
    /// </summary>
    public static class HK_ArchitectureProvider
    {
        static IArchitecture architecture;

        /// <summary>  
        /// 获取当前项目的架构实例，确保在第一次访问时进行初始化。  
        /// </summary>  
        public static IArchitecture Architecture
        {
            get
            {
                // 懒加载架构实例  
                if (architecture == null)
                {
                    SetArchitecture();
                }
                return architecture;
            }
        }

        /// <summary>  
        /// 设置当前项目的架构实例。  
        /// </summary>  
        static void SetArchitecture()
        {
            // 修改此处，设定为当前项目 Architecture 框架
            // [EngineTwin] 按 HKTools README「初始设置」要求，切换为本项目的架构
            architecture = EngineTwin.EngineTwinArchitecture.Interface;
        }
    }
}