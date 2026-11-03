/*************************************************************************************
 * 工    具：  HK_Curve2D
 * 
 * 描    述：  自定义的UI组件，用于在UGUI中绘制2D曲线，并可控制曲线默认显示状态，以及沿不同方向生长出现，适用场景：
 *             1.数据可视化中的曲线展示
 *             2.UI特效中的连线效果
 *             3.交互式界面中的动态路径展示
 * 
 * 版    本：  V1.1
 * 作    者：  京产肠饭
 * 
 * 创    建：  2024/01/25  V1.0
 *
 * 更    新：  2025/12/19  V1.1
 *            1.性能优化：缓存 AddQuad方法中的UIVertex数组，避免每次调用时创建新的数组
 *            2.功能优化：改进了四边形绘制的步长算法，并确保绘制的四边形长度更加一致
 *            3.功能优化：优化PlayShowAnim方法（重命名），使用Dotween实现曲线的生长动画（替代协程），提高生长动画效果
 *            4.功能新增：新增了曲线的隐藏动画 PlayHideAnim
 *            5.功能新增：新增了曲线的流动动画 PlayFlowAnim
 * 
 * 链    接：  https://gitee.com/JingChanChangFan/hk_-unity-tools
*************************************************************************************/

using DG.Tweening;
using HKTools;
using UnityEngine;
using UnityEngine.UI;

public enum CurveAnimDirection
{
    FromLeft,
    FromRight,
    FromTop,
    FromBottom,
}

public class HK_Curve2D : MonoBehaviour
{
    [Header("显示隐藏动画设置"), SerializeField]
    Ease animEaseType = Ease.InOutSine;
    [Header("显示隐藏动画时长"), SerializeField]
    float animDuration = 1f;

    [Header("流动动画时长"), SerializeField, Range(0, 10)]
    float flowSpeed = 1f;

    [Header("默认遮罩出血范围"), SerializeField, Range(-30, 0)]
    float defaultMaskBleedArea = -5f;

    HK_Curve2dDrawer curve2dDrawer;

    RectMask2D mask;
    RectTransform rtf;

    Tween animTween;

    void Awake()
    {
        if (mask == null)
            mask = GetComponent<RectMask2D>();
        if (rtf == null)
            rtf = GetComponent<RectTransform>();
        if (curve2dDrawer == null)
            curve2dDrawer = GetComponentInChildren<HK_Curve2dDrawer>();
        if (curve2dDrawer != null)
            curve2dDrawer.flowSpeed = flowSpeed;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (curve2dDrawer == null)
            curve2dDrawer = GetComponentInChildren<HK_Curve2dDrawer>();

        if (mask == null)
            mask = GetComponent<RectMask2D>();

        curve2dDrawer.flowSpeed = flowSpeed;
        mask.padding = new Vector4(defaultMaskBleedArea, defaultMaskBleedArea, defaultMaskBleedArea, defaultMaskBleedArea);
    }
#endif

    /// <summary>
    /// 设置 曲线的默认显示状态
    /// </summary>
    /// <param name="isShow">是否显示</param>
    public void SetCurveInitState(bool isShow)
    {
        if (isShow)
        {
            mask.padding = new Vector4(defaultMaskBleedArea, defaultMaskBleedArea, defaultMaskBleedArea, defaultMaskBleedArea);
        }
        else
        {
            mask.padding = new Vector4(defaultMaskBleedArea, defaultMaskBleedArea, rtf.rect.width, defaultMaskBleedArea);
        }
    }

    /// <summary>
    /// 曲线的出现动画
    /// </summary>
    /// <param name="animType">生长动画类型</param>
    public void PlayShowAnim(CurveAnimDirection animType)
    {
        // 停止当前动画
        if (animTween != null)
            animTween.Kill();

        // 根据动画类型设置初始状态
        Vector4 startPadding = new Vector4(defaultMaskBleedArea, defaultMaskBleedArea, defaultMaskBleedArea, defaultMaskBleedArea);
        Vector4 endPadding = new Vector4(defaultMaskBleedArea, defaultMaskBleedArea, defaultMaskBleedArea, defaultMaskBleedArea);

        switch (animType)
        {
            case CurveAnimDirection.FromLeft:
                startPadding = new Vector4(defaultMaskBleedArea, defaultMaskBleedArea, rtf.rect.width, defaultMaskBleedArea);
                break;
            case CurveAnimDirection.FromRight:
                startPadding = new Vector4(rtf.rect.width, defaultMaskBleedArea, defaultMaskBleedArea, defaultMaskBleedArea);
                break;
            case CurveAnimDirection.FromTop:
                startPadding = new Vector4(defaultMaskBleedArea, rtf.rect.height, defaultMaskBleedArea, defaultMaskBleedArea);
                break;
            case CurveAnimDirection.FromBottom:
                startPadding = new Vector4(defaultMaskBleedArea, defaultMaskBleedArea, defaultMaskBleedArea, rtf.rect.height);
                break;
        }

        mask.padding = startPadding;

        // 使用DOTween创建动画
        animTween = DOTween.To(() => mask.padding, x => mask.padding = x, endPadding, animDuration)
            .SetEase(animEaseType)
            .SetUpdate(true);
    }

