using UnityEngine;
using UnityEngine.UIElements;

namespace LastLight.Settings
{
    /// <summary>
    /// Ayar panelini kod icinde uretir.
    ///
    /// Panel iki yerde gorunuyor: ana menu ve duraklama menusu. UXML'de iki
    /// kez yazmak isaretlemeyi kopyalamak demekti, `ui:Template` ile parca
    /// eklemek de yol bagimliligi getiriyordu. Kodda tek yerde uretmek her
    /// ikisini de coz uyor - panel zaten dinamik (cozunurluk listesi
    /// makineden geliyor).
    /// </summary>
    public static class SettingsPanelBuilder
    {
        public static void BuildInto(VisualElement container)
        {
            GameSettings.Load();
            container.Clear();

            AddSlider(container, "Ses seviyesi", 0f, 1f, GameSettings.Volume,
                v => GameSettings.SetVolume(v),
                v => Mathf.RoundToInt(v * 100f) + "%");

            AddSlider(container, "Fare hassasiyeti",
                GameSettings.MinSensitivity, GameSettings.MaxSensitivity, GameSettings.Sensitivity,
                v => GameSettings.SetSensitivity(v),
                v => v.ToString("0.00"));

            AddSlider(container, "Gorus mesafesi", 0.5f, 1.5f, GameSettings.ViewDistanceScale,
                v => GameSettings.SetViewDistance(v),
                v => Mathf.RoundToInt(v * 100f) + "%");

            AddToggle(container, "Tam ekran", GameSettings.Fullscreen,
                v => GameSettings.SetFullscreen(v));

            AddResolutionField(container);

            var hint = new Label("Ayarlar aninda uygulaniyor ve otomatik kaydediliyor.");
            hint.AddToClassList("settings-hint");
            container.Add(hint);
        }

        static void AddSlider(VisualElement parent, string label, float min, float max,
                              float value, System.Action<float> onChange,
                              System.Func<float, string> format)
        {
            var row = Row(parent, label, out var valueLabel);

            var slider = new Slider(min, max) { value = value };
            slider.AddToClassList("settings-slider");
            valueLabel.text = format(value);

            slider.RegisterValueChangedCallback(e =>
            {
                onChange(e.newValue);
                valueLabel.text = format(e.newValue);
            });

            row.Add(slider);
            row.Add(valueLabel);
        }

        static void AddToggle(VisualElement parent, string label, bool value, System.Action<bool> onChange)
        {
            var row = Row(parent, label, out var valueLabel);

            var toggle = new Toggle { value = value };
            toggle.AddToClassList("settings-toggle");
            valueLabel.text = value ? "Acik" : "Kapali";

            toggle.RegisterValueChangedCallback(e =>
            {
                onChange(e.newValue);
                valueLabel.text = e.newValue ? "Acik" : "Kapali";
                GameSettings.Save();
            });

            row.Add(toggle);
            row.Add(valueLabel);
        }

        static void AddResolutionField(VisualElement parent)
        {
            var row = Row(parent, "Cozunurluk", out var valueLabel);

            var options = new System.Collections.Generic.List<string>();
            var resolutions = Screen.resolutions;

            // Ayni genislik/yukseklik farkli tazeleme hizlariyla birden fazla
            // kez geliyor; oyuncuya "1920 x 1080" secenegini yedi kez
            // gostermenin anlami yok, en yuksek tazelemeyi tutuyoruz.
            var seen = new System.Collections.Generic.Dictionary<string, int>();
            for (int i = 0; i < resolutions.Length; i++)
            {
                string key = resolutions[i].width + " x " + resolutions[i].height;
                seen[key] = i;   // liste artan tazeleme sirali, sonuncu en yuksek
            }
            foreach (var kv in seen) options.Add(kv.Key);

            if (options.Count == 0)
            {
                valueLabel.text = "Editor'de degistirilemez";
                return;
            }

            int current = 0;
            if (GameSettings.ResolutionIndex >= 0)
            {
                int idx = 0;
                foreach (var kv in seen)
                {
                    if (kv.Value == GameSettings.ResolutionIndex) { current = idx; break; }
                    idx++;
                }
            }
            else
            {
                int idx = 0;
                foreach (var kv in seen)
                {
                    if (kv.Key == Screen.width + " x " + Screen.height) { current = idx; break; }
                    idx++;
                }
            }

            var dropdown = new DropdownField(options, current);
            dropdown.AddToClassList("settings-dropdown");

            var indexList = new System.Collections.Generic.List<int>(seen.Values);
            dropdown.RegisterValueChangedCallback(e =>
            {
                int i = options.IndexOf(e.newValue);
                if (i >= 0 && i < indexList.Count) GameSettings.SetResolution(indexList[i]);
                GameSettings.Save();
            });

            row.Add(dropdown);
            valueLabel.text = "";
            row.Add(valueLabel);
        }

        static VisualElement Row(VisualElement parent, string label, out Label valueLabel)
        {
            var row = new VisualElement();
            row.AddToClassList("settings-row");

            var name = new Label(label);
            name.AddToClassList("settings-label");
            row.Add(name);

            valueLabel = new Label();
            valueLabel.AddToClassList("settings-value");

            parent.Add(row);
            return row;
        }
    }
}
