using System.Collections.Generic;
using UnityEngine;

namespace Dinner
{
    /// Spawning helpers for the asset packs: fit a prefab to a world size, sit it on a surface,
    /// keep URP happy, and split a mesh by which way its faces point.
    public static class Props
    {
        public enum Fit { Largest, X, Y, Z }

        /// Instantiate `prefab`, scale it so the chosen extent equals `size` (world units), and place it
        /// with the bottom-centre of its bounds at `position`, turned by `yaw` degrees. Pack units don't matter.
        public static GameObject Spawn(GameObject prefab, Transform parent, Vector3 position, float yaw, float size, Fit fit = Fit.Largest)
        {
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab);
            go.name = prefab.name;
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.transform.localScale = Vector3.one;
            FixMaterialsForUrp(go);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);

            var b = WorldBounds(go);
            float extent = fit == Fit.X ? b.size.x : fit == Fit.Y ? b.size.y : fit == Fit.Z ? b.size.z : Mathf.Max(b.size.x, b.size.z);
            float s = extent > 1e-5f ? size / extent : 1f;
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var pivotOffset = new Vector3(-b.center.x, -b.min.y, -b.center.z) * s;
            go.transform.localScale = Vector3.one * s;
            go.transform.rotation = rot;
            go.transform.position = position + rot * pivotOffset;
            if (parent != null) go.transform.SetParent(parent, true);
            return go;
        }

        /// The world height of the model's surface near its centre — the inside of a bowl or pan, or the
        /// top of a solid low-poly one. Scans mesh vertices within `radiusFraction` of the footprint's width.
        public static float TopSurfaceAtCenter(GameObject go, float radiusFraction = 0.25f)
        {
            var b = WorldBounds(go);
            float radius = Mathf.Max(b.size.x, b.size.z) * radiusFraction;
            float best = float.NegativeInfinity;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                Vector3[] verts;
                try { verts = mf.sharedMesh.vertices; } catch { continue; }   // non-readable mesh in a build
                foreach (var v in verts)
                {
                    var w = mf.transform.TransformPoint(v);
                    float dx = w.x - b.center.x, dz = w.z - b.center.z;
                    if (dx * dx + dz * dz <= radius * radius && w.y > best) best = w.y;
                }
            }
            return float.IsNegativeInfinity(best) ? b.max.y : best;
        }

        public static Bounds WorldBounds(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.1f);
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }

        /// Built-in-pipeline materials (the LowPoly kitchen pack) render magenta in URP; swap them for Lit copies.
        public static void FixMaterialsForUrp(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null || m.shader == null || m.shader.name.StartsWith("Universal Render Pipeline")) continue;
                    mats[i] = Converted(m);
                    changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        static readonly Dictionary<Material, Material> _converted = new Dictionary<Material, Material>();

        static Material Converted(Material src)
        {
            if (_converted.TryGetValue(src, out var m) && m != null) return m;
            var color = src.HasProperty("_Color") ? src.GetColor("_Color") : Color.white;
            float gloss = src.HasProperty("_Glossiness") ? src.GetFloat("_Glossiness") : 0.3f;
            float metal = src.HasProperty("_Metallic") ? src.GetFloat("_Metallic") : 0f;
            m = Visuals.Opaque(color, gloss, metal);
            if (src.HasProperty("_MainTex") && src.GetTexture("_MainTex") != null) m.SetTexture("_BaseMap", src.GetTexture("_MainTex"));
            m.name = src.name + " (URP)";
            _converted[src] = m;
            return m;
        }

        /// Split a mesh into three submeshes by face direction: 0 = facing down, 1 = facing up, 2 = sides.
        public static Mesh SplitByFacing(Mesh source, float upThreshold = 0.35f)
        {
            var verts = source.vertices;
            var norms = source.normals;
            var tris = source.triangles;
            bool hasNormals = norms != null && norms.Length == verts.Length;
            var down = new List<int>(); var up = new List<int>(); var side = new List<int>();
            for (int t = 0; t < tris.Length; t += 3)
            {
                int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                Vector3 n = hasNormals ? norms[a] + norms[b] + norms[c] : Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
                float m = n.magnitude;
                var list = m < 1e-6f ? side : n.y > upThreshold * m ? up : n.y < -upThreshold * m ? down : side;
                list.Add(a); list.Add(b); list.Add(c);
            }
            var mesh = new Mesh { name = source.name + " (faces)" };
            mesh.vertices = verts;
            if (hasNormals) mesh.normals = norms;
            mesh.uv = source.uv;
            mesh.subMeshCount = 3;
            mesh.SetTriangles(down, 0);
            mesh.SetTriangles(up, 1);
            mesh.SetTriangles(side, 2);
            if (!hasNormals) mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
