using UnityEngine;
using DG.Tweening;
using System;

namespace HKTools
{
    /// <summary>
    /// 提示标签控制脚本：悬停放大动画 + 点击转发。
    /// 注意：OnMouse 系只响应屏幕指针（鼠标/触摸），XR 手柄交互需另行接 IPointer 体系。
    /// </summary>
    [DisallowMultipleComponent]
    public class HK_TipTitle : MonoBehaviour
    {
        // 标签的点击事件回调
        public Action OnClickCallback;

        // 点击冷却时间（秒）：防止连点重复触发业务
        const float ClickCooldown = 0.3f;

        Vector3 initScale;

        // 初值给负无穷：避免场景加载后 0.3 秒内的首次点击被冷却吞掉
        float lastClickTime = -999f;

        // 所属 Tip 控制器（生长动画播放时悬停/震动让位，避免与动画的缩放写入相互争抢）
        HK_TipsCtrl tipCtrl;

        void Start()
        {
            initScale = transform.localScale;
            tipCtrl = GetComponentInParent<HK_TipsCtrl>();
        }

        /// <summary>生长动画播放中为 true（无控制器引用时 fail-open，不影响悬停）</summary>
        bool IsGrowAnimating()
        {
            return tipCtrl != null && tipCtrl.IsGrowAnimating;
        }

        void OnMouseEnter()
        {
            // 生长动画播放中让位：缩放由动画接管（下次进入时会以当前缩放为基准重新取）
            if (IsGrowAnimating())
                return;

            // 进入时重新取基准：外部（动态生成/布局脚本）改过 Title 缩放也能正确放大与还原
            initScale = transform.localScale;

            transform.DOScale(initScale * 1.15f, 0.3f);
        }

        void OnMouseExit()
        {
            if (IsGrowAnimating())
                return;

            transform.DOScale(initScale, 0.3f);
        }

        // 隐藏（淡出关子物体）/ 销毁时停掉本对象的悬停与震动动画：
        // 避免 tween 跑在已隐藏/已销毁的对象上（不同 DOTween 版本行为不一，可能刷警告），
        // 也避免恢复显示后停在错误的缩放值上（下次悬停会以当前缩放为基准重新取）
        void OnDisable()
        {
            transform.DOKill();
        }

        void OnDestroy()
        {
            transform.DOKill();
        }

        // 松手且仍在标签上才触发（按下后划出可取消）——OnMouseUpAsButton 自带「同物体按下并在其上松手」判定
        void OnMouseUpAsButton()
        {
            if (Time.time - lastClickTime < ClickCooldown)
                return;

            lastClickTime = Time.time;

            // 业务回调优先：动画播放中也照常派发点击事件
            OnClickCallback?.Invoke();

            // 动画播放中不做震动（震动的缩放写入会与生长动画争抢 localScale）
            if (IsGrowAnimating())
                return;

            // 先停掉悬停缩放动画，避免与震动动画争抢 localScale
            transform.DOKill();
            transform.DOShakeScale(0.25f, 0.2f, 10, 90, true);
        }
    }
}