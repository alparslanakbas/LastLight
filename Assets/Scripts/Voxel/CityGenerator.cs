using UnityEngine;

namespace LastLight.Voxel
{
    /// <summary>
    /// Yol izgarasi, parseller ve binalar uretir. Merkezde sik dokulu bir
    /// sehir, disinda daginik kasabalar var - keşif hissi mesafeyle degil
    /// yogunluk farkiyla uretiliyor, cunku dunya kucuk.
    /// </summary>
    public static class CityGenerator
    {
        const int LotSize = 14;      // parsel kenari
        const int RoadWidth = 4;     // yol genisligi
        const int BlendWidth = 16;   // sehir duzlugunden dogal araziye gecis seridi
        const int Pitch = LotSize + RoadWidth;

        public static void Generate(VoxelWorld world, int worldX, int worldZ, int seed)
        {
            var rng = new System.Random(seed);

            // Sehir dunyanin ortasinda; kenarlarda dogal arazi kaliyor ki
            // oyuncu sehre "varmis" hissetsin.
            // Sehir dunyanin ortasinda kucuk bir alan: once dunyanin yarisini
            // kapliyordu ve biyomlar kenar seridine sikismisti. Sehir kucukken
            // "sehre varmak" bir olay oluyor, buyukken dunyanin kendisi oluyor.
            int margin = Mathf.RoundToInt(Mathf.Min(worldX, worldZ) * 0.30f);
            int cityMinX = margin, cityMaxX = worldX - margin;
            int cityMinZ = margin, cityMaxZ = worldZ - margin;

            // Once tesviye: sehir duz bir zemine kuruluyor. Tesviyesiz birakinca
            // yollar arazinin tepelerini basamak basamak takip ediyor ve sehir
            // dagilmis gibi gorunuyor.
            FlattenCity(world, cityMinX, cityMaxX, cityMinZ, cityMaxZ);

            BuildRoads(world, cityMinX, cityMaxX, cityMinZ, cityMaxZ);
            BuildLots(world, cityMinX, cityMaxX, cityMinZ, cityMaxZ, rng);
            BuildOutskirts(world, worldX, worldZ, margin, rng);
        }

        // ---------- Tesviye ----------

        /// <summary>
        /// Sehir alanini ortalama yuksekligine duzler. Kenarda BlendWidth
        /// genisliginde gecis serid birakiliyor - sert bir plato kenari
        /// yapay duruyordu.
        /// </summary>
        static void FlattenCity(VoxelWorld world, int minX, int maxX, int minZ, int maxZ)
        {
            long total = 0;
            int n = 0;
            for (int x = minX; x < maxX; x += 2)
            for (int z = minZ; z < maxZ; z += 2)
            {
                total += FindSurface(world, x, z);
                n++;
            }
            if (n == 0) return;
            int level = Mathf.RoundToInt(total / (float)n);

            int outerMinX = minX - BlendWidth, outerMaxX = maxX + BlendWidth;
            int outerMinZ = minZ - BlendWidth, outerMaxZ = maxZ + BlendWidth;

            for (int x = outerMinX; x < outerMaxX; x++)
            for (int z = outerMinZ; z < outerMaxZ; z++)
            {
                int natural = FindSurface(world, x, z);

                // Kenara olan uzaklik: sehir icinde 1, gecis seridinde 0'a iniyor.
                int dx = Mathf.Min(x - outerMinX, outerMaxX - 1 - x);
                int dz = Mathf.Min(z - outerMinZ, outerMaxZ - 1 - z);
                float t = Mathf.Clamp01(Mathf.Min(dx, dz) / (float)BlendWidth);
                int target = Mathf.RoundToInt(Mathf.Lerp(natural, level, t));

                if (target > natural)
                {
                    // Dolgu, o noktanin kendi yuzey blogunu kullaniyor -
                    // sabit toprak koyunca corak bolgenin ortasinda kahverengi
                    // yamalar olusuyordu.
                    BlockId fill = world.GetBlock(x, natural, z);
                    if (!BlockDatabase.IsSolid(fill)) fill = BlockId.Stone;

                    // SetTerrain, SetBlock degil: tesviye ARAZIYI
                    // degistiriyor. SetBlock kupsel yazsaydi duzlenen alan
                    // cevresindeki puruzsuz araziden keskin bir plato gibi
                    // ayrilirdi.
                    for (int y = natural + 1; y <= target; y++)
                        world.SetTerrain(x, y, z, fill, 255);
                }
                else
                    for (int y = natural; y > target; y--)
                        world.SetTerrain(x, y, z, BlockId.Air, 0);
            }
        }

