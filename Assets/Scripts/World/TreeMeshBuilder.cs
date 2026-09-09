using System.Collections.Generic;
using LastLight.Voxel;
using UnityEngine;

namespace LastLight.World
{
    /// <summary>
    /// Prosedurel agac mesh'i uretir.
    ///
    /// Ilk surum duz bir govde + uc simetrik koniydi ve "primitive" gorunuyordu.
    /// Bu surumde silueti bozan uc sey var: govde segmentli ve hafif egri,
    /// yaprak katmanlari merkezden kaydirilmis ve farkli boyutlarda, ayrica
    /// govdeden cikan dallar var. Simetri kirilinca ayni poligon sayisiyla
    /// cok daha organik duruyor.
    ///
    /// UV'ler blok atlasindan okunuyor: govde ahsap, yapraklar yaprak hucresi.
    /// Boylece agac bloklarla ayni malzemeyi paylasiyor, ayri doku gerekmiyor.
    /// </summary>
    public static class TreeMeshBuilder
    {
        const int AtlasTiles = 4;
        const float Pad = 0.006f;

        /// <summary>Kabuk dokusunun atlas hucresi (blok degil, agaca ozel).</summary>
        const int BarkTile = 13;

        public static Mesh Build(int seed, bool bare)
        {
            var rng = new System.Random(seed);

            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();

            float height = 4.5f + (float)rng.NextDouble() * 3.5f;
            float radius = 0.19f + (float)rng.NextDouble() * 0.11f;

            // Govde egrisi: her segmentte kucuk bir kayma birikiyor. Tam dik
            // govde yapay duruyor, dogada agac hep biraz egilir.
            var spine = BuildSpine(rng, height, segments: 5);

            AddTrunk(v, n, uv, t, spine, radius);

            if (!bare)
            {
                // Yaprak katmanlari: govdenin ust yarisinda, merkezden
                // kaydirilmis ve boyutlari degisken.
                int layers = 4 + rng.Next(2);
                for (int i = 0; i < layers; i++)
                {
                    float tt = i / (float)(layers - 1);
                    float y = Mathf.Lerp(height * 0.38f, height * 1.02f, tt);

                    Vector3 center = SampleSpine(spine, y / height);
                    center += new Vector3(
                        ((float)rng.NextDouble() - 0.5f) * 0.45f,
                        0f,
                        ((float)rng.NextDouble() - 0.5f) * 0.45f);

                    float r = Mathf.Lerp(1.75f, 0.45f, tt) * (0.8f + (float)rng.NextDouble() * 0.45f);
                    float h = Mathf.Lerp(1.9f, 0.9f, tt);
                    AddCone(v, n, uv, t, center, y, r, h, rng);
                }

                // Birkac dal: yaprak kutlesinin altindan cikip silueti kiriyor.
                int branches = 2 + rng.Next(3);
                for (int i = 0; i < branches; i++)
                {
                    float y = height * (0.30f + (float)rng.NextDouble() * 0.30f);
                    float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                    AddBranch(v, n, uv, t, SampleSpine(spine, y / height), y, angle,
                              0.7f + (float)rng.NextDouble() * 0.6f, radius * 0.45f);
                }
            }
            else
            {
                // Olu agac: yapraksiz, daha cok ve daha uzun dal.
                int branches = 4 + rng.Next(4);
                for (int i = 0; i < branches; i++)
                {
                    float y = height * (0.35f + (float)rng.NextDouble() * 0.55f);
                    float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                    AddBranch(v, n, uv, t, SampleSpine(spine, y / height), y, angle,
                              0.9f + (float)rng.NextDouble() * 0.9f, radius * 0.4f);
                }
            }

            var mesh = new Mesh { name = bare ? "DeadTree" : "PineTree" };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---------- Govde omurgasi ----------

        static Vector3[] BuildSpine(System.Random rng, float height, int segments)
        {
            var pts = new Vector3[segments + 1];
            Vector3 drift = Vector3.zero;
            Vector3 dir = new(((float)rng.NextDouble() - 0.5f) * 0.12f, 0f,
                              ((float)rng.NextDouble() - 0.5f) * 0.12f);

            for (int i = 0; i <= segments; i++)
            {
                float f = i / (float)segments;
                drift += dir * (f * f);   // yukari cikinca egim artiyor
                pts[i] = new Vector3(drift.x, height * f, drift.z);
            }
            return pts;
        }

        static Vector3 SampleSpine(Vector3[] spine, float t01)
        {
            t01 = Mathf.Clamp01(t01);
            float f = t01 * (spine.Length - 1);
            int i = Mathf.Min(Mathf.FloorToInt(f), spine.Length - 2);
            var p = Vector3.Lerp(spine[i], spine[i + 1], f - i);
            return new Vector3(p.x, 0f, p.z);   // yatay kayma; yukseklik ayri veriliyor
        }

        // ---------- Parcalar ----------

        static void AddTrunk(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t,
                             Vector3[] spine, float radius)
        {
            const int sides = 7;
            var (u0, u1, v0, v1) = TileUVIndex(BarkTile);
            int segs = spine.Length - 1;

            for (int s = 0; s < segs; s++)
            {
                float f0 = s / (float)segs, f1 = (s + 1) / (float)segs;
                float r0 = Mathf.Lerp(radius, radius * 0.45f, f0);
                float r1 = Mathf.Lerp(radius, radius * 0.45f, f1);

                for (int i = 0; i < sides; i++)
                {
                    float a0 = i / (float)sides * Mathf.PI * 2f;
                    float a1 = (i + 1) / (float)sides * Mathf.PI * 2f;

                    Vector3 c0 = spine[s], c1 = spine[s + 1];
                    Vector3 p00 = c0 + new Vector3(Mathf.Cos(a0) * r0, 0f, Mathf.Sin(a0) * r0);
                    Vector3 p01 = c1 + new Vector3(Mathf.Cos(a0) * r1, 0f, Mathf.Sin(a0) * r1);
                    Vector3 p11 = c1 + new Vector3(Mathf.Cos(a1) * r1, 0f, Mathf.Sin(a1) * r1);
                    Vector3 p10 = c0 + new Vector3(Mathf.Cos(a1) * r0, 0f, Mathf.Sin(a1) * r0);

                    int b = v.Count;
                    v.Add(p00); v.Add(p01); v.Add(p11); v.Add(p10);

                    Vector3 nrm = new(Mathf.Cos((a0 + a1) * 0.5f), 0f, Mathf.Sin((a0 + a1) * 0.5f));
                    for (int k = 0; k < 4; k++) n.Add(nrm);

                    // Dikey UV segmente gore kayiyor: doku govde boyunca
                    // tekrarlaninca damar surekli gorunuyor.
                    float tv0 = Mathf.Lerp(v0, v1, f0);
                    float tv1 = Mathf.Lerp(v0, v1, f1);
                    uv.Add(new Vector2(u0, tv0));
                    uv.Add(new Vector2(u0, tv1));
                    uv.Add(new Vector2(u1, tv1));
                    uv.Add(new Vector2(u1, tv0));

                    t.Add(b + 0); t.Add(b + 1); t.Add(b + 2);
                    t.Add(b + 0); t.Add(b + 2); t.Add(b + 3);
                }
            }
        }

        static void AddCone(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t,
                            Vector3 center, float baseY, float radius, float height, System.Random rng)
        {
            const int sides = 9;
            var (u0, u1, v0, v1) = TileUV(BlockId.Leaves);
            Vector3 apex = center + new Vector3(0f, baseY + height, 0f);

            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)sides * Mathf.PI * 2f;

                // Yaricapta kucuk dalgalanma: tam daire koni plastik duruyor.
                float r0 = radius * (0.82f + (float)rng.NextDouble() * 0.36f);
                float r1 = radius * (0.82f + (float)rng.NextDouble() * 0.36f);

                Vector3 p0 = center + new Vector3(Mathf.Cos(a0) * r0, baseY, Mathf.Sin(a0) * r0);
                Vector3 p1 = center + new Vector3(Mathf.Cos(a1) * r1, baseY, Mathf.Sin(a1) * r1);

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
                v.Add(p0); v.Add(p1); v.Add(center + new Vector3(0f, baseY, 0f));
                for (int k = 0; k < 3; k++) n.Add(Vector3.down);
                uv.Add(new Vector2(u0, v0));
                uv.Add(new Vector2(u1, v0));
                uv.Add(new Vector2((u0 + u1) * 0.5f, (v0 + v1) * 0.5f));
                t.Add(b2 + 0); t.Add(b2 + 1); t.Add(b2 + 2);
            }
        }

