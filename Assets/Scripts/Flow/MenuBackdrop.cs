using UnityEngine;

namespace LastLight.Flow
{
    /// <summary>
    /// Ana menu arka plani: oyunun HDRI gokyuzunu alacakaranliga cekip cok
    /// yavas dondurur.
    ///
    /// Duragan bir arka plan ekranin donmus oldugu izlenimi veriyor - oyuncu
    /// oyunun acilip acilmadigindan emin olamiyor. Cok yavas bir donus
    /// dikkat dagitmadan "calisiyor" sinyali veriyor.
    ///
    /// Gokyuzu malzemesinin KOPYASI kullaniliyor: dogrudan degistirsek
    /// Editor'de varlik dosyasi kalici olarak bozulur ve oyun sahnesi de
    /// alacakaranlikta kalirdi.
    /// </summary>
    public sealed class MenuBackdrop : MonoBehaviour
    {
        [SerializeField] float degreesPerSecond = 1.2f;

        // Parlak ogle gokyuzu "Last Light" tonuna aykiri; poz dusurulup
        // maviye cekilince ayni varlik aksama donusuyor.
        [SerializeField] float exposure = 0.30f;
        [SerializeField] Color tint = new(0.62f, 0.66f, 0.82f);

        void Awake()
        {
            var sky = RenderSettings.skybox;
            if (sky == null) return;

            var copy = new Material(sky);
            if (copy.HasProperty("_Exposure")) copy.SetFloat("_Exposure", exposure);
            if (copy.HasProperty("_Tint")) copy.SetColor("_Tint", tint);

            RenderSettings.skybox = copy;
            DynamicGI.UpdateEnvironment();
        }

        // unscaledDeltaTime: menuye duraklama uzerinden gelinirse timeScale
        // sifir kalmis olabiliyor.
        void Update() =>
            transform.Rotate(Vector3.up, degreesPerSecond * Time.unscaledDeltaTime, Space.World);
    }
}
