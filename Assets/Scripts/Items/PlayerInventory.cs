using LastLight.Skills;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastLight.Items
{
    /// <summary>
    /// Envanteri tasiyan bilesen ve hotbar cizimi. UI simdilik OnGUI ile:
    /// UI Toolkit/uGUI kurulumu bu asamada oyunun geri kalanindan buyuk bir
    /// yatirim olurdu, oysa hotbar'in dogru calistigini gormek icin bu yeterli.
    /// </summary>
    public sealed class PlayerInventory : MonoBehaviour
    {
        public Inventory Inventory { get; } = new();

        void Awake()
        {
            // Baslangic esyasi: oyuncunun ilk dakikada bir sey insa edebilmesi
            // icin. Tasarimda ilk 3 dakika "kontrolleri ogren" bolumu.
            Inventory.Add(ItemId.Wood, 24);
            Inventory.Add(ItemId.Stone, 24);
        }

        void Update()
        {
            // "Yuk Tasiyici" acildiginda kapasite buyur.
            var skills = PlayerSkills.Instance;
            if (skills != null)
            {
                int target = Inventory.BaseSlots + skills.State.ExtraInventorySlots;
                if (Inventory.SlotCount < target) Inventory.Resize(target);
            }

            var kb = Keyboard.current;
            if (kb == null) return;

            // 1-8 tuslari dogrudan slot seciyor.
            for (int i = 0; i < Inventory.HotbarSize; i++)
                if (kb[Key.Digit1 + i].wasPressedThisFrame)
                    Inventory.Select(i);
        }


        /// <summary>Envanter kutusunun rengi. Blok malzemeleriyle ayni palet.</summary>
        public static Color ColorFor(ItemId id) => id switch
        {
            ItemId.Dirt => new Color(0.42f, 0.31f, 0.20f),
            ItemId.Stone => new Color(0.48f, 0.49f, 0.52f),
            ItemId.Wood => new Color(0.56f, 0.38f, 0.21f),
            ItemId.Metal => new Color(0.58f, 0.61f, 0.65f),
            ItemId.Concrete => new Color(0.62f, 0.61f, 0.58f),
            ItemId.Sand => new Color(0.78f, 0.70f, 0.46f),
            ItemId.Snow => new Color(0.90f, 0.93f, 0.96f),
            ItemId.Ash => new Color(0.28f, 0.26f, 0.25f),
            ItemId.Waste => new Color(0.44f, 0.35f, 0.28f),
            ItemId.Leaves => new Color(0.18f, 0.34f, 0.18f),
            ItemId.Plank => new Color(0.66f, 0.48f, 0.28f),
            ItemId.Stick => new Color(0.45f, 0.33f, 0.18f),
            ItemId.Torch => new Color(0.95f, 0.75f, 0.30f),
            ItemId.Fuel => new Color(0.30f, 0.25f, 0.12f),
            _ => new Color(0.2f, 0.2f, 0.2f),
        };
    }
}
