using System.Collections.Generic;
using UnityEngine;

namespace BlockBlast
{
    /// <summary>Sprite / font access. Sliced sprites live in Assets/Resources/Art.</summary>
    public static class Art
    {
        public static readonly string[] BlockNames =
            { "red", "orange", "yellow", "green", "cyan", "blue", "navy", "purple", "magenta", "tan", "rainbow" };

        public const int Rainbow = 10;

        /// <summary>Colours dealt to regular pieces.</summary>
        public static readonly int[] PieceColors = { 0, 1, 2, 3, 4, 5, 7, 8 };

        // Palette sampled from the mockup.
        public static readonly Color BoardBorder = Hex("62CFFF");
        public static readonly Color BoardGrid = Hex("081A48");
        public static readonly Color BoardCell = Hex("0D276B");
        public static readonly Color ScoreLabel = Hex("29C5F6");
        public static readonly Color BestYellow = Hex("FFE21F");
        public static readonly Color ComboYellow = Hex("FFE840");
        public static readonly Color ComboOutline = Hex("4B2FBF");
        public static readonly Color PanelFill = Hex("14246E");
        public static readonly Color TextNavy = Hex("0B1B5C");

        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static readonly Dictionary<int, Sprite> rounded = new Dictionary<int, Sprite>();
        static Font font;

        public static Font Font => font ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static Sprite Get(string name)
        {
            if (cache.TryGetValue(name, out var s) && s) return s;
            s = Resources.Load<Sprite>("Art/" + name);
            if (!s)
            {
                // Texture imported as Default instead of Sprite: wrap it at runtime.
                var tex = Resources.Load<Texture2D>("Art/" + name);
                if (tex) s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, .5f), 100);
                else Debug.LogWarning($"[BlockBlast] Missing sprite Art/{name}");
            }
            cache[name] = s;
            return s;
        }

        public static Sprite Block(int color) => Get("block_" + BlockNames[Mathf.Clamp(color, 0, BlockNames.Length - 1)]);

        /// <summary>White anti-aliased rounded rectangle, 9-sliced so corners keep <paramref name="radius"/> UI units.</summary>
        public static Sprite RoundedRect(int radius)
        {
            radius = Mathf.Max(1, radius);
            if (rounded.TryGetValue(radius, out var s) && s) return s;
            int size = radius * 2 + 2;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            float c = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(Mathf.Abs(x + .5f - c) - (c - radius), 0);
                    float dy = Mathf.Max(Mathf.Abs(y + .5f - c) - (c - radius), 0);
                    float a = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + .5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            rounded[radius] = s;
            return s;
        }

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }
    }
}
