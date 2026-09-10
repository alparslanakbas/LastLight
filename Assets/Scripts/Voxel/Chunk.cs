using UnityEngine;

namespace LastLight.Voxel
{
    /// <summary>
    /// Dunyanin 16x16x16'lik bir parcasi. Bloklar tek boyutlu dizide tutulur -
    /// [x,y,z] uc boyutlu dizi C#'ta her erisimde sinir kontrolu yapar ve Job'a gecirilemez.
    /// </summary>
    public sealed class Chunk
    {
        public const int Size = 16;
        public const int BlockCount = Size * Size * Size;

        public readonly Vector3Int Coord;   // chunk uzayindaki konum (dunya konumu = Coord * Size)
        readonly BlockId[] _blocks = new BlockId[BlockCount];

        // Bicim ayri dizide: blok kimligiyle ayni byte'a sikistirmak
        // (ust 3 bit bicim, alt 5 bit tip) bellek kazandirirdi ama her
        // okumada maskeleme gerektiriyor ve mesher bunu milyonlarca kez
        // yapiyor. Ayri dizi daha hizli ve okunakli.
        readonly BlockShape[] _shapes = new BlockShape[BlockCount];

        // Yogunluk: 0 = bos, 255 = tamamen dolu, esik 128.
        // byte yeterli - 256 kademe arazi yuzeyi icin fazlasiyla ince, float
        // tutmak dort kat bellek ve kayit dosyasi demekti (256 chunk x 4096).
        readonly byte[] _density = new byte[BlockCount];

        /// <summary>Mesh'in yeniden uretilmesi gerekiyor mu.</summary>
        public bool Dirty { get; set; } = true;

        public Chunk(Vector3Int coord) => Coord = coord;

        /// <summary>3B yerel koordinati duz dizi indeksine cevirir.</summary>
        public static int Index(int x, int y, int z) => x + Size * (y + Size * z);

        public static bool InBounds(int x, int y, int z) =>
            x >= 0 && x < Size && y >= 0 && y < Size && z >= 0 && z < Size;

        public BlockId Get(int x, int y, int z) =>
            InBounds(x, y, z) ? _blocks[Index(x, y, z)] : BlockId.Air;

        public void Set(int x, int y, int z, BlockId id)
        {
            if (!InBounds(x, y, z)) return;
            int i = Index(x, y, z);
            _blocks[i] = id;
            _shapes[i] = BlockShape.Cube;   // tip degisince bicim sifirlanir
            Dirty = true;
        }

        public BlockShape GetShape(int x, int y, int z) =>
            InBounds(x, y, z) ? _shapes[Index(x, y, z)] : BlockShape.Cube;

        public void SetShape(int x, int y, int z, BlockShape shape)
        {
            if (!InBounds(x, y, z)) return;
            _shapes[Index(x, y, z)] = shape;
            Dirty = true;
        }

        /// <summary>Mesher'in dogrudan okumasi icin - kopyalama maliyetinden kacinir.</summary>
        public BlockId[] Raw => _blocks;

        /// <summary>Kayit/yukleme icin ham bicim dizisi.</summary>
        public BlockShape[] RawShapes => _shapes;

        /// <summary>Kayit/yukleme ve mesher icin ham yogunluk dizisi.</summary>
        public byte[] RawDensity => _density;

        public byte GetDensity(int x, int y, int z) =>
            InBounds(x, y, z) ? _density[Index(x, y, z)] : (byte)0;

        public void SetDensity(int x, int y, int z, byte value)
        {
            if (!InBounds(x, y, z)) return;
            _density[Index(x, y, z)] = value;
            Dirty = true;
        }

        /// <summary>
        /// Tip ve yogunlugu birlikte yazar - arazi uretimi icin.
        /// Set() bicimi Cube'a sifirladigi icin ayri bir yol gerekiyor.
        /// </summary>
        public void SetSmooth(int x, int y, int z, BlockId id, byte density)
        {
            if (!InBounds(x, y, z)) return;
            int i = Index(x, y, z);
            _blocks[i] = id;
            _shapes[i] = BlockShape.Smooth;
            _density[i] = density;
            Dirty = true;
        }

        /// <summary>Yuklemede tum chunk'i tek seferde yazar.</summary>
        public void LoadRaw(BlockId[] blocks, BlockShape[] shapes, byte[] density)
        {
            System.Array.Copy(blocks, _blocks, BlockCount);
            System.Array.Copy(shapes, _shapes, BlockCount);
            System.Array.Copy(density, _density, BlockCount);
            Dirty = true;
        }

        public Vector3Int WorldOrigin => Coord * Size;
    }
}
