/*************************************************************************************
 * 工    具：  HK_TipSystem
 * 
 * 描    述：  HK_Tips 是一套三维场景中悬浮提示标签工具，可快速摆放编辑，运行时支持点击事件响应，主要功能如下：
 *            本工具是三维场景中，进行悬浮提示标签的便捷工具；
 *            可在场景中快速摆放编辑，运行时可通过点击的方式进行事件响应；
 * 
 * 版    本：  V1.2
 * 作    者：  京产肠饭
 * 
 * 创    建：  2024/03/28  V1.0
 *
 * 更    新：  2025/07/01  V1.1
 *            1.代码优化：减少冗余代码，提高代码可读性，提高运行时性能
 *            2.Bug修复：修复 OnValidate 编辑器下无法正确设置碰撞体激活/禁用的Bug
 *            3.功能新增：新增可根据距离动态调整LineRenderer线条粗细的功能
 *
 *            2026/10/26  V1.2
 *            1.Bug修复：初始化改三步校验（失败打日志，修好结构后节流自愈、不用重载场景）；
 *            距离显隐改用标签归位位置；批量显隐补失效引用防护；点击不再吞场景加载后的首点
 *            2.功能新增：HK_TipCamera 相机注册表（挂相机即注册，多相机切换、销毁自愈）；
 *            淡入淡出；曲线连线（锚点/控制点交替，弧度滑条可调）；指令级生长动画
 *            （ShowWithAnim/HideWithAnim，从原点生长、缩回消失，默认开）；SetText
 *            3.接口调整：点击事件仅携带 Tip 组件引用（_tip），tipName 配置项移除
 *            （需要名称时用物体名）；移除「手动收集」指令（Awake 自动注册、失败自愈）；
 *            指令与事件统一 TIP 前缀（CMD_TIP_Xxx / Event_TIP_OnClick）
 *            4.代码优化：移除冗余的 HK_TipTitleTarget，「旋转覆盖」字段改为「是否朝向相机」开关；
 *            四个组件加 DisallowMultipleComponent；SetAllTipsState 更名 SetAllTipsClickable
 *            5.性能优化：显隐与缩放共用一次距离计算；相机/自身/标签静止且缩放收敛时跳过每帧更新；
 *            Inspector 结构校验缓存化（选择/层级变化才重算）
 *            6.易用性：Inspector 卡片化分组（功能开关卡+实时校验+中文 Tooltip+品牌青标题栏）；
 *            运行时改参数即时生效；弧度/线宽实时预览；收集范围放宽到所有已加载场景
 *            7.体验修复：距离淡入淡出与指令显隐解耦（alpha 归距离独占、激活归指令独占）；
 *            修「隐藏→超距→显示→回距不显示」、恢复态空线、超距隐藏后近距显示先大后小、
 *            无相机时 Show 后隐形卡死
 * 
 * 链    接：  https://gitee.com/JingChanChangFan/hk_-unity-tools
*************************************************************************************/

