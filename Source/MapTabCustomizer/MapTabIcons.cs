using System.Collections.Generic;
using UnityEngine;

namespace MapTabCustomizer
{
    internal static class MapTabIcons
    {
        internal static readonly string[] Names = { "None", "Home", "Industry", "Medical", "Defense", "Research" };
        private static readonly List<Texture2D> textures = new List<Texture2D>();

        internal static Texture2D Get(int index)
        {
            if (index <= 0 || index >= Names.Length) return null;
            EnsureCreated();
            return textures[index - 1];
        }

        private static void EnsureCreated()
        {
            if (textures.Count > 0) return;
            textures.Add(CreateIcon(new[] { "00100", "01110", "11111", "10101", "11111" }));
            textures.Add(CreateIcon(new[] { "10100", "11100", "11111", "10101", "11111" }));
            textures.Add(CreateIcon(new[] { "00100", "00100", "11111", "00100", "00100" }));
            textures.Add(CreateIcon(new[] { "01110", "11011", "10101", "11011", "01110" }));
            textures.Add(CreateIcon(new[] { "10101", "01110", "00100", "01110", "10101" }));
        }

        private static Texture2D CreateIcon(string[] pixels)
        {
            const int scale = 4;
            Texture2D texture = new Texture2D(20, 20, TextureFormat.ARGB32, false);
            texture.name = "MapTabCustomizerIcon";
            texture.filterMode = FilterMode.Point;
            Color clear = new Color(0f, 0f, 0f, 0f);
            Color ink = Color.white;
            for (int y = 0; y < 20; y++)
            for (int x = 0; x < 20; x++)
                texture.SetPixel(x, y, pixels[4 - y / scale][x / scale] == '1' ? ink : clear);
            texture.Apply();
            return texture;
        }
    }
}
