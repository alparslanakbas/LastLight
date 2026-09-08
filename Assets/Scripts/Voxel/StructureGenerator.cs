using UnityEngine;

namespace LastLight.Voxel
{
    /// <summary>
    /// Tek bir binayi bloklardan uretir. Bina bir mesh degil, blok dizilimi -
    /// bu yuzden yapisal butunluk sistemi onu da kapsiyor: duvarini delince
    /// ustu cokebiliyor. Hazir bir 3D bina modeli bunu yapamazdi.
    /// </summary>
    public static class StructureGenerator
    {
        /// <summary>
        /// Verilen taban alanina bir bina kurar.
        /// </summary>
        /// <param name="groundY">Binanin oturacagi zemin yuksekligi.</param>
        public static void BuildHouse(VoxelWorld world, int x0, int z0, int width, int depth,
                                      int groundY, int height, System.Random rng)
        {
            BlockId wall = rng.Next(2) == 0 ? BlockId.Concrete : BlockId.Wood;
            int doorSide = rng.Next(4);
            int doorPos = rng.Next(1, Mathf.Max(2, (doorSide < 2 ? width : depth) - 1));

            // Temel: arazi egimliyse bina havada kalmasin diye zemine kadar doldur.
            for (int x = 0; x < width; x++)
            for (int z = 0; z < depth; z++)
            {
                int wx = x0 + x, wz = z0 + z;
                for (int y = groundY; y >= 0; y--)
                {
                    if (BlockDatabase.IsSolid(world.GetBlock(wx, y, wz))) break;
                    world.SetBlock(wx, y, wz, BlockId.Concrete);
                }
                world.SetBlock(wx, groundY, wz, BlockId.Concrete);   // zemin dosemesi
            }

            // Duvarlar
            for (int y = 1; y <= height; y++)
            for (int x = 0; x < width; x++)
            for (int z = 0; z < depth; z++)
            {
                bool edge = x == 0 || z == 0 || x == width - 1 || z == depth - 1;
                if (!edge) continue;

                int wx = x0 + x, wy = groundY + y, wz = z0 + z;

                if (IsDoor(x, z, width, depth, y, doorSide, doorPos))
                {
                    world.SetBlock(wx, wy, wz, BlockId.Air);
                    continue;
                }

                // Pencereler: iki blok yukseklikte, ikide bir. Cam blogu henuz
                // yok - bosluk birakiyoruz, gecici ama isigin iceri girmesini
                // sagliyor ki bina icinde karanlik/aydinlik farki hissedilsin.
                bool windowRow = y == 2 || y == 3;
                bool windowCol = (x + z) % 3 == 0;
                if (windowRow && windowCol && !IsCorner(x, z, width, depth))
                {
                    world.SetBlock(wx, wy, wz, BlockId.Air);
                    continue;
                }

                world.SetBlock(wx, wy, wz, wall);
            }

            // Duz cati
            for (int x = 0; x < width; x++)
            for (int z = 0; z < depth; z++)
                world.SetBlock(x0 + x, groundY + height + 1, z0 + z, BlockId.Concrete);
        }

        static bool IsCorner(int x, int z, int w, int d) =>
            (x == 0 || x == w - 1) && (z == 0 || z == d - 1);

        static bool IsDoor(int x, int z, int w, int d, int y, int side, int pos)
        {
            if (y > 2) return false;   // kapi 2 blok yuksekliginde
            return side switch
            {
                0 => z == 0 && x == pos,
                1 => z == d - 1 && x == pos,
                2 => x == 0 && z == pos,
                _ => x == w - 1 && z == pos,
            };
        }
    }
}
