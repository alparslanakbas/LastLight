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

        [Header("Blok atlasi malzemesi")]
        [SerializeField] Material blockMaterial;

        [Header("Puruzsuz arazi malzemesi")]
        [SerializeField] Material terrainMaterial;

        readonly Dictionary<Vector3Int, Chunk> _chunks = new();
        readonly Dictionary<Vector3Int, ChunkView> _views = new();

        // Yeniden mesh'lenmesi gereken chunk'lar. Her karede tum chunk'lari
        // taramak kucuk dunyada ucuz ama dunya buyudukce bosa maliyet.
        readonly HashSet<Vector3Int> _dirty = new();

        bool _generated;

        void Start() => EnsureGenerated();

        /// <summary>
        /// Dunyayi bir kez uretir. Baska bilesenler (ornegin bitki ortusu)
        /// dunyanin hazir olmasina ihtiyac duyuyor ama Start sirasi garanti
        /// degil; kare beklemek de coz+m degil cunku Editor arka plandayken
        /// kare uretilmiyor. Talep uzerine uretim ikisini de asiyor.
        /// </summary>
        public void EnsureGenerated()
        {
            if (_generated) return;
            _generated = true;

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
        /// <summary>Kayit sistemi icin chunk koleksiyonu.</summary>
        public IReadOnlyDictionary<Vector3Int, Chunk> Chunks => _chunks;

        /// <summary>Kayittan yuklenen chunk'lari yerlestirir ve mesh'i yeniler.</summary>
        public void ApplyLoadedChunk(Vector3Int coord, BlockId[] blocks, BlockShape[] shapes, byte[] density)
        {
            if (!_chunks.TryGetValue(coord, out var chunk))
            {
                chunk = new Chunk(coord);
                _chunks[coord] = chunk;
            }

            chunk.LoadRaw(blocks, shapes, density);
            _dirty.Add(coord);

            // Puruzsuz arazi chunk sinirlarini asarak orneklendigi icin
            // komsular da yeniden uretilmeli; yoksa yuklenen dunyanin chunk
            // sinirlarinda catlaklar kaliyor.
            MarkDirty(coord + Vector3Int.left);  MarkDirty(coord + Vector3Int.right);
            MarkDirty(coord + Vector3Int.down);  MarkDirty(coord + Vector3Int.up);
            MarkDirty(coord + new Vector3Int(0, 0, -1));
            MarkDirty(coord + new Vector3Int(0, 0, 1));
        }

        /// <summary>Yukleme oncesi: dunya uretimini atlamak icin isaretler.</summary>
        public void MarkGenerated() => _generated = true;

        /// <summary>Dunya koordinatindaki blogun bicimi.</summary>
        public BlockShape GetShape(int wx, int wy, int wz)
        {
            Vector3Int coord = ToChunkCoord(wx, wy, wz);
            if (!_chunks.TryGetValue(coord, out var chunk)) return BlockShape.Cube;
            return chunk.GetShape(wx - coord.x * Chunk.Size, wy - coord.y * Chunk.Size, wz - coord.z * Chunk.Size);
        }

        public void SetShape(int wx, int wy, int wz, BlockShape shape)
        {
            Vector3Int coord = ToChunkCoord(wx, wy, wz);
            if (!_chunks.TryGetValue(coord, out var chunk)) return;
            chunk.SetShape(wx - coord.x * Chunk.Size, wy - coord.y * Chunk.Size, wz - coord.z * Chunk.Size, shape);
            _dirty.Add(coord);
        }

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
        /// Yogunluk alanindan okur: 0 = bos, 1 = tam dolu.
        ///
        /// SADECE Smooth isaretli voxel'ler alana katkida bulunuyor.
        /// Oyuncunun koydugu kupler alanin disinda: aksi halde havaya
        /// konan tek bir kup cevresinde yuvarlak bir yumru olusturur,
        /// hem kup hem yumru cizilirdi.
        /// </summary>
        public float Density(int wx, int wy, int wz)
        {
            Vector3Int coord = ToChunkCoord(wx, wy, wz);
            if (!_chunks.TryGetValue(coord, out var chunk)) return 0f;

            int lx = wx - coord.x * Chunk.Size;
            int ly = wy - coord.y * Chunk.Size;
            int lz = wz - coord.z * Chunk.Size;

            if (chunk.GetShape(lx, ly, lz) != BlockShape.Smooth) return 0f;
            return chunk.GetDensity(lx, ly, lz) / 255f;
        }

        /// <summary>
        /// Dogal arazi voxel'i yazar: hem tip hem yogunluk, bicim Smooth.
        /// Sehir tesviyesi gibi ARAZIYI degistiren isler bunu kullaniyor;
        /// SetBlock kullansalardi duzlenen alan kupsel bir plato olurdu.
        /// </summary>
        public void SetTerrain(int wx, int wy, int wz, BlockId id, byte density)
        {
            Vector3Int coord = ToChunkCoord(wx, wy, wz);
            if (!_chunks.TryGetValue(coord, out var chunk)) return;

            int lx = wx - coord.x * Chunk.Size;
            int ly = wy - coord.y * Chunk.Size;
            int lz = wz - coord.z * Chunk.Size;

            chunk.SetSmooth(lx, ly, lz, id, density);
            MarkNeighbours(coord, lx, ly, lz);
        }

        public void SetDensity(int wx, int wy, int wz, byte value)
        {
            Vector3Int coord = ToChunkCoord(wx, wy, wz);
            if (!_chunks.TryGetValue(coord, out var chunk)) return;

            int lx = wx - coord.x * Chunk.Size;
            int ly = wy - coord.y * Chunk.Size;
            int lz = wz - coord.z * Chunk.Size;

            chunk.SetDensity(lx, ly, lz, value);
            MarkNeighbours(coord, lx, ly, lz);
        }

        /// <summary>Bu voxel yogunluk alaninin parcasi mi (dogal arazi mi).</summary>
        public bool IsSmooth(int wx, int wy, int wz)
        {
            Vector3Int coord = ToChunkCoord(wx, wy, wz);
            if (!_chunks.TryGetValue(coord, out var chunk)) return false;

            return chunk.GetShape(
                wx - coord.x * Chunk.Size,
                wy - coord.y * Chunk.Size,
                wz - coord.z * Chunk.Size) == BlockShape.Smooth;
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

            // Dogal araziden blok kirmak alani OYMALI. chunk.Set bicimi
            // Cube'a sifirladigi icin, kirilan yer yogunluk alanindan cikip
            // etrafinda kupsel bir bosluk birakiyordu; oysa referans oyunda
            // kazilan yer yuvarlak bir krater.
            if (id == BlockId.Air && chunk.GetShape(lx, ly, lz) == BlockShape.Smooth)
                chunk.SetSmooth(lx, ly, lz, BlockId.Air, 0);
            else
                chunk.Set(lx, ly, lz, id);

            MarkNeighbours(coord, lx, ly, lz);
        }

        /// <summary>
        /// Chunk'i ve sinirdaysa komsularini kirli isaretler.
        ///
        /// Puruzsuz arazide bu daha da kritik: yogunluk alani chunk
        /// sinirlarini asarak orneklendigi icin bir voxel degisince komsu
        /// chunk'in yuzeyi de kayiyor. Isaretlemezsek sinirda catlak kaliyor.
        /// </summary>
        void MarkNeighbours(Vector3Int coord, int lx, int ly, int lz)
        {
            _dirty.Add(coord);

            if (lx <= 1) MarkDirty(coord + Vector3Int.left);
            if (lx >= Chunk.Size - 2) MarkDirty(coord + Vector3Int.right);
            if (ly <= 1) MarkDirty(coord + Vector3Int.down);
            if (ly >= Chunk.Size - 2) MarkDirty(coord + Vector3Int.up);
            if (lz <= 1) MarkDirty(coord + new Vector3Int(0, 0, -1));
            if (lz >= Chunk.Size - 2) MarkDirty(coord + new Vector3Int(0, 0, 1));
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

            ChunkMesher.Build(chunk, this, view.Mesh);
            SurfaceNets.Build(chunk, this, view.SmoothMesh);

            // Tum bloklar ayni atlasi kullaniyor: tek malzeme, tek draw call.
            view.Renderer.sharedMaterial = blockMaterial;
            view.SmoothRenderer.sharedMaterial = terrainMaterial != null ? terrainMaterial : blockMaterial;

            view.SmoothCollider.sharedMesh = null;
            if (view.SmoothMesh.vertexCount > 0)
                view.SmoothCollider.sharedMesh = view.SmoothMesh;

            // Once bosalt, yoksa Unity eski mesh'i tutuyor. Tamamen bos chunk'a
            // (hava) mesh atarsak Unity "mesh has no vertices" uyarisi veriyor
            // ve bos bir collider tutmanin faydasi da yok.
            view.Collider.sharedMesh = null;
            if (view.Mesh.vertexCount > 0)
                view.Collider.sharedMesh = view.Mesh;
        }

        /// <summary>Dusen bloklar da ayni atlas malzemesini kullaniyor.</summary>
        public Material GetBlockMaterial(BlockId id) => blockMaterial;
    }
}
