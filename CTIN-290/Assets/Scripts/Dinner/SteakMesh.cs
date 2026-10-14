using System.Collections.Generic;
using UnityEngine;

namespace Dinner
{
    /// A procedurally built steak: an irregular rounded slab with a domed top, bevelled edges and a
    /// fat cap along one end. Submeshes: 0 bottom face, 1 top face, 2 sides, 3 fat — so each can be
    /// tinted on its own as it cooks. Centred on the origin, lying flat in XZ, length along X.
    public static class SteakMesh
    {
        const int N = 56;   // points around the outline

        public static Mesh Build(int seed, float length = 0.16f, float width = 0.115f, float thickness = 0.03f)
        {
            var rng = new System.Random(seed);
            float Rand() => (float)rng.NextDouble();
            float p1 = Rand() * 6.2832f, p2 = Rand() * 6.2832f, p3 = Rand() * 6.2832f;
            float a1 = 0.05f + 0.05f * Rand(), a2 = 0.03f + 0.04f * Rand(), a3 = 0.015f + 0.02f * Rand();
            float h = thickness * 0.5f;

            // the outline: an ellipse with a few low-frequency wobbles, so no two steaks match
            Vector3 P(float th, float inset, float y)
            {
                float r = (1f + a1 * Mathf.Sin(2f * th + p1) + a2 * Mathf.Sin(3f * th + p2) + a3 * Mathf.Sin(5f * th + p3)) * (1f - inset);
                return new Vector3(Mathf.Cos(th) * length * 0.5f * r, y, Mathf.Sin(th) * width * 0.5f * r);
            }

            var verts = new List<Vector3>();
            int Ring(float inset, float y) { int start = verts.Count; for (int i = 0; i < N; i++) verts.Add(P(i * 6.2831853f / N, inset, y)); return start; }
            int Point(Vector3 p) { verts.Add(p); return verts.Count - 1; }

            int bo = Ring(0f, -0.7f * h), bb = Ring(0.12f, -h), cb = Point(new Vector3(0f, -h, 0f));
            int to = Ring(0f, 0.7f * h), tb = Ring(0.12f, h), ti = Ring(0.42f, 1.08f * h), ct = Point(new Vector3(0f, 1.14f * h, 0f));

            var bottom = new List<int>(); var top = new List<int>(); var side = new List<int>(); var fat = new List<int>();
            Fan(bottom, cb, bb); Strip(bottom, bb, bo);
            Fan(top, ct, ti); Strip(top, ti, tb); Strip(top, tb, to);
            Strip(side, to, bo);

            // fat cap: a bent box hugging the +x end of the outline
            const int M = 14;
            float th0 = -0.6f, th1 = 0.6f;
            int fi0 = verts.Count; for (int i = 0; i <= M; i++) verts.Add(P(Mathf.Lerp(th0, th1, i / (float)M), 0.02f, -0.72f * h));
            int fo0 = verts.Count; for (int i = 0; i <= M; i++) verts.Add(P(Mathf.Lerp(th0, th1, i / (float)M), -0.08f, -0.72f * h));
            int fo1 = verts.Count; for (int i = 0; i <= M; i++) verts.Add(P(Mathf.Lerp(th0, th1, i / (float)M), -0.08f, 0.9f * h));
            int fi1 = verts.Count; for (int i = 0; i <= M; i++) verts.Add(P(Mathf.Lerp(th0, th1, i / (float)M), 0.02f, 0.98f * h));
            for (int i = 0; i < M; i++)
            {
                Quad(fat, fi0 + i, fi0 + i + 1, fo0 + i + 1, fo0 + i);   // underside
                Quad(fat, fo0 + i, fo0 + i + 1, fo1 + i + 1, fo1 + i);   // outer wall
                Quad(fat, fo1 + i, fo1 + i + 1, fi1 + i + 1, fi1 + i);   // top
                Quad(fat, fi1 + i, fi1 + i + 1, fi0 + i + 1, fi0 + i);   // inner wall, mostly buried in the meat
            }
            Quad(fat, fi0, fo0, fo1, fi1);
            Quad(fat, fi0 + M, fi1 + M, fo1 + M, fo0 + M);

            var uvs = new List<Vector2>(verts.Count);
            foreach (var v in verts) uvs.Add(new Vector2(v.x / length + 0.5f, v.z / width + 0.5f));

            FixWinding(verts, bottom); FixWinding(verts, top); FixWinding(verts, side); FixWinding(verts, fat);

            var mesh = new Mesh { name = "Steak" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 4;
            mesh.SetTriangles(bottom, 0);
            mesh.SetTriangles(top, 1);
            mesh.SetTriangles(side, 2);
            mesh.SetTriangles(fat, 3);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static void Fan(List<int> tris, int center, int ring)
        {
            for (int i = 0; i < N; i++) { tris.Add(center); tris.Add(ring + i); tris.Add(ring + (i + 1) % N); }
        }

        static void Strip(List<int> tris, int a, int b)
        {
            for (int i = 0; i < N; i++)
            {
                int i1 = (i + 1) % N;
                tris.Add(a + i); tris.Add(a + i1); tris.Add(b + i1);
                tris.Add(a + i); tris.Add(b + i1); tris.Add(b + i);
            }
        }

        static void Quad(List<int> tris, int a, int b, int c, int d)
        {
            tris.Add(a); tris.Add(b); tris.Add(c);
            tris.Add(a); tris.Add(c); tris.Add(d);
        }

        /// Make every triangle face away from the centre, whatever order it was listed in.
        /// (Unity's front face is clockwise; for that order Cross(v1-v0, v2-v0) points along the outward normal.)
        static void FixWinding(List<Vector3> verts, List<int> tris)
        {
            for (int t = 0; t < tris.Count; t += 3)
            {
                Vector3 v0 = verts[tris[t]], v1 = verts[tris[t + 1]], v2 = verts[tris[t + 2]];
                var cross = Vector3.Cross(v1 - v0, v2 - v0);
                var centroid = (v0 + v1 + v2) / 3f;
                if (Vector3.Dot(cross, centroid) < 0f) { int tmp = tris[t + 1]; tris[t + 1] = tris[t + 2]; tris[t + 2] = tmp; }
            }
        }
    }
}
