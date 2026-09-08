using LastLight.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastLight.Skills
{
    /// <summary>
    /// Beceri durumunu tasir ve K tusuyla acilan sayfayi cizer.
    /// Puan gecilen gece sayisindan geliyor; sayfa acikken imlec birakiliyor.
    /// </summary>
    public sealed class PlayerSkills : MonoBehaviour
    {
        public SkillState State { get; } = new();

        public static PlayerSkills Instance { get; private set; }

        bool _open;

        void Awake() => Instance = this;

        void Update()
        {
            var cycle = DayNightCycle.Instance;
            if (cycle != null) State.SyncPoints(cycle.NightsSurvived);

            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.kKey.wasPressedThisFrame)
            {
                _open = !_open;
                Cursor.lockState = _open ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = _open;
            }
        }

        void OnGUI()
        {
            if (!_open) return;

            const int w = 430;
            var panel = new Rect(Screen.width / 2 - w / 2, 60, w, 430);

            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(panel.x + 14, panel.y + 10, w, 22),
                      $"BECERILER   (K ile kapat)    Puan: {State.AvailablePoints}");
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            GUI.Label(new Rect(panel.x + 14, panel.y + 30, w - 20, 20),
                      "Puan her hayatta kalinan geceden gelir.");
            GUI.color = Color.white;

            float y = panel.y + 56;

            foreach (SkillBranch branch in System.Enum.GetValues(typeof(SkillBranch)))
            {
                GUI.color = new Color(0.55f, 0.75f, 0.95f);
                GUI.Label(new Rect(panel.x + 14, y, w, 20), branch.ToString().ToUpperInvariant());
                GUI.color = Color.white;
                y += 22;

                foreach (var perk in PerkDatabase.All)
                {
                    if (perk.Branch != branch) continue;

                    bool owned = State.Has(perk.Id);
                    bool affordable = State.AvailablePoints >= perk.Cost;
                    var row = new Rect(panel.x + 14, y, w - 28, 42);

                    GUI.color = owned ? new Color(0.20f, 0.36f, 0.22f, 0.95f)
                              : affordable ? new Color(0.22f, 0.22f, 0.26f, 0.95f)
                                           : new Color(0.15f, 0.15f, 0.15f, 0.95f);
                    GUI.DrawTexture(row, Texture2D.whiteTexture);

                    GUI.color = Color.white;
                    GUI.Label(new Rect(row.x + 8, row.y + 2, 260, 20), perk.Name);
                    GUI.color = new Color(0.75f, 0.75f, 0.75f);
                    GUI.Label(new Rect(row.x + 8, row.y + 20, 300, 20), perk.Description);
                    GUI.color = Color.white;

                    var btn = new Rect(row.xMax - 78, row.y + 8, 70, 26);
                    if (owned)
                    {
                        GUI.color = new Color(0.6f, 0.9f, 0.6f);
                        GUI.Label(new Rect(btn.x + 10, btn.y + 3, 70, 20), "ACIK");
                        GUI.color = Color.white;
                    }
                    else if (affordable && GUI.Button(btn, $"Ac ({perk.Cost})"))
                    {
                        State.Unlock(perk.Id);
                    }
                    else if (!affordable)
                    {
                        GUI.color = new Color(0.6f, 0.6f, 0.6f);
                        GUI.Label(new Rect(btn.x + 4, btn.y + 3, 80, 20), $"{perk.Cost} puan");
                        GUI.color = Color.white;
                    }

                    y += 46;
                }
                y += 6;
            }
        }
    }
}
