using System.IO;
using LastLight.Voxel;
using UnityEditor;
using UnityEngine;

namespace ProjectBootstrap
{
    /// <summary>
    /// Blok dokularini kod ile uretip tek bir atlas dosyasina yazar.
    ///
    /// Neden prosedurel: varlik butcesi sifir ve hazir doku paketleri hem
    /// birbirine uymuyor hem lisans takibi gerektiriyor. Uretilen dokular
    /// ayni gurultu fonksiyonundan ciktigi icin dogal olarak tutarli.
    ///
    /// Neden atlas: tek doku = tek malzeme = tek draw call. Blok tipi basina
    /// ayri malzeme kullanmak chunk basina 13 draw call demekti.
    /// </summary>
    public static class TextureAtlasGenerator
    {
        public const int TileSize = 16;      // blok basina piksel
        public const int AtlasTiles = 4;     // 4x4 = 16 hucre
        const int AtlasSize = TileSize * AtlasTiles;

        const string AtlasPath = "Assets/Textures/BlockAtlas.png";

        [MenuItem("LastLight/Blok Dokularini Uret")]
        public static void Generate()
        {
            var atlas = new Texture2D(AtlasSize, AtlasSize, TextureFormat.RGBA32, false);

            for (int id = 0; id < AtlasTiles * AtlasTiles; id++)
            {
                int tx = id % AtlasTiles;
                int ty = id / AtlasTiles;
                var pixels = MakeTile((BlockId)id, id);

                for (int y = 0; y < TileSize; y++)
                for (int x = 0; x < TileSize; x++)
                    atlas.SetPixel(tx * TileSize + x, ty * TileSize + y, pixels[y * TileSize + x]);
            }

            atlas.Apply();

            var dir = Path.GetDirectoryName(AtlasPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(AtlasPath, atlas.EncodeToPNG());
            Object.DestroyImmediate(atlas);

            AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceUpdate);

            // Point filtre sart: bilinear olursa komsu hucreler birbirine
            // karisiyor ve blok kenarlarinda yabanci renkler beliriyor.
            var importer = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            Debug.Log("[Atlas] Blok dokulari uretildi: " + AtlasPath);
        }

        const string GrassPath = "Assets/Textures/GrassBlade.png";

