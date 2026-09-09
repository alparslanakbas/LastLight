using LastLight.Skills;
using UnityEngine;

namespace LastLight.World
{
    /// <summary>
    /// Gunes acisini, ortam isigini ve gun sayacini yonetir.
    ///
    /// Gece bilincli olarak COK karanlik: merkez mekanik isik ekonomisi
    /// oldugu icin gecenin kendisi tehdit olmali. Oyuncunun gece rahatca
    /// gorebildigi bir oyunda mesale uretmenin anlami kalmaz.
    /// </summary>
    public sealed class DayNightCycle : MonoBehaviour
    {
        [Header("Sure (saniye)")]
        [SerializeField] float dayDuration = 480f;    // 8 dk
        [SerializeField] float nightDuration = 720f;  // 12 dk - gece daha uzun, baski surekli

        [Header("Referans")]
        [SerializeField] Light sun;

        [Header("Isik siddeti")]
        [SerializeField] float dayIntensity = 1.1f;
        [SerializeField] float nightIntensity = 0.02f;

        [Header("Ortam isigi")]
        [SerializeField] float dayAmbientIntensity = 1.0f;
        [SerializeField] float nightAmbientIntensity = 0.06f;

        [Header("Gokyuzu")]
        [SerializeField] float dayExposure = 1.0f;
        [SerializeField] float nightExposure = 0.12f;

        [Header("Sis")]
        [SerializeField] Color dayFog = new(0.62f, 0.70f, 0.80f);
        [SerializeField] Color nightFog = new(0.03f, 0.04f, 0.07f);
        [SerializeField] float dayFogEnd = 260f;
        [SerializeField] float nightFogEnd = 90f;   // gece gorus mesafesi kisa

        /// <summary>Kacinci gun (1'den baslar). Beceri seviyesi buna baglanacak.</summary>
        public int DayNumber { get; private set; } = 1;

        /// <summary>Hayatta kalinan gece sayisi - XP kaynagi.</summary>
        public int NightsSurvived { get; private set; }

        public bool IsNight { get; private set; }

        /// <summary>Icinde bulunulan evrenin ilerleyisi, 0-1.</summary>
        public float PhaseProgress { get; private set; }

        float _timer;

        public static DayNightCycle Instance { get; private set; }

        void Awake()
        {
            Instance = this;
            if (sun == null) sun = FindAnyObjectByType<Light>();
            // Ortam isigi HDRI'den geliyor: duz renk ambient'te gunes almayan
            // blok yuzleri simsiyah kaliyordu. Gokyuzunden gelen isik golgeleri
            // gokyuzu rengine boyuyor - gercek disari aydinlatmasi boyle.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;

            // Mesafe sisi derinlik algisini uretiyor: sissiz bir voxel dunyada
            // uzak bloklar yakinlarla ayni netlikte kaliyor ve sahne duz
            // gorunuyor. Gece sis cok daha yakin - gorus mesafesini kisaltmak
            // isik ekonomisinin baskisini dogrudan artiriyor.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 25f;
        }

        void Update()
        {
            float duration = IsNight ? nightDuration : dayDuration;
            _timer += Time.deltaTime;

            if (_timer >= duration)
            {
                _timer = 0f;
                if (IsNight)
                {
                    // Gece bitti: oyuncu hayatta kaldi.
                    NightsSurvived++;
                    DayNumber++;
                }
                IsNight = !IsNight;
            }

            PhaseProgress = _timer / duration;
            Apply();
        }

