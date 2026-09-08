using LastLight.Items;
using LastLight.Skills;
using LastLight.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace LastLight.UI
{
    /// <summary>
    /// Tek Tab ekrani: solda aktif sekmenin icerigi, sagda her zaman gorunen
    /// envanter. Onceki dagitik OnGUI panellerinin (uretim ayri, beceri ayri,
    /// hotbar ayri) yerini aliyor - oyuncu tek yerden her seye ulassin diye.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class GameMenuController : MonoBehaviour
    {
        VisualElement _overlay, _grid, _hotbar, _hud;
        ScrollView _recipeList, _skillList, _characterInfo;
        Label _clockLabel, _phaseLabel, _slotCountLabel, _skillPointsLabel;
        Button _tabCrafting, _tabSkills, _tabCharacter;
        VisualElement _craftingPage, _skillsPage, _characterPage;

        PlayerInventory _inventory;
        PlayerSkills _skills;

        bool _open;
        int _activeTab;
        int _lastSlotCount = -1;

        void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null) return;

            _overlay = root.Q<VisualElement>("menuOverlay");
            _grid = root.Q<VisualElement>("inventoryGrid");
            _hotbar = root.Q<VisualElement>("hotbar");
            _hud = root.Q<VisualElement>("hud");
            _recipeList = root.Q<ScrollView>("recipeList");
            _skillList = root.Q<ScrollView>("skillList");
            _characterInfo = root.Q<ScrollView>("characterInfo");
            _clockLabel = root.Q<Label>("clockLabel");
            _phaseLabel = root.Q<Label>("phaseLabel");
            _slotCountLabel = root.Q<Label>("slotCount");
            _skillPointsLabel = root.Q<Label>("skillPoints");

            _craftingPage = root.Q<VisualElement>("craftingPage");
            _skillsPage = root.Q<VisualElement>("skillsPage");
            _characterPage = root.Q<VisualElement>("characterPage");

            _tabCrafting = root.Q<Button>("tabCrafting");
            _tabSkills = root.Q<Button>("tabSkills");
            _tabCharacter = root.Q<Button>("tabCharacter");

            if (_tabCrafting != null) _tabCrafting.clicked += () => SetTab(0);
            if (_tabSkills != null) _tabSkills.clicked += () => SetTab(1);
            if (_tabCharacter != null) _tabCharacter.clicked += () => SetTab(2);

            _inventory = FindAnyObjectByType<PlayerInventory>();
            _skills = FindAnyObjectByType<PlayerSkills>();

            if (_inventory != null) _inventory.Inventory.Changed += Refresh;

            Refresh();
        }

        void OnDisable()
        {
            if (_inventory != null) _inventory.Inventory.Changed -= Refresh;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.tabKey.wasPressedThisFrame) Toggle();

            UpdateClock();

            // Kapasite perk'le buyuyebiliyor; slot sayisi degisirse grid yeniden kurulur.
            if (_open && _inventory != null && _inventory.Inventory.SlotCount != _lastSlotCount)
                Refresh();
        }

        void Toggle()
        {
            _open = !_open;
            if (_overlay != null) _overlay.EnableInClassList("menu-overlay--open", _open);
            // Menu acikken saat kutusu sekme ipucunun uzerine biniyordu.
            if (_hud != null) _hud.EnableInClassList("hud--hidden", _open);

            // Ekran acikken imlec serbest, kapaninca tekrar kilitli.
            // Tam nitelikli: UnityEngine.UIElements.Cursor ile ad cakismasi var.
            UnityEngine.Cursor.lockState = _open ? CursorLockMode.None : CursorLockMode.Locked;
            UnityEngine.Cursor.visible = _open;

            if (_open) Refresh();
        }

        void SetTab(int index)
        {
            _activeTab = index;

            _tabCrafting.EnableInClassList("tab-button--active", index == 0);
            _tabSkills.EnableInClassList("tab-button--active", index == 1);
            _tabCharacter.EnableInClassList("tab-button--active", index == 2);

            _craftingPage.EnableInClassList("page--hidden", index != 0);
            _skillsPage.EnableInClassList("page--hidden", index != 1);
            _characterPage.EnableInClassList("page--hidden", index != 2);

            Refresh();
        }

        // ---------- Yenileme ----------

        void Refresh()
        {
            if (_inventory == null) return;

            BuildInventory();
            BuildHotbar();

            if (_activeTab == 0) BuildRecipes();
            else if (_activeTab == 1) BuildSkills();
            else BuildCharacter();
        }

        void UpdateClock()
        {
            var cycle = DayNightCycle.Instance;
            if (cycle == null || _clockLabel == null) return;

            _clockLabel.text = "Gun " + cycle.DayNumber + "   " + cycle.ClockText();
            _phaseLabel.text = cycle.IsNight
                ? "GECE - isik yak"
                : "Gunduz  (gecilen gece: " + cycle.NightsSurvived + ")";
            _phaseLabel.EnableInClassList("clock-phase--night", cycle.IsNight);
            _phaseLabel.EnableInClassList("clock-phase--day", !cycle.IsNight);
        }

        // ---------- Envanter ----------

        void BuildInventory()
        {
            if (_grid == null) return;

            var inv = _inventory.Inventory;
            _lastSlotCount = inv.SlotCount;
            _grid.Clear();

            int dolu = 0;
            for (int i = 0; i < inv.SlotCount; i++)
            {
                if (!inv[i].IsEmpty) dolu++;
                bool secili = i == inv.SelectedIndex && i < Inventory.HotbarSize;
                _grid.Add(MakeSlot(inv[i], i, "slot", secili));
            }

            if (_slotCountLabel != null)
                _slotCountLabel.text = dolu + "/" + inv.SlotCount;
        }

        void BuildHotbar()
        {
            if (_hotbar == null) return;

            var inv = _inventory.Inventory;
            _hotbar.Clear();

            for (int i = 0; i < Inventory.HotbarSize; i++)
            {
                var slot = MakeSlot(inv[i], i, "hotbar-slot", i == inv.SelectedIndex);
                var index = new Label((i + 1).ToString());
                index.AddToClassList("hotbar-index");
                slot.Add(index);
                _hotbar.Add(slot);
            }
        }

        VisualElement MakeSlot(ItemStack stack, int index, string baseClass, bool selected)
        {
            var slot = new VisualElement();
            slot.AddToClassList(baseClass);
            if (selected) slot.AddToClassList(baseClass + "--selected");

            if (!stack.IsEmpty)
            {
                var fill = new VisualElement();
                fill.AddToClassList("slot-fill");
                // Renk esyaya gore degistigi icin dogrudan atanıyor; her esya
                // tipi icin ayri USS sinifi yazmak 15 kural demekti.
                fill.style.backgroundColor = PlayerInventory.ColorFor(stack.Id);
                slot.Add(fill);

                var name = new Label(ItemDatabase.DisplayName(stack.Id));
                name.AddToClassList("slot-name");
                slot.Add(name);

                var count = new Label(stack.Count.ToString());
                count.AddToClassList("slot-count");
                slot.Add(count);
            }

            int captured = index;
            slot.RegisterCallback<ClickEvent>(evt =>
            {
                if (captured < Inventory.HotbarSize)
                {
                    _inventory.Inventory.Select(captured);
                    Refresh();
                }
            });

            return slot;
        }

        // ---------- Uretim ----------

        void BuildRecipes()
        {
            if (_recipeList == null) return;

            _recipeList.Clear();
            var inv = _inventory.Inventory;

            foreach (var recipe in RecipeDatabase.All)
            {
                bool can = RecipeDatabase.CanCraft(inv, recipe);

                var row = new VisualElement();
                row.AddToClassList("row");
                row.AddToClassList(can ? "row--ready" : "row--locked");

                var body = new VisualElement();
                body.AddToClassList("row-body");

                var title = new Label(recipe.Name + "  x" + recipe.OutputCount);
                title.AddToClassList("row-title");
                body.Add(title);

                string need = "";
                foreach (var ing in recipe.Inputs)
                    need += ItemDatabase.DisplayName(ing.Id) + " " + inv.CountOf(ing.Id) + "/" + ing.Count + "    ";

                var sub = new Label(need);
                sub.AddToClassList("row-sub");
                sub.AddToClassList(can ? "row-sub--ok" : "row-sub--bad");
                body.Add(sub);

                row.Add(body);

                if (can)
                {
                    var captured = recipe;
                    var btn = new Button(() => { RecipeDatabase.Craft(inv, captured); Refresh(); });
                    btn.text = "Uret";
                    btn.AddToClassList("row-action");
                    row.Add(btn);
                }
                else
                {
                    var tag = new Label("eksik");
                    tag.AddToClassList("row-tag");
                    row.Add(tag);
                }

                _recipeList.Add(row);
            }
        }

        // ---------- Beceriler ----------

        void BuildSkills()
        {
            if (_skillList == null || _skills == null) return;

            _skillList.Clear();
            var state = _skills.State;

            if (_skillPointsLabel != null)
                _skillPointsLabel.text = "Puan: " + state.AvailablePoints;

            foreach (SkillBranch branch in System.Enum.GetValues(typeof(SkillBranch)))
            {
                var header = new Label(branch.ToString().ToUpperInvariant());
                header.AddToClassList("branch-title");
                _skillList.Add(header);

                foreach (var perk in PerkDatabase.All)
                {
                    if (perk.Branch != branch) continue;

                    bool owned = state.Has(perk.Id);
                    bool affordable = state.AvailablePoints >= perk.Cost;

                    var row = new VisualElement();
                    row.AddToClassList("row");
                    row.AddToClassList(owned ? "row--owned" : affordable ? "row--ready" : "row--locked");

                    var body = new VisualElement();
                    body.AddToClassList("row-body");

                    var title = new Label(perk.Name);
                    title.AddToClassList("row-title");
                    body.Add(title);

                    var desc = new Label(perk.Description);
                    desc.AddToClassList("row-sub");
                    body.Add(desc);

                    row.Add(body);

                    if (owned)
                    {
                        var tag = new Label("ACIK");
                        tag.AddToClassList("row-tag");
                        row.Add(tag);
                    }
                    else if (affordable)
                    {
                        var captured = perk.Id;
                        var btn = new Button(() => { state.Unlock(captured); Refresh(); });
                        btn.text = "Ac (" + perk.Cost + ")";
                        btn.AddToClassList("row-action");
                        row.Add(btn);
                    }
                    else
                    {
                        var tag = new Label(perk.Cost + " puan");
                        tag.AddToClassList("row-tag");
                        row.Add(tag);
                    }

                    _skillList.Add(row);
                }
            }
        }

        // ---------- Karakter ----------

        void BuildCharacter()
        {
            if (_characterInfo == null) return;

            _characterInfo.Clear();
            var cycle = DayNightCycle.Instance;
            var state = _skills != null ? _skills.State : null;

            AddInfo("Gun", cycle != null ? cycle.DayNumber.ToString() : "-");
            AddInfo("Hayatta kalinan gece", cycle != null ? cycle.NightsSurvived.ToString() : "-");
            AddInfo("Beceri puani", state != null ? state.AvailablePoints + " / " + state.TotalPoints : "-");
            AddInfo("Envanter kapasitesi", _inventory.Inventory.SlotCount.ToString());
            AddInfo("Kosma carpani", state != null ? state.SprintMultiplier.ToString("0.00") : "-");
            AddInfo("Yakit suresi carpani", state != null ? state.FuelDurationMultiplier.ToString("0.00") : "-");
            AddInfo("Ek kaynak sansi", state != null ? "%" + (state.ExtraDropChance * 100f).ToString("0") : "-");
        }

        void AddInfo(string label, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("row");

            var body = new VisualElement();
            body.AddToClassList("row-body");

            var title = new Label(label);
            title.AddToClassList("row-title");
            body.Add(title);

            row.Add(body);

            var val = new Label(value);
            val.AddToClassList("row-tag");
            row.Add(val);

            _characterInfo.Add(row);
        }
    }
}
