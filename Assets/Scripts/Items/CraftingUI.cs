using UnityEngine;
using UnityEngine.InputSystem;

namespace LastLight.Items
{
    /// <summary>
    /// Tab ile acilan basit uretim listesi. Uretilebilen tarifler vurgulu,
    /// eksik girdiler kirmizi gosteriliyor - oyuncunun neyi eksik oldugunu
    /// gormesi, tarifin neden calismadigini tahmin etmesinden iyi.
    /// </summary>
    public sealed class CraftingUI : MonoBehaviour
    {
        [SerializeField] PlayerInventory inventory;

        bool _open;

        void Awake()
        {
            if (inventory == null) inventory = GetComponent<PlayerInventory>();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.tabKey.wasPressedThisFrame)
            {
                _open = !_open;
                // Uretim ekrani acikken fareyi birakiyoruz, yoksa listeye
                // tiklanamiyor.
                Cursor.lockState = _open ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = _open;
            }
        }

        void OnGUI()
        {
            if (!_open || inventory == null) return;

            var inv = inventory.Inventory;
            const int w = 340, rowH = 46;
            int h = 44 + RecipeDatabase.All.Count * rowH;
            var panel = new Rect(24, 70, w, h);

            GUI.color = new Color(0f, 0f, 0f, 0.82f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(panel.x + 12, panel.y + 8, w, 22), "URETIM  (Tab ile kapat)");

            for (int i = 0; i < RecipeDatabase.All.Count; i++)
            {
                var r = RecipeDatabase.All[i];
                bool can = RecipeDatabase.CanCraft(inv, r);
                var row = new Rect(panel.x + 10, panel.y + 36 + i * rowH, w - 20, rowH - 6);

                GUI.color = can ? new Color(0.22f, 0.34f, 0.22f, 0.9f) : new Color(0.18f, 0.18f, 0.18f, 0.9f);
                GUI.DrawTexture(row, Texture2D.whiteTexture);

                GUI.color = Color.white;
                GUI.Label(new Rect(row.x + 8, row.y + 3, 200, 20), $"{r.Name} x{r.OutputCount}");

                // Girdiler: sahip olunan / gereken
                string need = "";
                foreach (var ing in r.Inputs)
                {
                    int have = inv.CountOf(ing.Id);
                    need += $"{ItemDatabase.DisplayName(ing.Id)} {have}/{ing.Count}   ";
                }
                GUI.color = can ? new Color(0.75f, 0.9f, 0.75f) : new Color(0.9f, 0.6f, 0.6f);
                GUI.Label(new Rect(row.x + 8, row.y + 22, w - 40, 20), need);

                GUI.color = Color.white;
                if (can && GUI.Button(new Rect(row.xMax - 66, row.y + 8, 58, 24), "Uret"))
                    RecipeDatabase.Craft(inv, r);
            }

            GUI.color = Color.white;
        }
    }
}