        void Apply()
        {
            // Gunes gunduz dogu-bati yayini cizer, gece ufkun altinda kalir.
            float angle = IsNight
                ? Mathf.Lerp(180f, 360f, PhaseProgress)
                : Mathf.Lerp(0f, 180f, PhaseProgress);

            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(angle, 150f, 0f);

                // Gun dogumu/batiminda yumusak gecis: sert kesme goz yoruyor
                // ve "gece basti" anini hissettirmiyor.
                float t = IsNight
                    ? Mathf.Clamp01(Mathf.Min(PhaseProgress, 1f - PhaseProgress) * 8f)
                    : Mathf.Clamp01(Mathf.Min(PhaseProgress, 1f - PhaseProgress) * 8f);

                sun.intensity = IsNight
                    ? Mathf.Lerp(dayIntensity, nightIntensity, t)
                    : Mathf.Lerp(nightIntensity, dayIntensity, t);

                sun.color = IsNight
                    ? new Color(0.35f, 0.42f, 0.62f)   // solgun ay isigi
                    : new Color(1f, 0.95f, 0.85f);

                float ambient = IsNight
                    ? Mathf.Lerp(dayAmbientIntensity, nightAmbientIntensity, t)
                    : Mathf.Lerp(nightAmbientIntensity, dayAmbientIntensity, t);

                // "Gece Gozu" karanligi tamamen kaldirmiyor, sadece siyahi
                // biraz aciyor - kaldirsaydi merkez mekanik ise yaramaz olurdu.
                float bonus = PlayerSkills.Instance?.State.NightAmbientBonus ?? 0f;
                if (IsNight && bonus > 0f) ambient += bonus * 3f;

                RenderSettings.ambientIntensity = ambient;

                RenderSettings.fogColor = IsNight
                    ? Color.Lerp(dayFog, nightFog, t)
                    : Color.Lerp(nightFog, dayFog, t);

                // Gorus mesafesi ayari sise carpan olarak giriyor: kamera
                // kirpma duzlemini tek basina degistirmek sisin icinde hicbir
                // sey degistirmiyor, gorunen mesafeyi belirleyen sis.
                float viewScale = LastLight.Settings.GameSettings.ViewDistanceScale;
                RenderSettings.fogEndDistance = viewScale * (IsNight
                    ? Mathf.Lerp(dayFogEnd, nightFogEnd, t)
                    : Mathf.Lerp(nightFogEnd, dayFogEnd, t));

                // HDRI sabit bir fotograf; gece hissini pozlama ve renk
                // veriyor. Ayri bir gece HDRI'sine gecmek sert bir kesme
                // uretiyordu, pozlama yumusak geciyor.
                if (RenderSettings.skybox != null && RenderSettings.skybox.HasProperty("_Exposure"))
                {
                    float exposure = IsNight
                        ? Mathf.Lerp(dayExposure, nightExposure, t)
                        : Mathf.Lerp(nightExposure, dayExposure, t);
                    RenderSettings.skybox.SetFloat("_Exposure", exposure);

                    // Gokyuzu gun icinde yavasca donuyor: sabit HDRI'de
                    // gunes hep ayni yerde duruyor ve zaman gecmiyor gibi.
                    float rot = (DayNumber * 40f + PhaseProgress * 60f) % 360f;
                    RenderSettings.skybox.SetFloat("_Rotation", rot);
                }
            }
        }

        /// <summary>Kayittan yukleme icin dongu durumunu geri yukler.</summary>
        public void RestoreState(int day, int nights, bool night, float phase)
        {
            DayNumber = day;
            NightsSurvived = nights;
            IsNight = night;
            PhaseProgress = Mathf.Clamp01(phase);

            float duration = IsNight ? nightDuration : dayDuration;
            _timer = PhaseProgress * duration;

            Apply();
        }

        /// <summary>HUD icin okunabilir saat.</summary>
        public string ClockText()
        {
            // Gunduz 06:00-18:00, gece 18:00-06:00 aralegina esleniyor.
            float hours = IsNight ? 18f + PhaseProgress * 12f : 6f + PhaseProgress * 12f;
            if (hours >= 24f) hours -= 24f;
            int h = Mathf.FloorToInt(hours);
            int m = Mathf.FloorToInt((hours - h) * 60f);
            return $"{h:00}:{m:00}";
        }
    }
}
