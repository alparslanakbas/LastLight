using System.Collections.Generic;

using LastLight.Skills;

namespace LastLight.Items
{
    public readonly struct Ingredient
    {
        public readonly ItemId Id;
        public readonly int Count;
        public Ingredient(ItemId id, int count) { Id = id; Count = count; }
    }

    public readonly struct Recipe
    {
        public readonly string Name;
        public readonly ItemId Output;
        public readonly int OutputCount;
        public readonly Ingredient[] Inputs;

        public Recipe(string name, ItemId output, int outputCount, params Ingredient[] inputs)
        {
            Name = name; Output = output; OutputCount = outputCount; Inputs = inputs;
        }
    }

    /// <summary>
    /// Tarif listesi. Zincir bilincli olarak isiga cikiyor:
    /// odun -> tahta -> cubuk ve odun -> yakit, ikisi birlesince mesale.
    /// Merkez mekanik isik ekonomisi oldugu icin uretimin ilk hedefi
    /// oyuncuya bir isik kaynagi vermek.
    /// </summary>
    public static class RecipeDatabase
    {
        public static readonly IReadOnlyList<Recipe> All = new[]
        {
            new Recipe("Tahta", ItemId.Plank, 4,
                new Ingredient(ItemId.Wood, 1)),

            new Recipe("Cubuk", ItemId.Stick, 2,
                new Ingredient(ItemId.Plank, 1)),

            // Odunu yakita cevirmek pahali: isik ucuz olmamali, yoksa
            // "yakit icin disari cik" baskisi kayboluyor.
            new Recipe("Yakit", ItemId.Fuel, 1,
                new Ingredient(ItemId.Wood, 3)),

            new Recipe("Mesale", ItemId.Torch, 2,
                new Ingredient(ItemId.Stick, 1),
                new Ingredient(ItemId.Fuel, 1)),

            new Recipe("Beton", ItemId.Concrete, 2,
                new Ingredient(ItemId.Stone, 2),
                new Ingredient(ItemId.Sand, 1)),

            new Recipe("Tas Blok", ItemId.Stone, 1,
                new Ingredient(ItemId.Concrete, 1)),
        };

        public static bool CanCraft(Inventory inv, in Recipe r)
        {
            foreach (var ing in r.Inputs)
                if (inv.CountOf(ing.Id) < ing.Count) return false;
            return true;
        }

        /// <summary>
        /// Uretir. Once girdileri kontrol edip sonra tuketiyoruz - tek tek
        /// tuketip ortada kalirsak oyuncunun esyasi bosa gider.
        /// </summary>
        public static bool Craft(Inventory inv, in Recipe r)
        {
            if (!CanCraft(inv, r)) return false;

            foreach (var ing in r.Inputs)
                inv.Consume(ing.Id, ing.Count);

            // "Usta Marangoz" yalnizca odun tabanli tarifleri etkiliyor:
            // her tarife bonus vermek betonu da ucuzlatir ve dallar arasi
            // secim anlamsizlasir.
            int bonus = 0;
            if (r.Output == ItemId.Plank || r.Output == ItemId.Stick)
                bonus = PlayerSkills.Instance?.State.ExtraCraftOutput ?? 0;

            int leftover = inv.Add(r.Output, r.OutputCount + bonus);
            return leftover == 0;
        }
    }
}
