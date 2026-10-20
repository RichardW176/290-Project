using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Dinner
{
    /// The HUD's look: palette, generated sprites (a 9-sliced rounded rect and a circle, so pills and cards
    /// need no image files), text helpers, and little icons assembled from those shapes.
    public static class UIKit
    {
        public static readonly Color Gold = new Color(0.878f, 0.69f, 0.369f);
        public static readonly Color GoldLight = new Color(0.95f, 0.79f, 0.5f);
        public static readonly Color GoldDark = new Color(0.76f, 0.57f, 0.27f);
        public static readonly Color Cream = new Color(1f, 0.969f, 0.902f);
        public static readonly Color Beige = new Color(0.91f, 0.843f, 0.741f);
        public static readonly Color Red = new Color(0.659f, 0.259f, 0.29f);
        public static readonly Color Green = new Color(0.431f, 0.62f, 0.416f);
        public static readonly Color Burnt = new Color(0.36f, 0.24f, 0.18f);
        public static readonly Color Ink = new Color(0.27f, 0.21f, 0.17f);
        public static readonly Color Heat = new Color(0.86f, 0.42f, 0.28f);

        /// Fonts: the chunky one for headings and keys, the plain one for labels. Defaults to TMP's.
        public static TMP_FontAsset Display, Body;

        const int R = 64;   // corner radius of the generated rounded rect, in texture pixels
        static Sprite _rounded, _circle;

        public static Sprite Rounded => _rounded != null ? _rounded : (_rounded = MakeRounded());
        public static Sprite Circle => _circle != null ? _circle : (_circle = MakeCircle());

        static Sprite MakeRounded()
        {
            int n = R * 2 + 2;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "UIKit Rounded" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, R, n - R), cy = Mathf.Clamp(y + 0.5f, R, n - R);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.Clamp01(R - d + 0.5f)));
                }
            t.SetPixels32(px); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(R, R, R, R));
        }

        static Sprite MakeCircle()
        {
            int n = 128; float r = n * 0.5f;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "UIKit Circle" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.Clamp01(r - d + 0.5f)));
                }
            t.SetPixels32(px); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        // ------------------------------------------------------------------ builders
        public static RectTransform Node(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        /// Shorthand for a rect anchored and pivoted at the same point.
        public static RectTransform At(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
            => Node(name, parent, anchor, anchor, anchor, pos, size);

        /// A rounded panel; radius in canvas units (half the height makes a pill). radius 0 = plain rect.
        public static Image Panel(RectTransform rt, Color color, float radius)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            if (radius > 0f)
            {
                img.sprite = Rounded;
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = R / radius;
            }
            return img;
        }

        public static Image Dot(RectTransform rt, Color color)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Circle; img.color = color; img.raycastTarget = false;
            return img;
        }

        public static TextMeshProUGUI Label(RectTransform rt, string text, float size, Color color, TextAlignmentOptions align, bool display = false, FontStyles style = FontStyles.Normal)
        {
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            var font = display ? Display : Body;
            if (font != null) t.font = font;
            t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
            t.fontStyle = style | (display && Display == null ? FontStyles.Bold : FontStyles.Normal);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            t.richText = true;
            return t;
        }

        /// A dark edge so free-standing text reads over the bright kitchen.
        public static TextMeshProUGUI Outlined(TextMeshProUGUI t, float width = 0.2f)
        {
            // the Distance Field shader only draws these with their keywords on
            var m = t.fontMaterial;   // per-text instance
            m.EnableKeyword(ShaderUtilities.Keyword_Outline);
            t.outlineWidth = width;
            t.outlineColor = new Color32(48, 34, 26, 255);
            m.EnableKeyword(ShaderUtilities.Keyword_Underlay);   // plus a soft drop shadow
            m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0.1f, 0.07f, 0.05f, 0.7f));
            m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.6f);
            m.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.4f);
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.5f);
            t.extraPadding = true;
            t.UpdateMeshPadding();
            return t;
        }

        public static RectTransform Fill(Transform parent) => Node("Fill", parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        /// A gold key cap with a cream letter.
        public static RectTransform Key(Transform parent, string letter, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var k = At("Key " + letter, parent, anchor, pos, size);
            Panel(k, Gold, 9f);
            Label(Fill(k), letter, size.y * 0.66f, Cream, TextAlignmentOptions.Center, true, FontStyles.Bold | FontStyles.Italic);
            return k;
        }

        // ------------------------------------------------------------------ icons (built inside a square `size` box)
        static Image Part(Transform icon, string name, Vector2 pos, Vector2 size, Color c, float radius, float angle = 0f)
        {
            var rt = At(name, icon, new Vector2(0.5f, 0.5f), pos, size);
            rt.localRotation = Quaternion.Euler(0f, 0f, angle);
            return radius < 0f ? Dot(rt, c) : Panel(rt, c, radius);
        }

        public static RectTransform SaltIcon(Transform parent, Vector2 anchor, Vector2 pos, float s)
        {
            var i = At("Salt", parent, anchor, pos, new Vector2(s, s));
            Part(i, "Outline", new Vector2(0f, -s * 0.1f), new Vector2(s * 0.6f, s * 0.74f), new Color(0.62f, 0.64f, 0.67f), s * 0.14f);
            Part(i, "Glass", new Vector2(0f, -s * 0.1f), new Vector2(s * 0.5f, s * 0.66f), new Color(0.95f, 0.96f, 0.97f), s * 0.1f);
            Part(i, "Salt", new Vector2(0f, -s * 0.22f), new Vector2(s * 0.5f, s * 0.38f), Color.white, s * 0.08f);
            Part(i, "Cap", new Vector2(0f, s * 0.32f), new Vector2(s * 0.54f, s * 0.26f), new Color(0.55f, 0.57f, 0.61f), s * 0.1f);
            for (int k = -1; k <= 1; k++) Part(i, "Hole", new Vector2(k * s * 0.1f, s * 0.32f), new Vector2(s * 0.06f, s * 0.06f), new Color(0.3f, 0.31f, 0.34f), -1f);
            return i;
        }

        public static RectTransform ButterIcon(Transform parent, Vector2 anchor, Vector2 pos, float s)
        {
            var i = At("Butter", parent, anchor, pos, new Vector2(s, s));
            Part(i, "Dish", new Vector2(0f, -s * 0.24f), new Vector2(s * 0.98f, s * 0.24f), new Color(0.82f, 0.8f, 0.76f), s * 0.1f, 18f);
            Part(i, "Block", new Vector2(s * 0.02f, s * 0.02f), new Vector2(s * 0.8f, s * 0.46f), new Color(0.98f, 0.84f, 0.38f), s * 0.07f, 18f);
            Part(i, "Top", new Vector2(0f, s * 0.17f), new Vector2(s * 0.7f, s * 0.14f), new Color(1f, 0.94f, 0.66f), s * 0.05f, 18f);
            return i;
        }

        public static RectTransform SteakIcon(Transform parent, Vector2 anchor, Vector2 pos, float s)
        {
            var i = At("Steak", parent, anchor, pos, new Vector2(s, s));
            Part(i, "Fat", new Vector2(s * 0.03f, -s * 0.02f), new Vector2(s * 0.9f, s * 0.68f), new Color(0.97f, 0.9f, 0.8f), s * 0.3f, -25f);
            Part(i, "Meat", new Vector2(-s * 0.02f, s * 0.02f), new Vector2(s * 0.78f, s * 0.56f), new Color(0.78f, 0.18f, 0.2f), s * 0.26f, -25f);
            Part(i, "Bone", new Vector2(s * 0.04f, s * 0.0f), new Vector2(s * 0.42f, s * 0.08f), new Color(0.98f, 0.94f, 0.86f), s * 0.04f, 35f);
            Part(i, "Marrow", new Vector2(-s * 0.22f, -s * 0.12f), new Vector2(s * 0.14f, s * 0.14f), new Color(0.98f, 0.94f, 0.86f), -1f);
            return i;
        }

        public static RectTransform FriendsIcon(Transform parent, Vector2 anchor, Vector2 pos, float s)
        {
            var i = At("Friends", parent, anchor, pos, new Vector2(s, s));
            var back = new Color(0.71f, 0.64f, 0.5f);
            Part(i, "BackBody", new Vector2(-s * 0.22f, -s * 0.3f), new Vector2(s * 0.36f, s * 0.4f), back, s * 0.14f);
            Part(i, "BackHead", new Vector2(-s * 0.22f, s * 0.04f), new Vector2(s * 0.28f, s * 0.28f), back, -1f);
            Part(i, "FrontBody", new Vector2(s * 0.12f, -s * 0.24f), new Vector2(s * 0.48f, s * 0.52f), Beige, s * 0.2f);
            Part(i, "FrontFeet", new Vector2(s * 0.12f, -s * 0.44f), new Vector2(s * 0.48f, s * 0.12f), Beige, 0f);
            Part(i, "FrontHead", new Vector2(s * 0.12f, s * 0.24f), new Vector2(s * 0.4f, s * 0.4f), Beige, -1f);
            return i;
        }

        public static RectTransform PanIcon(Transform parent, Vector2 anchor, Vector2 pos, float s)
        {
            var i = At("Pan", parent, anchor, pos, new Vector2(s, s));
            Part(i, "Handle", new Vector2(s * 0.3f, -s * 0.2f), new Vector2(s * 0.5f, s * 0.12f), new Color(0.45f, 0.3f, 0.2f), s * 0.06f, -35f);
            Part(i, "Rim", new Vector2(-s * 0.1f, s * 0.08f), new Vector2(s * 0.72f, s * 0.72f), new Color(0.35f, 0.36f, 0.4f), -1f);
            Part(i, "Floor", new Vector2(-s * 0.1f, s * 0.08f), new Vector2(s * 0.58f, s * 0.58f), new Color(0.5f, 0.52f, 0.56f), -1f);
            Part(i, "Steak", new Vector2(-s * 0.1f, s * 0.08f), new Vector2(s * 0.36f, s * 0.26f), new Color(0.72f, 0.3f, 0.22f), s * 0.12f, -20f);
            return i;
        }
    }
}
