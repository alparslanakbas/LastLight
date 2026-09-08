using UnityEngine;

namespace LastLight.Voxel
{
    public enum BiomeType : byte
    {
        Forest = 0,       // baslangic bolgesi: tehlikesiz, temel kaynak
        Desert = 1,       // gece daha karanlik, yakit bol
        Snow = 2,         // soguk yakiti hizli tuketir
        BurntForest = 3,  // felaketin kenari, sis
        Wasteland = 4,    // sehrin merkezi: en tehlikeli, en iyi kaynak
    }

    public readonly struct BiomeDef
    {
        public readonly BlockId Surface;      // en ust katman
        public readonly BlockId SubSurface;   // hemen altindaki birkac blok
        public readonly float TreeDensity;    // 0-1, blok basina agac olasiligi
        public readonly BlockId Trunk;
        public readonly BlockId Leaf;
        public readonly float HeightScale;    // arazi engebeliligi carpani

        /// <summary>Isik yakitinin bu biyomda tukenme hizi carpani.</summary>
        public readonly float FuelDrain;

        public BiomeDef(BlockId surface, BlockId sub, float treeDensity,
                        BlockId trunk, BlockId leaf, float heightScale, float fuelDrain)
        {
            Surface = surface; SubSurface = sub; TreeDensity = treeDensity;
            Trunk = trunk; Leaf = leaf; HeightScale = heightScale; FuelDrain = fuelDrain;
        }
    }

    public static class BiomeDatabase
    {
        static readonly BiomeDef[] Defs =
        {
            // Forest: yogun agac, orta engebe, referans yakit tuketimi
            new BiomeDef(BlockId.Grass, BlockId.Dirt, 0.075f, BlockId.Wood, BlockId.Leaves, 1.0f, 1.0f),
            // Desert: agacsiz, duz, gece karanlik oldugu icin yakit biraz daha hizli gider
            new BiomeDef(BlockId.Sand,  BlockId.Sand, 0.004f, BlockId.Wood, BlockId.Leaves, 0.6f, 1.2f),
            // Snow: agacli ama seyrek, soguk yakiti hizli tuketiyor
            new BiomeDef(BlockId.Snow,  BlockId.Dirt, 0.040f, BlockId.Wood, BlockId.Leaves, 1.3f, 1.5f),
            // BurntForest: olu agac govdeleri, yapraksiz
            new BiomeDef(BlockId.Ash,   BlockId.Dirt, 0.030f, BlockId.Wood, BlockId.Ash,   1.1f, 1.3f),
            // Wasteland: cıplak, moloz, en zor
            new BiomeDef(BlockId.Waste, BlockId.Stone, 0.002f, BlockId.Wood, BlockId.Ash,  0.8f, 1.8f),
        };

        public static BiomeDef Get(BiomeType t) => Defs[(int)t];
    }

    /// <summary>
    /// Konumdan biyom belirler. Duzen radyal: sehir merkezde ve etrafi corak,
    /// disa dogru yanik orman, en disarida yone gore orman / col / kar.
    ///
    /// Neden radyal: felaket sehirde oldu anlatisi hem biyom gecisini
    /// gerekcelendiriyor hem de oyuncuya net bir risk gradyani veriyor -
    /// merkeze yaklastikca tehlike ve odul artiyor.
    /// </summary>
    public static class BiomeMap
    {
        public static BiomeType At(int x, int z, int worldX, int worldZ)
        {
            float cx = worldX * 0.5f, cz = worldZ * 0.5f;
            float dx = (x - cx) / cx, dz = (z - cz) / cz;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);   // 0 = merkez, ~1 = kenar

            // Sinirlari noise ile bozuyoruz; tam daire yapay duruyor.
            float wobble = (Mathf.PerlinNoise(x * 0.03f, z * 0.03f) - 0.5f) * 0.22f;
            dist += wobble;

            if (dist < 0.42f) return BiomeType.Wasteland;
            if (dist < 0.60f) return BiomeType.BurntForest;

            // Dis halka yone gore uce bolunuyor.
            float angle = Mathf.Atan2(z - cz, x - cx) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;

            if (angle < 120f) return BiomeType.Desert;
            if (angle < 240f) return BiomeType.Snow;
            return BiomeType.Forest;
        }
    }
}
