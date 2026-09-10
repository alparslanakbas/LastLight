using UnityEngine;

namespace LastLight.Voxel
{
    /// <summary>Bir chunk'in sahnedeki gorsel/fiziksel karsiligi.</summary>
    public sealed class ChunkView : MonoBehaviour
    {
        public Mesh Mesh { get; private set; }
        public MeshRenderer Renderer { get; private set; }
        public MeshCollider Collider { get; private set; }

        // Puruzsuz arazi ayri bir nesnede: farkli malzeme (uc eksenli
        // dokuma) ve farkli mesh kullaniyor. Ayni nesnede iki alt mesh
        // olarak tutsaydik her yeniden uretimde ikisini birden yazmak
        // gerekirdi - oysa oyuncu blok koydugunda arazi degismiyor.
        public Mesh SmoothMesh { get; private set; }
        public MeshRenderer SmoothRenderer { get; private set; }
        public MeshCollider SmoothCollider { get; private set; }

        public static ChunkView Create(Transform parent, Vector3Int coord)
        {
            var go = new GameObject($"Chunk {coord.x},{coord.y},{coord.z}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = (Vector3)(coord * Chunk.Size);

            var view = go.AddComponent<ChunkView>();
            view.Mesh = new Mesh { name = $"ChunkMesh {coord}" };
            go.AddComponent<MeshFilter>().sharedMesh = view.Mesh;
            view.Renderer = go.AddComponent<MeshRenderer>();
            view.Collider = go.AddComponent<MeshCollider>();

            // MeshCollider eklendiginde Unity mesh'i MeshFilter'dan otomatik
            // aliyor; chunk henuz bos oldugu icin bu "mesh has no vertices"
            // uyarisi uretiyordu. Mesh dolunca VoxelWorld zaten atiyor.
            view.Collider.sharedMesh = null;

            var smoothGo = new GameObject("Arazi");
            smoothGo.transform.SetParent(go.transform, false);
            view.SmoothMesh = new Mesh { name = $"ChunkSmooth {coord}" };
            smoothGo.AddComponent<MeshFilter>().sharedMesh = view.SmoothMesh;
            view.SmoothRenderer = smoothGo.AddComponent<MeshRenderer>();
            view.SmoothCollider = smoothGo.AddComponent<MeshCollider>();
            view.SmoothCollider.sharedMesh = null;

            return view;
        }

        void OnDestroy()
        {
            if (Mesh != null) Destroy(Mesh);   // mesh'ler GC edilmez, elle temizlenir
            if (SmoothMesh != null) Destroy(SmoothMesh);
        }
    }
}
