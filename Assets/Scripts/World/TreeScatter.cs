using System.Collections.Generic;
using LastLight.Voxel;
using UnityEngine;

namespace LastLight.World
{
    /// <summary>
    /// Mesh agaclari dunyaya serpistirir.
    ///
    /// Agaclar once bloktan yapiliyordu ama birkac kupten olusan bir agacin
    /// silueti okunmuyordu. Blok arazi + mesh bitki ortusu bu turun standart
    /// karisimi; agac da o tarafa geciyor.
    ///
    /// Birkac farkli mesh uretiliyor: tek mesh kullanilsa butun orman ayni
    /// agactan olusmus gibi duruyor.
    /// </summary>
    public sealed class TreeScatter : MonoBehaviour
    {
        const int BatchSize = 1023;
        const int Variants = 6;

        [Header("Yogunluk")]
        [SerializeField, Range(0f, 0.2f)] float density = 0.035f;
        [SerializeField] float maxDistance = 200f;

        [Header("Gorunum")]
        [SerializeField] Material treeMaterial;

        VoxelWorld _world;
        Camera _camera;

        readonly Mesh[] _meshes = new Mesh[Variants];
        readonly List<Matrix4x4>[] _instances = new List<Matrix4x4>[Variants];
        readonly Matrix4x4[] _batch = new Matrix4x4[BatchSize];

        void Start()
        {
            _camera = Camera.main;

            for (int i = 0; i < Variants; i++)
            {
                // Son iki varyant olu agac: yanik orman ve corak bolge icin.
                bool bare = i >= Variants - 2;
                _meshes[i] = TreeMeshBuilder.Build(1000 + i * 37, bare);
                _instances[i] = new List<Matrix4x4>();
            }

            _world = FindAnyObjectByType<VoxelWorld>();
            if (_world == null) return;

            _world.EnsureGenerated();
            Scatter();
        }

        void Update()
        {
            if (treeMaterial == null) return;

            Vector3 cam = _camera != null ? _camera.transform.position : Vector3.zero;
            float maxSqr = maxDistance * maxDistance;

            for (int v = 0; v < Variants; v++)
            {
                var list = _instances[v];
                if (list.Count == 0) continue;

                int count = 0;
                for (int i = 0; i < list.Count; i++)
                {
                    Vector3 p = list[i].GetColumn(3);
                    if ((p - cam).sqrMagnitude > maxSqr) continue;

                    _batch[count++] = list[i];
                    if (count == BatchSize)
                    {
                        Graphics.DrawMeshInstanced(_meshes[v], 0, treeMaterial, _batch, count);
                        count = 0;
                    }
                }

                if (count > 0)
                    Graphics.DrawMeshInstanced(_meshes[v], 0, treeMaterial, _batch, count);
            }
        }

        void Scatter()
        {
            var rng = new System.Random(777);
            int wx = _world.WorldSizeX, wz = _world.WorldSizeZ;
            int total = 0;

            for (int x = 2; x < wx - 2; x++)
            for (int z = 2; z < wz - 2; z++)
            {
                var biome = BiomeMap.At(x, z, wx, wz);
                float d = density * BiomeFactor(biome);
                if (d <= 0f || rng.NextDouble() > d) continue;

                int y = SurfaceY(x, z);
                if (y <= 0) continue;

                var ground = _world.GetBlock(x, y, z);
                if (!IsPlantable(ground)) continue;

                // Olu agaclar yalnizca yanik orman ve corak bolgede.
                bool deadZone = biome == BiomeType.BurntForest || biome == BiomeType.Wasteland;
                int variant = deadZone
                    ? Variants - 2 + rng.Next(2)
                    : rng.Next(Variants - 2);

                float scale = 0.8f + (float)rng.NextDouble() * 0.7f;
                float yaw = (float)rng.NextDouble() * 360f;
                var pos = new Vector3(x + 0.5f, y + 1f, z + 0.5f);

                _instances[variant].Add(
                    Matrix4x4.TRS(pos, Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale));
                total++;
            }

            Debug.Log("[Trees] " + total + " agac serpistirildi.");
        }

        static bool IsPlantable(BlockId id) =>
            id == BlockId.Grass || id == BlockId.Dirt || id == BlockId.Snow ||
            id == BlockId.Waste || id == BlockId.Ash;

        static float BiomeFactor(BiomeType biome) => biome switch
        {
            BiomeType.Forest => 1.0f,
            BiomeType.Snow => 0.55f,
            BiomeType.BurntForest => 0.45f,
            BiomeType.Desert => 0.03f,
            BiomeType.Wasteland => 0.12f,
            _ => 0.5f,
        };

        int SurfaceY(int x, int z)
        {
            for (int y = Chunk.Size * 4 - 1; y >= 0; y--)
                if (BlockDatabase.IsSolid(_world.GetBlock(x, y, z))) return y;
            return 0;
        }
    }
}
