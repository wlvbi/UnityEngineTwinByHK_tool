using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EngineTwin.Editor
{
    /// <summary>场景环境：发动机试验台架、地面与安全线、墙面、灯带、工具柜、油桶、顶部聚光灯</summary>
    public static class EngineTwinSceneDresser
    {
        const string MatDir = "Assets/EngineTwin/Materials";
        const float FloorY = -0.62f;

        [MenuItem("EngineTwin/3. 布置场景环境（台架/车间）", priority = 2)]
        public static void Dress()
        {
            var old = GameObject.Find("Environment");
            if (old != null) Undo.DestroyObjectImmediate(old);

            var env = new GameObject("Environment").transform;
            Undo.RegisterCreatedObjectUndo(env.gameObject, "Create Environment");

            Material floor = Mat("Env_Floor", new Color(0.36f, 0.37f, 0.38f), 0f, 0.35f);
            floor.mainTexture = ConcreteTexture();
            floor.mainTextureScale = new Vector2(6, 6);
            Material wall = Mat("Env_Wall", new Color(0.20f, 0.23f, 0.27f), 0f, 0.2f);
            Material stand = Mat("Env_StandYellow", new Color(0.95f, 0.68f, 0.08f), 0.3f, 0.55f);
            Material steel = Mat("Env_Steel", new Color(0.55f, 0.56f, 0.58f), 0.9f, 0.5f);
            Material rubber = Mat("Env_Rubber", new Color(0.03f, 0.03f, 0.03f), 0f, 0.3f);
            Material stripe = Mat("Env_SafetyStripe", new Color(1f, 0.8f, 0.05f), 0f, 0.4f);
            Material led = Mat("Env_LED", Color.black, 0f, 0.5f, new Color(0.45f, 0.85f, 1.3f));
            Material cabinet = Mat("Env_CabinetRed", new Color(0.62f, 0.07f, 0.06f), 0.4f, 0.55f);
            Material barrel = Mat("Env_BarrelBlue", new Color(0.08f, 0.22f, 0.55f), 0.5f, 0.5f);

            // 地面（带碰撞体：漏油油渍、粒子据此确定地面高度）
            var f = GameObject.CreatePrimitive(PrimitiveType.Plane);
            f.name = "Floor";
            f.transform.SetParent(env, false);
            f.transform.position = new Vector3(0, FloorY, 0);
            f.transform.localScale = new Vector3(2.4f, 1, 2.4f);
            f.GetComponent<Renderer>().sharedMaterial = floor;

            // 安全线：台架四周的黄色矩形
            float sx = 0.85f, sz = 0.85f, w = 0.05f;
            Box(env, "Stripe_N", new Vector3(0, FloorY + 0.002f, sz), new Vector3(sx * 2 + w, 0.004f, w), stripe, false);
            Box(env, "Stripe_S", new Vector3(0, FloorY + 0.002f, -sz), new Vector3(sx * 2 + w, 0.004f, w), stripe, false);
            Box(env, "Stripe_E", new Vector3(sx, FloorY + 0.002f, 0), new Vector3(w, 0.004f, sz * 2 + w), stripe, false);
            Box(env, "Stripe_W", new Vector3(-sx, FloorY + 0.002f, 0), new Vector3(w, 0.004f, sz * 2 + w), stripe, false);

            // ---------- 试验台架 ----------
            var st = new GameObject("TestStand").transform;
            st.SetParent(env, false);
            float t = 0.06f, bx = 0.5f, bz = 0.42f, railY = 0.035f;
            Box(st, "Base_L", new Vector3(-bx, FloorY + t / 2, 0), new Vector3(t, t, bz * 2 + t), stand);
            Box(st, "Base_R", new Vector3(bx, FloorY + t / 2, 0), new Vector3(t, t, bz * 2 + t), stand);
            Box(st, "Base_F", new Vector3(0, FloorY + t / 2, bz), new Vector3(bx * 2, t, t), stand);
            Box(st, "Base_B", new Vector3(0, FloorY + t / 2, -bz), new Vector3(bx * 2, t, t), stand);
            foreach (float px in new[] { -bx, bx })
                foreach (float pz in new[] { -bz * 0.8f, bz * 0.8f })
                {
                    float h = railY - FloorY;
                    Box(st, "Post", new Vector3(px, FloorY + h / 2, pz), new Vector3(t, h, t), stand);
                    Cyl(st, "Foot", new Vector3(px, FloorY + 0.006f, pz), 0.06f, 0.012f, steel);
                }
            Box(st, "Rail_L", new Vector3(-bx, railY, 0), new Vector3(t, t, bz * 2), stand);
            Box(st, "Rail_R", new Vector3(bx, railY, 0), new Vector3(t, t, bz * 2), stand);
            // 发动机支脚：台架横梁 → 橡胶减震块 → 缸体侧面
            foreach (int side in new[] { -1, 1 })
                foreach (float mz in new[] { -0.16f, 0.16f })
                {
                    Box(st, "MountArm", new Vector3(side * 0.37f, railY + 0.04f, mz), new Vector3(0.26f, 0.025f, 0.07f), steel);
                    Cyl(st, "MountRubber", new Vector3(side * 0.27f, railY + 0.075f, mz), 0.035f, 0.045f, rubber);
                    Box(st, "MountPad", new Vector3(side * 0.245f, railY + 0.11f, mz), new Vector3(0.03f, 0.05f, 0.07f), steel);
                }

            // ---------- 车间 ----------
            float wallH = 4f;
            Box(env, "Wall_Back", new Vector3(0, FloorY + wallH / 2, -3.2f), new Vector3(12, wallH, 0.1f), wall);
            Box(env, "Wall_Side", new Vector3(3.6f, FloorY + wallH / 2, 0), new Vector3(0.1f, wallH, 12), wall);
            for (int i = 0; i < 3; i++)
            {
                float y = FloorY + 1.2f + i * 0.9f;
                Box(env, "LED_Back" + i, new Vector3(0, y, -3.14f), new Vector3(10, 0.03f, 0.02f), led, false);
                Box(env, "LED_Side" + i, new Vector3(3.54f, y, 0), new Vector3(0.02f, 0.03f, 10), led, false);
            }

            // 工具柜（抽屉缝 + 把手）
            var cab = new GameObject("ToolCabinet").transform;
            cab.SetParent(env, false);
            cab.position = new Vector3(2.4f, FloorY, -2.6f);
            Box(cab, "Body", new Vector3(0, 0.5f, 0), new Vector3(1.1f, 1f, 0.5f), cabinet);
            for (int i = 0; i < 5; i++)
            {
                Box(cab, "Gap", new Vector3(0, 0.15f + i * 0.17f, 0.252f), new Vector3(1.02f, 0.008f, 0.004f), rubber, false);
                Box(cab, "Handle", new Vector3(0, 0.22f + i * 0.17f, 0.265f), new Vector3(0.5f, 0.02f, 0.02f), steel, false);
            }
            Box(cab, "Top", new Vector3(0, 1.01f, 0), new Vector3(1.14f, 0.025f, 0.54f), steel);

            // 油桶
            for (int i = 0; i < 3; i++)
            {
                var p = new Vector3(3.0f - (i % 2) * 0.62f, FloorY + 0.44f, -1.4f + i * 0.7f);
                Cyl(env, "OilDrum" + i, p, 0.29f, 0.88f, barrel);
                Cyl(env, "DrumRing" + i, p + Vector3.up * 0.15f, 0.295f, 0.02f, steel);
                Cyl(env, "DrumRing" + i, p - Vector3.up * 0.15f, 0.295f, 0.02f, steel);
            }

            // 顶部聚光灯（软阴影）
            var spot = new GameObject("Spot_Top", typeof(Light)).transform;
            spot.SetParent(env, false);
            spot.position = new Vector3(-0.3f, 2.6f, 0.6f);
            spot.rotation = Quaternion.LookRotation(new Vector3(0.12f, -1f, -0.25f));
            var l = spot.GetComponent<Light>();
            l.type = LightType.Spot; l.range = 6f; l.spotAngle = 55f; l.innerSpotAngle = 30f;
            l.intensity = 2.2f; l.color = new Color(1f, 0.97f, 0.92f); l.shadows = LightShadows.Soft;

            // 相机背景改为墙面色，远处不再是纯灰
            if (Camera.main != null)
            {
                Undo.RecordObject(Camera.main, "Camera bg");
                Camera.main.backgroundColor = new Color(0.12f, 0.14f, 0.17f);
                Camera.main.farClipPlane = 60f;
            }

            EditorSceneManager.MarkSceneDirty(env.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[EngineTwin] 场景环境已布置");
        }

        // ---------- 工具 ----------

        static Transform Box(Transform parent, string name, Vector3 pos, Vector3 size, Material m, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go.transform;
        }

        static Transform Cyl(Transform parent, string name, Vector3 pos, float radius, float height, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(radius * 2, height / 2, radius * 2);
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go.transform;
        }

        static Material Mat(string name, Color color, float metallic, float smoothness, Color? emission = null)
        {
            if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/EngineTwin", "Materials");
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.color = color;
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Glossiness", smoothness);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>水泥地纹理：噪声 + 分缝线，保存为资产以便场景引用</summary>
        static Texture2D ConcreteTexture()
        {
            string path = $"{MatDir}/Env_Concrete.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "Env_Concrete", wrapMode = TextureWrapMode.Repeat };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float v = 0.82f + 0.12f * Mathf.PerlinNoise(x * 0.05f, y * 0.05f) + 0.06f * Mathf.PerlinNoise(x * 0.4f, y * 0.4f);
                    if (x < 2 || y < 2) v *= 0.7f;   // 地坪分缝
                    px[y * n + x] = new Color(v, v, v, 1f);
                }
            tex.SetPixels(px);
            tex.Apply(true);
            AssetDatabase.CreateAsset(tex, path);
            return tex;
        }
    }
}
