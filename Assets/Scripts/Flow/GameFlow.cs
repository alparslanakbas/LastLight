using UnityEngine.SceneManagement;

namespace LastLight.Flow
{
    /// <summary>
    /// Sahneler arasi gecis ve "yeni oyun mu, devam mi" karari.
    ///
    /// Ana menu ayri bir sahne. Tek sahnede menuyu kaplama olarak gostermek
    /// daha az dosya olurdu ama "yeni oyun" o zaman calisan bir dunyayi
    /// sifirlamak demek: envanter, dusmanlar, isiklar, gun sayaci, kirilmis
    /// bloklar. Sahneyi bastan yuklemek bu sifirlamalarin hepsini bedava
    /// yapiyor ve ileride unutulacak bir alan birakmiyor.
    ///
    /// Ayrica menudeyken dunya uretilmiyor - oyun aninda aciliyor.
    /// </summary>
    public static class GameFlow
    {
        public const string MenuScene = "MainMenu";
        public const string GameScene = "SampleScene";

        /// <summary>
        /// Oyun sahnesi acildiginda kaydin yuklenip yuklenmeyecegi.
        /// Ana menu bunu ayarlayip sahneyi yukluyor; oyun sahnesi dogrudan
        /// (Editor'de Play ile) acildiginda varsayilan "devam et" oluyor.
        /// </summary>
        public static bool LoadSaveOnStart { get; private set; } = true;

        public static void StartNewGame()
        {
            Persistence.SaveSystem.DeleteSave();
            LoadSaveOnStart = false;
            LoadGame();
        }

        public static void ContinueGame()
        {
            LoadSaveOnStart = true;
            LoadGame();
        }

        public static void ToMainMenu()
        {
            UnityEngine.Time.timeScale = 1f;
            SceneManager.LoadScene(MenuScene);
        }

        static void LoadGame()
        {
            UnityEngine.Time.timeScale = 1f;
            SceneManager.LoadScene(GameScene);
        }
    }
}
