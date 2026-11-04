using System.Collections.Generic;
using QFramework;
using UnityEngine;

namespace EngineTwin
{
    /// <summary>
    /// 运转动画：皮带轮按传动比转动、皮带纹理滚动、曲轴/活塞/连杆/凸轮轴联动。
    /// 真实转速太快会出现频闪，按 visualSpeedScale 缩放（6000 rpm × 0.02 ≈ 曲轴 2 转/秒）。
    /// </summary>
    public class EngineMotion : EngineTwinController
    {
        public float visualSpeedScale = 0.02f;

        static readonly string[] BeltOrder = { "Frt_CrankDamper", "Frt_PulleyIdler", "Frt_PulleyAlt", "Frt_PulleyWP", "Frt_PulleyTens", "Frt_PulleyIdlerLo" };

        IEngineTelemetryModel telemetry;
        Transform root;
        float crankAngle, crankRadius = 0.105f;

        class Spin { public RotatingPulley P; public Quaternion Base; public float Angle; }
        readonly List<Spin> spins = new List<Spin>();

        Renderer beltRenderer;   // 剖切会替换材质，每帧取当前材质写纹理偏移
        float beltOffset;

        // 曲柄连杆
        Transform crank;
        readonly List<(Transform piston, Transform rod, int pin, int bank, float zOffset)> slots = new List<(Transform, Transform, int, int, float)>();
        readonly List<Transform> cams = new List<Transform>();

        public Transform Internals { get; private set; }

        public void Init(Transform engineRoot, EngineAssembly assembly, EnginePart beltPart)
        {
            telemetry = this.GetModel<IEngineTelemetryModel>();
            root = engineRoot;

            foreach (var p in assembly.Pulleys)
            {
                spins.Add(new Spin { P = p, Base = p.Pivot.localRotation });
                if (p.Name == "Frt_CrankDamper") crankRadius = p.Radius;
            }

            if (beltPart != null) BuildBelt(assembly, beltPart);
            BuildInternals();
        }

        // ---------- 皮带 ----------

