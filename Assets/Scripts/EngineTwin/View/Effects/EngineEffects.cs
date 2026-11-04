using QFramework;
using UnityEngine;

namespace EngineTwin
{
    /// <summary>
    /// 运行特效：进气气流、排气烟雾（随转速/排温变化）；
    /// 故障表现：漏油滴落 + 地面油渍扩大、冷却液泄漏蒸汽、轴承磨损火花 + 整机振动、发电机故障电火花。
    /// </summary>
    public class EngineEffects : EngineTwinController
    {
        public float vibrationVisualScale = 0.0007f;   // 振动烈度（mm/s）→ 画面抖动幅度（米），已夸张处理

        IEngineTelemetryModel telemetry;
        IEngineSimulationSystem simulation;
        Transform root;
        Vector3 rootBasePos;
        Material alphaMat, additiveMat;

        ParticleSystem intake, exhaust, steam, wearSparks, elecSparks;
        ParticleSystem[] oilDrips;
        Transform puddle;
        float floorY, elecTimer;
        bool airflowOn = true;

        public bool AirflowOn => airflowOn;
        public void SetAirflow(bool on) => airflowOn = on;

        public void Init(Transform engineRoot, IEnginePartModel parts, Material alpha, Material additive)
        {
            telemetry = this.GetModel<IEngineTelemetryModel>();
            simulation = this.GetSystem<IEngineSimulationSystem>();
            root = engineRoot;
            rootBasePos = root.localPosition;
            alphaMat = alpha != null ? alpha : FxMaterial("Legacy Shaders/Particles/Alpha Blended");
            additiveMat = additive != null ? additive : FxMaterial("Legacy Shaders/Particles/Additive");

            Renderer Find(string n)
            {
                foreach (var p in parts.Parts)
                    foreach (var r in p.Renderers)
                        if (r != null && r.name == n) return r;
                return null;
            }

            Vector3 fwd = root.forward, up = root.up;

            // 进气：节气门口前方吸入
            if (Find("Int_TBBore") is Renderer tb)
            {
                intake = CreatePS("FX_Intake", tb.transform.parent, alphaMat, true);
                intake.transform.SetPositionAndRotation(tb.bounds.center + fwd * 0.16f, Quaternion.LookRotation(-fwd, up));
                var m = intake.main;
                m.startSize = new ParticleSystem.MinMaxCurve(0.004f, 0.009f);
                m.startColor = new Color(0.65f, 0.9f, 1f, 0.45f);
                var sh = intake.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 8f; sh.radius = 0.05f;
                FadeInOut(intake);
            }

            // 排气：集气管出口
            if (Find("Exh_OutFlange") is Renderer ex)
            {
                exhaust = CreatePS("FX_Exhaust", ex.transform.parent, alphaMat, false);
                exhaust.transform.SetPositionAndRotation(ex.bounds.center - fwd * 0.01f, Quaternion.LookRotation(-fwd, up));
                var m = exhaust.main;
                m.gravityModifier = -0.02f; // 降低上升速度
                m.startLifetime = 1.8f; // 延长生命周期
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.6f); // 降低初始速度
                m.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.04f); // 初始更小

                var sh = exhaust.shape;
                sh.shapeType = ParticleSystemShapeType.Cone;
                sh.angle = 15f; // 增大扩散角度
                sh.radius = 0.03f;

