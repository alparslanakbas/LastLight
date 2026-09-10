using System.Collections.Generic;
using UnityEngine;

namespace LastLight.Voxel
{
    /// <summary>
    /// Dogal arazinin puruzsuz yuzeyini uretir.
    ///
    /// NEDEN KLASIK MARCHING CUBES DEGIL: Surface Nets ayni aileden ve arazi
    /// icin gozle ayirt edilemeyecek kadar benzer bir sonuc veriyor, ama
    /// marching cubes'un 256 satirlik ucgen tablosuna ihtiyac duymuyor. O
    /// tabloda tek bir yanlis sayi mesh'te delik aciyor ve hangi satirdan
    /// geldigini bulmak saatler suruyor - tablo elle yazildiginda kacinilmaz
    /// bir risk. Surface Nets yapisi geregi dogru: hucre basina tek kose
    /// uretiliyor ve isaret degistiren her kenar bir dortgen kapatiyor,
    /// tablosuz.
    ///
    /// Ayrica hucre basina tek kose, marching cubes'un ayni yuzey icin
    /// urettiginden daha az kose demek.
    ///
    /// Referans oyunun kendisi de arazide bu aileden bir yontem kullaniyor;
    /// konuyu ogreten Unity ders serisi de ikinci derste marching cubes'tan
    /// dual contouring'e geciyor - sebep ayni.
    /// </summary>
    public static class SurfaceNets
    {
        const float Iso = 0.5f;

        // Hucrenin sekiz kosesi. Sira onemli: kenar tablosu buna dayaniyor.
        static readonly Vector3Int[] Corner =
        {
            new(0, 0, 0), new(1, 0, 0), new(1, 0, 1), new(0, 0, 1),
            new(0, 1, 0), new(1, 1, 0), new(1, 1, 1), new(0, 1, 1),
        };

        // On iki kenar, kose indeksleri olarak.
        static readonly int[,] Edge =
        {
            {0,1},{1,2},{2,3},{3,0},        // alt yuz
            {4,5},{5,6},{6,7},{7,4},        // ust yuz
            {0,4},{1,5},{2,6},{3,7},        // dikey
        };

        // Hucre araligi -1..Size: alt sinirdaki dortgenleri de kapatabilmek
        // icin bir hucre geriden basliyoruz, ust sinirda komsu chunk'a
        // tasiyoruz. Boylece chunk dikisleri gorunmuyor.
        const int Lo = -1;
        const int Hi = Chunk.Size;
        const int Span = Hi - Lo + 2;          // -1 .. Size dahil + 1 pay

        [System.ThreadStatic] static int[] _cellVertex;

        static readonly List<Vector3> _verts = new();
        static readonly List<Vector3> _norms = new();
        static readonly List<Vector2> _mats = new();
        static readonly List<int> _tris = new();

