using LastLight.Flow;
using LastLight.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastLight.Persistence
{
    /// <summary>
    /// Kaydetmeyi tetikler: acilista yukleme, F5 ile elle kayit, her gun
    /// basinda otomatik kayit.
    ///
    /// Otomatik kayit gun basinda: geceyi atlatmak oyunun asil basarisi ve
    /// oyuncunun kaybetmek istemeyecegi ilerleme o. Sabit araliklı otomatik
    /// kayit (ornegin 5 dakikada bir) gecenin ortasinda kaydedip oyuncuyu
    /// cikamayacagi bir duruma kilitleyebiliyor.
    ///
    /// Yukleme Start'ta, dunya uretildikten sonra yapiliyor. Uretimi atlayip
    /// dogrudan yuklemek daha hizli olurdu ama Start sirasi garanti olmadigi
    /// icin kirilgan; uretilen dunyanin uzerine yazmak bir saniye kaybettiriyor
    /// ama her durumda dogru calisiyor.
    /// </summary>
    public sealed class GameSaveManager : MonoBehaviour
    {
        [SerializeField] bool loadOnStart = true;
        [SerializeField] bool autoSaveOnNewDay = true;

        int _lastSavedDay = -1;
        float _statusTimer;

        /// <summary>HUD'da gosterilecek son kayit mesaji.</summary>
        public static string StatusMessage { get; private set; } = "";

        public static GameSaveManager Instance { get; private set; }

        void Awake() => Instance = this;

        void Start()
        {
            // Ana menude "yeni oyun" secildiyse kayit zaten silindi, ama
            // bayragi da kontrol ediyoruz: silme basarisiz olsa bile yeni
            // oyun eski dunyayla baslamasin.
            if (!loadOnStart || !GameFlow.LoadSaveOnStart || !SaveSystem.SaveExists) return;

            if (SaveSystem.Load())
                ShowStatus("Kayit yuklendi");
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f5Key.wasPressedThisFrame) SaveNow("Kaydedildi (F5)");

            if (autoSaveOnNewDay)
            {
                var cycle = DayNightCycle.Instance;
                if (cycle != null && !cycle.IsNight && cycle.DayNumber != _lastSavedDay)
                {
                    _lastSavedDay = cycle.DayNumber;
                    // Ilk gunde kaydetmiyoruz: yeni oyunun basinda kayit
                    // yazmak, oyuncunun onceki kaydini istemeden ezebiliyor.
                    if (cycle.DayNumber > 1) SaveNow("Otomatik kaydedildi - Gun " + cycle.DayNumber);
                }
            }

            if (_statusTimer > 0f)
            {
                _statusTimer -= Time.deltaTime;
                if (_statusTimer <= 0f) StatusMessage = "";
            }
        }

        public void SaveNow(string message)
        {
            if (SaveSystem.Save()) ShowStatus(message);
            else ShowStatus("Kayit basarisiz");
        }

        void ShowStatus(string message)
        {
            StatusMessage = message;
            _statusTimer = 3.5f;
        }

        void OnApplicationQuit()
        {
            // Cikista kaydetmek, oyuncunun "kaydetmeyi unuttum" durumunu
            // tamamen ortadan kaldiriyor.
            SaveSystem.Save();
        }
    }
}
