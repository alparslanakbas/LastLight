using LastLight.Audio;
using LastLight.Flow;
using LastLight.Persistence;
using LastLight.Settings;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace LastLight.UI
{
    /// <summary>
    /// ESC ile acilan duraklama ekrani.
    ///
    /// Ayri bir UIDocument: HUD belgesiyle ayni agaca koymak, duraklama
    /// panelinin HUD'un kaplama katmaniyla z sirasi icin yarismasi demekti.
    /// Ikinci belge daha yuksek sortingOrder ile her zaman ustte kaliyor.
    ///
    /// Duraklamada Time.timeScale sifirlaniyor. Dusman hareketi, isik yakiti
    /// ve gun dongusunun hepsi Time.deltaTime kullaniyor, dolayisiyla tek
    /// satirla hepsi duruyor - her sistemde ayri bir "durdu mu" bayragi
    /// tutmaya gerek kalmiyor.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class PauseMenuController : MonoBehaviour
    {
        VisualElement _veil, _home, _settingsPage;
        Label _status;
        float _statusTimer;

        public static bool IsPaused { get; private set; }

        void OnEnable()
        {
            IsPaused = false;
            GameSettings.ApplyAll();

            var root = GetComponent<UIDocument>().rootVisualElement;

            _veil = root.Q<VisualElement>("pauseVeil");
            _home = root.Q<VisualElement>("pauseHome");
            _settingsPage = root.Q<VisualElement>("pauseSettingsPage");
            _status = root.Q<Label>("pauseStatus");

            SettingsPanelBuilder.BuildInto(root.Q<VisualElement>("pauseSettingsHost"));

            Tikla(root.Q<Button>("resumeButton"), Resume);
            Tikla(root.Q<Button>("saveButton"), SaveNow);
            Tikla(root.Q<Button>("pauseSettings"), () => ShowSettings(true));
            Tikla(root.Q<Button>("pauseSettingsBack"), () => ShowSettings(false));
            Tikla(root.Q<Button>("toMenuButton"), ToMenu);

            ShowSettings(false);
        }

        void OnDisable() => IsPaused = false;

        /// <summary>
        /// Her dugmeye ayri ayri ses satiri yazmak yerine tek yerden
        /// bagliyoruz; yeni bir dugme eklenince sesini unutmak imkansiz.
        /// </summary>
        static void Tikla(UnityEngine.UIElements.Button btn, System.Action action)
        {
            if (btn == null) return;
            btn.clicked += () =>
            {
                AudioManager.OynatUI(SoundLibrary.Instance?.uiTik);
                action();
            };
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                // Ayarlar acikken ESC once ayarlari kapatsin: oyuncu yanlislikla
                // oyuna donup ayari yarim birakmasin.
                if (IsPaused && _settingsPage.ClassListContains("shell-page--open")) ShowSettings(false);
                else if (IsPaused) Resume();
                // Tab ekrani acikken ESC onu kapatmali; ikisi de ayni tusa
                // cevap verirse envanter kapanirken duraklama aciliyor ve
                // oyuncu tek tusla iki ekran degistirmis oluyor.
                else if (!GameMenuController.IsOpen) Pause();
            }

            // timeScale sifirken deltaTime de sifir; sayaci gercek zamanla isletiyoruz.
            if (_statusTimer > 0f)
            {
                _statusTimer -= Time.unscaledDeltaTime;
                if (_statusTimer <= 0f && _status != null) _status.text = "";
            }
        }

        public void Pause()
        {
            IsPaused = true;
            Time.timeScale = 0f;
            _veil.AddToClassList("shell-veil--open");
            ShowSettings(false);

            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        public void Resume()
        {
            IsPaused = false;
            Time.timeScale = 1f;
            _veil.RemoveFromClassList("shell-veil--open");

            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
        }

        void SaveNow()
        {
            bool ok = SaveSystem.Save();
            _status.text = ok ? "Kaydedildi" : "Kayit basarisiz";
            _statusTimer = 3f;
        }

        void ToMenu()
        {
            // Ana menuye donerken kaydetmek, "cikmadan kaydetmeyi unuttum"
            // durumunu ortadan kaldiriyor. Oyuncunun bilincli olarak
            // kaydetmemeyi secebilecegi bir tasarim degil - tek kayit yuvasi
            // var ve oyun otomatik kaydediyor zaten.
            SaveSystem.Save();
            GameFlow.ToMainMenu();
        }

        void ShowSettings(bool on)
        {
            _settingsPage.EnableInClassList("shell-page--open", on);
            _home.style.display = on ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
