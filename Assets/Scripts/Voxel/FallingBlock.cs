using UnityEngine;

namespace LastLight.Voxel
{
    /// <summary>
    /// Destegini kaybedip dusen blogun gecici fiziksel karsiligi.
    /// Voxel dunyasindan cikarilan blok burada rigidbody olarak dusuyor,
    /// omru dolunca yok oluyor.
    /// </summary>
    public sealed class FallingBlock : MonoBehaviour
    {
        /// <summary>Ayni anda sahnede bulunabilecek en fazla dusen blok.</summary>
        const int MaxActive = 200;
        const float Lifetime = 4f;

        static int _activeCount;
        static Mesh _sharedCube;

        float _age;

        public static void Spawn(VoxelWorld world, Vector3Int pos, BlockId id)
        {
            // Buyuk cokmelerde yuzlerce rigidbody kare hizini oldurur; limitin
            // ustundeki bloklar sessizce yok oluyor. Gorsel kayip, kare hizi kazanci.
            if (_activeCount >= MaxActive) return;

            var go = new GameObject($"Falling_{id}");
            go.transform.position = (Vector3)pos + Vector3.one * 0.5f;   // blok merkezi

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = GetCube();

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = world.GetBlockMaterial(id);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;  // kisa omurlu, golge gereksiz

            go.AddComponent<BoxCollider>();

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = Mathf.Max(1, BlockDatabase.Get(id).Mass);
            // Cok sayida kucuk kup ust uste dusunce cozucu titriyor; surekli
            // carpisma tespiti yerine hafif sonumleme daha ucuz ve yeterli.
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.4f;

            go.AddComponent<FallingBlock>();
            _activeCount++;
        }

        void Update()
        {
            _age += Time.deltaTime;
            if (_age >= Lifetime) Destroy(gameObject);
        }

        void OnDestroy() => _activeCount--;

        /// <summary>Tum dusen bloklarin paylastigi birim kup. Her blok icin mesh uretmek israf.</summary>
        static Mesh GetCube()
        {
            if (_sharedCube != null) return _sharedCube;

            var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _sharedCube = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(temp);
            return _sharedCube;
        }
    }
}
