using System.Collections.Generic;
using UnityEngine;

namespace LastLight.Voxel
{
    /// <summary>
    /// Kup disindaki blok bicimlerinin mesh'ini uretir.
    ///
    /// Voxel dunyanin "kutu yigini" gorunmesinin sebebi doku degil geometri:
    /// her blok kup oldugunda arazi her yukseltide basamak yapiyor. Rampa ve
    /// yarim blok bu basamaklari yumusatiyor - referans oyunlarin yuzlerce
    /// blok bicimi tutmasinin sebebi de bu.
    ///
    /// Kup icin yuz eleme yapiliyor (komsusu doluysa yuz uretilmez) ama egik
    /// bicimlerde eleme uygulanmiyor: egik yuzeyin komsusu her zaman kismen
    /// gorunur oluyor ve eleme mantigi hatali bosluklar uretiyordu.
    /// </summary>
    public static class ShapeMesher
    {
        const int AtlasTiles = 4;
        const float Pad = 0.006f;

        public static void AddShape(
            List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris,
            Vector3 origin, BlockId id, BlockShape shape)
        {
            var (u0, u1, v0, v1) = TileUV(id);

            switch (shape)
            {
                case BlockShape.Slab: AddSlab(verts, norms, uvs, tris, origin, u0, u1, v0, v1); break;
                case BlockShape.RampNorth: AddRamp(verts, norms, uvs, tris, origin, 0, u0, u1, v0, v1); break;
                case BlockShape.RampSouth: AddRamp(verts, norms, uvs, tris, origin, 1, u0, u1, v0, v1); break;
                case BlockShape.RampEast: AddRamp(verts, norms, uvs, tris, origin, 2, u0, u1, v0, v1); break;
                case BlockShape.RampWest: AddRamp(verts, norms, uvs, tris, origin, 3, u0, u1, v0, v1); break;
            }
        }

        // ---------- Yarim blok ----------

        static void AddSlab(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t,
                            Vector3 o, float u0, float u1, float v0, float v1)
        {
            const float h = 0.5f;

            // Alt
            Quad(v, n, uv, t,
                o + new Vector3(0, 0, 0), o + new Vector3(0, 0, 1),
                o + new Vector3(1, 0, 1), o + new Vector3(1, 0, 0),
                Vector3.down, u0, u1, v0, v1);

            // Ust
            Quad(v, n, uv, t,
                o + new Vector3(0, h, 1), o + new Vector3(0, h, 0),
                o + new Vector3(1, h, 0), o + new Vector3(1, h, 1),
                Vector3.up, u0, u1, v0, v1);

            // Dort yan (yarim yukseklikte)
            Quad(v, n, uv, t,
                o + new Vector3(0, 0, 0), o + new Vector3(0, h, 0),
                o + new Vector3(1, h, 0), o + new Vector3(1, 0, 0),
                Vector3.back, u0, u1, v0, v1);

            Quad(v, n, uv, t,
                o + new Vector3(1, 0, 1), o + new Vector3(1, h, 1),
                o + new Vector3(0, h, 1), o + new Vector3(0, 0, 1),
                Vector3.forward, u0, u1, v0, v1);

            Quad(v, n, uv, t,
                o + new Vector3(1, 0, 0), o + new Vector3(1, h, 0),
                o + new Vector3(1, h, 1), o + new Vector3(1, 0, 1),
                Vector3.right, u0, u1, v0, v1);

            Quad(v, n, uv, t,
                o + new Vector3(0, 0, 1), o + new Vector3(0, h, 1),
                o + new Vector3(0, h, 0), o + new Vector3(0, 0, 0),
                Vector3.left, u0, u1, v0, v1);
        }

        // ---------- Rampa ----------

