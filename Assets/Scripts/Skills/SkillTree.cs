using System.Collections.Generic;

namespace LastLight.Skills
{
    public enum SkillBranch { Dayaniklilik, Uretim, Kesif }

    public enum PerkId
    {
        None = 0,
        HizliAyak,      // kosma hizi
        YukTasiyici,    // envanter kapasitesi
        VerimliYakit,   // mesale daha uzun yanar
        UstaMarangoz,   // odun tarifleri fazla cikti
        GeceGozu,       // gece biraz daha iyi gorus
        Toplayici,      // blok kirinca ek kaynak sansi
    }

    public readonly struct PerkDef
    {
        public readonly PerkId Id;
        public readonly SkillBranch Branch;
        public readonly string Name;
        public readonly string Description;
        public readonly int Cost;

        public PerkDef(PerkId id, SkillBranch branch, string name, string desc, int cost)
        {
            Id = id; Branch = branch; Name = name; Description = desc; Cost = cost;
        }
    }

    public static class PerkDatabase
    {
        /// <summary>
        /// Her perk mevcut bir sisteme bagli. Henuz karsiligi olmayan bir yetenek
        /// (ornegin "silah hasari") listeye alinmadi - etkisi olmayan perk oyuncuyu
        /// kandiriyor ve agacin dengesini olcemez hale getiriyor.
        /// </summary>
        public static readonly IReadOnlyList<PerkDef> All = new[]
        {
            new PerkDef(PerkId.HizliAyak,   SkillBranch.Dayaniklilik, "Hizli Ayak",
                        "Kosma hizi %15 artar.", 1),
            new PerkDef(PerkId.YukTasiyici, SkillBranch.Dayaniklilik, "Yuk Tasiyici",
                        "Envanter kapasitesi artar.", 2),

            new PerkDef(PerkId.VerimliYakit, SkillBranch.Uretim, "Verimli Yakit",
                        "Mesaleler %30 daha uzun yanar.", 1),
            new PerkDef(PerkId.UstaMarangoz, SkillBranch.Uretim, "Usta Marangoz",
                        "Odun tariflerinden 1 fazla urun cikar.", 2),

            new PerkDef(PerkId.GeceGozu,   SkillBranch.Kesif, "Gece Gozu",
                        "Gece ortam isigi biraz artar.", 1),
            new PerkDef(PerkId.Toplayici,  SkillBranch.Kesif, "Toplayici",
                        "Blok kirinca %25 sansla 1 fazla kaynak.", 2),
        };

        public static PerkDef Get(PerkId id)
        {
            foreach (var p in All) if (p.Id == id) return p;
            return default;
        }
    }

    /// <summary>
    /// Oyuncunun beceri durumu. Puan kaynagi hayatta kalinan gece sayisi:
    /// oyuncu neyi odullendirirsen onu yapar - madencilikle seviye verirsek
    /// oyun madencilik oyunu olur, geceyle verirsek hayatta kalma oyunu.
    /// </summary>
    public sealed class SkillState
    {
        readonly HashSet<PerkId> _unlocked = new();

        public int TotalPoints { get; private set; }
        public int SpentPoints { get; private set; }
        public int AvailablePoints => TotalPoints - SpentPoints;

        public bool Has(PerkId id) => _unlocked.Contains(id);

        /// <summary>Gecilen gece sayisina gore puani gunceller.</summary>
        public void SyncPoints(int nightsSurvived)
        {
            TotalPoints = nightsSurvived;
        }

        public bool Unlock(PerkId id)
        {
            if (_unlocked.Contains(id)) return false;
            var def = PerkDatabase.Get(id);
            if (def.Id == PerkId.None) return false;
            if (AvailablePoints < def.Cost) return false;

            _unlocked.Add(id);
            SpentPoints += def.Cost;
            return true;
        }

        // ---------- Sistemlerin sordugu carpanlar ----------

        public float SprintMultiplier => Has(PerkId.HizliAyak) ? 1.15f : 1f;
        public float FuelDurationMultiplier => Has(PerkId.VerimliYakit) ? 1.30f : 1f;
        public int ExtraCraftOutput => Has(PerkId.UstaMarangoz) ? 1 : 0;
        public float NightAmbientBonus => Has(PerkId.GeceGozu) ? 0.035f : 0f;
        public float ExtraDropChance => Has(PerkId.Toplayici) ? 0.25f : 0f;
        public int ExtraInventorySlots => Has(PerkId.YukTasiyici) ? 8 : 0;
    }
}