        public static void Build(Chunk chunk, VoxelWorld world, Mesh mesh)
        {
            _verts.Clear(); _norms.Clear(); _mats.Clear(); _tris.Clear();

            _cellVertex ??= new int[Span * Span * Span];
            for (int i = 0; i < _cellVertex.Length; i++) _cellVertex[i] = -1;

            Vector3Int o = chunk.WorldOrigin;

            // ---- 1. Asama: isaret degistiren her hucreye bir kose koy ----
            for (int cx = Lo; cx <= Hi; cx++)
            for (int cy = Lo; cy <= Hi; cy++)
            for (int cz = Lo; cz <= Hi; cz++)
            {
                float d0 = world.Density(o.x + cx + 0, o.y + cy + 0, o.z + cz + 0);
                float d1 = world.Density(o.x + cx + 1, o.y + cy + 0, o.z + cz + 0);
                float d2 = world.Density(o.x + cx + 1, o.y + cy + 0, o.z + cz + 1);
                float d3 = world.Density(o.x + cx + 0, o.y + cy + 0, o.z + cz + 1);
                float d4 = world.Density(o.x + cx + 0, o.y + cy + 1, o.z + cz + 0);
                float d5 = world.Density(o.x + cx + 1, o.y + cy + 1, o.z + cz + 0);
                float d6 = world.Density(o.x + cx + 1, o.y + cy + 1, o.z + cz + 1);
                float d7 = world.Density(o.x + cx + 0, o.y + cy + 1, o.z + cz + 1);

                int mask = 0;
                if (d0 >= Iso) mask |= 1;
                if (d1 >= Iso) mask |= 2;
                if (d2 >= Iso) mask |= 4;
                if (d3 >= Iso) mask |= 8;
                if (d4 >= Iso) mask |= 16;
                if (d5 >= Iso) mask |= 32;
                if (d6 >= Iso) mask |= 64;
                if (d7 >= Iso) mask |= 128;

                // Tamamen ici ya da tamamen disi: yuzey yok.
                if (mask == 0 || mask == 255) continue;

                // Kose voxel'lerinden biri KUP ise bu hucrede puruzsuz yuzey
                // uretmiyoruz. Iki sorunu birden cozuyor:
                //
                // 1) Yol/bina kupleri yogunluk alanina katki vermiyor,
                //    dolayisiyla yuzey onlarin oldugu yerde cukura dusuyor ve
                //    her yolun kenarinda hendek olusuyordu.
                // 2) Kupleri alana dahil etseydik havaya konan tek bir blok
                //    cevresinde yuvarlak bir yumru olusurdu - hem kup hem
                //    yumru gorunurdu.
                //
                // Hucreyi atlayinca yuzey kupun tam kenarinda kesiliyor ve
                // acilan bosluk zaten kupun kendisiyle doluyor.
                if (TouchesCube(world, o, cx, cy, cz)) continue;

                var d = new[] { d0, d1, d2, d3, d4, d5, d6, d7 };

                // Kose, kesisen kenarlarin orta noktalarinin ortalamasi.
                // Ortalama almak yuzeyi yumusatiyor; tek bir kenardan
                // secseydik hucre izgarasi geri gorunurdu.
                Vector3 sum = Vector3.zero;
                int hits = 0;

                for (int e = 0; e < 12; e++)
                {
                    int a = Edge[e, 0], b = Edge[e, 1];
                    bool sa = d[a] >= Iso, sb = d[b] >= Iso;
                    if (sa == sb) continue;

                    // Dogrusal ara deger: yuzey tam esigin gectigi yerde.
                    // Kenarin ortasini almak arazi yuzeyini kademeli
                    // gosteriyordu; asil yumusakligi bu veriyor.
                    float t = Mathf.Clamp01((Iso - d[a]) / (d[b] - d[a]));
                    sum += Vector3.Lerp(Corner[a], Corner[b], t);
                    hits++;
                }

                if (hits == 0) continue;

                Vector3 local = sum / hits + new Vector3(cx, cy, cz);

                _cellVertex[CellIndex(cx, cy, cz)] = _verts.Count;
                _verts.Add(local);
                _norms.Add(Gradient(world, o.x + cx, o.y + cy, o.z + cz));
                _mats.Add(new Vector2(Material(world, o, cx, cy, cz, d), 0f));
            }

            // ---- 2. Asama: isaret degistiren her kenari dortgenle kapat ----
            for (int px = 0; px <= Hi; px++)
            for (int py = 0; py <= Hi; py++)
            for (int pz = 0; pz <= Hi; pz++)
            {
                bool solid = world.Density(o.x + px, o.y + py, o.z + pz) >= Iso;

                // X kenari: cevresindeki dort hucre Y ve Z'de bir geri.
                if (solid != (world.Density(o.x + px + 1, o.y + py, o.z + pz) >= Iso))
                    Quad(px, py - 1, pz - 1, px, py, pz - 1, px, py, pz, px, py - 1, pz, solid);

                if (solid != (world.Density(o.x + px, o.y + py + 1, o.z + pz) >= Iso))
                    Quad(px - 1, py, pz - 1, px - 1, py, pz, px, py, pz, px, py, pz - 1, solid);

                if (solid != (world.Density(o.x + px, o.y + py, o.z + pz + 1) >= Iso))
                    Quad(px - 1, py - 1, pz, px, py - 1, pz, px, py, pz, px - 1, py, pz, solid);
            }

            mesh.Clear();
            if (_verts.Count == 0) return;

            mesh.indexFormat = _verts.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

            mesh.SetVertices(_verts);
            mesh.SetNormals(_norms);
            mesh.SetUVs(0, _mats);          // uv.x = malzeme indeksi
            mesh.SetTriangles(_tris, 0);
            mesh.RecalculateBounds();
        }

