using UnityEngine;

namespace LastLight.Enemies
{
    /// <summary>
    /// Oyuncunun cani. Dusmanlar eklenene kadar hasar alacak bir sey yoktu.
    ///
    /// Olum cezasi bilincli olarak yumusak: oyuncu baslangic noktasina donuyor
    /// ve envanterini koruyor. Sert olum cezasi (esya kaybi) hayatta kalma
    /// oyunlarinda ogrenme egrisini dikleştiriyor; bu asamada oyuncunun
    /// mekanikleri denemesi kaybetmekten daha degerli.
    /// </summary>
    public sealed class PlayerHealth : MonoBehaviour
    {
        [SerializeField] float maxHealth = 100f;
        [SerializeField] float regenPerSecond = 0.8f;
        [SerializeField] float regenDelay = 8f;

        public float Max => maxHealth;
        public float Current { get; private set; }
        public bool IsDead { get; private set; }

        public static PlayerHealth Instance { get; private set; }

        float _lastDamageTime;
        Vector3 _spawnPoint;

        void Awake()
        {
            Instance = this;
            Current = maxHealth;
            _spawnPoint = transform.position;
        }

        void Update()
        {
            if (IsDead) return;

            // Gecikmeli yenilenme: catismadan hemen sonra degil, guvenli bir
            // sure gectikten sonra. Boylece geri cekilmek anlamli oluyor.
            if (Time.time - _lastDamageTime > regenDelay && Current < maxHealth)
                Current = Mathf.Min(maxHealth, Current + regenPerSecond * Time.deltaTime);
        }

        public void TakeDamage(float amount)
        {
            if (IsDead) return;

            Current -= amount;
            _lastDamageTime = Time.time;

            if (Current <= 0f)
            {
                Current = 0f;
                Die();
            }
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            Current = Mathf.Min(maxHealth, Current + amount);
        }

        void Die()
        {
            IsDead = true;
            Debug.Log("[Player] Oldu - baslangic noktasina donuluyor.");
            Respawn();
        }

        void Respawn()
        {
            // CharacterController acikken transform'a yazmak yok sayiliyor;
            // once kapatip sonra tasimak gerekiyor.
            var cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            transform.position = _spawnPoint + Vector3.up * 2f;

            if (cc != null) cc.enabled = true;

            Current = maxHealth;
            IsDead = false;
        }
    }
}
