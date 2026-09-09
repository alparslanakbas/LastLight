using System.Collections.Generic;
using LastLight.Voxel;
using UnityEngine;

namespace LastLight.World
{
    /// <summary>
    /// Zemine bitki ortusu serpistirir.
    ///
    /// Referans oyunlarda araziyi "zengin" gosteren sey agaclar degil, zemini
    /// kaplayan yuzlerce kucuk bitki. Ciplak zemin, dokusu ne kadar iyi olursa
    /// olsun ciplak gorunuyor.
    ///
    /// Her bitki icin GameObject uretmek binlerce nesnede kare hizini oldurur;
    /// bunun yerine Graphics.DrawMeshInstanced kullaniliyor: tek mesh, tek
    /// malzeme, tek cizim cagrisinda 1023 kopya.
    /// </summary>
    public sealed class FoliageScatter : MonoBehaviour
    {
        const int BatchSize = 1023;   // DrawMeshInstanced'in tek cagrida sinirı

        [Header("Yogunluk")]
        [SerializeField, Range(0f, 1f)] float grassDensity = 0.85f;
        [SerializeField] float maxDistance = 120f;     // bu mesafeden oteye cizilmiyor

        [Header("Gorunum")]
        [SerializeField] Material foliageMaterial;
        // Once cok kucuktu, sonra asiriya kacti: 1-2 metrelik cim kameranin
        // icinde kaliyordu. Bir blok 1 metre oldugu icin cim yarim metrenin
        // altinda kalmali.
        [SerializeField] float minScale = 0.30f;
        [SerializeField] float maxScale = 0.62f;

        VoxelWorld _world;
        Mesh _grassMesh;
        readonly List<Matrix4x4> _instances = new();
        readonly Matrix4x4[] _batch = new Matrix4x4[BatchSize];
        Camera _camera;

        void Start()
        {
            _camera = Camera.main;
            _grassMesh = BuildGrassMesh();
            _world = FindAnyObjectByType<VoxelWorld>();

            if (_world == null) return;

            // Dunyanin uretilmis olmasini garantiye aliyoruz: Start sirasi
            // belirsiz ve kare beklemek ise yaramiyor (Editor arka plandayken
            // kare uretilmiyor, ilk denemede 0 bitki cikmasinin sebebi buydu).
            _world.EnsureGenerated();
            Scatter();
        }

        void Update()
        {
            if (_instances.Count == 0 || foliageMaterial == null) return;

            // Kameradan uzaktakileri elemek gerekiyor; hepsini her karede
            // cizmek 100 bin kopyada anlamsiz maliyet.
            Vector3 cam = _camera != null ? _camera.transform.position : Vector3.zero;
            float maxSqr = maxDistance * maxDistance;

            int count = 0;
            for (int i = 0; i < _instances.Count; i++)
            {
                Vector3 p = _instances[i].GetColumn(3);
                if ((p - cam).sqrMagnitude > maxSqr) continue;

                _batch[count++] = _instances[i];
                if (count == BatchSize)
                {
                    Graphics.DrawMeshInstanced(_grassMesh, 0, foliageMaterial, _batch, count);
                    count = 0;
                }
            }

            if (count > 0)
                Graphics.DrawMeshInstanced(_grassMesh, 0, foliageMaterial, _batch, count);
        }

        // ---------- Serpistirme ----------

