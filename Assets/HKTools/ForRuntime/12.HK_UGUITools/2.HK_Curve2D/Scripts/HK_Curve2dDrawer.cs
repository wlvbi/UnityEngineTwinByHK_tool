using UnityEngine;
using UnityEngine.UI;

namespace HKTools
{
    /// <summary>
    /// 自定义的UI组件，用于在UGUI中绘制2D曲线
    /// </summary>
    public class HK_Curve2dDrawer : Image
    {
        [Header("曲线："), SerializeField] AnimationCurve curve; // 曲线形状
        [Header("曲线宽度："), SerializeField] float lineWidth = 4; // 曲线的宽度
        [Header("精度："), SerializeField, Range(0.2f, 6f)] float accuracy = 0.2f;  // 曲线的绘制精度

        [Header("纹理偏移："), SerializeField] float textureOffset = 0.0f; // 控制纹理的偏移，实现蚂蚁线的动画效果

        [HideInInspector]
        public float flowSpeed = 0.5f; // 蚂蚁线流动速度

        // 四个顶点的缓存
        UIVertex[] vertex = new UIVertex[4];

        // 记录上一个四边形的共享顶点（用于缝合）
        Vector2[] previousSharedVertices = new Vector2[2];

        // 重写MaskableGraphic的OnPopulateMesh方法，用于生成曲线的网格
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            var rect = this.rectTransform.rect;
            var pivotY = rectTransform.pivot.y;

            vh.Clear();

            // 使用自适应步长，确保每段长度基本相同
            float targetSegmentLength = 100 / accuracy; // 目标段长度
            float t = 0;

            previousSharedVertices = new Vector2[2];

            while (t < 1.0f)
            {
                // 当前点
                Vector2 currentPos = new Vector2(
                    rect.xMin + t * rect.width,
                    CalcY(t, pivotY) * rect.height
                );

                // 计算下一个点的近似位置
                float nextT = Mathf.Min(1, t + 0.01f);
                Vector2 nextPos = new Vector2(
                    rect.xMin + nextT * rect.width,
                    CalcY(nextT, pivotY) * rect.height
                );

                // 估算曲线在当前位置的导数（斜率）
                float derivative = (nextPos.y - currentPos.y) / (nextPos.x - currentPos.x);

                // 计算实际步长，使曲线段的长度接近目标长度
                float actualStep = targetSegmentLength / Mathf.Sqrt(1 + derivative * derivative);
                float deltaX = actualStep;

                // 计算新的t值
                float newT = t + deltaX / rect.width;
                newT = Mathf.Min(newT, 1.0f);

                // 计算新点的位置
                Vector2 pos = new Vector2(
                    rect.xMin + newT * rect.width,
                    CalcY(newT, pivotY) * rect.height
                );

                // 添加四边形
                AddQuad(vh, currentPos, pos);

                // 更新状态
                t = newT;
            }

            // 添加三角形，用于渲染四边形
            for (int i = 0; i < vh.currentVertCount - 4; i += 4)
            {
                vh.AddTriangle(i + 1, i + 2, i + 4);
                vh.AddTriangle(i + 1, i + 2, i + 7);
            }
        }

        // 根据曲线计算Y坐标
        float CalcY(float time, float pivotY)
        {
            return curve.Evaluate(time) - pivotY;
        }

        // 添加一个四边形到VertexHelper（优化版本，支持缝合）
        void AddQuad(VertexHelper vh, Vector2 pos1, Vector2 pos2)
        {
            // 计算两点间距离
            float dis = Vector2.Distance(pos1, pos2);

            // 避免除零错误
            if (dis <= 0.0001f)
                return;

            // 计算垂直于线段的向量作为宽度方向
            float invDis = 1.0f / dis;
            float dx = (pos2.x - pos1.x) * invDis;
            float dy = (pos2.y - pos1.y) * invDis;

            // 计算垂直向量（旋转90度）
            float perpX = -dy * lineWidth * 0.5f;
            float perpY = dx * lineWidth * 0.5f;

            // 计算当前四边形的四个顶点
            Vector2 topLeft = new Vector2(pos1.x + perpX, pos1.y + perpY);
            Vector2 topRight = new Vector2(pos2.x + perpX, pos2.y + perpY);
            Vector2 bottomRight = new Vector2(pos2.x - perpX, pos2.y - perpY);
            Vector2 bottomLeft = new Vector2(pos1.x - perpX, pos1.y - perpY);

            // 如果有上一个四边形，使用共享顶点进行缝合
            // 使用上一个四边形的右侧顶点作为当前四边形的左侧顶点

            if (previousSharedVertices[0] != Vector2.zero)
            {
                topLeft = previousSharedVertices[0];
                bottomLeft = previousSharedVertices[1];
            }

            // 设置四个顶点的位置
            vertex[0].position = topLeft;
            vertex[1].position = topRight;
            vertex[2].position = bottomRight;
            vertex[3].position = bottomLeft;

            // 设置UV坐标
            vertex[0].uv0 = new Vector2(1, 1 + textureOffset);
            vertex[1].uv0 = new Vector2(1, textureOffset);
            vertex[2].uv0 = new Vector2(0, textureOffset);
            vertex[3].uv0 = new Vector2(0, 1 + textureOffset);

            // 设置顶点的颜色
            for (int i = 0; i < 4; i++)
            {
                vertex[i].color = color;
            }

            // 将四边形的顶点添加到VertexHelper
            vh.AddUIVertexQuad(vertex);

            // 记录当前四边形的右侧顶点，供下一个四边形使用
            previousSharedVertices[0] = topRight;
            previousSharedVertices[1] = bottomRight;

        }

        // 更新纹理偏移量，实现蚂蚁线流动效果
        void Update()
        {
            if (!Application.isPlaying || flowSpeed == 0)
                return;

            // 定期更新纹理偏移量
            textureOffset += flowSpeed * Time.deltaTime;

            textureOffset %= 1.0f;

            // 触发UI重新绘制
            SetVerticesDirty();
        }
    }
}