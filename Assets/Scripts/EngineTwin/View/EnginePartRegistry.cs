using System;
using System.Collections.Generic;
using UnityEngine;

namespace EngineTwin
{
    /// <summary>按部件目录把发动机模型下的网格归类，并补上射线拾取用的 MeshCollider</summary>
    public static class EnginePartRegistry
    {
        public static List<EnginePart> Build(Transform engineRoot, EnginePartCatalog catalog, out int unmatched)
        {
            var parts = new List<EnginePart>();
            foreach (var info in catalog.parts)
                parts.Add(new EnginePart(info));

            unmatched = 0;
            foreach (var renderer in engineRoot.GetComponentsInChildren<MeshRenderer>(true))
            {
                EnginePart part = Match(parts, renderer.gameObject.name);
                if (part == null)
                {
                    unmatched++;
                    continue;
                }

                part.Renderers.Add(renderer);

                if (!renderer.TryGetComponent(out Collider collider))
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null) continue;
                    var meshCollider = renderer.gameObject.AddComponent<MeshCollider>();
                    meshCollider.sharedMesh = filter.sharedMesh;
                    collider = meshCollider;
                }
                part.Colliders.Add(collider);
            }

            parts.RemoveAll(p => p.Renderers.Count == 0);

            foreach (var p in parts)
            {
                Bounds b = p.Renderers[0].bounds;
                foreach (var r in p.Renderers) b.Encapsulate(r.bounds);
                p.Bounds = b;
            }
            return parts;
        }

        static EnginePart Match(List<EnginePart> parts, string objectName)
        {
            foreach (var p in parts)
            {
                if (p.Info.meshPrefixes == null) continue;
                foreach (var prefix in p.Info.meshPrefixes)
                    if (!string.IsNullOrEmpty(prefix) && objectName.StartsWith(prefix, StringComparison.Ordinal))
                        return p;
            }
            return null;
        }

        public static Vector3 ComputeCenter(IReadOnlyList<EnginePart> parts)
        {
            if (parts.Count == 0) return Vector3.zero;
            Bounds b = parts[0].Bounds;
            foreach (var p in parts) b.Encapsulate(p.Bounds);
            return b.center;
        }
    }
}