        static int CellIndex(int cx, int cy, int cz) =>
            (cx - Lo) + Span * ((cy - Lo) + Span * (cz - Lo));

        static void Quad(int ax, int ay, int az, int bx, int by, int bz,
                         int cx, int cy, int cz, int dx, int dy, int dz, bool flip)
        {
            int a = Vertex(ax, ay, az);
            int b = Vertex(bx, by, bz);
            int c = Vertex(cx, cy, cz);
            int d = Vertex(dx, dy, dz);

            // Dort hucrenin dordunde de kose olmali; biri eksikse bu kenar
            // chunk sinirinin disinda kaliyor demektir.
            if (a < 0 || b < 0 || c < 0 || d < 0) return;

            if (flip)
            {
                _tris.Add(a); _tris.Add(b); _tris.Add(c);
                _tris.Add(a); _tris.Add(c); _tris.Add(d);
            }
            else
            {
                _tris.Add(a); _tris.Add(c); _tris.Add(b);
                _tris.Add(a); _tris.Add(d); _tris.Add(c);
            }
        }

        static int Vertex(int cx, int cy, int cz)
        {
            if (cx < Lo || cx > Hi || cy < Lo || cy > Hi || cz < Lo || cz > Hi) return -1;
            return _cellVertex[CellIndex(cx, cy, cz)];
        }

        /// <summary>
        /// Yogunluk alaninin egimi = yuzey normali. Ucgen normallerini
        /// toplamak yerine bunu kullaniyoruz: alan zaten surekli oldugu icin
        /// egim komsu chunk'ta da ayni cikiyor ve dikiste isik kirilmiyor.
        /// </summary>
        static Vector3 Gradient(VoxelWorld w, int x, int y, int z)
        {
            float gx = w.Density(x + 1, y, z) - w.Density(x - 1, y, z);
            float gy = w.Density(x, y + 1, z) - w.Density(x, y - 1, z);
            float gz = w.Density(x, y, z + 1) - w.Density(x, y, z - 1);

            var g = new Vector3(-gx, -gy, -gz);
            return g.sqrMagnitude < 1e-8f ? Vector3.up : g.normalized;
        }

        /// <summary>
        /// En dolu KATI kosenin blok tipi koseye malzeme indeksi olur.
        ///
        /// "Kati" sarti onemli: eskiden yalnizca en yuksek yogunluga
        /// bakiyorduk ve sinir hucrelerinde en yuksek kose Hava cikabiliyordu.
        /// Hava'nin degeri 0, atlasin 0. karosu ise bos - ekranda parsel
        /// kenarlarinda renkli zikzak seritler olarak gorunuyordu.
        /// </summary>
        static float Material(VoxelWorld w, Vector3Int o, int cx, int cy, int cz, float[] d)
        {
            int best = -1;
            float bestD = -1f;

            for (int i = 0; i < 8; i++)
            {
                if (d[i] < Iso) continue;
                if (d[i] <= bestD) continue;

                var c = Corner[i];
                var id = w.GetBlock(o.x + cx + c.x, o.y + cy + c.y, o.z + cz + c.z);
                if (id == BlockId.Air) continue;

                bestD = d[i];
                best = i;
            }

            // Hicbir kati kose yoksa tas: gorunur bir yuzey her zaman bir
            // malzemeye sahip olmali, bos karo cizmektense yanlis tas cizmek
            // katbekat iyi.
            if (best < 0) return (int)BlockId.Stone;

            var bc = Corner[best];
            return (int)w.GetBlock(o.x + cx + bc.x, o.y + cy + bc.y, o.z + cz + bc.z);
        }

        /// <summary>Hucrenin sekiz kosesinden biri kupsel bir blok mu.</summary>
        static bool TouchesCube(VoxelWorld w, Vector3Int o, int cx, int cy, int cz)
        {
            for (int i = 0; i < 8; i++)
            {
                var c = Corner[i];
                int wx = o.x + cx + c.x, wy = o.y + cy + c.y, wz = o.z + cz + c.z;

                if (w.GetBlock(wx, wy, wz) == BlockId.Air) continue;
                if (!w.IsSmooth(wx, wy, wz)) return true;
            }
            return false;
        }
    }
}
