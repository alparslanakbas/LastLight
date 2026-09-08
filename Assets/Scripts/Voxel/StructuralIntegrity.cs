using System.Collections.Generic;
using UnityEngine;

namespace LastLight.Voxel
{
    /// <summary>
    /// Desteksiz kalan bloklari bulur ve dusurur.
    ///
    /// Model: zemine kesintisiz dikey hatti olan blogun destegi tam. Destek
    /// yukari dogru azalmadan tasinir (bir kolon istedigi kadar yuksek olabilir),
    /// yana ve asagiya dogru her adimda bir azalir. Boylece belirli bir
    /// uzunluktan sonra konsol (destegi olmayan yatay uzanti) cokuyor.
    ///
    /// Bu bir fizik simulasyonu degil, graf uzerinde destek yayilimi - dikey
    /// kenar maliyeti 0, yatay 1 oldugu icin 0-1 BFS (deque) ile hesaplaniyor.
    /// </summary>
    public static class StructuralIntegrity
    {
        /// <summary>Yatay olarak destegin tasinabilecegi en fazla blok sayisi.</summary>
        public const int MaxSupport = 16;

        /// <summary>Tek seferde islenen bolgenin yaricapi.</summary>
        const int Radius = MaxSupport + 2;

        /// <summary>Bir kirmada dusurulebilecek en fazla blok - zincirleme cokmede kilitlenmeyi onler.</summary>
        const int MaxCollapsePerPass = 512;

        static readonly Vector3Int[] Horizontal =
        {
            new(1, 0, 0), new(-1, 0, 0), new(0, 0, 1), new(0, 0, -1),
        };

        /// <summary>
        /// Degisen blogun cevresini yeniden degerlendirir ve desteksiz kalanlari dusurur.
        /// </summary>
        public static void Evaluate(VoxelWorld world, Vector3Int changed)
        {
            var support = new Dictionary<Vector3Int, int>();
            var queue = new LinkedList<Vector3Int>();

            Vector3Int min = changed - Vector3Int.one * Radius;
            Vector3Int max = changed + Vector3Int.one * Radius;
            if (min.y < 0) min.y = 0;

            // 1) Kaynaklar: bolgenin en alt katmanindaki ve dunya tabanindaki
            //    dolu bloklar tam destekli sayilir.
            //    Bolge sinirini destekli varsaymak bir yaklasim - alternatifi
            //    her kirmada tum dunyayi taramak olurdu. Sinirdan MaxSupport
            //    kadar uzakta yanlis sonuc uretebilir, oynaniste fark edilmez.
            for (int x = min.x; x <= max.x; x++)
            for (int z = min.z; z <= max.z; z++)
            {
                var p = new Vector3Int(x, min.y, z);
                if (!IsSolid(world, p)) continue;
                support[p] = MaxSupport;
                queue.AddFirst(p);
            }

            // 2) 0-1 BFS: dikey kenarlar maliyetsiz (basa), yatay kenarlar
            //    maliyet 1 (sona) - boylece kuyruk mesafeye gore sirali kaliyor.
            while (queue.Count > 0)
            {
                Vector3Int cur = queue.First.Value;
                queue.RemoveFirst();
                int s = support[cur];
                if (s <= 0) continue;

                // Yukari: destek azalmaz. Bir kolon sinirsiz yukselir.
                Relax(world, support, queue, cur + Vector3Int.up, s, min, max, cheap: true);

                // Yana ve asagiya: her adimda bir azalir.
                foreach (var d in Horizontal)
                    Relax(world, support, queue, cur + d, s - 1, min, max, cheap: false);

                Relax(world, support, queue, cur + Vector3Int.down, s - 1, min, max, cheap: false);
            }

            // 3) Bolgede destegi hic olmayan dolu bloklari topla.
            var doomed = new List<Vector3Int>();
            for (int y = min.y; y <= max.y; y++)
            for (int x = min.x; x <= max.x; x++)
            for (int z = min.z; z <= max.z; z++)
            {
                var p = new Vector3Int(x, y, z);
                if (!IsSolid(world, p)) continue;
                if (support.TryGetValue(p, out int s) && s > 0) continue;
                doomed.Add(p);
                if (doomed.Count >= MaxCollapsePerPass) goto collapse;
            }

            collapse:
            foreach (var p in doomed)
            {
                BlockId id = world.GetBlock(p.x, p.y, p.z);
                world.SetBlock(p.x, p.y, p.z, BlockId.Air);
                FallingBlock.Spawn(world, p, id);
            }
        }

        static void Relax(VoxelWorld world, Dictionary<Vector3Int, int> support,
                          LinkedList<Vector3Int> queue, Vector3Int p, int value,
                          Vector3Int min, Vector3Int max, bool cheap)
        {
            if (value <= 0) return;
            if (p.x < min.x || p.x > max.x || p.y < min.y || p.y > max.y || p.z < min.z || p.z > max.z) return;
            if (!IsSolid(world, p)) return;
            if (support.TryGetValue(p, out int existing) && existing >= value) return;

            support[p] = value;
            if (cheap) queue.AddFirst(p); else queue.AddLast(p);
        }

        static bool IsSolid(VoxelWorld world, Vector3Int p) =>
            BlockDatabase.IsSolid(world.GetBlock(p.x, p.y, p.z));
    }
}