        /// <summary>
        /// dir: 0 = +Z'ye yukselir, 1 = -Z, 2 = +X, 3 = -X.
        /// Rampa alcak kenardan yuksek kenara dogru egiliyor.
        /// </summary>
        static void AddRamp(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t,
                            Vector3 o, int dir, float u0, float u1, float v0, float v1)
        {
            // Kose yuksekliklerini yone gore belirliyoruz: rampanin yuksek
            // kenari 1, alcak kenari 0.
            float h00, h10, h11, h01;   // (x,z) = (0,0), (1,0), (1,1), (0,1)

            switch (dir)
            {
                case 0: h00 = 0; h10 = 0; h11 = 1; h01 = 1; break;   // +Z yukselir
                case 1: h00 = 1; h10 = 1; h11 = 0; h01 = 0; break;   // -Z
                case 2: h00 = 0; h10 = 1; h11 = 1; h01 = 0; break;   // +X
                default: h00 = 1; h10 = 0; h11 = 0; h01 = 1; break;  // -X
            }

            Vector3 p00 = o + new Vector3(0, h00, 0);
            Vector3 p10 = o + new Vector3(1, h10, 0);
            Vector3 p11 = o + new Vector3(1, h11, 1);
            Vector3 p01 = o + new Vector3(0, h01, 1);

            Vector3 b00 = o + new Vector3(0, 0, 0);
            Vector3 b10 = o + new Vector3(1, 0, 0);
            Vector3 b11 = o + new Vector3(1, 0, 1);
            Vector3 b01 = o + new Vector3(0, 0, 1);

            // Egik ust yuzey
            Vector3 slopeNormal = Vector3.Cross(p10 - p00, p01 - p00).normalized;
            if (slopeNormal.y < 0f) slopeNormal = -slopeNormal;
            Quad(v, n, uv, t, p01, p00, p10, p11, slopeNormal, u0, u1, v0, v1);

            // Alt yuzey
            Quad(v, n, uv, t, b00, b01, b11, b10, Vector3.down, u0, u1, v0, v1);

            // Dort yan: her biri alt kenardan ust kose yuksekligine kadar.
            // Yuksekligi sifir olan kenar dejenere ucgen uretmesin diye
            // kontrol ediliyor.
            AddSide(v, n, uv, t, b00, b10, p10, p00, Vector3.back, u0, u1, v0, v1);
            AddSide(v, n, uv, t, b11, b01, p01, p11, Vector3.forward, u0, u1, v0, v1);
            AddSide(v, n, uv, t, b10, b11, p11, p10, Vector3.right, u0, u1, v0, v1);
            AddSide(v, n, uv, t, b01, b00, p00, p01, Vector3.left, u0, u1, v0, v1);
        }

        static void AddSide(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t,
                            Vector3 bl, Vector3 br, Vector3 tr, Vector3 tl,
                            Vector3 normal, float u0, float u1, float v0, float v1)
        {
            // Iki ust kose de tabana yapisikse yuzey yok demektir.
            if (tr.y - br.y < 0.001f && tl.y - bl.y < 0.001f) return;
            Quad(v, n, uv, t, bl, tl, tr, br, normal, u0, u1, v0, v1);
        }

        // ---------- Yardimci ----------

        static void Quad(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t,
                         Vector3 a, Vector3 b, Vector3 c, Vector3 d,
                         Vector3 normal, float u0, float u1, float v0, float v1)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            for (int k = 0; k < 4; k++) n.Add(normal);

            uv.Add(new Vector2(u0, v0));
            uv.Add(new Vector2(u0, v1));
            uv.Add(new Vector2(u1, v1));
            uv.Add(new Vector2(u1, v0));

            t.Add(i + 0); t.Add(i + 1); t.Add(i + 2);
            t.Add(i + 0); t.Add(i + 2); t.Add(i + 3);
        }

        static (float u0, float u1, float v0, float v1) TileUV(BlockId id)
        {
            int tile = (int)id;
            float cell = 1f / AtlasTiles;
            float tx = (tile % AtlasTiles) * cell;
            float ty = (tile / AtlasTiles) * cell;
            return (tx + Pad, tx + cell - Pad, ty + Pad, ty + cell - Pad);
        }
    }
}
