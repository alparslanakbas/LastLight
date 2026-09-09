using UnityEngine;

namespace LastLight.Settings
{
    /// <summary>
    /// Oyuncunun ayarlari. PlayerPrefs'te tutuluyor, oyun kaydinda degil:
    /// fare hassasiyeti ve ses seviyesi kayda degil makineye ait. Kayit
    /// dosyasina koyarsak baska bir kayda gecen oyuncunun faresi degisir.
    ///
    /// Statik cunku ayara ihtiyaci olan yerler (oyuncu kontrolu, dongu, ana
    /// menu) farkli sahnelerde ve farkli omurlerde yasiyor; her birine
    /// referans gecirmek gereksiz baglanti uretiyordu.
    /// </summary>
    public static class GameSettings
    {
        const string KeyVolume = "ll_volume";
        const string KeySensitivity = "ll_sensitivity";
        const string KeyViewDistance = "ll_viewdistance";
        const string KeyFullscreen = "ll_fullscreen";
        const string KeyResolution = "ll_resolution";

        public const float MinSensitivity = 0.03f;
        public const float MaxSensitivity = 0.40f;

        /// <summary>0-1 arasi ana ses seviyesi.</summary>
        public static float Volume { get; private set; } = 0.8f;

        /// <summary>Fare hassasiyeti (PlayerController'in carpani).</summary>
        public static float Sensitivity { get; private set; } = 0.12f;

        /// <summary>Sis ve kamera uzak kirpma duzlemi carpani; 0.5 - 1.5.</summary>
        public static float ViewDistanceScale { get; private set; } = 1f;

        public static bool Fullscreen { get; private set; } = true;

        /// <summary>Screen.resolutions icindeki indeks; -1 = dokunma.</summary>
        public static int ResolutionIndex { get; private set; } = -1;

        static bool _loaded;

        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            Volume = PlayerPrefs.GetFloat(KeyVolume, 0.8f);
            Sensitivity = PlayerPrefs.GetFloat(KeySensitivity, 0.12f);
            ViewDistanceScale = PlayerPrefs.GetFloat(KeyViewDistance, 1f);
            Fullscreen = PlayerPrefs.GetInt(KeyFullscreen, 1) == 1;
            ResolutionIndex = PlayerPrefs.GetInt(KeyResolution, -1);

            ApplyAudio();
        }

        // Her ayar kendi setter'inda hem yaziliyor hem uygulaniyor: "kaydet"
        // dugmesi olmayan bir ayar ekrani, degisikligi aninda gormek isteyen
        // oyuncu icin daha iyi - ozellikle hassasiyet ve gorus mesafesinde
        // sonucu gormeden dogru degeri bulmak imkansiz.

        public static void SetVolume(float value)
        {
            Volume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(KeyVolume, Volume);
            ApplyAudio();
        }

        public static void SetSensitivity(float value)
        {
            Sensitivity = Mathf.Clamp(value, MinSensitivity, MaxSensitivity);
            PlayerPrefs.SetFloat(KeySensitivity, Sensitivity);
        }

        public static void SetViewDistance(float scale)
        {
            ViewDistanceScale = Mathf.Clamp(scale, 0.5f, 1.5f);
            PlayerPrefs.SetFloat(KeyViewDistance, ViewDistanceScale);
            ApplyCamera();
        }

        public static void SetFullscreen(bool value)
        {
            Fullscreen = value;
            PlayerPrefs.SetInt(KeyFullscreen, value ? 1 : 0);
            ApplyScreen();
        }

        public static void SetResolution(int index)
        {
            ResolutionIndex = index;
            PlayerPrefs.SetInt(KeyResolution, index);
            ApplyScreen();
        }

        public static void Save() => PlayerPrefs.Save();

        // ---------- Uygulama ----------

        static void ApplyAudio() => AudioListener.volume = Volume;

        public static void ApplyCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;

            // Uzak kirpma duzlemi sisin bittigi yerin biraz otesinde olmali;
            // esit olursa sisle kaybolan seyler yerine sert bir kesim gorunuyor.
            cam.farClipPlane = 300f * ViewDistanceScale + 40f;
        }

        static void ApplyScreen()
        {
            // Editor'de cozunurluk degistirmek Game view'i bozuyor ve oyun
            // penceresi zaten yok; sadece derlenmis oyunda uygula.
            if (Application.isEditor) return;

            var mode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            var list = Screen.resolutions;

            if (ResolutionIndex >= 0 && ResolutionIndex < list.Length)
            {
                var r = list[ResolutionIndex];
                Screen.SetResolution(r.width, r.height, mode, r.refreshRateRatio);
            }
            else
            {
                Screen.fullScreenMode = mode;
            }
        }

        /// <summary>Sahne yuklendikten sonra cagrilir; kamera o an var oluyor.</summary>
        public static void ApplyAll()
        {
            Load();
            ApplyAudio();
            ApplyCamera();
            ApplyScreen();
        }
    }
}
