using System;
using System.Collections.Generic;
using UnityEngine;

namespace EngineTwin
{
    /// <summary>旋转件：皮带轮及随其转动的螺栓/垫片，绕发动机前后轴转动</summary>
    public class RotatingPulley
    {
        public string Name;
        public Transform Pivot;
        public Vector3 LocalAxis;
        public float Radius;   // 用于按皮带传动比计算转速
    }

    /// <summary>
    /// 拆装与爆炸视图：运行时把每个部件的网格重新组织为
    /// Part_xx（整体拆下）→ L/C/R 子组（爆炸时左右列向两侧分开）→ 旋转枢轴 / 网格。
    /// </summary>
    public class EngineAssembly : MonoBehaviour
    {
        public float explodeDistance = 0.32f;
        public float detachDistance = 0.45f;
        public float smoothing = 6f;

        public class SubGroup { public Transform T; public Vector3 Dir; }

        public class Entry
        {
            public EnginePart Part;
            public Vector3 Dir;
            public float Detach, DetachTarget;
            public readonly List<SubGroup> Subs = new List<SubGroup>();
            public Vector3 OriginalLocalPos;       // 原始局部位置（用于吸附）
            public Vector3 CustomOffset;           // 用户拖拽的自定义偏移
            public bool HasCustomPosition;         // 是否有自定义位置
        }

        // 主皮带轮名 → 成员判定（螺栓、垫片随皮带轮一起转）
        static readonly (string main, Func<string, bool> member)[] PulleyRules =
        {
            ("Frt_CrankDamper",   n => n.StartsWith("Frt_Crank", StringComparison.Ordinal)),
            ("Frt_PulleyWP",      n => n.StartsWith("Frt_PulleyWP", StringComparison.Ordinal) || n.StartsWith("Frt_WPBolt", StringComparison.Ordinal) || n == "Frt_WPHub"),
            ("Frt_PulleyAlt",     n => n.StartsWith("Frt_PulleyAlt", StringComparison.Ordinal) || n == "Frt_AltFan"),
            ("Frt_PulleyTens",    n => n.StartsWith("Frt_PulleyTens", StringComparison.Ordinal)),
            ("Frt_PulleyIdler",   n => n == "Frt_PulleyIdler" || n.StartsWith("Frt_PulleyIdler_", StringComparison.Ordinal)),
            ("Frt_PulleyIdlerLo", n => n == "Frt_PulleyIdlerLo" || n.StartsWith("Frt_PulleyIdlerLo_", StringComparison.Ordinal)),
        };

        readonly List<Entry> entries = new List<Entry>();
        readonly Dictionary<EnginePart, Entry> byPart = new Dictionary<EnginePart, Entry>();
        Transform root;
        float explode, explodeTarget;
        EnginePart draggingPart; // 正在被拖拽的部件

        public readonly List<RotatingPulley> Pulleys = new List<RotatingPulley>();
        public float ExplodeTarget => explodeTarget;
        public event Action<EnginePart, bool> DetachChanged;

        public void Init(Transform engineRoot, IReadOnlyList<EnginePart> parts, Vector3 engineCenter)
        {
            root = engineRoot;
            foreach (var part in parts)
            {
                var group = new GameObject("Part_" + part.Id).transform;
                group.SetParent(root, false);
                part.Group = group;

                var e = new Entry { Part = part, OriginalLocalPos = Vector3.zero };
                Vector3 d = part.Info.id == "block" ? Vector3.zero          // 缸体作为基座不动
                    : part.Info.tipDirection.sqrMagnitude > 1e-4f ? root.TransformDirection(part.Info.tipDirection)
                    : part.Bounds.center - engineCenter;
                e.Dir = d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.up;
                if (part.Info.id == "block") e.Dir = Vector3.zero;
                e.OriginalLocalPos = Vector3.zero; // 初始时部件在原位

                // 1) 旋转枢轴：主皮带轮及其成员先收进枢轴，作为一个整体参与分组
                var units = new List<Transform>();
                var used = new HashSet<Renderer>();
                foreach (var (main, member) in PulleyRules)
                {
                    Renderer mainR = part.Renderers.Find(r => r.name == main);
                    if (mainR == null) continue;
                    var pivot = new GameObject("Rot_" + main).transform;
                    pivot.SetParent(group, false);
                    pivot.position = mainR.bounds.center;
                    foreach (var r in part.Renderers)
                        if (!used.Contains(r) && member(r.name)) { r.transform.SetParent(pivot, true); used.Add(r); }
                    Pulleys.Add(new RotatingPulley
                    {
                        Name = main, Pivot = pivot,
                        LocalAxis = group.InverseTransformDirection(root.forward),
                        Radius = Mathf.Max(0.01f, mainR.bounds.extents.x - 0.003f),
                    });
                    units.Add(pivot);
                }
                foreach (var r in part.Renderers)
                    if (!used.Contains(r)) units.Add(r.transform);

                // 2) 按左右分组：跨两列的部件（缸盖罩、线圈、燃油导轨…）爆炸时向两侧分开
                var subs = new Dictionary<int, SubGroup>();
                foreach (var u in units)
                {
                    float x = root.InverseTransformPoint(UnitCenter(u)).x - root.InverseTransformPoint(engineCenter).x;
                    int side = x > 0.08f ? 1 : x < -0.08f ? -1 : 0;
                    if (!subs.TryGetValue(side, out var sg))
                    {
                        var t = new GameObject(side > 0 ? "R" : side < 0 ? "L" : "C").transform;
                        t.SetParent(group, false);
                        sg = new SubGroup { T = t };
                        subs[side] = sg;
                        e.Subs.Add(sg);
                    }
                    u.SetParent(sg.T, true);
                }
                foreach (var kv in subs)
                {
                    Vector3 sideDir = root.right * kv.Key * 0.9f;
                    Vector3 dir = e.Dir == Vector3.zero ? Vector3.zero : (e.Dir + sideDir).normalized;
                    kv.Value.Dir = dir;
                }

                entries.Add(e);
                byPart[part] = e;
            }
        }