        // ---------- Yollar ----------

        static void BuildRoads(VoxelWorld world, int minX, int maxX, int minZ, int maxZ)
        {
            for (int x = minX; x < maxX; x++)
            for (int z = minZ; z < maxZ; z++)
            {
                if (!OnRoad(x, minX) && !OnRoad(z, minZ)) continue;

                int y = FindSurface(world, x, z);

                // Yol KUP degil ARAZI: zemine serilmis bir yuzey, kirilabilir
                // bir blok degil. Kup yazdigimizda yolun her kenarinda arazi
                // yuzeyiyle bulusmayan bir dikis olusuyordu - puruzsuz arazi
                // kupe teget gecemiyor. Araziye yazinca yol dogrudan zeminin
                // devami oluyor ve dikis tamamen kayboluyor.
                //
                // Binalar kup kalmaya devam ediyor: onlar zaten insa edilmis
                // yapilar ve kupsel gorunmeleri dogru.
                world.SetTerrain(x, y, z, BlockId.Road, 255);

                // Yolun ustu acik kalsin (tesviye sonrasi genelde zaten acik).
                for (int c = 1; c <= 3; c++)
                    world.SetTerrain(x, y + c, z, BlockId.Air, 0);
            }
        }

        static bool OnRoad(int coord, int origin) => (coord - origin) % Pitch >= LotSize;

        // ---------- Parseller ----------

        static void BuildLots(VoxelWorld world, int minX, int maxX, int minZ, int maxZ, System.Random rng)
        {
            for (int lx = minX; lx + LotSize < maxX; lx += Pitch)
            for (int lz = minZ; lz + LotSize < maxZ; lz += Pitch)
            {
                // Parsellerin bir kismi bos: her parsele bina koymak sehri
                // tekduze ve gezilemez yapiyor.
                if (rng.Next(100) < 25) continue;

                int inset = rng.Next(2, 4);
                int w = LotSize - inset * 2 - rng.Next(0, 3);
                int d = LotSize - inset * 2 - rng.Next(0, 3);
                if (w < 5 || d < 5) continue;

                int x0 = lx + inset, z0 = lz + inset;
                int groundY = AverageSurface(world, x0, z0, w, d);
                int height = rng.Next(4, 8);

                StructureGenerator.BuildHouse(world, x0, z0, w, d, groundY, height, rng);
            }
        }

        // ---------- Kasabalar ----------

        static void BuildOutskirts(VoxelWorld world, int worldX, int worldZ, int margin, System.Random rng)
        {
            // Kenar seritlerinde kucuk kumeler. Sehirden farkli olarak yolsuz
            // ve duzensiz - siluetten bile ayirt edilebilsin diye.
            for (int i = 0; i < 10; i++)
            {
                int cx = rng.Next(4, worldX - 4);
                int cz = rng.Next(4, worldZ - 4);
                bool inCity = cx > margin && cx < worldX - margin && cz > margin && cz < worldZ - margin;
                if (inCity) continue;

                int count = rng.Next(2, 5);
                for (int b = 0; b < count; b++)
                {
                    int w = rng.Next(5, 8), d = rng.Next(5, 8);
                    int x0 = Mathf.Clamp(cx + rng.Next(-10, 10), 1, worldX - w - 1);
                    int z0 = Mathf.Clamp(cz + rng.Next(-10, 10), 1, worldZ - d - 1);
                    int groundY = AverageSurface(world, x0, z0, w, d);
                    StructureGenerator.BuildHouse(world, x0, z0, w, d, groundY, rng.Next(3, 6), rng);
                }
            }
        }

        // ---------- Yardimcilar ----------

        /// <summary>Yukaridan asagi tarayip ilk dolu blogun y'sini dondurur.</summary>
        static int FindSurface(VoxelWorld world, int x, int z)
        {
            for (int y = Chunk.Size * 4 - 1; y >= 0; y--)
                if (BlockDatabase.IsSolid(world.GetBlock(x, y, z))) return y;
            return 0;
        }

        /// <summary>
        /// Bina tabani icin ortalama zemin. Tek nokta almak egimli arazide
        /// binanin bir kosesini havada birakiyordu.
        /// </summary>
        static int AverageSurface(VoxelWorld world, int x0, int z0, int w, int d)
        {
            int total = 0, n = 0;
            for (int x = x0; x < x0 + w; x++)
            for (int z = z0; z < z0 + d; z++)
            {
                total += FindSurface(world, x, z);
                n++;
            }
            return n == 0 ? 0 : Mathf.RoundToInt(total / (float)n);
        }
    }
}
