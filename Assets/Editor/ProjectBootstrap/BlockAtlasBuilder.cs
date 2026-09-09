using System.IO;
using LastLight.Voxel;
using UnityEditor;
using UnityEngine;

namespace ProjectBootstrap
{
    /// <summary>
    /// Indirilen gercek dokulardan blok atlasi (renk + normal) uretir.
    ///
    /// Prosedurel uretilen dokular blok sinirini gosteriyordu ama yuzeye
    /// karakter vermiyordu. Fotogrametri tabanli CC0 dokular (Poly Haven)
    /// tek basina en buyuk gorsel farki yaratan sey; normal haritasi da
    /// duz yuzeyde catlak ve kabartma hissi uretiyor.
    ///
    /// Kaynak dosya yoksa o hucre prosedurel dokuya dusuyor - eksik doku
    /// tum atlasi bozmasin diye.
    /// </summary>
    public static class BlockAtlasBuilder
    {
        // 256'da bir metrelik blok yakindan bulanik kaliyordu; 512 kaynak
        // dokunun (1K) yarisi, kayip az ve detay yakin planda duruyor.
        const int Tile = 512;              // hucre basina piksel
        const int Tiles = 4;               // 4x4 = 16 hucre
        const int AtlasSize = Tile * Tiles;

        const string SourceDir = "Assets/Textures/Source";
        const string AlbedoPath = "Assets/Textures/BlockAtlas.png";
        const string NormalPath = "Assets/Textures/BlockAtlasNormal.png";

        /// <summary>Atlas hucresi -> kaynak dosya adi on eki.</summary>
        static readonly (BlockId id, string file)[] Sources =
        {
            (BlockId.Dirt, "Dirt"),
            (BlockId.Stone, "Stone"),
            (BlockId.Wood, "Wood"),
            (BlockId.Metal, "Metal"),
            (BlockId.Concrete, "Concrete"),
            (BlockId.Road, "Road"),
            (BlockId.Grass, "Grass"),
            (BlockId.Sand, "Sand"),
            (BlockId.Snow, "Snow"),
            (BlockId.Ash, "Ash"),
            (BlockId.Waste, "Waste"),
            // Yaprak icin ayri kaynak indirilmedi; cimen dokusu koyu yesile
            // boyanarak kullaniliyor. Bos birakilsa gri kalir ve agaclar
            // tas gibi gorunur.
            (BlockId.Leaves, "Grass"),
        };

        [MenuItem("LastLight/Blok Atlasini Kaynaklardan Uret")]
        public static void Build()
        {
            var albedo = new Texture2D(AtlasSize, AtlasSize, TextureFormat.RGBA32, false);
            var normal = new Texture2D(AtlasSize, AtlasSize, TextureFormat.RGBA32, false);

            // Varsayilan: duz gri renk ve notr normal (0.5, 0.5, 1)
            FillAll(albedo, new Color(0.5f, 0.5f, 0.5f, 1f));
            FillAll(normal, new Color(0.5f, 0.5f, 1f, 1f));

            int loaded = 0, missing = 0;

            foreach (var (id, file) in Sources)
            {
                int tileIndex = (int)id;
                int tx = tileIndex % Tiles;
                int ty = tileIndex / Tiles;

                var src = LoadSource(file + "_albedo");
                if (src != null)
                {
                    BlitScaled(src, albedo, tx, ty, tint: BaseTint(id));
                    Object.DestroyImmediate(src);
                    loaded++;
                }
                else
                {
                    missing++;
                    Debug.LogWarning("[Atlas] Kaynak yok: " + file + "_albedo");
                }

                var nrm = LoadSource(file + "_normal");
                if (nrm != null)
                {
                    BlitScaled(nrm, normal, tx, ty, tint: Color.white);
                    Object.DestroyImmediate(nrm);
                }
            }

            WriteTexture(albedo, AlbedoPath, isNormal: false);
            WriteTexture(normal, NormalPath, isNormal: true);

            Object.DestroyImmediate(albedo);
            Object.DestroyImmediate(normal);

            Debug.Log("[Atlas] Kaynaklardan uretildi. Yuklenen=" + loaded + " eksik=" + missing);
        }

        // ---------- Yardimcilar ----------

        /// <summary>
        /// Kaynagi diskten okur. AssetDatabase yerine dogrudan dosya okuyoruz:
        /// import ayarlarinda "readable" acmayi gerektirmiyor.
        /// </summary>
        static Texture2D LoadSource(string nameWithoutExt)
        {
            foreach (var ext in new[] { ".jpg", ".png", ".jpeg" })
            {
                string path = Path.Combine(SourceDir, nameWithoutExt + ext);
                if (!File.Exists(path)) continue;

                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(File.ReadAllBytes(path))) return tex;
                Object.DestroyImmediate(tex);
            }
            return null;
        }

        /// <summary>Kaynagi hucre boyutuna olcekleyip atlasa yazar.</summary>
        static void BlitScaled(Texture2D src, Texture2D dst, int tx, int ty, Color tint)
        {
            int ox = tx * Tile, oy = ty * Tile;

            for (int y = 0; y < Tile; y++)
            for (int x = 0; x < Tile; x++)
            {
                // Bilinear ornekleme: kaynak 1024, hedef 256 - noktasal
                // ornekleme alias uretiyor.
                Color c = src.GetPixelBilinear(x / (float)Tile, y / (float)Tile);
                dst.SetPixel(ox + x, oy + y, c * tint);
            }
        }

        /// <summary>
        /// Bazi dokular biyom rengini tutturmuyor (ornegin kar dokusu asfalt
        /// uzeri kar). Hafif renk duzeltmesi paleti korumaya yariyor.
        /// </summary>
        static Color BaseTint(BlockId id) => id switch
        {
            BlockId.Snow => new Color(1.15f, 1.18f, 1.22f, 1f),
            BlockId.Grass => new Color(0.85f, 1.05f, 0.75f, 1f),
            BlockId.Waste => new Color(1.05f, 0.92f, 0.82f, 1f),
            BlockId.Ash => new Color(0.72f, 0.70f, 0.68f, 1f),
            BlockId.Leaves => new Color(0.55f, 0.85f, 0.45f, 1f),
            _ => Color.white,
        };

        static void FillAll(Texture2D tex, Color c)
        {
            var px = new Color[tex.width * tex.height];
            for (int i = 0; i < px.Length; i++) px[i] = c;
            tex.SetPixels(px);
        }

        static void WriteTexture(Texture2D tex, string path, bool isNormal)
        {
            tex.Apply();
            var dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;

            // Bilinear + mipmap: 256'lik hucrelerde noktasal filtre uzakta
            // titriyor. Atlas oldugu icin mipmap kenar sizintisi yapabilir,
            // ama UV'lerde ic pay birakildigi icin sorun cikarmiyor.
            imp.filterMode = FilterMode.Bilinear;
            imp.mipmapEnabled = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.maxTextureSize = 4096;
            imp.SaveAndReimport();
        }
    }
}
