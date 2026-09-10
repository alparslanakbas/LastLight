using UnityEngine;

namespace LastLight.Audio
{
    /// <summary>
    /// Tek atimlik sesleri calar.
    ///
    /// Her ses icin AudioSource yaratip yok etmek yerine havuz kullaniliyor:
    /// blok kirarken saniyede birkac ses cikiyor ve GameObject yaratmak Unity'de
    /// cop uretiyor. Havuz sabit sayida kaynak tutup en eskisini geri
    /// donduruyor - ses kesilse bile bu, cop toplama takilmasindan iyi.
    ///
    /// Ayni klibin ust uste tekrarini onlemek icin son calinan klip
    /// hatirlanip bir sonraki secimde eleniyor; aksi halde ayni tas sesi
    /// arka arkaya gelip yapay duruyor.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        const int PoolSize = 16;

        AudioSource[] _pool;
        int _next;

        AudioSource _ui;
        AudioClip _sonKlip;

        public static AudioManager Instance { get; private set; }

        void Awake()
        {
            Instance = this;

            _pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("Ses" + i);
                go.transform.SetParent(transform, false);

                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 1f;          // 3B: sesin yonu oynanista bilgi tasiyor
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 3f;
                src.maxDistance = 42f;
                _pool[i] = src;
            }

            var uiGo = new GameObject("SesUI");
            uiGo.transform.SetParent(transform, false);
            _ui = uiGo.AddComponent<AudioSource>();
            _ui.playOnAwake = false;
            _ui.spatialBlend = 0f;              // arayuz sesi kafanin icinde
        }

        /// <summary>Dunyadaki bir noktada calar.</summary>
        public void PlayAt(AudioClip[] variants, Vector3 position,
                           float volume = 1f, float pitchJitter = 0.08f)
        {
            var clip = Pick(variants);
            if (clip == null) return;

            var src = _pool[_next];
            _next = (_next + 1) % PoolSize;

            src.transform.position = position;
            src.clip = clip;
            src.volume = volume;
            src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            src.Play();
        }

        /// <summary>Arayuz sesi - konumu yok.</summary>
        public void PlayUI(AudioClip[] variants, float volume = 0.7f)
        {
            var clip = Pick(variants);
            if (clip == null) return;

            _ui.pitch = 1f;
            _ui.PlayOneShot(clip, volume);
        }

        AudioClip Pick(AudioClip[] variants)
        {
            if (variants == null || variants.Length == 0) return null;
            if (variants.Length == 1) return variants[0];

            // Son calinani eleyerek sec: iki denemede birakiyoruz, sonsuz
            // donguye girmemesi icin.
            var clip = variants[Random.Range(0, variants.Length)];
            if (clip == _sonKlip) clip = variants[Random.Range(0, variants.Length)];

            _sonKlip = clip;
            return clip;
        }

        // ---------- Kisayollar ----------
        // Cagri yerlerinin hem Instance hem SoundLibrary.Instance null
        // kontrolu yapmasi gerekmesin diye.

        public static void Oynat(AudioClip[] variants, Vector3 pos, float volume = 1f, float jitter = 0.08f)
            => Instance?.PlayAt(variants, pos, volume, jitter);

        public static void OynatUI(AudioClip[] variants, float volume = 0.7f)
            => Instance?.PlayUI(variants, volume);
    }
}
