namespace LastLight.Voxel
{
    /// <summary>Blok kimlikleri. byte olarak saklanir - 256 tip yeter, bellek 4 kat az.</summary>
    public enum BlockId : byte
    {
        Air = 0,
        Dirt = 1,
        Stone = 2,
        Wood = 3,
        Metal = 4,
        Concrete = 5,
        Road = 6,
        Grass = 7,
        Sand = 8,
        Snow = 9,
        Ash = 10,
        Waste = 11,
        Leaves = 12,
    }

    /// <summary>
    /// Blogun geometrik bicimi. Voxel dunyanin "kutu yigini" gorunmesinin
    /// sebebi doku degil, her blogun kup olmasi. Rampa ve yarim blok araziyi
    /// basamak olmaktan cikariyor - referans oyunlarin yuzlerce blok bicimi
    /// tutmasinin sebebi de bu.
    /// </summary>
    public enum BlockShape : byte
    {
        Cube = 0,
        /// <summary>Alt yarim blok - yumusak yukselti.</summary>
        Slab = 1,
        /// <summary>Rampa; yonu asagidaki dort degerden biriyle veriliyor.</summary>
        RampNorth = 2,   // +Z yonune yukselir
        RampSouth = 3,   // -Z
        RampEast = 4,    // +X
        RampWest = 5,    // -X

        /// <summary>
        /// Bu voxel kup degil, YOGUNLUK ALANININ parcasi.
        ///
        /// Dogal arazi boyle isaretleniyor: yuzeyi kuplerin kenarlari degil,
        /// yogunluk degerlerinin 0.5 esigini kestigi yer belirliyor. Voxel
        /// arazinin "Minecraft" gorunmesinin asil sebebi her yukseltinin
        /// keskin bir kup kenari olmasiydi; yogunluk alani bunu tumuyle
        /// ortadan kaldiriyor.
        ///
        /// Oyuncunun koydugu bloklar Cube kaliyor - referans oyun da boyle
        /// yapiyor: arazi puruzsuz, insa edilen yapi kupsel.
        /// </summary>
        Smooth = 6,
    }

    /// <summary>
    /// Bir blok tipinin degismeyen ozellikleri.
    /// Struct + readonly dizi: Job System'e tasindiginda referans tipi sorun cikarmasin diye.
    /// </summary>
    public readonly struct BlockDef
    {
        public readonly bool IsSolid;      // false ise mesh'te yuz uretilmez ve icinden gecilir
        public readonly float Hardness;    // kirilma suresi carpani
        public readonly int Load;          // yapisal butunluk: bu blok kac birim yuk tasir
        public readonly int Mass;          // yapisal butunluk: bu blok ne kadar yuk bindirir

        public BlockDef(bool isSolid, float hardness, int load, int mass)
        {
            IsSolid = isSolid;
            Hardness = hardness;
            Load = load;
            Mass = mass;
        }
    }

    public static class BlockDatabase
    {
        // Dizi indeksi = BlockId. Sozluk degil dizi: her mesh uretiminde milyonlarca kez okunuyor.
        static readonly BlockDef[] Defs =
        {
            /* Air   */ new BlockDef(false, 0f,   0,  0),
            /* Dirt  */ new BlockDef(true,  1f,   4,  2),
            /* Stone */ new BlockDef(true,  3f,  16,  4),
            /* Wood  */ new BlockDef(true,  1.5f, 8,  1),
            /* Metal */ new BlockDef(true,  6f,  32,  3),
            /* Concr */ new BlockDef(true,  4f,  24,  5),
            /* Road  */ new BlockDef(true,  2f,  12,  3),
            /* Grass */ new BlockDef(true,  1f,   4,  2),
            /* Sand  */ new BlockDef(true,  0.8f, 2,  2),
            /* Snow  */ new BlockDef(true,  0.6f, 2,  1),
            /* Ash   */ new BlockDef(true,  0.9f, 3,  2),
            /* Waste */ new BlockDef(true,  1.2f, 4,  2),
            /* Leaves*/ new BlockDef(true,  0.3f, 1,  1),
        };

        public static BlockDef Get(BlockId id) => Defs[(int)id];

        public static bool IsSolid(BlockId id) => Defs[(int)id].IsSolid;

        public static int TypeCount => Defs.Length;
    }
}
