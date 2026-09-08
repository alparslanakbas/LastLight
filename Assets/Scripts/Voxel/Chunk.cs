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
            _blocks[Index(x, y, z)] = id;
            Dirty = true;
        }

        /// <summary>Mesher'in dogrudan okumasi icin - kopyalama maliyetinden kacinir.</summary>
        public BlockId[] Raw => _blocks;

        public Vector3Int WorldOrigin => Coord * Size;
    }
}