using QFramework;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HKTools
{
    /// <summary>Tip 分组（按组批量控制用；开发时按需拓展枚举值即可，面板直接显示枚举名）</summary>
    public enum HK_TipGroup
    {
        TipGroup01,
        TipGroup02,
        TipGroup03,
        TipGroup04,
    }

    public interface IHK_TipSystem : ISystem
    {
        List<HK_TipsCtrl> TipsList { get; }

        /// <summary>
        /// 当前 Tip 相机（由场景中挂 HK_TipCamera 的相机注册而来，无注册时返回 null）
        /// </summary>
        Camera HK_TipCamera { get; }

        /// <summary>
        /// HK_TipCamera 组件注册用（OnEnable 时调用，业务勿手动调）
        /// </summary>
        void RegisterTipCamera(HK_TipCamera tipCamera);

        /// <summary>
        /// HK_TipCamera 组件注销用（OnDisable 时调用，业务勿手动调）
        /// </summary>
        void UnregisterTipCamera(HK_TipCamera tipCamera);

        /// <summary>
        /// 取不到相机时的诊断报错（HK_TipsCtrl 内部调用，只报一次避免刷屏）
        /// </summary>
        void LogMissingCameraErrorOnce();

        /// <summary>
        /// 手动添加 HKTip
        /// </summary>
        /// <param name="tip"></param>
        void AddTipItem(HK_TipsCtrl tip);

        /// <summary>
        /// 手动移除 HKTip
        /// </summary>
        /// <param name="tip"></param>
        void RemoveTipItem(HK_TipsCtrl tip);

        /// <summary>
        /// 显示激活 所有的 HKTip
        /// </summary>
        void ShowAllTips();

        /// <summary>
        /// 显示激活 HKTip
        /// </summary>
        void ShowTips(List<HK_TipsCtrl> tips);

        /// <summary>
        /// 显示激活 相应Group的 Tip
        /// </summary>
        void ShowTipsByGroup(HK_TipGroup group);

        /// <summary>
        /// 禁用隐藏 所有的 HKTip
        /// </summary>
        void HideAllTips();

        /// <summary>
        /// 禁用隐藏 HKTip
        /// </summary>
        void HideTips(List<HK_TipsCtrl> tips);

        /// <summary>
        /// 显示激活 相应Group的 Tip
        /// </summary>
        void HideTipsByGroup(HK_TipGroup group);

        /// <summary>
        /// 批量改变所有 HKTip 的可点击状态（显隐请用 Show/Hide 系列）
        /// </summary>
        /// <param name="isClickable"></param>
        void SetAllTipsClickable(bool isClickable);
    }

    public class HK_TipSystem : AbstractSystem, IHK_TipSystem
    {
        public List<HK_TipsCtrl> TipsList { get; } = new List<HK_TipsCtrl>();

        protected override void OnInit()
        {
            InitOrResetAllTips();

            HKDebug.Log("IHK_TipSystem 初始化完成!", LogColor.Cyan, LogStyle.Bold);
        }

        // ---------- 相机注册表 ----------

        // 当前注册的 Tip 相机标记（场景中挂 HK_TipCamera 的相机启用时注册）
        HK_TipCamera registeredTipCamera;

        public Camera HK_TipCamera => registeredTipCamera != null ? registeredTipCamera.Camera : null;

        public void RegisterTipCamera(HK_TipCamera tipCamera)
        {
            if (registeredTipCamera == tipCamera)
                return;

            if (registeredTipCamera != null)
                HKDebug.LogWarning($"[HK_TipSystem] 检测到多个 HK_TipCamera：{tipCamera.name} 将覆盖 {registeredTipCamera.name}");

            registeredTipCamera = tipCamera;
            isMissingCameraErrorLogged = false;   // 相机恢复后，下次缺失可再次报错
            HKDebug.Log($"[HK_TipSystem] Tip 相机已注册：{tipCamera.name}");
        }

        // 是否已报过「缺相机」（只报一次，避免每帧刷屏）
        bool isMissingCameraErrorLogged;

        public void LogMissingCameraErrorOnce()
        {
            if (isMissingCameraErrorLogged)
                return;

            isMissingCameraErrorLogged = true;
            HKDebug.LogError("[HK_TipSystem] 未检测到 HK_TipCamera：请把 HK_TipCamera 组件挂到驱动 Tip 的相机上，否则 Tip 不会朝向/缩放/按距离显隐");
        }

        public void UnregisterTipCamera(HK_TipCamera tipCamera)
        {
            // 只清自己注册的那个（被覆盖过的相机禁用时不误清他人的）
            if (registeredTipCamera == tipCamera)
                registeredTipCamera = null;
        }

        // 首次扫描并初始化所有已加载场景中的 Tip：由 OnInit 内部调用。
        // 日常 Tip 靠 HK_TipsCtrl.Awake → AddTipItem 自动入列初始化，无需业务手动触发；
        // 此方法不对外暴露（没有"手动收集"这一步，开发者只用指令和事件即可）
        void InitOrResetAllTips()
        {
            // 原地更新，保持 TipsList 实例不变（外部持有的引用不会失效）
            TipsList.Clear();

            // 收集所有已加载场景中的 Tip：含 Additive 副场景与 DontDestroyOnLoad；
            // Prefab 资产的场景未加载，会被 isLoaded 过滤掉
            TipsList.AddRange(Resources.FindObjectsOfTypeAll<HK_TipsCtrl>()
                .Where(tip => tip.gameObject.scene.isLoaded));

            TipsList.ForEach(tip => tip.Init());
        }

        public void AddTipItem(HK_TipsCtrl tip)
        {
            if (!TipsList.Contains(tip))
            {
                TipsList.Add(tip);
            }

            // 无论是否新加入都调一次：Init 幂等（已完成则直接返回）；
            // 批量初始化时被异常连累而未完成的 Tip，在这里兜底补上
            tip.Init();
        }

        public void RemoveTipItem(HK_TipsCtrl tip)
        {
            if (!TipsList.Contains(tip))
                return;

            TipsList.Remove(tip);
        }

        public void ShowAllTips()
        {
            foreach (HK_TipsCtrl tip in TipsList)
            {
                // Unity 重载了 ==：已销毁的组件判为 null，跳过（避免中断整批）
                if (tip != null)
                    tip.ShowWithAnim();
            }
        }

        public void ShowTipsByGroup(HK_TipGroup group)
        {
            foreach (HK_TipsCtrl tip in TipsList)
            {
                if (tip != null && tip.tipGroup == group)
                    tip.ShowWithAnim();
            }
        }

        public void HideAllTips()
        {
            foreach (HK_TipsCtrl tip in TipsList)
            {
                if (tip != null)
                    tip.HideWithAnim();
            }
        }

        public void HideTipsByGroup(HK_TipGroup group)
        {
            foreach (HK_TipsCtrl tip in TipsList)
            {
                if (tip != null && tip.tipGroup == group)
                    tip.HideWithAnim();
            }
        }

        public void SetAllTipsClickable(bool isClickable)
        {
            foreach (HK_TipsCtrl tip in TipsList)
            {
                if (tip != null)
                    tip.SetIsCanClick(isClickable);
            }
        }

        public void ShowTips(List<HK_TipsCtrl> tips)
        {
            if (tips == null)
                return;

            foreach (HK_TipsCtrl tip in tips)
            {
                // Unity 重载了 ==：已销毁的组件判为 null，跳过
                if (tip != null)
                    tip.ShowWithAnim();
            }
        }

        public void HideTips(List<HK_TipsCtrl> tips)
        {
            if (tips == null)
                return;

            foreach (HK_TipsCtrl tip in tips)
            {
                if (tip != null)
                    tip.HideWithAnim();
            }
        }
    }
}