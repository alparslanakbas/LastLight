using LastLight.Voxel;
using LastLight.World;
using UnityEngine;

namespace LastLight.Enemies
{
    public enum EnemyRole
    {
        /// <summary>Yavas, kalabalik. Temel baski.</summary>
        Walker,
        /// <summary>Bloklara vurup duvari deler - yapisal butunlugu oyuna sokan tip.</summary>
        Breaker,
        /// <summary>Hizli ve kirilgan; dogrudan isik kaynagina kosup sondurur.</summary>
        Snuffer,
    }

    /// <summary>
    /// Dusman davranisi.
    ///
    /// Isik ekonomisi merkez mekanik oldugu icin dusmanlar isikla iliskili
    /// tasarlandi: Walker ve Breaker isikta yavasliyor ve yipraniyor, Snuffer
    /// ise dogrudan isiga kosuyor. Boylece isik hem kalkan hem hedef oluyor -
    /// oyuncu isigi hem yakmak hem savunmak zorunda.
    ///
    /// Yol bulma yok: dusman hedefe dogru yuruyor, onunde blok varsa bir blok
    /// tirmaniyor, tirmanamiyorsa Breaker kiriyor digerleri yana kayiyor.
    /// Gercek yol bulma voxel dunyada her blok degisiminde grafi yenilemek
    /// demek; bu asamada maliyeti kazancindan buyuk.
    /// </summary>
    public sealed class Enemy : MonoBehaviour
    {
        public EnemyRole Role { get; private set; }

        VoxelWorld _world;
        Transform _player;
        float _health;
        float _speed;
        float _damage;
        float _attackCooldown;
        float _breakCooldown;
        float _groundY;

        // Isik altinda gecen sure - yipranma bunun uzerinden isliyor.
        float _lightExposure;

        public static int AliveCount { get; private set; }

        public void Init(EnemyRole role, VoxelWorld world, Transform player)
        {
            Role = role;
            _world = world;
            _player = player;

            switch (role)
            {
                case EnemyRole.Walker:
                    _health = 40f; _speed = 1.9f; _damage = 8f; break;
                case EnemyRole.Breaker:
                    _health = 80f; _speed = 1.4f; _damage = 14f; break;
                default:
                    _health = 22f; _speed = 3.6f; _damage = 5f; break;
            }

            AliveCount++;
        }

        void OnDestroy() => AliveCount--;

        void Update()
        {
            if (_world == null) return;

            // Dunya disina cikan dusman geri donemiyor (altinda zemin yok) ve
            // sonsuza kadar uzaklasiyor - olculen bir vakada 30 bin birime
            // kadar gitmisti. Sinir disina cikani temizliyoruz.
            if (IsOutOfBounds())
            {
                Destroy(gameObject);
                return;
            }

            // Kare araligi buyudugunde (Editor arka plandayken kare uretimi
            // duruyor, sonra tek karede toplu ilerliyor) dusman bir adimda
            // onlarca birim atliyor. Adim boyu sinirlandi.
            float dt = Mathf.Min(Time.deltaTime, 0.08f);
            _attackCooldown -= dt;
            _breakCooldown -= dt;

            var (target, isLight) = ChooseTarget();
            if (target == null) return;

            ApplyLightPressure(dt);
            MoveToward(target.Value, dt);
            TryAttack(target.Value, isLight, dt);
        }

        // ---------- Hedef ----------

        /// <summary>
        /// Sondurenler once isik arar, digerleri oyuncuya gider. Isik yoksa
        /// herkes oyuncuya yonelir - yoksa karanlikta dusmanlar oylece durur.
        /// </summary>
        (Vector3? pos, bool isLight) ChooseTarget()
        {
            if (Role == EnemyRole.Snuffer)
            {
                var light = NearestLight();
                if (light != null) return (light.transform.position, true);
            }

            if (_player != null) return (_player.position, false);
            return (null, false);
        }

        PlacedLight NearestLight()
        {
            var lights = Object.FindObjectsByType<PlacedLight>(FindObjectsSortMode.None);
            PlacedLight best = null;
            float bestSqr = 60f * 60f;   // bu mesafeden oteki isigi gormuyor

            foreach (var l in lights)
            {
                float d = (l.transform.position - transform.position).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = l; }
            }
            return best;
        }

        // ---------- Isik baskisi ----------

        void ApplyLightPressure(float dt)
        {
            if (Role == EnemyRole.Snuffer) return;   // sonduren isiga bagisik

            bool lit = IsInLight();
            if (lit)
            {
                _lightExposure += dt;
                // Isik oldurmuyor ama yipratiyor: oyuncunun isigi bir silah
                // degil, bir kalkan olmali.
                _health -= dt * 3.5f;
                if (_health <= 0f) { Destroy(gameObject); return; }
            }
            else
            {
                _lightExposure = Mathf.Max(0f, _lightExposure - dt * 0.5f);
            }
        }

