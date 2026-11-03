using System.Collections.Generic;
using UnityEngine;

namespace HKTools
{
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    public class HK_TipLine : MonoBehaviour
    {
#if UNITY_EDITOR
        LineRenderer lineRenderer;

        Transform title_TF;

        public List<Transform> linePos_List;

        void Start()
        {
            ResetLinPos();
        }

        void OnValidate()
        {
            ResetLinPos();
        }

        void Update()
        {
            if (Application.isPlaying)
                return;

            if (transform.childCount != linePos_List.Count)
            {
                ResetLinPos();
            }

            UpdateLineRendererPositions();
        }

        void ResetLinPos()
        {
            // lineRenderer 的兜底与判空统一放在 UpdateLineRendererPositions（缺组件时那里会报明确错误）
            title_TF = transform.parent.Find("Title");

            linePos_List = new List<Transform>();

            for (int i = 0; i < transform.childCount; i++)
            {
                linePos_List.Add(transform.GetChild(i));
            }
        }


        void UpdateLineRendererPositions()
        {
            if (linePos_List == null || linePos_List.Count == 0)
                return;

            if (lineRenderer == null)
                lineRenderer = GetComponent<LineRenderer>();

            if (lineRenderer == null)
            {
                Debug.LogError($"[HK_Tips] {name} 的 Line 上缺少 LineRenderer 组件，无法预览折线", this);
                return;
            }

            // 曲线开关、采样数与弧度跟运行时共用同一配置（HK_TipsCtrl），保证预览与运行一致
            HK_TipsCtrl tipCtrl = transform.parent.GetComponent<HK_TipsCtrl>();
            bool isUseCurve = tipCtrl != null && tipCtrl.IsUseCurve;
            int segments = tipCtrl != null ? tipCtrl.CurveSegments : 8;
            float strength = tipCtrl != null ? tipCtrl.CurveStrength : 0.5f;

            Vector3[] linePoses = HK_TipsCtrl.BuildLinePositions(linePos_List, isUseCurve, segments, strength);

            lineRenderer.positionCount = linePoses.Length;
            lineRenderer.SetPositions(linePoses);

            // 线宽也走实时预览：编辑器下调「连线宽度」立即生效（原仅运行后可见）
            if (tipCtrl != null)
                lineRenderer.widthMultiplier = tipCtrl.LineWidth;

            // Title 偏移与运行时共用同一数据源（读 HK_TipsCtrl.titleOffSet），编辑器预览所见即所得
            float titleOffset = tipCtrl != null ? tipCtrl.TitleOffSet : 0.2f;

            title_TF.position = linePos_List[linePos_List.Count - 1].position + Vector3.up * titleOffset;
        }
#endif
    }
}