                // 大小随时间变化（烟雾扩散）
                var sz = exhaust.sizeOverLifetime;
                sz.enabled = true;
                sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 2.5f)); // 扩散更大

                // 噪声（烟雾飘散）
                var nz = exhaust.noise;
                nz.enabled = true;
                nz.strength = 0.2f; // 增强扰动
                nz.frequency = 1.2f;
                nz.scrollSpeed = 0.15f;
                nz.damping = true;

                // 旋转（烟雾翻滚）
                var rot = exhaust.rotationOverLifetime;
                rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(-30f, 30f);

                FadeInOut(exhaust);
            }

            // 冷却液泄漏蒸汽：水泵出水口
            if (Find("Frt_WaterNeck") is Renderer wn)
            {
                steam = CreatePS("FX_Steam", wn.transform.parent, alphaMat, false);
                steam.transform.SetPositionAndRotation(wn.bounds.center + up * 0.05f, Quaternion.LookRotation(up, fwd));
                var m = steam.main;
                m.startLifetime = 1.6f; m.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.04f); m.startColor = new Color(1f, 1f, 1f, 0.35f);
                m.gravityModifier = -0.03f;
                var sh = steam.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 25f; sh.radius = 0.02f;
                var sz = steam.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.5f, 1, 3f));
                FadeInOut(steam);
            }

            // 轴承磨损火花：曲轴前端与正时盖之间
            if (Find("Frt_CrankDamper") is Renderer cd)
            {
                wearSparks = Sparks("FX_WearSparks", cd.transform.parent, cd.bounds.center - fwd * 0.05f - up * 0.06f, new Color(1f, 0.6f, 0.2f));
            }

            // 发电机故障电火花
            if (Find("Frt_AltRegulator") is Renderer reg)
            {
                elecSparks = Sparks("FX_ElecSparks", reg.transform.parent, reg.bounds.center + up * 0.03f, new Color(0.6f, 0.8f, 1f));
            }

            // 漏油：机油滤清器底部 + 油底壳边缘滴落
            floorY = FindFloorY();
            var dripPoints = new System.Collections.Generic.List<(Transform parent, Vector3 pos)>();
            if (Find("Misc_OilFilter") is Renderer of) dripPoints.Add((of.transform.parent, new Vector3(of.bounds.center.x, of.bounds.min.y, of.bounds.center.z)));
            if (Find("Blk_PanFlange") is Renderer pf) dripPoints.Add((pf.transform.parent, new Vector3(pf.bounds.min.x + 0.03f, pf.bounds.min.y, pf.bounds.center.z + 0.1f)));
            oilDrips = new ParticleSystem[dripPoints.Count];
            for (int i = 0; i < dripPoints.Count; i++)
            {
                var ps = CreatePS("FX_OilDrip" + i, dripPoints[i].parent, alphaMat, false); // 改为 false，不拉伸
                ps.transform.SetPositionAndRotation(dripPoints[i].pos, Quaternion.LookRotation(Vector3.down, fwd)); // 朝向改为向下
                var m = ps.main;
                m.startSpeed = 0f; // 初始速度为 0，完全靠重力
                m.gravityModifier = 2.5f; // 增加重力，加速下落
                m.startLifetime = Mathf.Sqrt(2f * Mathf.Max(0.05f, dripPoints[i].pos.y - floorY) / 9.81f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.004f, 0.007f); // 缩小粒子
                m.startColor = new Color(0.12f, 0.08f, 0.02f, 0.4f); // 更暗、更透明
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.005f; // 缩小发射范围

                // 添加大小随生命周期变化（模拟液滴形状）
                var sz = ps.sizeOverLifetime;
                sz.enabled = true;
                sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.2f));

                oilDrips[i] = ps;
            }

            // 地面油渍
            var pd = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pd.name = "FX_OilPuddle";
            Destroy(pd.GetComponent<Collider>());
            puddle = pd.transform;
            puddle.SetParent(transform, false);
            Vector3 c = dripPoints.Count > 0 ? dripPoints[0].pos : root.position;
            puddle.position = new Vector3(c.x + 0.05f, floorY + 0.002f, c.z);
            puddle.localScale = Vector3.zero;
            var pm = EngineMotionBuilder.Mat("OilPuddle", new Color(0.03f, 0.02f, 0.01f), 0.2f, 0.95f);
            pd.GetComponent<Renderer>().sharedMaterial = pm;
        }

        float FindFloorY()
        {
            Vector3 origin = root.position + root.up * 0.02f;
            foreach (var hit in Physics.RaycastAll(origin, Vector3.down, 5f))
                if (hit.collider.GetComponentInParent<EngineTwinBootstrap>() == null && !hit.collider.transform.IsChildOf(root))
                    return hit.point.y;
            return root.position.y - 0.5f;
        }

        // ---------- 每帧 ----------

        void Update()
        {
            if (telemetry == null) return;

            bool running = telemetry.IsRunning.Value;
            float rpm = telemetry.GetValue(SensorId.Rpm);
            float rpmN = Mathf.Clamp01(rpm / 6500f);
            float map = telemetry.GetValue(SensorId.IntakePressure);
            float egt = telemetry.GetValue(SensorId.ExhaustTemp);
            EngineFault faults = simulation.ActiveFaults;

            // 进气：流量 ∝ 转速 × 进气压力
            if (intake != null)
            {
                float flow = running && airflowOn ? rpmN * (map / 100f) : 0f;
                float speed = Mathf.Lerp(0.35f, 1.6f, flow);
                var m = intake.main;
                m.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.8f, speed * 1.2f);
                m.startLifetime = 0.15f / speed;
                Rate(intake, flow > 0.001f ? 40f + flow * 700f : 0f);
            }

            // 排气：转速越高越快越多，排温越高颜色越偏暖
            if (exhaust != null)
            {
                float speed = Mathf.Lerp(0.4f, 1.8f, rpmN);
                var m = exhaust.main;
                m.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.8f, speed * 1.2f);
                m.startLifetime = Mathf.Lerp(1.4f, 0.9f, rpmN);
                m.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.05f);
                float heat = Mathf.InverseLerp(400f, 900f, egt);
                m.startColor = Color.Lerp(new Color(0.62f, 0.64f, 0.68f, 0.32f), new Color(0.75f, 0.58f, 0.48f, 0.4f), heat);
                Rate(exhaust, running && airflowOn ? 18f + rpmN * 140f : 0f);
            }

            // 冷却液泄漏：温度越高蒸汽越多
            if (steam != null)
            {
                float coolant = telemetry.GetValue(SensorId.CoolantTemp);
                bool leak = (faults & EngineFault.CoolantLeak) != 0;
                Rate(steam, leak ? 6f + Mathf.Max(0f, coolant - 95f) * 2.5f : 0f);
            }

            // 漏油：滴落 + 地面油渍随漏油量扩大
            bool oilLeak = (faults & EngineFault.OilLeak) != 0;
            if (oilDrips != null)
                foreach (var d in oilDrips) Rate(d, oilLeak ? (running ? 7f : 2.5f) : 0f);
            if (puddle != null)
            {
                float leaked = Mathf.InverseLerp(1f, 0.05f, simulation.OilLevel);
                float target = leaked > 0.001f ? Mathf.Lerp(0.08f, 0.75f, Mathf.Sqrt(leaked)) : 0f;
                float cur = Mathf.Lerp(puddle.localScale.x, target, 1f - Mathf.Exp(-2f * Time.deltaTime));
                puddle.localScale = new Vector3(cur, 0.001f, cur * 0.8f);
            }

            // 轴承磨损：火花 + 整机振动
            float wear = simulation.Wear;
            if (wearSparks != null) Rate(wearSparks, running && wear > 0.15f ? wear * 90f * (0.3f + rpmN) : 0f);

            float vib = telemetry.GetValue(SensorId.Vibration);
            float amp = Mathf.Max(0f, vib - 3f) * vibrationVisualScale;
            float t = Time.time * 38f;
            Vector3 jitter = new Vector3(Mathf.PerlinNoise(t, 0.1f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f, Mathf.PerlinNoise(t, 3.3f) - 0.5f) * (2f * amp);
            root.localPosition = rootBasePos + jitter;

            // 发电机故障：间歇性电火花
            if (elecSparks != null && running && (faults & EngineFault.AlternatorFailure) != 0)
            {
                elecTimer -= Time.deltaTime;
                if (elecTimer <= 0f)
                {
                    elecSparks.Emit(Random.Range(6, 14));
                    elecTimer = Random.Range(0.4f, 1.6f);
                }
            }
        }

        void OnDisable()
        {
            if (root != null) root.localPosition = rootBasePos;
        }

        // ---------- 粒子工具 ----------

        static void Rate(ParticleSystem ps, float rate)
        {
            var e = ps.emission;
            e.rateOverTime = rate;
        }

        static ParticleSystem CreatePS(string name, Transform parent, Material mat, bool stretch)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main;
            m.loop = true;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.maxParticles = 1500;
            m.startLifetime = 1f;
            m.startSpeed = 0.5f;
            var e = ps.emission; e.rateOverTime = 0f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            if (stretch)
            {
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.velocityScale = 0.05f;
                r.lengthScale = 1.5f;
            }
            ps.Play();
            return ps;
        }

        ParticleSystem Sparks(string name, Transform parent, Vector3 pos, Color color)
        {
            var ps = CreatePS(name, parent, additiveMat, true);
            ps.transform.position = pos;
            var m = ps.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.4f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.003f, 0.006f);
            color.a = 0.6f; // 降低透明度
            m.startColor = color;
            m.gravityModifier = 1f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.015f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(color, 1f) },
                      new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) }); // 降低初始透明度
            col.color = g;
            return ps;
        }

        static void FadeInOut(ParticleSystem ps)
        {
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        static Material FxMaterial(string shaderName)
        {
            Shader s = Shader.Find(shaderName) ?? Shader.Find("Sprites/Default");
            var m = new Material(s) { mainTexture = SoftDot() };
            return m;
        }

        /// <summary>柔和圆点纹理（粒子用）</summary>
        public static Texture2D SoftDot()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "FX_SoftDot", wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    px[y * n + x] = new Color(1f, 1f, 1f, a * a * (3f - 2f * a));
                }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }
    }
}
