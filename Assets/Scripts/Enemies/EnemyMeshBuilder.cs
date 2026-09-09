using System.Collections.Generic;
using UnityEngine;

namespace LastLight.Enemies
{
    /// <summary>
    /// Dusman silueti uretir: insansi ama bozuk oranlarda kutu yigini.
    ///
    /// Hazir karakter modeli yerine silueti tercih etmenin iki sebebi var.
    /// Birincisi varlik butcesi: animasyonlu bir insan modeli en pahali
    /// varlik turu. Ikincisi tasarim: dusmanlar "karanliktan cikan seyler"
    /// oldugu icin net bir yuz ve doku olmamasi korkuyu artiriyor - oyuncunun
    /// gordugu sey bir siluet.
    ///
    /// Uzuvlar govdeye gore kaydirilmis ve farkli boyutlarda: simetrik bir
    /// kutu adam oyuncakcik gibi duruyor.
    /// </summary>
    public static class EnemyMeshBuilder
    {
        public static Mesh Build(int seed, float heightScale, float bulk)
        {
            var rng = new System.Random(seed);

            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();

            float h = 1.8f * heightScale;

            // Govde: hafif one egik, ust tarafi genis
            AddBox(v, n, uv, t,
                center: new Vector3(0f, h * 0.58f, Jitter(rng, 0.05f)),
                size: new Vector3(0.46f * bulk, h * 0.42f, 0.28f * bulk),
                rng);

            // Kafa: govdeye gore kucuk ve one dusuk
            AddBox(v, n, uv, t,
                center: new Vector3(Jitter(rng, 0.05f), h * 0.87f, 0.06f + Jitter(rng, 0.04f)),
                size: new Vector3(0.24f, 0.24f, 0.24f),
                rng);

            // Kollar: farkli uzunlukta, biri sarkik
            for (int side = -1; side <= 1; side += 2)
            {
                float len = h * (0.34f + (float)rng.NextDouble() * 0.14f);
                AddBox(v, n, uv, t,
                    center: new Vector3(side * (0.30f * bulk), h * 0.60f - len * 0.4f, Jitter(rng, 0.08f)),
                    size: new Vector3(0.13f, len, 0.13f),
                    rng);
            }

            // Bacaklar
            for (int side = -1; side <= 1; side += 2)
            {
                float len = h * 0.40f;
                AddBox(v, n, uv, t,
                    center: new Vector3(side * 0.13f, len * 0.5f, Jitter(rng, 0.04f)),
                    size: new Vector3(0.16f, len, 0.17f),
                    rng);
            }

            var mesh = new Mesh { name = "EnemySilhouette" };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static float Jitter(System.Random rng, float amount) =>
            ((float)rng.NextDouble() - 0.5f) * 2f * amount;

        static void AddBox(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t,
                           Vector3 center, Vector3 size, System.Random rng)
        {
            Vector3 e = size * 0.5f;

            // Alti yuz, her biri ayri normalle - duz golgeli kutu istiyoruz.
            AddFace(v, n, uv, t, center, new Vector3(e.x, 0, 0), new Vector3(0, e.y, 0), new Vector3(0, 0, e.z));
            AddFace(v, n, uv, t, center, new Vector3(-e.x, 0, 0), new Vector3(0, 0, e.z), new Vector3(0, e.y, 0));
            AddFace(v, n, uv, t, center, new Vector3(0, e.y, 0), new Vector3(0, 0, e.z), new Vector3(e.x, 0, 0));
            AddFace(v, n, uv, t, center, new Vector3(0, -e.y, 0), new Vector3(e.x, 0, 0), new Vector3(0, 0, e.z));
            AddFace(v, n, uv, t, center, new Vector3(0, 0, e.z), new Vector3(0, e.y, 0), new Vector3(e.x, 0, 0));
            AddFace(v, n, uv, t, center, new Vector3(0, 0, -e.z), new Vector3(e.x, 0, 0), new Vector3(0, e.y, 0));
        }

        static void AddFace(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t,
                            Vector3 center, Vector3 normalAxis, Vector3 a, Vector3 b)
        {
            Vector3 o = center + normalAxis;
            int i = v.Count;

            v.Add(o - a - b);
            v.Add(o + a - b);
            v.Add(o + a + b);
            v.Add(o - a + b);

            Vector3 nrm = normalAxis.normalized;
            for (int k = 0; k < 4; k++) n.Add(nrm);

            // Doku kullanilmiyor (duz koyu malzeme), UV sadece gecerli olsun.
            uv.Add(new Vector2(0, 0));
            uv.Add(new Vector2(1, 0));
            uv.Add(new Vector2(1, 1));
            uv.Add(new Vector2(0, 1));

            t.Add(i + 0); t.Add(i + 1); t.Add(i + 2);
            t.Add(i + 0); t.Add(i + 2); t.Add(i + 3);
        }
    }
}
