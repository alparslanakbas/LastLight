using System;
using System.IO;
using LastLight.Audio;
using LastLight.Flow;
using LastLight.Persistence;
using LastLight.Settings;
using UnityEngine;
using UnityEngine.UIElements;

namespace LastLight.UI
{
    /// <summary>
    /// Ana menu. Uc sayfa tek belgede tutuluyor (ana, ayarlar, onay); sayfa
    /// gecisi sinif degistirerek yapiliyor. Ayri belgeler acilis gecikmesi
    /// ve referans dagitma isi getiriyordu.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MainMenuController : MonoBehaviour
    {
        VisualElement _home, _settings, _confirm;

        void OnEnable()
        {
            GameSettings.ApplyAll();

            // Menude imlec serbest olmali; oyundan cikip gelindiyse kilitli kalmis oluyor.
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            Time.timeScale = 1f;

            var root = GetComponent<UIDocument>().rootVisualElement;

            _home = root.Q<VisualElement>("homePage");
            _settings = root.Q<VisualElement>("settingsPage");
            _confirm = root.Q<VisualElement>("confirmPage");

            SettingsPanelBuilder.BuildInto(root.Q<VisualElement>("settingsHost"));

            var cont = root.Q<Button>("continueButton");
            Tikla(cont, GameFlow.ContinueGame);
            cont.SetEnabled(SaveSystem.SaveExists);

            Tikla(root.Q<Button>("newGameButton"), OnNewGame);
            Tikla(root.Q<Button>("settingsButton"), () => Show(_settings));
            Tikla(root.Q<Button>("settingsBack"), () => Show(_home));
            Tikla(root.Q<Button>("quitButton"), Quit);

            Tikla(root.Q<Button>("confirmYes"), GameFlow.StartNewGame);
            Tikla(root.Q<Button>("confirmNo"), () => Show(_home));

            root.Q<Label>("saveInfo").text = SaveInfoText();
            root.Q<Label>("version").text = "surum " + Application.version;

            Show(_home);
        }

        /// <summary>Tek yerden ses + eylem; yeni dugmede sesi unutmak imkansiz.</summary>
        static void Tikla(Button btn, System.Action action)
        {
            if (btn == null) return;
            btn.clicked += () =>
            {
                AudioManager.OynatUI(SoundLibrary.Instance?.uiTik);
                action();
            };
        }

        void OnNewGame()
        {
            // Kayit yoksa onay sormanin anlami yok - gereksiz tiklama.
            if (!SaveSystem.SaveExists) { GameFlow.StartNewGame(); return; }

            _confirm.Q<Label>("confirmText").text =
                "Kayitli oyununuz var (" + SaveInfoText() + "). Yeni oyun baslatmak onu " +
                "kalici olarak siler. Tek kayit yuvasi var.";
            Show(_confirm);
        }

        void Show(VisualElement page)
        {
            _home.style.display = page == _home ? DisplayStyle.Flex : DisplayStyle.None;
            _settings.EnableInClassList("shell-page--open", page == _settings);
            _confirm.EnableInClassList("shell-page--open", page == _confirm);
        }

        static string SaveInfoText()
        {
            if (!SaveSystem.SaveExists) return "Kayit yok";

            try
            {
                var t = File.GetLastWriteTime(SaveSystem.SavePath);
                return "Son kayit: " + t.ToString("dd.MM.yyyy HH:mm");
            }
            catch (Exception)
            {
                // Dosya var ama tarihi okunamiyorsa (kilit, izin) menuyu
                // patlatmaktansa bilgiyi atlamak dogru.
                return "Kayit mevcut";
            }
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