        static Vector3 UnitCenter(Transform t)
        {
            var r = t.GetComponentInChildren<Renderer>();
            return r != null ? r.bounds.center : t.position;
        }

        // ---------- 对外接口 ----------

        public void SetExplode(float t) => explodeTarget = Mathf.Clamp01(t);

        public bool IsDetached(EnginePart part) => part != null && byPart.TryGetValue(part, out var e) && e.DetachTarget > 0.5f;

        /// <summary>拆下 / 装回单个部件</summary>
        public bool ToggleDetach(EnginePart part)
        {
            if (part == null || !byPart.TryGetValue(part, out var e) || e.Dir == Vector3.zero) return false;
            e.DetachTarget = e.DetachTarget > 0.5f ? 0f : 1f;
            DetachChanged?.Invoke(part, e.DetachTarget > 0.5f);
            return e.DetachTarget > 0.5f;
        }

        public void ReassembleAll()
        {
            explodeTarget = 0f;
            foreach (var e in entries)
                if (e.DetachTarget > 0.5f) { e.DetachTarget = 0f; DetachChanged?.Invoke(e.Part, false); }
        }

        public bool CanDetach(EnginePart part) => part != null && byPart.TryGetValue(part, out var e) && e.Dir != Vector3.zero;

        /// <summary>获取部件的原始局部位置（世界空间）</summary>
        public Vector3 GetOriginalWorldPosition(EnginePart part)
        {
            if (part == null || !byPart.TryGetValue(part, out var e)) return Vector3.zero;
            return root.TransformPoint(e.OriginalLocalPos);
        }

        /// <summary>获取部件的标准拆卸结束位置（世界空间，默认爆炸位置）</summary>
        public Vector3 GetDefaultDetachWorldPosition(EnginePart part)
        {
            if (part == null || !byPart.TryGetValue(part, out var e)) return Vector3.zero;
            Vector3 defaultDetachLocalPos = root.InverseTransformVector(e.Dir * detachDistance);
            return root.TransformPoint(defaultDetachLocalPos);
        }

        /// <summary>设置部件的自定义拆卸结束位置（世界空间）</summary>
        public void SetCustomPosition(EnginePart part, Vector3 worldPosition)
        {
            if (part == null || !byPart.TryGetValue(part, out var e)) return;

            // 自定义偏移 = 新的结束位置 - 初始位置
            Vector3 currentLocalPos = root.InverseTransformPoint(worldPosition);
            e.CustomOffset = currentLocalPos - e.OriginalLocalPos;
            e.HasCustomPosition = true;
        }

        /// <summary>清除部件的自定义位置</summary>
        public void ClearCustomPosition(EnginePart part)
        {
            if (part == null || !byPart.TryGetValue(part, out var e)) return;
            e.HasCustomPosition = false;
            e.CustomOffset = Vector3.zero;
        }

        /// <summary>标记部件为拖拽中（拖拽时停止自动更新位置）</summary>
        public void SetDragging(EnginePart part, bool isDragging)
        {
            draggingPart = isDragging ? part : null;
        }

        /// <summary>获取部件的内部数据（用于持久化）</summary>
        public Entry GetPartData(EnginePart part)
        {
            return byPart.TryGetValue(part, out var e) ? e : null;
        }

        /// <summary>恢复部件的自定义偏移（用于持久化加载）</summary>
        public void RestoreCustomPosition(EnginePart part, Vector3 customOffset)
        {
            if (part == null || !byPart.TryGetValue(part, out var e)) return;
            e.CustomOffset = customOffset;
            e.HasCustomPosition = true;
        }

        /// <summary>恢复所有部件到默认位置（清除自定义位置）</summary>
        public void ResetAllToDefault()
        {
            foreach (var e in entries)
            {
                if (e.HasCustomPosition)
                {
                    e.HasCustomPosition = false;
                    e.CustomOffset = Vector3.zero;
                }
            }
            Debug.Log("[EngineAssembly] 已恢复所有部件到默认位置");
        }

        void Update()
        {
            if (root == null) return;
            float k = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
            explode = Mathf.Lerp(explode, explodeTarget, k);

            foreach (var e in entries)
            {
                // 如果这个部件正在被拖拽，跳过自动位置更新
                if (e.Part == draggingPart)
                    continue;

                e.Detach = Mathf.Lerp(e.Detach, e.DetachTarget, k);

                // 计算目标位置
                Vector3 targetLocalPos;

                // 标准拆卸结束位置
                Vector3 defaultDetachPos = root.InverseTransformVector(e.Dir * detachDistance);

                // 自定义拆卸结束位置（如果有的话）
                Vector3 customDetachPos = e.HasCustomPosition
                    ? (e.OriginalLocalPos + e.CustomOffset)
                    : defaultDetachPos;

                // 插值：从初始位置(0) 到 结束位置(标准或自定义)
                targetLocalPos = Vector3.Lerp(e.OriginalLocalPos, customDetachPos, e.Detach);

                e.Part.Group.localPosition = targetLocalPos;

                foreach (var s in e.Subs)
                    s.T.localPosition = root.InverseTransformVector(s.Dir * (explode * explodeDistance));
            }
        }
    }
}
