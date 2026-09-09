using System.Collections.Generic;
using UnityEngine;

namespace LastLight.Voxel
{
    /// <summary>
    /// Bir chunk'i tek mesh'e cevirir. Her blok icin ayri GameObject uretmek
    /// birkac bin blokta kare hizini oldurur; burada sadece komsusu bos olan
    /// yuzler uretilir (face culling) ve hepsi tek mesh'te birlestirilir.
    /// </summary>
    public static class ChunkMesher
    {
        // Yuz yonleri: +X, -X, +Y, -Y, +Z, -Z
        static readonly Vector3Int[] Dirs =
        {
            new Vector3Int( 1, 0, 0), new Vector3Int(-1, 0, 0),
            new Vector3Int( 0, 1, 0), new Vector3Int( 0,-1, 0),
            new Vector3Int( 0, 0, 1), new Vector3Int( 0, 0,-1),
        };

        // Her yon icin yuzun 4 kosesi (birim kupun sol-alt-on kosesi orijinde).
        static readonly Vector3[][] FaceCorners =
        {
            new[] { new Vector3(1,0,0), new Vector3(1,1,0), new Vector3(1,1,1), new Vector3(1,0,1) }, // +X
            new[] { new Vector3(0,0,1), new Vector3(0,1,1), new Vector3(0,1,0), new Vector3(0,0,0) }, // -X
            new[] { new Vector3(0,1,0), new Vector3(0,1,1), new Vector3(1,1,1), new Vector3(1,1,0) }, // +Y
            new[] { new Vector3(0,0,1), new Vector3(0,0,0), new Vector3(1,0,0), new Vector3(1,0,1) }, // -Y
            new[] { new Vector3(1,0,1), new Vector3(1,1,1), new Vector3(0,1,1), new Vector3(0,0,1) }, // +Z
            new[] { new Vector3(0,0,0), new Vector3(0,1,0), new Vector3(1,1,0), new Vector3(1,0,0) }, // -Z
        };

        // Yeniden kullanilan tamponlar - her chunk icin yeni liste ayirmak GC baskisi yaratir.
        static readonly List<Vector3> Verts = new(4096);
        static readonly List<Vector3> Norms = new(4096);
        static readonly List<Vector2> Uvs = new(4096);

        // Tek liste: butun bloklar ayni atlas dokusunu kullandigi icin submesh
        // gerekmiyor. Onceki surumde her blok tipi ayri submesh'ti ve chunk
        // basina 13'e kadar draw call cikiyordu.
        static readonly List<int> Tris = new(6144);

        /// <summary>Atlas kenar uzunlugu (hucre sayisi). Uretecle ayni olmali.</summary>
        const int AtlasTiles = 4;

        /// <summary>
        /// Chunk'i mesh'e yazar. Her blok tipi ayri submesh olur; boylece tek
        /// materyal atlasi hazirlamadan tipe gore farkli malzeme atayabiliyoruz.
        /// </summary>
        public static void Build(Chunk chunk, VoxelWorld world, Mesh mesh)
        {
            Verts.Clear(); Norms.Clear(); Uvs.Clear(); Tris.Clear();

            Vector3Int origin = chunk.WorldOrigin;

            for (int z = 0; z < Chunk.Size; z++)
            for (int y = 0; y < Chunk.Size; y++)
            for (int x = 0; x < Chunk.Size; x++)
            {
                BlockId id = chunk.Get(x, y, z);
                if (!BlockDatabase.IsSolid(id)) continue;

                for (int d = 0; d < 6; d++)
                {
                    Vector3Int n = Dirs[d];
                    int nx = x + n.x, ny = y + n.y, nz = z + n.z;

                    // Chunk sinirindaysak komsu chunk'a bakmaliyiz; yoksa sinir
                    // duvarlari iki kez cizilir ve icerisi disaridan gorunur.
                    BlockId neighbour = Chunk.InBounds(nx, ny, nz)
                        ? chunk.Get(nx, ny, nz)
                        : world.GetBlock(origin.x + nx, origin.y + ny, origin.z + nz);

                    if (BlockDatabase.IsSolid(neighbour)) continue; // yuz gorunmez, atla

                    AddFace(new Vector3(x, y, z), d, id);
                }
            }

            mesh.Clear();
            if (Verts.Count == 0) return;

            mesh.indexFormat = Verts.Count > 65535
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

            mesh.SetVertices(Verts);
            mesh.SetNormals(Norms);
            mesh.SetUVs(0, Uvs);
            mesh.subMeshCount = 1;
            mesh.SetTriangles(Tris, 0);
            mesh.RecalculateBounds();
        }


        static void AddFace(Vector3 pos, int dir, BlockId id)
        {
            int baseIndex = Verts.Count;
            var corners = FaceCorners[dir];
            Vector3 normal = Dirs[dir];

            for (int i = 0; i < 4; i++)
            {
                Verts.Add(pos + corners[i]);
                Norms.Add(normal);
            }

            // Atlas hucresi: blok kimligi dogrudan hucre indeksi.
            int tile = (int)id;
            float cell = 1f / AtlasTiles;
            float tx = (tile % AtlasTiles) * cell;
            float ty = (tile / AtlasTiles) * cell;

            // Ic pay: tam kenardan orneklersek komsu hucrenin pikselleri sizip
            // blok kenarlarinda yabanci renk cizgileri olusuyor. Mipmap acik
            // oldugu icin uzak mesafede sizinti artiyor, bu yuzden pay genis.
            const float pad = 0.006f;
            float u0 = tx + pad, u1 = tx + cell - pad;
            float v0 = ty + pad, v1 = ty + cell - pad;

            Uvs.Add(new Vector2(u0, v0));
            Uvs.Add(new Vector2(u0, v1));
            Uvs.Add(new Vector2(u1, v1));
            Uvs.Add(new Vector2(u1, v0));

            Tris.Add(baseIndex + 0); Tris.Add(baseIndex + 1); Tris.Add(baseIndex + 2);
            Tris.Add(baseIndex + 0); Tris.Add(baseIndex + 2); Tris.Add(baseIndex + 3);
        }
    }
}
