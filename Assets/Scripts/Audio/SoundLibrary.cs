using LastLight.Voxel;
using UnityEngine;

namespace LastLight.Audio
{
    /// <summary>
    /// Oyundaki tum ses kliplerinin tek adresi.
    ///
    /// Klipler sahne kurulum script'i tarafindan dosya yolundan yukleniyor
    /// (bkz. SceneBootstrap). Elle surukle-birak yerine yoldan yuklemek,
    /// sahne bozulup yeniden kuruldugunda seslerin de geri gelmesi demek -
    /// projenin geri kalani da ayni ilkeyle kurulu.
    ///
    /// Her ses icin dizi tutuluyor cunku tek klibi tekrar tekrar calmak
    /// ("makineli tufek etkisi") yurumeyi rahatsiz edici hale getiriyor.
    /// </summary>
    public sealed class SoundLibrary : MonoBehaviour
    {
        [Header("Adim")]
        public AudioClip[] adimCimen;
        public AudioClip[] adimBeton;
        public AudioClip[] adimKar;
        public AudioClip[] adimTahta;
        public AudioClip[] adimYumusak;

        [Header("Blok vurma")]
        public AudioClip[] vurTas;
        public AudioClip[] vurTahta;
        public AudioClip[] vurMetal;
        public AudioClip[] vurYumusak;

        [Header("Blok kirma / koyma")]
        public AudioClip[] kirTas;
        public AudioClip[] kirTahta;
        public AudioClip[] kirMetal;
        public AudioClip[] kirYumusak;
        public AudioClip[] blokKoy;

        [Header("Oyuncu ve dusman")]
        public AudioClip[] dusmanVurus;
        public AudioClip[] dusmanOlum;

        [Header("Arayuz")]
        public AudioClip[] uiTik;
        public AudioClip[] uiAc;
        public AudioClip[] uiKapa;
        public AudioClip[] uiOnay;
        public AudioClip[] uiHata;
        public AudioClip[] uiUretim;
        public AudioClip[] uiToplama;

        public static SoundLibrary Instance { get; private set; }

        void Awake() => Instance = this;

        // ---------- Blok turune gore ses secimi ----------
        //
        // Ayri ayri alan tutmak yerine tek yerde eslestiriyoruz: yeni blok
        // eklendiginde ses secimi tek switch'te kaliyor, on parcaya dagilmiyor.

        public AudioClip[] AdimSesi(BlockId id) => id switch
        {
            BlockId.Grass or BlockId.Leaves => adimCimen,
            BlockId.Stone or BlockId.Concrete or BlockId.Road or BlockId.Metal => adimBeton,
            BlockId.Snow => adimKar,
            BlockId.Wood => adimTahta,
            _ => adimYumusak,   // toprak, kum, kul, atik
        };

        public AudioClip[] VurusSesi(BlockId id) => id switch
        {
            BlockId.Stone or BlockId.Concrete or BlockId.Road => vurTas,
            BlockId.Wood or BlockId.Leaves => vurTahta,
            BlockId.Metal => vurMetal,
            _ => vurYumusak,
        };

        public AudioClip[] KirmaSesi(BlockId id) => id switch
        {
            BlockId.Stone or BlockId.Concrete or BlockId.Road => kirTas,
            BlockId.Wood or BlockId.Leaves => kirTahta,
            BlockId.Metal => kirMetal,
            _ => kirYumusak,
        };
    }
}
