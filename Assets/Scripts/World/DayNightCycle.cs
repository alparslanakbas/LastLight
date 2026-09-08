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

        [Header("Ortam rengi")]
        [SerializeField] Color dayAmbient = new(0.55f, 0.57f, 0.60f);
        [SerializeField] Color nightAmbient = new(0.025f, 0.03f, 0.05f);

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
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
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

                Color ambient = IsNight
                    ? Color.Lerp(dayAmbient, nightAmbient, t)
                    : Color.Lerp(nightAmbient, dayAmbient, t);

                // "Gece Gozu" karanligi tamamen kaldirmiyor, sadece siyahi
                // biraz aciyor - kaldirsaydi merkez mekanik ise yaramaz olurdu.
                float bonus = PlayerSkills.Instance?.State.NightAmbientBonus ?? 0f;
                if (IsNight && bonus > 0f)
                    ambient += new Color(bonus, bonus, bonus * 1.2f);

                RenderSettings.ambientLight = ambient;
            }
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