    /// <summary>
    /// 曲线的消失动画
    /// </summary>
    /// <param name="animType">消失动画类型</param>
    public void PlayHideAnim(CurveAnimDirection animType)
    {
        // 停止当前动画
        if (animTween != null)
            animTween.Kill();

        // 根据动画类型设置结束状态（完全隐藏）
        Vector4 startPadding = new Vector4(defaultMaskBleedArea, defaultMaskBleedArea, defaultMaskBleedArea, defaultMaskBleedArea);
        Vector4 endPadding = new Vector4(defaultMaskBleedArea, defaultMaskBleedArea, defaultMaskBleedArea, defaultMaskBleedArea);

        switch (animType)
        {
            case CurveAnimDirection.FromLeft:
                endPadding = new Vector4(defaultMaskBleedArea, defaultMaskBleedArea, rtf.rect.width, defaultMaskBleedArea);
                break;
            case CurveAnimDirection.FromRight:
                endPadding = new Vector4(rtf.rect.width, defaultMaskBleedArea, defaultMaskBleedArea, defaultMaskBleedArea);
                break;
            case CurveAnimDirection.FromTop:
                endPadding = new Vector4(defaultMaskBleedArea, rtf.rect.height, defaultMaskBleedArea, defaultMaskBleedArea);
                break;
            case CurveAnimDirection.FromBottom:
                endPadding = new Vector4(defaultMaskBleedArea, defaultMaskBleedArea, defaultMaskBleedArea, rtf.rect.height);
                break;
        }

        mask.padding = startPadding;

        // 使用DOTween创建动画
        animTween = DOTween.To(() => mask.padding, x => mask.padding = x, endPadding, animDuration)
            .SetEase(animEaseType)
            .SetUpdate(true);
    }

    /// <summary>
    /// 曲线的流动动画，0是停止流动，大于0是流动速度
    /// </summary>
    /// <param name="flowSpeed">流动速度</param>
    public void PlayFlowAnim(float flowSpeed)
    {
        if (flowSpeed < 0)
            flowSpeed = 0;

        this.flowSpeed = flowSpeed;

        curve2dDrawer.flowSpeed = flowSpeed;
    }

    // 测试
    // [EngineTwin 兼容补丁] 仅在启用旧版 Input Manager 时编译测试热键；
    // 项目只启用 Input System 时 Input.GetKeyDown 会每帧抛 InvalidOperationException。
#if ENABLE_LEGACY_INPUT_MANAGER
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            PlayShowAnim(CurveAnimDirection.FromLeft);
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            PlayShowAnim(CurveAnimDirection.FromRight);
        }

        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            PlayShowAnim(CurveAnimDirection.FromTop);
        }

        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            PlayShowAnim(CurveAnimDirection.FromBottom);
        }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            PlayHideAnim(CurveAnimDirection.FromLeft);
        }

        if (Input.GetKeyDown(KeyCode.W))
        {
            PlayHideAnim(CurveAnimDirection.FromRight);
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            PlayHideAnim(CurveAnimDirection.FromTop);
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            PlayHideAnim(CurveAnimDirection.FromBottom);
        }

        if (Input.GetKeyDown(KeyCode.F1))
        {
            SetCurveInitState(true);
        }

        if (Input.GetKeyDown(KeyCode.F2))
        {
            SetCurveInitState(false);
        }

        if (Input.GetKeyDown(KeyCode.F3))
        {
            PlayFlowAnim(0.5f);
        }

        if (Input.GetKeyDown(KeyCode.F4))
        {
            PlayFlowAnim(0f);
        }
    }
#endif
}
