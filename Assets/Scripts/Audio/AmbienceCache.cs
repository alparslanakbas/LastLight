using UnityEngine;

namespace LastLight.Audio
{
    /// <summary>
    /// Uretilen ortam kliplerini bir kez uretip saklar.
    ///
    /// Ates klibi 8 saniyelik ve uretimi yuz binlerce ornek hesabi demek.
    /// Her mesale kendi klibini uretseydi oyuncu on mesale koydugunda on kez
    /// bu hesap yapilir ve oyun gorunur sekilde takilirdi. Ayni klibi
    /// paylasmak ayrica bellek de kazandiriyor.
    ///
    /// Farkli mesalelerin ayni anda ayni yerden calmasi sorun degil: her
    /// AudioSource klibin rastgele bir yerinden basliyor.
    /// </summary>
    public static class AmbienceCache
    {
        static AudioClip _ates;

        public static AudioClip Ates
        {
            get
            {
                if (_ates == null) _ates = ProceduralAmbience.Ates();
                return _ates;
            }
        }

        /// <summary>
        /// Play modundan cikinca uretilen klip yok olmus sayilir; Editor'de
        /// ikinci oynatista "eski nesneye erisim" hatasi almamak icin
        /// referansi temizliyoruz.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Sifirla() => _ates = null;
    }
}
