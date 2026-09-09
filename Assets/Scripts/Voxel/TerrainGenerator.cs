using UnityEngine;

namespace LastLight.Voxel
{
    /// <summary>
    /// Arazi ve bitki ortusu. Yukseklik alani biyomdan bagimsiz uretiliyor,
    /// biyom yalnizca engebeyi olcekliyor - boylece biyom sinirlarinda
    /// yukseklik kirilmasi olmuyor, sadece karakter degisiyor.
    /// </summary>
    public static class TerrainGenerator
    {
        const int BaseHeight = 8;
        const float NoiseScale = 0.045f;
        const int HeightRange = 7;

        /// <summary>Tek bir chunk'in bloklarini doldurur.</summary>
        public static void FillChunk(Chunk chunk, int worldX, int worldZ)
        {
            Vector3Int origin = chunk.WorldOrigin;

            for (int x = 0; x < Chunk.Size; x++)
            for (int z = 0; z < Chunk.Size; z++)
            {
                int wx = origin.x + x, wz = origin.z + z;
                BiomeType biome = BiomeMap.At(wx, wz, worldX, worldZ);
                BiomeDef def = BiomeDatabase.Get(biome);

                int height = SurfaceHeight(wx, wz, def);

                for (int y = 0; y < Chunk.Size; y++)
                {
                    int wy = origin.y + y;
                    if (wy > height) break;

                    BlockId id;
                    if (wy == height) id = def.Surface;
                    else if (wy > height - 4) id = def.SubSurface;
                    else id = BlockId.Stone;

                    chunk.Set(x, y, z, id);
                }
            }
        }

        public static int SurfaceHeight(int wx, int wz, BiomeDef def)
        {
            float n = Mathf.PerlinNoise(wx * NoiseScale, wz * NoiseScale);
            return BaseHeight + Mathf.RoundToInt(n * HeightRange * def.HeightScale);
        }

        /// <summary>
        /// Agaclari dunya olustuktan sonra ekler. Chunk doldurma sirasinda
        /// eklenemez: bir agac chunk sinirini asabiliyor ve o chunk henuz
        /// olusmamis olabiliyor.
        /// </summary>
        public static void PlantTrees(VoxelWorld world, int worldX, int worldZ, int seed)
        {
            // Agaclar artik mesh olarak serpistiriliyor (TreeScatter): bloktan
            // yapilan bir agacin silueti okunmuyordu. Blok agac uretimi
            // asagida duruyor ama cagrilmiyor - dunyaya agac blogu koymak
            // istersek tek satir silmek yetiyor.
            return;
#pragma warning disable CS0162
            var rng = new System.Random(seed ^ 0x5eed);

            for (int x = 2; x < worldX - 2; x++)
            for (int z = 2; z < worldZ - 2; z++)
            {
                BiomeType biome = BiomeMap.At(x, z, worldX, worldZ);
                BiomeDef def = BiomeDatabase.Get(biome);
                if (def.TreeDensity <= 0f) continue;
                if (rng.NextDouble() > def.TreeDensity) continue;

                int groundY = FindSurface(world, x, z);
                if (groundY <= 0) continue;
                if (world.GetBlock(x, groundY, z) != def.Surface) continue;   // yol/bina uzerine dikme

                bool bare = biome == BiomeType.BurntForest || biome == BiomeType.Wasteland;
                PlantPine(world, x, groundY, z, rng, def, bare);
            }
#pragma warning restore CS0162
        }

        /// <summary>Cam agaci: govde + daralan yaprak katmanlari. Olu biyomlarda yapraksiz.</summary>
        static void PlantPine(VoxelWorld world, int x, int groundY, int z,
                              System.Random rng, BiomeDef def, bool bare)
        {
            int trunkHeight = rng.Next(4, 8);

            for (int i = 1; i <= trunkHeight; i++)
                world.SetBlock(x, groundY + i, z, def.Trunk);

            if (bare)
            {
                // Olu agac: birkac ciplak dal yeter, siluet yeterince farkli.
                world.SetBlock(x + 1, groundY + trunkHeight, z, def.Trunk);
                world.SetBlock(x, groundY + trunkHeight, z - 1, def.Trunk);
                return;
            }

            // Yaprak konisi: alttan yukari daralan halkalar.
            int top = groundY + trunkHeight;
            for (int layer = 0; layer < 3; layer++)
            {
                int y = top - layer;
                int r = layer == 0 ? 1 : 2 - (layer == 2 ? 1 : 0);
                for (int dx = -r; dx <= r; dx++)
                for (int dz = -r; dz <= r; dz++)
                {
                    if (dx == 0 && dz == 0) continue;
                    if (Mathf.Abs(dx) + Mathf.Abs(dz) > r + 1) continue;   // kosleri kirp
                    if (!BlockDatabase.IsSolid(world.GetBlock(x + dx, y, z + dz)))
                        world.SetBlock(x + dx, y, z + dz, def.Leaf);
                }
            }

            world.SetBlock(x, top + 1, z, def.Leaf);   // tepe
        }

        static int FindSurface(VoxelWorld world, int x, int z)
        {
            for (int y = Chunk.Size * 4 - 1; y >= 0; y--)
                if (BlockDatabase.IsSolid(world.GetBlock(x, y, z))) return y;
            return 0;
        }
    }
}