        void BuildBelt(EngineAssembly assembly, EnginePart beltPart)
        {
            Renderer old = beltPart.Renderers.Find(r => r.name == "Frt_Belt");
            if (old == null) return;

            var centers = new List<Vector2>();
            var radii = new List<float>();
            foreach (var name in BeltOrder)
            {
                var p = assembly.Pulleys.Find(x => x.Name == name);
                if (p == null) { Debug.LogWarning($"[EngineTwin] 未找到皮带轮 {name}，跳过皮带重建"); return; }
                Vector3 c = root.InverseTransformPoint(p.Pivot.position);
                centers.Add(new Vector2(c.x, c.y));
                radii.Add(p.Radius);
            }

            float beltZ = root.InverseTransformPoint(old.bounds.center).z;
            Mesh mesh = EngineMotionBuilder.BuildBelt(centers, radii, out _);

            var go = new GameObject("Frt_Belt_Runtime", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(old.transform.parent, false);
            go.transform.SetPositionAndRotation(root.TransformPoint(0f, 0f, beltZ), root.rotation);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;

            var beltMaterial = EngineMotionBuilder.Mat("BeltRuntime", Color.white, 0f, 0.35f);
            beltMaterial.mainTexture = EngineMotionBuilder.BuildBeltTexture();
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = beltMaterial;
            beltRenderer = mr;

            old.enabled = false;                 // 原静态皮带只保留碰撞体用于拾取
            beltPart.Renderers.Add(mr);          // 参与高亮与剖切
        }

        // ---------- 曲柄连杆机构（缸体内部，剖切时可见） ----------

        void BuildInternals()
        {
            Material forged = EngineMotionBuilder.Mat("CrankSteel", new Color(0.42f, 0.43f, 0.45f), 0.9f, 0.55f);
            Material alu = EngineMotionBuilder.Mat("PistonAlu", new Color(0.78f, 0.78f, 0.80f), 0.85f, 0.6f);
            Material rodMat = EngineMotionBuilder.Mat("RodSteel", new Color(0.30f, 0.31f, 0.33f), 0.9f, 0.45f);
            Material camMat = EngineMotionBuilder.Mat("CamSteel", new Color(0.55f, 0.50f, 0.42f), 0.9f, 0.5f);

            Internals = new GameObject("Internals").transform;
            Internals.SetParent(root, false);

            crank = new GameObject("Crankshaft").transform;
            crank.SetParent(Internals, false);
            crank.localPosition = new Vector3(0f, EngineMotionBuilder.CrankHeight, 0f);

            var journal = EngineMotionBuilder.Cylinder("Journal", crank, 0.028f, 0.62f, forged);
            journal.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var snout = EngineMotionBuilder.Cylinder("Snout", crank, 0.02f, 0.12f, forged);
            snout.localRotation = Quaternion.Euler(90f, 0f, 0f);
            snout.localPosition = new Vector3(0f, 0f, 0.36f);

            float R = EngineMotionBuilder.CrankRadius;
            for (int i = 0; i < 4; i++)
            {
                float z = EngineMotionBuilder.CylinderZ[i];
                float a = EngineMotionBuilder.PinAngles[i] * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                var rot = Quaternion.FromToRotation(Vector3.up, dir);

                var pin = EngineMotionBuilder.Cylinder("Pin" + i, crank, 0.022f, 0.05f, forged);
                pin.localRotation = Quaternion.Euler(90f, 0f, 0f);
                pin.localPosition = dir * R + Vector3.forward * z;

                foreach (float dz in new[] { -0.033f, 0.033f })
                {
                    var web = EngineMotionBuilder.Box("Web", crank, new Vector3(0.06f, R + 0.04f, 0.014f), forged);
                    web.localRotation = rot;
                    web.localPosition = dir * (R * 0.5f) + Vector3.forward * (z + dz);
                    var cw = EngineMotionBuilder.Box("Counterweight", crank, new Vector3(0.11f, 0.045f, 0.014f), forged);
                    cw.localRotation = rot;
                    cw.localPosition = -dir * 0.04f + Vector3.forward * (z + dz);
                }

                for (int bank = -1; bank <= 1; bank += 2)
                {
                    var piston = EngineMotionBuilder.Cylinder($"Piston{i}{(bank > 0 ? "L" : "R")}", Internals, 0.042f, 0.055f, alu);
                    var rod = EngineMotionBuilder.Box($"Rod{i}{(bank > 0 ? "L" : "R")}", Internals, new Vector3(0.02f, EngineMotionBuilder.RodLength, 0.016f), rodMat);
                    slots.Add((piston, rod, i, bank, bank * 0.012f));
                }
            }

            // 凸轮轴：每列一根，位于缸盖内，转速为曲轴一半
            for (int bank = -1; bank <= 1; bank += 2)
            {
                Vector3 axis = BankAxis(bank);
                var cam = new GameObject(bank > 0 ? "CamL" : "CamR").transform;
                cam.SetParent(Internals, false);
                cam.localPosition = new Vector3(0f, EngineMotionBuilder.CrankHeight, 0f) + axis * 0.36f;
                var shaft = EngineMotionBuilder.Cylinder("Shaft", cam, 0.012f, 0.6f, camMat);
                shaft.localRotation = Quaternion.Euler(90f, 0f, 0f);
                for (int i = 0; i < 8; i++)
                {
                    var lobe = EngineMotionBuilder.Box("Lobe", cam, new Vector3(0.022f, 0.036f, 0.014f), camMat);
                    lobe.localPosition = new Vector3(0f, 0.008f, 0.26f - i * 0.075f);
                    lobe.localRotation = Quaternion.Euler(0f, 0f, i * 67f);
                }
                cams.Add(cam);
            }
        }

        /// <summary>缸列轴线（发动机根节点局部）：bank=+1 为 +X 一侧</summary>
        static Vector3 BankAxis(int bank) => new Vector3(bank * 0.7071f, 0.7071f, 0f);

        void Update()
        {
            if (telemetry == null) return;

            float degPerSec = telemetry.GetValue(SensorId.Rpm) / 60f * 360f * visualSpeedScale;
            float dA = degPerSec * Time.deltaTime;
            crankAngle = (crankAngle + dA) % 360f;

            // 皮带轮：按半径比换算角速度
            foreach (var s in spins)
            {
                s.Angle = (s.Angle + dA * crankRadius / s.P.Radius) % 360f;
                s.P.Pivot.localRotation = s.Base * Quaternion.AngleAxis(s.Angle, s.P.LocalAxis);
            }

            // 皮带线速度 = 曲轴轮缘线速度
            // 注意：剖切功能会替换材质实例，每帧从 beltRenderer 获取当前材质
            if (beltRenderer != null)
            {
                var mat = beltRenderer.sharedMaterial; // 每帧获取当前材质
                if (mat != null)
                {
                    float beltSpeed = dA * Mathf.Deg2Rad * crankRadius;
                    beltOffset = (beltOffset - beltSpeed / EngineMotionBuilder.BeltTextureLength) % 1f;
                    mat.mainTextureOffset = new Vector2(beltOffset, 0f);
                }
            }

            UpdateCrankTrain();
        }

        void UpdateCrankTrain()
        {
            if (crank == null) return;
            crank.localRotation = Quaternion.AngleAxis(crankAngle, Vector3.forward);
            foreach (var cam in cams)
                cam.localRotation = Quaternion.AngleAxis(crankAngle * 0.5f, Vector3.forward);

            float R = EngineMotionBuilder.CrankRadius, L = EngineMotionBuilder.RodLength;
            Vector3 center = new Vector3(0f, EngineMotionBuilder.CrankHeight, 0f);
            foreach (var (piston, rod, pin, bank, zOff) in slots)
            {
                float a = (EngineMotionBuilder.PinAngles[pin] + crankAngle) * Mathf.Deg2Rad;
                float z = EngineMotionBuilder.CylinderZ[pin];
                Vector3 pinPos = center + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * R;

                Vector3 axis = BankAxis(bank);
                Vector3 rel = pinPos - center;
                float along = Vector3.Dot(rel, axis);
                Vector3 perpV = rel - axis * along;
                float s = along + Mathf.Sqrt(Mathf.Max(0f, L * L - perpV.sqrMagnitude));
                Vector3 pistonPin = center + axis * s;

                Vector3 p0 = pinPos + Vector3.forward * (z + zOff);
                Vector3 p1 = pistonPin + Vector3.forward * (z + zOff);
                rod.localPosition = (p0 + p1) * 0.5f;
                rod.localRotation = Quaternion.FromToRotation(Vector3.up, p1 - p0);

                piston.localPosition = pistonPin + axis * 0.014f + Vector3.forward * z;
                piston.localRotation = Quaternion.FromToRotation(Vector3.up, axis);
            }
        }
    }
}