        bool IsInLight()
        {
            var lights = Object.FindObjectsByType<PlacedLight>(FindObjectsSortMode.None);
            foreach (var l in lights)
            {
                float r = 9f;
                if ((l.transform.position - transform.position).sqrMagnitude < r * r) return true;
            }
            return false;
        }

        /// <summary>Isikta yavaslama - oyuncunun aydinlattigi alan guvenli hissettirmeli.</summary>
        float CurrentSpeed => Role != EnemyRole.Snuffer && IsInLight() ? _speed * 0.45f : _speed;

        // ---------- Hareket ----------

        void MoveToward(Vector3 target, float dt)
        {
            Vector3 flat = new(target.x - transform.position.x, 0f, target.z - transform.position.z);
            float dist = flat.magnitude;
            if (dist < 0.6f) return;

            Vector3 dir = flat / dist;
            Vector3 next = transform.position + dir * (CurrentSpeed * dt);

            int bx = Mathf.FloorToInt(next.x);
            int bz = Mathf.FloorToInt(next.z);
            int feetY = Mathf.FloorToInt(transform.position.y);

            // Onunde blok var mi? Bir blok yuksekligi tirmanilabilir.
            bool blockedLow = BlockDatabase.IsSolid(_world.GetBlock(bx, feetY, bz));
            bool blockedHigh = BlockDatabase.IsSolid(_world.GetBlock(bx, feetY + 1, bz));

            if (blockedLow && blockedHigh)
            {
                if (Role == EnemyRole.Breaker) BreakBlock(bx, feetY + 1, bz);
                else next = transform.position + Vector3.Cross(dir, Vector3.up) * (CurrentSpeed * dt * 0.7f);
            }

            transform.position = new Vector3(next.x, SnapToGround(next), next.z);
            transform.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.z));
        }

        /// <summary>Voxel zemine oturtur; yol bulma olmadigi icin yukseklik boyle takip ediliyor.</summary>
        float SnapToGround(Vector3 pos)
        {
            int x = Mathf.FloorToInt(pos.x), z = Mathf.FloorToInt(pos.z);
            int start = Mathf.Min(Chunk.Size * 4 - 1, Mathf.FloorToInt(pos.y) + 3);

            for (int y = start; y >= 0; y--)
                if (BlockDatabase.IsSolid(_world.GetBlock(x, y, z)))
                {
                    _groundY = y + 1f;
                    return _groundY;
                }

            // Zemin yok: dusman bosluga girmis. Oldugu yukseklikte birakmak
            // onu havada yuruten bir hataya donusuyordu; asagi dusurup sinir
            // kontrolune birakiyoruz.
            return pos.y - 6f * Time.deltaTime;
        }

        // ---------- Saldiri ----------

        void TryAttack(Vector3 target, bool targetIsLight, float dt)
        {
            float dist = Vector3.Distance(transform.position, target);
            if (dist > 1.9f) return;

            if (targetIsLight)
            {
                // Sonduren isigi hedefliyor: mesaleyi yok ediyor.
                var light = NearestLight();
                if (light != null && _attackCooldown <= 0f)
                {
                    Destroy(light.gameObject);
                    _attackCooldown = 1.2f;
                }
                return;
            }

            if (_attackCooldown > 0f) return;
            _attackCooldown = 1.1f;

            var hp = _player != null ? _player.GetComponent<PlayerHealth>() : null;
            if (hp != null) hp.TakeDamage(_damage);
            LastLight.Audio.AudioManager.Oynat(
                LastLight.Audio.SoundLibrary.Instance?.dusmanVurus, transform.position, 0.85f, 0.18f);
        }

        void BreakBlock(int x, int y, int z)
        {
            if (_breakCooldown > 0f) return;
            _breakCooldown = 1.4f;

            var block = _world.GetBlock(x, y, z);
            if (!BlockDatabase.IsSolid(block)) return;

            _world.SetBlock(x, y, z, BlockId.Air);
            StructuralIntegrity.Evaluate(_world, new Vector3Int(x, y, z));
        }

        bool IsOutOfBounds()
        {
            var p = transform.position;
            if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z)) return true;

            const float margin = 12f;
            return p.x < -margin || p.z < -margin ||
                   p.x > _world.WorldSizeX + margin || p.z > _world.WorldSizeZ + margin ||
                   p.y < -20f || p.y > 120f;
        }

        public void TakeDamage(float amount)
        {
            _health -= amount;
            if (_health <= 0f)
            {
                // Ses nesneyle birlikte yok olmasin diye havuzdan calıyoruz;
                // dusmanin uzerindeki bir AudioSource ile calsaydi Destroy
                // sesi ortasindan keserdi.
                LastLight.Audio.AudioManager.Oynat(
                    LastLight.Audio.SoundLibrary.Instance?.dusmanOlum, transform.position, 0.9f, 0.15f);
                Destroy(gameObject);
            }
        }
    }
}
