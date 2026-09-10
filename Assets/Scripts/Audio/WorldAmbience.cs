using LastLight.World;
using UnityEngine;

namespace LastLight.Audio
{
    /// <summary>
    /// Surekli calan ruzgar dokusu. Gece yukseliyor ve pesleniyor.
    ///
    /// Oyunun tek gerilim kaynagi karanlik; sessizlikte karanlik yalnizca
    /// "goremiyorum" demek, ruzgarin yukselmesi ise "disarisi tehlikeli"
    /// diyor. Gorsel bir sey eklemeden gecenin agirligini artiran en ucuz yol.
    ///
    /// Iki kaynak ve capraz gecis yok: tek kaynagin ses ve perde degeri
    /// yumusakca suruyor. Gecis 20 dakikalik dongude zaten yavas oldugu icin
    /// ayrica karistirmaya gerek kalmiyor.
    /// </summary>
    public sealed class WorldAmbience : MonoBehaviour
    {
        [SerializeField] float gunduzSes = 0.10f;
        [SerializeField] float geceSes = 0.30f;

        AudioSource _src;

        void Start()
        {
            _src = gameObject.AddComponent<AudioSource>();
            _src.clip = ProceduralAmbience.Ruzgar();
            _src.loop = true;
            _src.spatialBlend = 0f;
            _src.volume = gunduzSes;
            _src.Play();
        }

        void Update()
        {
            var c = DayNightCycle.Instance;
            if (c == null || _src == null) return;

            // Gecis ilerlemesiyle yumusatiyoruz: gece bir anda basmasin,
            // gun batimi boyunca sinsice yukselsin.
            float t = Mathf.Clamp01(c.PhaseProgress);
            float hedef = c.IsNight
                ? Mathf.Lerp(gunduzSes, geceSes, Mathf.Min(1f, t * 3f))
                : Mathf.Lerp(geceSes, gunduzSes, Mathf.Min(1f, t * 3f));

            // deltaTime bilerek: duraklamada timeScale sifir oldugu icin
            // ses seviyesi de donuyor ve menudeyken kendiliginden degismiyor.
            _src.volume = Mathf.MoveTowards(_src.volume, hedef, 0.08f * Time.deltaTime);
            _src.pitch = c.IsNight ? 0.88f : 1f;
        }
    }
}