        /// <summary>
        /// Bitki ortusu icin alfa kanalli cim dokusu. Gercek bir doku
        /// geldiginde bu dosyanin uzerine yazilir - sistem doku kaynagini
        /// bilmiyor, sadece bu yolu okuyor.
        /// </summary>
        [MenuItem("LastLight/Cim Dokusu Uret")]
        public static void GenerateGrassBlade()
        {
            const int W = 48, H = 48;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            var rng = new System.Random(99);

            // Once tamamen seffaf
            var clear = new Color[W * H];
            for (int i = 0; i < clear.Length; i++) clear[i] = new Color(0, 0, 0, 0);
            tex.SetPixels(clear);

            // Birkac dikey cim teli: alttan kalin, yukari dogru incelen ve
            // hafif egilen seritler.
            int blades = 9;
            for (int b = 0; b < blades; b++)
            {
                float baseX = 3f + (float)rng.NextDouble() * (W - 6f);
                float lean = ((float)rng.NextDouble() - 0.5f) * 9f;
                int height = (int)(H * (0.45f + rng.NextDouble() * 0.5f));
                float thick = 1.6f + (float)rng.NextDouble() * 1.4f;

                // Tabanda koyu, ucta acik yesil - dogal derinlik
                Color tip = new Color(0.44f + (float)rng.NextDouble() * 0.12f, 0.68f, 0.26f, 1f);
                Color root = new Color(0.16f, 0.32f, 0.13f, 1f);

                for (int y = 0; y < height; y++)
                {
                    float t = y / (float)height;
                    float x = baseX + lean * t * t;
                    float w = Mathf.Lerp(thick, 0.4f, t);
                    Color c = Color.Lerp(root, tip, t);

                    for (int dx = -Mathf.CeilToInt(w); dx <= Mathf.CeilToInt(w); dx++)
                    {
                        int px = Mathf.RoundToInt(x) + dx;
                        if (px < 0 || px >= W) continue;
                        float edge = 1f - Mathf.Abs(dx) / (w + 0.5f);
                        if (edge <= 0f) continue;
                        tex.SetPixel(px, y, new Color(c.r, c.g, c.b, Mathf.Clamp01(edge * 1.6f)));
                    }
                }
            }

            tex.Apply();
            var dir = Path.GetDirectoryName(GrassPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(GrassPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(GrassPath, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(GrassPath);
            imp.alphaIsTransparency = true;
            imp.filterMode = FilterMode.Bilinear;   // cim kenarlari yumusak olsun
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();

            Debug.Log("[Atlas] Cim dokusu uretildi: " + GrassPath);
        }

        static Color[] MakeTile(BlockId id, int seed)
        {
            var px = new Color[TileSize * TileSize];
            var rng = new System.Random(seed * 7919 + 13);

            Color baseColor = BaseColor(id);

            for (int y = 0; y < TileSize; y++)
            for (int x = 0; x < TileSize; x++)
            {
                float v = 0f;

                switch (id)
                {
                    case BlockId.Wood:
                        // Dikey damar: x'e bagli sinus + hafif gurultu
                        v = Mathf.Sin(x * 1.9f) * 0.05f + Noise(rng) * 0.05f;
                        if (x % 7 == 3) v -= 0.07f;
                        break;

                    case BlockId.Metal:
                        // Yatay tarama cizgileri, metalik his
                        v = (y % 4 == 0 ? -0.06f : 0.02f) + Noise(rng) * 0.03f;
                        break;

                    case BlockId.Road:
                        // Asfalt: yogun ince benek
                        v = Noise(rng) * 0.16f;
                        break;

                    case BlockId.Stone:
                        // Iri benek + seyrek catlak
                        v = Noise(rng) * 0.13f;
                        if (rng.Next(28) == 0) v -= 0.14f;
                        break;

                    case BlockId.Leaves:
                        // Duzensiz koyu bosluklar - yaprak arasi golge
                        v = Noise(rng) * 0.12f;
                        if (rng.Next(5) == 0) v -= 0.16f;
                        break;

                    case BlockId.Snow:
                        // Neredeyse duz, cok hafif kirilma
                        v = Noise(rng) * 0.04f;
                        break;

                    case BlockId.Sand:
                        v = Noise(rng) * 0.08f;
                        break;

                    case BlockId.Grass:
                        v = Noise(rng) * 0.11f;
                        if (rng.Next(9) == 0) v += 0.07f;
                        break;

                    case BlockId.Concrete:
                        v = Noise(rng) * 0.06f;
                        if (x == 0 || y == 0) v -= 0.05f;   // panel derzi
                        break;

                    default:
                        v = Noise(rng) * 0.10f;
                        break;
                }

                // Blok kenarlarini hafif karart: bitisik bloklarin siniri
                // belli olsun, yoksa ayni tipten duvar tek buyuk leke gibi duruyor.
                if (x == 0 || y == 0 || x == TileSize - 1 || y == TileSize - 1)
                    v -= 0.045f;

                px[y * TileSize + x] = new Color(
                    Mathf.Clamp01(baseColor.r + v),
                    Mathf.Clamp01(baseColor.g + v),
                    Mathf.Clamp01(baseColor.b + v),
                    1f);
            }

            return px;
        }

        static float Noise(System.Random rng) => (float)rng.NextDouble() - 0.5f;

        /// <summary>Malzeme renkleriyle ayni palet - sahnedeki uyum bozulmasin.</summary>
        static Color BaseColor(BlockId id) => id switch
        {
            BlockId.Dirt => new Color(0.42f, 0.31f, 0.20f),
            BlockId.Stone => new Color(0.48f, 0.49f, 0.52f),
            BlockId.Wood => new Color(0.56f, 0.38f, 0.21f),
            BlockId.Metal => new Color(0.58f, 0.61f, 0.65f),
            BlockId.Concrete => new Color(0.62f, 0.61f, 0.58f),
            BlockId.Road => new Color(0.20f, 0.20f, 0.22f),
            BlockId.Grass => new Color(0.33f, 0.48f, 0.24f),
            BlockId.Sand => new Color(0.78f, 0.70f, 0.46f),
            BlockId.Snow => new Color(0.90f, 0.93f, 0.96f),
            BlockId.Ash => new Color(0.28f, 0.26f, 0.25f),
            BlockId.Waste => new Color(0.44f, 0.35f, 0.28f),
            BlockId.Leaves => new Color(0.18f, 0.34f, 0.18f),
            _ => new Color(0.5f, 0.5f, 0.5f),
        };
    }
}