        static void AddBranch(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t,
                              Vector3 offset, float y, float angle, float length, float thickness)
        {
            var (u0, u1, v0, v1) = TileUVIndex(BarkTile);

            Vector3 dir = new(Mathf.Cos(angle), 0.5f, Mathf.Sin(angle));
            dir.Normalize();
            Vector3 start = offset + new Vector3(0f, y, 0f);
            Vector3 end = start + dir * length;
            Vector3 side = Vector3.Cross(dir, Vector3.up).normalized * thickness;

            // Iki capraz dortgen: tek dortgen yandan bakinca kayboluyor.
            for (int pass = 0; pass < 2; pass++)
            {
                Vector3 s = pass == 0 ? side : Vector3.Cross(dir, side).normalized * thickness;

                int b = v.Count;
                v.Add(start - s); v.Add(end - s * 0.35f); v.Add(end + s * 0.35f); v.Add(start + s);
                Vector3 nrm = Vector3.Cross(dir, s).normalized;
                for (int k = 0; k < 4; k++) n.Add(nrm);

                uv.Add(new Vector2(u0, v0));
                uv.Add(new Vector2(u0, v1));
                uv.Add(new Vector2(u1, v1));
                uv.Add(new Vector2(u1, v0));

                t.Add(b + 0); t.Add(b + 1); t.Add(b + 2);
                t.Add(b + 0); t.Add(b + 2); t.Add(b + 3);
                t.Add(b + 0); t.Add(b + 2); t.Add(b + 1);
                t.Add(b + 0); t.Add(b + 3); t.Add(b + 2);
            }
        }

        static (float u0, float u1, float v0, float v1) TileUV(BlockId id) => TileUVIndex((int)id);

        static (float u0, float u1, float v0, float v1) TileUVIndex(int tile)
        {
            float cell = 1f / AtlasTiles;
            float tx = (tile % AtlasTiles) * cell;
            float ty = (tile / AtlasTiles) * cell;
            return (tx + Pad, tx + cell - Pad, ty + Pad, ty + cell - Pad);
        }
    }
}
