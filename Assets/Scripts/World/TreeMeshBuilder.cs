using System.Collections.Generic;
using LastLight.Voxel;
using UnityEngine;

namespace LastLight.World
{
    /// <summary>
    /// Prosedurel agac mesh'i uretir: silindir govde + ust uste daralan
    /// koni yaprak katmanlari.
    ///
    /// Bloktan yapilan agaclar voxel dunyada bile kotu duruyordu - bir agac
    /// birkac kup oldugunda silueti okunmuyor. Mesh agac ayni dunyada
    /// oturuyor cunku blok arazi ile mesh nesne karisimi bu turun standardi.
    ///
    /// UV'ler blok atlasindan okunuyor: govde ahsap hucresini, yapraklar
    /// yaprak hucresini kullaniyor. Boylece agac, bloklarla ayni malzemeyi
    /// paylasiyor ve ayri doku/malzeme gerektirmiyor.
    /// </summary>
    public static class TreeMeshBuilder
    {
        const int AtlasTiles = 4;
        const float Pad = 0.006f;

        /// <summary>Tek bir agac mesh'i uretir. Tur ve boyut tohuma bagli.</summary>
        public static Mesh Build(int seed, bool bare)
        {
            var rng = new System.Random(seed);

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            float trunkHeight = 3.5f + (float)rng.NextDouble() * 3f;
            float trunkRadius = 0.16f + (float)rng.NextDouble() * 0.1f;

            AddTrunk(verts, norms, uvs, tris, trunkHeight, trunkRadius, BlockId.Wood);

            if (!bare)
            {
                // Uc katman koni: alttan genis, yukari daralan. Tek koni
                // sivri ve yapay duruyor.
                int layers = 3;
                float baseY = trunkHeight * 0.45f;
                float span = trunkHeight * 0.85f;

                for (int i = 0; i < layers; i++)
                {
                    float t = i / (float)(layers - 1);
                    float y = baseY + span * t * 0.75f;
                    float radius = Mathf.Lerp(1.5f, 0.5f, t) * (0.85f + (float)rng.NextDouble() * 0.3f);
                    float height = Mathf.Lerp(1.6f, 1.0f, t);
                    AddCone(verts, norms, uvs, tris, y, radius, height, BlockId.Leaves);
                }
            }
            else
            {
                // Olu agac: birkac ciplak dal, siluet farkli olsun.
                for (int i = 0; i < 3; i++)
                {
                    float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float y = trunkHeight * (0.5f + 0.15f * i);
                    AddBranch(verts, norms, uvs, tris, y, angle, 0.9f, trunkRadius * 0.5f);
                }
            }

            var mesh = new Mesh { name = bare ? "DeadTree" : "PineTree" };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---------- Parcalar ----------

        static void AddTrunk(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t,
                             float height, float radius, BlockId tile)
        {
            const int sides = 7;   // tek sayi: silueti daha organik yapiyor
            var (u0, u1, v0, v1) = TileUV(tile);

            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)sides * Mathf.PI * 2f;

                // Yukari dogru hafif incelme: silindir yerine konik govde
                Vector3 b0 = new(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius);
                Vector3 b1 = new(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);
                Vector3 t0 = new(Mathf.Cos(a0) * radius * 0.7f, height, Mathf.Sin(a0) * radius * 0.7f);
                Vector3 t1 = new(Mathf.Cos(a1) * radius * 0.7f, height, Mathf.Sin(a1) * radius * 0.7f);

                int b = v.Count;
                v.Add(b0); v.Add(t0); v.Add(t1); v.Add(b1);

                Vector3 nrm = new Vector3(Mathf.Cos((a0 + a1) * 0.5f), 0f, Mathf.Sin((a0 + a1) * 0.5f));
                for (int k = 0; k < 4; k++) n.Add(nrm);

                uv.Add(new Vector2(u0, v0));
                uv.Add(new Vector2(u0, v1));
                uv.Add(new Vector2(u1, v1));
                uv.Add(new Vector2(u1, v0));

                t.Add(b + 0); t.Add(b + 1); t.Add(b + 2);
                t.Add(b + 0); t.Add(b + 2); t.Add(b + 3);
            }
        }

        static void AddCone(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t,
                            float baseY, float radius, float height, BlockId tile)
        {
            const int sides = 8;
            var (u0, u1, v0, v1) = TileUV(tile);
            Vector3 apex = new(0f, baseY + height, 0f);

            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)sides * Mathf.PI * 2f;

                Vector3 p0 = new(Mathf.Cos(a0) * radius, baseY, Mathf.Sin(a0) * radius);
                Vector3 p1 = new(Mathf.Cos(a1) * radius, baseY, Mathf.Sin(a1) * radius);

                int b = v.Count;
                v.Add(p0); v.Add(apex); v.Add(p1);

                Vector3 nrm = Vector3.Cross(apex - p0, p1 - p0).normalized;
                for (int k = 0; k < 3; k++) n.Add(nrm);

                uv.Add(new Vector2(u0, v0));
                uv.Add(new Vector2((u0 + u1) * 0.5f, v1));
                uv.Add(new Vector2(u1, v0));

                t.Add(b + 0); t.Add(b + 1); t.Add(b + 2);

                // Alt yuz: asagidan bakinca koni ici bos gorunmesin.
                int b2 = v.Count;
                v.Add(p0); v.Add(p1); v.Add(new Vector3(0f, baseY, 0f));
                for (int k = 0; k < 3; k++) n.Add(Vector3.down);
                uv.Add(new Vector2(u0, v0));
                uv.Add(new Vector2(u1, v0));
                uv.Add(new Vector2((u0 + u1) * 0.5f, (v0 + v1) * 0.5f));
                t.Add(b2 + 0); t.Add(b2 + 1); t.Add(b2 + 2);
            }
        }

        static void AddBranch(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t,
                              float y, float angle, float length, float thickness)
        {
            var (u0, u1, v0, v1) = TileUV(BlockId.Wood);

            Vector3 dir = new(Mathf.Cos(angle), 0.45f, Mathf.Sin(angle));
            dir.Normalize();
            Vector3 side = Vector3.Cross(dir, Vector3.up).normalized * thickness;
            Vector3 start = new(0f, y, 0f);
            Vector3 end = start + dir * length;

            int b = v.Count;
            v.Add(start - side); v.Add(end - side * 0.4f); v.Add(end + side * 0.4f); v.Add(start + side);
            for (int k = 0; k < 4; k++) n.Add(Vector3.up);

            uv.Add(new Vector2(u0, v0));
            uv.Add(new Vector2(u0, v1));
            uv.Add(new Vector2(u1, v1));
            uv.Add(new Vector2(u1, v0));

            t.Add(b + 0); t.Add(b + 1); t.Add(b + 2);
            t.Add(b + 0); t.Add(b + 2); t.Add(b + 3);
            // Arka yuz
            t.Add(b + 0); t.Add(b + 2); t.Add(b + 1);
            t.Add(b + 0); t.Add(b + 3); t.Add(b + 2);
        }

        /// <summary>Blok atlasindaki hucrenin UV sinirlari.</summary>
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
