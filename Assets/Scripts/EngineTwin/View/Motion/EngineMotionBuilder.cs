using System.Collections.Generic;
using UnityEngine;

namespace EngineTwin
{
    /// <summary>运行时生成的几何：带 UV 的皮带（可滚动纹理）、曲轴/活塞/连杆/凸轮轴</summary>
    public static class EngineMotionBuilder
    {
        // ================= 皮带 =================

        public const float BeltWidth = 0.034f;
        public const float BeltThickness = 0.006f;
        public const float BeltTextureLength = 0.25f;   // 纹理每重复一次对应的皮带长度（米）

        /// <summary>
        /// 按皮带轮外切线生成闭合皮带。centers 为皮带平面内的二维坐标（逆时针顺序），返回总长度。
        /// </summary>
        public static Mesh BuildBelt(IList<Vector2> centers, IList<float> radii, out float length)
        {
            int n = centers.Count;
            var tangents = new (float aOut, float aIn)[n];   // 第 i 段：离开 i 的角度、到达 i+1 的角度
            for (int i = 0; i < n; i++)
            {
                Vector2 c1 = centers[i], c2 = centers[(i + 1) % n];
                float r1 = radii[i], r2 = radii[(i + 1) % n];
                Vector2 d = c2 - c1;
                float th = Mathf.Atan2(d.y, d.x);
                float al = Mathf.Acos(Mathf.Clamp((r1 - r2) / d.magnitude, -1f, 1f));
                float a = th - al;
                tangents[i] = (a, a);
            }

            var pts = new List<Vector2>();
            var normals = new List<Vector2>();
            for (int i = 0; i < n; i++)
            {
                float aIn = tangents[(i - 1 + n) % n].aIn;
                float aOut = tangents[i].aOut;
                while (aOut < aIn) aOut += Mathf.PI * 2f;
                int steps = Mathf.Max(2, Mathf.CeilToInt((aOut - aIn) * radii[i] / 0.004f));
                for (int k = 0; k <= steps; k++)
                {
                    float a = Mathf.Lerp(aIn, aOut, k / (float)steps);
                    var nrm = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    pts.Add(centers[i] + nrm * radii[i]);
                    normals.Add(nrm);
                }
            }

            int m = pts.Count;
            var dist = new float[m + 1];
            for (int i = 1; i <= m; i++) dist[i] = dist[i - 1] + Vector2.Distance(pts[i - 1], pts[i % m]);
            length = dist[m];

            // 每个路径点 4 个截面顶点 × 4 个面（外/内/前/后），首尾各复制一圈以便 UV 连续
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var norms = new List<Vector3>();
            var tris = new List<int>();
            float hw = BeltWidth * 0.5f;

            void Face(System.Func<Vector2, Vector2, (Vector3 a, Vector3 b, Vector3 normal)> corner)
            {
                int start = verts.Count;
                for (int i = 0; i <= m; i++)
                {
                    int idx = i % m;
                    var (va, vb, nn) = corner(pts[idx], normals[idx]);
                    float u = dist[i] / BeltTextureLength;
                    verts.Add(va); uvs.Add(new Vector2(u, 0f)); norms.Add(nn);
                    verts.Add(vb); uvs.Add(new Vector2(u, 1f)); norms.Add(nn);
                }
                for (int i = 0; i < m; i++)
                {
                    int a = start + i * 2;
                    tris.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
                }
            }

            Vector3 V(Vector2 p, float z) => new Vector3(p.x, p.y, z);
            // 外表面（有花纹的一面）
            Face((p, nr) => (V(p + nr * BeltThickness, -hw), V(p + nr * BeltThickness, hw), V(nr, 0)));
            // 内表面
            Face((p, nr) => (V(p, hw), V(p, -hw), V(-nr, 0)));
            // 两侧（顶点顺序保证正面朝外：Unity 顺时针为正面）
            Face((p, nr) => (V(p + nr * BeltThickness, hw), V(p, hw), Vector3.forward));
            Face((p, nr) => (V(p, -hw), V(p + nr * BeltThickness, -hw), Vector3.back));

            var mesh = new Mesh { name = "Belt_Runtime" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetNormals(norms);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        /// <summary>皮带纹理：纵向楔形花纹 + 周期性白色印字标记（转动时能看出运动）</summary>
        public static Texture2D BuildBeltTexture()
        {
            const int w = 256, h = 32;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "BeltTex", wrapMode = TextureWrapMode.Repeat };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float rib = 0.5f + 0.5f * Mathf.Cos(y / (float)h * Mathf.PI * 2f * 6f);
                for (int x = 0; x < w; x++)
                {
                    float c = 0.025f + 0.03f * rib;
                    bool mark = x % 128 < 22 && y > 9 && y < 23 && (x % 128) % 6 < 4;   // 印字
                    if (mark) c = 0.55f;
                    px[y * w + x] = new Color(c, c, c, 1f);
                }
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        // ================= 曲柄连杆机构 =================

        public const float CrankRadius = 0.04f;
        public const float RodLength = 0.15f;
        public const float CrankHeight = 0.20f;                      // 曲轴中心高度（发动机根节点局部）
        public static readonly float[] CylinderZ = { 0.225f, 0.075f, -0.075f, -0.225f };
        public static readonly float[] PinAngles = { 0f, 90f, 270f, 180f };   // 十字形曲轴

        public static Transform Cylinder(string name, Transform parent, float radius, float length, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);   // 圆柱默认高 2
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go.transform;
        }

        public static Transform Box(string name, Transform parent, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go.transform;
        }

        public static Material Mat(string name, Color color, float metallic, float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard"); // 备用方案
            var m = new Material(shader) { name = name };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smoothness);
            return m;
        }
    }
}