        void Scatter()
        {
            _instances.Clear();
            var rng = new System.Random(4242);

            int wx = _world.WorldSizeX;
            int wz = _world.WorldSizeZ;

            for (int x = 0; x < wx; x++)
            for (int z = 0; z < wz; z++)
            {
                var biome = BiomeMap.At(x, z, wx, wz);
                float density = DensityFor(biome) * grassDensity;
                if (density <= 0f) continue;
                if (rng.NextDouble() > density) continue;

                int y = SurfaceY(x, z);
                if (y <= 0) continue;

                // Yalnizca dogal zemine: yol ve bina uzerinde bitki olmamali.
                var ground = _world.GetBlock(x, y, z);
                if (!IsNaturalGround(ground)) continue;

                // Blok icinde rastgele konum - izgara hissini kiriyor.
                float ox = (float)rng.NextDouble();
                float oz = (float)rng.NextDouble();
                float scale = Mathf.Lerp(minScale, maxScale, (float)rng.NextDouble());
                float yaw = (float)rng.NextDouble() * 360f;

                // Tek nokta yerine kucuk kume: tek tek dagilmis bitkiler
                // izgara gibi duruyordu, kume dogal gorunuyor.
                int clump = 1 + rng.Next(3);
                for (int c = 0; c < clump; c++)
                {
                    float jx = c == 0 ? 0f : ((float)rng.NextDouble() - 0.5f) * 0.9f;
                    float jz = c == 0 ? 0f : ((float)rng.NextDouble() - 0.5f) * 0.9f;
                    float s2 = scale * (0.75f + (float)rng.NextDouble() * 0.5f);
                    float y2 = yaw + c * 47f;

                    var pos = new Vector3(x + ox + jx, y + 1f, z + oz + jz);
                    _instances.Add(Matrix4x4.TRS(pos, Quaternion.Euler(0f, y2, 0f), Vector3.one * s2));
                }
            }

            Debug.Log("[Foliage] " + _instances.Count + " bitki serpistirildi.");
        }

        static bool IsNaturalGround(BlockId id) =>
            id == BlockId.Grass || id == BlockId.Dirt || id == BlockId.Snow ||
            id == BlockId.Sand || id == BlockId.Waste || id == BlockId.Ash;

        /// <summary>Biyoma gore bitki yogunlugu - col ve corak bolge ciplak olmali.</summary>
        static float DensityFor(BiomeType biome) => biome switch
        {
            BiomeType.Forest => 1.0f,
            BiomeType.Snow => 0.35f,
            BiomeType.BurntForest => 0.25f,
            BiomeType.Desert => 0.06f,
            BiomeType.Wasteland => 0.10f,
            _ => 0.5f,
        };

        int SurfaceY(int x, int z)
        {
            for (int y = Chunk.Size * 4 - 1; y >= 0; y--)
                if (BlockDatabase.IsSolid(_world.GetBlock(x, y, z))) return y;
            return 0;
        }

        // ---------- Mesh ----------

        /// <summary>
        /// Capraz duran uc dortgen. Tek dortgen yandan bakinca kayboluyor;
        /// uc capraz her acidan hacimli gorunuyor ve bu bitki ortusunun
        /// standart cozumu.
        /// </summary>
        static Mesh BuildGrassMesh()
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            var norms = new List<Vector3>();

            for (int i = 0; i < 3; i++)
            {
                float angle = i * 60f * Mathf.Deg2Rad;
                Vector3 dir = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 side = dir * 0.5f;

                int b = verts.Count;
                verts.Add(-side);
                verts.Add(-side + Vector3.up);
                verts.Add(side + Vector3.up);
                verts.Add(side);

                uvs.Add(new Vector2(0, 0));
                uvs.Add(new Vector2(0, 1));
                uvs.Add(new Vector2(1, 1));
                uvs.Add(new Vector2(1, 0));

                // Normal yukari bakiyor: bitki golgeyi zeminden alsin, yandan
                // gelen isikla yanip sonmesin.
                for (int n = 0; n < 4; n++) norms.Add(Vector3.up);

                tris.Add(b + 0); tris.Add(b + 1); tris.Add(b + 2);
                tris.Add(b + 0); tris.Add(b + 2); tris.Add(b + 3);

                // Arka yuz: tek tarafli cizimde bitki arkadan gorunmez oluyor.
                tris.Add(b + 0); tris.Add(b + 2); tris.Add(b + 1);
                tris.Add(b + 0); tris.Add(b + 3); tris.Add(b + 2);
            }

            var mesh = new Mesh { name = "GrassCross" };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
