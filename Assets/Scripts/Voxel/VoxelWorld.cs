using System.Collections.Generic;
using UnityEngine;

namespace LastLight.Voxel
{
    /// <summary>
    /// Chunk'lari tutar, dunya koordinatiyla blok okuma/yazma sunar ve
    /// degisen chunk'lari yeniden mesh'ler. Sahnede tek ornegi bulunur.
    /// </summary>
    public sealed class VoxelWorld : MonoBehaviour
    {
        [Header("Dunya boyutu (chunk cinsinden)")]
        [SerializeField] int sizeX = 8;
        [SerializeField] int sizeY = 2;
        [SerializeField] int sizeZ = 8;

        [Header("Uretim")]
        [SerializeField] int seed = 1337;
        [SerializeField] bool generateCity = true;

        [Header("Blok tipi basina malzeme (BlockId sirasiyla)")]
        [SerializeField] Material[] blockMaterials;

        readonly Dictionary<Vector3Int, Chunk> _chunks = new();
        readonly Dictionary<Vector3Int, ChunkView> _views = new();

        // Yeniden mesh'lenmesi gereken chunk'lar. Her karede tum chunk'lari
        // taramak kucuk dunyada ucuz ama dunya buyudukce bosa maliyet.
        readonly HashSet<Vector3Int> _dirty = new();

        void Start()
        {
            GenerateWorld();

            foreach (var coord in _chunks.Keys) _dirty.Add(coord);
            RebuildDirtyChunks();
        }

        void Update()
        {
            if (_dirty.Count > 0) RebuildDirtyChunks();
        }

        // ---------- Dunya koordinatiyla erisim ----------

        /// <summary>Dunya koordinatindaki blogu dondurur. Dunya disi = Air.</summary>
        /// <summary>Dunya genisligi (blok). Biyom sorgulari icin disariya acik.</summary>
        public int WorldSizeX => sizeX * Chunk.Size;
        public int WorldSizeZ => sizeZ * Chunk.Size;

        public BlockId GetBlock(int wx, int wy, int wz)
        {
            Vector3Int coord = ToChunkCoord(wx, wy, wz);
            if (!_chunks.TryGetValue(coord, out var chunk)) return BlockId.Air;

            return chunk.Get(
                wx - coord.x * Chunk.Size,
                wy - coord.y * Chunk.Size,
                wz - coord.z * Chunk.Size);
        }

        /// <summary>
        /// Blogu degistirir ve etkilenen chunk'lari kirli isaretler.
        /// Blok chunk sinirindaysa komsu chunk'in da mesh'i degisir - onu da isaretliyoruz,
        /// aksi halde sinirda gorunmez duvar veya delik kalir.
        /// </summary>
        public void SetBlock(int wx, int wy, int wz, BlockId id)
        {
            Vector3Int coord = ToChunkCoord(wx, wy, wz);
            if (!_chunks.TryGetValue(coord, out var chunk)) return;

            int lx = wx - coord.x * Chunk.Size;
            int ly = wy - coord.y * Chunk.Size;
            int lz = wz - coord.z * Chunk.Size;

            chunk.Set(lx, ly, lz, id);
            _dirty.Add(coord);

            if (lx == 0) MarkDirty(coord + Vector3Int.left);
            if (lx == Chunk.Size - 1) MarkDirty(coord + Vector3Int.right);
            if (ly == 0) MarkDirty(coord + Vector3Int.down);
            if (ly == Chunk.Size - 1) MarkDirty(coord + Vector3Int.up);
            if (lz == 0) MarkDirty(coord + new Vector3Int(0, 0, -1));
            if (lz == Chunk.Size - 1) MarkDirty(coord + new Vector3Int(0, 0, 1));
        }

        static Vector3Int ToChunkCoord(int wx, int wy, int wz) => new(
            Mathf.FloorToInt(wx / (float)Chunk.Size),
            Mathf.FloorToInt(wy / (float)Chunk.Size),
            Mathf.FloorToInt(wz / (float)Chunk.Size));

        void MarkDirty(Vector3Int coord)
        {
            if (!_chunks.TryGetValue(coord, out var c)) return;
            c.Dirty = true;
            _dirty.Add(coord);
        }

        // ---------- Uretim ve mesh ----------

        void GenerateWorld()
        {
            int worldX = sizeX * Chunk.Size;
            int worldZ = sizeZ * Chunk.Size;

            for (int cx = 0; cx < sizeX; cx++)
            for (int cy = 0; cy < sizeY; cy++)
            for (int cz = 0; cz < sizeZ; cz++)
            {
                var coord = new Vector3Int(cx, cy, cz);
                var chunk = new Chunk(coord);
                _chunks[coord] = chunk;
                TerrainGenerator.FillChunk(chunk, worldX, worldZ);
            }

            // Agaclar arazi bittikten sonra: bir agac chunk sinirini asabiliyor
            // ve o chunk henuz olusmamis olabilir.
            TerrainGenerator.PlantTrees(this, worldX, worldZ, seed);

            // Sehir en son: binalar zemin yuksekligini okuyarak oturuyor,
            // once kurulsa havada kalirdi.
            if (generateCity)
                CityGenerator.Generate(this, worldX, worldZ, seed);
        }

        /// <summary>Kirli chunk'lari yeniden mesh'ler. Temiz olanlara dokunmaz.</summary>
        public void RebuildDirtyChunks()
        {
            foreach (var coord in _dirty)
            {
                if (!_chunks.TryGetValue(coord, out var chunk)) continue;
                RebuildChunk(coord, chunk);
                chunk.Dirty = false;
            }
            _dirty.Clear();
        }

        void RebuildChunk(Vector3Int coord, Chunk chunk)
        {
            if (!_views.TryGetValue(coord, out var view))
            {
                view = ChunkView.Create(transform, coord);
                _views[coord] = view;
            }

            // Build, urettigi submesh'lerin blok tiplerini sirasiyla dondurur;
            // bu sira malzeme dizisiyle birebir ortusmezse bloklar yanlis cizilir.
            var types = ChunkMesher.Build(chunk, this, view.Mesh);
            var mats = new Material[types.Count];
            for (int i = 0; i < types.Count; i++)
                mats[i] = MaterialFor(types[i]);

            view.Renderer.sharedMaterials = mats;
            view.Collider.sharedMesh = null;          // once bosalt, yoksa Unity eski mesh'i tutar
            view.Collider.sharedMesh = view.Mesh;
        }

        /// <summary>Dusen bloklarin dogru renkte cizilebilmesi icin disariya acik.</summary>
        public Material GetBlockMaterial(BlockId id) => MaterialFor((int)id);

        Material MaterialFor(int blockType)
        {
            if (blockMaterials != null && blockType < blockMaterials.Length && blockMaterials[blockType] != null)
                return blockMaterials[blockType];
            return null;   // Unity pembe "missing material" gosterir - eksigi gorunur kilar
        }
    }
}
