// Tip 相机标记组件：挂到需要驱动 HK_Tips 的相机上，启用时自动向系统层注册。
// 不依赖 MainCamera 标签；多相机场景后启用者覆盖先启用者（切换相机即切 GameObject 激活）。

using QFramework;
using UnityEngine;

namespace HKTools
{
    /// <summary>
    /// Tip 相机标记：挂到驱动 HK_Tips 的相机上，OnEnable 自动注册、OnDisable 自动注销。
    /// 系统层经 HK_TipSystem.HK_TipCamera 读取当前相机；多相机场景后启用者覆盖先启用者。
    /// 必须挂在带 Camera 组件的物体上。
    /// </summary>
    [DisallowMultipleComponent]
    public class HK_TipCamera : HKTips_BaseController
    {
        Camera cachedCamera;

        /// <summary>相机组件（Awake 缓存，避免系统层每次 GetComponent）</summary>
        public Camera Camera => cachedCamera;

        void Awake()
        {
            cachedCamera = GetComponent<Camera>();
        }

        void OnEnable()
        {
            if (cachedCamera == null)
            {
                Debug.LogError($"[HK_Tips] {name} 上的 HK_TipCamera 未找到 Camera 组件，已跳过注册", this);
                return;
            }

            this.GetSystem<IHK_TipSystem>().RegisterTipCamera(this);
        }

        void OnDisable()
        {
            this.GetSystem<IHK_TipSystem>().UnregisterTipCamera(this);
        }
    }
}
