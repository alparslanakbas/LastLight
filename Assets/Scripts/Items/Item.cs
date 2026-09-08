using LastLight.Voxel;

namespace LastLight.Items
{
    /// <summary>
    /// Envanterde durabilen her sey. Bloklarla ayni numaralari kullanmiyor:
    /// her esya blok degil (odun cubugu, mesale yakiti), her blok da elde
    /// tutulmuyor. Ayirmak ilerde alet/silah eklerken donusum gerektirmiyor.
    /// </summary>
    public enum ItemId : byte
    {
        None = 0,
        Dirt, Stone, Wood, Metal, Concrete, Grass, Sand, Snow, Ash, Waste, Leaves,
        Stick,      // yapraktan/odundan
        Plank,      // islenmis odun
        Torch,      // isik kaynagi
        Fuel,       // yakit
    }

    public readonly struct ItemStack
    {
        public const int MaxStack = 64;

        public readonly ItemId Id;
        public readonly int Count;

        public bool IsEmpty => Id == ItemId.None || Count <= 0;

        public ItemStack(ItemId id, int count)
        {
            Id = count > 0 ? id : ItemId.None;
            Count = count > 0 ? count : 0;
        }

        public static readonly ItemStack Empty = new(ItemId.None, 0);

        public ItemStack With(int count) => new(Id, count);
    }

    public static class ItemDatabase
    {
        /// <summary>
        /// Blok kirildiginda ne dusuyor. Cogu blok kendini dusuruyor; yaprak
        /// dusurmuyor (cop envanteri doldurmasin), cimen toprak dusuruyor.
        /// </summary>
        public static ItemId DropFor(BlockId block) => block switch
        {
            BlockId.Dirt => ItemId.Dirt,
            BlockId.Grass => ItemId.Dirt,
            BlockId.Stone => ItemId.Stone,
            BlockId.Wood => ItemId.Wood,
            BlockId.Metal => ItemId.Metal,
            BlockId.Concrete => ItemId.Concrete,
            BlockId.Road => ItemId.Concrete,
            BlockId.Sand => ItemId.Sand,
            BlockId.Snow => ItemId.Snow,
            BlockId.Ash => ItemId.Ash,
            BlockId.Waste => ItemId.Waste,
            BlockId.Leaves => ItemId.None,
            _ => ItemId.None,
        };

        /// <summary>Bu esya yerlestirilebiliyorsa hangi blok olur.</summary>
        public static BlockId BlockFor(ItemId item) => item switch
        {
            ItemId.Dirt => BlockId.Dirt,
            ItemId.Stone => BlockId.Stone,
            ItemId.Wood => BlockId.Wood,
            ItemId.Metal => BlockId.Metal,
            ItemId.Concrete => BlockId.Concrete,
            ItemId.Sand => BlockId.Sand,
            ItemId.Snow => BlockId.Snow,
            ItemId.Ash => BlockId.Ash,
            ItemId.Waste => BlockId.Waste,
            ItemId.Plank => BlockId.Wood,
            _ => BlockId.Air,
        };

        public static bool IsPlaceable(ItemId item) => BlockFor(item) != BlockId.Air;

        public static string DisplayName(ItemId item) => item switch
        {
            ItemId.None => "-",
            ItemId.Dirt => "Toprak",
            ItemId.Stone => "Tas",
            ItemId.Wood => "Odun",
            ItemId.Metal => "Metal",
            ItemId.Concrete => "Beton",
            ItemId.Grass => "Cimen",
            ItemId.Sand => "Kum",
            ItemId.Snow => "Kar",
            ItemId.Ash => "Kul",
            ItemId.Waste => "Corak Toprak",
            ItemId.Leaves => "Yaprak",
            ItemId.Stick => "Cubuk",
            ItemId.Plank => "Tahta",
            ItemId.Torch => "Mesale",
            ItemId.Fuel => "Yakit",
            _ => item.ToString(),
        };
    }
}
