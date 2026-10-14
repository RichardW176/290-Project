using UnityEngine;
using UnityEngine.Rendering;

namespace Dinner
{
    /// Runtime material and primitive helpers. Everything the prototype adds to the kitchen
    /// (pan, steaks, friends, particles) is built from these so no extra art is needed.
    public static class Visuals
    {
        static Shader _lit, _particle;
        static Texture2D _softCircle, _marbling;

        static Shader Lit => _lit ??= Shader.Find("Universal Render Pipeline/Lit");
        static Shader Particle => _particle ??= Shader.Find("Universal Render Pipeline/Particles/Unlit");

        public static Material Opaque(Color color, float smoothness = 0.35f, float metallic = 0f)
        {
            var m = new Material(Lit);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            return m;
        }

        public static Material OpaqueTextured(Color tint, Texture2D map, float smoothness = 0.35f)
        {
            var m = Opaque(tint, smoothness);
            m.SetTexture("_BaseMap", map);
            return m;
        }

        /// Faint fat veins and mottling for the cut faces of a steak; multiplied by the face tint.
        public static Texture2D Marbling()
        {
            if (_marbling != null) return _marbling;
            const int n = 128;
            _marbling = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = x / (float)n, v = y / (float)n;
                    float a = Mathf.PerlinNoise(u * 4.2f + 11.3f, v * 4.2f + 7.1f);
                    float b = Mathf.PerlinNoise(u * 9f + 3.7f, v * 9f + 5.9f);
                    float vein = Mathf.Clamp01(1f - Mathf.Abs(a - 0.5f) / 0.045f) * (0.4f + 0.6f * b);
                    float meat = 0.8f + 0.14f * b;
                    float k = Mathf.Lerp(meat, 1f, vein);
                    _marbling.SetPixel(x, y, new Color(k, Mathf.Lerp(k * 0.9f, 1f, vein), Mathf.Lerp(k * 0.88f, 1f, vein), 1f));
                }
            _marbling.Apply();
            return _marbling;
        }

        public static Material ParticleSoft(Color color)
        {
            var m = new Material(Particle);
            m.SetColor("_BaseColor", color);
            m.SetTexture("_BaseMap", SoftCircle());
            // URP "transparent / alpha blend" surface setup, done by hand since there's no inspector.
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        /// Radial falloff sprite for steam, smoke and grease.
        public static Texture2D SoftCircle()
        {
            if (_softCircle != null) return _softCircle;
            const int n = 48;
            _softCircle = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2f);
                    _softCircle.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            _softCircle.Apply();
            return _softCircle;
        }

        public static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPos,
                                           Vector3 localScale, Material mat, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (!collider) Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.On;
            return go;
        }

        public static GameObject Empty(string name, Transform parent, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go;
        }

        /// A particle system with sane defaults; callers tweak emission/colour afterwards.
        public static ParticleSystem Particles(string name, Transform parent, Vector3 localPos, Color color,
                                               float size, float speed, float life, float radius, float rate)
        {
            var go = Empty(name, parent, localPos);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = life;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;
            main.playOnAwake = true;
            var em = ps.emission;
            em.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = radius;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.6f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = ParticleSoft(Color.white);
            r.renderMode = ParticleSystemRenderMode.Billboard;
            return ps;
        }

        public static Color Lerp3(Color a, Color b, Color c, float t)
            => t < 0.5f ? Color.Lerp(a, b, t * 2f) : Color.Lerp(b, c, (t - 0.5f) * 2f);
    }
}
