using LastLight.Enemies;
using LastLight.Items;
using LastLight.Skills;
using LastLight.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace LastLight.UI
{
    /// <summary>
    /// Tek Tab ekrani. Yedi sekmenin tamami ayni iki sutunlu kalibi kullaniyor:
    /// solda liste, sagda ustte secili ogenin detayi ve altta envanter.
    /// Kalip tek yerde durdugu icin yeni sekme eklemek bir metot yazmak kadar.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class GameMenuController : MonoBehaviour
    {
        enum Tab { Crafting, Character, Map, Skills, Quests, Challenges, Players }

        VisualElement _overlay, _grid, _hotbar, _hud;
        ScrollView _leftList, _detailList;
        Label _clockLabel, _phaseLabel, _slotCountLabel;
        Label _healthLabel, _threatLabel;
        VisualElement _healthFill;
        Label _menuTitle, _leftTitle, _leftBadge, _leftNote, _detailTitle;

        readonly Button[] _tabButtons = new Button[7];

        PlayerInventory _inventory;
        PlayerSkills _skills;

        bool _open;
        Tab _activeTab = Tab.Crafting;
        int _lastSlotCount = -1;

        void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null) return;

            _overlay = root.Q<VisualElement>("menuOverlay");
            _grid = root.Q<VisualElement>("inventoryGrid");
            _hotbar = root.Q<VisualElement>("hotbar");
            _hud = root.Q<VisualElement>("hud");

            _leftList = root.Q<ScrollView>("leftList");
            _detailList = root.Q<ScrollView>("detailList");

            _healthFill = root.Q<VisualElement>("healthFill");
            _healthLabel = root.Q<Label>("healthLabel");
            _threatLabel = root.Q<Label>("threatLabel");

            _clockLabel = root.Q<Label>("clockLabel");
            _phaseLabel = root.Q<Label>("phaseLabel");
            _slotCountLabel = root.Q<Label>("slotCount");
            _menuTitle = root.Q<Label>("menuTitle");
            _leftTitle = root.Q<Label>("leftTitle");
            _leftBadge = root.Q<Label>("leftBadge");
            _leftNote = root.Q<Label>("leftNote");
            _detailTitle = root.Q<Label>("detailTitle");

            _tabButtons[0] = root.Q<Button>("tabCrafting");
            _tabButtons[1] = root.Q<Button>("tabCharacter");
            _tabButtons[2] = root.Q<Button>("tabMap");
            _tabButtons[3] = root.Q<Button>("tabSkills");
            _tabButtons[4] = root.Q<Button>("tabQuests");
            _tabButtons[5] = root.Q<Button>("tabChallenges");
            _tabButtons[6] = root.Q<Button>("tabPlayers");

            for (int i = 0; i < _tabButtons.Length; i++)
            {
                if (_tabButtons[i] == null) continue;
                var captured = (Tab)i;
                _tabButtons[i].clicked += () => SetTab(captured);
            }

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
            if (kb == null) return;

            if (kb.tabKey.wasPressedThisFrame) Toggle();
            if (_open && kb.escapeKey.wasPressedThisFrame) Toggle();

            UpdateClock();
            UpdateVitals();

            // Kapasite perk'le buyuyebiliyor; slot sayisi degisirse grid yeniden kurulur.
            if (_open && _inventory != null && _inventory.Inventory.SlotCount != _lastSlotCount)
                Refresh();
        }

        void Toggle()
        {
            _open = !_open;
            if (_overlay != null) _overlay.EnableInClassList("menu-overlay--open", _open);
            // Menu acikken saat kutusu sekme cubuguyla ust uste biniyordu.
            if (_hud != null) _hud.EnableInClassList("hud--hidden", _open);

            // Tam nitelikli: UnityEngine.UIElements.Cursor ile ad cakismasi var.
            UnityEngine.Cursor.lockState = _open ? CursorLockMode.None : CursorLockMode.Locked;
            UnityEngine.Cursor.visible = _open;

            if (_open) Refresh();
        }

        void SetTab(Tab tab)
        {
            _activeTab = tab;
            for (int i = 0; i < _tabButtons.Length; i++)
                if (_tabButtons[i] != null)
                    _tabButtons[i].EnableInClassList("tab-button--active", i == (int)tab);
            Refresh();
        }

        // ---------- Yenileme ----------

        void Refresh()
        {
            if (_inventory == null) return;

            BuildInventory();
            BuildHotbar();

            _leftList.Clear();
            _detailList.Clear();
            _leftBadge.text = "";
            _leftNote.text = "";

            switch (_activeTab)
            {
                case Tab.Crafting: BuildCrafting(); break;
                case Tab.Character: BuildCharacter(); break;
                case Tab.Map: BuildMap(); break;
                case Tab.Skills: BuildSkills(); break;
                case Tab.Quests: BuildQuests(); break;
                case Tab.Challenges: BuildChallenges(); break;
                case Tab.Players: BuildPlayers(); break;
            }
        }

        void SetHeader(string menuTitle, string leftTitle, string detailTitle)
        {
            if (_menuTitle != null) _menuTitle.text = menuTitle;
            if (_leftTitle != null) _leftTitle.text = leftTitle;
            if (_detailTitle != null) _detailTitle.text = detailTitle;
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

        // ---------- Sekme: Uretim ----------

        void BuildCrafting()
        {
            SetHeader("URETIM", "TARIFLER", "INCELE");
            var inv = _inventory.Inventory;

            foreach (var recipe in RecipeDatabase.All)
            {
                bool can = RecipeDatabase.CanCraft(inv, recipe);

                string need = "";
                foreach (var ing in recipe.Inputs)
                    need += ItemDatabase.DisplayName(ing.Id) + " " + inv.CountOf(ing.Id) + "/" + ing.Count + "    ";

                var captured = recipe;
                var row = MakeRow(
                    recipe.Name + "  x" + recipe.OutputCount,
                    need,
                    can ? "row--ready" : "row--locked",
                    can ? "row-sub--ok" : "row-sub--bad",
                    can ? "Uret" : null,
                    () => { RecipeDatabase.Craft(inv, captured); Refresh(); },
                    can ? null : "eksik");

                row.RegisterCallback<ClickEvent>(evt => ShowRecipeDetail(captured));
                _leftList.Add(row);
            }

            AddDetailText("Bir tarife tikla, ayrintisi burada gorunsun.");
        }

        void ShowRecipeDetail(Recipe recipe)
        {
            _detailList.Clear();
            var inv = _inventory.Inventory;

            AddDetailText(recipe.Name + "  x" + recipe.OutputCount);
            foreach (var ing in recipe.Inputs)
                AddDetailNote("- " + ItemDatabase.DisplayName(ing.Id) + ": elinde " +
                              inv.CountOf(ing.Id) + ", gereken " + ing.Count);
        }

        // ---------- Sekme: Karakter ----------

        void BuildCharacter()
        {
            SetHeader("KARAKTER", "DURUM", "AYRINTI");
            var cycle = DayNightCycle.Instance;
            var state = _skills != null ? _skills.State : null;

            AddInfoRow("Gun", cycle != null ? cycle.DayNumber.ToString() : "-");
            AddInfoRow("Hayatta kalinan gece", cycle != null ? cycle.NightsSurvived.ToString() : "-");
            AddInfoRow("Beceri puani", state != null ? state.AvailablePoints + " / " + state.TotalPoints : "-");
            AddInfoRow("Envanter kapasitesi", _inventory.Inventory.SlotCount.ToString());
            AddInfoRow("Kosma carpani", state != null ? state.SprintMultiplier.ToString("0.00") : "-");
            AddInfoRow("Yakit suresi carpani", state != null ? state.FuelDurationMultiplier.ToString("0.00") : "-");
            AddInfoRow("Ek kaynak sansi", state != null ? "%" + (state.ExtraDropChance * 100f).ToString("0") : "-");

            AddDetailText("Zirh ve durum etkileri");
            AddDetailNote("Ekipman ve durum etkisi sistemleri henuz yazilmadi. " +
                          "Bu bolum, zirh slotlari ve etkiler eklendiginde dolacak.");
        }

        // ---------- Sekme: Harita ----------

        void BuildMap()
        {
            SetHeader("HARITA", "YOL NOKTALARI", "KONUM");
            AddEmptyNote("Harita henuz yok. Dunya voxel oldugu icin ustten gorunum " +
                         "uretilebilir - blok yuzey renklerinden gercek bir harita cikar.");

            var player = GameObject.Find("Player");
            if (player != null)
            {
                var p = player.transform.position;
                AddDetailText("Konum");
                AddDetailNote("X " + Mathf.RoundToInt(p.x) + "   Y " + Mathf.RoundToInt(p.y) +
                              "   Z " + Mathf.RoundToInt(p.z));
            }
        }

        // ---------- Sekme: Beceriler ----------

        void BuildSkills()
        {
            SetHeader("BECERILER", "BECERILER", "AYRINTI");
            if (_skills == null) return;

            var state = _skills.State;
            _leftBadge.text = "Kullanilabilir Puan: " + state.AvailablePoints;
            _leftNote.text = "Puan her hayatta kalinan geceden gelir.";

            foreach (SkillBranch branch in System.Enum.GetValues(typeof(SkillBranch)))
            {
                var header = new Label(branch.ToString().ToUpperInvariant());
                header.AddToClassList("branch-title");
                _leftList.Add(header);

                foreach (var perk in PerkDatabase.All)
                {
                    if (perk.Branch != branch) continue;

                    bool owned = state.Has(perk.Id);
                    bool affordable = state.AvailablePoints >= perk.Cost;
                    var captured = perk;

                    var row = MakeRow(
                        perk.Name,
                        perk.Description,
                        owned ? "row--owned" : affordable ? "row--ready" : "row--locked",
                        null,
                        owned ? null : affordable ? "Ac (" + perk.Cost + ")" : null,
                        () => { state.Unlock(captured.Id); Refresh(); },
                        owned ? "ACIK" : affordable ? null : perk.Cost + " puan");

                    row.RegisterCallback<ClickEvent>(evt =>
                    {
                        _detailList.Clear();
                        AddDetailText(captured.Name);
                        AddDetailNote(captured.Description);
                        AddDetailNote("Dal: " + captured.Branch + "   Maliyet: " + captured.Cost + " puan");
                    });

                    _leftList.Add(row);
                }
            }

            AddDetailText("Bir beceriye tikla, ayrintisi burada gorunsun.");
        }

        // ---------- Sekme: Gorevler ----------

        void BuildQuests()
        {
            SetHeader("GOREVLER", "GOREVLER", "GOREV");
            AddEmptyNote("Gorev sistemi henuz yok. Once tuccar/hedef altyapisi yazilmali; " +
                         "bu ekran onun uzerine oturacak.");
        }

        // ---------- Sekme: Mucadeleler ----------

        void BuildChallenges()
        {
            SetHeader("MUCADELELER", "MUCADELELER", "MUCADELE");
            AddEmptyNote("Mucadele (basarim) sistemi henuz yok. Sayac tabanli oldugu icin " +
                         "ucuz eklenir: ilk geceyi gec, 100 blok kir, ilk mesaleyi uret.");
        }

        // ---------- Sekme: Oyuncu ----------

        void BuildPlayers()
        {
            SetHeader("OYUNCU", "ISTATISTIK", "AYRINTI");
            var cycle = DayNightCycle.Instance;

            AddInfoRow("Oyuncu", "Tek oyunculu");
            AddInfoRow("Gun", cycle != null ? cycle.DayNumber.ToString() : "-");
            AddInfoRow("Gecilen gece", cycle != null ? cycle.NightsSurvived.ToString() : "-");

            AddDetailText("Cok oyunculu");
            AddDetailNote("Tasarim dokumaninda kapsam disinda birakildi: tek kisilik yapimda " +
                          "en cok zaman yiyen sistem. Bu sekme tek oyuncu istatistiklerini gosteriyor.");
        }

        // ---------- Ortak parcalar ----------

        VisualElement MakeRow(string title, string sub, string rowClass, string subClass,
                              string actionText, System.Action action, string tagText)
        {
            var row = new VisualElement();
            row.AddToClassList("row");
            if (rowClass != null) row.AddToClassList(rowClass);

            var body = new VisualElement();
            body.AddToClassList("row-body");

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("row-title");
            body.Add(titleLabel);

            if (!string.IsNullOrEmpty(sub))
            {
                var subLabel = new Label(sub);
                subLabel.AddToClassList("row-sub");
                if (subClass != null) subLabel.AddToClassList(subClass);
                body.Add(subLabel);
            }

            row.Add(body);

            if (actionText != null)
            {
                var btn = new Button(action);
                btn.text = actionText;
                btn.AddToClassList("row-action");
                row.Add(btn);
            }
            else if (tagText != null)
            {
                var tag = new Label(tagText);
                tag.AddToClassList("row-tag");
                row.Add(tag);
            }

            return row;
        }

        void AddInfoRow(string label, string value)
        {
            _leftList.Add(MakeRow(label, null, null, null, null, null, value));
        }

        void AddDetailText(string text)
        {
            var l = new Label(text);
            l.AddToClassList("detail-text");
            _detailList.Add(l);
        }

        void AddDetailNote(string text)
        {
            var l = new Label(text);
            l.AddToClassList("detail-note");
            _detailList.Add(l);
        }

        void AddEmptyNote(string text)
        {
            var l = new Label(text);
            l.AddToClassList("empty-note");
            _leftList.Add(l);
        }

        void UpdateVitals()
        {
            var hp = PlayerHealth.Instance;
            if (hp != null && _healthFill != null)
            {
                float ratio = hp.Max > 0f ? hp.Current / hp.Max : 0f;
                _healthFill.style.width = Length.Percent(ratio * 100f);
                _healthLabel.text = Mathf.CeilToInt(hp.Current) + " / " + Mathf.CeilToInt(hp.Max);
            }

            // Yakinlardaki dusman sayisi: oyuncunun gece ne kadar baski
            // altinda oldugunu gormesi gerekiyor.
            if (_threatLabel != null)
                _threatLabel.text = Enemy.AliveCount > 0 ? "TEHDIT: " + Enemy.AliveCount : "";
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
                // Renk esyaya bagli veri; her tip icin ayri USS sinifi yazmak
                // 15 kural demekti.
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
    }
}
