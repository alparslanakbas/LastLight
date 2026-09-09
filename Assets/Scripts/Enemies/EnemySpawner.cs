using LastLight.Voxel;
using LastLight.World;
using UnityEngine;

namespace LastLight.Enemies
{
    /// <summary>
    /// Gece dusman uretir, gunduz temizler.
    ///
    /// Dusmanlar yalnizca geceleri var: merkez mekanik isik ekonomisi oldugu
    /// icin tehdit karanliga bagli olmali. Gunduz bos bir dunya oyuncuya
    /// hazirlanma penceresi veriyor - baski surekli olsaydi hazirlanmak diye
    /// bir sey kalmazdi.
    ///
    /// Uretim oyuncudan uzakta ve isiktan uzakta yapiliyor: gozunun onunde
    /// beliren dusman tehdit degil, hata gibi gorunuyor.
    /// </summary>
    public sealed class EnemySpawner : MonoBehaviour
    {
        [Header("Uretim")]
        [SerializeField] int maxAlive = 22;
        [SerializeField] float spawnInterval = 3.5f;
        [SerializeField] float minDistance = 26f;
        [SerializeField] float maxDistance = 48f;

        [Header("Gorunum")]
        [SerializeField] Material enemyMaterial;

        VoxelWorld _world;
        Transform _player;
        float _timer;
        readonly Mesh[] _meshes = new Mesh[3];

        void Start()
        {
            _world = FindAnyObjectByType<VoxelWorld>();
            var player = GameObject.Find("Player");
            _player = player != null ? player.transform : null;

            // Rol basina bir siluet: Breaker iri, Snuffer ince ve kucuk.
            _meshes[(int)EnemyRole.Walker] = EnemyMeshBuilder.Build(11, 1.0f, 1.0f);
            _meshes[(int)EnemyRole.Breaker] = EnemyMeshBuilder.Build(22, 1.15f, 1.45f);
            _meshes[(int)EnemyRole.Snuffer] = EnemyMeshBuilder.Build(33, 0.9f, 0.7f);
        }

        void Update()
        {
            var cycle = DayNightCycle.Instance;
            if (cycle == null || _world == null || _player == null) return;

            if (!cycle.IsNight)
            {
                ClearAll();
                return;
            }

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = spawnInterval;

            // Gece ilerledikce baski artiyor: sabit sayida dusman gecenin
            // ortasinda sikici, sonunda kolay oluyor.
            int cap = Mathf.RoundToInt(Mathf.Lerp(6f, maxAlive, cycle.PhaseProgress));
            if (Enemy.AliveCount >= cap) return;

            SpawnOne(cycle);
        }

        void SpawnOne(DayNightCycle cycle)
        {
            for (int attempt = 0; attempt < 12; attempt++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                float dist = Random.Range(minDistance, maxDistance);
                Vector3 p = _player.position + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);

                int x = Mathf.FloorToInt(p.x), z = Mathf.FloorToInt(p.z);
                if (x < 1 || z < 1 || x >= _world.WorldSizeX - 1 || z >= _world.WorldSizeZ - 1) continue;

                int y = SurfaceY(x, z);
                if (y <= 0) continue;

                var spawnPos = new Vector3(x + 0.5f, y + 1f, z + 0.5f);
                if (IsLit(spawnPos)) continue;   // aydinlik yerde belirmesin

                Create(RollRole(cycle), spawnPos);
                return;
            }
        }

        /// <summary>
        /// Rol dagilimi gece boyunca degisiyor: basta kalabalik ve yavas,
        /// ilerledikce kirici ve sonduren orani artiyor. Oyuncunun savunmasi
        /// gece boyunca ayni kalmasin diye.
        /// </summary>
        EnemyRole RollRole(DayNightCycle cycle)
        {
            float t = cycle.PhaseProgress;
            float roll = Random.value;

            if (roll < Mathf.Lerp(0.15f, 0.35f, t)) return EnemyRole.Breaker;
            if (roll < Mathf.Lerp(0.25f, 0.55f, t)) return EnemyRole.Snuffer;
            return EnemyRole.Walker;
        }

        void Create(EnemyRole role, Vector3 pos)
        {
            var go = new GameObject("Enemy_" + role);
            go.transform.position = pos;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = _meshes[(int)role];

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = enemyMaterial;

            // Oyuncunun nisani raycast ile calisiyor; collider olmadan
            // dusmana vurulamiyor. Kapsul yerine kutu: siluet zaten kutu
            // yigini ve kapsul govdenin disina tasiyordu.
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.9f, 0f);
            col.size = new Vector3(0.6f, 1.8f, 0.5f);

            var enemy = go.AddComponent<Enemy>();
            enemy.Init(role, _world, _player);
        }

        void ClearAll()
        {
            if (Enemy.AliveCount == 0) return;
            foreach (var e in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
                Destroy(e.gameObject);
        }

        bool IsLit(Vector3 pos)
        {
            foreach (var l in FindObjectsByType<PlacedLight>(FindObjectsSortMode.None))
                if ((l.transform.position - pos).sqrMagnitude < 12f * 12f) return true;
            return false;
        }

        int SurfaceY(int x, int z)
        {
            for (int y = Chunk.Size * 4 - 1; y >= 0; y--)
                if (BlockDatabase.IsSolid(_world.GetBlock(x, y, z))) return y;
            return 0;
        }
    }
}
